# Autenticação opcional via Keycloak — "/mcp" sempre anônimo

**Status: concluído.** Quando a autenticação está ligada (`Keycloak:Enabled = true`), os endpoints
REST exigem um access token (`Authorization: Bearer <jwt>`) emitido pelo realm configurado. Quando
está desligada (o padrão), nada muda: todos os endpoints continuam abertos, como hoje. **"/mcp"
nunca exige token, em nenhum dos dois casos** — é uma exceção permanente, não uma omissão: os
clientes MCP deste deployment não têm como anexar um Bearer token à conexão, então proteger o
endpoint o tornaria inacessível para eles.

## Contexto

Mesmo ponto de partida do `code-ciir-indexer` (`.specs/05-keycloak-auth.md` lá): a API não tinha
nenhuma autenticação (`Program.cs` chamava `UseAuthorization()` sem nenhum esquema registrado).
Diferença central em relação ao indexer: aqui existe um segundo tipo de endpoint, `/mcp`
(`McpServerBuilderExtensions.AddCodeCiirTools`, mapeado via `app.MapMcp("/mcp")`), usado por
clientes MCP (ex.: Claude Code) para chamar as mesmas ferramentas de projeto/code-query expostas
via REST. Pedido explícito do usuário: ligar Keycloak **sem** quebrar esses clientes MCP, que não
passam por um fluxo de obtenção/anexação de token.

Escopo: **autenticação** apenas (o chamador apresenta um token válido do realm), só para
controllers REST. Não há autorização por role/scope/client. `/mcp` fica inteiramente fora do
escopo de autenticação, hoje e para qualquer tool adicionada no futuro a `CodeCiir.Mcp`.

## Configuração

Seção `Keycloak` (`appsettings.json` / variáveis `Keycloak__*`) — idêntica à do indexer:

| Chave | Tipo | Padrão | Descrição |
|---|---|---|---|
| `Enabled` | bool | `false` | Liga/desliga a autenticação dos controllers REST. Com `false`, todas as demais chaves são ignoradas. |
| `Authority` | string | *(vazio)* | URL do realm, ex.: `https://keycloak.home.arpa/realms/blogdoft`. Obrigatória quando `Enabled = true`. |
| `Audience` | string | *(vazio)* | Se preenchido, o claim `aud` do token precisa conter este valor. Se vazio, a audiência **não** é validada. |
| `ClientId` | string | *(vazio)* | `client_id` do client público que representa esta aplicação, compartilhado com o botão **Authorize** do Swagger UI. Se vazio, o Swagger UI só aceita um token colado. |
| `RequireHttpsMetadata` | bool | `true` | Exige HTTPS para buscar o *discovery document*/JWKS. Só `false` contra um Keycloak local em HTTP. |

Regras de habilitação e validação de startup: mesmas do indexer (`Authority` vazia, não-absoluta ou
`http://` com `RequireHttpsMetadata=true` falham o startup com `InvalidOperationException`, capturada
pelo `catch` do topo de `Program.cs`).

## Comportamento

### Autenticação desligada (`Enabled = false`)

Igual a hoje: nenhum esquema, nenhuma política, nenhum middleware de autenticação registrado.
`/mcp` e os controllers REST respondem exatamente como antes desta spec.

### Autenticação ligada (`Enabled = true`)

- Esquema `JwtBearer`, `FallbackPolicy = RequireAuthenticatedUser`: toda rota exige token por
  padrão, **exceto as que opõem `AllowAnonymous()` explicitamente**.
- `app.MapMcp("/mcp")` recebe `.AllowAnonymous()` incondicionalmente (é um no-op quando a
  autenticação está desligada, então a chamada não precisa ficar atrás de um `if`). Esta é a
  única exceção deliberada além de `/health` e Swagger/OpenAPI (que já ficam fora do
  `FallbackPolicy` por não passarem pelo roteamento de endpoints — ver "Implementação").
- Sem token, token malformado/expirado/com assinatura ou issuer inválidos em um controller REST:
  `401 Unauthorized`, `WWW-Authenticate: Bearer`, sem corpo. (⚠️ correção em `15-skills-alignment.md`: antes
  o corpo era `application/problem+json`.)
- `/mcp` nunca responde `401` por falta de token — uma chamada MCP mal formada ainda pode responder
  `400`/`406` pelas próprias regras do transporte Streamable HTTP, só não pela ausência de
  `Authorization`.

### Exceções a "toda rota exige token"

| Rota | Por que fica anônima |
|---|---|
| `/mcp` (e `/sse`, `/message` se `EnableLegacySse` for ligado no futuro) | Pedido explícito do usuário: clientes MCP não anexam Bearer token. `AllowAnonymous()` no `IEndpointConventionBuilder` que `MapMcp` devolve. |
| `GET /health` | `app.Map("/health", ...)` é um branch terminal de middleware clássico, montado fora do roteamento de endpoints — nunca passa por `UseAuthorization`, independente de posição no pipeline. Sem mudança nesta spec. |
| Swagger UI / `swagger.json` (`app.UseSwagger`/`app.UseSwaggerUI`) | Mesma razão: middleware clássico do Swashbuckle, não endpoints roteados. Um navegador não conseguiria anexar um Bearer token à navegação de qualquer forma. |

Diferente do indexer (que usa `Microsoft.AspNetCore.OpenApi`/`MapOpenApi`, um endpoint de verdade
que precisa de `.AllowAnonymous()` explícito), aqui o Swagger é Swashbuckle clássico
(`UseSwagger`/`UseSwaggerUI`), que já fica fora do roteamento por posição — nenhuma mudança extra
foi necessária para mantê-lo anônimo.

### OpenAPI / Swagger UI (só com a autenticação ligada)

