namespace ExpressionInterpreter.Tests;

public class InterpreterTests
{
    private static double Eval(string src) => new Interpreter().Evaluate(src);

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("-2 ^ 2", -4)]
    [InlineData("2 ^ -1", 0.5)]
    [InlineData("10 % 4", 2)]
    [InlineData("-7 % 3", -1)]
    [InlineData("7 / 2", 3.5)]
    [InlineData("1 < 2", 1)]
    [InlineData("2 <= 1", 0)]
    [InlineData("3 == 3", 1)]
    [InlineData("3 != 3", 0)]
    [InlineData("!0", 1)]
    [InlineData("!5", 0)]
    [InlineData("1 && 0", 0)]
    [InlineData("0 || 2", 1)]
    [InlineData("1 > 2 ? 10 : 20", 20)]
    [InlineData("1e3 + .5", 1000.5)]
    [InlineData("sqrt(16) + abs(-3)", 7)]
    [InlineData("max(3, 9, 4) - min(3, 9, 4)", 6)]
    [InlineData("sum(1, 2, 3, 4)", 10)]
    [InlineData("avg(2, 4)", 3)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("round(2.5)", 3)]
    [InlineData("round(3.14159, 2)", 3.14)]
    [InlineData("floor(-1.5) + ceil(1.2)", 0)]
    [InlineData("log(8, 2)", 3)]
    [InlineData("hypot(3, 4)", 5)]
    public void Arithmetic_AndFunctions(string src, double expected) => Assert.Equal(expected, Eval(src), 10);

    [Fact]
    public void Constants_Work()
    {
        Assert.Equal(Math.PI, Eval("pi"), 12);
        Assert.Equal(1.0, Eval("sin(pi / 2)"), 12);
        Assert.Equal(Math.E, Eval("exp(1)"), 12);
        Assert.Equal(-1.0, Eval("cos(pi)"), 12);
    }

    [Fact]
    public void Variables_PersistBetweenStatements()
    {
        var i = new Interpreter();
        Assert.Equal(5, i.Execute("x = 5").Value);
        Assert.Equal(12, i.Evaluate("x * 2 + 2"));
        i.Execute("x = x + 1");
        Assert.Equal(6, i.Variables["x"]);
    }

    [Fact]
    public void UserFunctions_AreCallable_AndSeeGlobals()
    {
        var i = new Interpreter();
        var def = i.Execute("area(w, h) = w * h * scale");
        Assert.Equal("defined area(w, h)", def.Message);
        Assert.Null(def.Value);
        i.Execute("scale = 2");
        Assert.Equal(24, i.Evaluate("area(3, 4)"));
    }

    [Fact]
    public void Parameters_ShadowGlobals_WithoutLeaking()
    {
        var i = new Interpreter();
        i.Execute("x = 100");
        i.Execute("f(x) = x + 1");
        Assert.Equal(2, i.Evaluate("f(1)"));
        Assert.Equal(100, i.Variables["x"]);
    }

    [Fact]
    public void Recursion_Works_AndConditionalIsLazy()
    {
        var i = new Interpreter();
        i.Execute("fact(n) = n <= 1 ? 1 : n * fact(n - 1)");
        Assert.Equal(3628800, i.Evaluate("fact(10)"));
        i.Execute("fib(n) = n < 2 ? n : fib(n - 1) + fib(n - 2)");
        Assert.Equal(610, i.Evaluate("fib(15)"));
    }

    [Fact]
    public void UnboundedRecursion_IsReportedNotCrashed()
    {
        var i = new Interpreter();
        i.Execute("loop(n) = loop(n + 1)");
        var ex = Assert.Throws<EvaluationException>(() => i.Evaluate("loop(0)"));
        Assert.Contains("Maximum call depth", ex.Message);
    }

    [Fact]
    public void Functions_CanCallOtherFunctions_AndArgumentsAreEvaluatedInCallerScope()
    {
        var i = new Interpreter();
        i.Execute("sq(x) = x ^ 2");
        i.Execute("dist(a, b) = sqrt(sq(a) + sq(b))");
        Assert.Equal(5, i.Evaluate("dist(3, 4)"));
        i.Execute("g(a, b) = a - b");
        Assert.Equal(1, i.Evaluate("g(2, 1)"), 10);
        i.Execute("h(a) = a");
        i.Execute("k(b) = h(b + 1)");
        Assert.Equal(8, i.Evaluate("k(7)"));
    }

    [Fact]
    public void ShortCircuit_AvoidsEvaluatingRightSide()
    {
        var i = new Interpreter();
        Assert.Equal(0, i.Evaluate("0 && 1 / 0"));
        Assert.Equal(1, i.Evaluate("1 || 1 / 0"));
        i.Execute("x = 0");
        Assert.Equal(0, i.Evaluate("x != 0 && 10 / x > 1"));
    }

    [Theory]
    [InlineData("1 / 0", "Division by zero", 2)]
    [InlineData("5 % 0", "Modulo by zero", 2)]
    [InlineData("y + 1", "Undefined variable 'y'", 0)]
    [InlineData("nope(1)", "Undefined function 'nope'", 0)]
    [InlineData("sqrt(1, 2)", "expects 1", 0)]
    [InlineData("max()", "at least 1", 0)]
    [InlineData("round(1, 2, 3)", "1 to 2", 0)]
    [InlineData("sqrt(-1)", "domain error", 0)]
    [InlineData("log(0)", "infinite", 0)]
    [InlineData("10 ^ 1000", "infinite", 3)]
    [InlineData("pi = 3", "Cannot reassign constant", 0)]
    [InlineData("sin = 3", "is a function", 0)]
    [InlineData("sin(x) = x", "Cannot redefine built-in", 0)]
    [InlineData("sin", "is a function", 0)]
    public void RuntimeErrors_HaveMessageAndPosition(string src, string messagePart, int position)
    {
        var ex = Assert.Throws<EvaluationException>(() => new Interpreter(constantFolding: false).Evaluate(src));
        Assert.Contains(messagePart, ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void WrongArity_ForUserFunction()
    {
        var i = new Interpreter();
        i.Execute("f(a, b) = a + b");
        Assert.Contains("expects 2", Assert.Throws<EvaluationException>(() => i.Evaluate("f(1)")).Message);
    }

    [Fact]
    public void VariableAndFunctionNamespaces_AreKeptApart()
    {
        var i = new Interpreter();
        i.Execute("x = 1");
        Assert.Throws<EvaluationException>(() => i.Execute("x(a) = a"));
    }

    [Fact]
    public void CustomFunctions_CanBeRegistered()
    {
        var i = new Interpreter();
        i.RegisterFunction("double", 1, 1, a => a[0] * 2);
        Assert.Equal(42, i.Evaluate("double(21)"));
    }

    [Fact]
    public void Evaluate_OnFunctionDefinition_Throws() =>
        Assert.Throws<EvaluationException>(() => new Interpreter().Evaluate("f(x) = x"));
}

public class ConstantFolderTests
{
    private static string Folded(string src) => AstPrinter.Print(ConstantFolder.Fold(Parser.Parse(src)));

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("x + 2 * 3", "(x + 6)")]
    [InlineData("-(2 + 3)", "-5")]
    [InlineData("1 < 2 ? x : y", "x")]
    [InlineData("f(1 + 1, 2 * 2)", "f(2, 4)")]
    [InlineData("a = 2 ^ 8", "a = 256")]
    [InlineData("g(n) = n * (1 + 1)", "g(n) = (n * 2)")]
    [InlineData("1 / 0", "(1 / 0)")]
    [InlineData("x * 0", "(x * 0)")]
    public void Folds(string src, string expected) => Assert.Equal(expected, Folded(src));

    [Fact]
    public void FoldingDoesNotChangeResults()
    {
        foreach (var src in new[] { "2 + 3 * 4 ^ 2 - 1", "(5 > 3) && (2 < 1 || 1)", "10 % 4 + -3", "1 ? 2 + 2 : 9" })
            Assert.Equal(new Interpreter(false).Evaluate(src), new Interpreter(true).Evaluate(src));
    }

    [Fact]
    public void FoldedDivisionByZero_StillReportedAtRuntime() =>
        Assert.Throws<EvaluationException>(() => new Interpreter(true).Evaluate("2 + 1 / 0"));
}

