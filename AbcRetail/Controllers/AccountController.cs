using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AbcRetail.Controllers;

public class AccountController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly IFunctionGateway _functions;
    private readonly IEmailSender _email;
    private readonly IVerifyTicket _verify;
    private readonly ICartOwner _cartOwner;
    private readonly ICartService _cart;
    private readonly PasswordHasher<CustomerEntity> _hasher = new();

    public AccountController(
        ITableStorageService tables,
        IAzureStorageGate gate,
        IFileStorageService files,
        IFunctionGateway functions,
        IEmailSender email,
        IVerifyTicket verify,
        ICartOwner cartOwner,
        ICartService cart)
    {
        _tables = tables;
        _gate = gate;
        _files = files;
        _functions = functions;
        _email = email;
        _verify = verify;
        _cartOwner = cartOwner;
        _cart = cart;
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _tables.GetUserByEmailAsync(model.Email, ct);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var (raw, hash) = EmailConfirmation.CreateToken();
        var expires = DateTimeOffset.UtcNow.Add(EmailConfirmation.Lifetime);
        var entity = new CustomerEntity
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Phone = model.Phone,
            City = model.City,
            AddressLine = model.AddressLine,
            PostalCode = model.PostalCode,
            Role = CustomerEntity.RoleCustomer,
            EmailConfirmed = false,
            EmailConfirmToken = hash,
            EmailConfirmExpiresUtc = expires
        };
        entity.PartitionKey = "USER";
        entity.RowKey = CustomerEntity.NormalizeEmail(entity.Email);
        entity.Email = entity.RowKey;
        entity.PasswordHash = _hasher.HashPassword(entity, model.Password);

        await _functions.UpsertAsync("Customers", entity.PartitionKey, entity.RowKey, CommerceFormatting.CustomerProperties(entity), ct);
        await IssueVerificationAsync(entity, raw, expires, ct);
        await _files.WriteActivityAsync("Register", entity.Email, "awaiting email confirmation", ct);

        return RedirectToAction(nameof(CheckEmail), new { email = entity.Email, returnUrl = model.ReturnUrl });
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome(User.IsInRole(CustomerEntity.RoleAdmin));
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _tables.GetUserByEmailAsync(model.Email, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            await _files.WriteActivityAsync("LoginFail", model.Email, "unknown user or missing password hash", ct);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            await _files.WriteActivityAsync("LoginFail", model.Email, "bad password", ct);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (!user.IsVerified)
        {
            await RotateAndSendAsync(user, ct);
            TempData["Status"] = "Confirm your email to continue.";
            return RedirectToAction(nameof(CheckEmail), new { email = user.Email, returnUrl = model.ReturnUrl });
        }

        await AfterVerifiedSignInAsync(user, ct);
        return RedirectAfterAuth(user, model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult CheckEmail(string? email, string? returnUrl = null)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var pending = _verify.Read();
        var address = CustomerEntity.NormalizeEmail(email ?? pending?.Email ?? string.Empty);
        if (string.IsNullOrWhiteSpace(address))
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        string? confirmUrl = null;
        if (pending is not null && string.Equals(pending.Email, address, StringComparison.OrdinalIgnoreCase))
        {
            confirmUrl = Url.Action(nameof(ConfirmEmail), "Account", new { email = address, token = pending.Token, returnUrl }, Request.Scheme);
        }

        return View(new CheckEmailViewModel
        {
            Email = address,
            ConfirmUrl = confirmUrl,
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(string email, string? returnUrl, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var user = await _tables.GetUserByEmailAsync(email, ct);
        if (user is not null && !user.IsVerified)
        {
            await RotateAndSendAsync(user, ct);
            TempData["Status"] = "We sent a new confirmation message.";
        }

        return RedirectToAction(nameof(CheckEmail), new { email, returnUrl });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string email, string token, string? returnUrl, CancellationToken ct)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        var user = await _tables.GetUserByEmailAsync(email, ct);
        if (user is null
            || string.IsNullOrWhiteSpace(token)
            || user.EmailConfirmExpiresUtc is null
            || user.EmailConfirmExpiresUtc < DateTimeOffset.UtcNow
            || !EmailConfirmation.Matches(token, user.EmailConfirmToken))
        {
            TempData["Error"] = "That confirmation link is invalid or has expired.";
            return RedirectToAction(nameof(CheckEmail), new { email, returnUrl });
        }

        user.EmailConfirmed = true;
        user.EmailConfirmToken = null;
        user.EmailConfirmExpiresUtc = null;
        await _functions.UpsertAsync("Customers", user.PartitionKey, user.RowKey, CommerceFormatting.CustomerProperties(user), ct);
        _verify.Clear();
        await AfterVerifiedSignInAsync(user, ct);
        await _files.WriteActivityAsync("EmailConfirm", user.Email, "confirmed", ct);
        TempData["Status"] = "Email confirmed. Welcome to ABC Retail.";
        return RedirectAfterAuth(user, returnUrl);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        TempData["Error"] = "You do not have access to that page.";
        return RedirectToRoleHome(User.IsInRole(CustomerEntity.RoleAdmin));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var email = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await _files.WriteActivityAsync("Logout", email, "signed out", ct);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _tables.GetUserByEmailAsync(email, ct);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ProfileViewModel
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            City = user.City,
            AddressLine = user.AddressLine,
            PostalCode = user.PostalCode,
            Role = user.Role
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken ct)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _tables.GetUserByEmailAsync(email, ct);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Phone = model.Phone;
        user.City = model.City;
        user.AddressLine = model.AddressLine;
        user.PostalCode = model.PostalCode;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            if (model.NewPassword.Length < 8)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be at least 8 characters.");
                model.Email = user.Email;
                model.Role = user.Role;
                return View(model);
            }

            user.PasswordHash = _hasher.HashPassword(user, model.NewPassword);
        }

        await _functions.UpsertAsync("Customers", user.PartitionKey, user.RowKey, CommerceFormatting.CustomerProperties(user), ct);
        await _files.WriteActivityAsync("ProfileUpdate", email, $"name={user.FirstName} {user.LastName} city={user.City}", ct);

        TempData["Status"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task RotateAndSendAsync(CustomerEntity user, CancellationToken ct)
    {
        var (raw, hash) = EmailConfirmation.CreateToken();
        var expires = DateTimeOffset.UtcNow.Add(EmailConfirmation.Lifetime);
        user.EmailConfirmed = false;
        user.EmailConfirmToken = hash;
        user.EmailConfirmExpiresUtc = expires;
        await _functions.UpsertAsync("Customers", user.PartitionKey, user.RowKey, CommerceFormatting.CustomerProperties(user), ct);
        await IssueVerificationAsync(user, raw, expires, ct);
    }

    private async Task IssueVerificationAsync(CustomerEntity user, string rawToken, DateTimeOffset expires, CancellationToken ct)
    {
        _verify.Issue(user.Email, rawToken, expires);
        var confirmUrl = Url.Action(nameof(ConfirmEmail), "Account", new { email = user.Email, token = rawToken }, Request.Scheme)
            ?? $"/Account/ConfirmEmail?email={Uri.EscapeDataString(user.Email)}&token={rawToken}";
        var html = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>
            <p>Confirm your email to finish creating your ABC Retail account.</p>
            <p><a href="{confirmUrl}" style="display:inline-block;padding:12px 20px;background:#0f6b5c;color:#fff;text-decoration:none;border-radius:999px;font-weight:600;">Confirm email</a></p>
            <p>This link expires in 24 hours.</p>
            """;
        await _email.SendAsync(user.Email, "Confirm your ABC Retail email", html, ct);
    }

    private async Task AfterVerifiedSignInAsync(CustomerEntity user, CancellationToken ct)
    {
        var guest = _cartOwner.GuestKey();
        if (string.Equals(user.Role, CustomerEntity.RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            if (guest is not null)
            {
                await _cart.ClearAsync(guest, ct);
            }

            _cartOwner.ClearGuest();
        }
        else if (guest is not null)
        {
            await _cart.MergeAsync(guest, user.Email, ct);
            _cartOwner.ClearGuest();
        }

        await SignInAsync(user);
        await _files.WriteActivityAsync("Login", user.Email, $"role={user.Role}", ct);
    }

    private async Task SignInAsync(CustomerEntity user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private IActionResult RedirectAfterAuth(CustomerEntity user, string? returnUrl)
    {
        var isAdmin = string.Equals(user.Role, CustomerEntity.RoleAdmin, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && !IsLoginLoop(returnUrl)
            && RoleCanOpen(returnUrl, isAdmin))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoleHome(isAdmin);
    }

    private IActionResult RedirectToRoleHome(bool _) =>
        RedirectToAction("Index", "Home");

    private static bool IsLoginLoop(string returnUrl)
    {
        var path = returnUrl.Split('?', '#')[0];
        return path.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/CheckEmail", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Account/ConfirmEmail", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RoleCanOpen(string returnUrl, bool isAdmin)
    {
        var path = returnUrl.Split('?', '#')[0];

        if (path.StartsWith("/Orders/Enqueue", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Orders/Dequeue", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Delete", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/Delete?", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var adminOnly =
            path.StartsWith("/Products/Manage", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Products/Create", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Products/Edit", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Customers", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Logs", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/Orders", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Orders/Index", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Orders/UpdateStatus", StringComparison.OrdinalIgnoreCase);

        if (adminOnly)
        {
            return isAdmin;
        }

        if (path.StartsWith("/Orders/Cart", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Cart", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Checkout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Orders/Mine", StringComparison.OrdinalIgnoreCase))
        {
            return !isAdmin;
        }

        return true;
    }
}
