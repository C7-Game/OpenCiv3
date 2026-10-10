using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using QueryCiv3.Biq;
using Xunit;

namespace EngineTests.GameData;

public class LeaderDataTest : IClassFixture<SaveGameFixture> {
	SaveGameFixture fixture;

	public LeaderDataTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void BaseLeaderPrototypeLoadsWithLeaderFlagAndScienceAgeAction() {
		// Standalone mode has no Leader prototype: the standalone addon drops
		// every prototype that has no art replacement (see Lua/standalone/ruleset.lua).
		SaveUnitPrototype leader = fixture.saveGame.UnitPrototypes
			.Find(proto => proto.name == "Leader");

		Assert.NotNull(leader);
		Assert.Contains(SaveUnitPrototype.Flag.Leader, leader.flags);
		Assert.Contains(UnitAction.ScienceAge, leader.actions);
	}

	[Fact]
	public void LeaderFlagAndScienceAgeActionSurviveConversionToGameData() {
		// The running game reads prototypes from GameData, not from the save, so
		// the flags have to make it across that conversion.
		UnitPrototype leader = fixture.saveGame.ToGameData(fixture.behaviors).unitPrototypes
			.Find(proto => proto.name == "Leader");

		Assert.NotNull(leader);
		Assert.Contains(SaveUnitPrototype.Flag.Leader, leader.flags);
		Assert.Contains(UnitAction.ScienceAge, leader.actions);
	}

	[Fact]
	public void LeaderKindRoundTripsThroughSave() {
		// 7 is not a kind the engine knows; the raw value must survive.
		foreach (int raw in new int[] { 0, 1, 2, 7 }) {
			SaveUnit saveUnit = new() { prototype = "Leader", owner = ID.None("player"), leaderKind = raw };

			MapUnit unit = saveUnit.ToMapUnit(
				[new UnitPrototype() { name = "Leader" }],
				[],
				[new Player() { id = ID.None("player"), civilization = new Civilization("Rome") }],
				[],
				new GameMap() { tiles = [] }
			);

			Assert.Equal((LeaderKind)raw, unit.leaderKind);
			Assert.Equal(raw, new SaveUnit(unit).leaderKind);
		}
	}

	[Fact]
	public void LeaderKindValuesMatchTheSaveInt() {
		Assert.Equal(0, (int)LeaderKind.None);
		Assert.Equal(1, (int)LeaderKind.Military);
		Assert.Equal(2, (int)LeaderKind.Scientific);
	}

	[Fact]
	public void ScientificLeaderResolvesSciArtRegardlessOfEra() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Middle_Ages", LeaderKind.Scientific);

