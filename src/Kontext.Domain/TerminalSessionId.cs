namespace Kontext.Domain;

public readonly record struct TerminalSessionId(Guid Value)
{
    public static TerminalSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
