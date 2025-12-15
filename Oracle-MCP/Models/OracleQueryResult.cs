namespace Oracle_MCP.Models;

public sealed record OracleColumnInfo(
    string Name,
    string? DbType = null);

public sealed record OracleQueryResult(
    IReadOnlyList<OracleColumnInfo> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated);

