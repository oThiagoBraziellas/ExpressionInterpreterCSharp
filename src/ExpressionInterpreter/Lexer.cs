using System.Globalization;

namespace ExpressionInterpreter;

/// <summary>Converts source text into tokens. Always ends the list with an <see cref="TokenKind.End"/> token.</summary>
public static class Lexer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < source.Length && char.IsAsciiDigit(source[i + 1])))
            {
                tokens.Add(ReadNumber(source, ref i));
                continue;
            }
            if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = i;
                while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Identifier, source[start..i], start));
                continue;
            }

            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            var (kind, length) = c switch
            {
                '+' => (TokenKind.Plus, 1),
                '-' => (TokenKind.Minus, 1),
                '*' => (TokenKind.Star, 1),
                '/' => (TokenKind.Slash, 1),
                '%' => (TokenKind.Percent, 1),
                '^' => (TokenKind.Caret, 1),
                '(' => (TokenKind.LParen, 1),
                ')' => (TokenKind.RParen, 1),
                ',' => (TokenKind.Comma, 1),
                '?' => (TokenKind.Question, 1),
                ':' => (TokenKind.Colon, 1),
                '=' when next == '=' => (TokenKind.Equal, 2),
                '=' => (TokenKind.Assign, 1),
                '!' when next == '=' => (TokenKind.NotEqual, 2),
                '!' => (TokenKind.Not, 1),
                '<' when next == '=' => (TokenKind.LessEqual, 2),
                '<' => (TokenKind.Less, 1),
                '>' when next == '=' => (TokenKind.GreaterEqual, 2),
                '>' => (TokenKind.Greater, 1),
                '&' when next == '&' => (TokenKind.And, 2),
                '|' when next == '|' => (TokenKind.Or, 2),
                _ => throw new LexException(
                    c is '&' or '|' ? $"Unexpected '{c}' (did you mean '{c}{c}'?)" : $"Unexpected character '{c}'", i)
            };
            tokens.Add(new Token(kind, source.Substring(i, length), i));
            i += length;
        }
        tokens.Add(new Token(TokenKind.End, "", source.Length));
        return tokens;
    }

    private static Token ReadNumber(string s, ref int i)
    {
        var start = i;
        while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        }
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            var j = i + 1;
            if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
            if (j < s.Length && char.IsAsciiDigit(s[j]))
            {
                while (j < s.Length && char.IsAsciiDigit(s[j])) j++;
                i = j;
            }
        }
        var text = s[start..i];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value))
            throw new LexException($"Invalid number '{text}'", start);
        return new Token(TokenKind.Number, text, start, value);
    }
}
