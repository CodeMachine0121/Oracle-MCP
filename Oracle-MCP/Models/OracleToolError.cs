namespace Oracle_MCP.Models;

public sealed record OracleToolError(
    string Message,
    string? Details = null);
