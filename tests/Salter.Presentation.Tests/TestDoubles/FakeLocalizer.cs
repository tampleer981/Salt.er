using System.Globalization;
using Salter.Presentation.Abstractions;

namespace Salter.Presentation.Tests.TestDoubles;

/// <summary>Localizer that returns the key itself, so tests can assert which string was chosen.</summary>
internal sealed class FakeLocalizer : ILocalizer
{
    public string Get(string key) => key;

    public string Format(string key, params object[] args) =>
        string.Join('|', [key, .. args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture))]);
}
