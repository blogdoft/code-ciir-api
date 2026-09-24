using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.CodeQueries;

public static class CodeQueryFailures
{
    public static Failure QuestionRequired() => new(
        "400-question-required",
        "The 'question' field is required and must not be empty or blank.");

    public static Failure QuestionTooLong(int maxLength) => new(
        "400-question-too-long",
        $"The 'question' field must not exceed {maxLength} characters.");

    public static Failure MinSimilarityOutOfRange() => new(
        "400-min-similarity-out-of-range",
        "The 'minSimilarity' field must be between 0.0 and 1.0.");

    public static Failure CodeDocumentIdInvalid() => new(
        "400-code-document-id-invalid",
        "The 'documentId' field must be a positive integer.");

    public static Failure CodeDocumentNotFound(long documentId) => new(
        "404-code-document-not-found",
        $"Code document with id {documentId} was not found.");

    public static Failure KindFilterValueRequired() => new(
        "400-kind-filter-value-required",
        "The 'kind' field must not be empty or blank when provided.");

    public static Failure KindFilterValueTooLong(int maxLength) => new(
        "400-kind-filter-value-too-long",
        $"The 'kind' field must not exceed {maxLength} characters.");

    public static Failure QualifiedNameFilterValueRequired() => new(
        "400-qualified-name-filter-value-required",
        "The 'qualifiedName' filter's 'value' field is required and must not be empty or blank.");

    public static Failure QualifiedNameFilterValueTooLong(int maxLength) => new(
        "400-qualified-name-filter-value-too-long",
        $"The 'qualifiedName' filter's 'value' field must not exceed {maxLength} characters.");
}
