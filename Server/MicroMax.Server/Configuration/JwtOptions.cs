namespace MicroMax.Server.Configuration;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "MicroMax";
    public string Audience { get; set; } = "MicroMax.Mobile";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
