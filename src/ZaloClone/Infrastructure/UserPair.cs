namespace ZaloClone.Infrastructure;

public static class UserPair
{
    public static (Guid User1, Guid User2) Order(Guid a, Guid b)
    {
        return a.CompareTo(b) < 0 ? (a, b) : (b, a);
    }
}
