using System.Diagnostics.CodeAnalysis;

namespace Salter.Core.Wallhaven;

/// <summary>The category of a failed wallhaven operation.</summary>
public enum WallhavenErrorKind
{
    /// <summary>The request could not reach wallhaven.</summary>
    Network,
    /// <summary>wallhaven rate-limited the request.</summary>
    RateLimited,
    /// <summary>The API key was rejected.</summary>
    Unauthorized,
    /// <summary>The resource does not exist.</summary>
    NotFound,
    /// <summary>wallhaven reported a server error.</summary>
    Server,
    /// <summary>The response could not be parsed.</summary>
    InvalidResponse,
}

/// <summary>Raised when a wallhaven request fails or returns an unusable response.</summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every failure must carry a WallhavenErrorKind; parameterless and kind-less constructors would allow invalid states.")]
public sealed class WallhavenException : Exception
{
    /// <summary>Creates an exception of the given kind.</summary>
    /// <param name="kind">The failure category.</param>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The underlying exception, if any.</param>
    public WallhavenException(WallhavenErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    /// <summary>The failure category.</summary>
    public WallhavenErrorKind Kind { get; }
}
