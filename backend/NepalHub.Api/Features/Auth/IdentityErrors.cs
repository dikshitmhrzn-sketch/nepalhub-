using Microsoft.AspNetCore.Identity;
using NepalHub.Api.Common;

namespace NepalHub.Api.Features.Auth;

internal static class IdentityErrors
{
    private static readonly Dictionary<string, (string Field, string Code)> Mappings = new(StringComparer.Ordinal)
    {
        ["DuplicateUserName"] = (FieldErrors.EmailField, "email_taken"),
        ["DuplicateEmail"] = (FieldErrors.EmailField, "email_taken"),
        ["InvalidUserName"] = (FieldErrors.EmailField, "email_invalid"),
        ["InvalidEmail"] = (FieldErrors.EmailField, "email_invalid"),
        ["PasswordTooShort"] = (FieldErrors.PasswordField, "password_too_short"),
        ["PasswordTooLong"] = (FieldErrors.PasswordField, "password_too_long"),
        ["PasswordRequiresUniqueChars"] = (FieldErrors.PasswordField, "password_too_weak"),
        ["PasswordRequiresNonAlphanumeric"] = (FieldErrors.PasswordField, "password_too_weak"),
        ["PasswordRequiresDigit"] = (FieldErrors.PasswordField, "password_too_weak"),
        ["PasswordRequiresLower"] = (FieldErrors.PasswordField, "password_too_weak"),
        ["PasswordRequiresUpper"] = (FieldErrors.PasswordField, "password_too_weak"),
        ["PhoneNumberAlreadyExists"] = (FieldErrors.PhoneNumberField, "phone_taken"),
    };

    internal static IReadOnlyList<FieldError> Translate(IEnumerable<IdentityError> errors)
    {
        var translated = new List<FieldError>();
        var seen = new HashSet<(string Field, string Code)>();

        foreach (var error in errors)
        {
            if (Mappings.TryGetValue(error.Code, out var mapping))
            {
                // Identity reports the same problem more than once, for example a
                // duplicate e-mail as both DuplicateUserName and DuplicateEmail.
                if (seen.Add((mapping.Field, mapping.Code)))
                {
                    translated.Add(new FieldError(mapping.Field, mapping.Code, ToMessage(mapping.Code, error.Description)));
                }

                continue;
            }

            if (seen.Add((FieldErrors.GeneralField, "request_invalid")))
            {
                translated.Add(new FieldError(FieldErrors.GeneralField, "request_invalid", error.Description));
            }
        }

        return translated;
    }

    private static string ToMessage(string code, string fallback) => code switch
    {
        "email_taken" => "An account with this email already exists.",
        "email_invalid" => "Enter a valid email address.",
        "password_too_short" => "Password must be at least 8 characters.",
        "password_too_long" => "Password must be at most 100 characters.",
        "password_too_weak" => "Password must contain upper case, lower case, a digit and a symbol.",
        "phone_taken" => "This phone number is already registered.",
        _ => fallback,
    };
}
