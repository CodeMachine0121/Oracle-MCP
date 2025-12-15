using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle;

internal interface IOracleSchemaSearcher
{
    Task ReadSchemaTableHitsAsync(
        DbConnection connection,
        OracleConnectionOptions options,
        string keyword,
        string? owner,
        int maxHits,
        List<OracleSchemaHit> hits,
        CancellationToken cancellationToken);

    Task ReadSchemaColumnHitsAsync(
        DbConnection connection,
        OracleConnectionOptions options,
        string keyword,
        string? owner,
        int maxHits,
        List<OracleSchemaHit> hits,
        CancellationToken cancellationToken);
}
