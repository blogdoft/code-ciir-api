# Fase 10 — Reranking em `code-queries`, e reversão de `size`/`page` para `limit`

**Status: concluído.** Portado o subsistema de reranking de `code-rag-api`
(`CodeCiir.Reranking.Abstraction`/`.Ollama`/`.OpenAI`), ligado incondicionalmente em
`CodeQueryService.QueryAsync` (`NoOpReranker` quando desligado). `size`+`page` (Fase 9)
foram revertidos para um único `limit`, já que reranking exige reordenar todo o pool de
candidatos antes de truncar — incompatível com paginação por `OFFSET`. Reranking já vai
ligado por padrão em toda parte (`appsettings.json` **e** `configmap.yaml`, ambos com
`Reranking__Provider=Ollama`/`qwen2.5:7b-instruct` — confirmado pelo usuário como o mesmo
modelo já instalado e em uso pelo `code-rag-api`), com `ServersTransport` de 120s no
Traefik. 135 testes verdes na solução inteira.

## Contexto

O usuário notou que `code-ciir-api` não tinha nenhuma configuração de modelo de reranking —
confirmado: reranking nunca foi implementado aqui, fora de escopo desde a Fase 1
(`.specs/08-ops-deployment.md`). `code-rag-api` já tem reranking real (providers Ollama e
Cohere), usado para reordenar os candidatos da busca vetorial por relevância antes de
truncar para o `limit` pedido.

## Decisões confirmadas com o usuário

1. **Só o provider Ollama** — mesmo host Ollama (`192.168.1.212:11434`) já usado pelos
   embeddings. Sem Cohere (nunca chegou a ser ligado em `code-rag-api` também — stub sem
   `ApiKey` configurada em lugar nenhum).
2. **Mais um provider, compatível com a API da OpenAI** (Chat Completions) — não existe em
   `code-rag-api` para reranking, mas existe um precedente direto para embeddings
   (`CodeRag.Embeddings.OpenAI`), adaptado aqui do formato `/embeddings` para
   `/chat/completions`. Funciona contra a OpenAI real ou qualquer gateway auto-hospedado
   compatível (vLLM, LiteLLM, o próprio endpoint `/v1` do Ollama).
3. **`size`+`page` (Fase 9) voltam a ser um único `limit`.** Reranking precisa reordenar
   **todo** o pool de candidatos do maior match para o menor antes de truncar — só é seguro
   com um único corte. Paginação via `OFFSET` rerankearia cada página isoladamente, podendo
   esconder um match globalmente melhor que caiu numa página seguinte, e quebra a garantia
   de "ordem única do maior para o menor". Motivo documentado no XML doc (OpenAPI) de
   `CodeQueriesController.QueryAsync`.
4. **Reranking já vai ligado por padrão em toda parte** — `Reranking__Provider=Ollama`
   tanto em `appsettings.json` quanto em `.eng/k8s/configmap.yaml`, incluindo o
   `ServersTransport` de timeout maior no Traefik. Diferente de `code-rag-api` (que só liga
   via `configmap.yaml`, deixando `appsettings.json` desligado por padrão), aqui o usuário
   confirmou explicitamente que `qwen2.5:7b-instruct` já está instalado e em uso nesse mesmo
   Ollama compartilhado, então não há razão para manter um default local desligado.

## Subsistema `CodeCiir.Reranking.*` (mirror de `CodeRag.Reranking.*`)

- **`CodeCiir.Reranking.Abstraction`**: `IReranker` (`Provider`, `CandidatePoolSize`,
  `RerankAsync`), `RerankCandidate`/`RerankedCandidate`, `RerankingOptions` (seção
  `"Reranking"`: `Provider`, `Model`, `BaseUrl`, `ApiKey`, `TimeoutSeconds=120`,
  `CandidatePoolSize=25`, `MaxConcurrency=6`), `NoOpReranker` (passthrough quando
  `Provider` vazio/"None" — **não** é erro de configuração, ao contrário de
  `Embeddings:Provider`), `IRerankerProviderFactory`, `RerankerResolver`,
  `RerankingException`, `AddRerankingAbstraction(configuration)`.
- **`CodeCiir.Reranking.Ollama`**: `OllamaReranker` — pointwise, um `POST
  {BaseUrl}/api/generate` por candidato, saída estruturada JSON (`{score: 0-100000}`,
  pedido como inteiro porque JSON Schema não expressa decimal limitado),
  `SemaphoreSlim(MaxConcurrency)`, score normalizado `/100000.0` e arredondado em 5 casas
  (`RerankedCandidate.Score` tem até 5 dígitos decimais de resolução — mais fino que a
  escala 0-10 original, mesmo sem garantia de que o modelo realmente distinga cada um
  desses dígitos). `OllamaRerankerProviderFactory` exige `BaseUrl`.
