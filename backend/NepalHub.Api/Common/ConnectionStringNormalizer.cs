using MySqlConnector;

namespace NepalHub.Api.Common;

/// <summary>
/// Accepts both spellings of a MySQL connection string. Hosts like Aiven hand out a
/// <c>mysql://user:pass@host:port/db?ssl-mode=REQUIRED</c> URL, while MySqlConnector parses
/// only the .NET <c>Server=...;Port=...;...</c> form — pasting the URL straight through
/// failed to parse at index 0 and turned every database endpoint into a 500. A value that is
/// already in key=value form (or absent) is returned untouched, so the normal configuration
/// path never sees a difference.
/// </summary>
public static class ConnectionStringNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("mariadb://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Host.Length == 0)
        {
            return value;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = uri.Host,
            Port = uri.Port > 0 ? (uint)uri.Port : 3306,
            Database = uri.AbsolutePath.Trim('/'),
            UserID = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = MySqlSslMode.Required,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length != 2)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(keyValue[0]).Replace('_', '-').ToLowerInvariant();
            var queryValue = Uri.UnescapeDataString(keyValue[1]);
            if (key is "ssl-mode" or "sslmode")
            {
                builder.SslMode = Enum.Parse<MySqlSslMode>(queryValue.Replace("_", string.Empty), ignoreCase: true);
            }
            else if (key is "connection-timeout" or "connect-timeout" or "timeout")
            {
                builder.ConnectionTimeout = uint.TryParse(queryValue, out var seconds) ? seconds : builder.ConnectionTimeout;
            }
        }

        return builder.ConnectionString;
    }
}
