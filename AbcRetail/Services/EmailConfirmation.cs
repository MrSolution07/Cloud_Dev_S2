using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbcRetail.Models;
using Microsoft.AspNetCore.DataProtection;

namespace AbcRetail.Services;

public static class EmailConfirmation
{
    public const string CookieName = "abc.verify";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static (string Raw, string Hash) CreateToken()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return (raw, Hash(raw));
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    public static bool Matches(string raw, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var hashed = Hash(raw);
        if (hashed.Length != storedHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashed),
            Encoding.UTF8.GetBytes(storedHash));
    }
}

public sealed class PendingVerify
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresUtc { get; set; }
}

public interface IVerifyTicket
{
    void Issue(string email, string rawToken, DateTimeOffset expiresUtc);
    PendingVerify? Read();
    void Clear();
}

public sealed class VerifyTicket : IVerifyTicket
{
    private readonly IHttpContextAccessor _http;
    private readonly IDataProtector _protector;

    public VerifyTicket(IHttpContextAccessor http, IDataProtectionProvider protection)
    {
        _http = http;
        _protector = protection.CreateProtector("AbcRetail.EmailVerify");
    }

    public void Issue(string email, string rawToken, DateTimeOffset expiresUtc)
    {
        var context = _http.HttpContext;
        if (context is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new PendingVerify
        {
            Email = CustomerEntity.NormalizeEmail(email),
            Token = rawToken,
            ExpiresUtc = expiresUtc
        });
        context.Response.Cookies.Append(EmailConfirmation.CookieName, _protector.Protect(json), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = expiresUtc
        });
    }

    public PendingVerify? Read()
    {
        var context = _http.HttpContext;
        if (context is null || !context.Request.Cookies.TryGetValue(EmailConfirmation.CookieName, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var json = _protector.Unprotect(value);
            var pending = JsonSerializer.Deserialize<PendingVerify>(json);
            if (pending is null || pending.ExpiresUtc < DateTimeOffset.UtcNow)
            {
                return null;
            }

            return pending;
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        _http.HttpContext?.Response.Cookies.Delete(EmailConfirmation.CookieName);
    }
}
