using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class BorderExpansionTest {
	private static City MakeCity(int borderFactor, int culture) {
		Player player = new() { rules = new Rules { BorderFactor = borderFactor } };
		City city = new(Tile.NONE, player, "Testville", ID.None("city"));
		city.perPlayerCulture[player] = culture;
		return city;
	}

	[Theory]
	[InlineData(10, 0, 1, 10)]
	[InlineData(10, 9, 1, 10)]
	[InlineData(10, 10, 2, 100)]
	[InlineData(10, 99, 2, 100)]
	[InlineData(10, 100, 3, 1000)]
	[InlineData(10, 1000, 4, 10000)]
	[InlineData(3, 0, 1, 3)]
	[InlineData(3, 3, 2, 9)]
	[InlineData(3, 26, 3, 27)]
	[InlineData(3, 27, 4, 81)]
	public void LevelAndThresholdFollowTheBorderFactor(int factor, int culture, int expectedLevel, long expectedThreshold) {
		City city = MakeCity(factor, culture);
		Assert.Equal(expectedLevel, city.GetBorderExpansionLevel());
		Assert.Equal(expectedThreshold, city.CultureForNextBorderExpansion());
	}

	[Fact]
	public void BorderFactorUnset_Throws() {
		Player player = new() { rules = new Rules() };
		City city = new(Tile.NONE, player, "Testville", ID.None("city"));
		city.perPlayerCulture[player] = 0;
		Assert.Throws<System.InvalidOperationException>(() => city.GetBorderExpansionLevel());
	}
}
