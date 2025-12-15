namespace Oracle_MCP.Models;

public sealed record OracleConnectionOptions(
    string ConnectionString,
    int CommandTimeoutSeconds,
    int DefaultMaxRows,
    int MaxMaxRows)
{
    public static OracleToolResponse<OracleConnectionOptions> FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("ORACLE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return OracleToolResponse<OracleConnectionOptions>.Fail(new OracleToolError("Missing ORACLE_CONNECTION_STRING environment variable."));
        }

        return OracleToolResponse<OracleConnectionOptions>.Success(new OracleConnectionOptions(
            ConnectionString: connectionString,
            CommandTimeoutSeconds: ReadInt("ORACLE_COMMAND_TIMEOUT_SECONDS", 30),
            DefaultMaxRows: ReadInt("ORACLE_DEFAULT_MAX_ROWS", 200),
            MaxMaxRows: ReadInt("ORACLE_MAX_MAX_ROWS", 2000)));
    }

    private static int ReadInt(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }
}
