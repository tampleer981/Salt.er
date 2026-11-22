using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.Resources;
using Salter.Presentation.Abstractions;

namespace Salter.App.Services;

/// <summary>Reads user-visible strings from <c>Strings/en-US/Resources.resw</c> through MRT Core.</summary>
internal sealed class ResourceLocalizer : ILocalizer
{
    private readonly ResourceLoader _loader = new();

    /// <inheritdoc/>
    /// <returns>The string, or <paramref name="key"/> itself when the resource is missing.</returns>
    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        try
        {
            var value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (COMException)
        {
            // MRT Core reports a missing resource as an error; showing the key keeps the UI usable.
            return key;
        }
    }

    /// <inheritdoc/>
    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
