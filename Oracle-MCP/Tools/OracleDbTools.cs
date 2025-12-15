using System.ComponentModel;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using Oracle_MCP.Models;
using Oracle_MCP.Services;

using Oracle;

namespace Oracle_MCP.Tools;

internal sealed class OracleDbTools(IOracleDbService oracleDbService, ILogger<OracleDbTools> logger)
{

    [McpServerTool]
    [Description("Checks Oracle database connectivity using ORACLE_CONNECTION_STRING.")]
    public async Task<OracleToolResponse<OraclePingResult>> OraclePing()
    {
        logger.LogTrace("oracle_ping invoked");
        return await oracleDbService.PingAsync(CancellationToken.None);
    }

    [McpServerTool]
    [Description("Executes a read-only Oracle SQL query (SELECT / WITH ... SELECT) with optional bind parameters.")]
    public async Task<OracleToolResponse<OracleQueryResult>> OracleQuery(
        [Description("Read-only SQL (SELECT / WITH ... SELECT). Do not include ';'.")]
        string sql,
        [Description("Bind parameters map (e.g. {\"id\": 123} for :id).")]
        Dictionary<string, object?>? parameters = null,
        [Description("Maximum rows to return (defaults to ORACLE_DEFAULT_MAX_ROWS).")]
        int? max_rows = null)
    {
        logger.LogTrace("oracle_query invoked");
        var request = new OracleQueryRequest(sql, parameters, max_rows);
        return await oracleDbService.QueryAsync(request, CancellationToken.None);
    }

    [McpServerTool]
    [Description("Searches Oracle schema metadata (tables/columns) by keyword using ALL_TABLES and ALL_TAB_COLUMNS.")]
    public async Task<OracleToolResponse<OracleSchemaSearchResult>> OracleSearchSchema(
        [Description("Keyword to search in table/column names.")]
        string keyword,
        [Description("Optional schema/owner to constrain results (e.g. HR).")]
        string? owner = null,
        [Description("Maximum hits to return (default 50, max 200).")]
        int? max_hits = null)
    {
        logger.LogTrace("oracle_search_schema invoked");
        var request = new OracleSchemaSearchRequest(keyword, owner, max_hits);
        return await oracleDbService.SearchSchemaAsync(request, CancellationToken.None);
    }
}
