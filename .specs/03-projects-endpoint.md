# Fase 2 — `/api/v1/projects` adaptado a `code3rag`

**Status: concluído, revisado.** `GET /api/v1/projects[?name=][&page=][&page_size=]` (paginado) e
`GET /api/v1/projects/{projectId}` estão implementados. Somente leitura - `POST`/`PUT`/`DELETE`
foram implementados (ver "Reversão" abaixo para o histórico daquela decisão) e depois **movidos
para `code-ciir-indexer`**, que agora expõe seu próprio CRUD completo em `POST/PUT/DELETE
/api/projects` (mesmo dono da tabela `projects`, mesma tabela `code3rag`). Essa segunda reversão
elimina o risco de dois escritores independentes sem coordenação que a seção "Reversão" abaixo
documentava como aceito conscientemente - `code-ciir-indexer` volta a ser o único escritor. Este
serviço lê a mesma tabela sem modificá-la, exatamente como um projeto criado/gerenciado
externamente sempre foi o modelo pretendido para `code-ciir-api`.

**⚠️ Achado durante a implementação original, não previsto no plano**: `[ApiController]` reescreve
um `NotFoundResult()` vazio em um corpo JSON de Problem Details automaticamente, a menos
que `ApiBehaviorOptions.SuppressMapClientErrors = true` seja configurado em `Program.cs`
— sem isso, `GET /projects/{id}` para um id inexistente devolvia 404 **com corpo**,
violando o contrato ("404 sem corpo"). Corrigido adicionando essa configuração (que a
Fase 1 tinha omitido por não ter nenhum 404 ainda), junto com
`InvalidModelStateResponseFactory` para uniformizar 400 de model-binding malformado com o
formato Problem Details das falhas de domínio. Esse mesmo achado foi replicado em
`code-ciir-indexer` ao mover o CRUD para lá, já que seu `ProjectsController` também retorna
`NotFoundResult()` vazio para os mesmos casos.

## Contexto

O `code-rag-api` expõe CRUD completo de projetos porque ele **é dono** da tabela
`projects` em `code2rag` — projetos são criados via `POST /projects` antes de qualquer
indexação. Em `code-ciir-api`, `public.projects` em `code3rag` é populada por
`code-ciir-indexer`, um serviço externo a este (confirmado: 21 projetos já existem em
produção, um por artefato/csproj indexado).

## Decisão original (Fase 2): Projects somente leitura — **revertida**

A primeira versão desta fase implementou apenas `GET /api/v1/projects[?name=]` e
`GET /api/v1/projects/{projectId}`, com o racional de que escrever na mesma tabela que
`code-ciir-indexer` gerencia criaria dois escritores independentes sem coordenação (risco
de nomes duplicados, de `code-ciir-api` apagar um projeto que `indexer-api` ainda está
indexando, etc.).

**Essa decisão foi revertida a pedido explícito do usuário**: o CRUD completo
(`POST`/`PUT`/`DELETE`) foi implementado mesmo assim, sem um mecanismo de coordenação
formal com `code-ciir-indexer` (ex.: lock distribuído, fila de eventos). O risco de dois
escritores sem coordenação descrito acima **permanece real e não foi mitigado** por esta
implementação — ele foi conscientemente aceito pelo usuário como tradeoff aceitável para
obter CRUD completo agora. Pontos a que qualquer consumidor/operador deste serviço deve
atentar:

- `code-ciir-api` e `code-ciir-indexer` podem, em teoria, tentar criar/atualizar um projeto
  de mesmo nome quase simultaneamente. A checagem de unicidade em `ProjectsService` (via
  `IProjectsRepository.ExistsByNameAsync`) faz um *check-then-act* não atômico — não há
  `SELECT ... FOR UPDATE`, advisory lock, nem retry em caso de violação de constraint
  única (`ux_projects_name`). Numa colisão de timing real, o segundo `INSERT` simplesmente
  falha com uma exceção do Npgsql não tratada (500), não um 409 "bonito".
- `DELETE /api/v1/projects/{projectId}` apaga a linha de `projects` sem tocar
  `ciir_documents`/`ciir_relations` (não há `ON DELETE CASCADE` de `projects` para essas
  tabelas, conforme `01-schema-discovery.md` — as FKs em cascata são de `ciir_relations`
  para `ciir_documents`, não de `ciir_documents`/`ciir_relations` para `projects`). Isso
  deixa `ciir_documents`/`ciir_relations` órfãos apontando para um `project_id` inexistente
  se um projeto for deletado por esta API enquanto ainda tem documentos indexados. Nenhuma
  validação/bloqueio foi adicionado para impedir isso.
- Se `code-ciir-indexer` reprocessar/recriar um projeto que esta API deletou (ou
  vice-versa), o comportamento resultante depende inteiramente de timing, não de um
  protocolo acordado entre os dois serviços.

Se esses riscos se tornarem um problema real em produção, a mitigação recomendada (fora do
escopo desta fase) é ou (a) voltar ao read-only original neste serviço, ou (b) desenhar um
mecanismo de coordenação explícito com o time de `code-ciir-indexer` (fila de eventos,
lock distribuído, ou uma flag de "gerenciado externamente" por projeto).

