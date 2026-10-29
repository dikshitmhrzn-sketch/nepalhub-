using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
using NepalHub.Api.Features.Email;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Jobs;
using NepalHub.Api.Features.Leaderboard;
using NepalHub.Api.Features.Moderation;
using NepalHub.Api.Features.Ownership;
using NepalHub.Api.Features.Payments;
using NepalHub.Api.Features.Quiz;
using NepalHub.Api.Features.Search;
using NepalHub.Api.Features.Students;
using NepalHub.Api.Features.Subscriptions;

var builder = WebApplication.CreateBuilder(args);

// Outside Development the logs are read by a shipper or a grep on the server rather than by a
// person watching a terminal scroll, so each line is one JSON object: level, timestamp, scope
// (which carries the request id) and message as fields instead of colour and padding. Development
// keeps the plain console because that log is read by eye as it goes past.
if (builder.Environment.IsProduction())
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
    });
}

const string FrontendCorsPolicy = "frontend";

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var connectionString = builder.Configuration.GetConnectionString("Default");
var serverVersionText = builder.Configuration.GetValue("Database:ServerVersion", "8.4.0-mysql");
var databaseConfigured = !string.IsNullOrWhiteSpace(connectionString);

// The schema is applied by the API itself, so a deploy is a restart rather than a manual
// `dotnet ef database update` somebody has to remember. A host that wants the schema change
// applied separately sets this to false and runs the update itself before starting the build.
var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
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

// Registering a listing and asking for it to be checked both write into the same staff
// queue a claim does, so both are bounded like filing a claim per IP: one person
// legitimately registers a handful of listings they own, while a loop registering
// hundreds is exactly the flood the queue exists to not be handed.
var businessRateLimitPermitLimit = builder.Configuration.GetValue("Business:RateLimit:PermitLimit", 10);
var businessRateLimitWindow = builder.Configuration.GetValue("Business:RateLimit:WindowSeconds", 3600)
    is { } businessWindowSeconds
    ? TimeSpan.FromSeconds(businessWindowSeconds)
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

// Starting or reconciling a checkout is bounded like booking per IP: both spend something
// scarce — a seat and now money — and the loop worth stopping is the one firing off
// checkouts it never intends to pay for. The quota covers verify too, because every
// verify reaches the provider on the buyer's behalf and that traffic is worth rationing
// before it reaches the provider's servers.
var paymentRateLimitPermitLimit = builder.Configuration.GetValue("Payment:RateLimit:PermitLimit", 20);
var paymentRateLimitWindow = builder.Configuration.GetValue("Payment:RateLimit:WindowSeconds", 3600)
    is { } paymentWindowSeconds
    ? TimeSpan.FromSeconds(paymentWindowSeconds)
    : TimeSpan.FromHours(1);

// Asking for a link that hands over an account — the reset half of recovery and the
// confirmation half of registration — is bounded like filing a report per IP: a person
// asks a handful of times in an hour, while a loop asking hundreds is either harvesting
// addresses or grinding tokens. Both endpoints answer the same either way, so the quota
// is the only thing a flood ever learns.
var passwordResetRateLimitPermitLimit = builder.Configuration.GetValue("PasswordReset:RateLimit:PermitLimit", 10);
var passwordResetRateLimitWindow = builder.Configuration.GetValue("PasswordReset:RateLimit:WindowSeconds", 3600)
    is { } passwordResetWindowSeconds
    ? TimeSpan.FromSeconds(passwordResetWindowSeconds)
    : TimeSpan.FromHours(1);

// Resending a confirmation link is bounded on the same reasoning and the same hour: the
// legitimate caller lost one email, while a loop is using the endpoint to make this host
// write to somebody else's inbox at volume.
var resendVerificationRateLimitPermitLimit = builder.Configuration.GetValue("ResendVerification:RateLimit:PermitLimit", 10);
var resendVerificationRateLimitWindow = builder.Configuration.GetValue("ResendVerification:RateLimit:WindowSeconds", 3600)
    is { } resendVerificationWindowSeconds
    ? TimeSpan.FromSeconds(resendVerificationWindowSeconds)
    : TimeSpan.FromHours(1);

// Changing a password requires the current one, so the quota exists for the caller who is
// grinding at it rather than the one changing it: a handful of attempts in an hour is a
// person who keeps fat-fingering, and the loop worth stopping is trying to guess the
// current password through an endpoint that does not lock out.
var changePasswordRateLimitPermitLimit = builder.Configuration.GetValue("ChangePassword:RateLimit:PermitLimit", 10);
var changePasswordRateLimitWindow = builder.Configuration.GetValue("ChangePassword:RateLimit:WindowSeconds", 3600)
    is { } changePasswordWindowSeconds
    ? TimeSpan.FromSeconds(changePasswordWindowSeconds)
    : TimeSpan.FromHours(1);

