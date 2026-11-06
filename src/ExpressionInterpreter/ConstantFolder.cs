namespace ExpressionInterpreter;

/// <summary>
/// Simplifies an AST by evaluating sub-trees that contain only literals. Sub-trees that would raise an error
/// (such as <c>1/0</c>) are left intact so the error is still reported at run time, with its position.
/// </summary>
public static class ConstantFolder
{
    public static Node Fold(Node node)
    {
        switch (node)
        {
            case UnaryExpr u:
            {
                var operand = Fold(u.Operand);
                return operand is NumberLiteral n
                    ? new NumberLiteral(Operators.Unary(u.Op, n.Value), u.Position)
                    : u with { Operand = operand };
            }
            case BinaryExpr b:
            {
                var left = Fold(b.Left);
                var right = Fold(b.Right);
                if (left is NumberLiteral l && right is NumberLiteral r)
                {
                    try { return new NumberLiteral(Operators.Binary(b.Op, l.Value, r.Value, b.Position), b.Position); }
                    catch (EvaluationException) { /* keep the node; the evaluator reports it */ }
                }
                return b with { Left = left, Right = right };
            }
            case ConditionalExpr c:
            {
                var cond = Fold(c.Condition);
                var yes = Fold(c.WhenTrue);
                var no = Fold(c.WhenFalse);
                return cond is NumberLiteral n ? (n.Value != 0 ? yes : no) : c with { Condition = cond, WhenTrue = yes, WhenFalse = no };
            }
            case CallExpr call:
                return call with { Arguments = call.Arguments.Select(Fold).ToList() };
            case AssignStmt a:
                return a with { Value = Fold(a.Value) };
            case FunctionDefStmt f:
                return f with { Body = Fold(f.Body) };
            default:
                return node;
        }
    }
}