public class ReplTests
{
    [Fact]
    public void Session_ProducesExpectedTranscript()
    {
        var input = new StringReader("""
            x = 4
            sq(n) = n ^ 2
            sq(x) + 1

            :ast 1 + 2 * 3
            :vars
            1 +
            1 / 0
            :bogus
            :quit
            never reached
            """);
        var output = new StringWriter();
        new Repl().Run(input, output, showPrompt: false);
        var lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal("4", lines[0]);
        Assert.Equal("defined sq(n)", lines[1]);
        Assert.Equal("17", lines[2]);
        Assert.Equal("(1 + (2 * 3))", lines[3]);
        Assert.Contains("x = 4", output.ToString());
        Assert.Contains("pi = 3.14159265358979", output.ToString());
        Assert.DoesNotContain("never reached", output.ToString());
    }

    [Fact]
    public void Errors_ShowCaretUnderPosition()
    {
        var result = new Repl().Process("1 + * 2");
        var lines = result.Split(Environment.NewLine);
        Assert.StartsWith("Parse error", lines[0]);
        Assert.Equal("  1 + * 2", lines[1]);
        Assert.Equal("      ^", lines[2]);
    }

    [Fact]
    public void RuntimeError_IsReportedWithoutEndingTheSession()
    {
        var repl = new Repl();
        Assert.StartsWith("Error: Division by zero", repl.Process("1 / 0"));
        Assert.Equal("3", repl.Process("1 + 2"));
    }

    [Fact]
    public void Help_AndFuncs()
    {
        var repl = new Repl();
        Assert.Contains(":ast", repl.Process(":help"));
        repl.Process("f(x) = x");
        Assert.Contains("user: f", repl.Process(":funcs"));
        Assert.Contains("sqrt", repl.Process(":funcs"));
    }
}
