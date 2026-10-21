using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ImageMagitek.Tables;

/// <summary>
/// An expression failed during evaluation, for example a division by zero or a read of an unmapped pointer
/// </summary>
public sealed class TableEvaluationException(string message) : Exception(message);

/// <summary>
/// Supplies identifier values while a <see cref="TableExpression"/> is evaluated
/// </summary>
public interface IExpressionScope
{
    /// <summary>
    /// A field of the record in scope, or <c>index</c>, <c>address</c>, <c>i</c>, <c>slot</c> or <c>value</c>
    /// </summary>
    long GetValue(string identifier);

    long GetContextValue(string field);
    long GetLookupValue(string lookup, long index, string field);
}

/// <summary>
/// The identifiers an expression may name, checked when a definition loads
/// </summary>
public sealed class ExpressionBinding
{
    public IReadOnlySet<string> Identifiers { get; init; } = new HashSet<string>();

    /// <summary>
    /// Fields readable as <c>context.field</c>, or null when the expression has no context
    /// </summary>
    public IReadOnlySet<string>? ContextFields { get; init; }

    /// <summary>
    /// Lookup names with the fields of their record type
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Lookups { get; init; } = new Dictionary<string, IReadOnlySet<string>>();
}

/// <summary>
/// A parsed integer expression with C operators and precedence, <c>min</c>, <c>max</c>, context and lookup references (LIB-TABLES-040)
/// </summary>
public sealed class TableExpression
{
    public string Text { get; }
    private readonly Node _root;

    private TableExpression(string text, Node root)
    {
        Text = text;
        _root = root;
    }

