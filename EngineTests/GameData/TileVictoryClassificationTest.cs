using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class TileVictoryClassificationTest {

	private static Tile TileWithTerrain(string terrainKey) {
		return new Tile(ID.None("tile")) { baseTerrainType = new TerrainType { Key = terrainKey } };
	}

	[Theory]
	[InlineData("sea", true)]
	[InlineData("coast", false)]
	[InlineData("ocean", false)]
	[InlineData("grassland", false)]
	public void TerrainType_IsSea_OnlyMatchesTheSeaKey(string key, bool expected) {
		var terrain = new TerrainType { Key = key };

		Assert.Equal(expected, terrain.isSea());
	}

	[Theory]
	[InlineData("grassland", true)]  // land
	[InlineData("hills", true)]      // land
	[InlineData("coast", true)]      // coast counts too
	[InlineData("sea", false)]       // open water does not count
	[InlineData("ocean", false)]     // open water does not count
	public void Tile_IsCountedForDomination_LandAndCoastOnly(string terrainKey, bool expected) {
		Tile tile = TileWithTerrain(terrainKey);

		Assert.Equal(expected, tile.IsCountedForDomination());
	}

	[Theory]
	[InlineData("grassland", true)] // land
	[InlineData("coast", true)]     // coast
	[InlineData("sea", true)]       // sea now counts for score
	[InlineData("ocean", false)]    // ocean still excluded
	public void Tile_IsCountedForScore_IncludesSeaButNotOcean(string terrainKey, bool expected) {
		Tile tile = TileWithTerrain(terrainKey);

		Assert.Equal(expected, tile.IsCountedForScore());
	}

	[Fact]
	public void Tile_IsSea_DelegatesToTerrainType() {
		Assert.True(TileWithTerrain("sea").IsSea());
		Assert.False(TileWithTerrain("ocean").IsSea());
		Assert.False(TileWithTerrain("grassland").IsSea());
	}
}
