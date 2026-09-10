# Fase 5 — Feedback de efetividade (paridade opcional)

**Status: concluído — código e DDL, ambos em produção.**
`POST /api/v1/projects/{projectId}/code-queries/feedback` implementado e testado
(20 testes: 9 unit `FeedbackServiceTests`, 2 Testcontainers `FeedbackRepositoryTests`,
3 HTTP `CodeQueryFeedbackEndpointTests` — dentro do total de 64 testes verdes do repo).
`stats`/`export` **não implementados** — permanecem fora de escopo, ver nota original
abaixo.

**Atualização — DDL aplicada em `code3rag` real via `code-ciir-indexer`**: a tabela abaixo
foi criada por uma migration FluentMigrator no repositório dono do schema
(`sauron/code-ciir-indexer`,
`M20260909000000_AddCodeQueryFeedback.cs`, commit `afe2f3f`), rodando o próprio
`Ciir.Indexer.Api` contra `code3rag` — não foi criada diretamente por SQL solto nem pelo
`code-ciir-api`, mantendo a convenção de que só `code-ciir-indexer` roda DDL nessa base.
**Validado de ponta a ponta**: `POST .../code-queries/feedback` contra `code3rag` real
(projeto id 1) → `201 Created`, linha confirmada em
`SELECT * FROM public.code_query_feedback` no Postgres real. Decisão original de desenho
(tabela nova, FK para `projects`) mantida sem alteração; commit local ainda não passou por
`git push` (não solicitado).

```sql
CREATE TABLE public.code_query_feedback (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    project_id bigint NOT NULL REFERENCES public.projects(id),
    question text NOT NULL,
    useful boolean NOT NULL,
    similarities float8[] NOT NULL,
    reason text,
    username text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
```

## Contexto

`code-rag-api` tem `POST .../code-queries/feedback` +
`GET .../code-queries/feedback/stats` + `GET .../code-queries/feedback/export`
(`.specs/query-feedback.md`, `.specs/code-query-feedback-stats.md`,
`.specs/code-query-feedback-export.md` do repo de referência). O pedido original do
usuário não menciona feedback explicitamente — esta fase existe só para paridade de
contrato com o OpenAPI de referência, e pode ser adiada indefinidamente sem impacto no
requisito central (grafo de relações).

## Problema específico de `code-ciir-api`: onde escrever

`code-rag-api` é dono do schema onde grava feedback. `code-ciir-api` não é dono de
`code3rag` (é consumidor de `code-ciir-indexer` — confirmado por introspecção, ver
`01-schema-discovery.md`: **não existe nenhuma tabela de feedback em `code3rag` hoje**).
Escrever uma tabela nova (`code_query_feedback` ou equivalente) dentro de `code3rag` é a
única forma de manter o mesmo modelo relacional (FK para `projects`), mas exige uma de
duas coisas:
1. Permissão de escrita/DDL em `code3rag` para `code-ciir-api` (mesmo que restrita a uma
   tabela nova, claramente separada das tabelas de `code-ciir-indexer`) — requer
   alinhamento com quem administra `code3rag`/`code-ciir-indexer`, fora do controle deste
   plano.
2. Uma base separada só para feedback (menos acoplamento, mas perde o `JOIN`/FK direto com
   projetos de `code3rag` — feedback ficaria só com `project_id` solto, sem integridade
   referencial garantida pelo banco).

**Proposta**: opção 1 (tabela nova, adicional, dentro de `code3rag`, com FK para a tabela
de projetos), condicionada a confirmar com o dono de `indexer-api`/`code3rag` que isso é
aceitável. Mesma convenção do code-rag-api de "a API nunca roda migrations" se aplica
igual aqui — mesmo sendo uma tabela que `code-ciir-api` "possui" conceitualmente, o DDL é
aplicado manualmente/documentado, nunca por código de startup.

Esta decisão fica **em aberto** até essa conversa acontecer; esta fase não deve ser
iniciada antes disso.

## Contrato (se prosseguir)

Mesma forma do code-rag-api, ajustada para incluir os `similarity` retornados
(inalterado — continuam vindo de `matches`, não de `graph`, já que só os matches têm
similaridade):

```
POST /api/v1/projects/{projectId}/code-queries/feedback
{
  "question": "...",
  "useful": true,
  "similarities": [0.83, 0.71],
  "reason": null,
  "user": "ftathiago"
}
```

`stats`/`export` (agregação semanal, CSV) só entram depois que o endpoint de submissão
estiver estável — não há motivo para adiantar essas duas antes de ter dados reais fluindo.

## Verificação de saída desta fase

- Confirmação explícita (fora desta spec) de que `code-ciir-api` pode ter uma tabela
  própria dentro de `code3rag`.
- Testes espelhando `.specs/query-feedback.md` do code-rag-api (400 por campo obrigatório,
  404 projeto inexistente, 201 com corpo ecoado, sem `Location`).
