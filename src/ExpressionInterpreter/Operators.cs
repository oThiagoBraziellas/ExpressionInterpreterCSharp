namespace ExpressionInterpreter;

/// <summary>Arithmetic semantics shared by the evaluator and the constant folder.</summary>
internal static class Operators
{
    public static double Unary(UnaryOp op, double x) => op switch
    {
        UnaryOp.Negate => -x,
        UnaryOp.Plus => x,
        _ => x == 0 ? 1 : 0
    };

    /// <summary>Applies a non-short-circuit binary operator. Throws <see cref="EvaluationException"/> for math errors.</summary>
    public static double Binary(BinaryOp op, double a, double b, int position)
    {
        switch (op)
        {
            case BinaryOp.Divide when b == 0: throw new EvaluationException("Division by zero", position);
            case BinaryOp.Modulo when b == 0: throw new EvaluationException("Modulo by zero", position);
        }
        var result = op switch
        {
            BinaryOp.Add => a + b,
            BinaryOp.Subtract => a - b,
            BinaryOp.Multiply => a * b,
            BinaryOp.Divide => a / b,
            BinaryOp.Modulo => a % b,
            BinaryOp.Power => Math.Pow(a, b),
            BinaryOp.Less => a < b ? 1 : 0,
            BinaryOp.LessEqual => a <= b ? 1 : 0,
            BinaryOp.Greater => a > b ? 1 : 0,
            BinaryOp.GreaterEqual => a >= b ? 1 : 0,
            BinaryOp.Equal => a == b ? 1 : 0,
            BinaryOp.NotEqual => a != b ? 1 : 0,
            BinaryOp.And => a != 0 && b != 0 ? 1 : 0,
            BinaryOp.Or => a != 0 || b != 0 ? 1 : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        return Check(result, position);
    }

    public static double Check(double value, int position)
    {
        if (double.IsNaN(value)) throw new EvaluationException("Result is not a number (domain error)", position);
        if (double.IsInfinity(value)) throw new EvaluationException("Result is infinite (overflow)", position);
        return value;
    }
}
