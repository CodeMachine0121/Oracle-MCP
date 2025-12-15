using System.Collections.Generic;

namespace Oracle_MCP.Models;

public sealed record OracleQueryRequest(
    string Sql,
    IReadOnlyDictionary<string, object?>? Parameters,
    int? MaxRows);