// Bound once here and injected into the service as well, so the numbers the API advertises
// and the numbers it awards cannot drift apart.
var quizSettings = builder.Configuration.GetSection(QuizSettings.SectionName).Get<QuizSettings>() ?? new QuizSettings();
var quizRateLimit = new QuizRateLimitOptions(quizSettings);

// The payment section as committed carries only the provider name and the environment —
// the merchant code and the signing key are bound from user-secrets or the environment
// and are absent from every committed file, which is why IsConfigured stays false on a
// fresh clone and checkout answers "not configured" until somebody deliberately wires it.
var paymentSettings = builder.Configuration
    .GetSection(PaymentSettings.SectionName)
    .Get<PaymentSettings>() ?? new PaymentSettings();

// The hosts follow the environment rather than being spelled out in configuration, so
// switching to production is one word and cannot leave a sandbox URL in a live setting.
// Either can still be overridden in configuration for a host that differs.
if (string.Equals(paymentSettings.Environment, "production", StringComparison.OrdinalIgnoreCase))
{
    paymentSettings.CheckoutBaseUrl = new Uri("https://epay.esewa.com.np");
    paymentSettings.StatusBaseUrl = new Uri("https://esewa.com.np");
}

// Where mail leaves the building. Checked before the host starts, so a bad setting — an
// unknown provider, a development sender pointed at a live deployment, links built from no
// origin at all — stops the boot with one sentence instead of surfacing later as mail that
// never arrives or an account nobody can sign into.
var emailSettings = builder.Configuration
    .GetSection(EmailSettings.SectionName)
    .Get<EmailSettings>() ?? new EmailSettings();

if (emailSettings.Validate(builder.Environment.IsProduction()) is { } emailProblem)
{
    throw new InvalidOperationException(emailProblem);
}

