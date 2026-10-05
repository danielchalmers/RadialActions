namespace RadialActions.Tests;

public class SingleInstanceTests
{
    private static string UniqueName() => $@"Local\RadialActions.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void FirstCopy_IsFirst_LaterCopyIsNot()
    {
        var name = UniqueName();

        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
    }

    [Fact]
    public void RequestShow_FromLaterCopy_NotifiesFirstCopy()
    {
        var name = UniqueName();
        using var showRequested = new ManualResetEventSlim();
        using var first = new SingleInstance(name);
        first.ListenForShowRequests(showRequested.Set);

        using (var second = new SingleInstance(name))
        {
            second.RequestShow();
        }

        Assert.True(showRequested.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AfterFirstCopyExits_NextCopyIsFirst()
    {
        var name = UniqueName();

        new SingleInstance(name).Dispose();
        using var next = new SingleInstance(name);

        Assert.True(next.IsFirst);
    }
}
