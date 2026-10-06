using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NepalHub.Api.Data;
using NepalHub.Api.Common;
using NepalHub.Api.Data.Entities;
using NepalHub.Api.Features.Auth;
using NepalHub.Api.Features.Bookings;
using NepalHub.Api.Features.Events;
using NepalHub.Api.Features.Payments;
using NepalHub.Api.Features.Subscriptions;

namespace NepalHub.Tests;

/// <summary>
/// A host with a provider wired up and every status answer scripted, so a test can stand in
/// the place of eSewa: no network, no merchant account, and no key that would otherwise have
/// to be committed somewhere to make the feature switchable on.
///
/// <para>
/// The signed halves of the protocol are the real ones — the outgoing form and the returned
/// payload both run through <see cref="EsewaPaymentGateway"/> — and only the server-to-server
/// status check is scripted, because that is the one call that leaves the building.
/// </para>
/// </summary>
public class PaymentsApiFactory : NepalHubApiFactory
{
    public const string SecretKey = "signing-key-for-the-tests-only";
    public const string ProductCode = "nepalhub-sandbox";

    public PaymentsApiFactory()
        : this(10_000)
    {
    }

    protected PaymentsApiFactory(int paymentRateLimitPermitLimit)
        : base(
            authRateLimitPermitLimit: 10_000,
            calculateRateLimitPermitLimit: 10_000,
            quizRateLimitPermitLimit: 10_000,
            searchRateLimitPermitLimit: 10_000,
            reportRateLimitPermitLimit: 10_000,
            logoutRateLimitPermitLimit: 10_000,
            claimRateLimitPermitLimit: 10_000,
            bookingRateLimitPermitLimit: 10_000,
            paymentRateLimitPermitLimit: paymentRateLimitPermitLimit)
    {
        Settings = new PaymentSettings
        {
            Provider = "esewa",
            Environment = "sandbox",
            ProductCode = ProductCode,
            SecretKey = SecretKey,
            ReturnBaseUrl = "https://app.nepalhub.test",
        };

        Gateway = new FakePaymentGateway(Settings);
    }

    public PaymentSettings Settings { get; }

    public FakePaymentGateway Gateway { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PaymentSettings>();
            services.AddSingleton(Settings);

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Gateway);
        });
    }
}

/// <summary>The same host, with only the payment quota lowered so it can be seen to bite.</summary>
public sealed class PaymentLimitedApiFactory : PaymentsApiFactory
{
    public const int PermitLimit = 3;

    public PaymentLimitedApiFactory()
        : base(PermitLimit)
    {
    }
}

/// <summary>
/// The status answer under the test's control. Everything else — how a form is signed, how a
/// returned payload is verified — is the real adapter's work.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly EsewaPaymentGateway _signedHalf;
    private readonly List<CheckoutIntent> _checkouts = [];

    public FakePaymentGateway(PaymentSettings settings) =>
        _signedHalf = new EsewaPaymentGateway(settings, new NoHttpCalls());

    /// <summary>What the provider says became of the reference being asked about.</summary>
    public ProviderNoticeKind Answer { get; set; } = ProviderNoticeKind.Pending;

    /// <summary>Stands in for the provider being unreachable, which must never read as a failure.</summary>
    public bool Unreachable { get; set; }

    /// <summary>A reply about a different total, the one thing that must be recorded and not believed.</summary>
    public int? ReportedAmount { get; set; }

    public int StatusChecks { get; private set; }

    public IReadOnlyList<CheckoutIntent> Checkouts => _checkouts;

    public Task<CheckoutHandshake> BeginCheckoutAsync(
        CheckoutIntent intent,
        CancellationToken cancellationToken = default)
    {
        _checkouts.Add(intent);

        return _signedHalf.BeginCheckoutAsync(intent, cancellationToken);
    }

    public Task<ProviderNotice?> ReadNoticeAsync(
        string body,
        string signature,
        CancellationToken cancellationToken = default) =>
        _signedHalf.ReadNoticeAsync(body, signature, cancellationToken);

    public Task<StatusReport?> CheckStatusAsync(
        string reference,
        int expectedTotalNpr,
        CancellationToken cancellationToken = default)
    {
        StatusChecks++;

        if (Unreachable)
        {
            return Task.FromResult<StatusReport?>(null);
        }

        return Task.FromResult<StatusReport?>(new StatusReport(
            reference,
            ReportedAmount ?? expectedTotalNpr,
            Answer,
            "TEST-REF"));
    }

    /// <summary>The signed half of the protocol never makes an HTTP call of its own.</summary>
    private sealed class NoHttpCalls : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

