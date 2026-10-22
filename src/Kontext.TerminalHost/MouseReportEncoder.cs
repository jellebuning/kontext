namespace Kontext.TerminalHost;

public static class MouseReportEncoder
{
    private const int MoveFlag = 32;
    private const int ReleaseCodeInLegacyFormat = 3;

    private const int LegacyLimit = 223;

    public static string? Encode(MouseButton button, MouseAction action, int column, int row, MouseModifiers modifiers, bool sgr)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 1);

        var code = (int)button + (int)modifiers;
        if (action == MouseAction.Move)
        {
            code += MoveFlag;
        }

        if (sgr)
        {
            var final = action == MouseAction.Release ? 'm' : 'M';
            return $"\u001b[<{code};{column};{row}{final}";
        }

        if (column > LegacyLimit || row > LegacyLimit)
        {
            return null;
        }

        if (action == MouseAction.Release)
        {
            code = ReleaseCodeInLegacyFormat + (int)modifiers;
        }

        return string.Concat("\u001b[M", (char)(32 + code), (char)(32 + column), (char)(32 + row));
    }
}
