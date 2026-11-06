namespace ExpressionInterpreter;

public enum TokenKind
{
    Number, Identifier,
    Plus, Minus, Star, Slash, Percent, Caret,
    LParen, RParen, Comma, Question, Colon,
    Assign, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual,
    And, Or, Not,
    End
}

public readonly record struct Token(TokenKind Kind, string Text, int Position, double Number = 0);
