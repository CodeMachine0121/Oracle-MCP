using System.Data.Common;

using Oracle_MCP.Models;
using Oracle_MCP.Services;

namespace Oracle;

internal sealed class OracleDbConnectionFactory : IOracleDbConnectionFactory
{
    /// <summary>
    ///  create db connection
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>

    public OracleToolResponse<DbConnection> Create(OracleConnectionOptions options)
    {
        var oracleConnectionType = Type.GetType("Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess", false);
        if (oracleConnectionType is null)
        {
            return OracleToolResponse<DbConnection>.Fail(
                new OracleToolError("Oracle .NET driver not found. Install Oracle.ManagedDataAccess (or ensure it is available at runtime)."));
        }

        try
        {
            if (Activator.CreateInstance(oracleConnectionType, options.ConnectionString) is not DbConnection connection)
            {
                return OracleToolResponse<DbConnection>.Fail(
                    new OracleToolError("Oracle driver loaded, but OracleConnection is not a DbConnection (unexpected)."));
            }

            return OracleToolResponse<DbConnection>.Success(connection);
        }
        catch (Exception ex)
        {
            return OracleToolResponse<DbConnection>.Fail(new OracleToolError("Failed to initialize Oracle connection.",
                OracleErrorFormatter.SanitizeExceptionMessage(ex)));
        }
    }
}