using CodeCiir.Application.CodeQueries;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CodeCiir.Api.Contracts;

/// <summary>
/// Optional filter narrowing results to code documents matching a <c>symbolQualifiedName</c>
/// condition (the member's full name including its namespace/package or language equivalent).
/// </summary>
/// <param name="Operator">Comparison operator to apply.</param>
/// <param name="Value">
/// Value to compare the qualified name against. Must not be empty or blank. When the operator is
/// <c>Contains</c> or <c>NotContains</c>, <c>*</c> acts as a wildcard matching any sequence of
/// characters (e.g. <c>*Controller*</c> matches values containing "Controller"); a value with no
/// <c>*</c> is matched exactly (case-insensitively). Ignored for <c>Equals</c>.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryQualifiedNameFilterRequest(
    [property: JsonRequired] QualifiedNameFilterOperator Operator,
    [property: JsonRequired]
    [Required(AllowEmptyStrings = false, ErrorMessage = "The 'qualifiedName' filter's 'value' field is required and must not be empty or blank."),
        StringLength(CodeQueryService.MaxFilterValueLength, ErrorMessage = "The 'qualifiedName' filter's 'value' field must not exceed {1} characters.")] string Value);
#pragma warning restore SA1313
