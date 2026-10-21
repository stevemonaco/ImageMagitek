using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ImageMagitek.Tables;

/// <summary>
/// A name or folder template: literal text with <c>{expr}</c>, <c>{expr:X6}</c>, <c>{table[expr]}</c>, <c>{name:outputId}</c> and <c>{n}</c> segments (LIB-TABLES-080)
/// </summary>
public sealed partial class NameTemplate
{
    public IReadOnlyList<Segment> Segments { get; }

    public abstract record Segment;
    public sealed record LiteralSegment(string Text) : Segment;
    public sealed record ExpressionSegment(TableExpression Expression, char Format, int Digits) : Segment;
    public sealed record NameTableSegment(NameTable Table, TableExpression Value) : Segment;
    public sealed record OutputNameSegment(string OutputId) : Segment;
    public sealed record CounterSegment : Segment;

    private NameTemplate(IReadOnlyList<Segment> segments)
    {
        Segments = segments;
    }

    public bool UsesCounter => Segments.Any(x => x is CounterSegment);
    public IEnumerable<string> OutputNames => Segments.OfType<OutputNameSegment>().Select(x => x.OutputId);
    public IEnumerable<TableExpression> Expressions => Segments.Select(x => x switch
    {
        ExpressionSegment e => e.Expression,
        NameTableSegment t => t.Value,
        _ => null
    }).OfType<TableExpression>();

    [GeneratedRegex(@"^(?<expr>.*):(?<format>[XxDd])(?<digits>[0-9]{0,2})$", RegexOptions.Singleline)]
    private static partial Regex FormatPattern();

    [GeneratedRegex(@"^\s*(?<table>[A-Za-z_][A-Za-z0-9_]*)\s*\[(?<expr>.*)\]\s*$", RegexOptions.Singleline)]
    private static partial Regex NameTablePattern();

    public static MagitekResult<NameTemplate> Parse(string text, IReadOnlyDictionary<string, NameTable> nameTables)
    {
        var segments = new List<Segment>();
        var literal = new StringBuilder();
        var pos = 0;

        while (pos < text.Length)
        {
            var c = text[pos];
            if (c == '{' && pos + 1 < text.Length && text[pos + 1] == '{')
            {
                literal.Append('{');
                pos += 2;
            }
            else if (c == '}' && pos + 1 < text.Length && text[pos + 1] == '}')
            {
                literal.Append('}');
                pos += 2;
            }
            else if (c == '}')
            {
                return new MagitekResult<NameTemplate>.Failed($"Template '{text}' has an unmatched '}}'");
            }
            else if (c == '{')
            {
                var end = text.IndexOf('}', pos + 1);
                if (end < 0)
                    return new MagitekResult<NameTemplate>.Failed($"Template '{text}' has an unmatched '{{'");

                if (literal.Length > 0)
                {
                    segments.Add(new LiteralSegment(literal.ToString()));
                    literal.Clear();
                }

                var segmentResult = ParseSegment(text[(pos + 1)..end].Trim(), nameTables);
                if (segmentResult.HasFailed)
                    return new MagitekResult<NameTemplate>.Failed($"Template '{text}': {segmentResult.AsError.Reason}");

                segments.Add(segmentResult.AsSuccess.Result);
                pos = end + 1;
            }
            else
            {
                literal.Append(c);
                pos++;
            }
        }

        if (literal.Length > 0)
            segments.Add(new LiteralSegment(literal.ToString()));

        return new MagitekResult<NameTemplate>.Success(new NameTemplate(segments));
    }

    private static MagitekResult<Segment> ParseSegment(string body, IReadOnlyDictionary<string, NameTable> nameTables)
    {
        if (body == "n")
            return new MagitekResult<Segment>.Success(new CounterSegment());

        if (body.StartsWith("name:", StringComparison.Ordinal))
            return new MagitekResult<Segment>.Success(new OutputNameSegment(body["name:".Length..].Trim()));

        if (NameTablePattern().Match(body) is { Success: true } tableMatch && nameTables.TryGetValue(tableMatch.Groups["table"].Value, out var table))
        {
            var valueResult = TableExpression.Parse(tableMatch.Groups["expr"].Value);
            return valueResult.HasSucceeded
                ? new MagitekResult<Segment>.Success(new NameTableSegment(table, valueResult.AsSuccess.Result))
                : new MagitekResult<Segment>.Failed(valueResult.AsError.Reason);
        }

        var expressionText = body;
        var format = 'D';
        var digits = 0;

        if (FormatPattern().Match(body) is { Success: true } formatMatch)
        {
            expressionText = formatMatch.Groups["expr"].Value;
            format = char.ToUpperInvariant(formatMatch.Groups["format"].Value[0]);
            digits = formatMatch.Groups["digits"].Length > 0 ? int.Parse(formatMatch.Groups["digits"].Value) : 0;
        }

        var result = TableExpression.Parse(expressionText);
        return result.HasSucceeded
            ? new MagitekResult<Segment>.Success(new ExpressionSegment(result.AsSuccess.Result, format, digits))
            : new MagitekResult<Segment>.Failed(result.AsError.Reason);
    }

    public static string FormatNumber(long value, char format, int digits) =>
        format == 'X' ? value.ToString($"X{digits}") : value.ToString($"D{digits}");
}
