# Fase 5 — Feedback de efetividade (paridade opcional)

**Status: concluído — submissão, stats e export, código e DDL.**
`POST /api/v1/projects/{projectId}/code-queries/feedback` implementado e testado
(20 testes: 9 unit `FeedbackServiceTests`, 2 Testcontainers `FeedbackRepositoryTests`,
3 HTTP `CodeQueryFeedbackEndpointTests` — dentro do total de 64 testes verdes do repo).

**Atualização — paridade completa com `code-rag-api` (`GET .../feedback/stats` e
`GET .../feedback/export`)**: a pedido explícito do usuário ("o code-rag-api tem um
endpoint de report de feedback, faça a mesma implementação aqui"), os dois endpoints de
leitura/relatório que a nota original abaixo deixava propositalmente fora de escopo foram
implementados, espelhando 1:1 o `FeedbackController` do `code-rag-api`
(`.specs/code-query-feedback-stats.md` e `.specs/code-query-feedback-export.md` do repo de
referência) — mesmas regras de janela padrão/máxima, mesma grade densa semana × projeto,
mesmo CSV com timezone opcional. Nenhuma tool MCP nova (mesma decisão do repo de
referência: são endpoints de relatório/dashboard para consumo humano, não parte do fluxo
de consulta de código dos agentes MCP).

Novo em `CodeCiir.Application/Feedback`: `IFeedbackService.GetStatsAsync`/`ExportAsync`,
`FeedbackService.DefaultWindowDays`/`MaxWindowDays`, `FeedbackStatsResult`,
`WeeklyFeedbackStats`, `ProjectFeedbackStats`, `FeedbackExportResult`, `FeedbackExportRow`,
e em `FeedbackFailures`: `InvalidDateRange`, `WindowTooLarge`, `InvalidTimezone` (métodos,
não propriedades, seguindo a convenção já estabelecida neste repo — `ProjectNotFound` já
existente em `ProjectFailures` é reaproveitado, sem duplicar em `FeedbackFailures` como o
repo de referência faz).

Novo em `CodeCiir.Infrastructure.Database/Feedback/FeedbackRepository`:
`GetStatsAsync`/`ExportAsync`, mesmo SQL do repo de referência (CTE `weeks` via
`generate_series`/`date_trunc('week', ...)`, `CROSS JOIN` com projetos elegíveis, `LEFT
JOIN` do feedback). Sem `ExistsForProjectAsync` — o repo de referência só usa isso para
bloquear `DELETE` de projeto com feedback associado, e `ProjectsService.DeleteAsync` deste
repo não tem (nem foi pedido) esse guard.

Novo em `CodeCiir.Api`: `Controllers/FeedbackController.cs`
(`[Route("api/v1/code-queries/feedback")]`, `GroupName = "Code Query"`, irmão de
`CodeQueriesController`, sem o warning S6960 deste porque não tem múltiplas
responsabilidades), `Contracts/CodeQueryFeedbackStatsResponse.cs`,
`WeeklyFeedbackStatsResponse.cs`, `ProjectFeedbackStatsResponse.cs`. Pacote `CsvHelper`
adicionado a `CodeCiir.Api.csproj` (para `/export`) e a `CodeCiir.Api.Tests.csproj` (para
o teste HTTP ler o CSV de volta).

**Divergência deliberada de teste em relação ao repo de referência**: `code-rag-api` testa
`stats`/`export` via `WebApplicationFactory` + Testcontainers reais (`ApiFixture`,
`CodeQueryFeedbackStatsEndpointTests`/`ExportEndpointTests` batem no Postgres de verdade).
Este repo segue a convenção já estabelecida em `CodeQueryFeedbackEndpointTests.cs`
existente: todo teste HTTP em `CodeCiir.Api.Tests` usa `CustomWebApplicationFactory` com
`IFeedbackService` mockado via NSubstitute (`ConfigureTestServices`/`RemoveAll`), sem tocar
banco algum — a cobertura de SQL real fica inteiramente em
`CodeCiir.Infrastructure.Database.Tests/Feedback/FeedbackRepositoryTests.cs`
(Testcontainers), que ganhou os mesmos 8 casos de `GetStatsAsync`/`ExportAsync` do repo de
referência (grade densa com semana pulada, exclusão fora da janela, filtro `project_id`,
todos os projetos quando sem filtro).

**Índice composto não adicionado**: o repo de referência adiciona
`ix_code_query_feedback_project_id_created_at` ao aplicar esta feature, já que a nova
consulta sempre filtra `created_at` dentro do grupo de `project_id`. Este repo não roda
DDL (nem mesmo na própria tabela `code_query_feedback` que "possui" conceitualmente — ver
seção "Problema específico" abaixo) — adicionar esse índice exigiria a mesma coordenação
com o dono de `code-ciir-indexer`/`code3rag` já mencionada ali, e fica deliberadamente fora
do escopo desta mudança (é uma otimização de performance, não de correção).

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
