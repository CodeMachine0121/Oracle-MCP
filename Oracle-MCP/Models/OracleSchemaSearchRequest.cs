namespace Oracle_MCP.Models;

public sealed record OracleSchemaSearchRequest(
    string Keyword,
    string? Owner,
    int? MaxHits);
