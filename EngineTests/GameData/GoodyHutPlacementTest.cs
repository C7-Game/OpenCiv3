using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GoodyHutPlacementTest {
	private const int Seed = 4242;

	private static (SaveGame save, WorldCharacteristics wc) CreateWorld(BarbarianActivity activity, int seed = Seed) {
		GameMode gameMode = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3"));
		SaveGame save = gameMode.GetSave();

		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = activity,
			worldSize = new WorldSize() {
				width = 60,
				height = 60,
				numberOfCivs = 4,
				distanceBetweenCivs = 8,
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = seed,
			behaviors = gameMode.behaviors,
		};

		return (save, wc);
	}

	private static List<Tile> Huts(GameMap map) {
		return map.tiles.Where(t => t.hasGoodyHut).ToList();
	}

	[Fact]
	public void HutsFollowCiv3SiteRules() {
		(_, WorldCharacteristics wc) = CreateWorld(BarbarianActivity.Roaming);
		GameMap map = MapGenerator.GenerateMap(wc);

		List<Tile> huts = Huts(map);

		Assert.NotEmpty(huts);
		// One attempt per 32 tiles, never retried, so that is the cap.
		Assert.True(huts.Count <= map.tiles.Count / 32);

		foreach (Tile hut in huts) {
			Assert.True(hut.IsLand());
			Assert.False(hut.HasResource());
			Assert.False(hut.hasBarbarianCamp);
			Assert.DoesNotContain(hut, map.startingLocations);

			// No other hut in the surrounding 7x7 box.
			Assert.DoesNotContain(hut.GetTilesWithinTileSquare(3), other => other != hut && other.hasGoodyHut);
		}
	}

	[Fact]
	public void SameSeedPlacesSameHuts() {
		(_, WorldCharacteristics first) = CreateWorld(BarbarianActivity.Roaming);
		(_, WorldCharacteristics second) = CreateWorld(BarbarianActivity.Roaming);

		List<(int, int)> firstHuts = Huts(MapGenerator.GenerateMap(first)).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();
		List<(int, int)> secondHuts = Huts(MapGenerator.GenerateMap(second)).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();

		Assert.NotEmpty(firstHuts);
		Assert.Equal(firstHuts, secondHuts);
	}

	[Fact]
	public void NoBarbariansMeansNoHuts() {
		(_, WorldCharacteristics wc) = CreateWorld(BarbarianActivity.None);

		Assert.Empty(Huts(MapGenerator.GenerateMap(wc)));
	}

	[Fact]
	public void SedentaryBarbariansStillGetHuts() {
		(_, WorldCharacteristics wc) = CreateWorld(BarbarianActivity.Sedentary);

		Assert.NotEmpty(Huts(MapGenerator.GenerateMap(wc)));
	}

	[Fact]
	public void NoBehaviorsSkipsHutPlacement() {
		(_, WorldCharacteristics wc) = CreateWorld(BarbarianActivity.Roaming);
		wc.behaviors = null;

		Assert.Empty(Huts(MapGenerator.GenerateMap(wc)));
	}

	[Fact]
	public void HutsSurviveSaveAndLoad() {
		(SaveGame save, WorldCharacteristics wc) = CreateWorld(BarbarianActivity.Roaming);

		GameSetup gameSetup = new() {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = [new SelectedOpponent { isRandom = true }],
			victoryConditions = new VictoryConditions(),
		};
		gameSetup.Populate(save);

		List<(int, int)> savedHuts = save.Map.tiles
			.Where(t => t.features.Contains(Tile.TileOverlays.GOODY_HUT))
			.Select(t => (t.X, t.Y))
			.ToList();
		Assert.NotEmpty(savedHuts);

		C7GameData.GameData gameData = save.ToGameData(wc.behaviors);
		List<(int, int)> loadedHuts = Huts(gameData.map).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();

		Assert.Equal(savedHuts.OrderBy(h => h), loadedHuts.OrderBy(h => h));
	}
}
