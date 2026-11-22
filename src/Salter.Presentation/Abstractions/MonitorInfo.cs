namespace Salter.Presentation.Abstractions;

/// <summary>Describes a display attached to the machine.</summary>
/// <param name="Id">Stable platform identifier of the monitor.</param>
/// <param name="DisplayName">Human-readable name shown in the UI.</param>
/// <param name="Width">Width in physical pixels.</param>
/// <param name="Height">Height in physical pixels.</param>
/// <param name="IsPrimary">Whether this is the primary monitor.</param>
public sealed record MonitorInfo(string Id, string DisplayName, int Width, int Height, bool IsPrimary);
