using Oracle_MCP.Services;
using Oracle_MCP.Tools;

using Oracle;

var builder = WebApplication.CreateBuilder(args);

// Configure all logs to go to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddTransient<IOracleDbConnectionFactory, OracleDbConnectionFactory>();
builder.Services.AddTransient<IOracleDatabaseInfoRepository, OracleDatabaseInfoRepository>();
builder.Services.AddTransient<IOracleDataMapper, OracleDataMapper>();
builder.Services.AddTransient<IOracleSchemaSearcher, OracleSchemaSearchRepository>();
builder.Services.AddTransient<IOracleDbService, OracleDbService>();

// Add the MCP services: the transport to use (streamable HTTP) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<OracleDbTools>();

var app = builder.Build();

app.MapMcp();
app.MapGet("/healthz", () => Results.Ok(new
{
    ok = true
}));

string? url = Environment.GetEnvironmentVariable("MCP_HTTP_URL");
if (!string.IsNullOrWhiteSpace(url))
{
    app.Urls.Add(url);
}

await app.RunAsync();
