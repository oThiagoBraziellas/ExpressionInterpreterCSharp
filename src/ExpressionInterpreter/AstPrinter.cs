using System.Globalization;

namespace ExpressionInterpreter;

/// <summary>Renders an AST as fully parenthesised infix text, which makes precedence visible.</summary>
public static class AstPrinter
{
    public static string Print(Node node) => node switch
    {
        NumberLiteral n => Format(n.Value),
        VariableRef v => v.Name,
        UnaryExpr u => $"({Symbol(u.Op)}{Print(u.Operand)})",
        BinaryExpr b => $"({Print(b.Left)} {Symbol(b.Op)} {Print(b.Right)})",
        ConditionalExpr c => $"({Print(c.Condition)} ? {Print(c.WhenTrue)} : {Print(c.WhenFalse)})",
        CallExpr call => $"{call.Name}({string.Join(", ", call.Arguments.Select(Print))})",
        AssignStmt a => $"{a.Name} = {Print(a.Value)}",
        FunctionDefStmt f => $"{f.Name}({string.Join(", ", f.Parameters)}) = {Print(f.Body)}",
        _ => throw new ArgumentException($"Unknown node {node.GetType().Name}")
    };

    public static string Format(double value) =>
        value.ToString("G15", CultureInfo.InvariantCulture);

    private static string Symbol(UnaryOp op) => op switch { UnaryOp.Negate => "-", UnaryOp.Plus => "+", _ => "!" };

    private static string Symbol(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+", BinaryOp.Subtract => "-", BinaryOp.Multiply => "*", BinaryOp.Divide => "/",
        BinaryOp.Modulo => "%", BinaryOp.Power => "^", BinaryOp.Less => "<", BinaryOp.LessEqual => "<=",
        BinaryOp.Greater => ">", BinaryOp.GreaterEqual => ">=", BinaryOp.Equal => "==", BinaryOp.NotEqual => "!=",
        BinaryOp.And => "&&", _ => "||"
    };
}
