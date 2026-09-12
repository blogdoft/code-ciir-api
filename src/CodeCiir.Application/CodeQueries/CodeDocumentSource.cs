namespace CodeCiir.Application.CodeQueries;

/// <summary>
/// File locators for one indexed code document. <see cref="SourceFile"/> is relative to the
/// project root; <see cref="GitRawUrl"/> is null when the project has no raw Git URL configured.
/// </summary>
public sealed record CodeDocumentSource(long DocumentId, string? SourceFile, Uri? GitRawUrl);
