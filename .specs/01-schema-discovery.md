# Fase 0 — Descoberta do schema real de `code3rag`

**Status: concluído.** Schema confirmado por introspecção ao vivo diretamente em
`code3rag` (`192.168.1.212:5432`), via `psql` rodado em um container
Docker descartável (não havia cliente Postgres instalado localmente). Um achado anterior
desta mesma fase (ver "Anexo histórico" no fim deste documento) tinha inspecionado
`code2rag` por engano — schema completamente diferente, descartado como base de desenho.

## Contexto

`code-ciir-api` lê de `jdbc:postgresql://192.168.1.212:5432/code3rag`, uma base populada
por outro serviço de indexação (`code-ciir-indexer`, dono do schema). `code-ciir-api` é
**consumidor** desse schema, não seu dono — nunca roda DDL/migrations nele (mesma
convenção do `code-rag-api` em relação a `code2rag`).

## Tabelas em `public` (code3rag)

```
ciir-indexer-VersionInfo   -- bookkeeping de migration do indexer, irrelevante aqui
ciir_documents             -- os NÓS do grafo (entidades de código indexadas)
ciir_relations             -- as ARESTAS do grafo (relações entre entidades)
ciir_uploads               -- fila durável de uploads CIIR do indexer, irrelevante aqui
code_query_feedback        -- feedback de code-queries (escrito por esta API)
indexing_runs              -- bookkeeping de execuções do indexer, irrelevante aqui
projects                   -- projetos indexados
```

Extensão `vector` (pgvector) instalada; índice de similaridade é **HNSW**, não IVFFlat.

## `projects`

```
id                     bigint PK, identity   -- chave interna, só usada nas FKs
public_id              uuid NOT NULL, UNIQUE (ux_projects_public_id) -- UUIDv7, identificador exposto
name                   text NOT NULL, UNIQUE (ux_projects_name)
git_url                text                  -- opcional
git_raw_url            text                  -- opcional
created_at             timestamptz NOT NULL default now() UTC
updated_at             timestamptz NOT NULL default now() UTC
```

Divergências importantes em relação ao `ProjectResponse` do code-rag-api:
- O identificador exposto é `public_id` (UUID), não o `id` numérico. Toda rota/tool desta API
  recebe e devolve `projectId` como UUID e resolve o `id` interno uma única vez, antes de
  consultar `ciir_documents`/`ciir_relations`/`code_query_feedback`.
- `git_url`/`git_raw_url` existem e são opcionais; valores em branco são tratados como `null`
  na leitura. São a base para `gitUrl`/`gitRawUrl` nos resultados de `code-queries` e na tool
  `get_code_source`.
- **Não há modelo de embedding por projeto.** O modelo/dimensão são configuração da
  implantação (tanto no indexer quanto nesta API, seção `Embeddings`) e ficam registrados por
  documento em `ciir_documents.embedding_model`/`embedding_dimensions`.
- `updated_at` existe (não existia no code-rag-api).

Dado real no momento da descoberta (21 linhas): granularidade de "projeto" era **por
artefato/csproj**, não por repositório inteiro — os dados eram o próprio código-fonte do
`code-rag-api` indexado, um "projeto" por `.csproj` (`CodeRag.Api`, `CodeRag.Application`,
`CodeRag.Api.Tests`, ...), todos embedados com `bge-m3`/1024. Desde então o indexer passou a
receber a identidade do projeto do chamador (um projeto por upload, cadastrado previamente via
`POST /api/indexer/projects`), então não assumir que "projeto" mapeia 1:1 a "csproj" nem a
"repositório git".

## `ciir_documents` (os nós do grafo)

