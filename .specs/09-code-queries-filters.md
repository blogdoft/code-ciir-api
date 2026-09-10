# Fase 9 — Filtros estruturados, paginação e `projectId` opcional em `code-queries`

> **Paginação (`size`+`page`) revertida para `limit` na Fase 10** (`.specs/10-reranking.md`):
> reranking exige reordenar todo o pool de candidatos do maior match para o menor antes de
> truncar, o que é incompatível com paginação por `OFFSET` (cada página seria rerankeada
> isoladamente). Os filtros `kind`/`qualified_name`/`project_id` descritos abaixo
> continuam válidos e inalterados — só a parte de paginação está desatualizada.

**Status: concluído.** `POST /api/v1/projects/{projectId}/code-queries` virou
`POST /api/v1/code-queries` (sem `{projectId}` na rota); `CodeQueryRequest` ganhou
`project_id` (opcional), `kind` (igualdade), `qualified_name` (`Equals`/`Contains`/
`NotContains`) e `size`+`page` (substituindo `limit`). O embedding da pergunta passou a
usar sempre o modelo configurado na aplicação (`Embeddings:Model`/`Embeddings:Dimensions`),
não mais `projects.embedding_model`/`embedding_dimensions`. `WhereBuilder`/`SqlPagination`/
`AsSqlWildCard` de `BlogDoFT.Libs.DapperUtils.*` (já referenciada, nunca usada até aqui)
foram finalmente ligados. 92 testes verdes na solução inteira (43 unit
`CodeQueryServiceTests` + outros, 28 Testcontainers `CodeDocumentsRepositoryTests` + outros,
14 HTTP `CodeCiir.Api.Tests`, 7 `CodeCiir.Mcp.Tests`).

## Contexto

O endpoint de busca semântica (Fase 3, `.specs/04-code-queries-baseline.md`) só aceitava
`question`+`limit`/`min_similarity`, sempre escopado por um `projectId` fixo na rota. O
usuário pediu 4 filtros novos e que `projectId` deixasse de ser parte da rota, virando um
campo opcional do corpo — para permitir buscar por `kind`/`qualifiedName` e paginar de
verdade, com ou sem restringir a um projeto específico.

## Decisões de arquitetura

**Embedding sempre pelo modelo da aplicação, nunca mais pelo projeto.** Antes desta fase, o
modelo/dimensões usados para embedar a `question` vinham de `projects.embedding_model`/
`embedding_dimensions` (por projeto). Como `projectId` virou opcional, essa fonte deixou de
fazer sentido — e não é necessária: `ciir_documents.embedding` sempre teve uma única largura
fixa (`vector(1024)`) para toda a instalação de `code3rag` (ver
`.specs/01-schema-discovery.md`, "modelo por projeto" sempre foi metadado informativo, nunca
uma capacidade real de misturar dimensões). Por isso `EmbeddingOptions` ganhou `Model`/
`Dimensions` (seção `Embeddings`, hoje `bge-m3`/`1024` — os mesmos valores já em uso pelos
901 documentos reais indexados), e `CodeQueryService` resolve o gerador de embedding
sempre a partir dessa configuração, nunca mais do projeto. `EmbeddingGeneratorResolver.
ValidateProviderConfigured()` agora também falha rápido no startup se `Model`/`Dimensions`
estiverem ausentes/inválidos.

**`projectId` vira puramente um filtro de escopo opcional**, no mesmo nível de `kind`/
`qualifiedName`: quando informado, deve ser um inteiro positivo existente (400/404 nos
mesmos moldes de antes); quando omitido, a busca roda entre todos os projetos.

**Grafo de relações só quando `projectId` é informado.** `ciir_relations` é ela própria
escopada por `project_id` (`RelationshipGraphRepository.GetGraphAsync` recebe um único
`projectId`). Quando a busca cruza vários projetos (sem `projectId`), expandir e mesclar o
grafo de cada projeto separadamente nunca foi pedido — `matches` sem grafo (`graph` vazio) é
o comportamento adotado nesse caso. Com `projectId` informado, o comportamento de expansão
2-hop é idêntico ao da Fase 4/5, apenas aplicado à página atual de `matches`.

