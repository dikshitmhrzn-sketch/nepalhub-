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
using NepalHub.Api.Features.Admin;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Badges;
using NepalHub.Api.Features.Bookings;
using NepalHub.Api.Features.Calculators;
using NepalHub.Api.Features.Calculators.Handlers;
using NepalHub.Api.Features.Businesses;
using NepalHub.Api.Features.Deals;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Jobs;
using NepalHub.Api.Features.Leaderboard;
using NepalHub.Api.Features.Moderation;
using NepalHub.Api.Features.Ownership;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Search;
using NepalHub.Api.Features.Students;
using NepalHub.Api.Features.Subscriptions;

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

// Signing out gets its own counter rather than sharing auth's: signing in and signing
// out are different acts, and a burst of one must not shut off the other. A signed-out
// visitor hammering the endpoint is cheap to answer and worth bounding anyway, because
// each call still tears down a session server-side.
var logoutRateLimitPermitLimit = builder.Configuration.GetValue("Logout:RateLimit:PermitLimit", 20);
var logoutRateLimitWindow = builder.Configuration.GetValue("Logout:RateLimit:WindowSeconds", 60)
    is { } logoutWindowSeconds
    ? TimeSpan.FromSeconds(logoutWindowSeconds)
    : TimeSpan.FromMinutes(1);
var calculateRateLimitPermitLimit = builder.Configuration.GetValue("Calculate:RateLimit:PermitLimit", 60);
var calculateRateLimitWindow = builder.Configuration.GetValue("Calculate:RateLimit:WindowSeconds", 60)
    is { } calculateWindowSeconds
    ? TimeSpan.FromSeconds(calculateWindowSeconds)
    : TimeSpan.FromMinutes(1);

// Search is limited harder than calculation. A calculation is one row of arithmetic, whereas one
// search runs six queries, and an unthrottled loop of them is the cheapest way to make the whole
// API slow. The ceiling is still generous enough to type a query per keystroke while someone is
// still typing, which is what the header form does with its debounce.
var searchRateLimitPermitLimit = builder.Configuration.GetValue("Search:RateLimit:PermitLimit", 60);
var searchRateLimitWindow = builder.Configuration.GetValue("Search:RateLimit:WindowSeconds", 60)
    is { } searchWindowSeconds
    ? TimeSpan.FromSeconds(searchWindowSeconds)
    : TimeSpan.FromMinutes(1);

// Filing a report is a write against a shared queue rather than a read of one, so the
// quota is deliberately much tighter than search's and counted over an hour: a person
// with a genuine complaint files a handful, while a loop filing hundreds is exactly the
// abuse this endpoint exists under.
var reportRateLimitPermitLimit = builder.Configuration.GetValue("Report:RateLimit:PermitLimit", 10);
var reportRateLimitWindow = builder.Configuration.GetValue("Report:RateLimit:WindowSeconds", 3600)
    is { } reportWindowSeconds
    ? TimeSpan.FromSeconds(reportWindowSeconds)
    : TimeSpan.FromHours(1);

// Claiming a listing lands in a staff queue, so it is bounded like filing a report rather than
// like reading a page: one person legitimately claims a handful of listings they own, while a
// loop claiming hundreds is exactly the flood the queue exists to not be handed.
var claimRateLimitPermitLimit = builder.Configuration.GetValue("Claim:RateLimit:PermitLimit", 10);
var claimRateLimitWindow = builder.Configuration.GetValue("Claim:RateLimit:WindowSeconds", 3600)
    is { } claimWindowSeconds
    ? TimeSpan.FromSeconds(claimWindowSeconds)
    : TimeSpan.FromHours(1);

