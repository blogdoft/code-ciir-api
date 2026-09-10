# Fase 7 — CI/CD e deploy

> **Caminhos movidos numa fase posterior**: `Dockerfile` e `docker-compose.yml`, descritos
> abaixo na raiz do repositório, vivem hoje em `.eng/docker/` (`.dockerignore` continua na
> raiz — precisa estar na raiz do build context, que continua sendo a raiz do repositório).
> `.forgejo/workflows/docker-publish.yml` e `.eng/k8s/*.yaml` não mudaram de lugar. Também
> foi adicionado `.forgejo/workflows/mirror-to-github.yml` (espelha todo push — branches e
> tags — para `https://github.com/blogdoft/code-ciir-api`, cópia exata do workflow homônimo
> de `code-rag-api`/`code-ciir-indexer`, mesmo mecanismo de token via
> `MIRROR_GITHUB_TOKEN`). Não requer nenhuma mudança de código, só o secret configurado no
> Forgejo (`Settings > Actions > Secrets`) para funcionar.

**Status: manifests/workflow concluídos e validados localmente; deploy real (secrets, DNS,
push ao registry, ArgoCD) pendente — depende de ações fora deste repositório (ver
checklist).** `.forgejo/workflows/docker-publish.yml`, `.eng/k8s/*.yaml`, `Dockerfile`
criados/atualizados.

**Verificação real feita nesta sessão** (sem publicar nada externamente):
- `docker build` da imagem: sucesso, `mcr.microsoft.com/dotnet/sdk:10.0` →
  `mcr.microsoft.com/dotnet/aspnet:10.0`.
- `docker run` da imagem construída, com a connection string real de `code3rag` injetada
  via variável de ambiente (mesmo mecanismo do `secretKeyRef` do `deployment.yaml`):
  `GET /version` → 200, `GET /api/v1/projects` → 200 com os 21 projetos reais. Confirma
  que a imagem publicada funcionaria em produção tal como está.
- Aviso benigno observado nos logs do container: `Cannot load library
  libgssapi_krb5.so.2` (Npgsql tentando negociação GSSAPI/Kerberos, ausente na imagem
  `aspnet:10.0` mínima) — não impede a conexão (autenticação por senha funciona
  normalmente, confirmado pelo 200 acima); ignorar ou, se algum dia a base exigir auth
  Kerberos, trocar a imagem de runtime por uma que inclua `libgssapi-krb5-2`.
- **⚠️ Achado durante a verificação**: `dotnet build -c Release` (o que o `Dockerfile` e o
  workflow de CI usam) revela mais avisos do StyleCop (documentação/formatação — SA1101,
  SA1600, SA1629, SA1615, SA1633, SA1009/SA1010/SA1000) do que um `dotnet build` comum em
  Debug no dia a dia — nenhum novo erro, só mais avisos de estilo. Não bloqueia esta fase,
  mas uma limpeza desses avisos (ou ajuste de severidade no `stylecop.json`/`.editorconfig`)
  é recomendada antes de tratar o pipeline de CI como "verde de verdade" — o
  `code-rag-api` de referência tem avisos pré-existentes reconhecidos de forma parecida
  (ex. o bug do analisador StyleCop com `RecordDeclaration`, também presente aqui).
- `.config/dotnet-tools.json` ganhou `gitversion.tool` (6.8.2) — necessário para o step
  "Compute version" do workflow; `dotnet tool restore` verificado localmente.

## Contexto

Mesmo padrão do `code-rag-api`: Forgejo Actions builda/testa/publica imagem Docker em
push para `main` e tags de versão; o mesmo workflow estampa a tag da imagem nos manifests
k8s (`.eng/k8s`) e sincroniza para `argo-local-apps`, que o ArgoCD observa para o cluster
k8s local.

## Diferenças em relação ao code-rag-api

- **Sem serviço `postgres` no `docker-compose.yml`** (ver `02-bootstrap-solution.md`) —
  `code3rag` é remoto, não um container descartável local.
- **Secret de conexão** diferente: `code-ciir-secrets` (não reaproveitar
  `code-rag-secrets`, mesmo que aponte para o mesmo host — são bases/credenciais
  logicamente distintas, e um serviço não deveria conseguir, mesmo que por engano via
  copy-paste de manifest, escrever na base do outro).
- **Ingress host**: `code-ciir-api.home.arpa` (a confirmar disponibilidade/convenção com o
  usuário antes de criar o registro DNS/Ingress real).
- **Sem reranking na Fase 1** → sem `Reranking__*` no `configmap.yaml` inicial, sem o
  `ServersTransport` de 120s calibrado para reranking pesado (o `code-rag-api` usa 120s
  especificamente por causa da latência de reranking via LLM; sem essa feature, o timeout
  padrão do Traefik é suficiente até prova em contrário).
- **`readinessProbe`**: apontar para o endpoint `GET` que sobreviver à decisão da Fase 2
  (`/api/v1/projects` se Projects continuar exposto; caso contrário, um endpoint de
  health/version dedicado, mesmo padrão do `VersionController` do code-rag-api).

## Checklist de configuração fora deste repositório (mesma natureza do
`ARGO_DEPLOY_SSH_KEY`/`code-rag-secrets` do code-rag-api — não gerenciado em código):

1. Deploy key com escrita em `sauron/argo-local-apps` (pode reaproveitar a mesma chave do
   `code-rag-api` se a política de segurança do usuário permitir compartilhar entre
   serviços, ou gerar uma dedicada — decisão do usuário).
2. Secret `ARGO_DEPLOY_SSH_KEY` no repo `code-ciir-api` no Forgejo.
3. Secret `code-ciir-secrets` (chave `connection-string`) no namespace alvo do cluster,
   com a Npgsql connection string para `code3rag` — credenciais de **somente leitura**
   sempre que a Fase 2 confirmar Projects read-only e a Fase 5 não avançar (nenhum motivo
   para esta API ter permissão de escrita em `code3rag` além do estritamente necessário).

## Verificação de saída desta fase

- Pipeline verde builda/testa/publica a imagem a partir de um push de teste.
- Deploy manual (ou via ArgoCD) sobe o pod, `readinessProbe` fica `Ready`, e uma chamada
  real via `https://code-ciir-api.home.arpa/api/v1/projects` (ou o endpoint de health
  escolhido) responde.
