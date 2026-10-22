using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Barbarians ransacking a city take the gold given by the Lua behavior
// gameplay.barbarians.ransack_gold (#1048).
public class BarbarianRansackTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private C7GameData.GameData gameData;

	public BarbarianRansackTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
	}

	private void StartGame(BehaviorEngine behaviors) {
		gameData = fixture.saveGame.Clone().ToGameData(behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	// Loads the civ3 game mode plus an addon that replaces the ransack formula
	// with `return <luaExpression>`, the way a mod would.
	private static BehaviorEngine BehaviorsWithRansackFormula(string luaExpression) {
		string addonDir = Path.Combine(Path.GetTempPath(), "openciv3-ransack-test-addon");
		Directory.CreateDirectory(addonDir);
		File.WriteAllText(Path.Combine(addonDir, "behaviors.lua"),
			"return function(behaviors)\n" +
			"  behaviors.gameplay.barbarians.ransack_gold = function(barbarian, city)\n" +
			$"    return {luaExpression}\n" +
			"  end\n" +
			"  return behaviors\n" +
			"end\n");
		try {
			return GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3", [addonDir])).behaviors;
		} finally {
			Directory.Delete(addonDir, true);
		}
	}

	// Founds a city for the human player on an empty tile away from its start, puts a
	// barbarian warrior next to it and returns the warrior, the city and the direction
	// from the warrior to the city.
	private (MapUnit barbarian, City city, TileDirection direction) SetUpRaid(int ownerGold) {
		Player human = gameData.players.First(p => p.isHuman);
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		UnitPrototype warrior = gameData.unitPrototypes.Find(u => u.name == "Warrior");
		Tile start = human.units[0].location;

		static bool IsFree(Tile t) => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity();
		foreach (Tile cityTile in start.GetTilesWithinRankDistance(3).Where(t => t.DistanceTo(start) >= 2 && IsFree(t))) {
			foreach (KeyValuePair<TileDirection, Tile> neighbor in cityTile.neighbors) {
				Tile from = neighbor.Value;
				if (!IsFree(from) || from.DistanceTo(start) < 2)
					continue;

				City city = CityInteractions.BuildCity(cityTile, human, "Ransackville");
				human.gold = ownerGold;
				gameData.SpawnUnit(barbarians, warrior, from);
				MapUnit barbarian = barbarians.units.Last();
				TileDirection towardsCity = from.neighbors.First(n => n.Value == cityTile).Key;
				return (barbarian, city, towardsCity);
			}
		}
		throw new System.Exception("no free spot for a city and a barbarian near the human start");
	}

	private static List<MsgCityRansacked> RansackMessages() =>
		EngineStorage.messagesToUI.OfType<MsgCityRansacked>().ToList();

	[Theory]
	[InlineData(100, 25)]
	[InlineData(7, 1)] // rounds down
	[InlineData(0, 0)]
	public async Task DefaultFormulaTakesAQuarterOfTheOwnersGold(int ownerGold, int expectedTaken) {
		StartGame(fixture.behaviors);
		(MapUnit barbarian, City city, TileDirection direction) = SetUpRaid(ownerGold);

		await barbarian.Move(direction);

		Assert.Equal(ownerGold - expectedTaken, city.owner.gold);
		MsgCityRansacked message = Assert.Single(RansackMessages());
		Assert.Equal(city, message.city);
		Assert.Equal(expectedTaken, message.goldLiberated);
		// The barbarian is used up and the city survives.
		Assert.DoesNotContain(barbarian, city.location.unitsOnTile);
		Assert.Contains(city, city.owner.cities);
	}

	[Theory]
	// The formula gets both the barbarian and the city: 10 per citizen (1) + the warrior's attack (1).
	[InlineData("10 * #city.residents + barbarian.unitType.attack", 100, 11)]
	// Results are kept between 0 and the owner's gold.
	[InlineData("1000", 100, 100)]
	[InlineData("-5", 100, 0)]
	public async Task GameModesCanReplaceTheFormula(string luaExpression, int ownerGold, int expectedTaken) {
		StartGame(BehaviorsWithRansackFormula(luaExpression));
		(MapUnit barbarian, City city, TileDirection direction) = SetUpRaid(ownerGold);

		await barbarian.Move(direction);

		Assert.Equal(ownerGold - expectedTaken, city.owner.gold);
		Assert.Equal(expectedTaken, Assert.Single(RansackMessages()).goldLiberated);
	}
}