/// <summary>
/// Shared moves for the payment tests: the three endpoints a buyer uses, the admin's two, and
/// a signed return payload built the way the provider's own redirect would arrive.
/// </summary>
internal static class PaymentsApi
{
    internal const string CheckoutPath = "/api/payments/checkout";
    internal const string VerifyPath = "/api/payments/verify";
    internal const string ConfigPath = "/api/payments/config";
    internal const string HistoryPath = "/api/account/payments";
    internal const string AdminPath = "/api/admin/payments";

    public static async Task<PaymentConfigResponse> ReadConfigAsync(HttpClient client)
    {
        using var response = await client.GetAsync(ConfigPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var envelope = document.RootElement
            .Deserialize<ApiEnvelope<PaymentConfigResponse>>(TestJson.Options);
        Assert.NotNull(envelope);

        return envelope!.Data!;
    }

    public static Task<ApiResponseSnapshot> CheckoutForAsync(HttpClient client, object body) =>
        new AuthApiClient(client).PostForSnapshotAsync(CheckoutPath, body);

    public static async Task<CheckoutResponse> CheckoutAsync(HttpClient client, object body)
    {
        var snapshot = await CheckoutForAsync(client, body);
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<CheckoutResponse>(snapshot);
    }

    public static Task<ApiResponseSnapshot> VerifyForAsync(HttpClient client, object body) =>
        new AuthApiClient(client).PostForSnapshotAsync(VerifyPath, body);

    public static async Task<PaymentResponse> VerifyAsync(HttpClient client, object body)
    {
        var snapshot = await VerifyForAsync(client, body);
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);

        return Read<PaymentResponse>(snapshot);
    }

    public static async Task<PaymentResponse> ReadPaymentAsync(HttpClient client, string reference) =>
        await new AuthApiClient(client).GetAsync<PaymentResponse>($"/api/payments/{reference}");

    public static async Task<IReadOnlyList<PaymentSummaryResponse>> ReadHistoryAsync(HttpClient client)
    {
        var page = await new AuthApiClient(client)
            .GetAsync<PagedResult<PaymentSummaryResponse>>($"{HistoryPath}?pageSize=100");

        return page.Items;
    }

    public static async Task<PagedResult<AdminPaymentResponse>> ReadAdminListAsync(
        HttpClient admin,
        string query = "") =>
        await new AuthApiClient(admin).GetAsync<PagedResult<AdminPaymentResponse>>($"{AdminPath}{query}");

    public static Task<ApiResponseSnapshot> CheckStatusForAsync(HttpClient admin, string reference) =>
        new AuthApiClient(admin).PostForSnapshotAsync($"{AdminPath}/{reference}/check-status", null);

    /// <summary>How many payments a subscription request has behind it — one, however often checkout ran.</summary>
    public static async Task<int> PaymentCountForRequestAsync(NepalHubApiFactory factory, Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.PaymentTransactions
            .CountAsync(item => item.SubscriptionRequestId == requestId);
    }

