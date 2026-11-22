using Salter.Presentation.Startup;

namespace Salter.Presentation.Tests.Startup;

public class LaunchArgumentsTests
{
    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--background" }, true)]
    [InlineData(new[] { "--BACKGROUND" }, true)]
    [InlineData(new[] { "--other", "--background" }, true)]
    [InlineData(new[] { "--background-x" }, false)]
    public void IsBackground_detects_flag_in_argument_list(string[] args, bool expected)
    {
        Assert.Equal(expected, LaunchArguments.IsBackground(args));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("\"C:\\Program Files\\Salt.er\\Salter.App.exe\"", false)]
    [InlineData("\"C:\\Program Files\\Salt.er\\Salter.App.exe\" --background", true)]
    [InlineData("--background", true)]
    [InlineData("\"--background\"", true)]
    [InlineData("C:\\apps\\Salter.App.exe\t--Background ", true)]
    [InlineData("C:\\my--background\\Salter.App.exe", false)]
    public void IsBackground_detects_flag_in_command_line(string commandLine, bool expected)
    {
        Assert.Equal(expected, LaunchArguments.IsBackground(commandLine));
    }

    [Fact]
    public void IsBackground_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => LaunchArguments.IsBackground((string[])null!));
        Assert.Throws<ArgumentNullException>(() => LaunchArguments.IsBackground((string)null!));
    }
}
