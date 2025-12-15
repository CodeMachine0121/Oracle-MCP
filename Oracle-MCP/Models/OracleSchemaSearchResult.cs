namespace Oracle_MCP.Models;

public sealed record OracleSchemaHit(
    string Owner,
    string TableName,
    string MatchType,
    string? ColumnName = null,
    string? DataType = null);

public sealed record OracleSchemaSearchResult(
    IReadOnlyList<OracleSchemaHit> Hits);

