using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

using Oracle_MCP.Models;

namespace Oracle_MCP.Services;

public sealed class OracleDbService : IOracleDbService
{
    private static readonly Regex DangerousSqlRegex = new(
        @"\b(insert|update|delete|merge|drop|alter|truncate|create|grant|revoke|begin|declare|commit|rollback|execute|exec|call)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ForUpdateRegex = new(
        @"\bfor\s+update\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<OracleToolResponse<OraclePingResult>> PingAsync(CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
        {
            return OracleToolResponse<OraclePingResult>.Fail(optionsResponse.Error!);
        }

        var connectionResponse = TryCreateConnection(optionsResponse.Result);
        if (!connectionResponse.Ok || connectionResponse.Result is null)
        {
            return OracleToolResponse<OraclePingResult>.Fail(connectionResponse.Error!);
        }

        await using var connection = connectionResponse.Result;
        try
        {
            await connection.OpenAsync(cancellationToken);

            string? dbInfo = await TryGetDatabaseInfoAsync(connection, optionsResponse.Result, cancellationToken);
            return OracleToolResponse<OraclePingResult>.Success(new OraclePingResult(true, dbInfo));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OraclePingResult>.Fail(new OracleToolError("Failed to connect to Oracle.",
                SanitizeExceptionMessage(ex)));
        }
    }

    public async Task<OracleToolResponse<OracleQueryResult>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
            return OracleToolResponse<OracleQueryResult>.Fail(optionsResponse.Error!);

        if (!IsReadonlySql(sql, out string? whyNot))
        {
            return OracleToolResponse<OracleQueryResult>.Fail(new OracleToolError("Only read-only SQL is allowed (SELECT / WITH ... SELECT).",
                whyNot));
        }

        int effectiveMaxRows = ClampMaxRows(maxRows ?? optionsResponse.Result.DefaultMaxRows, optionsResponse.Result.MaxMaxRows);
        string wrappedSql = WrapWithRowNumLimit(sql, effectiveMaxRows);

        var connectionResponse = TryCreateConnection(optionsResponse.Result);
        if (!connectionResponse.Ok || connectionResponse.Result is null)
            return OracleToolResponse<OracleQueryResult>.Fail(connectionResponse.Error!);

        await using var connection = connectionResponse.Result;
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = wrappedSql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = optionsResponse.Result.CommandTimeoutSeconds;

            if (parameters is not null)
            {
                foreach ((string name, object? value) in parameters)
                {
                    AddParameter(command, name, value);
                }
            }

