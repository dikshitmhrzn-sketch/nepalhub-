namespace NepalHub.Api.Common;

public sealed record FieldError(string Field, string Code, string Message);

public static class FieldErrors
{
    public const string EmailField = "email";
    public const string PasswordField = "password";
    public const string PhoneNumberField = "phoneNumber";
    public const string GeneralField = "general";
}
