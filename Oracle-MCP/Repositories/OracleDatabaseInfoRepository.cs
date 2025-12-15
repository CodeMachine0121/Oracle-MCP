using System.Data;
using System.Data.Common;

using Oracle_MCP.Models;
using Oracle_MCP.Services;

namespace Oracle;

public class OracleDatabaseInfoRepository : IOracleDatabaseInfoRepository
{
    public async Task<string?> TryGetDatabaseInfoAsync(DbConnection connection, OracleConnectionOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select sys_context('userenv','db_name') as db_name from dual";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = options.CommandTimeoutSeconds;

            object? value = await command.ExecuteScalarAsync(cancellationToken);
            return value is DBNull or null ? null : value.ToString();
        }
        catch
        {
            return null;
        }
    }
}