            AddParameter(command, "mcp_max_rows", effectiveMaxRows);

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var columns = ReadColumns(reader);

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadRow(reader, columns));
            }

            return OracleToolResponse<OracleQueryResult>.Success(new OracleQueryResult(
                columns,
                rows,
                rows.Count >= effectiveMaxRows));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OracleQueryResult>.Fail(new OracleToolError(SanitizeExceptionMessage(ex)));
        }
    }

    public async Task<OracleToolResponse<OracleSchemaSearchResult>> SearchSchemaAsync(
        string keyword,
        string? owner,
        int? maxHits,
        CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(optionsResponse.Error!);

        string normalizedKeyword = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedKeyword))
            return OracleToolResponse<OracleSchemaSearchResult>.Success(new OracleSchemaSearchResult(Array.Empty<OracleSchemaHit>()));

        int effectiveMaxHits = ClampMaxRows(maxHits ?? 50, 200);
        string? normalizedOwner = string.IsNullOrWhiteSpace(owner)
            ? null
            : owner.Trim().ToUpperInvariant();

        var connectionResponse = TryCreateConnection(optionsResponse.Result);
        if (!connectionResponse.Ok || connectionResponse.Result is null)
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(connectionResponse.Error!);

        await using var connection = connectionResponse.Result;
        try
        {
            await connection.OpenAsync(cancellationToken);

            var hits = new List<OracleSchemaHit>(effectiveMaxHits);
            await ReadSchemaTableHitsAsync(connection, optionsResponse.Result, normalizedKeyword, normalizedOwner, effectiveMaxHits, hits, cancellationToken);
            if (hits.Count < effectiveMaxHits)
            {
                await ReadSchemaColumnHitsAsync(connection, optionsResponse.Result, normalizedKeyword, normalizedOwner, effectiveMaxHits - hits.Count, hits,
                    cancellationToken);
            }

            return OracleToolResponse<OracleSchemaSearchResult>.Success(new OracleSchemaSearchResult(hits));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(new OracleToolError("Oracle schema search failed.",
                SanitizeExceptionMessage(ex)));
        }
    }

    private static int ClampMaxRows(int value, int max)
    {
        if (value <= 0) return 1;

        return value > max
            ? max
            : value;
    }

    private static bool IsReadonlySql(string sql, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(sql))
        {
            reason = "SQL is empty.";
            return false;
        }

        if (sql.Contains(';', StringComparison.Ordinal))
        {
            reason = "Multiple statements are not allowed.";
            return false;
        }

        string leading = StripLeadingComments(sql).TrimStart();
        if (leading.Length == 0)
        {
            reason = "SQL is empty after trimming comments.";
            return false;
        }

        bool startsReadOnly =
            leading.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
            leading.StartsWith("with", StringComparison.OrdinalIgnoreCase);

        if (!startsReadOnly)
        {
            reason = "SQL must start with SELECT or WITH.";
            return false;
        }

        string sanitized = RemoveSingleQuotedLiterals(leading);
        if (DangerousSqlRegex.IsMatch(sanitized))
        {
            reason = "Detected potentially non-read-only keyword.";
            return false;
        }

        if (ForUpdateRegex.IsMatch(sanitized))
        {
            reason = "SELECT ... FOR UPDATE is not allowed.";
            return false;
        }

        return true;
    }

    private static string StripLeadingComments(string sql)
    {
        string s = sql;
        while (true)
        {
            string trimmed = s.TrimStart();
            if (trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                int newline = trimmed.IndexOf('\n');
                s = newline >= 0
                    ? trimmed[(newline + 1)..]
                    : string.Empty;
                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf("*/", StringComparison.Ordinal);
                s = end >= 0
                    ? trimmed[(end + 2)..]
                    : string.Empty;
                continue;
            }

            return trimmed;
        }
    }

    private static string RemoveSingleQuotedLiterals(string sql)
    {
        char[] chars = sql.ToCharArray();
        bool inString = false;

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '\'')
            {
                if (inString) chars[i] = ' ';
                continue;
            }

            if (!inString)
            {
                inString = true;
                chars[i] = ' ';
                continue;
            }

            // Escaped quote inside a string: ''
            if (i + 1 < chars.Length && chars[i + 1] == '\'')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i++;
                continue;
            }

            inString = false;
            chars[i] = ' ';
        }

        if (inString)
        {
            // Unbalanced quotes; treat as unsafe.
            return string.Empty;
        }

        return new string(chars);
    }

    private static string WrapWithRowNumLimit(string sql, int maxRows)
    {
        // Oracle bind variable identifiers must start with a letter.
        return $"select * from ({sql}) where rownum <= :mcp_max_rows";
    }

    private static IReadOnlyList<OracleColumnInfo> ReadColumns(DbDataReader reader)
    {
        var columns = new List<OracleColumnInfo>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(new OracleColumnInfo(
                reader.GetName(i),
                SafeGetDataTypeName(reader, i)));
        }

        return columns;
    }

    private static string? SafeGetDataTypeName(DbDataReader reader, int ordinal)
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

    private static IReadOnlyDictionary<string, object?> ReadRow(DbDataReader reader, IReadOnlyList<OracleColumnInfo> columns)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Count; i++)
        {
            string name = columns[i].Name;
            object? value;
            if (reader.IsDBNull(i))
            {
                value = null;
            }
            else
            {
                value = CoerceToJsonSafe(reader.GetValue(i));
            }

            row[name] = value;
        }

        return row;
    }

    private static object? CoerceToJsonSafe(object value)
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
            _ when value is IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        string parameterName = name.StartsWith(':')
            ? name[1..]
            : name;
        var p = command.CreateParameter();
        p.ParameterName = parameterName;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }

    private static async Task<string?> TryGetDatabaseInfoAsync(DbConnection connection, OracleConnectionOptions options, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select sys_context('userenv','db_name') as db_name from dual";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = options.CommandTimeoutSeconds;

            object? value = await command.ExecuteScalarAsync(cancellationToken);
            return value is DBNull or null
                ? null
                : value.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static async Task ReadSchemaTableHitsAsync(
        DbConnection connection,
        OracleConnectionOptions options,
        string keyword,
        string? owner,
        int maxHits,
        List<OracleSchemaHit> hits,
        CancellationToken cancellationToken)
    {
        string sql =
            """
            select owner, table_name
            from all_tables
            where upper(table_name) like '%' || :kw || '%'
            """;

        if (owner is not null)
            sql += " and owner = :owner";

        sql = $"select * from ({sql}) where rownum <= :limit";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        AddParameter(command, "kw", keyword.ToUpperInvariant());
        if (owner is not null) AddParameter(command, "owner", owner);
        AddParameter(command, "limit", maxHits);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new OracleSchemaHit(
                reader.GetString(0),
                reader.GetString(1),
                "table"));
        }
    }

    private static async Task ReadSchemaColumnHitsAsync(
        DbConnection connection,
        OracleConnectionOptions options,
        string keyword,
        string? owner,
        int maxHits,
        List<OracleSchemaHit> hits,
        CancellationToken cancellationToken)
    {
        string sql =
            """
            select owner, table_name, column_name, data_type
            from all_tab_columns
            where upper(column_name) like '%' || :kw || '%'
               or upper(table_name) like '%' || :kw || '%'
            """;

        if (owner is not null)
            sql += " and owner = :owner";

        sql = $"select * from ({sql}) where rownum <= :limit";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        AddParameter(command, "kw", keyword.ToUpperInvariant());
        if (owner is not null) AddParameter(command, "owner", owner);
        AddParameter(command, "limit", maxHits);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new OracleSchemaHit(
                reader.GetString(0),
                reader.GetString(1),
                ColumnName: reader.GetString(2),
                DataType: reader.IsDBNull(3)
                    ? null
                    : reader.GetString(3),
                MatchType: "column"));
        }
    }

    private static OracleToolResponse<DbConnection> TryCreateConnection(OracleConnectionOptions options)
    {
        var oracleConnectionType = Type.GetType("Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess", false);
        if (oracleConnectionType is null)
        {
            return OracleToolResponse<DbConnection>.Fail(new OracleToolError("Oracle .NET driver not found. Install Oracle.ManagedDataAccess (or ensure it is available at runtime)."));
        }

        try
        {
            if (Activator.CreateInstance(oracleConnectionType, options.ConnectionString) is not DbConnection connection)
            {
                return OracleToolResponse<DbConnection>.Fail(new OracleToolError("Oracle driver loaded, but OracleConnection is not a DbConnection (unexpected)."));
            }

            return OracleToolResponse<DbConnection>.Success(connection);
        }
        catch (Exception ex)
        {
            return OracleToolResponse<DbConnection>.Fail(new OracleToolError("Failed to initialize Oracle connection.",
                SanitizeExceptionMessage(ex)));
        }
    }

    private static string SanitizeExceptionMessage(Exception ex)
    {
        return ex.GetBaseException().Message;
    }
}
