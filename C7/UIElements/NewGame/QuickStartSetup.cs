using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7Engine;
using C7Engine.Lua;
using Serilog;

public partial class QuickStartSetup : Node {
	private static ILogger log = LogManager.ForContext<ScenarioSetup>();

	public static void Init(GlobalSingleton global) {
		log.Information("Setting up a QuickStart game");

		global.ResetLoadGameFields();

		var game = GameMode.Load(GamePaths.GameModesDir, GamePaths.GameMode);
		var save = game.GetSave();
		WorldSize worldSize = GameSetup.WorldSizeFromSettings(save.WorldSizes);

		global.WorldCharacteristics = new WorldCharacteristics(save) {
			barbarianActivity = GameSetup.BarbarianActivityFromSettings(),
			landform = GameSetup.LandformFromSettings(),
			oceanCoverage = GameSetup.OceanCoverageFromSettings(),
			age = GameSetup.AgeFromSettings(),
			climate = GameSetup.ClimateFromSettings(),
			temperature = GameSetup.TemperatureFromSettings(),
			worldSize = worldSize,
			mapSeed = new Random().Next(),
		};

		global.SaveGame = save;

		Civilization player = GetPlayerCivilization(save.Civilizations);
		Difficulty difficulty = GetDifficulty(save.Difficulties);

		List<SelectedOpponent> opponents = GameSetup.OpponentsFromSettings(save.Civilizations, worldSize.numberOfCivs - 1);

		GameSetup gameSetup = new() {
			playerCivilization = player,
			difficulty = difficulty,
			worldCharacteristics = global.WorldCharacteristics,
			opponents = opponents,
			victoryConditions = VictoryConditions.FromSettings()
		};

		gameSetup.Populate(save);
	}

	private static Civilization GetPlayerCivilization(List<Civilization> civilizations) {
		string lastCiv = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Civilization, "Netherlands");
		return civilizations.FirstOrDefault(civ => civ.name == lastCiv) ?? civilizations.First();
	}

	private static Difficulty GetDifficulty(List<Difficulty> difficulties) {
		string lastDiff = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Difficulty, "Regent");
		return difficulties.FirstOrDefault(diff => diff.Name == lastDiff) ?? difficulties.First();
	}

}
