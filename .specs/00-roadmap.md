# Plano de implementação — code-ciir-api

**Status: fases 0–14 concluídas; deploy em produção.** Este documento é o índice evolutivo de
todo o trabalho de construção do `code-ciir-api`. Cada fase abaixo tem um arquivo irmão em
`.specs/` no mesmo formato usado pelo repositório de referência
[`sauron/code-rag-api`](https://forgejo.home.arpa/sauron/code-rag-api) — narrativo, com
contexto, decisões confirmadas com o usuário, e uma seção final de verificação preenchida
*durante* a execução da fase (não antes).

## Contexto e objetivo

Construir uma API que segue a mesma forma do `code-rag-api`
(`https://code-rag-api.home.arpa/swagger/v1/swagger.json`), mas:

1. lê os dados de uma base **diferente**, `code3rag`
   (`jdbc:postgresql://192.168.1.212:5432/code3rag`), que é populada e possuída por outro
   serviço, [`code-ciir-indexer`](https://forgejo.home.arpa/sauron/code-ciir-indexer) (ver
   `.specs/01-schema-discovery.md`) — `code-ciir-api` é um **consumidor** desse schema, não o
   seu dono;
2. no endpoint de busca (`POST /api/code-queries`), além da busca semântica por vetor já
   existente no `code-rag-api`, expande cada resultado até **dois níveis do grafo de
   relações** entre entidades de código, incluindo **todas** as relações (sem filtrar por
   tipo);
3. os demais endpoints (feedback, stats, export) são **adaptados** à realidade das tabelas de
   `code3rag`, não copiados 1:1 do schema de `code2rag` usado por `code-rag-api`. O CRUD de
   projetos não faz parte desta API: pertence ao `code-ciir-indexer`.

O serviço é exposto pelo gateway compartilhado `https://blogdoft.home.arpa/code-brain`
(Traefik remove o prefixo `/code-brain` antes de encaminhar): `/code-brain/api/code-queries*`
e `/code-brain/mcp` chegam aqui como `/api/code-queries*` e `/mcp`.

## Por que seguir a arquitetura do code-rag-api

O `code-rag-api` já é um sistema em produção, com convenções maduras e testadas
(`.specs/code-queries-filters.md`, `.specs/query-feedback.md`, `.specs/reranking.md` do
repo de referência). Reaproveitar essas convenções reduz risco e mantém os dois serviços
operáveis pelo mesmo time com a mesma "forma mental":

- **Camadas**: `Api` (controllers, contratos REST, Problem Details) →
  `Application` (regras de negócio, `Result<T>`/`Failure` da lib `BlogDoFT.Libs.ResultPattern`)
  → `Infrastructure.Database` (Dapper/Npgsql cru, sem ORM/migrations) → `Mcp` (tools que
  chamam a Application diretamente, espelhando o REST).
- **Nunca roda migrations**: a API só lê/escreve nas tabelas que já existem; o DDL é do
  `code-ciir-indexer` (FluentMigrator), inclusive o da tabela de feedback.
- **Erros**: RFC 7807 Problem Details nos `400`; `401`/`403`/`404`/`5xx` sem corpo (ver
  `15-skills-alignment.md`). Falhas de domínio modeladas como `Failure` com `Code` prefixado
  pelo status HTTP (`"400-question-required"`, `"404-project-not-found"`, etc.), nunca
  exceções para fluxo de controle esperado.
- **JSON**: sempre `camelCase`, `application/json` estrito (sem outros content-types).
- **Testes**: unitários (NSubstitute/Bogus/Shouldly) na Application; integração real via
  Testcontainers (Postgres) na Infrastructure; `WebApplicationFactory` fim-a-fim na Api.
- **Specs evolutivas**: toda mudança de contrato relevante ganha um arquivo em `.specs/`
  *antes* de implementar (plano), e as specs existentes são mantidas coerentes com o código.

Onde `code-ciir-api` diverge dessas convenções (por ser consumidor de um schema alheio,
não dono dele), a divergência é uma decisão explícita, documentada na fase correspondente.

## Schema confirmado

A Fase 0 inspecionou `code3rag` (`192.168.1.212:5432`) ao vivo e cruzou o
resultado com as migrations autoritativas do serviço dono do schema —
[`sauron/code-ciir-indexer`](https://forgejo.home.arpa/sauron/code-ciir-indexer)
(`ciir-indexer`, tabelas `projects`/`ciir_documents`/`ciir_relations`/`indexing_runs`/
`ciir_uploads`/`code_query_feedback`). `code-ciir-api` só lê essas tabelas, com exceção de
`code_query_feedback`, onde grava o feedback.

Achado central para o requisito do grafo: `ciir_relations` já modela exatamente o que foi
pedido — arestas dirigidas com FK bigint real (`ON DELETE CASCADE`) para os nós em
`ciir_documents`, tipadas por `kind` (9 valores em produção: `calls`, `reads`,
`constructs`, `overrides`, `writes`, `implements`, `throws`, `inherits`, `catches`), com
índices cobrindo travessia nos dois sentidos. Dado real no momento da descoberta: 21 projetos,
901 documentos, 4429 relações, grau de saída médio 8.53 (máximo 48).

Projetos têm uma chave interna `id` (bigint, usada só nas FKs) e um identificador público
`public_id` (UUIDv7) — é o `public_id` que esta API recebe e devolve como `projectId`.

## Fases

| # | Spec | Escopo | Depende de | Status |
|---|------|--------|------------|--------|
| 0 | [`01-schema-discovery.md`](./01-schema-discovery.md) | Introspecção completa de `code3rag`: tabelas, colunas, FKs, índices (inclusive pgvector), volumetria | Acesso ao Postgres | **Concluído** |
| 1 | [`02-bootstrap-solution.md`](./02-bootstrap-solution.md) | Scaffold da solution .NET (`CodeCiir.*`), Docker, appsettings | — (não depende do schema) | **Concluído** |
| 2 | [`03-projects-endpoint.md`](./03-projects-endpoint.md) | Endpoints de projetos adaptados a `code3rag` — depois transferidos ao `code-ciir-indexer`; resta só a tool MCP `list_projects` | Fase 0 | **Concluído** (REST removido) |
| 3 | [`04-code-queries-baseline.md`](./04-code-queries-baseline.md) | Busca vetorial baseline (paridade com code-rag-api, sem grafo ainda) | Fase 0, 2 | **Concluído** |
| 4 | [`05-code-queries-relationship-graph.md`](./05-code-queries-relationship-graph.md) | **Requisito central**: expansão de até 2 níveis do grafo de relações, todas as relações | Fase 0, 3 | **Concluído** |
| 5 | [`06-code-query-feedback.md`](./06-code-query-feedback.md) | Paridade de feedback/stats/export | Fase 0, 4 | **Concluído** |
| 6 | [`07-mcp-tools.md`](./07-mcp-tools.md) | Tools MCP espelhando o REST (`list_projects`, `query_project_code`, `submit_code_query_feedback`) | Fase 2, 3, 4 | **Concluído** |
| 7 | [`08-ops-deployment.md`](./08-ops-deployment.md) | Forgejo CI/CD, imagem Docker, manifests k8s, ingress no gateway `blogdoft.home.arpa/code-brain` | Fase 1 | **Concluído** |
| 8 | [`09-code-queries-filters.md`](./09-code-queries-filters.md) | Filtros `kind`/`qualifiedName`, `projectId` opcional no body (sai da rota); embedding da pergunta passa a usar o modelo configurado na aplicação | Fase 3, 4 | **Concluído** (paginação revertida na Fase 9) |
| 9 | [`10-reranking.md`](./10-reranking.md) | Reranking opcional (Ollama + OpenAI-compatível) em `code-queries`; `size`/`page` revertidos para `limit` | Fase 8 | **Concluído** |
| 10 | [`11-validation-problem-details.md`](./11-validation-problem-details.md) | 400 de model binding passa a incluir `errors` nomeando o campo e a razão — afeta todos os endpoints | — | **Concluído** |
| 11 | [`12-match-relations.md`](./12-match-relations.md) | `matches[].relations` — relações diretas (1 hop, ambas direções) de cada match | Fase 4 | **Concluído** |
| 12 | [`13-mcp-code-source.md`](./13-mcp-code-source.md) | Tool MCP `get_code_source`: resolve o `id` de um match em `sourceFile` e `gitRawUrl` | Fase 6 | **Concluído** |
| 13 | [`14-keycloak-auth.md`](./14-keycloak-auth.md) | Autenticação opcional via Keycloak (`Keycloak:Enabled`) para os controllers REST — `/mcp` fica permanentemente fora do escopo de autenticação | — | **Concluído** |
| 14 | [`15-skills-alignment.md`](./15-skills-alignment.md) | Refactor para as skills `csharp-*`: contrato HTTP em `camelCase`, `401/403/5xx` sem corpo, tags kebab-case, tipos `*Table`, testes `Should_X_When_Y` | Fases 1-13 | **Concluído** |

Ordem de execução seguida: **0 → 1 → 2 → 3 → 4** foi o caminho crítico até satisfazer o
pedido original (endpoint de code-queries com grafo); as demais fases vieram depois.

## Decisões confirmadas com o usuário

- `code3rag` é de fato um banco distinto de `code2rag` (não um typo) — confirmado depois
  de a introspecção inicial ter, por engano, mirado `code2rag`.
- Credenciais de acesso a `code3rag` **nunca são commitadas em nenhum arquivo deste
  repositório** — vêm de secret (ver `01-schema-discovery.md`).
- **Projetos pertencem ao `code-ciir-indexer`**: o CRUD chegou a ser implementado aqui e foi
  transferido para lá, que é o único escritor da tabela `projects` (ver
  `03-projects-endpoint.md`). Esta API não expõe mais nenhuma rota REST de projetos; só a tool
  MCP `list_projects`, somente leitura.
- **Forma da resposta do grafo**: `{ matches, graph: { nodes, edges, truncated } }`, nós
  desduplicados (ver `05-code-queries-relationship-graph.md`).
- **Nomenclatura de campos**: expõe os nomes nativos do schema (`symbolContainer`,
  `symbolName`, `symbolQualifiedName`, `symbolCanonicalName`) em vez de
  `namespace`/`typeName`/`member` do code-rag-api (ver `04-code-queries-baseline.md`).
- **Onde vive o feedback**: tabela `code_query_feedback` dentro de `code3rag`, criada por
  migration do `code-ciir-indexer` (ver `06-code-query-feedback.md`).
- **Namespace .NET**: `CodeCiir.*`, espelhando `CodeRag.*`.
