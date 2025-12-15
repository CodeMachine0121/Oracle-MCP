using System.Data.Common;

using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

internal interface IOracleDataMapper
{
    IReadOnlyList<OracleColumnInfo> ReadColumns(DbDataReader reader);

    string? SafeGetDataTypeName(DbDataReader reader, int ordinal);

    IReadOnlyDictionary<string, object?> ReadRow(DbDataReader reader, IReadOnlyList<OracleColumnInfo> columns);

    object? CoerceToJsonSafe(object value);
}