		Assert.Equal("SciLeader", unit.GetArtName());
	}

	[Fact]
	public void MilitaryLeaderUsesEraArt() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Middle_Ages", LeaderKind.Military);

		Assert.Equal("Leader Middle Ages", unit.GetArtName());
	}

	[Fact]
	public void NonLeaderUnitStillUsesEraArt() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Middle_Ages", LeaderKind.None);

		Assert.Equal("Leader Middle Ages", unit.GetArtName());
	}

	[Fact]
	public void ScientificLeaderFallsBackToDefaultArtWhenSciVariationMissing() {
		MapUnit unit = MakeLeaderArtUnit("ERAS_Ancient_Times", LeaderKind.Scientific);
		unit.unitType.art.mainArt.variations.Remove("SCI");

		Assert.Equal("Leader Ancient Times", unit.GetArtName());
	}

	[SkippableFact]
	public void SavImportReadsLeaderRulesAndNamePools() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string savPath = Path.Combine(PathUtils.getDataPath("saves"), "12345.SAV");
		Skip.If(!File.Exists(savPath), "Sample save 12345.SAV not present; run LoadSampleSaves to fetch it.");

		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		Assert.Equal(savData.Game.AllowScientificLeaders, save.Rules.AllowScientificLeaders);

		// One name pool per RACE, sized by RACE.NumberOfScientificLeaders.
		Assert.Equal(savData.Bic.Race.Length, save.Civilizations.Count);
		int totalScientificLeaders = 0;
		for (int i = 0; i < savData.Bic.Race.Length; ++i) {
			Assert.Equal(savData.Bic.Race[i].NumberOfScientificLeaders, save.Civilizations[i].scientificLeaderNames.Count);
			totalScientificLeaders += savData.Bic.Race[i].NumberOfScientificLeaders;
		}
		Assert.True(totalScientificLeaders > 0);
		Assert.Contains(save.Civilizations, civ => civ.scientificLeaderNames.Any(name => name.Length > 0));
	}

	[SkippableFact]
	public void SavImportReadsLeaderFlagAndScienceAgeActionFromPrto() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string savPath = Path.Combine(PathUtils.getDataPath("saves"), "12345.SAV");
		Skip.If(!File.Exists(savPath), "Sample save 12345.SAV not present; run LoadSampleSaves to fetch it.");

		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		List<PRTO> leaderProtos = savData.Bic.Prto.Where(prto => prto.Leader).ToList();
		Assert.NotEmpty(leaderProtos);

		foreach (PRTO prto in leaderProtos) {
			SaveUnitPrototype imported = save.UnitPrototypes.Find(proto => proto.name == prto.Name);
			Assert.NotNull(imported);
			Assert.Contains(SaveUnitPrototype.Flag.Leader, imported.flags);
			Assert.Equal(prto.ScienceAge, imported.actions.Contains(UnitAction.ScienceAge));
		}

		foreach (PRTO prto in savData.Bic.Prto.Where(prto => !prto.Leader)) {
			Assert.False(prto.ScienceAge, $"{prto.Name} unexpectedly has the Science Age bit set");
		}

		SaveUnitPrototype warrior = save.UnitPrototypes.Find(proto => proto.name == "Warrior");
		Assert.DoesNotContain(SaveUnitPrototype.Flag.Leader, warrior.flags);
		Assert.DoesNotContain(UnitAction.ScienceAge, warrior.actions);
	}

	[SkippableTheory]
	[InlineData("0-A AA Philosophy Scientific Leader.SAV", (int)LeaderKind.Scientific, "Aristotle")]
	[InlineData("0-A AA Pyrrhus Military Leader.SAV", (int)LeaderKind.Military, "Pyrrhus")]
	public void SavLeaderSavesCarryTheirKindOnTheUnit(string fileName, int expectedKind, string expectedName) {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		// Not in the repo; drop the file in C7/Saves.
		string savPath = Path.Combine(PathUtils.getBasePath("../C7/Saves"), fileName);
		Skip.If(!File.Exists(savPath), $"{fileName} not present.");

		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		int leaderIndex = Array.FindIndex(savData.Bic.Prto, prto => prto.Leader);

		Assert.Equal(1, savData.Unit.Count(unit => unit.LeaderKind != 0));
		QueryCiv3.Sav.UNIT greatLeader = savData.Unit.First(unit => unit.LeaderKind != 0);
		Assert.Equal(expectedKind, greatLeader.LeaderKind);
		Assert.Equal(expectedName, greatLeader.Name);
		Assert.Equal(leaderIndex, greatLeader.UnitType);

		SaveUnit imported = save.Units.Find(unit => unit.name == expectedName);
		Assert.NotNull(imported);
		Assert.Equal(expectedKind, imported.leaderKind);
		Assert.Equal(expectedKind, (int)(LeaderKind)imported.leaderKind);
	}

	[SkippableFact]
	public void SavImportReadsPerUnitLeaderKind() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string savPath = Path.Combine(PathUtils.getDataPath("saves"), "12345.SAV");
		Skip.If(!File.Exists(savPath), "Sample save 12345.SAV not present; run LoadSampleSaves to fetch it.");

		SavData savData = new SavData(Util.ReadFile(savPath), Util.ReadFile(PathUtils.defaultBicPath));
		SaveGame save = ImportCiv3.ImportSav(savPath, PathUtils.defaultBicPath, (_) => PathUtils.defaultPediaIconsPath);

		// This save has no great leaders, so every unit is LeaderKind.None.
		foreach (QueryCiv3.Sav.UNIT unit in savData.Unit) {
			Assert.Equal(0, unit.LeaderKind);
		}

		Assert.All(save.Units, saveUnit => Assert.Equal(0, saveUnit.leaderKind));
	}

	private static MapUnit MakeLeaderArtUnit(string era, LeaderKind kind) {
		Player player = new Player() {
			civilization = new Civilization("Rome"),
			eraCivilopediaName = era,
		};
		return new(ID.None("leader")) {
			name = "Great Scientist",
			leaderKind = kind,
			unitType = new UnitPrototype {
				name = "Leader",
				art = new Art {
					mainArt = new MainArt {
						defaultName = "Leader Ancient Times",
						variations = new Dictionary<string, string> {
							["ERAS_Ancient_Times"] = "Leader Ancient Times",
							["ERAS_Middle_Ages"] = "Leader Middle Ages",
							["SCI"] = "SciLeader",
						},
					},
				},
			},
			owner = player,
		};
	}
}