```
id                          bigint PK, identity
project_id                  bigint NOT NULL FK -> projects.id
ciir_id                     text NOT NULL      -- id lógico determinístico (sha256:...), conteúdo-endereçável
schema_version              text NOT NULL
kind                        text NOT NULL      -- valores observados: project, namespace, type, method, constructor, field, property
language                    text NOT NULL      -- ex. "csharp"
symbol_name                 text               -- nome curto (ex. "QueryAsync")
symbol_qualified_name       text               -- nome pontilhado sem tipos de parâmetro (ex. "CodeRag.Api.Controllers.CodeQueriesController.QueryAsync")
symbol_canonical_name       text               -- assinatura completa com tipos de parâmetro, desambigua overloads
symbol_container            text               -- nome qualificado do container direto (tipo ou namespace-pai); vazio para kind=namespace de topo
source_path                 text               -- caminho relativo do arquivo fonte
embedding_text              text               -- texto que foi de fato embedado
embedding_text_strategy     text               -- ex. "semantic-v1"
embedding_text_hash         text
embedding_model             text               -- modelo usado para ESTA linha (configuração da implantação do indexer)
embedding_dimensions        integer
embedding_fingerprint_hash  text
content                     jsonb NOT NULL     -- payload estruturado rico (símbolo, comentários, etc.); índice GIN
last_seen_run_id            uuid               -- FK lógica (não declarada) -> indexing_runs.id
created_at                  timestamptz NOT NULL default now() UTC
updated_at                  timestamptz NOT NULL default now() UTC
embedding                   vector(1024)       -- nullable; índice HNSW parcial WHERE embedding IS NOT NULL
UNIQUE (project_id, ciir_id)                    -- ux_ciir_documents_project_ciir_id
```

Índices relevantes: `ix_ciir_documents_project_id`, `ix_ciir_documents_kind`,
`ix_ciir_documents_symbol_qualified_name`, `ix_ciir_documents_content_gin`,
`ix_ciir_documents_embedding_hnsw` (HNSW, `vector_cosine_ops`, parcial `WHERE embedding IS
NOT NULL` — **não** filtrado por modelo como em `code2rag`, já que aqui o modelo é fixo
para toda a instalação).

**Não existem colunas `namespace`/`type_name`/`member` separadas** como no
`code-rag-api`. O mapeamento mais próximo:

| Conceito `code-rag-api` | Equivalente em `ciir_documents` |
|---|---|
| `kind` | `kind` (valores diferentes — ver lista acima; nota: `namespace` e `project` são também `kind`s de documento aqui, não só metadados) |
| `member` (nome do método/campo/propriedade) | `symbol_name` |
| `type_name` (tipo contêiner) | `symbol_container` — mas é o nome **qualificado completo** do container (ex. `"CodeRag.Api.Controllers.CodeQueriesController"`), não só o nome curto do tipo |
| `namespace` | Não há coluna dedicada. Para um documento `kind=namespace`, `symbol_qualified_name` já É o namespace. Para os demais kinds, o namespace está embutido como prefixo de `symbol_container`/`symbol_qualified_name`, não isolado — extrair exigiria parsing de string ou uma relação estrutural (que não existe explicitamente, ver `ciir_relations` abaixo) |
| `source_file` | `source_path` |
| `embedding_text` | `embedding_text` (igual) |

**Decisão proposta para `04-code-queries-baseline.md`**: expor os campos nativos
(`symbol_name`, `symbol_qualified_name`, `symbol_canonical_name`, `symbol_container`) em
vez de forçar um encaixe artificial em `namespace`/`type_name`/`member` que não existem de
verdade neste schema — é a interpretação mais direta de "adaptar à realidade das
tabelas". O filtro `namespace`/`type_name` do code-rag-api (`.specs/code-queries-filters.md`
do repo de referência) vira, aqui, um filtro sobre `symbol_container`/`symbol_qualified_name`
(`Contains`/`Equals`/etc. continuam fazendo sentido do mesmo jeito, só a coluna-alvo muda).

## `ciir_relations` (as arestas do grafo — peça central da Fase 4)

```
id                   bigint PK, identity
project_id           bigint NOT NULL FK -> projects.id
source_ciir_id       text NOT NULL           -- ciir_id lógico da origem (sempre presente)
target_ciir_id       text                    -- ciir_id lógico do destino (nulo quando não resolvido a um documento)
source_document_id   bigint FK -> ciir_documents.id ON DELETE CASCADE   -- nullable no schema, mas 100% preenchido na prática (origem é sempre algo indexado)
target_document_id   bigint FK -> ciir_documents.id ON DELETE CASCADE   -- NULL quando o alvo está fora do universo indexado ou ainda não resolvido
kind                 text NOT NULL           -- tipo da relação (ver universo confirmado abaixo)
target_symbol        text NOT NULL           -- nome qualificado do alvo, SEMPRE presente mesmo quando target_document_id é nulo
resolution_status     text NOT NULL          -- 'resolved' | 'external'
resolution_origin     text NOT NULL          -- 'project' | 'solution' | 'framework' | 'dependency'
resolution_reason     text                   -- nullable, motivo quando não resolvido
source_path           text
start_line, start_column, end_line, end_column   integer   -- localização exata da referência no código-fonte
idempotency_key       varchar(64) NOT NULL, UNIQUE
last_seen_run_id      uuid
created_at, updated_at   timestamptz NOT NULL default now() UTC
```

