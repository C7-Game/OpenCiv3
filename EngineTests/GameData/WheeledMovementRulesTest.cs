using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

/// <summary>
/// Guards the shipped ruleset data for terrain passability and the units Civ3
/// marks as wheeled.
///
/// Civ3 marks mountains, jungle, marsh and volcano as impassable to wheeled
/// units, and flags chariots, catapults, cannon, trebuchets and the like as
/// wheeled. Each terrain lists which unit abilities it blocks and which
/// improvements let them through, so a typo in the data would otherwise
/// silently do nothing.
/// </summary>
public class WheeledMovementRulesTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly SaveGame save;

	public WheeledMovementRulesTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		save = fixture.saveGame;
	}

	[Theory]
	[InlineData("mountains")]
	[InlineData("jungle")]
	[InlineData("marsh")]
	[InlineData("volcano")]
	public void TerrainIsImpassableToWheeled(string key) {
		TerrainType terrain = save.TerrainTypes.First(t => t.Key == key);

		// Civ3 lifts the restriction when the tile is roaded or railed.
		Assert.Equal(new[] { "road", "railroad" }, terrain.impassableTo[SaveUnitPrototype.Flag.Wheeled]);
	}

	[Theory]
	[InlineData("desert")]
	[InlineData("plains")]
	[InlineData("grassland")]
	[InlineData("tundra")]
	[InlineData("hills")]
	[InlineData("forest")]
	public void TerrainIsPassableToWheeled(string key) {
		TerrainType terrain = save.TerrainTypes.First(t => t.Key == key);

		Assert.DoesNotContain(SaveUnitPrototype.Flag.Wheeled, terrain.impassableTo.Keys);
	}

	[Theory]
	[InlineData("Chariot")]
	[InlineData("War Chariot")]
	[InlineData("Three-Man Chariot")]
	[InlineData("Catapult")]
	[InlineData("Trebuchet")]
	[InlineData("Cannon")]
	[InlineData("Hwach'a")]
	public void UnitIsWheeled(string name) {
		SaveUnitPrototype proto = save.UnitPrototypes.First(p => p.name == name);

		Assert.Contains(SaveUnitPrototype.Flag.Wheeled, proto.flags);
	}

	[Theory]
	[InlineData("Settler")]
	[InlineData("Worker")]
	[InlineData("Warrior")]
	[InlineData("Horseman")]
	public void UnitIsNotWheeled(string name) {
		SaveUnitPrototype proto = save.UnitPrototypes.First(p => p.name == name);

		Assert.DoesNotContain(SaveUnitPrototype.Flag.Wheeled, proto.flags);
	}

	[Fact]
	public void WheeledFlagSurvivesConversionToRuntimeUnitPrototype() {
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);

		// The movement code reads the runtime unit prototype, so the flag has
		// to make it across from the save data for the rule to take effect.
		UnitPrototype catapult = gameData.unitPrototypes.First(p => p.name == "Catapult");
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");

		Assert.True(catapult.wheeled);
		Assert.False(warrior.wheeled);
	}

	[Fact]
	public void TerrainRestrictionSurvivesConversionToRuntimeGameData() {
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);

		// The movement code reads the terrain off the tile, so the restriction
		// has to make it across from the save data for the rule to take effect.
		TerrainType mountains = gameData.terrainTypes.First(t => t.Key == "mountains");

		Assert.Equal(new[] { "road", "railroad" }, mountains.impassableTo[SaveUnitPrototype.Flag.Wheeled]);
	}
}
