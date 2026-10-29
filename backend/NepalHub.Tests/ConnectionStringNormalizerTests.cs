using NepalHub.Api.Common;
using MySqlConnector;

namespace NepalHub.Tests;

/// <summary>
/// The two spellings of a connection string. Aiven hands out a <c>mysql://</c> URL; pasting it
/// straight through failed to parse at index 0 and turned every database endpoint into a 500,
/// so both spellings must land on one parseable value.
/// </summary>
public class ConnectionStringNormalizerTests
{
    [Fact]
    public void A_mysql_url_becomes_a_connection_string_my_sql_connector_accepts()
    {
        var normalized = ConnectionStringNormalizer.Normalize(
            "mysql://avnadmin:secret@mysql-host.aivencloud.com:18326/nepalhub");

        var parsed = new MySqlConnectionStringBuilder(normalized!);
        Assert.Equal("mysql-host.aivencloud.com", parsed.Server);
        Assert.Equal(18326u, parsed.Port);
        Assert.Equal("nepalhub", parsed.Database);
        Assert.Equal("avnadmin", parsed.UserID);
        Assert.Equal("secret", parsed.Password);
        Assert.Equal(MySqlSslMode.Required, parsed.SslMode);
    }

    [Fact]
    public void A_ssl_mode_query_parameter_wins_over_the_default()
    {
        var normalized = ConnectionStringNormalizer.Normalize(
            "mysql://avnadmin:secret@host.example:18326/nepalhub?ssl-mode=VERIFY_CA");

        var parsed = new MySqlConnectionStringBuilder(normalized!);
        Assert.Equal(MySqlSslMode.VerifyCA, parsed.SslMode);
    }

    [Fact]
    public void A_percent_encoded_password_is_decoded_once()
    {
        var normalized = ConnectionStringNormalizer.Normalize(
            "mysql://avnadmin:p%40ss%3Bw0rd@host.example:18326/nepalhub");

        var parsed = new MySqlConnectionStringBuilder(normalized!);
        Assert.Equal("p@ss;w0rd", parsed.Password);
    }

    [Fact]
    public void An_absent_port_falls_back_to_the_mysql_default()
    {
        var normalized = ConnectionStringNormalizer.Normalize(
            "mysql://avnadmin:secret@host.example/nepalhub");

        var parsed = new MySqlConnectionStringBuilder(normalized!);
        Assert.Equal(3306u, parsed.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unconfigured_value_passes_through_untouched(string? value)
    {
        Assert.Equal(value, ConnectionStringNormalizer.Normalize(value));
    }

    [Fact]
    public void A_dotnet_connection_string_is_returned_byte_for_byte()
    {
        const string value = "Server=host.example;Port=18326;Database=nepalhub;UserID=avnadmin;Password=secret;SslMode=Required";

        Assert.Equal(value, ConnectionStringNormalizer.Normalize(value));
    }

    [Theory]
    [InlineData("mysql-3ba31b39-dikshitmhrzn-1526.h.aivencloud.com")]
    [InlineData("mysql://")]
    [InlineData("just a password")]
    public void A_value_that_is_not_a_url_passes_through_untouched(string value)
    {
        Assert.Equal(value, ConnectionStringNormalizer.Normalize(value));
    }
}