**Teto de `size`: 255**, alinhado ao limite que `BlogDoFT.Libs.DapperUtils.Postgres.
SqlPagination` já impõe internamente (lança `ArgumentOutOfRangeException` fora de 1–255) —
por isso a validação de `size`/`page` acontece em `CodeQueryService` *antes* de chegar à
lib, devolvendo 400 amigável em vez de deixar a exceção virar 500.

**`limit` foi removido**, substituído por `size` (registros por página, default 10) +
`page` (página zero-based, default 0) — paginação real via `LIMIT`/`OFFSET`, não mais só um
top-K de similaridade.

## Reaproveitamento de `code-rag-api`/`BlogDoFT.Libs`

`code-rag-api` já resolve exatamente esse padrão de filtro opcional com operador (`kind`/
`namespace`/`typeName`, cada um com `Contains`/`Equals`/`NotEquals`) usando
`BlogDoFT.Libs.DapperUtils.Postgres.WhereBuilder` e `BlogDoFT.Libs.DapperUtils.
Abstractions.Extensions.SqlExtensions.AsSqlWildCard`. Esta fase reaproveita o mesmo idioma:

- `WhereBuilder.AndWith(paramValue, condition)` — só adiciona a condição quando o valor não
  é `null`; usado para `project_id`/`kind`/`qualified_name` opcionais antes das condições
  fixas (`embedding IS NOT NULL`, `min_similarity`).
- `AsSqlWildCard(value, toUpperCase: false)` — troca `*` por `%`, aplicado só a `Contains`/
  `NotContains` de `qualified_name`; sem `*` no valor, a condição `ILIKE` vira igualdade
  case-insensitive (nunca `%valor%` implícito).
- `SqlPagination.From(new PageFilter { Page, Size })` — gera o `LIMIT`/`OFFSET` literal a
  partir de `size`/`page` já validados.

`kind` é **sempre igualdade simples** (`cd.kind = @KindValue`, sem `ILIKE`/wildcard) — mais
simples que o filtro de `kind` de `code-rag-api` (que tem 3 operadores), pedido explícito
do usuário.

## Contrato

```
POST /api/v1/code-queries
{
  "question": "string (obrigatório)",
  "project_id": "long? (opcional)",
  "min_similarity": "double? (opcional, 0.0-1.0)",
  "kind": "string? (opcional, igualdade exata)",
  "qualified_name": { "operator": "equals|contains|not_contains", "value": "string" } | null,
  "size": "int? (opcional, 1-255, default 10)",
  "page": "int? (opcional, >=0, default 0)"
}
```

A rota de feedback (`POST api/v1/projects/{projectId}/code-queries/feedback`) **não foi
alterada** — o pedido do usuário foi só sobre o endpoint de busca. `CodeQueriesController`
usa uma rota absoluta (`[Route("~/api/v1/code-queries")]`) só na ação `QueryAsync`, mantendo
`[Route("api/v1/projects/{projectId}/code-queries")]` no nível da classe para a ação de
feedback continuar exatamente como antes.

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
LIMIT @Size OFFSET @Page*@Size
```

## Fora de escopo nesta fase

- Metadados de paginação na resposta (total de registros, `has_next_page`, etc.) — só os
  filtros de entrada foram pedidos.
- Expor os novos filtros no fluxo de feedback (`CodeQueryFeedbackRequest`).
- Expandir/mesclar o grafo de relações entre múltiplos projetos quando `projectId` é
  omitido (ver decisão de arquitetura acima).

## Verificação de saída desta fase

- `dotnet build`/`dotnet test` na solução inteira: 92 testes verdes (0 falhas), cobrindo as
  4 suites afetadas (`CodeCiir.Application.Tests`, `CodeCiir.Infrastructure.Database.Tests`
  via Testcontainers, `CodeCiir.Api.Tests`, `CodeCiir.Mcp.Tests`).
- Pendente: smoke test manual real contra `code3rag`/Ollama (`192.168.1.212:11434`,
  `bge-m3`) com `kind`+`qualified_name` (operador `Contains` com `*`) + `size`/`page`, com e
  sem `project_id`, mesma infra do smoke test da Fase 3.
