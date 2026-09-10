# Code CIIR API

API para consultar em linguagem natural o código indexado em `code3rag`, retornando não só
os trechos semanticamente mais similares (como o
[`code-rag-api`](https://forgejo.home.arpa/sauron/code-rag-api)), mas também o grafo de
relações estruturais de código — até dois níveis, todas as relações — a partir de cada
resultado.

Este repositório está na fase de planejamento. Todo o desenho evolutivo do que será
construído, fase a fase, vive em [`.specs/`](./.specs/00-roadmap.md) — comece por lá.

## Status

Ver [`.specs/00-roadmap.md`](./.specs/00-roadmap.md) para o roteiro completo e o status de
cada fase. O schema real de `code3rag` já foi confirmado por introspecção ao vivo (Fase 0
concluída — ver [`.specs/01-schema-discovery.md`](./.specs/01-schema-discovery.md)):
`code-ciir-indexer` já populou 21 projetos, 901 documentos e 4429 relações de código,
prontos para validar as próximas fases contra dado real. Nenhuma linha de código ainda foi
escrita — as fases 1-4 (bootstrap, projects, busca vetorial, grafo de relações) são o
caminho crítico até o requisito central deste serviço.
