# Fase 2 — Projetos adaptados a `code3rag`

**Status: concluído — superfície REST removida.** Esta API **não expõe mais nenhuma rota REST
de projetos**. O CRUD completo (`GET/POST/PUT/DELETE`) vive no `code-ciir-indexer`, dono da
tabela `projects` e único escritor dela, em `/api/indexer/projects` (ver
`code-ciir-indexer/.specs/03-projects-crud.md`). Aqui restam:

- a camada de aplicação de leitura (`IProjectsService`/`ProjectsService`,
  `IProjectsRepository`), usada para resolver o `projectId` (UUID) recebido por
  `code-queries`/feedback/stats/export no `id` interno, e pela tool MCP `list_projects`;
- a tool MCP `list_projects` (ver "MCP").

## Contexto

O `code-rag-api` expõe CRUD completo de projetos porque ele **é dono** da tabela
`projects` em `code2rag` — projetos são criados via `POST /projects` antes de qualquer
indexação. Em `code-ciir-api`, `public.projects` em `code3rag` é populada e possuída por
`code-ciir-indexer`, um serviço externo a este.

## Histórico da decisão

1. **Somente leitura** (primeira versão): `GET /api/v1/projects[?name=]` e
   `GET /api/v1/projects/{projectId}`, com o racional de que escrever na mesma tabela que
   `code-ciir-indexer` gerencia criaria dois escritores independentes sem coordenação.
2. **CRUD completo**, a pedido explícito do usuário, aceitando conscientemente o risco de dois
   escritores sem coordenação (checagem de nome não atômica, `DELETE` deixando
   `ciir_documents`/`ciir_relations` órfãos, comportamento dependente de timing).
3. **CRUD movido para o `code-ciir-indexer`**, eliminando o duplo escritor. Esta API voltou a
   ser somente leitura (`GET` paginado + `GET` por id).
4. **Rotas REST de leitura removidas** quando os dois serviços passaram a ficar atrás do mesmo
   gateway (`blogdoft.home.arpa/code-brain`): o front-end e qualquer outro cliente REST
   consultam projetos diretamente no indexer; manter uma segunda cópia de leitura aqui não
   tinha consumidor.

## Identificador do projeto

`code3rag.projects` tem um `id` bigint interno (alvo das FKs de `ciir_documents`,
`ciir_relations` e `code_query_feedback`) e um `public_id` UUIDv7. Esta API **só recebe e só
devolve o `public_id`**, chamado de `projectId`. `IProjectsRepository.GetByPublicIdAsync`
resolve o projeto, e os serviços usam o `id` interno a partir daí para escopar as consultas.

## Mapeamento de campos (`Project`, camada de aplicação)

| `Project` | `code3rag.public.projects` |
|---|---|
| `Id` (long, interno, nunca exposto) | `id` bigint PK identity |
| `PublicId` (Guid, exposto como `projectId`/`id`) | `public_id` uuid NOT NULL UNIQUE |
| `Name` | `name` text NOT NULL UNIQUE |
| `GitUrl` (`Uri?`) | `git_url` text, opcional — em branco é lido como `null` |
| `GitRawUrl` (`Uri?`) | `git_raw_url` text, opcional — em branco é lido como `null` |
| `CreatedAt` / `UpdatedAt` | `created_at` / `updated_at` timestamptz |

Não há modelo/dimensão de embedding por projeto: é configuração da implantação (ver
`01-schema-discovery.md`).

## Validação (`ProjectsService.ListAsync`)

- `name` (filtro): opcional, não pode ser vazio/branco quando informado, máx. 200 caracteres
  (`ProjectsService.MaxNameFilterLength`), match parcial case-insensitive.
- `page`: opcional, default 0 (zero-based), não pode ser negativo.
- `pageSize`: opcional, default 20, entre 1 e 100 (`DefaultPageSize`/`MaxPageSize`).

## MCP

`list_projects` (mirror do antigo `GET /projects`), com `name`/`page`/`pageSize` opcionais.
Devolve uma lista simples (sem envelope de paginação) de `{ id, name, createdAt, updatedAt }`,
onde `id` é o `public_id` (UUID) a ser usado como `projectId` em `query_project_code`. Não há
tools de criação/edição/exclusão — use a API REST do `code-ciir-indexer`.

## Verificação

- Testes de integração (Testcontainers) de `ProjectsRepository` (`SearchAsync` paginado,
  `GetByPublicIdAsync`).
- Testes de unidade de `ProjectsService` (validação do filtro e da paginação).
- Testes da tool `list_projects` em `CodeCiir.Mcp.Tests`.
- A cobertura de criação/atualização/exclusão vive em `code-ciir-indexer`
  (`ProjectStoreTests`, `{ListProjects,CreateProject,UpdateProject,DeleteProject}Tests`,
  `ProjectsControllerTests`).
