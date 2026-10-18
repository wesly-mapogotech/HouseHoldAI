using HouseHoldAI;
using ModelContextProtocol.AspNetCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Konfigurasi dibaca dari User Secrets atau environment variables.
var connectionString =
    builder.Configuration.GetConnectionString("Household");

var apiKey = builder.Configuration["MCP_API_KEY"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Household belum dikonfigurasi.");
}

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "MCP_API_KEY belum dikonfigurasi.");
}

// Database connection pool.
builder.Services.AddSingleton(
    _ => NpgsqlDataSource.Create(connectionString));

// Daftarkan MCP Server dengan transport HTTP.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<ScheduleTools>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
// Lindungi endpoint MCP menggunakan Bearer API key.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        var authorization =
            context.Request.Headers.Authorization.ToString();

        if (!string.Equals(
            authorization,
            $"Bearer {apiKey}",
            StringComparison.Ordinal))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsync("Unauthorized");
            return;
        }
    }

    await next();
});

// Endpoint untuk memeriksa apakah aplikasi berjalan.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "alia-mcp-server"
}));

// Endpoint MCP.
app.MapMcp("/mcp");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run("http://0.0.0.0:3001");
