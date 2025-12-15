namespace Oracle_MCP.Models;

public sealed record OraclePingResult(
    bool Ok,
    string? DatabaseInfo = null);

