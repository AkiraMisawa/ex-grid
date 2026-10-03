namespace ExSheet.Engine.Formulas;

/// <summary>Text functions admitted against the 2026-10-03 Windows observations (ADR-0047).</summary>
internal static partial class FunctionLibrary
{
    private static Operand Upper(FunctionCall call)
    {
        if (!TryText(call, 0, out var text, out var failure)) return failure;
        if (text.Any(c => !ObservedCaseCharacter(c))) return Operand.Of(ErrorValue.Value);
        return Operand.Of(Value.FromText(new string(text.Select(ObservedUpper).ToArray())));
    }

    private static Operand Lower(FunctionCall call)
    {
        if (!TryText(call, 0, out var text, out var failure)) return failure;
        if (text.Any(c => !ObservedCaseCharacter(c) && c is not ('Α' or 'Ο')) || !ObservedSigmaContexts(text))
            return Operand.Of(ErrorValue.Value);
        var result = text.ToCharArray();
        for (var i = 0; i < result.Length; i++)
            result[i] = text[i] == 'Σ'
                ? (i + 1 < text.Length && text[i + 1] is 'Α' or 'Ο' or 'Σ' ? 'σ' : 'ς')
                : ObservedLower(text[i]);
        return Operand.Of(Value.FromText(new string(result)));
    }

    // LOWER's contextual sigma is observed only in uppercase Greek words separated by spaces.
    // A mixed script, a combining mark or punctuation beside that word remains unobserved.
    private static bool ObservedSigmaContexts(string text)
    {
        foreach (var word in text.Split(' '))
            if (word.Contains('Σ') && word.Any(c => c is not ('Α' or 'Ο' or 'Σ'))) return false;
        return true;
    }

    private static char ObservedLower(char c) => c switch
    {
        >= 'A' and <= 'Z' => (char)(c + 32),
        'É' => 'é',
        'İ' => 'i',
        'Ǳ' or 'ǲ' => 'ǳ',
        'Α' => 'α',
        'Ο' => 'ο',
        _ => c,
    };

    private static Operand Proper(FunctionCall call)
    {
        if (!TryText(call, 0, out var text, out var failure)) return failure;
        if (text.Any(c => !ObservedCaseCharacter(c))) return Operand.Of(ErrorValue.Value);
        // Only isolated sigma letters were observed for PROPER; its word context is separate
        // from LOWER's, and cannot be inferred from a Unicode title-case implementation.
        foreach (var word in text.Split(' '))
            if (word.Length > 1 && word.Any(c => c is 'σ' or 'ς' or 'Σ')) return Operand.Of(ErrorValue.Value);
        var result = text.ToCharArray();
        var initial = true;
        for (var i = 0; i < result.Length; i++)
        {
            var lowered = ObservedLower(text[i]);
            result[i] = initial ? ObservedUpper(lowered) : lowered;
            initial = !ObservedCaseLetter(text[i]);
        }
        return Operand.Of(Value.FromText(new string(result)));
    }

    private static bool ObservedCaseLetter(char c) => char.IsAsciiLetter(c) || c is
        'é' or 'É' or 'ß' or 'ẞ' or 'ı' or 'İ' or 'Ǳ' or 'ǲ' or 'ǳ' or 'ﬃ' or 'σ' or 'ς' or 'Σ';

    /// <summary>
    /// SEARCH has its own observed case folding: dotless i does not equal i, although UPPER
    /// maps both to I. Positions and question marks are answered only for the admitted BMP text.
    /// </summary>
    private static Operand SearchText(FunctionCall call)
    {
        if (!TryText(call, 0, out var wanted, out var failure)) return failure;
        if (!TryText(call, 1, out var within, out failure)) return failure;
        var start = 1.0;
        if (call.Has(2) && !TryNumber(call, 2, out start, out failure)) return failure;
        start = Math.Truncate(start);
        if (start < 1 || start > within.Length || wanted.Any(c => !ObservedSearchCharacter(c)) ||
            within.Any(c => !ObservedSearchCharacter(c))) return Operand.Of(ErrorValue.Value);
        if (wanted.Length == 0) return Operand.Of(Value.FromNumber(start));

        var tokens = new List<(char Character, bool Wild)>();
        for (var i = 0; i < wanted.Length; i++)
        {
            var c = wanted[i];
            if (c == '~' && i + 1 < wanted.Length && wanted[i + 1] is '*' or '?' or '~')
                tokens.Add((wanted[++i], false));
            else
                tokens.Add((c, c is '*' or '?'));
        }

        // A suffix of the pattern is matched at every text position. Once the pattern ends,
        // remaining text is allowed: SEARCH finds a substring, unlike a full-cell criterion.
        var next = new bool[within.Length + 1];
        var current = new bool[within.Length + 1];
        Array.Fill(next, true);
        for (var p = tokens.Count - 1; p >= 0; p--)
        {
            var (c, wild) = tokens[p];
            current[within.Length] = wild && c == '*' && next[within.Length];
            for (var t = within.Length - 1; t >= (int)start - 1; t--)
                current[t] = wild && c == '*'
                    ? next[t] || current[t + 1]
                    : next[t + 1] && (wild || ObservedSearchFold(c) == ObservedSearchFold(within[t]));
            (current, next) = (next, current);
        }
        for (var i = (int)start - 1; i < within.Length; i++)
            if (next[i]) return Operand.Of(Value.FromNumber(i + 1));
        return Operand.Of(ErrorValue.Value);
    }

    private static bool ObservedSearchCharacter(char c) => char.IsAscii(c) || c is
        'é' or 'É' or 'ß' or 'ı' or 'İ' or 'σ' or 'ς' or 'Σ';

    private static char ObservedSearchFold(char c) => c switch
    {
        >= 'a' and <= 'z' => (char)(c - 32),
        'é' => 'É',
        'σ' or 'ς' => 'Σ',
        _ => c,
    };

    // An explicit alphabet keeps the result independent of an operating system's Unicode tables.
    private static bool ObservedCaseCharacter(char c) => char.IsAscii(c) || c is
        'é' or 'É' or 'ß' or 'ẞ' or 'ı' or 'İ' or 'Ǳ' or 'ǲ' or 'ǳ' or 'ﬃ' or '\u00a0' or 'σ' or 'ς' or 'Σ';

    private static char ObservedUpper(char c) => c switch
    {
        >= 'a' and <= 'z' => (char)(c - 32),
        'é' => 'É',
        'ı' => 'I',
        'ǲ' or 'ǳ' => 'Ǳ',
        'σ' or 'ς' => 'Σ',
        _ => c,
    };
}
