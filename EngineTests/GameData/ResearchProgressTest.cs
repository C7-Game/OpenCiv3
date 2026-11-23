using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class ResearchProgressTest : IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;

	public ResearchProgressTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	private Player HumanPlayer() {
		Player player = gameData.players.First(p => p.isHuman);
		player.knownTechs.Clear();
		player.cities.Clear();
		return player;
	}

	// Two prerequisite-free techs, so research can point at either directly.
	private (Tech, Tech) TwoBasicTechs() {
		List<Tech> basic = gameData.techs.Where(t => t.Prerequisites.Count == 0).Take(2).ToList();
		return (basic[0], basic[1]);
	}

	private Player SaveAndLoad(SavePlayer saved) {
		return saved.ToPlayer(
			gameData.map,
			gameData.players.Select(p => p.civilization).ToList(),
			gameData.governments,
			gameData.techs,
			gameData.rules,
			new HashSet<Alliance>());
	}

	[Fact]
	public void SwitchingResearchKeepsProgress() {
		Player player = HumanPlayer();
		(Tech first, Tech second) = TwoBasicTechs();

		player.SetCurrentlyResearchedTech(first.id);
		player.AddBeakers(first.id, 10);
		player.SetCurrentlyResearchedTech(second.id);
		player.SetCurrentlyResearchedTech(first.id);

		Assert.Equal(10, player.GetBeakers(first.id));
	}

	[Fact]
	public void GainingATechClearsItsProgress() {
		Player player = HumanPlayer();
		(Tech tech, _) = TwoBasicTechs();
		player.AddBeakers(tech.id, 10);
		player.freeTechsRemaining = 1;

		Assert.True(player.ClaimFreeTech(gameData, tech));

		Assert.False(player.ResearchProgressByTech.ContainsKey(tech.id));
	}

	[Fact]
	public void BeakersWithNoTechSelectedAreDropped() {
		Player player = HumanPlayer();

		player.AddBeakers(null, 5);

		Assert.Empty(player.ResearchProgressByTech);
		Assert.Equal(0, player.GetBeakers(null));
	}

	[Fact]
	public void TechCostCountsProgressOnTechsNotBeingResearched() {
		Player player = HumanPlayer();
		(Tech current, Tech other) = TwoBasicTechs();
		player.SetCurrentlyResearchedTech(current.id);
		int fullCost = gameData.TechCostFor(other, player);

		player.AddBeakers(other.id, 5);

		Assert.Equal(System.Math.Max(fullCost - 5, 0), gameData.TechCostFor(other, player));
	}

	[Fact]
	public void ProgressOnEveryTechSurvivesASaveAndLoad() {
		Player player = HumanPlayer();
		(Tech first, Tech second) = TwoBasicTechs();
		player.SetCurrentlyResearchedTech(first.id);
		player.SetResearchProgress(first.id, new Player.ResearchProgress { beakers = 7, turns = 1 });
		player.SetResearchProgress(second.id, new Player.ResearchProgress { beakers = 3, turns = 4 });

		Player loaded = SaveAndLoad(new SavePlayer(player));

		Assert.Equal(7, loaded.GetBeakers(first.id));
		Assert.Equal(1, loaded.GetTurnsResearched(first.id));
		Assert.Equal(3, loaded.GetBeakers(second.id));
		Assert.Equal(4, loaded.GetTurnsResearched(second.id));
	}

	[Fact]
	public void ProgressKeyedByIdRoundTripsThroughJson() {
		(Tech first, Tech second) = TwoBasicTechs();
		Dictionary<ID, Player.ResearchProgress> progress = new() {
			[first.id] = new Player.ResearchProgress { beakers = 7, turns = 1 },
			[second.id] = new Player.ResearchProgress { beakers = 3, turns = 4 },
		};
		// The settings SaveGame uses that matter here.
		JsonSerializerOptions options = new() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			IncludeFields = true,
			Converters = { new IDJsonConverter() },
		};

		string json = JsonSerializer.Serialize(progress, options);
		Dictionary<ID, Player.ResearchProgress> loaded =
			JsonSerializer.Deserialize<Dictionary<ID, Player.ResearchProgress>>(json, options);

		Assert.Equal(progress, loaded);
	}

	[Fact]
	public void OldSaveProgressLoadsOntoTheCurrentTech() {
		Player player = HumanPlayer();
		(Tech current, _) = TwoBasicTechs();
		player.SetCurrentlyResearchedTech(current.id);
		// An older save: progress only in the single beakers/turns fields.
		SavePlayer saved = new(player) { beakers = 7, turnsResearched = 2 };

		Player loaded = SaveAndLoad(saved);

		Assert.Single(loaded.ResearchProgressByTech);
		Assert.Equal(7, loaded.GetBeakers(current.id));
		Assert.Equal(2, loaded.GetTurnsResearched(current.id));
	}
}
