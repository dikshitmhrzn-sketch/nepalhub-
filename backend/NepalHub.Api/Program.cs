using System.Text;
using System.Text.Json.Serialization;
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
using NepalHub.Api.Features.Calculators;
using NepalHub.Api.Features.Calculators.Handlers;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Students;

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
var calculateRateLimitPermitLimit = builder.Configuration.GetValue("Calculate:RateLimit:PermitLimit", 60);
var calculateRateLimitWindow = builder.Configuration.GetValue("Calculate:RateLimit:WindowSeconds", 60)
    is { } calculateWindowSeconds
    ? TimeSpan.FromSeconds(calculateWindowSeconds)
    : TimeSpan.FromMinutes(1);

// Bound once here and injected into the service as well, so the numbers the API advertises
// and the numbers it awards cannot drift apart.
var quizSettings = builder.Configuration.GetSection(QuizSettings.SectionName).Get<QuizSettings>() ?? new QuizSettings();
var quizRateLimit = new QuizRateLimitOptions(quizSettings);

// Enums go over the wire as their names, so the catalogue filter and the input kinds
// stay readable and a new member cannot silently renumber the ones before it.
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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

    options.AddPolicy(RateLimitPolicies.Calculate, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = calculateRateLimitPermitLimit,
            Window = calculateRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    // Only the submit endpoint is limited. Reading a day's questions is a cheap, idempotent
    // GET, and a player reloading the page must never be treated as abuse.
    //
    // The quota is read from the options holder on every partition lookup rather than
    // captured into a closure. That is what lets a test host lower the limit before it
    // issues a request, and it would let the limit be changed without restarting.
    options.AddPolicy(RateLimitPolicies.Quiz, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = quizRateLimit.PermitLimit,
            Window = quizRateLimit.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

// Each calculator is one handler. Registering them all under the interface is what
// lets CalculatorService look one up by slug, and it is the only place a new
// calculator has to be registered.
builder.Services.AddSingleton<ICalculatorHandler, LoanEmiHandler>();
builder.Services.AddSingleton<ICalculatorHandler, CompoundInterestHandler>();
builder.Services.AddSingleton<ICalculatorHandler, PercentageHandler>();
builder.Services.AddScoped<CalculatorService>();

// The quiz clock is the framework's own TimeProvider so tests can pin "today" without a
// new package and without the feature reaching for the machine clock directly.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INepalDateProvider, NepalDateProvider>();
builder.Services.AddSingleton(quizSettings);
builder.Services.AddSingleton(quizRateLimit);
builder.Services.AddScoped<QuizService>();
builder.Services.AddScoped<StudentResourceService>();

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
    await CalculatorSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        CancellationToken.None);
    await QuizSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        CancellationToken.None);

    // The resource seeder is given the Nepal date rather than reading the clock, so a seeded
    // scholarship deadline is computed against the same "today" the application will use to
    // decide what has already closed. Two different clocks would let a row be seeded as open
    // and be treated as expired on the same request.
    await StudentResourceSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<INepalDateProvider>().Today,
        CancellationToken.None);
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
