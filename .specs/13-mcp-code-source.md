# Fase 13 — resolver a origem de um resultado de código via MCP

**Status: concluído.** A tool MCP `get_code_source` recebe o `id` de um item retornado por `query_project_code` e devolve os localizadores do arquivo que contém o item: `source_file`, relativo à raiz do projeto, e `git_raw_url`, a URL direta do arquivo bruto quando o projeto a tiver configurada.

## Objetivo e fluxo para harness/LLM

Hoje `query_project_code` já retorna `matches[].id` e `matches[].source_file`. A camada Application também já conhece a URL calculada do arquivo (`GitRawUrl`), mas o mirror MCP a descarta e não há uma operação de lookup por id. Assim, um cliente não tem um contrato MCP explícito para transformar um match selecionado em uma referência que possa abrir ou passar a outra ferramenta.

O fluxo esperado passa a ser:

1. chamar `query_project_code` e escolher um item de `matches`;
2. passar exatamente `matches[].id` para `get_code_source`;
3. usar `source_file` para uma ferramenta que trabalha com caminhos relativos à raiz do repositório e/ou `git_raw_url` para obter o conteúdo bruto remotamente;
4. não tentar formar a URL por concatenação no harness: a API é a fonte da composição entre o `git_raw_url` do projeto e o caminho indexado.

`get_code_source` **não lê nem baixa o conteúdo do arquivo**. Ela apenas resolve a referência estável do documento indexado. Isso mantém a tool de baixo custo, não exige acesso de rede do servidor e deixa a obtenção do conteúdo sob controle do cliente.

## Contrato MCP proposto

Nome MCP: `get_code_source`; método .NET: `GetCodeSourceAsync`.

| Parâmetro | Tipo | Obrigatório | Descrição destinada ao cliente MCP |
|---|---:|:---:|---|
| `documentId` | `long` | sim | `id` exato de um item de `matches` retornado por `query_project_code`; não é `projectId`, nem id de nó/aresta do grafo. |

A descrição da própria tool deve dizer: “Use this after `query_project_code`, passing a selected `matches[].id`. Returns the source-file path from the project root and, when configured, the direct raw Git URL for that same file.” Isso é parte do contrato: é o texto que o protocolo expõe ao harness/LLM durante a descoberta de tools.

Criar `CodeSourceToolResult`:

```csharp
public sealed record CodeSourceToolResult(
    long DocumentId,
    string? SourceFile,
    Uri? GitRawUrl);
```

No transporte MCP, os nomes seguirão a configuração global `snake_case`:

```json
{
  "document_id": 481,
  "source_file": "src/Widgets/Widget.cs",
  "git_raw_url": "https://raw.githubusercontent.com/acme/widgets/main/src/Widgets/Widget.cs"
}
```

Regras:

- `source_file` é o `ciir_documents.source_path` como indexado, relativo à raiz do projeto; a operação não o normaliza nem o transforma em caminho local.
- `git_raw_url` é `projects.git_raw_url` unido a `source_file`, separando os dois por exatamente uma `/`, a mesma regra já usada na busca semântica. É a URL do **arquivo**, não só a URL-base do repositório.
- `git_raw_url` é `null` quando o projeto não configurou `git_raw_url`; isto não torna o documento inválido e `source_file` continua disponível.
- Se o documento não tiver `source_path`, ambos os campos podem ser `null`; o `document_id` ainda identifica o documento retornado. Esse caso deve ser preservado, não convertido em uma URL-base enganosa.
- Um `documentId` inexistente produz a falha de domínio `404-code-document-not-found`, que a tool converte em `McpException`, seguindo a convenção MCP já existente. Valores `<= 0` produzem `400-code-document-id-invalid` antes de consultar o banco.

Não haverá `projectId` como parâmetro: `ciir_documents.id` é a chave primária global e a consulta faz join com o projeto dono. Exigir os dois ids duplicaria informação e abriria a possibilidade de um par inconsistente.

## Desenho de implementação

