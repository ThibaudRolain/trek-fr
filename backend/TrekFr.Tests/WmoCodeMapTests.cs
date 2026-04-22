using TrekFr.Infrastructure.Weather;
using Xunit;

namespace TrekFr.Tests;

public class WmoCodeMapTests
{
    [Theory]
    [InlineData(0, "Ciel clair")]
    [InlineData(1, "Peu nuageux")]
    [InlineData(2, "Partiellement nuageux")]
    [InlineData(3, "Couvert")]
    [InlineData(45, "Brouillard")]
    [InlineData(48, "Brouillard")]
    [InlineData(51, "Bruine légère")]
    [InlineData(61, "Pluie légère")]
    [InlineData(63, "Pluie")]
    [InlineData(65, "Pluie forte")]
    [InlineData(71, "Neige légère")]
    [InlineData(80, "Averses légères")]
    [InlineData(95, "Orage")]
    [InlineData(96, "Orage avec grêle")]
    [InlineData(99, "Orage avec grêle")]
    public void Known_codes_map_to_expected_french_label(int code, string expected)
    {
        Assert.Equal(expected, WmoCodeMap.LabelFr(code));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]      // gap entre 3 et 45
    [InlineData(42)]     // gap
    [InlineData(100)]    // au-dessus du max
    [InlineData(999)]
    public void Unknown_codes_fall_back_to_dash(int code)
    {
        Assert.Equal("—", WmoCodeMap.LabelFr(code));
    }
}
