using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.ExternalTools.Tests;

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
    public void VisualStudioOpensTheSolutionOfTheFolder()
    {
        var files = new FakeFolderFiles().WithFolder(Folder, @"C:\wt\4-add\App.sln");

        var tool = new VisualStudioTool(files, new FakeVisualStudioLocator(null), _processes);
        tool.Open(Folder);

        Assert.That(tool.Tool, Is.EqualTo(WorkspaceTool.VisualStudio));
        Assert.That(_processes.Started.Single().FileName, Is.EqualTo(@"C:\wt\4-add\App.sln"));
        Assert.That(_processes.Started.Single().UseShellExecute, Is.True);
    }

    [Test]
    public void VisualStudioPrefersTheNewSolutionFormat()
    {
        var files = new FakeFolderFiles().WithFolder(Folder, @"C:\wt\4-add\App.sln", @"C:\wt\4-add\App.slnx");

        new VisualStudioTool(files, new FakeVisualStudioLocator(null), _processes).Open(Folder);

        Assert.That(_processes.Started.Single().FileName, Is.EqualTo(@"C:\wt\4-add\App.slnx"));
    }

    [Test]
    public void VisualStudioOpensTheFolderItselfWhenThereIsNoSolution()
    {
        var files = new FakeFolderFiles().WithFolder(Folder);

        new VisualStudioTool(files, new FakeVisualStudioLocator(@"C:\VS\devenv.exe"), _processes).Open(Folder);

        Assert.That(_processes.Started.Single().FileName, Is.EqualTo(@"C:\VS\devenv.exe"));
        Assert.That(_processes.Started.Single().Arguments, Is.EqualTo(Expect.Seq(Folder)));
    }

    [Test]
    public void VisualStudioSaysSoWhenItIsNotInstalled()
    {
        var files = new FakeFolderFiles().WithFolder(Folder);
        var tool = new VisualStudioTool(files, new FakeVisualStudioLocator(null), _processes);

        Assert.That(() => tool.Open(Folder), Throws.TypeOf<ExternalToolException>().With.Message.Contains("Visual Studio"));
    }
}
