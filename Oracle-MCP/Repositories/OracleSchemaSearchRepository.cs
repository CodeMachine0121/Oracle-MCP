using System.Data;
using System.Data.Common;

using Oracle_MCP.Models;
using Oracle_MCP.Services;

namespace Oracle;

internal sealed class OracleSchemaSearchRepository : IOracleSchemaSearcher
{
    public async Task ReadSchemaTableHitsAsync(
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
        {
            sql += " and owner = :owner";
        }

        sql = $"select * from ({sql}) where rownum <= :limit";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        OracleCommandParameterBinder.AddParameter(command, "kw", keyword.ToUpperInvariant());

        if (owner is not null)
        {
            OracleCommandParameterBinder.AddParameter(command, "owner", owner);
        }

        OracleCommandParameterBinder.AddParameter(command, "limit", maxHits);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new OracleSchemaHit(reader.GetString(0), reader.GetString(1), "table"));
        }
    }

    public async Task ReadSchemaColumnHitsAsync(
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
        {
            sql += " and owner = :owner";
        }

        sql = $"select * from ({sql}) where rownum <= :limit";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        OracleCommandParameterBinder.AddParameter(command, "kw", keyword.ToUpperInvariant());
        if (owner is not null)
        {
            OracleCommandParameterBinder.AddParameter(command, "owner", owner);
        }
        OracleCommandParameterBinder.AddParameter(command, "limit", maxHits);

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
}