1. Na Application, introduzir o record enxuto `CodeDocumentSource` (`DocumentId`, `SourceFile`, `GitRawUrl`) e o serviço `ICodeDocumentSourceService` / `CodeDocumentSourceService`. O serviço valida o id e traduz ausência em `Result` com as falhas acima. Não reutilizar `CodeQueryResult`: ele carrega campos de ranking, embedding e relações que não pertencem a uma resolução por id.
2. Estender `ICodeDocumentsRepository` com `GetSourceAsync(long documentId, CancellationToken)`, retornando `CodeDocumentSource?`. A implementação Dapper fará uma única consulta por chave, juntando `ciir_documents cd` a `projects p` e selecionando apenas `cd.id`, `cd.source_path` e a expressão já consolidada para a URL bruta:

   ```sql
   CASE
     WHEN p.git_raw_url IS NULL OR cd.source_path IS NULL THEN NULL
     ELSE RTRIM(p.git_raw_url, '/') || '/' || LTRIM(cd.source_path, '/')
   END
   ```

   O resultado de URL continua convertido para `Uri` absoluta na Infrastructure.
3. Registrar o novo serviço na composição de dependências da API/MCP. O repositório existente permanece registrado uma única vez e é reutilizado; não criar controller REST ou endpoint HTTP nesta fase, pois o requisito é exclusivamente uma capacidade MCP.
4. Adicionar `GetCodeSourceAsync` a `CodeQueryTools`, injetando o novo serviço. Decorar com `[McpServerTool(Name = "get_code_source")]` e as descrições de fluxo definidas acima. Mapear o resultado da Application para `CodeSourceToolResult` e mapear failures para `McpException`, como as outras duas operações da classe.
5. Atualizar o `[Description]` de `query_project_code` para indicar que, depois de selecionar um match, seu `id` pode ser passado para `get_code_source` quando o cliente precisar do caminho/URL do arquivo. Isso cria a ligação bidirecional de descoberta exigida para o harness/LLM. Manter `source_file` já presente no match por compatibilidade; a nova tool é a fonte canônica para obter também `git_raw_url` por id.

## Testes e verificação

- **Application:** id zero/negativo não acessa o repositório e retorna 400; id ausente retorna 404; resultado existente é preservado, inclusive URL nula.
- **Infrastructure (Testcontainers):** lookup encontra o documento e o caminho; URL-base com ou sem barra e caminho com ou sem barra resulta em uma única separação; projeto sem `git_raw_url` retorna nulo; id inexistente retorna nulo. Cobrir também `source_path` nulo.
- **MCP unitário:** `GetCodeSourceAsync` repassa o id/cancellation token, mapeia os três campos e converte failure em `McpException`. Atualizar a construção de `CodeQueryTools` nos testes para a nova dependência.
- **Descoberta/contrato:** testar por reflexão os atributos `McpServerTool` e `Description` para garantir o nome `get_code_source` e a orientação que menciona `query_project_code`/`matches[].id`; assim uma mudança acidental no texto não reduz a capacidade de o cliente descobrir o fluxo.
- Rodar `dotnet test CodeCiir.slnx` e `git diff --check`. Como smoke test opcional, conectar um cliente MCP ao `/mcp`, chamar `query_project_code`, passar um dos ids para `get_code_source` e confirmar que a URL devolvida aponta para o mesmo `source_file`.

## Fora de escopo

- Transferir bytes do arquivo, autenticar contra Forgejo/GitHub ou seguir redirecionamentos.
- Resolver um símbolo por nome, caminho ou id de grafo: a entrada suportada é exclusivamente o id de documento devolvido em `matches`.
- Alterar o contrato REST existente ou adicionar uma rota REST equivalente.
- Garantir que a URL raw seja publicamente acessível: a tool apenas retorna a configuração do projeto e o caminho indexado.

## Verificação executada

- Testes unitários da Application: validação de id, ausência e resultado com URL nula.
- Testes de integração do repositório com Testcontainers: lookup por id, composição de barras, documento inexistente, projeto sem URL raw e caminho ausente.
- Testes MCP: mapeamento, falha para `McpException` e atributos de descoberta que vinculam `get_code_source` a `query_project_code`/`matches[].id`.
- `dotnet test CodeCiir.slnx --no-restore`: 246 testes aprovados; `git diff --check`: aprovado.
