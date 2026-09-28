# Fase 10 — 400 de model binding passa a dizer qual campo falhou e por quê

**Status: concluído.** `ApiBehaviorOptions.InvalidModelStateResponseFactory` (Program.cs)
devolvia sempre o mesmo `detail` genérico ("The request body is missing or invalid.") para
qualquer corpo rejeitado pelo model binding/deserialização JSON do ASP.NET — tipo errado,
propriedade desconhecida (`JsonUnmappedMemberHandling.Disallow`), objeto aninhado sem
propriedade `[JsonRequired]`, JSON malformado, etc. Passou a devolver também `errors`
(`ValidationProblemDetails.Errors`, o mesmo formato-padrão do ASP.NET Core), construído a
partir do `ModelStateDictionary` real da requisição — nomeando o campo e a razão exata.

## Contexto

O usuário reportou um 400 real do endpoint de code-queries só com o `detail` genérico,
sem indicar qual regra foi violada. Essa resposta vinha exatamente desse factory
compartilhado — não das falhas de domínio (`Failure`, que já carregam mensagem específica
desde sempre, ex. `"The 'question' field is required..."`) — então o problema afeta
**todos** os endpoints igualmente (Projects, CodeQueries, Feedback), não é específico de
nenhum controller.

## Antes / depois

Antes:
```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request body is missing or invalid.",
  "instance": "/api/code-queries"
}
```

Depois (`projectId` enviado como número em vez de UUID):
```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request body is missing or invalid - see 'errors' for which field(s) and why.",
  "instance": "/api/code-queries",
  "errors": {
    "request": ["The request field is required."],
    "$.projectId": ["The JSON value could not be converted to CodeCiir.Api.Contracts.CodeQueryRequest. Path: $.projectId | LineNumber: 0 | BytePositionInLine: 20."]
  }
}
```
A entrada `"request"` é um efeito colateral do próprio `ModelState` do ASP.NET (o parâmetro
`[FromBody] CodeQueryRequest request` também é marcado inválido) — inofensiva, mantida por
ser exatamente o comportamento padrão do framework, sem necessidade de filtragem customizada.

Outros exemplos reais confirmados: propriedade desconhecida →
`"$.campoInexistente": ["The JSON property 'campoInexistente' could not be mapped to any .NET member contained in type '...'."]`; objeto aninhado sem propriedade obrigatória →
`"$.qualifiedName": ["JSON deserialization for type '...CodeQueryQualifiedNameFilterRequest' was missing required properties including: 'operator'."]`.

## Implementação

`src/CodeCiir.Api/Problems/ProblemResults.cs` tem `BadRequestValidation(ModelStateDictionary,
PathString)`, que constrói um `ValidationProblemDetails` a partir do `ModelState` (chaves de
`errors` convertidas para `camelCase`) com os mesmos `Type`/`Title`/`Status`/`Instance` do
resto do contrato. Quem chama é o filtro global `Filters/ModelValidationFilter`, que também
cobre as validações de forma declaradas nos DTOs por DataAnnotations (obrigatório, tamanho,
faixa — ver `15-skills-alignment.md`), antes de o use case ser chamado.

## Fora de escopo

- Filtrar a entrada redundante `"request"` do `errors` — comportamento padrão do ASP.NET
  Core, não vale a complexidade de suprimi-lo.
- Aplicar o mesmo tratamento a falhas de domínio (`Failure`) — essas já eram específicas.

## Verificação de saída desta fase

- Testes de corpo inválido (tipo errado, propriedade desconhecida, objeto aninhado sem campo
  obrigatório, e resposta `application/problem+json` com `type`/`title`/`status`/`instance`
  corretos) — hoje em `tests/CodeCiir.Api.Tests/Filters/ModelValidationFilterTests.cs`.
- `dotnet build`/`dotnet test` na solução inteira: 139 testes verdes (0 falhas).