Índices: `ix_ciir_relations_project_id`, `ix_ciir_relations_kind`,
`ix_ciir_relations_source_ciir_id`, `ix_ciir_relations_target_ciir_id`,
`ix_ciir_relations_source_document_kind` `(source_document_id, kind)`,
`ix_ciir_relations_target_document_kind` `(target_document_id, kind)`,
`ix_ciir_relations_target_symbol`.

### Achados críticos para a Fase 4

- **FKs de verdade, com `ON DELETE CASCADE`**, para `ciir_documents.id` (bigint) em ambos
  os lados — diferente do que o achado inicial (código2rag) sugeria, **a travessia do
  grafo pode ser feita direto por `source_document_id`/`target_document_id` (bigint)**,
  sem nenhum `JOIN`/cast por texto. Isso simplifica bastante o SQL da Fase 4 em relação ao
  rascunho anterior.
- **`target_document_id` é nullable e frequentemente nulo por design**, não por
  inconsistência de dado: `resolution_status = 'external'` (2521 de 4429 linhas, ~57%,
  medido em produção) significa "a relação existe e o alvo tem um nome conhecido
  (`target_symbol`), mas o alvo está fora do universo indexado" (ex.: chamando um método
  do framework/.NET, de uma dependência NuGet, etc. — `resolution_origin` distingue
  `framework`/`dependency`/`solution`/`project`). Além disso, **mesmo `resolution_status =
  'resolved'` pode ter `target_document_id` nulo** (438 de 2429 linhas resolvidas, ~18% —
  medido em produção): o `ciir_id` do alvo é conhecido mas o processo de resolver não
  encontrou (ainda?) o documento correspondente. Em nenhum dos dois casos há erro de
  integridade — é o desenho esperado de um "resolver" assíncrono (ver nota do
  `indexer-api` no anexo histórico). **Consequência de desenho para a Fase 4**: a
  travessia do grafo só pode avançar por arestas com `target_document_id IS NOT NULL`
  (não há nó para expandir a partir de um alvo externo/não-resolvido), mas essas arestas
  "sem nó" ainda devem aparecer na resposta como uma **aresta terminal** (usando
  `target_symbol` como rótulo, sem um `id` de nó de verdade) — descartá-las silenciosamente
  violaria o requisito de "todas as relações".
- **Nenhuma FK/coluna de escopo redundante além de `project_id` direto na tabela** — igual
  ao suposto no rascunho original, a condição de projeto no `WHERE` é direta.
- **Universo completo de `kind` (relation_type), confirmado por `SELECT kind, COUNT(*)
  GROUP BY kind` em produção** (9 valores, 4429 linhas totais):

  | kind | contagem |
  |---|---|
  | `calls` | 1951 |
  | `reads` | 1740 |
  | `constructs` | 351 |
  | `overrides` | 140 |
  | `writes` | 138 |
  | `implements` | 70 |
  | `throws` | 19 |
  | `inherits` | 14 |
  | `catches` | 6 |

  Nenhum `contains`/`declares` — a relação de containment (método pertence a tipo, tipo
  pertence a namespace) **não é modelada como aresta em `ciir_relations`**; ela só existe
  implicitamente via `ciir_documents.symbol_container`. A Fase 4 (grafo de "relações")
  não precisa nem deve inventar essa aresta — o requisito é sobre as relações que já
  existem na tabela.
