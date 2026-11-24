using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class MapUnitPromotionTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;

	public MapUnitPromotionTest(SaveGameFixture fixture) {
		EngineStorage.animationsEnabled = false;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		gameData = fixture.saveGame.Clone().ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	// Replaces the experience levels with ones whose promotion chance is controlled by the test.
	private ExperienceLevel[] UseExperienceLevels(double firstLevelPromotionChance) {
		ExperienceLevel[] levels = [
			new("green", "Green", 3, 0.0, firstLevelPromotionChance),
			new("veteran", "Veteran", 4, 0.0, firstLevelPromotionChance),
			new("elite", "Elite", 5, 0.0, 0.0),
		];
		gameData.experienceLevels.Clear();
		gameData.experienceLevels.AddRange(levels);
		return levels;
	}

	private MapUnit SpawnWarrior() {
		Player human = gameData.players.First(p => p.isHuman);
		UnitPrototype warrior = gameData.unitPrototypes.Find(u => u.name == "Warrior");
		gameData.SpawnUnit(human, warrior, human.units[0].location);
		return human.units.Last();
	}

	private static void Equip(MapUnit unit, ExperienceLevel level) {
		unit.experienceLevel = level;
		unit.experienceLevelKey = level.key;
	}

	private MapUnit MakeOpponent(int attack, int defense) =>
		new(ID.None("unit")) {
			owner = gameData.players.First(p => p.isBarbarians),
			unitType = new() { attack = attack, defense = defense },
		};

	[Fact]
	public void UnitThatWinsTwiceInOneTurnIsGuaranteedToPromoteAtLeastOnce() {
		// A 0% promotion chance means the first win can't promote, so the second win has to.
		MapUnit unit = SpawnWarrior();
		ExperienceLevel[] levels = UseExperienceLevels(firstLevelPromotionChance: 0.0);
		Equip(unit, levels[0]);
		MapUnit victim = MakeOpponent(attack: 1, defense: 1);

		unit.RollToPromote(victim);
		Assert.Equal("green", unit.experienceLevelKey);

		unit.RollToPromote(victim);
		Assert.Equal("veteran", unit.experienceLevelKey);
	}

	[Fact]
	public void GuaranteedPromotionIsClearedAtStartOfNextTurn() {
		MapUnit unit = SpawnWarrior();
		ExperienceLevel[] levels = UseExperienceLevels(firstLevelPromotionChance: 0.0);
		Equip(unit, levels[0]);
		MapUnit victim = MakeOpponent(attack: 1, defense: 1);

		unit.RollToPromote(victim);
		Assert.True(unit.guaranteedPromotion);

		unit.owner.ResetUnitPromotionFlags();
		Assert.False(unit.guaranteedPromotion);

		unit.RollToPromote(victim);
		Assert.Equal("green", unit.experienceLevelKey);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(0, 1)]
	[InlineData(1, 0)]
	public void UnitNeverPromotesWhenKillingUnitWithNoCombatStrength(int attack, int defense) {
		bool isCombatUnit = attack > 0 || defense > 0;
		// 100% chance: a combat opponent would always promote, so only the combat check can prevent it.
		MapUnit unit = SpawnWarrior();
		ExperienceLevel[] levels = UseExperienceLevels(firstLevelPromotionChance: 1.0);
		Equip(unit, levels[0]);
		MapUnit victim = MakeOpponent(attack, defense);

		for (int i = 0; i < 50; i++)
			unit.RollToPromote(victim);

		if (isCombatUnit)
			Assert.Equal("elite", unit.experienceLevelKey);
		else {
			Assert.Equal("green", unit.experienceLevelKey);
			Assert.False(unit.guaranteedPromotion);
		}
	}
}