    /// <summary>
    /// A returned payload the way eSewa appends it: Base64 over JSON, signed over the fields
    /// the payload itself names. Signing it here rather than pasting a canned blob keeps the
    /// test honest about which values the signature actually covers.
    /// </summary>
    public static (string Data, string Signature) SignedReturn(
        PaymentSettings settings,
        string reference,
        string status = "COMPLETE")
    {
        const string refId = "100001";
        const string signedFieldNames = "transaction_uuid,status,ref_id";

        var payload = new Dictionary<string, string>
        {
            ["transaction_uuid"] = reference,
            ["status"] = status,
            ["ref_id"] = refId,
            ["signed_field_names"] = signedFieldNames,
        };

        var signature = EsewaSignature.Compute(
            settings.SecretKey,
            new[]
            {
                new KeyValuePair<string, string>("transaction_uuid", reference),
                new KeyValuePair<string, string>("status", status),
                new KeyValuePair<string, string>("ref_id", refId),
            });

        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));

        return (data, signature);
    }

    private static T Read<T>(ApiResponseSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.Json);
        var envelope = document.RootElement.Deserialize<ApiEnvelope<T>>(TestJson.Options);

        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);

        return envelope.Data!;
    }
}

/// <summary>
/// What a checkout looks like before anybody has paid for it, and the one thing a fresh
/// install has to say about payments at all: whether there is a provider to send anybody to.
/// </summary>
public sealed class PaymentConfigurationTests(NepalHubApiFactory factory) : IClassFixture<NepalHubApiFactory>
{
    [Fact]
    public async Task Checkout_is_refused_while_no_provider_is_configured()
    {
        var config = await PaymentsApi.ReadConfigAsync(factory.CreateClient());
        Assert.False(config.Available);
        Assert.Equal("none", config.Provider);

        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var snapshot = await PaymentsApi.CheckoutForAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, snapshot.StatusCode);
        Assert.Equal("payments_not_configured", snapshot.Error!.Code);

        // No question was opened either: there was never anything to ask.
        var state = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.Null(state.PendingRequestId);
        Assert.Null(state.Tier);
    }
}

/// <summary>
/// Buying a plan: the queue is opened by the checkout and closed by the money, through the
/// same approval an administrator would have given it.
/// </summary>
public sealed class PaidSubscriptionTests(PaymentsApiFactory factory) : IClassFixture<PaymentsApiFactory>
{
    [Fact]
    public async Task A_confirmed_payment_opens_the_featured_window_and_closes_its_own_queue_row()
    {
        var (email, businessId, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var plans = await SubscriptionApi.ReadPlansAsync(owner);
        var featured = plans.Single(plan => plan.Tier == PlanTier.Featured);
        Assert.True(featured.PriceNpr > 0, "The featured plan has to cost something for this test to mean anything.");

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        // The form the browser posts carries the price, the reference and the merchant, all
        // signed with the configured key — and the key itself never appears in it.
        var fields = checkout.Fields!;
        Assert.Equal(PaymentsApiFactory.ProductCode, fields["product_code"]);
        Assert.Equal(featured.PriceNpr.ToString(), fields["total_amount"]);
        Assert.Equal(checkout.Reference, fields["transaction_uuid"]);
        Assert.Equal(EsewaSignature.SignedFieldNames, fields["signed_field_names"]);
        Assert.DoesNotContain(PaymentsApiFactory.SecretKey, string.Join(";", fields.Values));

        var expectedSignature = EsewaSignature.Compute(
            PaymentsApiFactory.SecretKey,
            new[]
            {
                new KeyValuePair<string, string>("total_amount", fields["total_amount"]),
                new KeyValuePair<string, string>("transaction_uuid", fields["transaction_uuid"]),
                new KeyValuePair<string, string>("product_code", fields["product_code"]),
            });
        Assert.Equal(expectedSignature, fields["signature"]);

        // The listing is now waiting on money rather than on a person.
        var requested = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.NotNull(requested.PendingRequestId);
        Assert.Null(requested.Tier);

        // Asking again resumes the same checkout instead of opening a second question.
        var resumed = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });
        Assert.Equal(checkout.Reference, resumed.Reference);
        Assert.Equal(1, await PaymentsApi.PaymentCountForRequestAsync(factory, requested.PendingRequestId!.Value));

        // Nobody has answered yet, so nothing is granted.
        factory.Gateway.Answer = ProviderNoticeKind.Pending;
        var inFlight = await PaymentsApi.VerifyAsync(owner, new { reference = checkout.Reference });
        Assert.Equal(PaymentStatus.Pending, inFlight.Status);
        Assert.Null(inFlight.SettledAt);

