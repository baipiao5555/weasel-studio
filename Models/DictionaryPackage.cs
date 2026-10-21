namespace WeaselStudio.Models;

public sealed record DictionaryPackage(
    string Id,
    string Name,
    string Description,
    string Url,
    string Sha256,
    long SizeBytes,
    string License,
    string SourceUrl,
    IReadOnlyList<string> SchemaIds);
