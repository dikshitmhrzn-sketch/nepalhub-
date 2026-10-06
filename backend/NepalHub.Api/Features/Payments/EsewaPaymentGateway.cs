using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// eSewa's ePay v2: a signed form the browser posts to leave this site, a signed redirect
/// back, and a server-to-server status check that is the only thing allowed to settle.
///
/// <para>
/// eSewa ePay v2 pushes nothing to the server — there is no webhook to receive — so the
/// two halves of this adapter are "where to send the buyer" and "ask eSewa directly what
/// happened". The redirect carries a signed payload and is worth verifying precisely so a
/// forged one can be rejected early, but even a genuine redirect only ever triggers a
/// status check; the browser is under the buyer's control and a status check is not.
/// </para>
///
/// <para>
/// Refunds have no API here either: staff issue them in eSewa's merchant portal and this
/// adapter's job is to notice, on the next status check, that the money went back.
/// </para>
/// </summary>
public sealed class EsewaPaymentGateway(PaymentSettings settings, IHttpClientFactory httpClientFactory)
    : IPaymentGateway
{
    public const string HttpClientName = "esewa";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<CheckoutHandshake> BeginCheckoutAsync(
        CheckoutIntent intent,
        CancellationToken cancellationToken = default)
    {
        // Whole rupees, invariant culture: "3000" is what the signature message must
        // contain, and a locale that printed "3.000" would sign one string while the form
        // posted another.
        var totalAmount = intent.AmountNpr.ToString(CultureInfo.InvariantCulture);

        var successUrl = BuildReturnUrl(intent.Reference, failed: false);
        var failureUrl = BuildReturnUrl(intent.Reference, failed: true);

        // Signing order is the protocol's, not ours: total_amount, transaction_uuid,
        // product_code — computed here, on the server, so the key behind it never leaves.
        var signedFields = new List<KeyValuePair<string, string>>
        {
            new("total_amount", totalAmount),
            new("transaction_uuid", intent.Reference),
            new("product_code", settings.ProductCode),
        };

        var signature = EsewaSignature.Compute(settings.SecretKey, signedFields);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = totalAmount,
            ["tax_amount"] = "0",
            ["product_service_charge"] = "0",
            ["product_delivery_charge"] = "0",
            ["total_amount"] = totalAmount,
            ["transaction_uuid"] = intent.Reference,
            ["product_code"] = settings.ProductCode,
            ["success_url"] = successUrl,
            ["failure_url"] = failureUrl,
            ["signed_field_names"] = EsewaSignature.SignedFieldNames,
            ["signature"] = signature,
        };

        var checkoutUrl = new Uri(settings.CheckoutBaseUrl, "/api/epay/main/v2/form");

        return Task.FromResult(new CheckoutHandshake(intent.Reference, checkoutUrl, intent.Reference, fields));
    }

    public Task<ProviderNotice?> ReadNoticeAsync(
        string body,
        string signature,
        CancellationToken cancellationToken = default)
    {
        // The payload arrives Base64-wrapped. Decoding it is not the same as trusting it:
        // nothing below is read for meaning until the signature over its own declared
        // fields has passed.
        if (!TryDecode(body, out var payload))
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        if (!payload.TryGetProperty("signed_field_names", out var namesElement))
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        var signedFieldNames = namesElement.GetString();

        if (!EsewaSignature.Verify(settings.SecretKey, payload, signedFieldNames, signature))
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        if (!payload.TryGetProperty("transaction_uuid", out var referenceElement))
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        var reference = referenceElement.GetString();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        var status = payload.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;

        var providerReference = payload.TryGetProperty("ref_id", out var refIdElement)
            ? refIdElement.GetString()
            : null;

        var kind = MapStatus(status);
        if (kind is null)
        {
            return Task.FromResult<ProviderNotice?>(null);
        }

        return Task.FromResult<ProviderNotice?>(new ProviderNotice(reference, kind.Value, providerReference));
    }

    public async Task<StatusReport?> CheckStatusAsync(
        string reference,
        int expectedTotalNpr,
        CancellationToken cancellationToken = default)
    {
        // The amount we quoted is part of the question, because eSewa answers with the
        // amount it recorded: a reply about a different total is not an answer about this
        // payment, and the caller compares the two rather than assuming they match.
        var url = new Uri(
            settings.StatusBaseUrl,
            "/api/epay/transaction/status/?"
            + new StringBuilder()
                .Append("product_code=").Append(Uri.EscapeDataString(settings.ProductCode))
                .Append("&total_amount=").Append(expectedTotalNpr.ToString(CultureInfo.InvariantCulture))
                .Append("&transaction_uuid=").Append(Uri.EscapeDataString(reference))
                .ToString());

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var payload = document.RootElement;
            if (payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("status", out var statusElement))
            {
                // An error shape rather than a status answer: no verdict to act on.
                return null;
            }

            var kind = MapStatus(statusElement.GetString());
            if (kind is null)
            {
                return null;
            }

            var echoedReference = payload.TryGetProperty("transaction_uuid", out var referenceOut)
                ? referenceOut.GetString()
                : null;

            var echoedTotal = -1;
            if (payload.TryGetProperty("total_amount", out var totalOut)
                && totalOut.ValueKind == JsonValueKind.Number)
            {
                echoedTotal = totalOut.TryGetInt32(out var whole)
                    ? whole
                    : (int)decimal.Round(totalOut.GetDecimal());
            }

            var providerReference = payload.TryGetProperty("ref_id", out var refId)
                ? refId.GetString()
                : null;

            return new StatusReport(
                echoedReference ?? string.Empty,
                echoedTotal,
                kind.Value,
                providerReference);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A provider that cannot be reached has said nothing. No answer must never be
            // mistaken for a failed payment — the caller leaves the row pending.
            return null;
        }
    }

    private string BuildReturnUrl(string reference, bool failed) =>
        $"{settings.ReturnBaseUrl.TrimEnd('/')}/payments/{(failed ? "failed" : "return")}/{reference}";

    private static bool TryDecode(string body, out JsonElement payload)
    {
        payload = default;

        try
        {
            var base64 = body.Trim();
            var bytes = Convert.FromBase64String(base64);
            var json = Encoding.UTF8.GetString(bytes);

            using var document = JsonDocument.Parse(json);
            payload = document.RootElement.Clone();
            return payload.ValueKind == JsonValueKind.Object;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// eSewa's status vocabulary to the one this system settles on. Anything it does not
    /// recognise maps to null, which callers read as "no verdict" and leave alone.
    /// </summary>
    private static ProviderNoticeKind? MapStatus(string? status) =>
        status?.Trim().ToUpperInvariant() switch
        {
            "COMPLETE" or "SUCCESS" => ProviderNoticeKind.Succeeded,
            "FULL_REFUND" or "PARTIAL_REFUND" => ProviderNoticeKind.Refunded,
            "PENDING" or "AMBIGUOUS" or "BOOKED" => ProviderNoticeKind.Pending,
            "FAILED" or "CANCELED" or "CANCELLED" or "NOT_FOUND" or "REVERTED" => ProviderNoticeKind.Failed,
            _ => null,
        };
}
