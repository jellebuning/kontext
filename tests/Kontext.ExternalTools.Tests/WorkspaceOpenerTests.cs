using Kontext.Domain;

namespace Kontext.ExternalTools.Tests;

[TestFixture]
public class WorkspaceOpenerTests
{
    private const string Folder = @"C:\wt\4-add";

    private sealed class RecordingTool(WorkspaceTool tool) : IWorkspaceTool
    {
        public WorkspaceTool Tool { get; } = tool;

        public List<string> Opened { get; } = [];

        public void Open(string folder) => Opened.Add(folder);
    }

    private RecordingTool _explorer = null!;
    private RecordingTool _terminal = null!;
    private WorkspaceOpener _opener = null!;

    [SetUp]
    public void CreateOpener()
    {
        _explorer = new RecordingTool(WorkspaceTool.Explorer);
        _terminal = new RecordingTool(WorkspaceTool.Terminal);
        _opener = new WorkspaceOpener([_explorer, _terminal], new FakeFolderFiles().WithFolder(Folder));
    }

    [Test]
    public void HandsTheFolderToTheToolThatWasAskedFor()
    {
        _opener.Open(WorkspaceTool.Terminal, Folder);

        Assert.That(_terminal.Opened, Is.EqualTo(new[] { Folder }));
        Assert.That(_explorer.Opened, Is.Empty);
    }

    [Test]
    public void SaysSoWhenTheFolderIsGone()
    {
        Assert.That(() => _opener.Open(WorkspaceTool.Explorer, @"C:\gone"), Throws.TypeOf<ExternalToolException>().With.Message.Contains(@"C:\gone"));
        Assert.That(_explorer.Opened, Is.Empty);
    }

    [Test]
    public void SaysSoWhenNoToolKnowsHowToOpenIt()
    {
        Assert.That(() => _opener.Open(WorkspaceTool.VisualStudio, Folder), Throws.TypeOf<ExternalToolException>());
    }
}
