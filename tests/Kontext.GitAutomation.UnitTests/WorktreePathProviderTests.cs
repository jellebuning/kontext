using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class WorktreePathProviderTests
{
    private readonly WorktreePathProvider _provider = new(new GitAutomationOptions(@"C:\state", @"C:\worktrees", @"C:\repos"));

    [Test]
    public void PlacesWorktreesUnderOwnerAndRepoUsingTheBranchWithoutItsPrefix()
    {
        var path = _provider.GetPath(new RepositoryRef("acme", "app"), "kontext/123-fix-login");

        Assert.That(path, Is.EqualTo(Path.Combine(@"C:\worktrees", "acme", "app", "123-fix-login")));
    }

    [Test]
    public void ReplacesCharactersThatAreInvalidInFileNames()
    {
        var path = _provider.GetPath(new RepositoryRef("ac:me", "a?pp"), "kontext/1-x");

        Assert.That(path, Is.EqualTo(Path.Combine(@"C:\worktrees", "ac_me", "a_pp", "1-x")));
    }
}
