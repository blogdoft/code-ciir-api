# Plano de implementação — code-ciir-api

**Status: rascunho — em elaboração.** Este documento é o índice evolutivo de todo o
trabalho de construção do `code-ciir-api`. Cada fase abaixo tem (ou terá) um arquivo irmão
em `.specs/` no mesmo formato usado pelo repositório de referência
[`sauron/code-rag-api`](https://forgejo.home.arpa/sauron/code-rag-api) — narrativo, com
contexto, decisões confirmadas com o usuário, e uma seção final de verificação preenchida
*durante* a execução da fase (não antes).

## Contexto e objetivo

Construir uma API que segue o mesmo contrato/forma do `code-rag-api`
(`https://code-rag-api.home.arpa/swagger/v1/swagger.json`), mas:

1. lê os dados de uma base **diferente**, `code3rag`
   (`jdbc:postgresql://192.168.1.212:5432/code3rag`), que é populada por outro serviço
   (`indexer-api`, ver `.specs/01-schema-discovery.md`) — `code-ciir-api` é, por padrão, um
   **consumidor** desse schema, não o seu dono;
2. no endpoint `POST /api/v1/projects/{projectId}/code-queries`, além da busca semântica
   por vetor já existente no `code-rag-api`, expande cada resultado até **dois níveis do
   grafo de relações** entre entidades de código, incluindo **todas** as relações (sem
   filtrar por tipo);
3. os demais endpoints (`projects`, feedback, etc.) são **adaptados** à realidade das
   tabelas de `code3rag`, não copiados 1:1 do schema de `code2rag` usado por `code-rag-api`.

## Por que seguir a arquitetura do code-rag-api

O `code-rag-api` já é um sistema em produção, com convenções maduras e testadas
(`.specs/code-queries-filters.md`, `.specs/query-feedback.md`, `.specs/reranking.md` do
repo de referência). Reaproveitar essas convenções reduz risco e mantém os dois serviços
operável pelo mesmo time com a mesma "forma mental":

- **Camadas**: `Api` (controllers, contratos REST, Problem Details) →
  `Application` (regras de negócio, `Result<T>`/`Failure` da lib `BlogDoFT.Libs.ResultPattern`)
  → `Infrastructure.Database` (Dapper/Npgsql cru, sem ORM/migrations) → `Mcp` (tools que
  chamam a Application diretamente, espelhando o REST).
- **Nunca roda migrations**: a API só lê/escreve nas tabelas que já existem; DDL local para
  dev fica em `db/init.sql`, nunca executado em produção pela própria API.
- **Erros**: RFC 7807 Problem Details em tudo, exceto 404 (sem corpo). Falhas de domínio
  modeladas como `Failure` com `Code` prefixado pelo status HTTP (`"400-question-required"`,
  `"404-project-not-found"`, etc.), nunca exceções para fluxo de controle esperado.
- **JSON**: sempre `snake_case`, `application/json` estrito (sem outros content-types).
- **Testes**: unitários (NSubstitute/Bogus/Shouldly) na Application; integração real via
  Testcontainers (Postgres) na Infrastructure; `WebApplicationFactory` fim-a-fim na Api.
- **Specs evolutivas**: toda mudança de contrato relevante ganha um arquivo em `.specs/`
  *antes* de implementar (plano) e é atualizado com "⚠️ correção" quando a implementação
  revela que uma premissa do plano estava errada — nunca reescrito silenciosamente.

Onde `code-ciir-api` diverge dessas convenções (por ser consumidor de um schema alheio,
não dono dele), a divergência é uma decisão explícita, documentada na fase correspondente.

## Schema confirmado

A Fase 0 foi concluída: `code3rag` (`192.168.1.212:5432`, usuário `fatlip`) foi
inspecionado ao vivo via `psql` (container Docker descartável, já que não há cliente
Postgres instalado localmente), e cruzado contra a migration autoritativa do serviço dono
do schema — [`sauron/code-ciir-indexer`](https://forgejo.home.arpa/sauron/code-ciir-indexer)
(`ciir-indexer`, tabelas `projects`/`ciir_documents`/`ciir_relations`/`indexing_runs`) —
`code-ciir-api` é consumidor, nunca escreve nessas tabelas. Uma tentativa anterior de
obter esse schema via a sessão irmã `indexer-api` trouxe, por engano de conexão, o schema
de `code2rag` (o banco do `code-rag-api`) — descartado, ver histórico em
`.specs/01-schema-discovery.md`.

Achado central para o requisito do grafo: `ciir_relations` já modela exatamente o que foi
pedido — arestas dirigidas com FK bigint real (`ON DELETE CASCADE`) para os nós em
`ciir_documents`, tipadas por `kind` (9 valores em produção: `calls`, `reads`,
`constructs`, `overrides`, `writes`, `implements`, `throws`, `inherits`, `catches`), com
índices cobrindo travessia nos dois sentidos. Dado real de produção: 21 projetos, 901
documentos, 4429 relações, grau de saída médio 8.53 (máximo 48).

**Todas as fases abaixo já refletem esse schema real**, não mais suposições.

## Fases

| # | Spec | Escopo | Depende de | Status |
|---|------|--------|------------|--------|
| 0 | [`01-schema-discovery.md`](./01-schema-discovery.md) | Introspecção completa de `code3rag`: tabelas, colunas, FKs, índices (inclusive pgvector), volumetria | Acesso ao Postgres | **Concluído** |
| 1 | [`02-bootstrap-solution.md`](./02-bootstrap-solution.md) | Scaffold da solution .NET (`CodeCiir.*`), Docker, appsettings, esqueleto de CI/CD | — (não depende do schema) | **Concluído** |
| 2 | [`03-projects-endpoint.md`](./03-projects-endpoint.md) | `GET/POST/PUT/DELETE /api/v1/projects[...]` adaptado a `code3rag` | Fase 0 | **Concluído** |
| 3 | [`04-code-queries-baseline.md`](./04-code-queries-baseline.md) | `POST .../code-queries` — busca vetorial baseline (paridade com code-rag-api, sem grafo ainda) | Fase 0, 2 | **Concluído** |
| 4 | [`05-code-queries-relationship-graph.md`](./05-code-queries-relationship-graph.md) | **Requisito central**: expansão de até 2 níveis do grafo de relações, todas as relações | Fase 0, 3 | **Concluído** |
| 5 | [`06-code-query-feedback.md`](./06-code-query-feedback.md) | Paridade opcional de feedback/stats/export | Fase 0, 4 | **Concluído** — DDL aplicada em `code3rag` real via migration em `code-ciir-indexer`, validado ponta a ponta |
| 6 | [`07-mcp-tools.md`](./07-mcp-tools.md) | Tools MCP espelhando o REST (`list_projects`, `query_project_code`, ...) | Fase 2, 3, 4 | **Concluído** |
| 7 | [`08-ops-deployment.md`](./08-ops-deployment.md) | Forgejo CI/CD, imagem Docker, manifests k8s, ingress `code-ciir-api.home.arpa` | Fase 1 | **Manifests concluídos e validados**; deploy real pendente |
| 8 | [`09-code-queries-filters.md`](./09-code-queries-filters.md) | Filtros `kind`/`qualifiedName`, paginação `size`/`page`, `projectId` opcional no body (sai da rota) em `code-queries`; embedding da pergunta passa a usar modelo configurado na aplicação | Fase 3, 4 | **Concluído** (paginação revertida na Fase 10) |
| 9 | [`10-reranking.md`](./10-reranking.md) | Reranking opcional (Ollama + OpenAI-compatível) em `code-queries`; `size`/`page` revertidos para `limit` (incompatíveis com reranking); reranking já ligado em produção | Fase 8 | **Concluído** |
| 10 | [`11-validation-problem-details.md`](./11-validation-problem-details.md) | 400 de model binding (corpo malformado/campo com tipo errado/propriedade desconhecida) passa a incluir `errors` nomeando o campo e a razão, em vez de um `detail` genérico — afeta todos os endpoints | — | **Concluído** |
| 11 | [`12-match-relations.md`](./12-match-relations.md) | `matches[].relations` — relações diretas (1 hop, ambas direções) de cada match, sempre completas, independente do truncamento do `graph` de 2 hops | Fase 4 | **Concluído** |

Ordem de execução recomendada: **0 → 1 → 2 → 3 → 4** é o caminho crítico até satisfazer o
pedido original (endpoint de code-queries com grafo). 5-7 podem ser paralelizados ou
adiados sem bloquear a entrega do requisito central.

## Decisões já confirmadas com o usuário

- `code3rag` é de fato um banco distinto de `code2rag` (não um typo) — confirmado depois
  de a introspecção inicial ter, por engano, mirado `code2rag`.
- Acesso a `code3rag` obtido via `psql` num container Docker descartável, com credenciais
  fornecidas diretamente pelo usuário no chat (usuário `fatlip`) — **a senha não deve ser
  commitada em nenhum arquivo deste repositório**; recomenda-se rotacioná-la após esta
  sessão, já que transitou por um canal de chat (ver `01-schema-discovery.md`).

## Decisões em aberto (a resolver durante as fases correspondentes, não bloqueiam o plano)

- **Projects é read-only ou CRUD completo em `code-ciir-api`?** `code-ciir-indexer` é quem
  cria/atualiza projetos em `code3rag`; `code-ciir-api` escrever na mesma tabela cria um
  cenário de dois escritores. Proposta em `03-projects-endpoint.md`: read-only por padrão.
- **Forma da resposta do grafo em code-queries**: nós aninhados por resultado vs. resposta
  em formato grafo (`{ matches, graph: { nodes, edges } }`). Proposta em
  `05-code-queries-relationship-graph.md`.
- **Nomenclatura de campos**: expor os nomes nativos do schema (`symbol_container`,
  `symbol_name`, `symbol_qualified_name`) em vez de forçar o encaixe em
  `namespace`/`type_name`/`member` do code-rag-api, que não existem em `code3rag`.
  Proposta em `04-code-queries-baseline.md`.
- **Onde vive o feedback**, já que `code-ciir-api` não deveria rodar migrations no schema
  de `code-ciir-indexer` — confirmado que não existe hoje nenhuma tabela de feedback em
  `code3rag`. Proposta em `06-code-query-feedback.md`.
- **Nome do namespace .NET** (`CodeCiir.*`, espelhando `CodeRag.*`) — usado neste plano
  como placeholder; ajustar se o usuário preferir outro.
