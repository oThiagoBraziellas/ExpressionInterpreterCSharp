namespace ExpressionInterpreter;

/// <summary>Base class for all errors with a source position (0-based character offset).</summary>
public abstract class SourceException : Exception
{
    protected SourceException(string message, int position) : base(message) => Position = position;
    public int Position { get; }
}

public sealed class LexException(string message, int position) : SourceException(message, position);

public sealed class ParseException(string message, int position) : SourceException(message, position);

public sealed class EvaluationException(string message, int position = -1) : SourceException(message, position);
