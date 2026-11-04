using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Dispersing a barbarian camp pays the gold set by the rules (#1047).
public class BarbarianCampTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public BarbarianCampTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// A copy, so that changing the rules here doesn't leak into the fixture's save.
		gameData = fixture.saveGame.Clone().ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
	}

	// Gives the player a warrior at its start position (only combat units can enter a
	// camp), puts a barbarian camp on an empty neighboring tile and returns the
	// warrior, the camp and the direction to walk into it.
	private (MapUnit unit, Tile camp, TileDirection direction) PlaceCampNextTo(Player player) {
		Tile start = player.units[0].location;
		gameData.SpawnUnit(player, gameData.unitPrototypes.Find(u => u.name == "Warrior"), start);
		MapUnit warrior = player.units.Last();

		foreach (KeyValuePair<TileDirection, Tile> neighbor in start.neighbors) {
			Tile tile = neighbor.Value;
			if (tile.unitsOnTile.Count > 0 || tile.HasCity() || !warrior.CanEnter(tile))
				continue;
			tile.hasBarbarianCamp = true;
			gameData.map.barbarianCamps.Add(tile);
			return (warrior, tile, neighbor.Key);
		}
		throw new System.Exception($"{player.civilization.name} has no empty land next to its start");
	}

	private static List<MsgBarbarianCampDispersed> CampMessages() =>
		EngineStorage.messagesToUI.OfType<MsgBarbarianCampDispersed>().ToList();

	[Fact]
	public void RulesetDefinesCampGold() {
		// The value must come from ruleset.json, not just the Rules default.
		using JsonDocument ruleset = JsonDocument.Parse(File.ReadAllText(Path.Combine(PathUtils.GameModesDir, "civ3", "ruleset.json")));
		int rulesetGold = ruleset.RootElement.GetProperty("rules").GetProperty("barbarianCampGold").GetInt32();

		Assert.Equal(25, rulesetGold);
		Assert.Equal(rulesetGold, fixture.saveGame.Rules.BarbarianCampGold);
		Assert.Equal(rulesetGold, gameData.rules.BarbarianCampGold);
	}

	[Fact]
	public void SavesWithoutTheRuleGetTheDefault() {
		// Games saved before the rule existed have no barbarianCampGold in their JSON.
		string path = Path.Combine(Path.GetTempPath(), "barbarian-camp-old-save.json");
		fixture.saveGame.Save(path);
		string json = File.ReadAllText(path);
		Assert.Contains("\"barbarianCampGold\"", json);
		File.WriteAllText(path, Regex.Replace(json, "\"barbarianCampGold\":\\s*\\d+,?", ""));

		SaveGame oldSave = SaveGame.Load(path, s => s, s => s);
		File.Delete(path);

		Assert.Equal(25, oldSave.Rules.BarbarianCampGold);
	}

	[Theory]
	[InlineData(25)]
	[InlineData(40)]
	[InlineData(0)]
	public async Task HumanDispersingACampEarnsTheRuleGoldAndIsTold(int campGold) {
		gameData.rules.BarbarianCampGold = campGold;
		Player human = gameData.players.First(p => p.isHuman);
		(MapUnit unit, Tile camp, TileDirection direction) = PlaceCampNextTo(human);
		int goldBefore = human.gold;

		Assert.True(await unit.Move(direction));

		Assert.Equal(camp, unit.location);
		Assert.False(camp.hasBarbarianCamp);
		Assert.DoesNotContain(camp, gameData.map.barbarianCamps);
		Assert.Equal(goldBefore + campGold, human.gold);

		MsgBarbarianCampDispersed message = Assert.Single(CampMessages());
		Assert.Equal(human.id, message.controllerId);
		Assert.Equal(campGold, message.goldEarned);
	}

	[Fact]
	public async Task ComputerDispersingACampEarnsTheRuleGoldWithoutAPopup() {
		gameData.rules.BarbarianCampGold = 40;
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians && p.units.Count > 0);
		(MapUnit unit, Tile camp, TileDirection direction) = PlaceCampNextTo(ai);
		int goldBefore = ai.gold;

		Assert.True(await unit.Move(direction));

		Assert.False(camp.hasBarbarianCamp);
		Assert.Equal(goldBefore + 40, ai.gold);
		Assert.Empty(CampMessages());
	}
}
