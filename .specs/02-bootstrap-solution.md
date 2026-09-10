# Fase 1 — Scaffold da solution .NET

**Status: concluído.** Entregue com escopo mais enxuto que o planejado originalmente —
ver "⚠️ Correções em relação ao plano original" no fim deste documento antes de ler as
seções abaixo como se fossem o estado atual (elas descrevem o plano *inicial*; a seção
final é a fonte da verdade sobre o que existe hoje no repositório).

## Contexto

O diretório de trabalho só tinha `.editorconfig` e `stylecop.json` (herdados/copiados do
`code-rag-api`, mesmo estilo de código) — nenhuma solution .NET existia. Esta fase criou o
esqueleto, com o mínimo de lógica necessário para ter algo de ponta a ponta testável.

## Nomenclatura (confirmada)

`CodeRag.*` → `CodeCiir.*`, sem objeção do usuário.

## Projetos criados nesta fase

- **`CodeCiir.Api`** (`Microsoft.NET.Sdk.Web`, `net9.0`) — `Program.cs` (Serilog,
  controllers com `snake_case` JSON, `UnhandledExceptionFilter`, Swashbuckle+Scalar em
  dev), `VersionController`/`VersionResponse`/`AppVersion` (`GET /version`, sem prefixo
  `/api/v1`, health-check-style — mesmo padrão do code-rag-api), `Problems/
  ServerErrorProblemDetails`, `Filters/UnhandledExceptionFilter`, `OpenApi/
  ControllerTagDescriptionsDocumentFilter`.
- **`CodeCiir.Application`** — `ServiceCollectionExtensions.AddApplication()` **vazio** por
  enquanto (nenhum serviço ainda; o primeiro chega na Fase 2).
- **`CodeCiir.Infrastructure.Database`** — `ServiceCollectionExtensions.
  AddDatabaseInfrastructure()` registrando só `NpgsqlDataSource` (resolução preguiçosa da
  connection string, mesmo padrão do code-rag-api). Nenhum repositório ainda.

## `tests/`

Criado apenas **`CodeCiir.Api.Tests`** (`WebApplicationFactory`), com um teste end-to-end
de `GET /version`. `CustomWebApplicationFactory` sobrescreve `ConnectionStrings:Database`
com um valor sintaticamente válido mas inalcançável — nenhum teste desta fase toca o
banco de verdade, então isso apenas comprova que nada depende acidentalmente dele.
`CodeCiir.Application.Tests`/`CodeCiir.Infrastructure.Database.Tests` **adiados** para a
Fase 2 (não há nada de negócio para testar ainda).

## Configuração

- `ConnectionStrings:Database` **não está em nenhum arquivo commitado** (nem
  `appsettings.Development.json`) — só via `dotnet user-secrets`/variável de ambiente
  local, ou Kubernetes Secret em produção (ver `08-ops-deployment.md`). A senha de
  `fatlip` usada durante a Fase 0 transitou por um canal de chat — recomendado rotacioná-
  la antes de produção.
- Nenhuma seção `Embeddings`/`Reranking` ainda — adiadas, ver correções abaixo.

## Docker / dev local

`docker-compose.yml` só com o serviço `api` — sem Postgres local (a API lê `code3rag`
remoto) e sem Ollama ainda (nada usa embeddings nesta fase). A connection string vem de
`${CIIR_CONNECTION_STRING}` (variável de ambiente do host, não commitada).

## CI/CD

Adiado para a Fase 7 (`08-ops-deployment.md`) — sem repositório Forgejo ainda para este
projeto (diretório de trabalho não é um repositório git no momento desta fase).

## Verificação de saída desta fase (executada)

- `dotnet build CodeCiir.slnx`: **0 avisos, 0 erros**.
- `dotnet test CodeCiir.slnx`: **1 teste, 0 falhas** (`GET /version` → 200, corpo com
  `version` não vazio).
- Conectividade real com `code3rag` confirmada via um script Npgsql descartável (fora do
  repositório, mesma string de conexão que `AddDatabaseInfrastructure` vai usar):
  `Connected OK. database=code3rag projects_count=21` — prova que o caminho
  host/porta/usuário/senha funciona antes de qualquer endpoint real ser implementado, sem
  acoplar o startup da aplicação a uma dependência de rede externa (ver correção 3 abaixo).

## ⚠️ Correções em relação ao plano original

1. **`CodeCiir.slnx` em vez de `CodeCiir.sln`**: o SDK instalado (`dotnet 10.0.400`,
   default do ambiente) gera o novo formato `.slnx` (XML) por padrão em `dotnet new sln`.
   `dotnet build`/`dotnet test`/`dotnet sln add` funcionam de forma idêntica com ele — só
   o nome do arquivo muda nos comandos (`dotnet build CodeCiir.slnx`, não
   `CodeCiir.sln`). Atualizar qualquer referência futura a `CodeRag.sln`-com-nome-trocado
   para `CodeCiir.slnx`.
2. **`CodeCiir.Embeddings.*`/`CodeCiir.Mcp`/`CodeCiir.Reranking.*` NÃO foram criados
   nesta fase**, ao contrário do plano original. Motivo: nenhum consumidor real existe
   ainda para eles (o primeiro é `CodeQueryService` na Fase 3, para Embeddings; a Fase 6,
   para Mcp) — criá-los agora seria scaffolding morto sem uso imediato, e o desenho de
   `EmbeddingGeneratorResolver` já precisa mudar (resolução por `model`/`dimensions` **por
   requisição**, a partir de `projects.embedding_model`/`embedding_dimensions`, não uma
   vez no startup a partir de config global — ver `04-code-queries-baseline.md`) — melhor
   projetar isso junto com quem de fato vai chamá-lo, na Fase 3, do que construir e ter
   que retrabalhar. Reranking segue adiado, sem mudança em relação ao plano original.
3. **Sem checagem de conectividade com `code3rag` embutida no startup do app.** O plano
   original sugeria um fail-fast tipo `GetRequiredService<NpgsqlDataSource>()` logo após
   `app.Build()` (mesmo padrão do code-rag-api para `IEmbeddingGenerator`/`IReranker`).
   Decisão: **não** fazer isso — acoplaria todo `dotnet test`/boot do
   `WebApplicationFactory` a uma dependência de rede externa (`192.168.1.212`, só
   alcançável na rede local do usuário), quebrando testes/CI hermeticidade sem necessidade
   real nesta fase (nenhum endpoint ainda consulta o banco). A verificação de
   conectividade foi feita como um passo manual único (script descartável, ver acima),
   suficiente para o objetivo desta fase. Revisitar um fail-fast em produção (não em
   testes) só quando a Fase 2+ já tiver um repositório real que dependa do banco no
   caminho crítico de startup, se fizer sentido então.
4. **`BlogDoFT.Libs.Api.OpenTelemetry`, `BlogDoFT.Libs.ResultPattern`, `CsvHelper` não
   referenciados ainda** — nenhum usa essas libs nesta fase (Result pattern chega na Fase
   2 junto com o primeiro `Failure`; OpenTelemetry é uma decisão de observabilidade mais
   apropriada para `08-ops-deployment.md`, quando/se este serviço ganhar um namespace de
   observability próprio).
5. **Sem hook Husky/pre-push**: o `.config/dotnet-tools.json` já existente no repo
   referencia `husky`, mas o diretório de trabalho não é um repositório git nesta fase —
   o MSBuild target de provisionamento automático do code-rag-api foi deliberadamente
   omitido do `CodeCiir.Api.csproj` até o repositório ser de fato inicializado com git
   (fora do escopo desta fase; ninguém pediu isso ainda).
