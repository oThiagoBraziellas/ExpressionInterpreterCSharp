namespace ExpressionInterpreter;

/// <summary>Abstract syntax tree. Every node remembers where it started in the source for error reporting.</summary>
public abstract record Node(int Position);

public sealed record NumberLiteral(double Value, int Position) : Node(Position);

public sealed record VariableRef(string Name, int Position) : Node(Position);

public enum UnaryOp { Negate, Plus, Not }

public sealed record UnaryExpr(UnaryOp Op, Node Operand, int Position) : Node(Position);

public enum BinaryOp { Add, Subtract, Multiply, Divide, Modulo, Power, Less, LessEqual, Greater, GreaterEqual, Equal, NotEqual, And, Or }

public sealed record BinaryExpr(BinaryOp Op, Node Left, Node Right, int Position) : Node(Position);

public sealed record ConditionalExpr(Node Condition, Node WhenTrue, Node WhenFalse, int Position) : Node(Position);

public sealed record CallExpr(string Name, IReadOnlyList<Node> Arguments, int Position) : Node(Position);

public sealed record AssignStmt(string Name, Node Value, int Position) : Node(Position);

public sealed record FunctionDefStmt(string Name, IReadOnlyList<string> Parameters, Node Body, int Position) : Node(Position);
