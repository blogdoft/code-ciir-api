namespace CodeCiir.Application.CodeQueries;

/// <summary>Comparison operators supported by the <c>qualifiedName</c> code query filter.</summary>
public enum QualifiedNameFilterOperator
{
    /// <summary>Matches documents whose <c>symbol_qualified_name</c> equals the filter value.</summary>
    Equals,

    /// <summary>Matches documents whose <c>symbol_qualified_name</c> contains the filter value.</summary>
    Contains,

    /// <summary>Matches documents whose <c>symbol_qualified_name</c> does not contain the filter value.</summary>
    NotContains,
}
