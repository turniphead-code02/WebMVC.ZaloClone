namespace ZaloClone.Models.Entities;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId1 { get; set; }
    public Guid UserId2 { get; set; }
    public DateTimeOffset EstablishedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;
}

