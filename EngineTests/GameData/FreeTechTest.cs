using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class FreeTechTest : IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;

	public FreeTechTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	private Player HumanPlayer() {
		Player player = gameData.players.First(p => p.isHuman);
		player.knownTechs.Clear();
		player.cities.Clear();
		return player;
	}

	// A prerequisite-free tech, so research can point at it directly.
	private Tech BasicTech() {
		return gameData.techs.First(t => t.Prerequisites.Count == 0);
	}

	[Fact]
	public void SettingResearchDoesNotSpendAFreeTech() {
		Player player = HumanPlayer();
		player.freeTechsRemaining = 1;
		Tech tech = BasicTech();

		player.SetCurrentlyResearchedTech(tech.id);

		Assert.Equal(tech.id, player.currentlyResearchedTech);
		Assert.Equal(1, player.freeTechsRemaining);
		Assert.DoesNotContain(tech.id, player.knownTechs);
	}

	[Fact]
	public void SettingResearchWithNoFreeTechIsUnchanged() {
		Player player = HumanPlayer();
		player.freeTechsRemaining = 0;
		Tech tech = BasicTech();

		player.SetCurrentlyResearchedTech(tech.id);

		Assert.Equal(tech.id, player.currentlyResearchedTech);
		Assert.Equal(0, player.freeTechsRemaining);
	}

	[Fact]
	public void ClaimFreeTechGrantsTheTechAndKeepsCurrentResearch() {
		Player player = HumanPlayer();
		Tech current = BasicTech();
		Tech free = gameData.techs.First(t => t.id != current.id && t.Prerequisites.Count == 0);

		player.SetCurrentlyResearchedTech(current.id);
		player.freeTechsRemaining = 1;

		Assert.True(player.ClaimFreeTech(gameData, free));

		Assert.Contains(free.id, player.knownTechs);
		Assert.Equal(0, player.freeTechsRemaining);
		Assert.Equal(current.id, player.currentlyResearchedTech);
	}

	[Fact]
	public void ClaimFreeTechSpendsOnlyOneOfSeveral() {
		Player player = HumanPlayer();
		player.freeTechsRemaining = 2;
		Tech free = BasicTech();

		Assert.True(player.ClaimFreeTech(gameData, free));
		Assert.Equal(1, player.freeTechsRemaining);
	}

	[Fact]
	public void ClaimFreeTechFailsWithNoneRemaining() {
		Player player = HumanPlayer();
		player.freeTechsRemaining = 0;
		Tech free = BasicTech();

		Assert.False(player.ClaimFreeTech(gameData, free));
		Assert.DoesNotContain(free.id, player.knownTechs);
	}

	[Fact]
	public void ClaimFreeTechRejectsTechsAlreadyKnown() {
		Player player = HumanPlayer();
		Tech free = BasicTech();
		player.knownTechs.Add(free.id);
		player.freeTechsRemaining = 1;

		Assert.False(player.ClaimFreeTech(gameData, free));
		Assert.Equal(1, player.freeTechsRemaining);
	}

	[Fact]
	public void ClaimFreeTechRejectsNull() {
		Player player = HumanPlayer();
		player.freeTechsRemaining = 1;

		Assert.False(player.ClaimFreeTech(gameData, null));
		Assert.Equal(1, player.freeTechsRemaining);
	}

	[Fact]
	public void ClaimingSeveralFreeTechsInARowWorks() {
		// Guards against a claim re-entering the prompt and looping.
		Player player = HumanPlayer();
		player.freeTechsRemaining = 3;

		Tech[] techs = gameData.techs.Where(t => t.Prerequisites.Count == 0).Take(3).ToArray();
		Assert.Equal(3, techs.Length);

		foreach (Tech tech in techs) {
			Assert.True(player.ClaimFreeTech(gameData, tech));
		}

		Assert.Equal(0, player.freeTechsRemaining);
		Assert.Equal(3, player.knownTechs.Count);
	}

	[Fact]
	public void AnAIResearchingSpendsItsFreeTechs() {
		Player player = HumanPlayer();
		player.isHuman = false;
		player.freeTechsRemaining = 1;

		PlayerAI.MaybePickTechToResearch(player, gameData.techs);

		Assert.Equal(0, player.freeTechsRemaining);
		Assert.NotEmpty(player.knownTechs);
	}

	[Fact]
	public void FreeTechsSurviveASaveAndLoad() {
		Player player = HumanPlayer();
		Tech current = BasicTech();
		player.SetCurrentlyResearchedTech(current.id);
		player.freeTechsRemaining = 2;
		player.beakers = 7;
		player.turnsResearched = 1;

		SavePlayer saved = new(player);
		Assert.Equal(2, saved.freeTechsRemaining);

		Player loaded = saved.ToPlayer(
			gameData.map,
			gameData.players.Select(p => p.civilization).ToList(),
			gameData.governments,
			gameData.techs,
			gameData.rules,
			new HashSet<Alliance>());
		Assert.Equal(2, loaded.freeTechsRemaining);
		Assert.Equal(current.id, loaded.currentlyResearchedTech);
		// Loading must not spend the free tech or wipe research progress.
		Assert.Equal(7, loaded.beakers);
		Assert.Equal(1, loaded.turnsResearched);
	}

	[Fact]
	public void ClaimingTheTechBeingResearchedStopsResearchingIt() {
		Player player = HumanPlayer();
		Tech researching = BasicTech();
		player.CalculateFreshTechQueueAndAssignNewCurrent(researching);
		player.freeTechsRemaining = 1;

		Assert.True(player.ClaimFreeTech(gameData, researching));

		Assert.Contains(researching.id, player.knownTechs);
		Assert.Null(player.currentlyResearchedTech);
		Assert.DoesNotContain(researching, player.ResearchQueue);
	}

	[Fact]
	public void TradingForTechsKeepsCurrentResearch() {
		Player player = HumanPlayer();
		Tech researching = BasicTech();
		Tech traded = gameData.techs.First(t => t.id != researching.id && t.Prerequisites.Count == 0);
		Player other = gameData.players.First(p => !p.isHuman);

		player.CalculateFreshTechQueueAndAssignNewCurrent(researching);

		TradeOffer ourOffer = new();
		TradeOffer theirOffer = new();
		theirOffer.techs.Add(traded);
		player.ExecuteDeal(gameData, other, theirOffer, ourOffer);

		Assert.Contains(traded.id, player.knownTechs);
		Assert.Equal(researching.id, player.currentlyResearchedTech);
		Assert.Contains(researching, player.ResearchQueue);
	}

	[Fact]
	public void TradingForTheTechBeingResearchedStopsResearchingIt() {
		Player player = HumanPlayer();
		Tech researching = BasicTech();
		Player other = gameData.players.First(p => !p.isHuman);

		player.CalculateFreshTechQueueAndAssignNewCurrent(researching);

		TradeOffer theirOffer = new();
		theirOffer.techs.Add(researching);
		player.ExecuteDeal(gameData, other, theirOffer, new TradeOffer());

		Assert.Contains(researching.id, player.knownTechs);
		Assert.Null(player.currentlyResearchedTech);
		Assert.DoesNotContain(researching, player.ResearchQueue);
	}

	[Fact]
	public void QueuingTheFirstTechStartsResearchingIt() {
		Player player = HumanPlayer();
		Tech tech = BasicTech();

		player.CalculateTechQueueAndAppendToCurrentQueue(tech);

		Assert.Equal(tech.id, player.currentlyResearchedTech);
		Assert.Contains(tech, player.ResearchQueue);
	}

	[Fact]
	public void QueuingTechsKeepsTheExistingResearchTarget() {
		Player player = HumanPlayer();
		Tech researching = BasicTech();
		Tech queued = gameData.techs.First(t => t.id != researching.id && t.Prerequisites.Count == 0);

		player.CalculateFreshTechQueueAndAssignNewCurrent(researching);
		player.CalculateTechQueueAndAppendToCurrentQueue(queued);

		Assert.Equal(researching.id, player.currentlyResearchedTech);
		Assert.Contains(queued, player.ResearchQueue);
	}

	[Fact]
	public void ClaimingTheQueuedTechMakesTheNextQueuedTechCurrent() {
		Player player = HumanPlayer();
		Tech first = BasicTech();
		Tech second = gameData.techs.First(t => t.id != first.id && t.Prerequisites.Count == 0);
		player.freeTechsRemaining = 2;

		player.CalculateFreshTechQueueAndAssignNewCurrent(first);
		player.CalculateTechQueueAndAppendToCurrentQueue(second);

		Assert.Equal(first.id, player.currentlyResearchedTech);

		Assert.True(player.ClaimFreeTech(gameData, first));

		Assert.Contains(first.id, player.knownTechs);
		Assert.DoesNotContain(first, player.ResearchQueue);
		Assert.Contains(second, player.ResearchQueue);
		// The next tech in the queue should take over as the current research.
		Assert.Equal(second.id, player.currentlyResearchedTech);
	}

	[Fact]
	public void AvailableTechsAreOrderedCheapestFirst() {
		Player player = HumanPlayer();
		player.knownTechs.Clear();

		List<Tech> available = player.GetAvailableTechsToResearch(gameData.techs);
		Assert.NotEmpty(available);

		// The popup uses the first entry as its default, so ordering must be stable.
		List<int> costs = available.Select(t => t.Cost).ToList();
		Assert.Equal(costs.OrderBy(c => c).ToList(), costs);
	}
}
