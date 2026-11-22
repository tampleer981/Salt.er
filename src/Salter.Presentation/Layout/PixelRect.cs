namespace Salter.Presentation.Layout;

/// <summary>A rectangle in physical pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>Gets the exclusive right edge.</summary>
    public int Right => X + Width;

    /// <summary>Gets the exclusive bottom edge.</summary>
    public int Bottom => Y + Height;
}
