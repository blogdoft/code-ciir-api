# Code CIIR API

API para consultar em linguagem natural o código indexado em `code3rag`, retornando não só
os trechos semanticamente mais similares (como o
[`code-rag-api`](https://forgejo.home.arpa/sauron/code-rag-api)), mas também o grafo de
relações estruturais de código — até dois níveis, todas as relações — a partir de cada
resultado.

Todo o desenho evolutivo do serviço, fase a fase, vive em [`.specs/`](./.specs/00-roadmap.md) —
comece por lá.

## Status

Em produção atrás do gateway compartilhado `https://blogdoft.home.arpa/code-brain`:

- `POST /api/code-queries` — busca semântica + reranking + grafo de relações (2 hops);
- `POST /api/code-queries/feedback`, `GET /api/code-queries/feedback/stats`,
  `GET /api/code-queries/feedback/export` — feedback e relatórios;
- `GET /version`, `GET /health`;
- `/mcp` — tools MCP (`list_projects`, `query_project_code`, `get_code_source`,
  `submit_code_query_feedback`), sempre anônimo.

Projetos (CRUD) são responsabilidade do
[`code-ciir-indexer`](https://forgejo.home.arpa/sauron/code-ciir-indexer), dono do schema de
`code3rag`. Ver [`.specs/00-roadmap.md`](./.specs/00-roadmap.md) para o status de cada fase.