// Booking takes a scarce seat, so it is bounded like a claim per IP rather than per account:
// the loop worth stopping books places it never intends to attend, and a person's own account
// is not what a scripted flood uses. The quota is double the claim's because booking and then
// releasing a place are two calls, and both must stay possible inside an hour of normal use.
var bookingRateLimitPermitLimit = builder.Configuration.GetValue("Booking:RateLimit:PermitLimit", 20);
var bookingRateLimitWindow = builder.Configuration.GetValue("Booking:RateLimit:WindowSeconds", 3600)
    is { } bookingWindowSeconds
    ? TimeSpan.FromSeconds(bookingWindowSeconds)
    : TimeSpan.FromHours(1);

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

    options.AddPolicy(RateLimitPolicies.Logout, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = logoutRateLimitPermitLimit,
            Window = logoutRateLimitWindow,
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

    // Search is a read, so a page of results is cheap, but a page of results is six queries and one
    // request per submitted search. The quota is per IP and generous for human use; the point is to
    // stop a loop, not to ration searches.
    options.AddPolicy(RateLimitPolicies.Search, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = searchRateLimitPermitLimit,
            Window = searchRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    // Reports are filed per IP, on the same reasoning as auth: one visitor filing a
    // hundred complaints about the same listing is the problem, and the person's account
    // is not the thing to throttle when the requests are coming from a loop.
    options.AddPolicy(RateLimitPolicies.Report, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = reportRateLimitPermitLimit,
            Window = reportRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    // Claims share the report's reasoning and its hour-long window: both write into a queue a
    // person works through, so the quota counts submissions per IP, not people per account.
    options.AddPolicy(RateLimitPolicies.Claim, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = claimRateLimitPermitLimit,
            Window = claimRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    // Booking shares the claim's per-IP hour: both spend something scarce into a queue or a
    // counter, and both are worth the same protection against a loop.
    options.AddPolicy(RateLimitPolicies.Booking, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = bookingRateLimitPermitLimit,
            Window = bookingRateLimitWindow,
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
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<DealService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<SearchService>();

// Ranks on read, so there is no derived board to refresh and no background job to fall
// behind: both windows are indexed queries over tables the quiz already maintains.
builder.Services.AddScoped<LeaderboardService>();

// The audit log shares the request's AppDbContext, which is the whole point: an action
// and the entry recording it are written by one SaveChanges or not at all. Moderation
// and user administration take the interface; the concrete class also serves the admin
// screen's read of the trail.
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<IAuditLog>(provider => provider.GetRequiredService<AuditLogService>());
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<UserAdminService>();

// Claims and the queue that decides them. Scoped like every other feature service, so the
// claim, the audit entry recording its approval and the ownership change it makes are all
// tracked by the one AppDbContext of the request.
builder.Services.AddScoped<OwnershipService>();

// Plans, entitlement and the request queue, sharing that same request-scoped context for
// the same reason: a grant and its audit line commit together or not at all.
builder.Services.AddScoped<SubscriptionService>();

// Bookings hold the event counter and the attendee row in one transaction, so they need the
// same single request-scoped context: the latch and the row it pays for must commit together.
builder.Services.AddScoped<BookingService>();

// Grants badges after a quiz commits and serves the account page's list. Scoped, so it
// shares the request's AppDbContext and sees the quiz writes in that same context.
builder.Services.AddScoped<BadgeAwardService>();

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

    // The job seeder gets the same date for the same reason: a seeded deadline has to be computed
    // against the same "today" the application will use to decide what has already closed.
    await JobSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<INepalDateProvider>().Today,
        CancellationToken.None);

    // The business seeder needs no date: opening hours are wall-clock times that do not expire.
    await BusinessSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        CancellationToken.None);

    // Plans are reference data, inserted once by tier and never touched again, so a staff
    // price edit or a deactivated plan survives a restart.
    await PlanSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        CancellationToken.None);

    // The offers and events seeders get the same date the resource and job seeders get, for the same
    // reason: a seeded end date has to be computed against the same "today" the application will use to
    // decide what has already expired. Two different clocks would let a row be seeded as live and be
    // treated as finished on the same request.
    var nepalToday = scope.ServiceProvider.GetRequiredService<INepalDateProvider>().Today;

    await DealSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        nepalToday,
        CancellationToken.None);

    await EventSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        nepalToday,
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
