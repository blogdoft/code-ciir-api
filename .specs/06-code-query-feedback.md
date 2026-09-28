# Fase 5 — Feedback de efetividade (paridade com o code-rag-api)

**Status: concluído — submissão, stats e export.** Submissão validada de ponta a ponta contra
`code3rag` real (`201 Created`, linha confirmada em `public.code_query_feedback`).

## Contexto

`code-rag-api` tem submissão de feedback, estatísticas semanais e export CSV
(`.specs/query-feedback.md`, `.specs/code-query-feedback-stats.md`,
`.specs/code-query-feedback-export.md` do repo de referência). O pedido original do usuário não
mencionava feedback; a submissão entrou por paridade de contrato, e `stats`/`export` a pedido
explícito do usuário ("o code-rag-api tem um endpoint de report de feedback, faça a mesma
implementação aqui"), espelhando 1:1 o `FeedbackController` do `code-rag-api` — mesmas regras
de janela padrão/máxima, mesma grade densa semana × projeto, mesmo CSV com timezone opcional.

## Onde o feedback é gravado

`code-ciir-api` não é dono de `code3rag` — o `code-ciir-indexer` é. A decisão foi uma tabela
nova dentro de `code3rag`, com FK para `projects`, criada **por migration do
`code-ciir-indexer`** (`M20260909000000_AddCodeQueryFeedback`, FluentMigrator), mantendo a
convenção de que só o indexer roda DDL nessa base. Esta API só faz `INSERT`/`SELECT`.

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

CREATE INDEX ix_code_query_feedback_project_id_created_at
    ON public.code_query_feedback (project_id, created_at);
```

`project_id` é o `id` interno do projeto; o contrato HTTP/MCP usa sempre o `public_id` (UUID),
resolvido antes do `INSERT`/`SELECT`.

## Contrato

Todos os endpoints ficam sob `api/code-queries` (tag OpenAPI `code-queries`), JSON em
`camelCase`.

### `POST /api/code-queries/feedback`

```json
{
  "projectId": "0199a1b2-...-uuid",
  "question": "...",
  "useful": true,
  "similarities": [0.83, 0.71],
  "reason": null,
  "user": "jane.doe"
}
```

- `projectId` (UUID), `question` (máx. 1000), `useful`, `similarities` (pode ser vazio, máx.
  50 valores) e `user` (máx. 200) são obrigatórios; `reason` é opcional (máx. 1000).
- `201` com o registro criado (`id`, `projectId`, `question`, `useful`, `similarities`,
  `reason`, `user`, `createdAt`), sem `Location`.
- `400` (Problem Details) para campo ausente/inválido; `404` sem corpo para projeto
  inexistente. Não há `409`: submissões repetidas são aceitas.
- As `similarities` vêm de `matches` (só os matches têm similaridade, não o `graph`).

### `GET /api/code-queries/feedback/stats`

Query string opcional: `startDate`, `endDate`, `projectId`. Janela padrão de 30 dias
(`FeedbackService.DefaultWindowDays`), máxima de 366 (`MaxWindowDays`). Resposta: grade densa
semana × projeto (`{ startDate, endDate, weeks: [{ weekStart, weekEnd, projects: [{ projectId,
projectName, totalCount, usefulCount, notUsefulCount, usefulPercentage,
notUsefulPercentage }] }] }`) — semanas sem feedback aparecem zeradas.

### `GET /api/code-queries/feedback/export`

Mesmos filtros de `stats`, mais `timezone` (nome IANA) para renderizar `createdAt` no horário
local em vez de UTC. Resposta `text/csv` com as linhas brutas; os cabeçalhos do CSV continuam
em snake_case (`project_id`, `created_at`, ...).

Falhas de domínio: `InvalidDateRange`, `WindowTooLarge`, `InvalidTimezone` (`FeedbackFailures`)
e `ProjectNotFound` (`ProjectFailures`, reaproveitada).

## Implementação

- `CodeCiir.Application/Feedback`: `IFeedbackService` (`SubmitAsync`, `GetStatsAsync`,
  `ExportAsync`), `FeedbackStatsResult`, `WeeklyFeedbackStats`, `ProjectFeedbackStats`,
  `FeedbackExportResult`, `FeedbackExportRow`.
- `CodeCiir.Infrastructure.Database/Feedback/FeedbackRepository`: mesmo SQL do repo de
  referência (CTE `weeks` via `generate_series`/`date_trunc('week', ...)`, `CROSS JOIN` com
  projetos elegíveis, `LEFT JOIN` do feedback), devolvendo o `public_id` do projeto.
- `CodeCiir.Api`: submissão em `CodeQueriesController` (`POST feedback`); `stats`/`export` em
  `FeedbackController` (`[Route("api/code-queries/feedback")]`); export CSV em
  `Csv/FeedbackCsvExporter` (CsvHelper).
- MCP: só a submissão (`submit_code_query_feedback`, ver `07-mcp-tools.md`). `stats`/`export`
  são relatórios para consumo humano, sem tool MCP — mesma decisão do repo de referência.

## Verificação

- Unit (`FeedbackServiceTests`): validação de campos, projeto inexistente, janelas de data,
  timezone inválido.
- Testcontainers (`FeedbackRepositoryTests`): inserção e os casos de `GetStatsAsync`/
  `ExportAsync` do repo de referência (grade densa com semana pulada, exclusão fora da janela,
  filtro por projeto, todos os projetos quando sem filtro).
- HTTP (`CustomWebApplicationFactory` com `IFeedbackService` mockado via NSubstitute): 400 por
  campo obrigatório, 404 projeto inexistente, 201 com corpo ecoado, CSV lido de volta com
  CsvHelper. Diferente do repo de referência, os testes HTTP não tocam banco; a cobertura de
  SQL real fica inteira nos testes de repositório.
