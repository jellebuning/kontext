using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.ExternalTools.UnitTests;

[TestFixture]
public class ToolTests
{
    private const string Folder = @"C:\wt\4-add";

    private FakeProcessStarter _processes = null!;

    [SetUp]
    public void CreateFakes() => _processes = new FakeProcessStarter();

    [Test]
    public void ExplorerOpensTheFolder()
    {
        var tool = new ExplorerTool(_processes);

        tool.Open(Folder);

        Assert.That(tool.Tool, Is.EqualTo(WorkspaceTool.Explorer));
        Assert.That(_processes.Started.Single().FileName, Is.EqualTo("explorer.exe"));
        Assert.That(_processes.Started.Single().Arguments, Is.EqualTo(Expect.Seq(Folder)));
    }

    [Test]
    public void TheTerminalOpensWindowsTerminalInTheFolder()
    {
        var tool = new TerminalTool(_processes);

        tool.Open(Folder);

        Assert.That(tool.Tool, Is.EqualTo(WorkspaceTool.Terminal));
        Assert.That(_processes.Started.Single().FileName, Is.EqualTo("wt.exe"));
        Assert.That(_processes.Started.Single().Arguments, Is.EqualTo(Expect.Seq("-d", Folder)));
    }

    [Test]
    public void TheTerminalFallsBackToPowerShellWhenWindowsTerminalIsNotInstalled()
    {
        _processes.Missing.Add("wt.exe");

        new TerminalTool(_processes).Open(Folder);

        Assert.That(_processes.Started.Single().FileName, Is.EqualTo("powershell.exe"));
        Assert.That(_processes.Started.Single().WorkingDirectory, Is.EqualTo(Folder));
    }

    [Test]
    public void TheSolutionOfTheFolderIsOpenedWithItsDefaultApp()
    {
        var files = new FakeFolderFiles().WithFolder(Folder, @"C:\wt\4-add\App.sln");

        var tool = new SolutionTool(files, _processes);
        tool.Open(Folder);

        Assert.That(tool.Tool, Is.EqualTo(WorkspaceTool.Solution));
        Assert.That(_processes.Started.Single().FileName, Is.EqualTo(@"C:\wt\4-add\App.sln"));
        Assert.That(_processes.Started.Single().UseShellExecute, Is.True);
    }

    [Test]
    public void TheNewSolutionFormatIsPreferred()
    {
        var files = new FakeFolderFiles().WithFolder(Folder, @"C:\wt\4-add\App.sln", @"C:\wt\4-add\App.slnx");

        new SolutionTool(files, _processes).Open(Folder);

        Assert.That(_processes.Started.Single().FileName, Is.EqualTo(@"C:\wt\4-add\App.slnx"));
    }

    [Test]
    public void SaysSoWhenTheFolderHasNoSolution()
    {
        var files = new FakeFolderFiles().WithFolder(Folder);
        var tool = new SolutionTool(files, _processes);

        Assert.That(() => tool.Open(Folder), Throws.TypeOf<ExternalToolException>().With.Message.Contains("solution"));
        Assert.That(_processes.Started, Is.Empty);
    }
}
