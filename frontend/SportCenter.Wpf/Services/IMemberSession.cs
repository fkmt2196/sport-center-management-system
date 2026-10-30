namespace SportCenter.Wpf.Services;

/// <summary>Implement with the authenticated session when the auth module is integrated.</summary>
public interface IMemberSession
{
    int MemberId { get; }
    bool IsDemo { get; }
}

public sealed record DemoMemberSession(int MemberId = 1) : IMemberSession
{
    public bool IsDemo => true;
}
