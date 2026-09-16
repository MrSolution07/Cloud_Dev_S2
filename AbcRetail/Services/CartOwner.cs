using AbcRetail.Models;

namespace AbcRetail.Services;

public interface ICartOwner
{
    string CurrentKey(bool createGuest = false);
    string? GuestKey();
    void ClearGuest();
}

public sealed class CartOwner : ICartOwner
{
    public const string CookieName = "abc.cart";

    private readonly IHttpContextAccessor _http;

    public CartOwner(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string CurrentKey(bool createGuest = false)
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true
            && !user.IsInRole(CustomerEntity.RoleAdmin)
            && !string.IsNullOrWhiteSpace(user.Identity.Name))
        {
            return CustomerEntity.NormalizeEmail(user.Identity.Name);
        }

        var existing = GuestKey();
        if (existing is not null)
        {
            return existing;
        }

        if (!createGuest)
        {
            return string.Empty;
        }

        var id = Guid.NewGuid().ToString("N");
        var context = _http.HttpContext;
        context?.Response.Cookies.Append(CookieName, id, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
        return $"guest.{id}";
    }

    public string? GuestKey()
    {
        var raw = _http.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var id = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        return id.Length == 0 ? null : $"guest.{id.ToLowerInvariant()}";
    }

    public void ClearGuest()
    {
        _http.HttpContext?.Response.Cookies.Delete(CookieName);
    }
}
