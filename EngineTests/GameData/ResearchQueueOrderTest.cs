using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class ResearchQueueOrderTest : IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;

	public ResearchQueueOrderTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	private Player HumanPlayer() {
		Player player = gameData.players.First(p => p.isHuman);
		player.knownTechs.Clear();
		player.cities.Clear();
		player.ResearchQueue.Clear();
		player.SetCurrentlyResearchedTech(null);
		return player;
	}

	// Every tech in the queue must have each of its prerequisites either already known or earlier in the queue.
	private void AssertQueueIsDependencyOrdered(Player player) {
		HashSet<ID> done = new(player.knownTechs);
		foreach (Tech tech in player.ResearchQueue) {
			foreach (Tech prereq in tech.Prerequisites) {
				Assert.True(done.Contains(prereq.id),
					$"{tech.Name} is queued before its prerequisite {prereq.Name}");
			}
			done.Add(tech.id);
		}
	}

	[Fact]
	public void FreshQueueForAnyTechIsDependencyOrdered() {
		foreach (Tech target in gameData.techs) {
			Player player = HumanPlayer();
			player.CalculateFreshTechQueueAndAssignNewCurrent(target);

			Assert.Contains(target, player.ResearchQueue);
			AssertQueueIsDependencyOrdered(player);
		}
	}

	[Fact]
	public void AppendedQueuesStayDependencyOrdered() {
		foreach (Tech first in gameData.techs) {
			foreach (Tech second in gameData.techs) {
				Player player = HumanPlayer();
				player.CalculateTechQueueAndAppendToCurrentQueue(first);
				player.CalculateTechQueueAndAppendToCurrentQueue(second);

				AssertQueueIsDependencyOrdered(player);
			}
		}
	}

	[Fact]
	public void MetallurgyIsNeverQueuedBeforeChemistry() {
		Tech metallurgy = gameData.techs.First(t => t.Name == "Metallurgy");
		Tech chemistry = gameData.techs.First(t => t.Name == "Chemistry");
		Player player = HumanPlayer();

		player.CalculateFreshTechQueueAndAssignNewCurrent(metallurgy);

		List<Tech> queue = player.ResearchQueue.ToList();
		Assert.True(queue.IndexOf(chemistry) < queue.IndexOf(metallurgy));
		Assert.NotEqual(metallurgy.id, player.currentlyResearchedTech);
	}
}
