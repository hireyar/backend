using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Common;

internal static class TitleComparison
{
    // Unicode whitespace plus Persian zero-width non-joiner. String.Replace is
    // translated by EF; the same expression also normalizes the incoming title.
    private const string Spacing = "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\u200C";
    private static readonly Expression<Func<string, string>> WithoutSpacing = BuildExpression();
    private static readonly Func<string, string> RemoveSpacing = WithoutSpacing.Compile();

    public static string Normalize(string title) => RemoveSpacing(title).ToLowerInvariant();

    public static Task<bool> ExistsAsync(IQueryable<string> titles, string normalized, CancellationToken ct) =>
        titles.Select(WithoutSpacing).AnyAsync(t => t.ToLower() == normalized, ct);

    private static Expression<Func<string, string>> BuildExpression()
    {
        var title = Expression.Parameter(typeof(string), "title");
        Expression result = title;
        var replace = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
        foreach (var spacing in Spacing)
            result = Expression.Call(result, replace, Expression.Constant(spacing.ToString()), Expression.Constant(""));
        return Expression.Lambda<Func<string, string>>(result, title);
    }
}