## Contrato implementado

- `GET /api/v1/projects?name=&page=&page_size=` — paginado (page zero-based, default 0;
  page_size default 20, máx. 100), com filtro parcial case-insensitive por nome. Resposta
  200 traz um envelope `{items, page, page_size, total_count, total_pages}` (antes era um
  array simples — mudança de contrato em relação à versão read-only original).
- `GET /api/v1/projects/{projectId}` — mantido.
- `POST`/`PUT`/`DELETE /api/v1/projects` — **removidos**. Use os equivalentes em
  `code-ciir-indexer` (`POST/PUT/DELETE /api/projects`), que é quem possui a tabela.

## Mapeamento de campos (confirmado via introspecção ao vivo em `code3rag`)

`public.projects` em `code3rag` **diverge** da forma do `code-rag-api` em pontos
importantes:

| `ProjectResponse` (code-rag-api) | `code3rag.public.projects` |
|---|---|
| `id` (int64) | `id` bigint PK identity |
| `name` (string, unique) | `name` text NOT NULL UNIQUE |
| `git_url` (string?, ≤2000) | **não existe** — removido do contrato, não exposto como sempre-`null` |
| `git_raw_url` (string?, ≤2000) | **não existe** — mesma remoção; consequência direta: `CodeQueryResultResponse.gitUrl`/`gitRawUrl` também não existem no contrato desta API (ver `04-code-queries-baseline.md`) |
| `created_at` (timestamptz) | `created_at` timestamptz NOT NULL default now() UTC |
| — (sem equivalente no code-rag-api) | **`embedding_model`** (text NOT NULL) — identifica qual modelo de embedding esse projeto usa, informação necessária para o cliente entender por que a mesma pergunta pode ter relevância diferente entre projetos com modelos distintos |
| — | **`embedding_dimensions`** (integer NOT NULL) — mesma razão acima |
| — | **`updated_at`** (timestamptz NOT NULL) — exposto por completude |

`ProjectResponse` em `code-ciir-api`: `id`, `name`, `embedding_model`,
`embedding_dimensions`, `created_at`, `updated_at` — sem `git_url`/`git_raw_url`. Usado
apenas nas respostas de leitura (`GET`).

## Validação (camada de aplicação, `ProjectsService`)

- `name` (filtro de busca): opcional, não pode ser vazio/branco quando informado, máx. 200
  caracteres (`ProjectsService.MaxNameFilterLength`).
- `page`: opcional, default 0, não pode ser negativo.
- `page_size`: opcional, default 20, deve estar entre 1 e 100
  (`ProjectsService.DefaultPageSize`/`MaxPageSize`).

Validação de `name`/`embedding_model`/`embedding_dimensions` para criação/atualização, e a
checagem de unicidade de nome, agora vivem em `code-ciir-indexer`
(`Ciir.Indexer.Application.UseCases.ProjectValidation`/`CreateProject`/`UpdateProject`).

## Erros

Mantém o padrão RFC 7807: 400 para `name`/`page`/`page_size` inválidos ou `projectId`
não-numérico/negativo (reutiliza `RouteId.TryParsePositive`, ver `02-bootstrap-solution.md`),
404 sem corpo para projeto inexistente (`GET`), 500 para exceção não tratada.

## MCP

`list_projects` mantido (mirror de `GET /projects`), com `page`/`page_size` opcionais
espelhando a paginação da API REST; segue retornando uma lista simples (sem o envelope de
paginação do REST). Não há ferramentas MCP para `create`/`update`/`delete` - use a API REST
de `code-ciir-indexer` para essas operações.

## Verificação de saída desta fase

- Testes de integração (Testcontainers) cobrindo `SearchAsync` (paginado) e `GetByIdAsync` —
  `ProjectsRepositoryTests`. `ExistsByNameAsync`/`InsertAsync`/`UpdateAsync`/`DeleteAsync`
  moveram para `Ciir.Indexer.Infrastructure.PostgreSql.Tests.ProjectStoreTests` em
  `code-ciir-indexer`.
- Testes de unidade de `ProjectsService` cobrindo validação do filtro e paginação —
  `ProjectsServiceTests`. A cobertura de criação/atualização/exclusão moveu para
  `Ciir.Indexer.Application.Tests.UseCases.{ListProjects,CreateProject,UpdateProject,DeleteProject}Tests`
  em `code-ciir-indexer`.
- Testes HTTP end-to-end (`IProjectsService` substituído via NSubstitute) cobrindo os dois
  verbos de leitura — `ProjectsEndpointTests`. A cobertura HTTP dos verbos de escrita moveu
  para `Ciir.Indexer.Api.Tests.Controllers.ProjectsControllerTests` em `code-ciir-indexer`.
- Suíte completa (`dotnet test`) verde após a mudança, incluindo os testes de integração
  reais contra Postgres via Testcontainers, em ambos os repositórios.
