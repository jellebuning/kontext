using System.Text;

namespace Kontext.TerminalHost;

public static class PasteEncoder
{
    private const char Escape = '\u001b';
    private const string BracketedStart = "\u001b[200~";
    private const string BracketedEnd = "\u001b[201~";

    public static string Encode(string text, bool bracketed)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = new StringBuilder(text.Length + BracketedStart.Length + BracketedEnd.Length);

        if (bracketed)
        {
            body.Append(BracketedStart);
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            switch (character)
            {
                case Escape:
                    break;
                case '\r':
                    body.Append('\r');
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                case '\n':
                    body.Append('\r');
                    break;
                default:
                    body.Append(character);
                    break;
            }
        }

        if (bracketed)
        {
            body.Append(BracketedEnd);
        }

        return body.ToString();
    }
}
