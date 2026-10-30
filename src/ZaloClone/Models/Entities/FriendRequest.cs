namespace ZaloClone.Models.Entities;

public class FriendRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Status { get; set; } = "Pending"; // 'Pending', 'Accepted', 'Rejected', 'Cancelled'
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}

