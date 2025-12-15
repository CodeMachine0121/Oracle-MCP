using System.Data;
using System.Data.Common;

using Oracle_MCP.Models;

using Oracle;

namespace Oracle_MCP.Services;

public class OracleDbService(
    IOracleDbConnectionFactory connectionFactory,
    IOracleDatabaseInfoRepository databaseInfoRepository,
    IOracleDataMapper dataMapper,
    IOracleSchemaSearcher schemaSearcher)
    : IOracleDbService
{

    public async Task<OracleToolResponse<OraclePingResult>> PingAsync(CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
        {
            return OracleToolResponse<OraclePingResult>.Fail(optionsResponse.Error!);
        }

        var connectionResponse = connectionFactory.Create(optionsResponse.Result);
        if (!connectionResponse.Ok || connectionResponse.Result is null)
        {
            return OracleToolResponse<OraclePingResult>.Fail(connectionResponse.Error!);
        }

        await using var connection = connectionResponse.Result;
        try
        {
            await connection.OpenAsync(cancellationToken);

            string? dbInfo = await databaseInfoRepository.TryGetDatabaseInfoAsync(connection, optionsResponse.Result, cancellationToken);
            return OracleToolResponse<OraclePingResult>.Success(new OraclePingResult(true, dbInfo));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OraclePingResult>.Fail(new OracleToolError("Failed to connect to Oracle.",
                OracleErrorFormatter.SanitizeExceptionMessage(ex)));
        }
    }

    public async Task<OracleToolResponse<OracleQueryResult>> QueryAsync(
        OracleQueryRequest request,
        CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
            return OracleToolResponse<OracleQueryResult>.Fail(optionsResponse.Error!);

        if (!OracleSqlGuard.IsReadonlySql(request.Sql, out string? whyNot))
        {
            return OracleToolResponse<OracleQueryResult>.Fail(new OracleToolError("Only read-only SQL is allowed (SELECT / WITH ... SELECT).",
                whyNot));
        }

        int effectiveMaxRows = OracleSqlGuard.ClampMaxRows(request.MaxRows ?? optionsResponse.Result.DefaultMaxRows, optionsResponse.Result.MaxMaxRows);
        string wrappedSql = OracleSqlGuard.WrapWithRowNumLimit(request.Sql);

        var connectionResponse = connectionFactory.Create(optionsResponse.Result);
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

            if (request.Parameters is not null)
            {
                foreach ((string name, object? value) in request.Parameters)
                {
                    OracleCommandParameterBinder.AddParameter(command, name, value);
                }
            }

            OracleCommandParameterBinder.AddParameter(command, "mcp_max_rows", effectiveMaxRows);

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var columns = dataMapper.ReadColumns(reader);

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(dataMapper.ReadRow(reader, columns));
            }

            return OracleToolResponse<OracleQueryResult>.Success(new OracleQueryResult(
                columns,
                rows,
                rows.Count >= effectiveMaxRows));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OracleQueryResult>.Fail(new OracleToolError(OracleErrorFormatter.SanitizeExceptionMessage(ex)));
        }
    }

    public async Task<OracleToolResponse<OracleSchemaSearchResult>> SearchSchemaAsync(
        OracleSchemaSearchRequest request,
        CancellationToken cancellationToken)
    {
        var optionsResponse = OracleConnectionOptions.FromEnvironment();
        if (!optionsResponse.Ok || optionsResponse.Result is null)
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(optionsResponse.Error!);

        string normalizedKeyword = (request.Keyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedKeyword))
            return OracleToolResponse<OracleSchemaSearchResult>.Success(new OracleSchemaSearchResult(Array.Empty<OracleSchemaHit>()));

        int effectiveMaxHits = OracleSqlGuard.ClampMaxRows(request.MaxHits ?? 50, 200);
        string? normalizedOwner = string.IsNullOrWhiteSpace(request.Owner)
            ? null
            : request.Owner.Trim().ToUpperInvariant();

        var connectionResponse = connectionFactory.Create(optionsResponse.Result);
        if (!connectionResponse.Ok || connectionResponse.Result is null)
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(connectionResponse.Error!);

        await using var connection = connectionResponse.Result;
        try
        {
            await connection.OpenAsync(cancellationToken);

            var hits = new List<OracleSchemaHit>(effectiveMaxHits);
            await schemaSearcher.ReadSchemaTableHitsAsync(connection, optionsResponse.Result, normalizedKeyword, normalizedOwner, effectiveMaxHits, hits, cancellationToken);
            if (hits.Count < effectiveMaxHits)
            {
                await schemaSearcher.ReadSchemaColumnHitsAsync(connection, optionsResponse.Result, normalizedKeyword, normalizedOwner, effectiveMaxHits - hits.Count, hits,
                    cancellationToken);
            }

            return OracleToolResponse<OracleSchemaSearchResult>.Success(new OracleSchemaSearchResult(hits));
        }
        catch (Exception ex)
        {
            return OracleToolResponse<OracleSchemaSearchResult>.Fail(new OracleToolError("Oracle schema search failed.",
                OracleErrorFormatter.SanitizeExceptionMessage(ex)));
        }
    }
}
