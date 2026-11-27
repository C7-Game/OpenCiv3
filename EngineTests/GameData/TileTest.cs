using C7Engine;
using C7GameData;
using C7GameData.Save;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.GameData;

public class TileTest {
	public TileTest() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		EngineStorage.gameData.techs.Add(new Tech() { id = ID.FromString("tech-1"), IrrigateWithoutWater = true });
	}

	[Fact]
	public void CanNotIrrigateWithoutWater() {
		var tile = new Tile(ID.FromString("a-1"));
		Assert.False(tile.CanBeIrrigated(MakeIrrigationImprovement(tile), new Player()));
	}

	[Fact]
	public void CanIrrigateWithElectricity() {
		var tile = new Tile(ID.FromString("a-1"));
		var player = new Player();
		player.knownTechs.Add(ID.FromString("tech-1"));
		Assert.True(tile.CanBeIrrigated(MakeIrrigationImprovement(tile), player));
	}

	static TerrainImprovement MakeIrrigationImprovement(Tile tile) {
		var save_irrigation = new SaveTerrainImprovement(Tile.TileOverlays.IRRIGATION, TerrainImprovement.Layer.ResourceDevelopment);
		var yields = new Dictionary<Tile.YieldType, int> { { Tile.YieldType.Food, 2 } };
		save_irrigation.bonusYields.Add("", yields);
		return new TerrainImprovement(save_irrigation, null, _ => tile.overlayTerrainType);
	}
}