Mesmo comportamento do indexer: o documento ganha um *security scheme* `Bearer` (`type: http`,
`scheme: bearer`, `bearerFormat: JWT`) e, se `ClientId` estiver configurado, também `OAuth2`
(authorization code + PKCE) — o Swagger UI ganha o botão **Authorize**. `/mcp` não aparece nesse
documento (não é um controller MVC), então não há nada a dizer sobre ele ali.

## Implementação

Autenticação é uma preocupação da borda HTTP - vive só em `CodeCiir.Api`, mirando a mesma
organização do indexer:

- `Authentication/KeycloakOptions.cs`, `Authentication/KeycloakAuthenticationExtensions.cs` —
  cópias funcionais do indexer (mesma lógica de validação/registro), namespace `CodeCiir.Api.Authentication`.
- `OpenApi/KeycloakSecurityDocumentFilter.cs` — equivalente Swashbuckle (`IDocumentFilter`) do
  `KeycloakSecurityDocumentTransformer` do indexer (que usa `Microsoft.AspNetCore.OpenApi`,
  incompatível com o Swashbuckle já usado aqui).
- `Program.cs`:
  - `KeycloakOptions.FromConfiguration(builder.Configuration)` lido uma vez, no topo, **antes**
    de `builder.Build()` — mesma posição do indexer;
  - se não nulo: `AddKeycloakAuthentication` + `KeycloakSecurityDocumentFilter` no `AddSwaggerGen`
    + `OAuthClientId`/`OAuthUsePkce`/`OAuthScopes` no `UseSwaggerUI` quando `ClientId` está
    preenchido + `app.UseAuthentication()` antes de `app.UseAuthorization()`;
  - `app.MapMcp("/mcp").AllowAnonymous()` — incondicional, é o núcleo desta spec.
- Dependência nova: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 (mesma versão do
  indexer).
- `appsettings.json`: seção `Keycloak` com `Enabled: false` e o resto vazio (documenta o formato).
- `.eng/k8s/configmap.yaml`: bloco comentado com `Keycloak__Authority`/`Keycloak__Audience`/
  `Keycloak__ClientId`, destacando que `/mcp` continua anônimo mesmo com `Enabled=true`.

## Atualização — "/mcp" exposto pelo ingress

**Status: concluído.** A pedido do usuário, `.eng/k8s/ingress.yaml` ganhou uma segunda `path` para
o mesmo `Service` (`code-ciir-api`): `/code-brain/mcp`, prefixo-removido pelo mesmo Middleware
`code-ciir-api-strip-code-brain` já usado para `/code-brain/api/code-queries` (a anotação
`router.middlewares` vale para o `Ingress` inteiro, não por `path`) — chega no backend como `/mcp`,
igual ao roteamento local (`app.MapMcp("/mcp")`).

Consequência de segurança explícita: `/mcp` passa a ser alcançável por qualquer um que alcance
`blogdoft.home.arpa`, sem autenticação possível (ver acima — é permanente, não um gap). Essa é
exatamente a mesma exposição que `/code-brain/api/code-queries` já tem hoje enquanto
`Keycloak:Enabled` estiver `false` (o padrão) — não é uma categoria de risco nova, só um segundo
caminho de entrada com o mesmo nível de proteção (nenhum, por ora). Nenhuma mudança de timeout foi
necessária: `code-ciir-api-transport` (`serverstransport.yaml`) já se aplica no nível do `Service`,
cobrindo qualquer `path` do mesmo backend, inclusive `/mcp`.

## Fora de escopo

- Autorização por role/scope/client (mesmo motivo do indexer).
- Qualquer forma de autenticação para `/mcp` (proxy de token, client credentials injetado pelo
  servidor, etc.) — decisão explícita do usuário, não uma lacuna a fechar depois.

## Verificação

Testes em `CodeCiir.Api.Tests`, contra o `Program` real via `CustomWebApplicationFactory`/
`WebApplicationFactory<Program>` (não um host mínimo à parte como o indexer usou) — sem Keycloak
real, com o discovery document do realm substituído por uma chave simétrica fixa.

Achado durante a implementação: `KeycloakOptions.FromConfiguration(builder.Configuration)` roda
**antes** de `builder.Build()`, e `WithWebHostBuilder`'s `ConfigureAppConfiguration`/
`ConfigureServices` só são de fato aplicados no builder no momento em que `Build()` é interceptado
pelo `WebApplicationFactory` — ou seja, tarde demais para essa leitura específica. As chaves
`Keycloak__*` são ligadas via variável de ambiente de processo (visível desde a primeira linha de
`Program.cs`) em vez de `ConfigureAppConfiguration`; seguro porque `xunit.runner.json` já desliga
`parallelizeTestCollections`.

- `KeycloakOptionsTests`: mesmos casos do indexer (habilitação, validação de `Authority`,
  trimming).
- `KeycloakAuthenticationExtensionsTests` (ex-`KeycloakAuthenticationTests`): `GET /version` (controller sem `[Authorize]` próprio) sem token →
  401 + `WWW-Authenticate: Bearer` + corpo vazio; com token válido → 200; com token
  expirado → 401; `POST /mcp` sem token, autenticação ligada → nunca 401 (400, pelas regras do
  próprio transporte MCP para um corpo vazio) — prova que o `FallbackPolicy` alcança `/version`
  mas não `/mcp`; `POST /mcp` com autenticação desligada → mesmo comportamento (400, nunca 401).
- `KeycloakSecurityDocumentFilterTests`: `swagger.json` sem `components.securitySchemes` com
  autenticação desligada; com ela ligada, `Bearer` sempre presente, `OAuth2` só com `ClientId`
  preenchido.
- `dotnet format` → `dotnet build` (zero warnings) → `dotnet test` verdes (45/45 em
  `CodeCiir.Api.Tests`).
