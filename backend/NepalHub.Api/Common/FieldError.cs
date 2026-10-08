namespace NepalHub.Api.Common;

public sealed record FieldError(string Field, string Code, string Message);

public static class FieldErrors
{
    public const string EmailField = "email";
    public const string PasswordField = "password";

    /// <summary>
    /// The password already on the account, as opposed to the one being set. Only
    /// change-password can refuse the current one, and its refusal must not land under
    /// the input where a person is typing their new password.
    /// </summary>
    public const string CurrentPasswordField = "currentPassword";

    public const string PhoneNumberField = "phoneNumber";
    public const string GeneralField = "general";
}
