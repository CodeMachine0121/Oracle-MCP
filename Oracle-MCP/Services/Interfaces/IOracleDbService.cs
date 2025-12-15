using Oracle_MCP.Models;

namespace Oracle;

public interface IOracleDbService
{
    Task<OracleToolResponse<OraclePingResult>> PingAsync(CancellationToken cancellationToken);

    Task<OracleToolResponse<OracleQueryResult>> QueryAsync(
        OracleQueryRequest request,
        CancellationToken cancellationToken);

    Task<OracleToolResponse<OracleSchemaSearchResult>> SearchSchemaAsync(
        OracleSchemaSearchRequest request,
        CancellationToken cancellationToken);
}
