using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.TestSupport;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class TestKontextException(string message) : KontextException(message);

public static class TrackerExtensions
{
    public static List<IssueWorkStatus> Record(this IIssueWorkTracker tracker, IssueKey issue)
    {
        var statuses = new List<IssueWorkStatus>();
        tracker.Changed += (_, e) =>
        {
            if (e.Issue == issue)
            {
                statuses.Add(e.State.Status);
            }
        };

        return statuses;
    }
}

public static class AsyncAssert
{
    public static async Task EventuallyAsync(Func<bool> condition, string because)
    {
        var deadline = Task.Delay(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (deadline.IsCompleted)
            {
                Assert.Fail($"Timed out waiting for: {because}");
            }

            await Task.Delay(10);
        }
    }
}

public static class Expect
{
    public static T[] Seq<T>(params T[] items) => items;
}
