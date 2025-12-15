using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

public interface IOracleDataMapper
{
    IReadOnlyList<OracleColumnInfo> ReadColumns(DbDataReader reader);

    IReadOnlyDictionary<string, object?> ReadRow(DbDataReader reader, IReadOnlyList<OracleColumnInfo> columns);
}
