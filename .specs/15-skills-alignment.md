# 15 — Alinhamento com as skills do projeto (`.claude/skills/csharp-*`)

Status: **Concluído** (refactor). Decisões de escopo confirmadas com o usuário antes de implementar.

## Por quê

Foram adicionadas seis skills de convenção C# (`csharp-api`, `csharp-domain`, `csharp-infra-database`,
`csharp-unit-test`, `csharp-worker`, `csharp-cli`). Este refactor alinha o código existente a elas,
**sem mudar regra de negócio** — o que muda é contrato HTTP, estrutura interna e testes.

## Escopo decidido

| Skill | Decisão |
|---|---|
| `csharp-api` | **Seguida por inteiro**, inclusive o que quebra contrato (abaixo). |
| `csharp-domain` | **Adiada**: não existe camada Core/Domain; `Project`, `CodeQueryResult` etc. seguem como records em `CodeCiir.Application`, e a validação segue nos services (o MCP depende dela). |
| `csharp-infra-database` | Tipos `*Table` com `ToDomain()`/`FromDomain()`; demais itens não se aplicam (ver "Divergências"). |
| `csharp-unit-test` | Todos os projetos de teste renomeados/reestruturados. |
| `csharp-worker`, `csharp-cli` | Não se aplicam: o projeto não tem `BackgroundService` nem CLI. |

## ⚠️ Mudanças de contrato HTTP (breaking)

- **JSON e query string em `camelCase`** (antes `snake_case`): `projectId`, `minSimilarity`,
  `symbolQualifiedName`, `gitRawUrl`, `relationType`, `startDate`, `endDate`, … Enums também
  (`qualifiedName.operator`: `equals` | `contains` | `notContains`). Um campo em `snake_case` no
  body vira `400` (o body rejeita propriedades desconhecidas); uma query string em `snake_case` é
  **ignorada em silêncio**. Só o CSV do export mantém seus cabeçalhos (`project_id`, `created_at`, …).
- **`500` sem corpo** (antes `application/problem+json` com tipo/mensagem/stack trace da exceção —
  vazamento). O detalhe vai só para o log.
- **`401` e `403` sem corpo** (antes o `401` trazia Problem Details). `401` mantém
  `WWW-Authenticate: Bearer`. O `403` registra o *username* (`preferred_username` → `name` → `sub`).
  `404` segue sem corpo; `400` segue `application/problem+json`.
- **Tags do OpenAPI em kebab-case**: `code-queries` e `version` (antes `Code Query` / `Version`).
- Erros de validação (`errors`) agora vêm com chaves em camelCase e mensagens que citam o campo em
  camelCase; validações de forma (obrigatório, tamanho, faixa) passam a ser feitas no DTO
  (DataAnnotations) por um `ModelValidationFilter` global, antes do use case.

As **tools MCP** não foram alteradas (a skill cobre o adapter HTTP): nomes `snake_case` de tools e o
formato dos resultados continuam como em `07-mcp-tools.md` / `13-mcp-code-source.md`.

## Mudanças estruturais

- `Program.cs` enxuto; registro por feature em `Extensions/` (`AddApiControllers`,
  `AddSwaggerDocumentation`/`UseSwaggerDocumentation`, `AddObservability`,
  `AddAiProviderServices`, `AddMcpServices`, `MapHealthProbe`).
- Mapeamento Application → DTO nos próprios DTOs (`XxxResponse.From(...)`); export CSV extraído do
  controller para `Csv/FeedbackCsvExporter`.
- Mapeamento `Failure` → HTTP continua central (`Problems/FailureResults`), agora também para
  `401/403/5xx`, com log de aplicação.
- OpenAPI: exemplos de request (`RequestExamplesSchemaFilter`) e respostas `401`/`403` documentadas
  quando o Keycloak está ligado.
- **Observabilidade**: Serilog + OpenTelemetry manual substituídos por
  `BlogDoFT.Libs.Api.OpenTelemetry` (seção `Observability`), mais `AddNpgsql()` para spans do
  Postgres. Logs seguem em stdout como JSON estruturado (`AddJsonConsole`). Mudança de deploy: o
  `configmap.yaml` troca as variáveis `OTEL_*` por `Observability__*` + `ApplicationName`. O
  *request logging* do Serilog deixou de existir (o nível `Microsoft.AspNetCore` segue `Warning`);
  a visibilidade de requisições vem dos traces, e falhas `4xx/5xx` mapeadas são logadas pela app.
- Infra: `ProjectTable` e `CodeQueryFeedbackTable` (`ToDomain()`; `FromDomain()` só onde há escrita).
  `IFeedbackRepository.InsertAsync` passou a receber um `NewFeedback` em vez de 6 parâmetros.
  Demais tipos de linha são projeções (`*Projection`).

## Divergências deliberadas das skills

- **`IDatabaseFacade`** (`BlogDoFT.Libs.DapperUtils.Abstractions`) não é usado: não aceita
  `CancellationToken` e o projeto precisa do `NpgsqlDataSource` com pgvector (`UseVector()`). Os
  repositórios seguem com `NpgsqlDataSource` + `CommandDefinition`, mas usam `WhereBuilder` /
  `PaginatedSqlBuilder` da mesma lib.
- **Chave primária numérica exposta na API / UUID7**: o schema é do `code-ciir-indexer`
  (ver `01-schema-discovery.md`); não é decisão deste repositório.
- **Migrations / `IWarmUpCommand`**: esta API nunca roda migrations (ver `00-roadmap.md`).
- **Controllers em vez de minimal APIs**: mantidos — há filtros globais, agrupamento por rota e
  `[ApiController]`; misturar estilos na mesma feature seria pior.

## Testes

Nomes `Should_ExpectedResult_When_Scenario`; um arquivo por classe testada, ou pasta
`<Classe>Tests/` com `Base<Classe>Tests` + uma classe por método público; Given/When/Then com linha
em branco; dados via `Faker<T>` (Bogus); asserções de `Result` verificam a falha inteira
(`ShouldBeFailure`). Novos testes: `ModelValidationFilter`, `UnhandledExceptionFilter`,
`FailureResults`, `ClaimsPrincipalExtensions`, respostas `401/403` do Keycloak, tags/exemplos do
OpenAPI, ausência de `snake_case` na resposta.
