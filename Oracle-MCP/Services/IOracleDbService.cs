using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

public interface IOracleDbService
{
    Task<OracleToolResponse<OraclePingResult>> PingAsync(CancellationToken cancellationToken);

    Task<OracleToolResponse<OracleQueryResult>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<OracleToolResponse<OracleSchemaSearchResult>> SearchSchemaAsync(
        string keyword,
        string? owner,
        int? maxHits,
        CancellationToken cancellationToken);
}

