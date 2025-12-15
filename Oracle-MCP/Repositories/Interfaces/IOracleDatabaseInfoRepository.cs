using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

internal interface IOracleDatabaseInfoRepository
{
    Task<string?> TryGetDatabaseInfoAsync(DbConnection connection, OracleConnectionOptions options, CancellationToken cancellationToken);
}
