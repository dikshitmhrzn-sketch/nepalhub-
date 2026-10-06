using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NepalHub.Api.Features.Payments;

/// <summary>
/// The pure cryptography of the eSewa ePay v2 protocol, kept apart from any HTTP or
/// configuration so the signature rules can be tested against known messages without a
/// host, a client or a network.
///
/// <para>
/// Both directions are the same construction: an HMAC-SHA256 over <c>field=value</c>
/// pairs joined by commas in the order the signed field names declare, Base64-encoded.
/// The order is part of the signature — signing <c>total_amount,transaction_uuid,
/// product_code</c> in any other order produces a different byte string and a mismatch,
/// which is why the field list travels with the payload rather than being assumed, and
/// why every helper here carries an ordered list rather than a map.
/// </para>
/// </summary>
public static class EsewaSignature
{
    /// <summary>The fields every outgoing checkout signature covers, in signing order.</summary>
    public const string SignedFieldNames = "total_amount,transaction_uuid,product_code";

    public static string Compute(string secretKey, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var message = string.Join(",", fields.Select(pair => $"{pair.Key}={pair.Value}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Reads a returned payload's signed fields in exactly the order its own
    /// <c>signed_field_names</c> declares — that list is the payload's claim about how it
    /// was signed, and verification recomputes against exactly it. A payload that names a
    /// field it does not carry, or names none at all, yields nothing to verify against.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ReadSignedFields(
        JsonElement payload,
        string? signedFieldNames)
    {
        if (string.IsNullOrWhiteSpace(signedFieldNames))
        {
            return [];
        }

        var fields = new List<KeyValuePair<string, string>>();

        foreach (var name in signedFieldNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0 || !payload.TryGetProperty(trimmed, out var value))
            {
                // Not "skip it": a signature over a declared set is only that set's
                // signature, and a payload missing a field it named has not been signed
                // the way it says it was.
                return [];
            }

            fields.Add(new KeyValuePair<string, string>(trimmed, TextOf(value)));
        }

        return fields;
    }

    /// <summary>
    /// Recomputes a signature over a payload and compares in constant time. A caller must
    /// never read a payload whose signature has not passed: the signature is the only
    /// thing in a browser redirect that proves eSewa — rather than the buyer — wrote it.
    /// </summary>
    public static bool Verify(
        string secretKey,
        JsonElement payload,
        string? signedFieldNames,
        string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var fields = ReadSignedFields(payload, signedFieldNames);
        if (fields.Count == 0)
        {
            return false;
        }

        var expected = Compute(secretKey, fields);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>
    /// A payload value as the signature message saw it: numbers by their literal text so
    /// "100.0" does not become "100", everything else by its string.
    /// </summary>
    private static string TextOf(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString(),
        };
}