        // The provider confirms the money.
        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var settled = await PaymentsApi.VerifyAsync(owner, new { reference = checkout.Reference });

        Assert.Equal(PaymentStatus.Succeeded, settled.Status);
        Assert.Equal(PaymentKind.Subscription, settled.Kind);
        Assert.Equal(featured.PriceNpr, settled.AmountNpr);
        Assert.NotNull(settled.SettledAt);
        Assert.Null(settled.FailureCode);
        Assert.Equal("TEST-REF", settled.ProviderReference);

        var state = await SubscriptionApi.GetStateAsync(owner, slug);
        Assert.Equal(PlanTier.Featured, state.Tier);
        Assert.True(state.IsActive);
        Assert.Null(state.PendingRequestId);
        Assert.Equal(1, await SubscriptionApi.ActiveSubscriptionCountAsync(factory, businessId));

        // The queue row settled itself, and says which payment closed it.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var queue = await SubscriptionApi.ReadRequestsAsync(admin, "?pageSize=100");
        var row = queue.Items.Single(item => item.Id == requested.PendingRequestId);
        Assert.Equal(SubscriptionRequestStatus.Approved, row.Status);
        Assert.Contains(checkout.Reference, row.ReviewNote, StringComparison.Ordinal);

        var audit = await Moderations.ReadAuditAsync(admin, "?action=payment.succeeded&pageSize=100");
        Assert.Single(
            audit.Data!.Items,
            candidate => candidate.TargetType == "payment_transactions"
                && candidate.TargetId != null
                && candidate.After!.Contains(checkout.Reference, StringComparison.Ordinal));

        // The buyer's own history shows it, and only their own payments are in it.
        var history = await PaymentsApi.ReadHistoryAsync(owner);
        Assert.Contains(history, payment => payment.Reference == checkout.Reference
            && payment.Status == PaymentStatus.Succeeded
            && payment.AmountNpr == featured.PriceNpr);
    }

    [Fact]
    public async Task A_reply_about_another_amount_is_recorded_and_believed_about_nothing()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        factory.Gateway.ReportedAmount = 1;

        var answered = await PaymentsApi.VerifyAsync(owner, new { reference = checkout.Reference });

        Assert.Equal(PaymentStatus.Pending, answered.Status);
        Assert.Equal("amount_mismatch", answered.FailureCode);
        Assert.Null(answered.SettledAt);

        // Reading it back does not settle it either: nothing was answered about this payment.
        var reread = await PaymentsApi.ReadPaymentAsync(owner, checkout.Reference);
        Assert.Equal(PaymentStatus.Pending, reread.Status);
        Assert.Equal("amount_mismatch", reread.FailureCode);
    }

    [Fact]
    public async Task An_unreachable_provider_leaves_the_payment_exactly_as_it_was()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        factory.Gateway.Unreachable = true;

        var unanswered = await PaymentsApi.VerifyAsync(owner, new { reference = checkout.Reference });
        Assert.Equal(PaymentStatus.Pending, unanswered.Status);
        Assert.Null(unanswered.FailureCode);

        var checksBefore = factory.Gateway.StatusChecks;

        // Once the provider is reachable again, the same question settles it.
        factory.Gateway.Unreachable = false;
        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var settled = await PaymentsApi.VerifyAsync(owner, new { reference = checkout.Reference });

        Assert.Equal(PaymentStatus.Succeeded, settled.Status);
        Assert.True(factory.Gateway.StatusChecks > checksBefore);
    }

    [Fact]
    public async Task A_return_payload_that_is_not_genuinely_signed_is_refused_before_anything_else()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;

        var (data, signature) = PaymentsApi.SignedReturn(factory.Settings, checkout.Reference);

        // The same payload with somebody else's signature on it settles nothing, whatever
        // it claims happened, and the payment is untouched while it is being refused.
        var forged = await PaymentsApi.VerifyForAsync(
            owner,
            new { reference = checkout.Reference, data, signature = $"{signature}x" });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("payment_signature_invalid", forged.Error!.Code);

        var wrongKey = await PaymentsApi.VerifyForAsync(
            owner,
            new { reference = checkout.Reference, data, signature = Convert.ToBase64String(new byte[32]) });
        Assert.Equal(HttpStatusCode.BadRequest, wrongKey.StatusCode);
        Assert.Equal("payment_signature_invalid", wrongKey.Error!.Code);

        var refused = await PaymentsApi.ReadPaymentAsync(owner, checkout.Reference);
        Assert.Equal(PaymentStatus.Pending, refused.Status);
        Assert.Null(refused.SettledAt);

        // The payload that is signed correctly settles it — the signature was checked
        // first and still not believed: the settlement underneath asked the provider.
        var genuine = await PaymentsApi.VerifyForAsync(
            owner,
            new { reference = checkout.Reference, data, signature });
        Assert.Equal(HttpStatusCode.OK, genuine.StatusCode);

        var settled = await PaymentsApi.ReadPaymentAsync(owner, checkout.Reference);
        Assert.Equal(PaymentStatus.Succeeded, settled.Status);
    }

    [Fact]
    public async Task Somebody_elses_payment_is_not_reachable_by_reference()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var buyer = factory.CreateSignedInClientAsync(email);
        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "Subscription", slug, tier = "Featured" });

        using var stranger = factory.CreateSignedInClientAsync(TestData.UniqueEmail("stranger"));
        using var read = await new AuthApiClient(stranger)
            .GetRawAsync($"/api/payments/{checkout.Reference}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var verify = await PaymentsApi.VerifyForAsync(stranger, new { reference = checkout.Reference });
        Assert.Equal(HttpStatusCode.NotFound, verify.StatusCode);
        Assert.Equal("payment_not_found", verify.Error!.Code);
    }
}