// Enums go over the wire as their names, so the catalogue filter and the input kinds
// stay readable and a new member cannot silently renumber the ones before it.
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
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
        // The confirmation gate is enforced in the endpoints rather than here: Identity's
        // own switch refuses unconfirmed accounts before it has even looked at the
        // password, which would answer differently for an address that exists — and the
        // answer for a wrong password has to stay the same for everyone.
        options.SignIn.RequireConfirmedEmail = false;

        // Identity mints and verifies the email and reset tokens with these two providers,
        // each on its own configured clock, so a reset link expires in an hour while a
        // confirmation link stays valid for the day.
        options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProvider.ProviderName;
        options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<EmailConfirmationTokenProvider>(EmailConfirmationTokenProvider.ProviderName)
    .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

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

    // Registering a listing and submitting it for verification are the claim's twin: both
    // open a row in a queue staff settle, so the quota counts submissions per IP in an hour
    // rather than people per account.
    options.AddPolicy(RateLimitPolicies.Business, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = businessRateLimitPermitLimit,
            Window = businessRateLimitWindow,
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

    // Checkouts and verifications share booking's hour and quota, for the same reason and
    // with the same per-IP accounting: each one reaches a payment provider on the caller's
    // behalf, so a loop of them is traffic we would be sending to eSewa.
    options.AddPolicy(RateLimitPolicies.Payment, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = paymentRateLimitPermitLimit,
            Window = paymentRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    // Both halves of recovery — asking for the link and spending it — draw on one quota,
    // so a flood cannot switch between the two endpoints and get double the attempts at
    // somebody else's account.
    options.AddPolicy(RateLimitPolicies.PasswordReset, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = passwordResetRateLimitPermitLimit,
            Window = passwordResetRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    options.AddPolicy(RateLimitPolicies.ResendVerification, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = resendVerificationRateLimitPermitLimit,
            Window = resendVerificationRateLimitWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    options.AddPolicy(RateLimitPolicies.ChangePassword, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = changePasswordRateLimitPermitLimit,
            Window = changePasswordRateLimitWindow,
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

// Registration's other half: the request an owner opens, the queue staff settle and the
// approval that publishes it, sharing that same request-scoped context so the badge, the
// publication, the review and its audit line commit together or not at all.
builder.Services.AddScoped<BusinessVerificationService>();

// Plans, entitlement and the request queue, sharing that same request-scoped context for
// the same reason: a grant and its audit line commit together or not at all.
builder.Services.AddScoped<SubscriptionService>();

// Bookings hold the event counter and the attendee row in one transaction, so they need the
// same single request-scoped context: the latch and the row it pays for must commit together.
builder.Services.AddScoped<BookingService>();

// Which provider answers checkout is configuration, not code: a configured provider is
// registered as the adapter, an unconfigured install gets the one that refuses, and
// nothing else in the request pipeline knows the difference. The HTTP client is named so
// a test host can stand in for eSewa's status endpoint without touching either.
builder.Services.AddSingleton(paymentSettings);
builder.Services.AddHttpClient(EsewaPaymentGateway.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

if (paymentSettings.IsConfigured)
{
    builder.Services.AddScoped<IPaymentGateway, EsewaPaymentGateway>();
}
else
{
    builder.Services.AddSingleton<IPaymentGateway, RejectingPaymentGateway>();
}

// The settlement engine shares the request's AppDbContext, so the payment row, the seat
// or entitlement it buys and any audit line all commit by one SaveChanges or not at all.
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<PaymentReconciliationWorker>();

// One identity for the data-protection keys, whatever the deployment's content-root path
// happens to be: the default discriminator is that path, so without this every redeploy
// (or clone, or CI workspace) rotates the key ring and every verification link and session
// cookie issued before the change stops verifying. DataProtection:KeyPath points the ring
// itself at a folder that survives a redeploy — or is shared across instances — instead of
// living beside the binaries. A host whose filesystem is wiped on every start (a container
// platform with no volume) sets DataProtection:Provider=database instead, and the same
// ring is kept in the DataProtectionKeys table through the AppDbContext.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("NepalHub");
if (builder.Configuration["DataProtection:KeyPath"] is { Length: > 0 } keyPath)
{
    dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(keyPath));
}
else if (databaseConfigured
    && string.Equals(builder.Configuration["DataProtection:Provider"], "database", StringComparison.OrdinalIgnoreCase))
{
    dataProtection.PersistKeysToDbContext<AppDbContext>();
}

// Which sender answers is configuration, not code, mirroring the payment gateway: a
// configured provider is registered as the adapter, and an unconfigured install gets the
// one that accepts every message and sends none — so the default host behaves exactly as
// it did before this module existed.
builder.Services.AddSingleton(emailSettings);
builder.Services.AddSingleton<EmailLinkBuilder>();
builder.Services.AddSingleton<EmailSendCooldown>();

if (string.Equals(emailSettings.Provider, EmailSettings.ProviderFile, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}
else if (string.Equals(emailSettings.Provider, EmailSettings.ProviderSmtp, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

// The account events that end in a message: scoped, so it shares the request's
// UserManager and mints its tokens against the same user the endpoint is acting on.
builder.Services.AddScoped<EmailDispatchService>();

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

// Outermost, so the request id and the timing line cover every other piece of the pipeline,
// including the answer an exception handler produces.
app.UseMiddleware<RequestLoggingMiddleware>();

// The built SPA, when one has been placed where the API can find it, is served from this same
// origin: the deployment runbook copies frontend/dist to Frontend:DistPath (wwwroot by default)
// and one host then ships both halves. The decision is taken after Build() and the provider is
// passed explicitly to every piece of static-file middleware, so serving does not depend on the
// web-root convention being resolvable for a folder that may sit outside the content root. A host
// without that folder keeps answering JSON only, exactly as it does today.
var spaRootPath = Path.Combine(
    app.Environment.ContentRootPath,
    app.Configuration["Frontend:DistPath"] ?? "wwwroot");
var spaFiles = Directory.Exists(spaRootPath) ? new PhysicalFileProvider(spaRootPath) : null;


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

// One host behind one reverse proxy, so the default believed-proxy rule (loopback) is already
// the right one: only a proxy this app trusts may rewrite X-Forwarded-For and X-Forwarded-Proto,
// and forged headers arriving from the open internet are dropped rather than believed. Any further
// hop can be named in ForwardedHeaders:KnownProxies. A platform whose edge rewrites the forwarded
// header wholesale - Vercel replaces X-Forwarded-For outright, so a client cannot inject its own -
// reaches the app from a pool of router addresses too dynamic to name one by one, and is opted
// into with ForwardedHeaders:TrustAnyProxy: clearing the known-proxy list means believing
// whatever address is in front, which is safe only where that platform is the sole route in.
// This runs before everything the forwarded
// values decide: the rate limiter buckets by client address, HSTS applies only once the scheme is
// https, and both auth cookies keep SecurePolicy.SameAsRequest, which under the forwarded scheme
// means "Secure in production, not Secure on localhost" without a second code path.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
{
    if (IPAddress.TryParse(proxy, out var knownProxy))
    {
        forwardedHeaders.KnownProxies.Add(knownProxy);
    }
}

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAnyProxy"))
{
    forwardedHeaders.KnownNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
}

app.UseForwardedHeaders(forwardedHeaders);
app.UseHsts();
app.UseHttpsRedirection();

if (spaFiles is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFiles });
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AntiforgeryMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (spaFiles is not null)
{
    // A typo'd API path must still answer as the API. Without this route the SPA fallback below
    // would return index.html with a 200 for any unknown /api/... path, and every client would
    // then fail to parse a page as an envelope. Real controller routes are more specific, so
    // they keep winning the match, and both of these opt out of the default-deny fallback policy
    // because neither the page nor a 404 body is a protected resource.
    app.Map("/api/{**path}", () => Results.Json(
            ApiResponse<object>.Fail(new ApiError("not_found", "No such endpoint.")),
            statusCode: StatusCodes.Status404NotFound))
        .AllowAnonymous();

    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = spaFiles })
        .AllowAnonymous();
}

async Task ApplySchemaAndSeedAsync()
{
    if (!databaseConfigured)
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();

    // Pending migrations are applied here, before the first seeder reads the schema, because a
    // fresh database has to reach a runnable state on its own and every seeder below assumes the
    // tables exist. One host means no second process can race the schema, and MySQL's implicit
    // commits make a half-applied migration visible rather than silent - the history row is
    // written after the DDL, so a crash leaves the migration pending and the next start retries.
    if (autoMigrate)
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedAsync(roleManager, CancellationToken.None);

    // One development administrator, after the roles it holds exist. Config-driven and
    // Development-only: with no DevAdmin:Email set it does nothing, which is why the test
    // host, a fresh clone and production all run past this line unchanged.
    if (app.Environment.IsDevelopment())
    {
        await DevAdminSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            app.Configuration,
            app.Environment,
            app.Logger,
            CancellationToken.None);
    }

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

    // The listing seeders below are demo content; the calculators, quiz, resources, roles and
    // plans above and below are not. An empty production database without roles or calculators
    // is a broken one, while one that grew twenty-six fictional businesses simply by booting is
    // a lie the directory would then tell its users. The policy lives in SeedingPolicy so the
    // rule has one definition and one test, and it defaults to off only in Production.
    var seedDemoContent = SeedingPolicy.SeedsDemoContent(builder.Configuration, builder.Environment);
    if (!seedDemoContent)
    {
        app.Logger.LogInformation(
            "Demo listings are not seeded on this host ({Environment}): businesses, jobs, deals and events keep whatever the database already holds.",
            app.Environment.EnvironmentName);
    }

    if (seedDemoContent)
    {
        // The job seeder gets the same date as the resource seeder for the same reason: a seeded
        // deadline has to be computed against the same "today" the application will use to decide
        // what has already closed.
        await JobSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<INepalDateProvider>().Today,
            CancellationToken.None);
    }

    if (seedDemoContent)
    {
        // The business seeder needs no date: opening hours are wall-clock times that do not expire.
        await BusinessSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            CancellationToken.None);
    }

    // Plans are reference data, inserted once by tier and never touched again, so a staff
    // price edit or a deactivated plan survives a restart.
    await PlanSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        CancellationToken.None);

    if (seedDemoContent)
    {
        // The offers and events seeders get the same date the resource and job seeders get, for the
        // same reason: a seeded end date has to be computed against the same "today" the application
        // will use to decide what has already expired. Two different clocks would let a row be seeded
        // as live and be treated as finished on the same request.
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
}

if (app.Environment.IsEnvironment("Testing"))
{
    // The test suite rebuilds the database once per process and relies on the host's startup
    // to migrate and seed it before the first request, so the work stays synchronous here.
    await ApplySchemaAndSeedAsync();
}
else
{
    // Anywhere but the test host, Kestrel answers first. A container platform's health probe
    // waits only a few seconds for the port to open, and a first-boot migration against a
    // remote free-tier database can take longer than that - which used to surface as an
    // opaque FUNCTION_INVOCATION_FAILED before a single request was served. The schema work
    // now runs in the background: /api/health reports the database as unreachable until it
    // finishes and connected once it has, and a failure is logged rather than killing the
    // process, so the next start retries and an operator can read what went wrong.
    _ = Task.Run(async () =>
    {
        try
        {
            await ApplySchemaAndSeedAsync();
            app.Logger.LogInformation("Database schema and seed data are ready.");
        }
        catch (Exception exception)
        {
            app.Logger.LogError(
                exception,
                "Database migration or seeding failed. The site is up but database-backed endpoints will not work until the next start succeeds.");
        }
    }, CancellationToken.None);
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
