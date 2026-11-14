using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using IniParser.Model;
using Xunit;

namespace EngineTests;

public class GameSetupTests {
	private static (SaveGame save, WorldCharacteristics wc) CreateTestWorld() {
		GameMode.Config config = new("civ3");
		SaveGame save = GameMode.Load(PathUtils.GameModesDir, config).GetSave();

		WorldSize worldSize = new() {
			width = 60,
			height = 60,
			numberOfCivs = 4,
			distanceBetweenCivs = 8,
			techRate = 240,
			optimalNumberOfCities = 20,
		};

		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = worldSize,
			mapSeed = 42,
		};

		return (save, wc);
	}

	[Fact]
	public void NoneOpponentsDoNotBecomePlayers() {
		(SaveGame save, WorldCharacteristics wc) = CreateTestWorld();

		GameSetup gameSetup = new() {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = [
				new SelectedOpponent { isNone = true },
				new SelectedOpponent { isRandom = true },
				new SelectedOpponent { isNone = true },
			],
			victoryConditions = new VictoryConditions()
		};

		gameSetup.Populate(save);

		// Barbarian + human + one rival; the two NONE slots add no players.
		Assert.Equal(3, save.Players.Count);
		string barbarianCiv = save.Civilizations.First(c => c.isBarbarian).name;
		Assert.Equal(2, save.Players.Count(p => p.civilization != barbarianCiv));

		// Starts are generated for the map's maximum civ count.
		Assert.Equal(wc.worldSize.numberOfCivs, save.Map.startingLocations.Count);
	}

	// All NONE rivals means a solo game: only the barbarian and the human.
	[Fact]
	public void AllNoneOpponentsMakeSoloGame() {
		(SaveGame save, WorldCharacteristics wc) = CreateTestWorld();

		GameSetup gameSetup = new() {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = [
				new SelectedOpponent { isNone = true },
				new SelectedOpponent { isNone = true },
				new SelectedOpponent { isNone = true },
			],
			victoryConditions = new VictoryConditions()
		};

		gameSetup.Populate(save);

		Assert.Equal(2, save.Players.Count);
		string barbarianCiv = save.Civilizations.First(c => c.isBarbarian).name;
		Assert.Equal(1, save.Players.Count(p => p.civilization != barbarianCiv));

		// Starts are generated for the map's maximum civ count.
		Assert.Equal(wc.worldSize.numberOfCivs, save.Map.startingLocations.Count);
	}

	private static readonly List<Civilization> availableCivs = [
		new Civilization { name = "Rome" },
		new Civilization { name = "Germany" },
	];

	// Parse against a scratch settings store so the tests don't touch C7.ini.
	private static List<SelectedOpponent> OpponentsFromStored(string storedValue, int expectedCount) {
		IniData previousSettings = C7Settings.settings;
		try {
			C7Settings.settings = new IniData();
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Opponents, storedValue);
			return GameSetup.OpponentsFromSettings(availableCivs, expectedCount);
		} finally {
			C7Settings.settings = previousSettings;
		}
	}

	[Fact]
	public void OpponentsFromSettingsPadsMissingEntriesWithRandom() {
		List<SelectedOpponent> opponents = OpponentsFromStored("", 3);

		Assert.Equal(3, opponents.Count);
		Assert.All(opponents, opponent => Assert.True(opponent.isRandom));
	}

	[Fact]
	public void OpponentsFromSettingsRestoresStoredSelections() {
		List<SelectedOpponent> opponents = OpponentsFromStored("Rome|NONE|Random|Germany", 3);

		// Parsed in stored order, truncated to the expected count.
		Assert.Equal(3, opponents.Count);
		Assert.Equal("Rome", opponents[0].Name);
		Assert.True(opponents[1].isNone);
		Assert.True(opponents[2].isRandom);

		// Short stored lists are padded with random.
		List<SelectedOpponent> padded = OpponentsFromStored("NONE", 2);
		Assert.True(padded[0].isNone);
		Assert.True(padded[1].isRandom);
	}

	[Fact]
	public void OpponentsFromSettingsTurnsUnknownCivsIntoRandom() {
		List<SelectedOpponent> opponents = OpponentsFromStored("Atlantis|NONE", 2);

		Assert.True(opponents[0].isRandom);
		Assert.True(opponents[1].isNone);
	}
}
