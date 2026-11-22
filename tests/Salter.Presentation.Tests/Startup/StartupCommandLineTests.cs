using Salter.Presentation.Startup;

namespace Salter.Presentation.Tests.Startup;

public class StartupCommandLineTests
{
    [Fact]
    public void Format_quotes_path_and_starts_in_background()
    {
        var command = StartupCommandLine.Format("C:\\Users\\me\\AppData\\Local\\Salt.er\\Salter.App.exe");

        Assert.Equal("\"C:\\Users\\me\\AppData\\Local\\Salt.er\\Salter.App.exe\" --background", command);
        Assert.True(LaunchArguments.IsBackground(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("C:\\a\"b.exe")]
    public void Format_rejects_paths_that_cannot_be_quoted(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => StartupCommandLine.Format(path));
    }

    [Fact]
    public void Format_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => StartupCommandLine.Format(null!));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Salt.er\\Salter.App.exe\" --background", "C:\\Program Files\\Salt.er\\Salter.App.exe")]
    [InlineData("\"C:\\Program Files\\Salt.er\\Salter.App.exe\"", "C:\\Program Files\\Salt.er\\Salter.App.exe")]
    [InlineData("  \"C:\\apps\\Salter.App.exe\"--background", "C:\\apps\\Salter.App.exe")]
    [InlineData("C:\\apps\\Salter.App.exe --background", "C:\\apps\\Salter.App.exe")]
    [InlineData("C:\\apps\\Salter.App.exe\t--background", "C:\\apps\\Salter.App.exe")]
    [InlineData("C:\\apps\\Salter.App.exe", "C:\\apps\\Salter.App.exe")]
    public void GetExecutablePath_reads_first_token(string commandLine, string expected)
    {
        Assert.Equal(expected, StartupCommandLine.GetExecutablePath(commandLine));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\" --background")]
    [InlineData("\"C:\\apps\\Salter.App.exe --background")]
    public void GetExecutablePath_returns_null_without_a_path(string? commandLine)
    {
        Assert.Null(StartupCommandLine.GetExecutablePath(commandLine));
    }
}
