# Fase 8 — Filtros estruturados e `projectId` opcional em `code-queries`

**Status: concluído.** A busca saiu de `POST /api/v1/projects/{projectId}/code-queries` para
uma rota sem projeto — hoje `POST /api/code-queries` —, e `CodeQueryRequest` ganhou
`projectId` (opcional), `kind` (igualdade) e `qualifiedName` (`equals`/`contains`/
`notContains`). O embedding da pergunta passou a usar sempre o modelo configurado na
aplicação (`Embeddings:Model`/`Embeddings:Dimensions`). Esta fase também trocou `limit` por
paginação `size`+`page`, o que foi revertido na Fase 9 (`10-reranking.md`): reranking precisa
reordenar todo o pool de candidatos antes de truncar, incompatível com paginação por `OFFSET`.

## Contexto

O endpoint de busca semântica (Fase 3, `04-code-queries-baseline.md`) só aceitava
`question`+`limit`/`minSimilarity`, sempre escopado por um projeto fixo na rota. O usuário
pediu filtros novos e que o projeto deixasse de ser parte da rota, virando um campo opcional
do corpo — para permitir buscar por `kind`/`qualifiedName` com ou sem restringir a um projeto.

## Decisões de arquitetura

**Embedding sempre pelo modelo da aplicação.** Com o projeto opcional, o modelo não pode vir
do projeto — e não precisa: `ciir_documents.embedding` tem uma única largura fixa
(`vector(1024)`) para toda a instalação de `code3rag` (ver `01-schema-discovery.md`).
`EmbeddingOptions` tem `Model`/`Dimensions` (seção `Embeddings`, hoje `bge-m3`/`1024`, os
mesmos do indexer), e `EmbeddingGeneratorResolver` falha rápido no startup se estiverem
ausentes/inválidos.

**`projectId` é um filtro de escopo opcional**, no mesmo nível de `kind`/`qualifiedName`.
Quando informado, é o UUID (`public_id`) de um projeto existente: `404` se não existir, `400`
se não for um UUID válido. Quando omitido, a busca roda entre todos os projetos.

**Grafo de relações só quando `projectId` é informado.** `ciir_relations` é escopada por
`project_id` (`RelationshipGraphRepository.GetGraphAsync` recebe um único projeto). Sem
`projectId`, `matches` vem sem grafo (`graph` vazio) — expandir e mesclar o grafo de vários
projetos nunca foi pedido. O mesmo vale para `matches[].relations` (`12-match-relations.md`).

## Reaproveitamento de `code-rag-api`/`BlogDoFT.Libs`

`code-rag-api` já resolve filtro opcional com operador usando
`BlogDoFT.Libs.DapperUtils.Postgres.WhereBuilder` e
`BlogDoFT.Libs.DapperUtils.Abstractions.Extensions.SqlExtensions.AsSqlWildCard`:

- `WhereBuilder.AndWith(paramValue, condition)` — só adiciona a condição quando o valor não é
  `null`; usado para projeto/`kind`/`qualifiedName` opcionais antes das condições fixas
  (`embedding IS NOT NULL`, `minSimilarity`).
- `AsSqlWildCard(value, toUpperCase: false)` — troca `*` por `%`, aplicado só a `contains`/
  `notContains` de `qualifiedName`; sem `*` no valor, a condição `ILIKE` vira igualdade
  case-insensitive (nunca `%valor%` implícito).

`kind` é **sempre igualdade simples** (`cd.kind = @KindValue`, sem `ILIKE`/wildcard) — mais
simples que o filtro de `kind` de `code-rag-api` (que tem 3 operadores), pedido explícito do
usuário.

## Contrato

```
POST /api/code-queries
{
  "question": "string (obrigatório)",
  "projectId": "uuid? (opcional)",
  "minSimilarity": "double? (opcional, 0.0-1.0)",
  "kind": "string? (opcional, igualdade exata)",
  "qualifiedName": { "operator": "equals|contains|notContains", "value": "string" } | null,
  "limit": "int? (opcional, default 10, teto 50 — ver 10-reranking.md)"
}
```

`qualifiedName` compara contra `ciir_documents.symbol_qualified_name`.

## SQL de referência

```sql
-- filtros opcionais (project_id/kind/qualified_name) entram antes das condições fixas
SELECT cd.id, cd.kind, cd.symbol_container, cd.symbol_name, cd.symbol_qualified_name,
       cd.symbol_canonical_name, cd.source_path, cd.embedding_text,
       ROUND((1 - (cd.embedding <=> @Embedding))::numeric, 10)::float8 AS similarity
FROM public.ciir_documents cd
WHERE [cd.project_id = @ProjectId and] [cd.kind = @KindValue and] [<qualified_name condition> and]
      cd.embedding IS NOT NULL
  AND (1 - (cd.embedding <=> @Embedding)) >= @MinSimilarity
ORDER BY cd.embedding <=> @Embedding
LIMIT @SearchLimit
```

(`@ProjectId` aqui é o `id` interno, resolvido a partir do `projectId` recebido; o SELECT real
também traz `gitUrl`/`gitRawUrl` do projeto. `@SearchLimit` é o tamanho do pool de candidatos
do reranking, ver `10-reranking.md`.)

## Fora de escopo

- Paginação e metadados de paginação na resposta.
- Expor os filtros no fluxo de feedback.
- Expandir/mesclar o grafo de relações entre múltiplos projetos quando `projectId` é omitido.

## Verificação de saída desta fase

- `dotnet build`/`dotnet test` na solução inteira: 92 testes verdes (0 falhas), cobrindo as
  4 suites afetadas (`CodeCiir.Application.Tests`, `CodeCiir.Infrastructure.Database.Tests`
  via Testcontainers, `CodeCiir.Api.Tests`, `CodeCiir.Mcp.Tests`).
- Pendente: smoke test manual real contra `code3rag`/Ollama com `kind`+`qualifiedName`
  (`contains` com `*`), com e sem `projectId`.
