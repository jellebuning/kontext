using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.GitAutomation.IntegrationTests;

[TestFixture]
public class JsonRepositoryCloneStoreTests
{
    private TempDirectory _temp = null!;
    private GitAutomationOptions _options = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _temp = new TempDirectory();
        _options = new GitAutomationOptions(_temp.Combine("state"), _temp.Combine("worktrees"), _temp.Combine("repos"));
    }

    [TearDown]
    public void DeleteScratchDirectory() => _temp.Dispose();

    [Test]
    public void FindsNothingBeforeAnythingIsSaved()
    {
        Assert.That(new JsonRepositoryCloneStore(_options).Find(new RepositoryRef("acme", "app")), Is.Null);
    }

    [Test]
    public void PersistsMappingsAcrossInstances()
    {
        new JsonRepositoryCloneStore(_options).Save(new RepositoryRef("acme", "app"), @"C:\clones\app");

        Assert.That(new JsonRepositoryCloneStore(_options).Find(new RepositoryRef("acme", "app")), Is.EqualTo(@"C:\clones\app"));
    }

    [Test]
    public void LooksUpRepositoriesIgnoringCase()
    {
        var store = new JsonRepositoryCloneStore(_options);
        store.Save(new RepositoryRef("Acme", "App"), @"C:\clones\app");

        Assert.That(store.Find(new RepositoryRef("acme", "app")), Is.EqualTo(@"C:\clones\app"));
    }

    [Test]
    public void KeepsEarlierMappingsWhenSavingAnotherRepository()
    {
        var store = new JsonRepositoryCloneStore(_options);
        store.Save(new RepositoryRef("acme", "app"), @"C:\a");
        store.Save(new RepositoryRef("acme", "other"), @"C:\b");

        Assert.That(store.Find(new RepositoryRef("acme", "app")), Is.EqualTo(@"C:\a"));
        Assert.That(store.Find(new RepositoryRef("acme", "other")), Is.EqualTo(@"C:\b"));
    }

    [Test]
    public void TreatsACorruptFileAsEmptyAndRecoversOnNextSave()
    {
        Directory.CreateDirectory(_options.StateDirectory);
        File.WriteAllText(Path.Combine(_options.StateDirectory, "clones.json"), "{ not json");
        var store = new JsonRepositoryCloneStore(_options);

        Assert.That(store.Find(new RepositoryRef("acme", "app")), Is.Null);

        store.Save(new RepositoryRef("acme", "app"), @"C:\a");
        Assert.That(store.Find(new RepositoryRef("acme", "app")), Is.EqualTo(@"C:\a"));
    }

    [Test]
    public void ListsEveryRepositoryWithASavedCloneKeepingTheNamesAsSaved()
    {
        var store = new JsonRepositoryCloneStore(_options);
        store.Save(new RepositoryRef("GalleryPixels", "GalleryPixels"), @"C:\clones\gp");
        store.Save(new RepositoryRef("acme", "app"), @"C:\clones\app");

        var listed = store.List().Select(repository => repository.FullName).ToList();

        Assert.That(listed, Is.EquivalentTo(Expect.Seq("GalleryPixels/GalleryPixels", "acme/app")));
    }

    [Test]
    public void ListsNothingBeforeAnythingIsSaved()
    {
        Assert.That(new JsonRepositoryCloneStore(_options).List(), Is.Empty);
    }}
