namespace Kontext.ExternalTools;

internal sealed record ProcessLaunch(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null, bool UseShellExecute = false);
