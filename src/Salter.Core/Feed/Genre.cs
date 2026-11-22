using Salter.Core.Wallhaven;

namespace Salter.Core.Feed;

/// <summary>A fixed browsing genre that maps to a wallhaven tag and category selection.</summary>
/// <remarks>The display name is a host concern, looked up from the key (for example <c>Genre_nature</c>).</remarks>
/// <param name="Key">The stable lowercase identifier, for example <c>nature</c>.</param>
/// <param name="Tag">The tag searched for, or <see langword="null"/> to search the whole category selection.</param>
/// <param name="Categories">The wallhaven categories searched.</param>
public sealed record Genre(string Key, string? Tag, Categories Categories);
