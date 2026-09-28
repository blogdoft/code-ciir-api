# Fase 7 — CI/CD e deploy

**Status: concluído — em produção.** Imagem publicada pelo Forgejo Actions, manifests
sincronizados para `argo-local-apps` e aplicados pelo ArgoCD no namespace `code-brain`,
servidos no gateway `https://blogdoft.home.arpa/code-brain`.

## Contexto

Mesmo padrão do `code-rag-api`: o Forgejo Actions builda/testa/publica a imagem Docker; o
mesmo workflow estampa a tag da imagem nos manifests k8s (`.eng/k8s`) e sincroniza para
`sauron/argo-local-apps` (`manifests/code-brain/code-ciir-api`), que o ArgoCD observa.

## Arquivos

- `.eng/docker/Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0` →
  `mcr.microsoft.com/dotnet/aspnet:10.0`) e `.eng/docker/docker-compose.yml`. O
  `.dockerignore` fica na raiz, que é o build context.
- `.forgejo/workflows/docker-publish.yml`:
  - `test` em pull requests para `main` e em tags `vX.Y.Z`;
  - `ciir` em paralelo com `test`: analisa este repositório com a ferramenta `BlogDoFT.Ciir`
    (dogfooding) e, numa tag, envia o `ciir.jsonl` resultante para o `code-ciir-indexer`;
  - build/publish da imagem e sync para `argo-local-apps` só em tags `vX.Y.Z`. A versão vem da
    tag (`gitversion.tool` em `.config/dotnet-tools.json`).
- `.forgejo/workflows/mirror-to-github.yml`: espelha todos os branches e tags para
  `https://github.com/blogdoft/code-ciir-api` a cada push e diariamente (cron), com token via
  secret `MIRROR_GITHUB_TOKEN`.
- `.eng/k8s/`: `deployment.yaml`, `service.yaml`, `configmap.yaml`, `ingress.yaml`,
  `middleware.yaml`, `serverstransport.yaml`, `kustomization.yaml`.

## Decisões de deploy

- **Sem serviço `postgres` no `docker-compose.yml`** — `code3rag` é remoto.
- **Secret de conexão próprio**: `code-ciir-secrets` (chave `connection-string`), não
  `code-rag-secrets` — são bases logicamente distintas, e um serviço não deveria conseguir,
  mesmo que por engano via copy-paste de manifest, escrever na base do outro.
- **Ingress no gateway compartilhado**: host `blogdoft.home.arpa`, caminhos
  `/code-brain/api/code-queries` e `/code-brain/mcp` (`pathType: Prefix`); o Middleware
  `code-ciir-api-strip-code-brain` remove `/code-brain` antes de encaminhar. `/version` não tem
  regra própria aqui: no gateway, ele cai no catch-all `/code-brain` do `code-rag-front`, cujo
  nginx repassa para este serviço.
- **Swagger** sempre publicado (não só em Development), em `api/code-queries/swagger`, com
  `PublicServerDocumentFilter` definindo o `servers` do OpenAPI a partir de `PublicBaseUrl`.
- **Timeout**: `serverstransport.yaml` (`code-ciir-api-transport`, `responseHeaderTimeout:
  120s`), referenciado via anotação Traefik em `service.yaml`, por causa da latência do
  reranking via LLM (ver `10-reranking.md`). Vale para qualquer caminho do mesmo `Service`.
- **Probes**: `readinessProbe` e `livenessProbe` em `GET /health` (anônimo, fora de traces e
  métricas).
- **Configuração** (`configmap.yaml`): `Embeddings__*`, `Reranking__*`, `Keycloak__*`,
  `Observability__*`, `OTEL_SERVICE_NAME`, `PublicBaseUrl`.

## Observações de verificação

- `docker run` da imagem com a connection string real injetada via variável de ambiente
  (mesmo mecanismo do `secretKeyRef`): `GET /version` → 200.
- Aviso benigno nos logs: `Cannot load library libgssapi_krb5.so.2` (Npgsql tentando
  negociação GSSAPI/Kerberos, ausente na imagem `aspnet:10.0` mínima) — não impede a conexão
  por senha; só seria preciso trocar a imagem de runtime se a base passasse a exigir Kerberos.
- `dotnet build -c Release` (usado pelo `Dockerfile` e pelo CI) aplica mais regras do
  StyleCop (documentação/formatação) do que o build Debug do dia a dia; rode em Release antes
  de publicar para ver os mesmos avisos que o pipeline.

## Configuração fora deste repositório

Não gerenciada em código:

1. Deploy key com escrita em `sauron/argo-local-apps` e o secret `ARGO_DEPLOY_SSH_KEY` no repo
   `code-ciir-api` no Forgejo.
2. Secret `code-ciir-secrets` (chave `connection-string`) no namespace `code-brain`, com a
   connection string Npgsql para `code3rag`. O usuário precisa de leitura em `code3rag` e de
   escrita em `code_query_feedback`.
3. Secret `MIRROR_GITHUB_TOKEN` para o espelhamento no GitHub.
