using System;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GameDataTest : RemoteSaveLoader, IClassFixture<SaveGameFixture> {
	private const string SAVES_FOLDER = "saves/game-data";
	C7GameData.GameData gameData;
	private BehaviorEngine behaviorEngine;

	public GameDataTest(SaveGameFixture fixture) {
		this.behaviorEngine = fixture.behaviors;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);

		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[SkippableFact]
	public async void SeedInGameData_SAV() {
		if (Civ3TestData.ShouldSkipCiv3DependentTests()) {
			return;
		}

		string saveName = "Conquests 16 Players.SAV";
		string uri = "https://www.dropbox.com/scl/fi/gmxbx1mtrammzfc6vly1g/Conquests-16-Players.SAV?rlkey=2z1es5aetqva4ymv59qduq1at&st=d0udmb3w&dl=1";

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(saveName, SAVES_FOLDER, uri);

		C7GameData.GameData gd = game.ToGameData(behaviorEngine);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(33127520, game.Seed);
		Assert.Equal(33127520, gd.seed);
	}

	[SkippableFact]
	public async void SeedInGameData_JSON() {
		if (Civ3TestData.ShouldSkipCiv3DependentTests()) {
			return;
		}

		string saveName = "Conquests 16 Players.json";
		string uri = "https://www.dropbox.com/scl/fi/g1qxuvc6xptg1l6hx9s21/Conquests-16-Players.json?rlkey=bkq158od7469pibhtw44g04if&st=e03xrz3g&dl=1";

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(saveName, SAVES_FOLDER, uri);

		C7GameData.GameData gd = game.ToGameData(behaviorEngine);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(-1, game.Seed);
		Assert.NotEqual(-1, gd.seed);
	}

	[Fact]
	public void SeedInGameData_Default() {
		C7GameData.GameData gd = new C7GameData.GameData();
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.NotEqual(-1, gd.seed);
	}

	[Fact]
	public void SeedInGameData_Custom() {
		C7GameData.GameData gd = new C7GameData.GameData(654972132);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(654972132, gd.seed);
	}
}
