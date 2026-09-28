# Fase 3 — `POST /api/code-queries` (baseline, sem grafo)

> **Estado atual do endpoint em `.specs/09-code-queries-filters.md`, `10-reranking.md`,
> `12-match-relations.md` e `15-skills-alignment.md`.** Hoje a rota é `POST /api/code-queries`
> (sem `{projectId}` na rota; `projectId` é um UUID opcional no body), o JSON é `camelCase`, o
> modelo de embedding da pergunta vem da configuração da aplicação (`Embeddings:*`), e cada
> resultado traz `gitUrl`/`gitRawUrl` derivados de `projects.git_url`/`git_raw_url`. Este
> documento registra como a Fase 3 foi entregue.

**Status: concluído.** Entregue: `CodeCiir.Embeddings.Abstraction`/`.Ollama`,
`CodeQueryService`, `CodeDocumentsRepository` (pgvector, `ROUND(...)::float8` para
`similarity`), `CodeQueriesController`. 26 testes verdes (10 unit `CodeQueryServiceTests`,
10 Testcontainers `CodeDocumentsRepositoryTests`, 4 HTTP `CodeQueriesEndpointTests`, com
`ICodeQueryService` substituído via NSubstitute).

**Smoke test real contra `code3rag` + Ollama (`192.168.1.212:11434`, `bge-m3`)**: a pergunta
"where is the code that handles a natural language code query request?" contra o projeto
`CodeRag.Api` devolveu como primeiro resultado exatamente
`CodeRag.Api.Controllers.CodeQueriesController.QueryAsync` — confirma o pipeline completo de
ponta a ponta (embedding via Ollama → busca vetorial → mapeamento de campos) contra dado e
infraestrutura reais, não só fixtures sintéticas.

## Contexto

Antes de somar a expansão de grafo (Fase 4, o requisito central pedido), esta fase
estabelece a busca semântica pura contra `code3rag.public.ciir_documents`: dado
`projectId` + `question`, embedar a pergunta e retornar as entidades de código mais
similares por cosseno.

## Requisitos herdados do `code-rag-api` (mantidos)

- 200 com array vazio quando não há match (nunca erro).
- 404 só quando `projectId` é informado e não existe.
- Mesmo desenho de filtros opcionais por campo com operador (`Contains`/`Equals`/
  `NotEquals`/etc., wildcard `*` → `%`), adaptado às colunas reais (ver abaixo) —
  reaproveitar a lib `BlogDoFT.Libs.DapperUtils.Postgres`/`WhereBuilder` como o
  code-rag-api já faz.

## Contrato de resposta — adaptado aos campos reais de `ciir_documents`

`code3rag` não tem `namespace`/`type_name`/`member` (ver `01-schema-discovery.md`).
Contrato de `CodeQueryResultResponse` (JSON em `camelCase`):

| Campo (code-rag-api) | `code-ciir-api` | Origem |
|---|---|---|
| `id` | `id` | `ciir_documents.id` |
| `kind` | `kind` | `ciir_documents.kind` (valores: `project`, `namespace`, `type`, `method`, `constructor`, `field`, `property`) |
| `typeName` | `symbolContainer` | `ciir_documents.symbol_container` — nome qualificado completo do container, não só o nome curto |
| `member` | `symbolName` | `ciir_documents.symbol_name` |
| — (novo) | `symbolQualifiedName` | identificador legível único sem precisar concatenar container+nome |
| — (novo) | `symbolCanonicalName` | assinatura completa (desambigua overloads) |
| `sourceFile` | `sourceFile` | `ciir_documents.source_path` — nome mantido por familiaridade com consumidores do code-rag-api |
| `gitUrl` | `gitUrl` | `projects.git_url` (nulo quando não configurado) |
| `gitRawUrl` | `gitRawUrl` | `projects.git_raw_url` + `/` + `source_path` (nulo quando o projeto não tem a URL ou o documento não tem caminho) |
| `embeddingText` | `embeddingText` | igual |
| `similarity` | `similarity` | calculado, igual |

`document_id`/`ciir_id` (chave lógica interna usada pela Fase 4 para navegar
`ciir_relations`) **não precisa ser exposto no JSON de resposta** — a Fase 4 o usa
internamente entre `Application`/`Infrastructure`, mas o consumidor externo só vê `id`
(bigint), consistente entre `matches` e `graph.nodes`.

## Embedding da pergunta

A pergunta é embedada com o modelo/dimensão da configuração da aplicação
(`Embeddings:Model`/`Embeddings:Dimensions`), resolvido por `EmbeddingGeneratorResolver`
(mesma abstração `CodeCiir.Embeddings.*` do code-rag-api). `code3rag` tem uma única largura
de vetor para toda a instalação (ver `01-schema-discovery.md`), então esse modelo precisa ser o
mesmo usado pelo `code-ciir-indexer`. A busca vetorial filtra `ciir_documents` por
`project_id` quando o projeto é informado (o índice HNSW é parcial só por `embedding IS NOT
NULL`, sem filtro de modelo).

## SQL de referência

```sql
SELECT cd.id
     , cd.ciir_id
     , cd.kind
     , cd.symbol_container
     , cd.symbol_name
     , cd.symbol_qualified_name
     , cd.symbol_canonical_name
     , cd.source_path
     , cd.embedding_text
     , 1 - (cd.embedding <=> @Embedding) AS similarity
FROM public.ciir_documents cd
WHERE cd.project_id = @ProjectId
  AND cd.embedding IS NOT NULL
  AND (1 - (cd.embedding <=> @Embedding)) >= @MinSimilarity
ORDER BY cd.embedding <=> @Embedding
LIMIT @Limit
```

(Filtros opcionais — `kind` e `qualifiedName` — e o `projectId` opcional foram adicionados
na Fase 8, ver `09-code-queries-filters.md`.)

## Fora de escopo nesta fase

- Grafo de relações (Fase 4).
- Reranking (Fase 9, ver `10-reranking.md`).
- Feedback (Fase 5).

## Verificação de saída desta fase

- Teste de integração (Testcontainers, schema real de `ciir_documents`/`projects`) com
  match e sem match, e projeto inexistente (404).
- Smoke test manual contra `code3rag` real (21 projetos, 901 documentos `bge-m3`/1024 no
  momento), descrito no topo deste documento.
