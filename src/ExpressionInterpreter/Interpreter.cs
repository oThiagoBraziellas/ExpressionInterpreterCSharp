namespace ExpressionInterpreter;

/// <summary>Outcome of executing one statement.</summary>
/// <param name="Value">The numeric result of an expression or assignment; null for function definitions.</param>
/// <param name="Message">Human-readable note, e.g. "defined f(x, y)".</param>
public sealed record ExecutionResult(double? Value, string? Message = null);

/// <summary>
/// Tree-walking interpreter. Holds global variables and user-defined functions; function calls get a fresh local scope
/// (parameters shadow globals) and recursion depth is limited so runaway recursion becomes a normal error.
/// All values are <see cref="double"/>; comparisons and logical operators yield 1 (true) or 0 (false).
/// </summary>
public sealed class Interpreter
{
    public const int MaxCallDepth = 200;

    private sealed record Builtin(int MinArgs, int MaxArgs, Func<double[], double> Impl);

    private readonly Dictionary<string, double> _globals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionDefStmt> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Builtin> _builtins = new(StringComparer.Ordinal);
    private readonly bool _optimize;

    public Interpreter(bool constantFolding = true)
    {
        _optimize = constantFolding;
        DefineConstant("pi", Math.PI);
        DefineConstant("e", Math.E);
        DefineConstant("tau", Math.Tau);
        RegisterDefaults();
    }

    public IReadOnlyDictionary<string, double> Variables => _globals;
    public IReadOnlyCollection<string> UserFunctions => _functions.Keys;
    public IReadOnlyCollection<string> BuiltinFunctions => _builtins.Keys;

    /// <summary>Adds a native function callable from expressions.</summary>
    public void RegisterFunction(string name, int minArgs, int maxArgs, Func<double[], double> implementation) =>
        _builtins[name] = new Builtin(minArgs, maxArgs, implementation);

    public void SetVariable(string name, double value) => _globals[name] = value;

    public double Evaluate(string source) =>
        Execute(source).Value ?? throw new EvaluationException("Statement does not produce a value");

    public ExecutionResult Execute(string source)
    {
        var node = Parser.Parse(source);
        if (_optimize) node = ConstantFolder.Fold(node);
        return Execute(node);
    }

    public ExecutionResult Execute(Node statement)
    {
        switch (statement)
        {
            case AssignStmt a:
                if (_constants.Contains(a.Name)) throw new EvaluationException($"Cannot reassign constant '{a.Name}'", a.Position);
                if (_builtins.ContainsKey(a.Name) || _functions.ContainsKey(a.Name))
                    throw new EvaluationException($"'{a.Name}' is a function and cannot be used as a variable", a.Position);
                var value = Eval(a.Value, null, 0);
                _globals[a.Name] = value;
                return new ExecutionResult(value);
            case FunctionDefStmt f:
                if (_builtins.ContainsKey(f.Name)) throw new EvaluationException($"Cannot redefine built-in function '{f.Name}'", f.Position);
                if (_globals.ContainsKey(f.Name)) throw new EvaluationException($"'{f.Name}' is already a variable", f.Position);
                _functions[f.Name] = f;
                return new ExecutionResult(null, $"defined {f.Name}({string.Join(", ", f.Parameters)})");
            default:
                return new ExecutionResult(Eval(statement, null, 0));
        }
    }

    private double Eval(Node node, Dictionary<string, double>? locals, int depth)
    {
        switch (node)
        {
            case NumberLiteral n:
                return n.Value;

            case VariableRef v:
                if (locals is not null && locals.TryGetValue(v.Name, out var local)) return local;
                if (_globals.TryGetValue(v.Name, out var global)) return global;
                if (_functions.ContainsKey(v.Name) || _builtins.ContainsKey(v.Name))
                    throw new EvaluationException($"'{v.Name}' is a function; call it as {v.Name}(...)", v.Position);
                throw new EvaluationException($"Undefined variable '{v.Name}'", v.Position);

            case UnaryExpr u:
                return Operators.Unary(u.Op, Eval(u.Operand, locals, depth));

            case BinaryExpr { Op: BinaryOp.And } b:
                // Short-circuit: the right side is not evaluated when the left is false.
                return Eval(b.Left, locals, depth) != 0 && Eval(b.Right, locals, depth) != 0 ? 1 : 0;

            case BinaryExpr { Op: BinaryOp.Or } b:
                return Eval(b.Left, locals, depth) != 0 || Eval(b.Right, locals, depth) != 0 ? 1 : 0;

            case BinaryExpr b:
                return Operators.Binary(b.Op, Eval(b.Left, locals, depth), Eval(b.Right, locals, depth), b.Position);

            case ConditionalExpr c:
                return Eval(c.Condition, locals, depth) != 0 ? Eval(c.WhenTrue, locals, depth) : Eval(c.WhenFalse, locals, depth);

            case CallExpr call:
                return Call(call, locals, depth);

            default:
                throw new EvaluationException($"Cannot evaluate {node.GetType().Name} here", node.Position);
        }
    }

