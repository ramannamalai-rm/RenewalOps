using Microsoft.AspNetCore.Identity;
using RenewalOps.Domain.Enums;

namespace RenewalOps.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public UserRole Role { get; set; } = UserRole.Owner;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
