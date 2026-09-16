using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

public class RegisterViewModel
{
    [Required, StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(200)]
    public string? AddressLine { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(200)]
    public string? AddressLine { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    public string Role { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmNewPassword { get; set; }
}
