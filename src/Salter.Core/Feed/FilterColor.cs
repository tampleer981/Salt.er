namespace Salter.Core.Feed;

/// <summary>A colour swatch offered by the feed's colour filter.</summary>
/// <remarks>The display name is a host concern, looked up from the key (for example <c>Color_red</c>).</remarks>
/// <param name="Key">The stable lowercase identifier, for example <c>red</c>.</param>
/// <param name="Hex">The wallhaven colour searched for, as <c>#rrggbb</c>.</param>
public sealed record FilterColor(string Key, string Hex);
