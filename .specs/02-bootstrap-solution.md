# Fase 1 — Scaffold da solution .NET

**Status: concluído.** Esta fase criou o esqueleto da solution com o mínimo de lógica
necessário para ter algo de ponta a ponta testável; os projetos de Embeddings, Reranking e MCP
foram criados nas fases que os usam pela primeira vez (ver "Decisões de escopo").

## Contexto

O diretório de trabalho só tinha `.editorconfig` e `stylecop.json` (herdados/copiados do
`code-rag-api`, mesmo estilo de código) — nenhuma solution .NET existia.

## Nomenclatura (confirmada)

`CodeRag.*` → `CodeCiir.*`, sem objeção do usuário. A solution é `CodeCiir.slnx` (formato XML
gerado por padrão pelo SDK do .NET 10 em `dotnet new sln`); `dotnet build`/`dotnet test`/
`dotnet sln add` funcionam da mesma forma, só o nome do arquivo muda nos comandos.

## Projetos criados nesta fase

Todos com `net10.0`.

- **`CodeCiir.Api`** (`Microsoft.NET.Sdk.Web`) — `Program.cs`, controllers com JSON em
  `camelCase`, `UnhandledExceptionFilter`, Swashbuckle, `VersionController`/`VersionResponse`/
  `AppVersion` (`GET /version`, sem prefixo `/api`, health-check-style — mesmo padrão do
  code-rag-api), `Problems/`, `OpenApi/ControllerTagDescriptionsDocumentFilter`.
- **`CodeCiir.Application`** — `ServiceCollectionExtensions.AddApplication()`, vazio nesta
  fase (o primeiro serviço chega na Fase 2).
- **`CodeCiir.Infrastructure.Database`** — `ServiceCollectionExtensions.
  AddDatabaseInfrastructure()` registrando só `NpgsqlDataSource` (resolução preguiçosa da
  connection string, mesmo padrão do code-rag-api).

## `tests/`

Criado apenas **`CodeCiir.Api.Tests`** (`WebApplicationFactory`), com um teste end-to-end
de `GET /version`. `CustomWebApplicationFactory` sobrescreve `ConnectionStrings:Database`
com um valor sintaticamente válido mas inalcançável — nenhum teste desta fase toca o
banco de verdade. `CodeCiir.Application.Tests`/`CodeCiir.Infrastructure.Database.Tests`
foram criados na Fase 2, quando passou a haver regra de negócio para testar.

## Configuração

- `ConnectionStrings:Database` **não está em nenhum arquivo commitado** (nem
  `appsettings.Development.json`) — só via `dotnet user-secrets`/variável de ambiente
  local, ou Kubernetes Secret em produção (ver `08-ops-deployment.md`).
- Seções `Embeddings`/`Reranking` entram nas fases que as usam (Fases 3 e 9).

## Docker / dev local

`.eng/docker/docker-compose.yml` só com o serviço `api` — sem Postgres local (a API lê
`code3rag` remoto). A connection string vem de `${CIIR_CONNECTION_STRING}` (variável de
ambiente do host, não commitada). O `Dockerfile` também vive em `.eng/docker/`; o build
context é a raiz do repositório (onde fica o `.dockerignore`).

## CI/CD

Ver `08-ops-deployment.md`.

## Decisões de escopo

1. **`CodeCiir.Embeddings.*`/`CodeCiir.Mcp`/`CodeCiir.Reranking.*` não foram criados nesta
   fase**, e sim junto com o primeiro consumidor real: Embeddings na Fase 3
   (`CodeQueryService`), Mcp na Fase 6, Reranking na Fase 9. Criá-los antes seria scaffolding
   morto, sujeito a retrabalho.
2. **Sem checagem de conectividade com `code3rag` embutida no startup do app.** Um fail-fast
   tipo `GetRequiredService<NpgsqlDataSource>()` logo após `app.Build()` acoplaria todo
   `dotnet test`/boot do `WebApplicationFactory` a uma dependência de rede externa
   (`192.168.1.212`, só alcançável na rede local do usuário), quebrando a hermeticidade de
   testes/CI. A verificação de conectividade desta fase foi um passo manual único (script
   descartável). O startup valida só configuração (ex.: `ValidateProviderConfiguration()`
   para embeddings/reranking).
3. **Bibliotecas adicionadas quando usadas**: `BlogDoFT.Libs.ResultPattern` na Fase 2,
   `CsvHelper` na Fase 5 (export de feedback), `BlogDoFT.Libs.Api.OpenTelemetry` na Fase 14.
   Os logs são JSON estruturado via Serilog (`Logging/StructuredLoggingExtensions`), único
   sink registrado, com uma linha por requisição (exceto `/health`).
4. **Sem hook Husky/pre-push** provisionado pelo build: o `.config/dotnet-tools.json`
   referencia `husky`, mas o target MSBuild de provisionamento automático do code-rag-api não
   foi adotado aqui.

## Verificação de saída desta fase (executada)

- `dotnet build CodeCiir.slnx`: **0 avisos, 0 erros**.
- `dotnet test CodeCiir.slnx`: **1 teste, 0 falhas** (`GET /version` → 200, corpo com
  `version` não vazio).
- Conectividade real com `code3rag` confirmada via um script Npgsql descartável (fora do
  repositório, mesma string de conexão que `AddDatabaseInfrastructure` usa):
  `Connected OK. database=code3rag projects_count=21`.
