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
		WorldSize worldSize = GetWorldSize(save.WorldSizes);

		global.WorldCharacteristics = new WorldCharacteristics(save) {
			barbarianActivity = GetBarbarianActivity(),
			landform = GetLandform(),
			oceanCoverage = GetOceanCoverage(),
			age = GetAge(),
			climate = GetClimate(),
			temperature = GetTemperature(),
			worldSize = worldSize,
			mapSeed = new Random().Next(),
		};

		global.SaveGame = save;

		Civilization player = GetPlayerCivilization(save.Civilizations);
		Difficulty difficulty = GetDifficulty(save.Difficulties);

		List<SelectedOpponent> opponents = GetOpponents(save.Civilizations, worldSize.numberOfCivs - 1);

		GameSetup gameSetup = new() {
			playerCivilization = player,
			difficulty = difficulty,
			worldCharacteristics = global.WorldCharacteristics,
			opponents = opponents,
			victoryConditions = new VictoryConditions() // TODO: preferred victory conditions
		};

		gameSetup.Populate(save);
	}

	private static BarbarianActivity GetBarbarianActivity() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.BarbarianActivity, BarbarianActivity.Roaming);
	}

	private static WorldCharacteristics.Landform GetLandform() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Landform, WorldCharacteristics.Landform.Pangaea);
	}

	private static WorldCharacteristics.OceanCoverage GetOceanCoverage() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.OceanCoverage, WorldCharacteristics.OceanCoverage.Percent_70);
	}

	private static WorldCharacteristics.Age GetAge() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Age, WorldCharacteristics.Age.Billion_4);
	}

	private static WorldCharacteristics.Climate GetClimate() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Climate, WorldCharacteristics.Climate.Normal);
	}

	private static WorldCharacteristics.Temperature GetTemperature() {
		return C7Settings.GetTypedSettingOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Temperature, WorldCharacteristics.Temperature.Temperate);
	}

	private static WorldSize GetWorldSize(List<WorldSize> availableSizes) {
		string lastWorldSize = C7Settings.GetSettingValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.WorldSize);
		return availableSizes.FirstOrDefault(ws =>
				   string.Equals(ws.name, lastWorldSize, StringComparison.OrdinalIgnoreCase))
			   ?? availableSizes.FirstOrDefault(ws => ws.isDefault)
			   ?? WorldSize.Generic();
	}

	private static Civilization GetPlayerCivilization(List<Civilization> civilizations) {
		string lastCiv = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Civilization, "Netherlands");
		return civilizations.FirstOrDefault(civ => civ.name == lastCiv) ?? civilizations.First();
	}

	private static Difficulty GetDifficulty(List<Difficulty> difficulties) {
		string lastDiff = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Difficulty, "Regent");
		return difficulties.FirstOrDefault(diff => diff.Name == lastDiff) ?? difficulties.First();
	}

	private static List<SelectedOpponent> GetOpponents(List<Civilization> availableCivilizations, int expectedCount) {
		string opsRaw = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.Opponents, "");
		List<SelectedOpponent> opponents = [];

		if (!string.IsNullOrEmpty(opsRaw)) {
			var cleanNames = opsRaw.Split('|')
				.Select(n => n.Trim())
				.Where(n => !string.IsNullOrEmpty(n));

			opponents.AddRange(cleanNames.Select(name => ToSelectedOpponent(name, availableCivilizations)));
		}

		while (opponents.Count < expectedCount) {
			opponents.Add(new SelectedOpponent { isRandom = true });
		}

		return opponents.Take(expectedCount).ToList();
	}

	private static SelectedOpponent ToSelectedOpponent(string name, List<Civilization> availableCivs) {
		if (name == "Random" || availableCivs.All(c => c.name != name)) {
			return new SelectedOpponent { isRandom = true };
		}

		return new SelectedOpponent { isRandom = false, Name = name };
	}

}
