namespace ExpressionInterpreter;

/// <summary>
/// Recursive-descent parser. Grammar, lowest to highest precedence:
/// <code>
/// statement   = funcdef | assignment | expression
/// funcdef     = IDENT "(" [IDENT {"," IDENT}] ")" "=" expression
/// assignment  = IDENT "=" expression
/// expression  = or ["?" expression ":" expression]
/// or          = and {"||" and}
/// and         = equality {"&amp;&amp;" equality}
/// equality    = comparison {("==" | "!=") comparison}
/// comparison  = additive {("&lt;" | "&lt;=" | "&gt;" | "&gt;=") additive}
/// additive    = term {("+" | "-") term}
/// term        = unary {("*" | "/" | "%") unary}
/// unary       = ("-" | "+" | "!") unary | power
/// power       = primary ["^" unary]            (right associative; -2^2 = -4)
/// primary     = NUMBER | IDENT ["(" [expression {"," expression}] ")"] | "(" expression ")"
/// </code>
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    private Parser(List<Token> tokens) => _tokens = tokens;

    public static Node Parse(string source)
    {
        var parser = new Parser(Lexer.Tokenize(source));
        if (parser.Peek.Kind == TokenKind.End) throw new ParseException("Empty input", 0);
        var node = parser.ParseStatement();
        if (parser.Peek.Kind != TokenKind.End)
            throw new ParseException($"Unexpected '{parser.Peek.Text}'", parser.Peek.Position);
        return node;
    }

    private Token Peek => _tokens[_pos];
    private Token PeekAt(int offset) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];
    private Token Next() => _tokens[_pos++];

    private bool Match(TokenKind kind)
    {
        if (Peek.Kind != kind) return false;
        _pos++;
        return true;
    }

    private Token Expect(TokenKind kind, string what)
    {
        if (Peek.Kind != kind)
            throw new ParseException($"Expected {what} but found {Describe(Peek)}", Peek.Position);
        return Next();
    }

    private static string Describe(Token t) => t.Kind == TokenKind.End ? "end of input" : $"'{t.Text}'";

    private Node ParseStatement()
    {
        if (Peek.Kind == TokenKind.Identifier)
        {
            if (PeekAt(1).Kind == TokenKind.Assign)
            {
                var name = Next();
                Next();
                return new AssignStmt(name.Text, ParseExpression(), name.Position);
            }
            if (PeekAt(1).Kind == TokenKind.LParen && IsFunctionDefinition())
                return ParseFunctionDefinition();
        }
        return ParseExpression();
    }

    /// <summary>Looks ahead for IDENT "(" [IDENT {"," IDENT}] ")" "=" without consuming tokens.</summary>
    private bool IsFunctionDefinition()
    {
        var i = 2;
        if (PeekAt(i).Kind == TokenKind.Identifier)
        {
            i++;
            while (PeekAt(i).Kind == TokenKind.Comma && PeekAt(i + 1).Kind == TokenKind.Identifier) i += 2;
        }
        return PeekAt(i).Kind == TokenKind.RParen && PeekAt(i + 1).Kind == TokenKind.Assign;
    }

    private Node ParseFunctionDefinition()
    {
        var name = Next();
        Expect(TokenKind.LParen, "'('");
        var parameters = new List<string>();
        if (Peek.Kind == TokenKind.Identifier)
        {
            do
            {
                var p = Expect(TokenKind.Identifier, "parameter name");
                if (parameters.Contains(p.Text)) throw new ParseException($"Duplicate parameter '{p.Text}'", p.Position);
                parameters.Add(p.Text);
            } while (Match(TokenKind.Comma));
        }
        Expect(TokenKind.RParen, "')'");
        Expect(TokenKind.Assign, "'='");
        return new FunctionDefStmt(name.Text, parameters, ParseExpression(), name.Position);
    }

    private Node ParseExpression()
    {
        var condition = ParseOr();
        if (Peek.Kind != TokenKind.Question) return condition;
        var q = Next();
        var whenTrue = ParseExpression();
        Expect(TokenKind.Colon, "':' in conditional expression");
        var whenFalse = ParseExpression();
        return new ConditionalExpr(condition, whenTrue, whenFalse, q.Position);
    }

    private Node ParseOr() => ParseLeftAssociative(ParseAnd, (TokenKind.Or, BinaryOp.Or));

    private Node ParseAnd() => ParseLeftAssociative(ParseEquality, (TokenKind.And, BinaryOp.And));

    private Node ParseEquality() =>
        ParseLeftAssociative(ParseComparison, (TokenKind.Equal, BinaryOp.Equal), (TokenKind.NotEqual, BinaryOp.NotEqual));

    private Node ParseComparison() =>
        ParseLeftAssociative(ParseAdditive,
            (TokenKind.Less, BinaryOp.Less), (TokenKind.LessEqual, BinaryOp.LessEqual),
            (TokenKind.Greater, BinaryOp.Greater), (TokenKind.GreaterEqual, BinaryOp.GreaterEqual));

    private Node ParseAdditive() =>
        ParseLeftAssociative(ParseTerm, (TokenKind.Plus, BinaryOp.Add), (TokenKind.Minus, BinaryOp.Subtract));

    private Node ParseTerm() =>
        ParseLeftAssociative(ParseUnary,
            (TokenKind.Star, BinaryOp.Multiply), (TokenKind.Slash, BinaryOp.Divide), (TokenKind.Percent, BinaryOp.Modulo));

    private Node ParseLeftAssociative(Func<Node> operand, params (TokenKind Kind, BinaryOp Op)[] operators)
    {
        var left = operand();
        while (true)
        {
            var match = operators.FirstOrDefault(o => o.Kind == Peek.Kind);
            if (match == default) return left;
            var op = Next();
            left = new BinaryExpr(match.Op, left, operand(), op.Position);
        }
    }

    private Node ParseUnary()
    {
        var t = Peek;
        switch (t.Kind)
        {
            case TokenKind.Minus: Next(); return new UnaryExpr(UnaryOp.Negate, ParseUnary(), t.Position);
            case TokenKind.Plus: Next(); return new UnaryExpr(UnaryOp.Plus, ParseUnary(), t.Position);
            case TokenKind.Not: Next(); return new UnaryExpr(UnaryOp.Not, ParseUnary(), t.Position);
            default: return ParsePower();
        }
    }

    private Node ParsePower()
    {
        var left = ParsePrimary();
        if (Peek.Kind != TokenKind.Caret) return left;
        var caret = Next();
        // The right operand is a unary, so 2^-3 works and the operator is right associative: 2^3^2 = 2^(3^2).
        return new BinaryExpr(BinaryOp.Power, left, ParseUnary(), caret.Position);
    }

    private Node ParsePrimary()
    {
        var t = Peek;
        switch (t.Kind)
        {
            case TokenKind.Number:
                Next();
                return new NumberLiteral(t.Number, t.Position);
            case TokenKind.Identifier:
                Next();
                if (Peek.Kind != TokenKind.LParen) return new VariableRef(t.Text, t.Position);
                Next();
                var args = new List<Node>();
                if (Peek.Kind != TokenKind.RParen)
                {
                    do args.Add(ParseExpression()); while (Match(TokenKind.Comma));
                }
                Expect(TokenKind.RParen, "')' after arguments");
                return new CallExpr(t.Text, args, t.Position);
            case TokenKind.LParen:
                Next();
                var inner = ParseExpression();
                Expect(TokenKind.RParen, "')'");
                return inner;
            default:
                throw new ParseException($"Unexpected {Describe(t)}", t.Position);
        }
    }
}
