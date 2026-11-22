namespace Salter.Core.Feed;

/// <summary>
/// Rules for the tags the user excludes from the feed. wallhaven drops results carrying a tag written with a
/// leading minus in the search text (<c>q=nature -people -car</c>).
/// </summary>
/// <remarks>
/// A tag is kept as one lower-case word of letters <c>a</c>–<c>z</c>, digits, <c>_</c> and <c>-</c>. Tags with spaces
/// are rejected: wallhaven reads <c>-digital art</c> as "exclude <c>digital</c>, search <c>art</c>" and has no quoting
/// for a multi-word exclusion, so accepting them would silently search for part of the tag instead.
/// </remarks>
public static class TagExclusions
{
    /// <summary>The most tags that can be excluded at once; keeps the search text short.</summary>
    public const int MaxCount = 10;

    /// <summary>The longest tag accepted, in characters.</summary>
    public const int MaxLength = 32;

    /// <summary>Normalizes a tag typed or picked by the user.</summary>
    /// <param name="tag">The tag; surrounding white space and leading <c>#</c>, <c>-</c> or <c>+</c> are ignored.</param>
    /// <returns>The lower-case tag, or <see langword="null"/> if it is blank, too long or has other characters.</returns>
    public static string? Normalize(string? tag)
    {
        var value = tag?.Trim().TrimStart('#', '-', '+').Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return null;
        }

        foreach (var c in value)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-'))
            {
                return null;
            }
        }

        return value;
    }

    /// <summary>Normalizes a list of tags: invalid ones are dropped, duplicates removed and the rest capped.</summary>
    /// <param name="tags">The tags, or <see langword="null"/> for none.</param>
    /// <returns>At most <see cref="MaxCount"/> distinct normalized tags, in their original order.</returns>
    public static IReadOnlyList<string> NormalizeAll(IEnumerable<string?>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var tag in tags)
        {
            if (Normalize(tag) is { } value && !result.Contains(value, StringComparer.Ordinal))
            {
                result.Add(value);
                if (result.Count == MaxCount)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Appends the exclusions to a search text as <c>-tag</c> terms.</summary>
    /// <param name="text">The search text, or <see langword="null"/> for none.</param>
    /// <param name="tags">The tags to exclude; normalized with <see cref="NormalizeAll"/> first.</param>
    /// <returns>
    /// The text followed by one <c>-tag</c> term per tag, only the terms when there is no text, or
    /// <paramref name="text"/> unchanged when nothing is excluded.
    /// </returns>
    public static string? AppendTo(string? text, IEnumerable<string?>? tags)
    {
        var excluded = NormalizeAll(tags);
        if (excluded.Count == 0)
        {
            return text;
        }

        var terms = string.Join(' ', excluded.Select(tag => "-" + tag));
        return string.IsNullOrWhiteSpace(text) ? terms : text.Trim() + " " + terms;
    }
}
