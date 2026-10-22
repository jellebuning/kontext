using Kontext.Kernel;

namespace Kontext.Domain;

public sealed record WorktreeRequest(RepositoryRef Repository, string BranchName, string? BaseBranch = null);
