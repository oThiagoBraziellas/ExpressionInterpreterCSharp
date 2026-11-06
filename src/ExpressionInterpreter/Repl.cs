namespace ExpressionInterpreter;

/// <summary>Read-eval-print loop over arbitrary text streams (so it can be tested without a terminal).</summary>
public sealed class Repl
{
    private readonly Interpreter _interpreter;

    public Repl(Interpreter? interpreter = null) => _interpreter = interpreter ?? new Interpreter();

    public void Run(TextReader input, TextWriter output, bool showPrompt = true)
    {
        if (showPrompt) output.WriteLine("Expression interpreter. Type :help for commands, :quit to exit.");
        while (true)
        {
            if (showPrompt) output.Write("> ");
            var line = input.ReadLine();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line is ":quit" or ":q" or ":exit") break;
            output.WriteLine(Process(line));
        }
    }

    /// <summary>Processes a single line and returns the text to display.</summary>
    public string Process(string line)
    {
        try
        {
            if (line.StartsWith(':')) return Command(line);
            var result = _interpreter.Execute(line);
            return result.Message ?? AstPrinter.Format(result.Value!.Value);
        }
        catch (SourceException ex)
        {
            return Describe(ex, line);
        }
    }

    private string Command(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0])
        {
            case ":help":
                return string.Join(Environment.NewLine,
                    ":vars            list variables",
                    ":funcs           list user-defined and built-in functions",
                    ":ast <expr>      show the parsed tree (fully parenthesised)",
                    ":quit            exit",
                    "Examples: 2 + 3 * 4 | x = 5 | sq(n) = n ^ 2 | fact(n) = n <= 1 ? 1 : n * fact(n - 1)");
            case ":vars":
                return string.Join(Environment.NewLine,
                    _interpreter.Variables.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key} = {AstPrinter.Format(v.Value)}"));
            case ":funcs":
                return "user: " + string.Join(", ", _interpreter.UserFunctions.OrderBy(x => x, StringComparer.Ordinal)) + Environment.NewLine +
                       "builtin: " + string.Join(", ", _interpreter.BuiltinFunctions.OrderBy(x => x, StringComparer.Ordinal));
            case ":ast" when parts.Length == 2:
                return AstPrinter.Print(Parser.Parse(parts[1]));
            default:
                return $"Unknown command '{parts[0]}'. Type :help.";
        }
    }

    private static string Describe(SourceException ex, string source)
    {
        var kind = ex switch { LexException => "Syntax error", ParseException => "Parse error", _ => "Error" };
        if (ex.Position < 0 || ex.Position > source.Length) return $"{kind}: {ex.Message}";
        return $"{kind}: {ex.Message}{Environment.NewLine}  {source}{Environment.NewLine}  {new string(' ', ex.Position)}^";
    }
}
