using System.Runtime.InteropServices;

namespace Kontext.TerminalHost.Tests;

[TestFixture]
public class CommandLineBuilderTests
{
#pragma warning disable SYSLIB1054
    [DllImport("shell32.dll", EntryPoint = "CommandLineToArgvW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CommandLineToArgv(string commandLine, out int argumentCount);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);
#pragma warning restore SYSLIB1054

    private static List<string> SplitLikeWindows(string commandLine)
    {
        var argv = CommandLineToArgv(commandLine, out var count);
        try
        {
            return Enumerable.Range(0, count).Select(index => Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, index * IntPtr.Size))!).ToList();
        }
        finally
        {
            LocalFree(argv);
        }
    }

    private static void AssertRoundTrips(string command, params string[] arguments)
    {
        var commandLine = CommandLineBuilder.Build(command, arguments);

        Assert.That(SplitLikeWindows(commandLine), Is.EqualTo(new[] { command }.Concat(arguments).ToList()), $"command line was: {commandLine}");
    }

    [Test]
    public void KeepsSimpleArgumentsUnquoted()
    {
        Assert.That(CommandLineBuilder.Build("claude", ["--resume", "abc-123"]), Is.EqualTo("claude --resume abc-123"));
    }

    [Test]
    public void RoundTripsPlainArguments() => AssertRoundTrips("claude", "--permission-mode", "acceptEdits");

    [Test]
    public void RoundTripsArgumentsWithSpaces() => AssertRoundTrips(@"C:\Program Files\tool.exe", "two words", "a b c");

    [Test]
    public void RoundTripsEmbeddedQuotes() => AssertRoundTrips("claude", "say \"hello\" to them", "\"", "\"\"");

    [Test]
    public void RoundTripsBackslashesBeforeQuotes() => AssertRoundTrips("claude", @"path\with\""quote", @"ends with backslash\", @"two backslashes\\", @"\\server\share name\");

    [Test]
    public void RoundTripsEmptyArguments() => AssertRoundTrips("claude", string.Empty, "after");

    [Test]
    public void RoundTripsMultiLineArguments() => AssertRoundTrips("claude", "line one\nline two\r\nline three", "tab\tseparated");

    [Test]
    public void RoundTripsARealisticPrompt()
    {
        var prompt = "You are working on GitHub issue #4 in acme/app: Add \"notifications\"\n\nIssue description:\nWhen a task is due, show a toast.\n- [ ] works in tray\n\nInstructions:\n- Leave changes uncommitted.\n";

        AssertRoundTrips("claude", "--session-id", "6d8f503c-c671-e387-7413-d0d074951f3a", "--permission-mode", "acceptEdits", prompt);
    }

    [Test]
    public void RoundTripsAVeryLongArgument() => AssertRoundTrips("claude", new string('x', 20_000) + " tail");

    [Test]
    public void RoundTripsUnicode() => AssertRoundTrips("claude", "café — 日本語 🚀");

    [Test]
    public void RefusesAnEmptyCommand()
    {
        Assert.That(() => CommandLineBuilder.Build(" ", []), Throws.ArgumentException);
    }
}
