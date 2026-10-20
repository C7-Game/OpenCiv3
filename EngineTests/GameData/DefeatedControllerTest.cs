using System;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class DefeatedControllerTest : IClassFixture<SaveGameFixture>, IDisposable {

	private readonly SaveGameFixture fixture;

	public DefeatedControllerTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
	}

	public void Dispose() {
		// Don't leave messages behind for tests that drain the queue and don't expect them
		EngineStorage.messagesToUI.Clear();
	}

	private async Task<Player> StartGame() {
		Player human = await CreateGame.createGame(fixture.saveGame, (_) => { return fixture.behaviors; });
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return human;
	}

	/// Removes every unit of the player the way combat would, which destroys the civ.
	private static void DestroyCiv(Player player) {
		foreach (City city in player.cities.ToList()) {
			CityInteractions.DestroyCity(city);
		}
		foreach (MapUnit unit in player.units.ToList()) {
			EngineStorage.gameData.RemoveUnit(unit);
		}
	}

	/// Mimics MsgEndTurn, but gives up instead of hanging if the engine never hands control back.
	/// If that happens the loop keeps running in the background until the test run ends.
	private static async Task EndTurn(Player controller) {
		TurnHandling.OnEndTurn(controller);
		controller.hasPlayedThisTurn = true;

		Task advance = Task.Run(TurnHandling.AdvanceTurn);
		Task finished = await Task.WhenAny(advance, Task.Delay(TimeSpan.FromSeconds(60)));
		Assert.True(finished == advance, "AdvanceTurn kept running turns instead of returning to the UI");
		await advance;
	}

	[Fact]
	public async Task DefeatedHuman_StopsTheTurnLoopAndTheUiIsTold() {
		Player human = await StartGame();
		int turnBefore = EngineStorage.gameData.turn;

		DestroyCiv(human);
		Assert.True(human.defeated);
		Assert.Contains(EngineStorage.messagesToUI.OfType<MsgCivilizationDestroyed>(),
			m => m.civilization == human.civilization);

		await EndTurn(human);

		Assert.Equal(turnBefore + 1, EngineStorage.gameData.turn);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgStartTurn>());
	}

	[Fact]
	public async Task DefeatedController_InObserverMode_StillGetsEveryTurn() {
		Player human = await StartGame();
		EngineStorage.gameData.observerMode = true;
		foreach (Player p in EngineStorage.gameData.players) {
			p.isHuman = false;
		}

		DestroyCiv(human);
		Assert.True(human.defeated);
		int turnBefore = EngineStorage.gameData.turn;

		await EndTurn(human);

		Assert.Equal(turnBefore + 1, EngineStorage.gameData.turn);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgStartTurn>());
	}

	[Fact]
	public async Task DefeatedAi_DoesNotStopTheTurn() {
		Player human = await StartGame();
		Player ai = EngineStorage.gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		DestroyCiv(ai);
		Assert.True(ai.defeated);
		int turnBefore = EngineStorage.gameData.turn;

		await EndTurn(human);

		Assert.False(human.defeated);
		Assert.Equal(turnBefore + 1, EngineStorage.gameData.turn);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgStartTurn>());
		Assert.True(ai.defeated);
		Assert.Empty(ai.units);
	}
}
