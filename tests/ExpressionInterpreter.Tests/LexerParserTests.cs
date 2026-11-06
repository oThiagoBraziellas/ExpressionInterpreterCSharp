namespace ExpressionInterpreter.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenizes_AllKinds()
    {
        var kinds = Lexer.Tokenize("a1 = 3.5e2 + (b_2 * .5) >= 1 && !x || y != z ? 1 : 2 % 3 ^ 4 , == < > <=").Select(t => t.Kind).ToArray();
        Assert.Equal(new[]
        {
            TokenKind.Identifier, TokenKind.Assign, TokenKind.Number, TokenKind.Plus, TokenKind.LParen, TokenKind.Identifier,
            TokenKind.Star, TokenKind.Number, TokenKind.RParen, TokenKind.GreaterEqual, TokenKind.Number, TokenKind.And,
            TokenKind.Not, TokenKind.Identifier, TokenKind.Or, TokenKind.Identifier, TokenKind.NotEqual, TokenKind.Identifier,
            TokenKind.Question, TokenKind.Number, TokenKind.Colon, TokenKind.Number, TokenKind.Percent, TokenKind.Number,
            TokenKind.Caret, TokenKind.Number, TokenKind.Comma, TokenKind.Equal, TokenKind.Less, TokenKind.Greater,
            TokenKind.LessEqual, TokenKind.End
        }, kinds);
    }

    [Theory]
    [InlineData("42", 42.0)]
    [InlineData("3.25", 3.25)]
    [InlineData(".5", 0.5)]
    [InlineData("1e3", 1000.0)]
    [InlineData("2.5E-2", 0.025)]
    [InlineData("7.", 7.0)]
    public void Numbers(string text, double expected) => Assert.Equal(expected, Lexer.Tokenize(text)[0].Number);

    [Fact]
    public void ExponentWithoutDigits_IsNotConsumed()
    {
        var tokens = Lexer.Tokenize("2e");
        Assert.Equal(TokenKind.Number, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
    }

    [Fact]
    public void Positions_AreRecorded()
    {
        var tokens = Lexer.Tokenize("abc + 12");
        Assert.Equal(new[] { 0, 4, 6, 8 }, tokens.Select(t => t.Position).ToArray());
    }

    [Theory]
    [InlineData("3 # 4", 2)]
    [InlineData("a & b", 2)]
    [InlineData("a | b", 2)]
    [InlineData("1 $", 2)]
    public void UnknownCharacters_Throw(string src, int position) =>
        Assert.Equal(position, Assert.Throws<LexException>(() => Lexer.Tokenize(src)).Position);

    [Fact]
    public void HugeNumber_Throws() => Assert.Throws<LexException>(() => Lexer.Tokenize("1e999"));
}

public class ParserTests
{
    [Theory]
    [InlineData("1 + 2 * 3", "(1 + (2 * 3))")]
    [InlineData("(1 + 2) * 3", "((1 + 2) * 3)")]
    [InlineData("1 - 2 - 3", "((1 - 2) - 3)")]
    [InlineData("8 / 4 / 2", "((8 / 4) / 2)")]
    [InlineData("2 ^ 3 ^ 2", "(2 ^ (3 ^ 2))")]
    [InlineData("-2 ^ 2", "(-(2 ^ 2))")]
    [InlineData("2 ^ -1", "(2 ^ (-1))")]
    [InlineData("--3", "(-(-3))")]
    [InlineData("1 < 2 == 2 > 1", "((1 < 2) == (2 > 1))")]
    [InlineData("1 || 0 && 0", "(1 || (0 && 0))")]
    [InlineData("!a && b", "((!a) && b)")]
    [InlineData("a ? b : c ? d : e", "(a ? b : (c ? d : e))")]
    [InlineData("a == 1 ? 2 + 3 : 4", "((a == 1) ? (2 + 3) : 4)")]
    [InlineData("max(1, 2 + 3, f(4))", "max(1, (2 + 3), f(4))")]
    [InlineData("pi", "pi")]
    [InlineData("10 % 3 * 2", "((10 % 3) * 2)")]
    [InlineData("x = y = 3", null)]
    public void Precedence_AndAssociativity(string source, string? expected)
    {
        if (expected is null) { Assert.Throws<ParseException>(() => Parser.Parse(source)); return; }
        Assert.Equal(expected, AstPrinter.Print(Parser.Parse(source)));
    }

    [Fact]
    public void Assignment_AndFunctionDefinition_Parse()
    {
        Assert.IsType<AssignStmt>(Parser.Parse("x = 1 + 2"));
        var def = Assert.IsType<FunctionDefStmt>(Parser.Parse("add(a, b) = a + b"));
        Assert.Equal(new[] { "a", "b" }, def.Parameters);
        Assert.Empty(Assert.IsType<FunctionDefStmt>(Parser.Parse("zero() = 0")).Parameters);
    }

    [Fact]
    public void CallThatLooksLikeDefinition_IsCall() =>
        Assert.IsType<CallExpr>(Parser.Parse("f(1, 2)"));

    [Theory]
    [InlineData("", 0)]
    [InlineData("1 +", 3)]
    [InlineData("(1 + 2", 6)]
    [InlineData("1 2", 2)]
    [InlineData("* 3", 0)]
    [InlineData("f(1,", 4)]
    [InlineData("a ? b", 5)]
    [InlineData("f(a, a) = 1", 5)]
    [InlineData("1 + )", 4)]
    public void SyntaxErrors_ReportPosition(string source, int position) =>
        Assert.Equal(position, Assert.ThrowsAny<SourceException>(() => Parser.Parse(source)).Position);
}
