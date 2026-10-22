namespace Kontext.GitAutomation;

public interface IGitAuthentication
{
    IReadOnlyDictionary<string, string> EnvironmentVariables();
}
