using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle;

public interface IOracleDbConnectionFactory
{
    OracleToolResponse<DbConnection> Create(OracleConnectionOptions options);
}