- **`CodeCiir.Reranking.OpenAI`** (novo desenho, sem equivalente em `code-rag-api` para
  reranking — adaptado de `CodeRag.Embeddings.OpenAI`): `OpenAIReranker` — pointwise, um
  `POST {BaseUrl}/chat/completions` por candidato, `response_format` com JSON Schema
  estrito (`{score: 0-100000}`, mesma normalização/resolução de 5 casas do provider
  Ollama), `BaseUrl` default `https://api.openai.com/v1/`,
  `Authorization: Bearer {ApiKey}` **só** quando `ApiKey` não é vazio (muitos gateways
  compatíveis não exigem chave — diferença deliberada do provider OpenAI de embeddings, que
  sempre exige `ApiKey`). `OpenAIRerankerProviderFactory` exige `Model` (único campo
  realmente obrigatório).

Topologia de referências: `CodeCiir.Application` → `Reranking.Abstraction` apenas;
`CodeCiir.Api` (raiz de composição) → `Abstraction` + `Ollama` + `OpenAI`; `CodeCiir.Mcp`
não precisa de referência nova (usa `IReranker` transitivamente via `ICodeQueryService`).

## Integração em `CodeQueryService`

```csharp
var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxResultLimit);
var searchLimit = Math.Min(Math.Max(effectiveLimit, reranker.CandidatePoolSize), MaxCandidatePoolSize);

var candidates = await codeDocumentsRepository.SearchAsync(..., searchLimit, ct);

var rerankedScores = await reranker.RerankAsync(question, candidates, ct);   // sempre, incondicional

var results = candidates
    .Select(c => c with { RerankScore = scoresById[c.Id] })
    .OrderByDescending(c => c.RerankScore ?? double.NegativeInfinity)        // estável: empates mantêm ordem por similaridade
    .Take(effectiveLimit)
    .ToList();
```

`MaxResultLimit = 50` e `DefaultLimit = 10` voltam aos valores originais (pré-Fase 9);
`MaxCandidatePoolSize = 200` é novo (mesmo teto de `code-rag-api`, mesma lógica defensiva
já usada em `MaxGraphNodes`). `limit` continua sendo *clampado* (não validado com 400) —
comportamento original deste repositório, mantido sem alteração.

O grafo de relações (2-hop) continua só sendo expandido quando `project_id` é informado
(decisão da Fase 9, inalterada) — a partir da página final, já reordenada por reranking.

## Contrato

`CodeQueryResultResponse` ganha `rerank_score` (`double?`, `null` quando reranking está
desligado ou o candidato não foi pontuado). `CodeQueryRequest` perde `size`/`page`, ganha de
volta `limit` (`int?`, default 10, teto 50).

## Configuração

`appsettings.json` e `.eng/k8s/configmap.yaml` usam os mesmos valores reais (confirmados
pelo usuário: é o mesmo `qwen2.5:7b-instruct` já instalado e em uso pelo `code-rag-api`
nesse Ollama compartilhado) — `Reranking__Provider=Ollama`,
`Reranking__Model=qwen2.5:7b-instruct` (modelo de instrução/chat — **não** `bge-m3`, que é
só para embeddings), mesmo host `192.168.1.212:11434`, `TimeoutSeconds=120`,
`CandidatePoolSize=25`, `MaxConcurrency=6`. Ao contrário de `code-rag-api` (que deixa
`appsettings.json` com `Provider` vazio e só liga via `configmap.yaml`), aqui reranking vem
ligado por padrão em qualquer ambiente que leia `appsettings.json` sem overrides — decisão
explícita do usuário, não um default conservador herdado do repo de referência. Novo
`.eng/k8s/serverstransport.yaml`
(`code-ciir-api-transport`, `responseHeaderTimeout: 120s`), referenciado via anotação
Traefik em `service.yaml` e listado em `kustomization.yaml`.

## Fora de escopo

- Provider Cohere.
- Opt-out de reranking por requisição — 100% controlado por config do servidor.
- Cache de resultados de reranking, retry automático em falha de provider.

## Verificação de saída desta fase

- `dotnet build`/`dotnet test` na solução inteira: 135 testes verdes (0 falhas) — 3 novos
  projetos de teste de reranking (Abstraction: 12, Ollama: 12, OpenAI: 15) mais os
  ajustes nas 4 suites já existentes de `code-queries`.
- Confirmado pelo usuário: `qwen2.5:7b-instruct` já está instalado nesse Ollama
  compartilhado e em uso pelo `code-rag-api` — não é mais uma pendência.
- Pendente: smoke test manual real contra `code3rag`/Ollama com reranking ligado,
  confirmando que `rerank_score` vem preenchido e a ordem reflete a nota do reranker, não
  só a similaridade de cosseno crua.
