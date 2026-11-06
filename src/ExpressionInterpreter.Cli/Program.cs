using ExpressionInterpreter;

// Usage:
//   calc "2 + 3 * 4"        evaluates the expression(s) given on the command line
//   calc                    starts the REPL (reads stdin; works with pipes too)
var repl = new Repl();
if (args.Length > 0)
{
    var output = repl.Process(string.Join(' ', args));
    Console.WriteLine(output);
    return output.StartsWith("Syntax error") || output.StartsWith("Parse error") || output.StartsWith("Error") ? 1 : 0;
}

repl.Run(Console.In, Console.Out, showPrompt: !Console.IsInputRedirected);
return 0;