    private double Call(CallExpr call, Dictionary<string, double>? locals, int depth)
    {
        if (_functions.TryGetValue(call.Name, out var fn))
        {
            if (call.Arguments.Count != fn.Parameters.Count)
                throw new EvaluationException($"{call.Name} expects {fn.Parameters.Count} argument(s) but got {call.Arguments.Count}", call.Position);
            if (depth >= MaxCallDepth)
                throw new EvaluationException($"Maximum call depth ({MaxCallDepth}) exceeded in '{call.Name}'", call.Position);
            var scope = new Dictionary<string, double>(fn.Parameters.Count, StringComparer.Ordinal);
            for (var i = 0; i < fn.Parameters.Count; i++) scope[fn.Parameters[i]] = Eval(call.Arguments[i], locals, depth);
            return Eval(fn.Body, scope, depth + 1);
        }

        if (_builtins.TryGetValue(call.Name, out var builtin))
        {
            var n = call.Arguments.Count;
            if (n < builtin.MinArgs || n > builtin.MaxArgs)
            {
                var expected = builtin.MinArgs == builtin.MaxArgs ? $"{builtin.MinArgs}"
                    : builtin.MaxArgs == int.MaxValue ? $"at least {builtin.MinArgs}" : $"{builtin.MinArgs} to {builtin.MaxArgs}";
                throw new EvaluationException($"{call.Name} expects {expected} argument(s) but got {n}", call.Position);
            }
            var args = new double[n];
            for (var i = 0; i < n; i++) args[i] = Eval(call.Arguments[i], locals, depth);
            return Operators.Check(builtin.Impl(args), call.Position);
        }

        throw new EvaluationException($"Undefined function '{call.Name}'", call.Position);
    }

    private void DefineConstant(string name, double value)
    {
        _globals[name] = value;
        _constants.Add(name);
    }

    private void RegisterDefaults()
    {
        void F1(string name, Func<double, double> f) => RegisterFunction(name, 1, 1, a => f(a[0]));
        F1("sin", Math.Sin); F1("cos", Math.Cos); F1("tan", Math.Tan);
        F1("asin", Math.Asin); F1("acos", Math.Acos); F1("atan", Math.Atan);
        F1("sqrt", Math.Sqrt); F1("cbrt", Math.Cbrt); F1("abs", Math.Abs); F1("exp", Math.Exp);
        F1("floor", Math.Floor); F1("ceil", Math.Ceiling); F1("trunc", Math.Truncate);
        F1("sign", x => Math.Sign(x)); F1("log10", Math.Log10); F1("log2", Math.Log2);
        RegisterFunction("round", 1, 2, a => a.Length == 1 ? Math.Round(a[0], MidpointRounding.AwayFromZero) : Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero));
        RegisterFunction("log", 1, 2, a => a.Length == 1 ? Math.Log(a[0]) : Math.Log(a[0], a[1]));
        RegisterFunction("atan2", 2, 2, a => Math.Atan2(a[0], a[1]));
        RegisterFunction("pow", 2, 2, a => Math.Pow(a[0], a[1]));
        RegisterFunction("hypot", 2, 2, a => Math.Sqrt(a[0] * a[0] + a[1] * a[1]));
        RegisterFunction("min", 1, int.MaxValue, a => a.Min());
        RegisterFunction("max", 1, int.MaxValue, a => a.Max());
        RegisterFunction("sum", 1, int.MaxValue, a => a.Sum());
        RegisterFunction("avg", 1, int.MaxValue, a => a.Average());
        RegisterFunction("clamp", 3, 3, a => Math.Clamp(a[0], a[1], a[2]));
    }
}
