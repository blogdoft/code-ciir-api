# Fase 6 — Tools MCP

**Status: concluído.** `list_projects`, `query_project_code`, `submit_code_query_feedback`
implementadas exatamente como propostas, montadas em `/mcp` (Streamable HTTP, stateless)
via `AddMcpServer().WithHttpTransport().AddCodeCiirTools()`. 6 testes verdes
(`ProjectToolsTests`, `CodeQueryToolsTests` — passthrough de parâmetros e mapeamento
`Failure` → `McpException`). Smoke test manual: app sobe com `/mcp` registrado
(`GET /mcp` → 405, esperado para Streamable HTTP que só aceita `POST`), sem exceção no
startup.

## Contexto

`code-rag-api` expõe `list_projects`, `query_project_code` e
`submit_code_query_feedback` via MCP (Streamable HTTP, stateless, `/mcp`), cada uma
chamando a camada `Application` diretamente (sem passar pelo controller HTTP). Mesma
convenção adotada aqui.

## Tools propostas

- **`list_projects`** — mirror de `GET /projects` (Fase 2). Se Projects for read-only
  (proposta da Fase 2), a tool também é só leitura — sem tool de criação/edição de
  projeto.
- **`query_project_code`** — mirror de `POST .../code-queries` (Fases 3+4). **Mudança de
  forma da resposta em relação ao code-rag-api**: passa a devolver `{ matches, graph }`
  (ver `05-code-queries-relationship-graph.md`), não mais um array simples de resultados —
  o `[Description]` da tool precisa deixar isso explícito para o agente consumidor
  (ex.: "retorna matches por similaridade semântica **e** o grafo de relações de código
  até 2 saltos a partir deles — use `graph.edges`/`graph.nodes` para navegar contexto
  estrutural além do texto do match").
- **`submit_code_query_feedback`** — só faz sentido se a Fase 5 (feedback) for adiante;
  caso contrário, omitida do MCP também.

## Verificação de saída desta fase

- `CodeQueryToolsTests`/`ProjectToolsTests` espelhando os testes de passthrough do
  code-rag-api (parâmetros repassados corretamente para a Application, falha de domínio
  vira `McpException`).
- Smoke test manual do servidor MCP local (`claude mcp add --transport http code-ciir
  http://localhost:PORT/mcp`) confirmando que `query_project_code` retorna a nova forma
  de resposta e que um cliente consegue navegar `graph.edges` a partir de um `match`.