/// <summary>
/// Buying a place at an event: the seat goes to whoever is paying for it and comes back the
/// moment the money does not arrive, is refused, or is later refunded.
/// </summary>
public sealed class PaidTicketTests(PaymentsApiFactory factory) : IClassFixture<PaymentsApiFactory>
{
    [Fact]
    public async Task A_priced_event_is_bought_and_a_free_one_is_still_booked()
    {
        var paid = await BookingApi.CreateEventAsync(factory, capacity: 4, ticketPrice: 500);
        var free = await BookingApi.CreateEventAsync(factory, capacity: 4, ticketPrice: 0);

        using var buyer = factory.CreateSignedInClientAsync(TestData.UniqueEmail("ticket"));

        var refused = await new AuthApiClient(buyer).PostForSnapshotAsync(
            $"/api/events/{paid.Slug}/booking",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("payment_required", refused.Error!.Code);

        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "EventTicket", slug = paid.Slug, quantity = 2 });
        Assert.Equal("1000", checkout.Fields!["total_amount"]);

        // The place is held before the provider is asked, so a sold-out event cannot be
        // bought out from under somebody waiting for their payment to be confirmed.
        var detail = await BookingApi.ReadDetailAsync(buyer, paid.Slug);
        Assert.Equal(2, detail.RegistrationCount);
        Assert.Equal(2, detail.RemainingSpots);

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var settled = await PaymentsApi.VerifyAsync(buyer, new { reference = checkout.Reference });
        Assert.Equal(PaymentStatus.Succeeded, settled.Status);
        Assert.Equal(PaymentKind.EventTicket, settled.Kind);

        detail = await BookingApi.ReadDetailAsync(buyer, paid.Slug);
        Assert.Equal(2, detail.RegistrationCount);
        Assert.Equal(2, detail.RemainingSpots);

        var attendance = await ReadAttendanceAsync(factory, paid.Id);
        Assert.NotEmpty(attendance.Bookings);
        Assert.All(attendance.Bookings, row => Assert.Equal(EventBookingStatus.Confirmed, row.Status));
        Assert.Equal(2, attendance.Bookings.Sum(row => row.Quantity));

