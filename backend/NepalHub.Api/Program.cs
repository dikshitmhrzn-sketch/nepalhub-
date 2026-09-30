using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Data;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var connectionString = builder.Configuration.GetConnectionString("Default");
var serverVersionText = builder.Configuration.GetValue("Database:ServerVersion", "8.4.0-mysql");
var databaseConfigured = !string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }));

if (databaseConfigured)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.Parse(serverVersionText)));
}

var app = builder.Build();

if (!databaseConfigured)
{
    app.Logger.LogWarning(
        "No database connection string found. Set ConnectionStrings:Default via user-secrets or the "
        + "ConnectionStrings__Default environment variable. Database-backed endpoints are disabled.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseCors(FrontendCorsPolicy);
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
