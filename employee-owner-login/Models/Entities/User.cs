namespace PoultryOS.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Sale> Sales { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();
}
