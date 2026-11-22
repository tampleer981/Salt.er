using System.Globalization;
using Salter.Presentation.Formatting;

namespace Salter.Presentation.Tests.Formatting;

public class FileSizeTextTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(4_299_161L, "4.1 MB")]
    [InlineData(12_998_000L, "12.4 MB")]
    [InlineData(1_048_575L, "1.0 MB")] // 1023.999 KB would read "1024.0 KB"
    [InlineData(3_221_225_472L, "3.0 GB")]
    public void Format_uses_binary_units_with_one_decimal(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeText.Format(bytes));
    }

    [Fact]
    public void Format_ignores_the_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            Assert.Equal("4.1 MB", FileSizeText.Format(4_299_161L));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Format_rejects_negative_sizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileSizeText.Format(-1));
    }
}
