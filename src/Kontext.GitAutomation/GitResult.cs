namespace Kontext.GitAutomation;

public sealed record GitResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    public string Output => StandardOutput.Trim();
}
