namespace Oracle_MCP.Models;

public sealed record OracleToolResponse<T>(
    bool Ok,
    T? Result = default,
    OracleToolError? Error = null)
{
    public static OracleToolResponse<T> Success(T result) => new(true, result, null);

    public static OracleToolResponse<T> Fail(OracleToolError error) => new(false, default, error);
}

