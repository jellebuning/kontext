using Kontext.Domain;

namespace Kontext.ExternalTools;

internal interface IWorkspaceTool
{
    WorkspaceTool Tool { get; }

    void Open(string folder);
}
