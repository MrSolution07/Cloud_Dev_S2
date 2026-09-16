using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;

namespace AbcRetail.Models;

/// <summary>Customer/Admin login profile stored in Azure Table Storage (Customers table, partition USER).</summary>
public class CustomerEntity : ITableEntity
{
    public const string RoleCustomer = "Customer";
    public const string RoleAdmin = "Admin";

    public string PartitionKey { get; set; } = "USER";
    public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

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

    // Hashed with PasswordHasher<CustomerEntity>; empty for legacy rows created before auth existed.
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = RoleCustomer;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
