# Fase 6 — Tools MCP

**Status: concluído.** Servidor MCP montado em `/mcp` (Streamable HTTP, stateless) via
`AddMcpServer().WithHttpTransport()` e `app.MapMcp("/mcp")`, sempre anônimo (ver
`14-keycloak-auth.md`). No gateway, é exposto em `https://blogdoft.home.arpa/code-brain/mcp`.
Smoke test manual: app sobe com `/mcp` registrado (`GET /mcp` → 405, esperado para
Streamable HTTP, que só aceita `POST`), sem exceção no startup.

## Contexto

`code-rag-api` expõe `list_projects`, `query_project_code` e `submit_code_query_feedback`
via MCP, cada uma chamando a camada `Application` diretamente (sem passar pelo controller
HTTP). Mesma convenção adotada aqui.

## Tools

| Tool | Espelha | Parâmetros |
|---|---|---|
| `list_projects` | leitura de projetos (`03-projects-endpoint.md`) | `name?`, `page?`, `pageSize?` |
| `query_project_code` | `POST /api/code-queries` | `question`, `projectId?` (UUID), `minSimilarity?`, `kind?`, `qualifiedNameOperator?`, `qualifiedNameValue?`, `limit?` |
| `get_code_source` | — (só MCP, ver `13-mcp-code-source.md`) | `documentId` |
| `submit_code_query_feedback` | `POST /api/code-queries/feedback` | `projectId` (UUID), `question`, `useful`, `similarities`, `user`, `reason?` |

- **`list_projects`** é somente leitura — não há tool de criação/edição de projeto (o CRUD é
  do `code-ciir-indexer`). Devolve `id` (UUID), `name`, `createdAt`, `updatedAt`; o `id` é o
  `projectId` das demais tools.
- **`query_project_code`** devolve `{ matches, graph }` (ver
  `05-code-queries-relationship-graph.md` e `12-match-relations.md`), não um array simples
  como no code-rag-api. O `[Description]` da tool deixa isso explícito para o agente
  consumidor: `matches` com as `relations` diretas de cada match, `graph` com até 2 hops,
  ambos só preenchidos quando `projectId` é informado, e a indicação de usar
  `get_code_source` com um `matches[].id` para obter o arquivo.
- **`submit_code_query_feedback`** exige que `user` seja o nome do próprio agente/ferramenta
  que chama.
- Falhas de domínio (`Failure`) viram `McpException` com a mensagem da falha.
- Os resultados das tools são serializados pelo serializador padrão do SDK MCP
  (`camelCase`); o refactor da Fase 14 não alterou nomes nem formatos das tools.

## Verificação

- `ProjectToolsTests`/`CodeQueryToolsTests` espelhando os testes de passthrough do
  code-rag-api (parâmetros repassados para a Application, falha de domínio vira
  `McpException`).
- Smoke test manual do servidor MCP local (`claude mcp add --transport http code-ciir
  http://localhost:PORT/mcp`) confirmando que `query_project_code` retorna a forma
  `{ matches, graph }` e que um cliente consegue navegar `graph.edges` a partir de um `match`.
