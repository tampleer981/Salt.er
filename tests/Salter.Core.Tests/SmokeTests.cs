using Salter.Core;

namespace Salter.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Assembly_loads()
    {
        Assert.NotNull(typeof(AssemblyMarker).Assembly);
    }
}
