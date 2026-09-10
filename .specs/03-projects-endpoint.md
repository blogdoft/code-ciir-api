# Fase 2 — `/api/v1/projects` adaptado a `code3rag`

**Status: concluído.** `GET /api/v1/projects[?name=]` e `GET /api/v1/projects/{projectId}`
implementados exatamente como proposto abaixo (read-only, sem `git_url`/`git_raw_url`,
com `embedding_model`/`embedding_dimensions`). 18 testes verdes (7 unit +
`ProjectsServiceTests`, 5 Testcontainers + `ProjectsRepositoryTests`, 6 HTTP end-to-end +
`ProjectsEndpointTests`, com `IProjectsService` substituído via NSubstitute).

**⚠️ Achado durante a implementação, não previsto no plano**: `[ApiController]` reescreve
um `NotFoundResult()` vazio em um corpo JSON de Problem Details automaticamente, a menos
que `ApiBehaviorOptions.SuppressMapClientErrors = true` seja configurado em `Program.cs`
— sem isso, `GET /projects/{id}` para um id inexistente devolvia 404 **com corpo**,
violando o contrato ("404 sem corpo"). Corrigido adicionando essa configuração (que a
Fase 1 tinha omitido por não ter nenhum 404 ainda), junto com
`InvalidModelStateResponseFactory` para uniformizar 400 de model-binding malformado com o
formato Problem Details das falhas de domínio.

## Contexto

O `code-rag-api` expõe CRUD completo de projetos porque ele **é dono** da tabela
`projects` em `code2rag` — projetos são criados via `POST /projects` antes de qualquer
indexação. Em `code-ciir-api`, `public.projects` em `code3rag` é populada por
`code-ciir-indexer`, um serviço externo a este (confirmado: 21 projetos já existem em
produção, um por artefato/csproj indexado).

## Decisão proposta: Projects é read-only em `code-ciir-api`

**A confirmar com o usuário antes de implementar.** Racional: se `indexer-api` já cria/
gerencia projetos como parte do seu próprio fluxo de indexação, `code-ciir-api` escrever
na mesma tabela (`POST`/`PUT`/`DELETE`) cria um cenário de dois escritores independentes
sem coordenação — risco de nomes duplicados, de `code-ciir-api` apagar um projeto que
`indexer-api` ainda está indexando, etc. Também é o padrão mais simples e mais seguro para
uma primeira versão.

Se confirmado, o contrato desta fase fica:

- `GET /api/v1/projects?name=` — mantido, idêntico em espírito ao code-rag-api (filtro
  parcial case-insensitive por nome).
- `GET /api/v1/projects/{projectId}` — mantido.
- `POST /api/v1/projects`, `PUT /api/v1/projects/{projectId}`,
  `DELETE /api/v1/projects/{projectId}` — **removidos** do contrato desta API (não
  expostos), ou implementados como `501 Not Implemented` explícito se o usuário preferir
  manter os paths no OpenAPI por paridade documental. Preferência: **remover** — um
  endpoint que sempre falha é pior sinal para um consumidor do que um endpoint ausente.

Se o usuário rejeitar essa proposta (quiser CRUD completo mesmo assim), esta seção deve
ser reescrita descrevendo o mecanismo de coordenação com `indexer-api` (ex.: `code-ciir-api`
só pode criar projetos que `indexer-api` ainda não conhece, ou os dois passam a usar um
`SELECT ... FOR UPDATE`/constraint de unicidade como árbitro) antes de implementar.

## Mapeamento de campos (confirmado via introspecção ao vivo em `code3rag`)

`public.projects` em `code3rag` **diverge** da forma do `code-rag-api` em pontos
importantes:

| `ProjectResponse` (code-rag-api) | `code3rag.public.projects` |
|---|---|
| `id` (int64) | `id` bigint PK identity |
| `name` (string, unique) | `name` text NOT NULL UNIQUE |
| `git_url` (string?, ≤2000) | **não existe** — remover do contrato, não expor como sempre-`null` |
| `git_raw_url` (string?, ≤2000) | **não existe** — mesma remoção; consequência direta: `CodeQueryResultResponse.gitUrl`/`gitRawUrl` também deixam de existir no contrato desta API (ver `04-code-queries-baseline.md`) |
| `created_at` (timestamptz) | `created_at` timestamptz NOT NULL default now() UTC |
| — (sem equivalente no code-rag-api) | **`embedding_model`** (text NOT NULL) — novo campo, expor no `ProjectResponse`: identifica qual modelo de embedding esse projeto usa, informação necessária para o cliente entender por que a mesma pergunta pode ter relevância diferente entre projetos com modelos distintos |
| — | **`embedding_dimensions`** (integer NOT NULL) — novo campo, mesma razão acima |
| — | **`updated_at`** (timestamptz NOT NULL) — novo campo, expor por completude |

Proposta de `ProjectResponse` para `code-ciir-api`: `id`, `name`, `embedding_model`,
`embedding_dimensions`, `created_at`, `updated_at` — sem `git_url`/`git_raw_url`.

## Erros

Mantém o padrão RFC 7807 do code-rag-api: 400 para `projectId` não-numérico/negativo
(reutilizar `RouteId.TryParsePositive`, ver `02-bootstrap-solution.md`), 404 sem corpo
para projeto inexistente, 500 para exceção não tratada. Sem 409 (não há mais `POST`/`PUT`
para colidir em nome).

## MCP

`list_projects` mantido (mirror de `GET /projects`), mesma assinatura do code-rag-api.

## Verificação de saída desta fase

- Testes de integração confirmando que `GET /projects` e `GET /projects/{id}` batem com
  dados reais existentes em `code3rag` (não dados sintéticos criados pelo próprio teste,
  já que este serviço não escreve nessa tabela — Testcontainers desta fase precisa seedar
  a tabela de projetos artificialmente com o schema real confirmado na Fase 0, não usar
  `code3rag` de produção nos testes).