        // The same place cannot be bought twice.
        var again = await PaymentsApi.CheckoutForAsync(
            buyer,
            new { kind = "EventTicket", slug = paid.Slug, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_booked", again.Error!.Code);

        // And a free event never reaches checkout at all.
        var freeCheckout = await PaymentsApi.CheckoutForAsync(
            buyer,
            new { kind = "EventTicket", slug = free.Slug, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, freeCheckout.StatusCode);
        Assert.Equal("payment_not_required", freeCheckout.Error!.Code);

        var booked = await BookingApi.BookAsync(buyer, free.Slug, 1);
        Assert.Equal(EventBookingStatus.Confirmed, booked.Status);
    }

    [Fact]
    public async Task A_payment_that_never_arrives_gives_the_place_back()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 1, ticketPrice: 500);

        using var buyer = factory.CreateSignedInClientAsync(TestData.UniqueEmail("failed"));
        using var next = factory.CreateSignedInClientAsync(TestData.UniqueEmail("next"));

        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "EventTicket", slug = calendarEvent.Slug, quantity = 1 });

        var held = await BookingApi.ReadDetailAsync(buyer, calendarEvent.Slug);
        Assert.Equal(0, held.RemainingSpots);

        factory.Gateway.Answer = ProviderNoticeKind.Failed;
        var refused = await PaymentsApi.VerifyAsync(buyer, new { reference = checkout.Reference });

        Assert.Equal(PaymentStatus.Failed, refused.Status);
        Assert.Equal("provider_failed", refused.FailureCode);

        var released = await BookingApi.ReadDetailAsync(buyer, calendarEvent.Slug);
        Assert.Equal(0, released.RegistrationCount);
        Assert.Equal(1, released.RemainingSpots);

        var attendance = await ReadAttendanceAsync(factory, calendarEvent.Id);
        Assert.NotEmpty(attendance.Bookings);
        Assert.All(attendance.Bookings, row => Assert.Equal(EventBookingStatus.Cancelled, row.Status));

        // The seat the failed payment was holding is available to somebody else.
        var retried = await PaymentsApi.CheckoutAsync(
            next,
            new { kind = "EventTicket", slug = calendarEvent.Slug, quantity = 1 });
        Assert.NotEqual(checkout.Reference, retried.Reference);
    }

    [Fact]
    public async Task A_refund_discovered_by_the_status_check_takes_the_place_back()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 1, ticketPrice: 750);

        using var buyer = factory.CreateSignedInClientAsync(TestData.UniqueEmail("refunded"));

        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "EventTicket", slug = calendarEvent.Slug, quantity = 1 });

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var settled = await PaymentsApi.VerifyAsync(buyer, new { reference = checkout.Reference });
        Assert.Equal(PaymentStatus.Succeeded, settled.Status);

        var afterPurchase = await BookingApi.ReadDetailAsync(buyer, calendarEvent.Slug);
        Assert.Equal(1, afterPurchase.RegistrationCount);

        // Somebody returns the money in the provider's portal — eSewa has no refund API —
        // and the next status check is what notices it.
        factory.Gateway.Answer = ProviderNoticeKind.Refunded;

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        var checkedStatus = await PaymentsApi.CheckStatusForAsync(admin, checkout.Reference);
        Assert.Equal(HttpStatusCode.OK, checkedStatus.StatusCode);

        var payment = await PaymentsApi.ReadPaymentAsync(buyer, checkout.Reference);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);

        var attendance = await ReadAttendanceAsync(factory, calendarEvent.Id);
        Assert.NotEmpty(attendance.Bookings);
        Assert.All(attendance.Bookings, row => Assert.Equal(EventBookingStatus.Cancelled, row.Status));

        var afterRefund = await BookingApi.ReadDetailAsync(buyer, calendarEvent.Slug);
        Assert.Equal(0, afterRefund.RegistrationCount);
        Assert.Equal(1, afterRefund.RemainingSpots);

        var audit = await Moderations.ReadAuditAsync(admin, "?action=payment.refunded&pageSize=100");
        Assert.Contains(
            audit.Data!.Items,
            candidate => candidate.TargetType == "payment_transactions"
                && candidate.After!.Contains(checkout.Reference, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelling_a_paid_place_marks_the_money_as_owed_back()
    {
        var calendarEvent = await BookingApi.CreateEventAsync(factory, capacity: 2, ticketPrice: 400);

        using var buyer = factory.CreateSignedInClientAsync(TestData.UniqueEmail("cancelled"));

        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "EventTicket", slug = calendarEvent.Slug, quantity = 1 });

        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        await PaymentsApi.VerifyAsync(buyer, new { reference = checkout.Reference });

        var cancelled = await BookingApi.CancelAsync(buyer, calendarEvent.Slug);
        Assert.Equal(EventBookingStatus.Cancelled, cancelled.Status);

        var payment = await PaymentsApi.ReadPaymentAsync(buyer, checkout.Reference);
        Assert.Equal(PaymentStatus.RefundDue, payment.Status);
        Assert.Equal("booking_cancelled", payment.FailureCode);

        var detail = await BookingApi.ReadDetailAsync(buyer, calendarEvent.Slug);
        Assert.Equal(0, detail.RegistrationCount);

        // The refund itself is a portal act, so the row says it is due rather than that it
        // happened — until a status check says otherwise.
        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        await PaymentsApi.CheckStatusForAsync(admin, checkout.Reference);

        var stillOwed = await PaymentsApi.ReadPaymentAsync(buyer, checkout.Reference);
        Assert.Equal(PaymentStatus.RefundDue, stillOwed.Status);
    }

    private static async Task<EventAttendanceResponse> ReadAttendanceAsync(
        PaymentsApiFactory factory,
        Guid eventId)
    {
        using var staff = await factory.CreateStaffClientAsync(Roles.Admin);

        return await new AuthApiClient(staff)
            .GetAsync<EventAttendanceResponse>($"/api/admin/events/{eventId}/bookings");
    }
}

