using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Normaliza lo que se oye (o el texto de un botón) para poder compararlo:
// minúsculas, sin tildes, sin signos y con los números escritos en palabras ("1" → "uno").
// Así "¡Izquierda!", "izquierda" e "IZQUIERDA" son la misma orden, en Windows y en la web.
public static class VoiceText
{
    static readonly Regex RichTextTags = new Regex("<[^>]+>", RegexOptions.Compiled);

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = RichTextTags.Replace(text, " ").ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            // La Ñ se conserva (si no, "eñe" y "ene" serían la misma letra al deletrear).
            if (c == '̃' && sb.Length > 0 && sb[sb.Length - 1] == 'n') { sb[sb.Length - 1] = 'ñ'; continue; }
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // tildes
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        string[] tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
            tokens[i] = NumberWord(tokens[i]);
        return string.Join(" ", tokens);
    }

    public static string[] Tokens(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // El navegador suele escribir los números con cifras.
    static string NumberWord(string token)
    {
        switch (token)
        {
            case "1": return "uno";
            case "2": return "dos";
            case "3": return "tres";
            default: return token;
        }
    }
}
