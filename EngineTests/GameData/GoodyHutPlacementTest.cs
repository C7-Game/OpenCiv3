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

	private static (SaveGame save, MapGeneratorData mgd) CreateWorld(BarbarianActivity activity, int seed = Seed) {
		GameMode gameMode = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3"));
		SaveGame save = gameMode.GetSave();

		save.Seed = seed;
		save.BarbarianInfo.barbarianActivity = activity;
		save.WorldCharacteristics = new WorldCharacteristics() {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			worldSize = new WorldSize() {
				width = 60,
				height = 60,
				numberOfCivs = 4,
				distanceBetweenCivs = 8,
				techRate = 240,
				optimalNumberOfCities = 20,
			},
		};

		EngineStorage.behaviors = gameMode.behaviors;
		return (save, new MapGeneratorData(save));
	}

	private static List<Tile> Huts(GameMap map) {
		return map.tiles.Where(t => t.HasGoodyHut()).ToList();
	}

	[Fact]
	public void HutsFollowCiv3SiteRules() {
		(_, MapGeneratorData mgd) = CreateWorld(BarbarianActivity.Roaming);
		GameMap map = MapGenerator.GenerateMap(mgd);

		List<Tile> huts = Huts(map);

		Assert.NotEmpty(huts);
		// One attempt per 32 tiles, never retried, so that is the cap.
		Assert.True(huts.Count <= map.tiles.Count / 32);

		foreach (Tile hut in huts) {
			Assert.True(hut.IsLand());
			Assert.False(hut.HasResource());
			Assert.False(hut.HasBarbarianCamp());
			Assert.DoesNotContain(hut, map.startingLocations);

			// No other hut in the surrounding 7x7 box.
			Assert.DoesNotContain(hut.GetTilesWithinTileSquare(3), other => other != hut && other.HasGoodyHut());
		}
	}

	[Fact]
	public void SameSeedPlacesSameHuts() {
		(_, MapGeneratorData first) = CreateWorld(BarbarianActivity.Roaming);
		(_, MapGeneratorData second) = CreateWorld(BarbarianActivity.Roaming);

		List<(int, int)> firstHuts = Huts(MapGenerator.GenerateMap(first)).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();
		List<(int, int)> secondHuts = Huts(MapGenerator.GenerateMap(second)).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();

		Assert.NotEmpty(firstHuts);
		Assert.Equal(firstHuts, secondHuts);
	}

	[Fact]
	public void NoBarbariansMeansNoHuts() {
		(_, MapGeneratorData mgd) = CreateWorld(BarbarianActivity.None);

		Assert.Empty(Huts(MapGenerator.GenerateMap(mgd)));
	}

	[Fact]
	public void SedentaryBarbariansStillGetHuts() {
		(_, MapGeneratorData mgd) = CreateWorld(BarbarianActivity.Sedentary);

		Assert.NotEmpty(Huts(MapGenerator.GenerateMap(mgd)));
	}

	[Fact]
	public void NoBehaviorsSkipsHutPlacement() {
		(_, MapGeneratorData mgd) = CreateWorld(BarbarianActivity.Roaming);
		BehaviorEngine loaded = EngineStorage.behaviors;
		EngineStorage.behaviors = null;

		try {
			Assert.Empty(Huts(MapGenerator.GenerateMap(mgd)));
		} finally {
			EngineStorage.behaviors = loaded;
		}
	}

	[Fact]
	public void HutsSurviveSaveAndLoad() {
		(SaveGame save, _) = CreateWorld(BarbarianActivity.Roaming);

		GameSetup gameSetup = new() {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = save.WorldCharacteristics,
			opponents = [new SelectedOpponent { isRandom = true }],
			victoryConditions = new VictoryConditions(),
		};
		gameSetup.Populate(save);

		List<(int, int)> savedHuts = save.Map.tiles
			.Where(t => t.overlays.Contains(Tile.TileOverlays.GOODY_HUT))
			.Select(t => (t.X, t.Y))
			.ToList();
		Assert.NotEmpty(savedHuts);

		C7GameData.GameData gameData = save.ToGameData(EngineStorage.behaviors);
		List<(int, int)> loadedHuts = Huts(gameData.map).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();

		Assert.Equal(savedHuts.OrderBy(h => h), loadedHuts.OrderBy(h => h));

		// Loading swaps the map generator's stand-in for the ruleset's hut.
		TerrainImprovement rulesetHut = gameData.terrainImprovements.Single(ti => ti.layer == TerrainImprovement.Layer.GoodyHut);
		Assert.All(Huts(gameData.map), t => Assert.Same(rulesetHut, t.overlays.ImprovementAtLayer(TerrainImprovement.Layer.GoodyHut)));
	}
}