/// <summary>The money from the back office: the list, the filter, and asking by hand.</summary>
public sealed class AdminPaymentTests(PaymentsApiFactory factory) : IClassFixture<PaymentsApiFactory>
{
    [Fact]
    public async Task The_payment_list_is_admin_only_filters_and_can_ask_the_provider_by_hand()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var buyer = factory.CreateSignedInClientAsync(email);

        var checkout = await PaymentsApi.CheckoutAsync(
            buyer,
            new { kind = "Subscription", slug, tier = "Featured" });

        using var member = factory.CreateSignedInClientAsync(TestData.UniqueEmail("not-staff"));
        using var forbidden = await member.GetAsync(PaymentsApi.AdminPath);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = await factory.CreateStaffClientAsync(Roles.Admin);
        using var nonsense = await admin.GetAsync($"{PaymentsApi.AdminPath}?status=Bananas");
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
        Assert.Contains("status_invalid", await nonsense.Content.ReadAsStringAsync());

        var pending = await PaymentsApi.ReadAdminListAsync(admin, "?status=Pending&pageSize=100");
        var row = Assert.Single(pending.Items, item => item.Reference == checkout.Reference);
        Assert.Equal(PaymentKind.Subscription, row.Kind);
        Assert.Equal(PaymentStatus.Pending, row.Status);
        Assert.False(string.IsNullOrWhiteSpace(row.BuyerEmail));

        var succeeded = await PaymentsApi.ReadAdminListAsync(admin, "?status=Succeeded&pageSize=100");
        Assert.DoesNotContain(succeeded.Items, item => item.Reference == checkout.Reference);

        // Asking by hand is how a settlement that nobody came back for is found.
        factory.Gateway.Answer = ProviderNoticeKind.Succeeded;
        var checkedStatus = await PaymentsApi.CheckStatusForAsync(admin, checkout.Reference);
        Assert.Equal(HttpStatusCode.OK, checkedStatus.StatusCode);

        var settled = await PaymentsApi.ReadPaymentAsync(buyer, checkout.Reference);
        Assert.Equal(PaymentStatus.Succeeded, settled.Status);

        var unknown = await PaymentsApi.CheckStatusForAsync(admin, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("payment_not_found", unknown.Error!.Code);
    }
}