- **Grau de nó, medido em produção** (`ciir_relations` com `source_document_id`/
  `target_document_id` não nulo): grau de saída médio **8.53**, máximo **48**; grau de
  entrada máximo observado **29**. Fan-out real é modesto — um BFS de 2 níveis a partir de
  poucos matches não deve, na prática atual, se aproximar de limites problemáticos, mas o
  limite de segurança (`Graph:MaxNodesPerQuery`) continua sendo boa prática defensiva
  contra um hub futuro ou um projeto muito mais denso.

## Feedback

`code_query_feedback` não existia na descoberta inicial. Foi criada depois por migration do
`code-ciir-indexer` (`M20260909000000_AddCodeQueryFeedback`), com FK para `projects.id` e
índice `ix_code_query_feedback_project_id_created_at` — ver `06-code-query-feedback.md`.

## Credenciais/conexão

Credenciais de acesso a `code3rag` **não devem ser commitadas em nenhum arquivo deste
repositório** (`.specs/`, `appsettings.json`, etc.); a connection string de produção/dev vem de
secret (Kubernetes Secret / `dotnet user-secrets` local), nunca de arquivo versionado — ver
`02-bootstrap-solution.md`/`08-ops-deployment.md`.

## Confirmação cruzada com a migration autoritativa

`indexer-api` forneceu a URL do repositório dono deste schema:
[`sauron/code-ciir-indexer`](https://forgejo.home.arpa/sauron/code-ciir-indexer)
(`src/Ciir.Indexer.Infrastructure.PostgreSql/Migrations/Migrations/M20260908000000_InitialSchema.cs`,
FluentMigrator). Conferido linha a linha contra a introspecção ao vivo — **bate
integralmente** com o levantado acima, mais um detalhe que só a migration revela:

> `embedding vector({_options.EmbeddingDimensions})` é um `ALTER TABLE` cru (fora da API
> tipada do FluentMigrator) com **um único valor de dimensão fixado por instalação** do
> indexer, comentado explicitamente no código como "v1 assumes one fixed dimensionality
> per install" — **não** um design multi-modelo por linha com índice parcial (esse é o
> desenho de `code2rag`, não o de `code3rag`).

Consequência: **a coluna `ciir_documents.embedding` tem uma largura fixa única para toda a
instalação de `code3rag`**, e o modelo/dimensão são configuração da implantação, não do
projeto. Esta API embeda a pergunta com o modelo da sua própria configuração
(`Embeddings:Model`/`Embeddings:Dimensions`, ver `09-code-queries-filters.md`), que precisa
ser o mesmo usado pelo indexer.

`indexer-api` também confirmou que os comentários da migration referenciam um documento de
especificação interno do `code-ciir-indexer` (spec §10/§11/§12/§16/§26 + "embedding-
fingerprint addendum") não obtido nesta sessão — relevante só se uma fase futura precisar
entender a fundo a semântica de `embedding_fingerprint_hash`/reaproveitamento de embeddings
entre documentos idênticos (hoje não é usado por nenhuma fase deste plano).

## Pendências residuais (não bloqueiam as fases seguintes)

- Estrutura completa do `content` jsonb não foi mapeada a fundo (só uma amostra) — só
  relevante se alguma fase futura decidir expor campos de `content` além dos já cobertos
  por colunas próprias (`comments`, por exemplo, aparece como chave em pelo menos um
  documento amostrado).

## Anexo histórico — achado inicial equivocado (`code2rag`)

Uma tentativa anterior nesta mesma fase pediu à sessão irmã `indexer-api` para inspecionar
o schema, mas a conexão MCP dela no momento apontava para `code2rag` (o banco do
`code-rag-api`), não `code3rag` — resultado descartado como base de desenho, mantido aqui
só para registro histórico do processo (a mesma convenção de "⚠️ correção" das specs do
`code-rag-api` de referência, que nunca reescrevem silenciosamente um achado errado).

`code2rag.public` tinha `projects`/`embedding_models`/`code_documents` no mesmo formato
documentado em `db/init.sql` do `code-rag-api`, mais uma tabela `code_document_relations`
não documentada nesse DDL (arestas por `document_id` **texto**, sem FK formal a
`code_documents`). Nenhuma dessas tabelas tem relação com o desenho final de `code3rag`
acima — schemas e nomes de tabela são inteiramente diferentes entre os dois bancos. Não há
mais nenhuma dependência deste plano em relação a `code2rag`.
