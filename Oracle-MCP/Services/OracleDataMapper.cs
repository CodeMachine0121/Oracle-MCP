using System.Data.Common;
using System.Globalization;

using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

public class OracleDataMapper : IOracleDataMapper
{
    public IReadOnlyList<OracleColumnInfo> ReadColumns(DbDataReader reader)
    {
        var columns = new List<OracleColumnInfo>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(new OracleColumnInfo(reader.GetName(i), SafeGetDataTypeName(reader, i)));
        }

        return columns;
    }

    public string? SafeGetDataTypeName(DbDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetDataTypeName(ordinal);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyDictionary<string, object?> ReadRow(DbDataReader reader, IReadOnlyList<OracleColumnInfo> columns)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Count; i++)
        {
            string name = columns[i].Name;
            object? value = reader.IsDBNull(i) ? null : CoerceToJsonSafe(reader.GetValue(i));
            row[name] = value;
        }

        return row;
    }

    public object? CoerceToJsonSafe(object value)
    {
        return value switch
        {
            null => null,
            string => value,
            bool => value,
            byte or sbyte or short or ushort or int or uint or long or ulong => value,
            float or double or decimal => value,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToBase64String(bytes),
            Guid g => g.ToString(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