    public static MagitekResult<TableExpression> Parse(string text)
    {
        try
        {
            var parser = new Parser(Tokenize(text));
            var root = parser.ParseExpression();
            if (!parser.AtEnd)
                throw new FormatException($"unexpected '{parser.Current.Text}'");

            return new MagitekResult<TableExpression>.Success(new TableExpression(text, root));
        }
        catch (FormatException ex)
        {
            return new MagitekResult<TableExpression>.Failed($"'{text}' does not parse: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks that every identifier the expression names is in <paramref name="binding"/>
    /// </summary>
    public MagitekResult Bind(ExpressionBinding binding)
    {
        var error = _root.Bind(binding);
        return error is null ? MagitekResult.SuccessResult : new MagitekResult.Failed($"{error} in '{Text}'");
    }

    /// <exception cref="TableEvaluationException">The expression cannot be evaluated in this scope</exception>
    public long Evaluate(IExpressionScope scope)
    {
        try
        {
            return _root.Evaluate(scope);
        }
        catch (TableEvaluationException ex)
        {
            throw new TableEvaluationException($"{ex.Message} in '{Text}'");
        }
    }

    public MagitekResult<long> TryEvaluate(IExpressionScope scope)
    {
        try
        {
            return new MagitekResult<long>.Success(Evaluate(scope));
        }
        catch (TableEvaluationException ex)
        {
            return new MagitekResult<long>.Failed(ex.Message);
        }
    }

    public bool IsTrue(IExpressionScope scope) => Evaluate(scope) != 0;

    public override string ToString() => Text;

    private enum TokenKind { Number, Identifier, Symbol, End }

    private readonly record struct Token(TokenKind Kind, string Text, long Value = 0);

    private static readonly string[] _symbols =
        ["<<", ">>", "<=", ">=", "==", "!=", "(", ")", "[", "]", ".", ",", "?", ":", "+", "-", "*", "/", "%", "&", "|", "^", "~", "<", ">"];

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var pos = 0;

        while (pos < text.Length)
        {
            var c = text[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (char.IsAsciiDigit(c))
            {
                var start = pos;
                if (c == '0' && pos + 1 < text.Length && text[pos + 1] is 'x' or 'X')
                {
                    pos += 2;
                    while (pos < text.Length && char.IsAsciiHexDigit(text[pos]))
                        pos++;

                    if (pos == start + 2 || !long.TryParse(text.AsSpan(start + 2, pos - start - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                        throw new FormatException($"'{text[start..pos]}' is not a number");

                    tokens.Add(new(TokenKind.Number, text[start..pos], hex));
                }
                else
                {
                    while (pos < text.Length && char.IsAsciiDigit(text[pos]))
                        pos++;

                    if (!long.TryParse(text.AsSpan(start, pos - start), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                        throw new FormatException($"'{text[start..pos]}' is not a number");

                    tokens.Add(new(TokenKind.Number, text[start..pos], value));
                }

                if (pos < text.Length && (char.IsAsciiLetter(text[pos]) || text[pos] == '_'))
                    throw new FormatException($"'{text[start..(pos + 1)]}' is not a number");
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = pos;
                while (pos < text.Length && (char.IsAsciiLetterOrDigit(text[pos]) || text[pos] == '_'))
                    pos++;
                tokens.Add(new(TokenKind.Identifier, text[start..pos]));
            }
            else
            {
                var symbol = _symbols.FirstOrDefault(x => string.CompareOrdinal(text, pos, x, 0, x.Length) == 0)
                    ?? throw new FormatException($"unexpected character '{c}'");
                tokens.Add(new(TokenKind.Symbol, symbol));
                pos += symbol.Length;
            }
        }

        tokens.Add(new(TokenKind.End, "end of expression"));
        return tokens;
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _pos;

        public Token Current => tokens[_pos];
        public bool AtEnd => Current.Kind == TokenKind.End;

        private bool Accept(string symbol)
        {
            if (Current.Kind == TokenKind.Symbol && Current.Text == symbol)
            {
                _pos++;
                return true;
            }

            return false;
        }

        private void Expect(string symbol)
        {
            if (!Accept(symbol))
                throw new FormatException($"expected '{symbol}' but found '{Current.Text}'");
        }

        private string ExpectIdentifier()
        {
            if (Current.Kind != TokenKind.Identifier)
                throw new FormatException($"expected a name but found '{Current.Text}'");

            return tokens[_pos++].Text;
        }

        public Node ParseExpression()
        {
            var condition = ParseBinary(0);
            if (!Accept("?"))
                return condition;

            var whenTrue = ParseExpression();
            Expect(":");
            var whenFalse = ParseExpression();
            return new Ternary(condition, whenTrue, whenFalse);
        }

        // Lowest precedence first, as in C
        private static readonly string[][] _levels =
        [
            ["|"], ["^"], ["&"], ["==", "!="], ["<", "<=", ">", ">="], ["<<", ">>"], ["+", "-"], ["*", "/", "%"]
        ];

        private Node ParseBinary(int level)
        {
            if (level == _levels.Length)
                return ParseUnary();

            var left = ParseBinary(level + 1);
            while (Current.Kind == TokenKind.Symbol && _levels[level].Contains(Current.Text))
            {
                var op = tokens[_pos++].Text;
                left = new Binary(op, left, ParseBinary(level + 1));
            }

            return left;
        }

        private Node ParseUnary()
        {
            if (Accept("-"))
                return new Unary("-", ParseUnary());
            if (Accept("~"))
                return new Unary("~", ParseUnary());
            if (Accept("+"))
                return ParseUnary();

            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            var token = Current;

            if (token.Kind == TokenKind.Number)
            {
                _pos++;
                return new Literal(token.Value);
            }

            if (Accept("("))
            {
                var inner = ParseExpression();
                Expect(")");
                return inner;
            }

            if (token.Kind != TokenKind.Identifier)
                throw new FormatException($"unexpected '{token.Text}'");

            _pos++;
            var name = token.Text;

            if (name is "min" or "max" && Accept("("))
            {
                var arguments = new List<Node> { ParseExpression() };
                while (Accept(","))
                    arguments.Add(ParseExpression());
                Expect(")");
                return new Call(name, arguments);
            }

            if (name == "context" && Accept("."))
                return new ContextField(ExpectIdentifier());

            if (Accept("["))
            {
                var index = ParseExpression();
                Expect("]");
                Expect(".");
                return new LookupField(name, index, ExpectIdentifier());
            }

            return new Identifier(name);
        }
    }

    private abstract record Node
    {
        public abstract long Evaluate(IExpressionScope scope);
        public abstract string? Bind(ExpressionBinding binding);
    }

    private sealed record Literal(long Value) : Node
    {
        public override long Evaluate(IExpressionScope scope) => Value;
        public override string? Bind(ExpressionBinding binding) => null;
    }

    private sealed record Identifier(string Name) : Node
    {
        public override long Evaluate(IExpressionScope scope) => scope.GetValue(Name);

        public override string? Bind(ExpressionBinding binding) =>
            binding.Identifiers.Contains(Name) ? null : $"unknown identifier '{Name}'";
    }

    private sealed record ContextField(string Field) : Node
    {
        public override long Evaluate(IExpressionScope scope) => scope.GetContextValue(Field);

        public override string? Bind(ExpressionBinding binding) => binding.ContextFields switch
        {
            null => "'context' is only available in an output with a context",
            var fields when !fields.Contains(Field) => $"unknown context field '{Field}'",
            _ => null
        };
    }

    private sealed record LookupField(string Lookup, Node Index, string Field) : Node
    {
        public override long Evaluate(IExpressionScope scope) => scope.GetLookupValue(Lookup, Index.Evaluate(scope), Field);

        public override string? Bind(ExpressionBinding binding)
        {
            if (!binding.Lookups.TryGetValue(Lookup, out var fields))
                return $"unknown lookup '{Lookup}'";

            return fields.Contains(Field) ? Index.Bind(binding) : $"unknown field '{Field}' of lookup '{Lookup}'";
        }
    }

    private sealed record Unary(string Op, Node Operand) : Node
    {
        public override long Evaluate(IExpressionScope scope)
        {
            var value = Operand.Evaluate(scope);
            return Op == "-" ? unchecked(-value) : ~value;
        }

        public override string? Bind(ExpressionBinding binding) => Operand.Bind(binding);
    }

    private sealed record Binary(string Op, Node Left, Node Right) : Node
    {
        public override long Evaluate(IExpressionScope scope)
        {
            var a = Left.Evaluate(scope);
            var b = Right.Evaluate(scope);

            return unchecked(Op switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? throw new TableEvaluationException("division by zero") : a == long.MinValue && b == -1 ? a : a / b,
                "%" => b == 0 ? throw new TableEvaluationException("modulo by zero") : b == -1 ? 0 : a % b,
                "<<" => b is < 0 or > 63 ? throw new TableEvaluationException($"shift count {b} is outside 0 to 63") : a << (int)b,
                ">>" => b is < 0 or > 63 ? throw new TableEvaluationException($"shift count {b} is outside 0 to 63") : a >> (int)b,
                "<" => a < b ? 1 : 0,
                "<=" => a <= b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                ">=" => a >= b ? 1 : 0,
                "==" => a == b ? 1 : 0,
                "!=" => a != b ? 1 : 0,
                "&" => a & b,
                "^" => a ^ b,
                "|" => a | b,
                _ => throw new InvalidOperationException($"Unknown operator '{Op}'")
            });
        }

        public override string? Bind(ExpressionBinding binding) => Left.Bind(binding) ?? Right.Bind(binding);
    }

    private sealed record Ternary(Node Condition, Node WhenTrue, Node WhenFalse) : Node
    {
        public override long Evaluate(IExpressionScope scope) =>
            Condition.Evaluate(scope) != 0 ? WhenTrue.Evaluate(scope) : WhenFalse.Evaluate(scope);

        public override string? Bind(ExpressionBinding binding) =>
            Condition.Bind(binding) ?? WhenTrue.Bind(binding) ?? WhenFalse.Bind(binding);
    }

    private sealed record Call(string Name, List<Node> Arguments) : Node
    {
        public override long Evaluate(IExpressionScope scope)
        {
            var values = Arguments.Select(x => x.Evaluate(scope));
            return Name == "min" ? values.Min() : values.Max();
        }

        public override string? Bind(ExpressionBinding binding) =>
            Arguments.Select(x => x.Bind(binding)).FirstOrDefault(x => x is not null);
    }
}
