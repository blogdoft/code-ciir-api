# Fase 3 — `POST /api/v1/projects/{projectId}/code-queries` (baseline, sem grafo)

> **Superseded por `.specs/09-code-queries-filters.md`.** A rota, o contrato de request
> (`limit` → `size`+`page`, `projectId` saiu da rota e virou `project_id` opcional no body)
> e a fonte do modelo de embedding (projeto → configuração da aplicação) descritos abaixo
> não refletem mais o estado atual do endpoint — mantidos aqui só como registro histórico
> da Fase 3.

**Status: concluído.** Implementado exatamente como desenhado abaixo: `CodeCiir.
Embeddings.Abstraction`/`.Ollama` (resolução de gerador por `model`/`dimensions` **por
requisição**, lido de `projects.embedding_model`/`embedding_dimensions`, com cache por par
no `EmbeddingGeneratorResolver`), `CodeQueryService`, `CodeDocumentsRepository`
(pgvector, `ROUND(...)::float8` para `similarity`), `CodeQueriesController`. 26 testes
verdes (10 unit `CodeQueryServiceTests`, 10 Testcontainers `CodeDocumentsRepositoryTests`,
4 HTTP `CodeQueriesEndpointTests`, com `ICodeQueryService` substituído via NSubstitute).

**Smoke test real contra `code3rag` + Ollama (`192.168.1.212:11434`, `bge-m3`)**:
`POST /api/v1/projects/1/code-queries` com a pergunta "where is the code that handles a
natural language code query request?" contra o projeto `CodeRag.Api` (id 1, 21 projetos
reais já existentes) devolveu como primeiro resultado exatamente
`CodeRag.Api.Controllers.CodeQueriesController.QueryAsync` — confirma o pipeline completo
de ponta a ponta (leitura do modelo do projeto → embedding via Ollama → busca vetorial →
mapeamento de campos) contra dado e infraestrutura reais, não só fixtures sintéticas.

## Contexto

Antes de somar a expansão de grafo (Fase 4, o requisito central pedido), esta fase
estabelece a busca semântica pura contra `code3rag.public.ciir_documents`: dado
`projectId` + `question`, embedar a pergunta e retornar as entidades de código mais
similares por cosseno.

## Requisitos herdados do `code-rag-api` (mantidos)

- 200 com array vazio quando não há match (nunca erro).
- 404 só quando `projectId` não existe.
- `RouteId.TryParsePositive` para validar `projectId`.
- Mesmo desenho de filtros opcionais por campo com operador (`Contains`/`Equals`/
  `NotEquals`/etc., wildcard `*` → `%`), adaptado às colunas reais (ver abaixo) —
  reaproveitar a lib `BlogDoFT.Libs.DapperUtils.Postgres`/`WhereBuilder` como o
  code-rag-api já faz.

## Contrato de resposta — adaptado aos campos reais de `ciir_documents`

`code3rag` não tem `namespace`/`type_name`/`member`/`git_url`/`git_raw_url` (ver
`01-schema-discovery.md`). Contrato proposto para `CodeQueryResultResponse`:

| Campo (code-rag-api) | `code-ciir-api` (proposto) | Origem |
|---|---|---|
| `id` | `id` | `ciir_documents.id` |
| `kind` | `kind` | `ciir_documents.kind` (valores: `project`, `namespace`, `type`, `method`, `constructor`, `field`, `property`) |
| `type_name` | `symbol_container` | `ciir_documents.symbol_container` — nome qualificado completo do container, não só o nome curto |
| `member` | `symbol_name` | `ciir_documents.symbol_name` |
| — (novo) | `symbol_qualified_name` | útil como identificador legível único sem precisar concatenar container+nome |
| — (novo) | `symbol_canonical_name` | assinatura completa (desambigua overloads) — expor pois `code-rag-api` não tinha esse problema resolvido e aqui o dado já existe pronto |
| `source_file` | `source_path` (renomeado para casar com a coluna, ou mantido `source_file` por compatibilidade de nome de campo — **decisão de nomenclatura a confirmar**, proposta: manter `source_file` no JSON por familiaridade com consumidores do code-rag-api, mesmo a coluna interna sendo `source_path`) | `ciir_documents.source_path` |
| `embedding_text` | `embedding_text` | igual |
| `similarity` | `similarity` | calculado, igual |
| `gitUrl`/`gitRawUrl` | **removidos** | sem equivalente em `code3rag.projects` |

`document_id`/`ciir_id` (chave lógica interna usada pela Fase 4 para navegar
`ciir_relations`) **não precisa ser exposto no JSON de resposta** — a Fase 4 o usa
internamente entre `Application`/`Infrastructure`, mas o consumidor externo só vê `id`
(bigint), consistente entre `matches` e `graph.nodes`.

## Embedding da pergunta — modelo por projeto, não global

Diferença estrutural relevante: em `code3rag`, `embedding_model`/`embedding_dimensions`
vivem em **`projects`**, não numa tabela `embedding_models` separada e nem por linha de
documento (embora `ciir_documents` também tenha suas próprias colunas
`embedding_model`/`embedding_dimensions`, que na prática devem sempre casar com as do
projeto — não custa validar isso defensivamente na query, mas a fonte de verdade para
"qual modelo usar para embedar a pergunta" é `projects.embedding_model`/
`embedding_dimensions` do `projectId` da chamada).

Fluxo: `CodeQueryService.QueryAsync` primeiro busca o projeto (já precisa fazer isso para
o 404), lê `embedding_model`/`embedding_dimensions` dele, resolve o `IEmbeddingGenerator`
correspondente (mesma abstração `CodeCiir.Embeddings.*` do code-rag-api,
`EmbeddingGeneratorResolver` por provider/model/dimensions), embeda a pergunta, e só então
faz a busca vetorial filtrando `ciir_documents` por `project_id` (o índice HNSW já é
parcial só por `embedding IS NOT NULL`, sem filtro de modelo — não há necessidade de
condição extra de modelo no `WHERE` para usar o índice, mas continua sendo boa prática
verificar `embedding_dimensions = @Dimensions` como guarda contra erro de configuração).

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

(Filtros opcionais de `kind`/`symbol_container`/`symbol_qualified_name` entram antes de
`cd.project_id = @ProjectId`, mesma convenção do code-rag-api.)

## Fora de escopo nesta fase

- Grafo de relações (Fase 4).
- Reranking (não incluído no bootstrap, ver `02-bootstrap-solution.md`).
- Feedback (Fase 5).

## Verificação de saída desta fase

- Teste de integração (Testcontainers, schema real de `ciir_documents`/`projects`) com
  match e sem match, e projeto inexistente (404).
- Teste manual contra `code3rag` real: já existem 21 projetos e 901 documentos
  (`bge-m3`/1024 dims) prontos para uma pergunta real de ponta a ponta assim que a Fase 1
  (bootstrap) e esta fase estiverem implementadas — não é necessário esperar dado
  sintético para o primeiro smoke test.
