using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle;

internal interface IOracleDbConnectionFactory
{
    OracleToolResponse<DbConnection> Create(OracleConnectionOptions options);
}
