namespace ZaloClone.Models.Entities;

public class UserBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User Blocker { get; set; } = null!;
    public User Blocked { get; set; } = null!;
}