/// <summary>Starting checkouts is rationed, exactly as taking a place is.</summary>
public sealed class PaymentRateLimitTests(PaymentLimitedApiFactory factory)
    : IClassFixture<PaymentLimitedApiFactory>
{
    [Fact]
    public async Task Starting_a_checkout_is_rationed_per_hour()
    {
        var (email, _, slug) = await OwnershipApi.MakeOwnerAsync(factory);
        using var owner = factory.CreateSignedInClientAsync(email);

        for (var attempt = 0; attempt < PaymentLimitedApiFactory.PermitLimit; attempt++)
        {
            var snapshot = await PaymentsApi.CheckoutForAsync(
                owner,
                new { kind = "Subscription", slug, tier = "Featured" });

            Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        }

        var refused = await PaymentsApi.CheckoutForAsync(
            owner,
            new { kind = "Subscription", slug, tier = "Featured" });

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }
}

/// <summary>
/// The protocol's cryptography on its own: an order-sensitive HMAC over the fields the
/// payload names, checked without a host, a database or a network.
/// </summary>
public sealed class EsewaSignatureTests
{
    private const string Secret = "the-key-the-test-signed-with";

    private static readonly IReadOnlyList<KeyValuePair<string, string>> Fields =
    [
        new("total_amount", "3000"),
        new("transaction_uuid", "abc123"),
        new("product_code", "EPAY_TEST"),
    ];

    [Fact]
    public void The_same_fields_sign_to_the_same_bytes_every_time()
    {
        var first = EsewaSignature.Compute(Secret, Fields);
        var second = EsewaSignature.Compute(Secret, Fields);

        Assert.Equal(first, second);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void Signing_the_same_values_in_another_order_produces_a_different_signature()
    {
        var inOrder = EsewaSignature.Compute(Secret, Fields);
        var reversed = EsewaSignature.Compute(Secret, Fields.Reverse().ToList());

        Assert.NotEqual(inOrder, reversed);
    }

    [Fact]
    public void A_different_key_a_different_value_and_a_different_field_set_all_mismatch()
    {
        var expected = EsewaSignature.Compute(Secret, Fields);

        var otherKey = EsewaSignature.Compute("not-the-key", Fields);
        var otherAmount = EsewaSignature.Compute(
            Secret,
            Fields.Select(pair => pair.Key == "total_amount"
                ? new KeyValuePair<string, string>(pair.Key, "3001")
                : pair).ToList());
        var fewerFields = EsewaSignature.Compute(Secret, Fields.Take(2).ToList());

        Assert.NotEqual(expected, otherKey);
        Assert.NotEqual(expected, otherAmount);
        Assert.NotEqual(expected, fewerFields);
    }

    [Fact]
    public void A_signature_only_covers_the_fields_the_payload_says_it_covers()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "total_amount": "3000",
              "transaction_uuid": "abc123",
              "product_code": "EPAY_TEST",
              "signed_field_names": "total_amount,transaction_uuid"
            }
            """);

        var payload = document.RootElement;
        var signature = EsewaSignature.Compute(Secret, Fields.Take(2).ToList());

        Assert.True(EsewaSignature.Verify(Secret, payload, "total_amount,transaction_uuid", signature));
        Assert.False(EsewaSignature.Verify(Secret, payload, "total_amount,transaction_uuid,product_code", signature));
        Assert.False(EsewaSignature.Verify(Secret, payload, "total_amount,transaction_uuid", $"{signature}x"));
        Assert.False(EsewaSignature.Verify(Secret, payload, "total_amount,transaction_uuid", null));
        Assert.False(EsewaSignature.Verify(Secret, payload, null, signature));
    }

    [Fact]
    public void A_payload_naming_a_field_it_does_not_carry_cannot_be_verified_against_it()
    {
        using var document = JsonDocument.Parse(
            """
            { "total_amount": "3000", "signed_field_names": "total_amount,transaction_uuid" }
            """);

        var payload = document.RootElement;
        var overWhatWasThere = EsewaSignature.Compute(
            Secret,
            [new KeyValuePair<string, string>("total_amount", "3000")]);

        Assert.False(EsewaSignature.Verify(
            Secret,
            payload,
            "total_amount,transaction_uuid",
            overWhatWasThere));
    }
}
