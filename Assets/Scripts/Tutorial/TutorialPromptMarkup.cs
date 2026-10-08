using System;
using System.Text;

/// <summary>
/// Converts tutorial prompt markup into TextMeshPro rich text:
/// <list type="bullet">
/// <item><c>[RIGHT_CLICK]</c> becomes a button prompt labelled "RIGHT CLICK" (underscores become spaces).</item>
/// <item><c>[@Aim]</c> / <c>[@Move/left]</c> shows that action's keyboard/mouse binding from
/// playerActions.inputactions (optionally a composite part), so it follows binding changes.</item>
/// <item><c>~text~</c> marks spooky (wiggling) text. An unclosed <c>~</c> runs to the end.</item>
/// <item><c>\~</c>, <c>\[</c> and <c>\\</c> print the character literally.</item>
/// </list>
/// TMP tags such as &lt;b&gt; pass through untouched.
/// </summary>
public static class TutorialPromptMarkup
{
    /// <summary>TMP link ID wrapped around spooky spans; the display wiggles the characters inside it.</summary>
    public const string SpookyLinkId = "spooky";

    /// <param name="keyFormat">Rich-text format for a button prompt; {0} is the label.</param>
    /// <param name="spookyColorHex">Colour of spooky text without '#', or null to leave it unchanged.</param>
    /// <param name="actionLabel">Resolves "Action" or "Action/part" to a binding label.</param>
    public static string ToRichText(string markup, string keyFormat, string spookyColorHex, Func<string, string> actionLabel)
    {
        if (string.IsNullOrEmpty(markup)) return string.Empty;
        var result = new StringBuilder(markup.Length * 2);
        bool spooky = false;

        for (int i = 0; i < markup.Length; i++)
        {
            char c = markup[i];
            if (c == '\\' && i + 1 < markup.Length && (markup[i + 1] == '~' || markup[i + 1] == '[' || markup[i + 1] == '\\'))
            {
                result.Append(markup[++i]);
                continue;
            }
            if (c == '~')
            {
                result.Append(spooky ? CloseSpooky(spookyColorHex) : OpenSpooky(spookyColorHex));
                spooky = !spooky;
                continue;
            }
            if (c == '[')
            {
                int end = markup.IndexOf(']', i + 1);
                if (end > i + 1)
                {
                    string token = markup.Substring(i + 1, end - i - 1).Trim();
                    result.AppendFormat(keyFormat, KeyLabel(token, actionLabel));
                    i = end;
                    continue;
                }
            }
            result.Append(c);
        }
        if (spooky) result.Append(CloseSpooky(spookyColorHex));
        return result.ToString();
    }

    private static string KeyLabel(string token, Func<string, string> actionLabel)
    {
        if (token.StartsWith("@") && actionLabel != null) return actionLabel(token.Substring(1).Trim());
        return token.Replace('_', ' ').ToUpperInvariant();
    }

    private static string OpenSpooky(string colorHex) =>
        (colorHex != null ? "<color=#" + colorHex + ">" : string.Empty) + "<link=\"" + SpookyLinkId + "\">";

    private static string CloseSpooky(string colorHex) =>
        "</link>" + (colorHex != null ? "</color>" : string.Empty);
}
