using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AbcRetail.Controllers;

/// <summary>Customer registration/login/profile backed by the Azure Table Storage Customers table.</summary>
public class AccountController : Controller
{
    private readonly ITableStorageService _tables;
    private readonly IAzureStorageGate _gate;
    private readonly IFileStorageService _files;
    private readonly PasswordHasher<CustomerEntity> _hasher = new();

    public AccountController(ITableStorageService tables, IAzureStorageGate gate, IFileStorageService files)
    {
        _tables = tables;
        _gate = gate;
        _files = files;
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

        var entity = new CustomerEntity
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            Phone = model.Phone,
            City = model.City,
            Role = CustomerEntity.RoleCustomer
        };
        entity.PasswordHash = _hasher.HashPassword(entity, model.Password);

        await _tables.AddCustomerAsync(entity, ct);
        await SignInAsync(entity);
        await _files.WriteActivityAsync("Register", entity.Email, $"role={entity.Role} city={entity.City}", ct);

        TempData["Status"] = "Welcome to ABC Retail — your profile is saved !";
        return RedirectAfterAuth(entity, model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!_gate.IsConfigured)
        {
            return View("~/Views/Shared/StorageNotConfigured.cshtml", _gate.MissingReason);
        }

        // Already signed in --> send straight to role home (avoids weird re-login).
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

        await SignInAsync(user);
        await _files.WriteActivityAsync("Login", user.Email, $"role={user.Role}", ct);
        return RedirectAfterAuth(user, model.ReturnUrl);
    }

    // Forbidden page for wrong role — never bounce back to Login (that felt like a 404 / broken loop).
    [HttpGet]
    public IActionResult AccessDenied()
    {
        TempData["Error"] = "You do not have access to that page. Use the menu for pages available to your account.";
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
        await _tables.UpdateCustomerAsync(user, ct);
        await _files.WriteActivityAsync("ProfileUpdate", email, $"name={user.FirstName} {user.LastName} city={user.City}", ct);

        TempData["Status"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
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
            || path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
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
            || path.StartsWith("/Customers", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Logs", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/Orders", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/Orders/Index", StringComparison.OrdinalIgnoreCase);

        if (adminOnly)
        {
            return isAdmin;
        }

        if (path.StartsWith("/Orders/Cart", StringComparison.OrdinalIgnoreCase))
        {
            return !isAdmin;
        }

        return true;
    }
}
