using System.Collections.Generic;
using CommunityToolkit.Diagnostics;

namespace ImageMagitek.ExtensionMethods;
public static class EnumerableExtensions
{
    public static IEnumerable<TOutput> NotNull<TInput, TOutput>(this IEnumerable<TInput> source)
        where TOutput : notnull
        where TInput : TOutput
    {
        Guard.IsNotNull(source);

        return NotNullIterator<TInput, TOutput>(source);
    }

    private static IEnumerable<TOutput> NotNullIterator<TInput, TOutput>(IEnumerable<TInput> source) where TOutput : notnull
    {
        foreach (object? obj in source)
        {
            if (obj is TOutput result)
            {
                yield return result;
            }
        }
    }
}
