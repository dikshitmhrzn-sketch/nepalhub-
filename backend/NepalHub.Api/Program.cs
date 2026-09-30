using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Data.Seeding;
using NepalHub.Api.Features.Auth;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var connectionString = builder.Configuration.GetConnectionString("Default");
var serverVersionText = builder.Configuration.GetValue("Database:ServerVersion", "8.4.0-mysql");
var databaseConfigured = !string.IsNullOrWhiteSpace(connectionString);
var authRateLimitPermitLimit = builder.Configuration.GetValue("Auth:RateLimit:PermitLimit", 20);
var authRateLimitWindow = builder.Configuration.GetValue("Auth:RateLimit:WindowSeconds", 60) is { } windowSeconds
    ? TimeSpan.FromSeconds(windowSeconds)
    : TimeSpan.FromMinutes(1);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var fieldErrors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new FieldError(
                ToCamelCase(entry.Key),
                "invalid",
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? "This value is not valid." : error.ErrorMessage)))
            .ToArray();

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail(new ApiError("validation_failed", "The request is not valid.", fieldErrors)));
    });

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

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = AuthConstants.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AuthConstants.CsrfHeaderName;
    options.Cookie.Name = AuthConstants.CsrfCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.FormFieldName = "csrf_token";
    options.SuppressXFrameOptionsHeader = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(AuthPolicies.StaffOnly, policy => policy.RequireRole(Roles.Admin, Roles.Moderator));

    // Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(new ApiError(
                "rate_limited",
                "Too many attempts. Wait a moment and try again.")),
            cancellationToken);
    };

    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authRateLimitPermitLimit,
            Window = authRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

if (databaseConfigured)
{
    builder.Services.AddDbContext<AppDbContext>(options => options
        .UseMySql(
            connectionString,
            ServerVersion.Parse(serverVersionText),
            mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
        .UseSnakeCaseNamingConvention());
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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AntiforgeryMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (databaseConfigured)
{
    await using var scope = app.Services.CreateAsyncScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedAsync(roleManager, CancellationToken.None);
}

app.Run();

static string ToCamelCase(string name)
{
    if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
    {
        return name;
    }

    var builder = new StringBuilder(name.Length);
    for (var index = 0; index < name.Length; index++)
    {
        var character = name[index];
        builder.Append(index == 0 ? char.ToLowerInvariant(character) : character);
    }

    return builder.ToString();
}

// Exposed so the integration tests can boot the real host with WebApplicationFactory.
public partial class Program;
