using BlogDoFT.Libs.ResultPattern;

namespace CodeCiir.Application.Feedback;

public static class FeedbackFailures
{
    public static Failure QuestionRequired() => new(
        "400-question-required", "The 'question' field is required and must not be empty or blank.");

    public static Failure QuestionTooLong(int maxLength) => new(
        "400-question-too-long", $"The 'question' field must not exceed {maxLength} characters.");

    public static Failure UsefulRequired() => new(
        "400-useful-required", "The 'useful' field is required.");

    public static Failure SimilaritiesRequired() => new(
        "400-similarities-required", "The 'similarities' field is required (may be an empty array).");

    public static Failure TooManySimilarities(int maxCount) => new(
        "400-too-many-similarities", $"The 'similarities' field must not contain more than {maxCount} values.");

    public static Failure UserRequired() => new(
        "400-user-required",
        "The 'user' field is required and must not be empty. For MCP callers, this must be the calling agent/tool's own name.");

    public static Failure UserTooLong(int maxLength) => new(
        "400-user-too-long", $"The 'user' field must not exceed {maxLength} characters.");

    public static Failure ReasonTooLong(int maxLength) => new(
        "400-reason-too-long", $"The 'reason' field must not exceed {maxLength} characters.");

    public static Failure InvalidDateRange() => new(
        "400-invalid-date-range", "The 'startDate' must not be after 'endDate'.");

    public static Failure WindowTooLarge() => new(
        "400-window-too-large", "The requested time window must not exceed 366 days (12 months).");

    public static Failure InvalidTimezone(string timezone) => new(
        "400-invalid-timezone", $"'{timezone}' is not a recognized IANA time zone name.");
}
