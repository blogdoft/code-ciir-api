# Fase 11 — 400 de model binding passa a dizer qual campo falhou e por quê

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
  "instance": "/api/v1/code-queries"
}
```

Depois (`project_id` enviado como string em vez de número):
```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request body is missing or invalid - see 'errors' for which field(s) and why.",
  "instance": "/api/v1/code-queries",
  "errors": {
    "request": ["The request field is required."],
    "$.project_id": ["The JSON value could not be converted to CodeCiir.Api.Contracts.CodeQueryRequest. Path: $.project_id | LineNumber: 0 | BytePositionInLine: 43."]
  }
}
```
A entrada `"request"` é um efeito colateral do próprio `ModelState` do ASP.NET (o parâmetro
`[FromBody] CodeQueryRequest request` também é marcado inválido) — inofensiva, mantida por
ser exatamente o comportamento padrão do framework, sem necessidade de filtragem customizada.

Outros exemplos reais confirmados: propriedade desconhecida →
`"$.campo_inexistente": ["The JSON property 'campo_inexistente' could not be mapped to any .NET member contained in type '...'."]`; objeto aninhado sem propriedade obrigatória →
`"$.qualified_name": ["JSON deserialization for type '...QualifiedNameFilterRequest' was missing required properties including: 'operator'."]`.

## Implementação

`src/CodeCiir.Api/Problems/ProblemResults.cs` ganhou `BadRequestValidation(ModelStateDictionary, PathString)`,
que constrói um `Microsoft.AspNetCore.Mvc.ValidationProblemDetails(modelState)` (populando
`Errors` automaticamente a partir do `ModelState`) com os mesmos `Type`/`Title`/`Status`/
`Instance` do resto do contrato. `Program.cs`'s `InvalidModelStateResponseFactory` passou a
chamar esse método em vez do antigo `ProblemResults.BadRequest(string, PathString)` de
detail fixo (esse método continua existindo, ainda usado por `RouteId.TryParsePositive` para
seus próprios erros de rota, que já eram específicos por parâmetro).

## Fora de escopo

- Filtrar a entrada redundante `"request"` do `errors` — comportamento padrão do ASP.NET
  Core, não vale a complexidade de suprimi-lo.
- Aplicar o mesmo tratamento a falhas de domínio (`Failure`) — essas já eram específicas.

## Verificação de saída desta fase

- Novo `tests/CodeCiir.Api.Tests/InvalidRequestBodyTests.cs` (4 testes: tipo errado,
  propriedade desconhecida, objeto aninhado sem campo obrigatório, e que a resposta
  continua `application/problem+json` com `type`/`title`/`status`/`instance` corretos).
- `dotnet build`/`dotnet test` na solução inteira: 139 testes verdes (0 falhas).
