using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData.Victory;

public class CheckVictoryTest : IClassFixture<SaveGameFixture> {

	private readonly SaveGameFixture fixture;

	public CheckVictoryTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine message queue is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
	}

	private const int TurnLimit = 540;

	private static Player MakePlayer(string id, string civ, bool barbarian = false, bool defeated = false) {
		return new Player {
			id = ID.FromString(id),
			civilization = new Civilization(civ) { isBarbarian = barbarian },
			defeated = defeated,
		};
	}

	/// Builds a minimal GameData with score + time-limit victories registered (as SaveGame.ConvertVictoryConditions does),
	/// and a single history record per non-barbarian player carrying the given score.
	private static C7GameData.GameData MakeGame(int turn, params (Player player, int score)[] entries) {
		var difficulty = new Difficulty();
		var gd = new C7GameData.GameData {
			turn = turn,
			difficulties = new List<Difficulty> { difficulty },
			gameDifficulty = difficulty,
			history = new Dictionary<string, List<HistTurnRecord>>(),
		};
		foreach (var (player, score) in entries) {
			gd.players.Add(player);
			if (!player.isBarbarians)
				gd.history[player.id.ToString()] = new List<HistTurnRecord> { new HistTurnRecord { Score = score } };
		}
		gd.victories.Add(new ScoreVictory());
		gd.victories.Add(new TimeLimitVictory(TurnLimit));
		return gd;
	}

	private static List<MsgVictory> VictoryMessages() =>
		EngineStorage.messagesToUI.OfType<MsgVictory>().ToList();

	// ---------------------------------------------------------------- turn-limit boundary

	[Theory]
	[InlineData(TurnLimit - 1, false)]
	[InlineData(TurnLimit, true)]
	[InlineData(TurnLimit + 1, true)]
	public void CheckVictory_OnlyEndsGameOnceTurnLimitIsReached(int turn, bool expectGameOver) {
		var rome = MakePlayer("player-2", "Rome");
		var greece = MakePlayer("player-3", "Greece");
		var gd = MakeGame(turn, (rome, 10), (greece, 20));

		TurnHandling.CheckVictory(gd);

		Assert.Equal(expectGameOver, gd.winner != null);
		Assert.Equal(expectGameOver, gd.gameOver);
		Assert.Equal(expectGameOver ? 1 : 0, VictoryMessages().Count);
	}

	// ---------------------------------------------------------------- winner selection

	[Fact]
	public void CheckVictory_AtLimit_HighestScoreWins_AndExactlyOneMessageIsSent() {
		var rome = MakePlayer("player-2", "Rome");
		var greece = MakePlayer("player-3", "Greece");
		var egypt = MakePlayer("player-4", "Egypt");
		var gd = MakeGame(TurnLimit, (rome, 10), (greece, 30), (egypt, 20));

		TurnHandling.CheckVictory(gd);

		Assert.Same(greece, gd.winner);
		Assert.True(gd.gameOver);

		var msgs = VictoryMessages();
		Assert.Single(msgs);
		Assert.Same(greece, msgs[0].winner);
		Assert.True(gd.gameOver);
	}

	[Fact]
	public void CheckVictory_AtLimit_TiedScores_FirstPlayerInListWins() {
		// Documents current tie-break behaviour (stable OrderByDescending => list order).
		// If ties should be broken some other way (human first? most cities?), change this test.
		var rome = MakePlayer("player-2", "Rome");
		var greece = MakePlayer("player-3", "Greece");
		var gd = MakeGame(TurnLimit, (rome, 25), (greece, 25));

		TurnHandling.CheckVictory(gd);

		Assert.Same(rome, gd.winner);
	}

	[Fact]
	public void CheckVictory_AtLimit_BarbariansAreNeverWinnersAndDoNotNeedHistory() {
		var barbs = MakePlayer("player-0", "Barbarians", barbarian: true);
		var rome = MakePlayer("player-2", "Rome");
		var gd = MakeGame(TurnLimit, (barbs, 999), (rome, 1));

		TurnHandling.CheckVictory(gd);

		Assert.Same(rome, gd.winner);
	}

	[Fact]
	public void CheckVictory_AtLimit_DefeatedPlayerWithTopScoreDoesNotWin() {
		// EXPECTED-TO-FAIL on caf614a: CheckVictory never looks at Player.defeated, and TimeLimitVictory
		// reports "victory" for every player, so a defeated civ with the best (frozen) score can be declared winner.
		var dead = MakePlayer("player-4", "Carthage", defeated: true);
		var rome = MakePlayer("player-2", "Rome");
		var gd = MakeGame(TurnLimit, (dead, 50), (rome, 10));

		TurnHandling.CheckVictory(gd);

		Assert.Same(rome, gd.winner);
	}

	// ---------------------------------------------------------------- game-over state

	[Fact]
	public void CheckVictory_AfterGameOver_DoesNotDeclareOrSendAgain() {
		var rome = MakePlayer("player-2", "Rome");
		var greece = MakePlayer("player-3", "Greece");
		var gd = MakeGame(TurnLimit, (rome, 10), (greece, 20));

		TurnHandling.CheckVictory(gd);
		Player firstWinner = gd.winner;

		// "Continue playing" for a few turns; scores would now favour Rome if anything re-evaluated
		gd.turn += 3;
		gd.history[rome.id.ToString()].Add(new HistTurnRecord { Score = 500 });
		TurnHandling.CheckVictory(gd);

		Assert.Same(firstWinner, gd.winner);
		Assert.Single(VictoryMessages());
	}

	[Fact]
	public void UpdateHistory_AfterGameOver_DoesNotAddRecords() {
		// The popup promises "No further score will be entered".
		var rome = MakePlayer("player-2", "Rome");
		var gd = MakeGame(TurnLimit, (rome, 10));
		gd.winner = rome;
		gd.gameOver = true;

		rome.UpdateHistory(gd);

		Assert.Single(gd.history[rome.id.ToString()]);
	}

	// ---------------------------------------------------------------- persistence

	[Fact]
	public void GameOverState_SurvivesSaveAndLoad() {
		C7GameData.GameData gd = SaveGameFixture.HydrateSaveGame(fixture.saveGame);
		gd.winner = gd.players.First(p => !p.isBarbarians);
		gd.gameOver = true;

		SaveGame save = SaveGame.FromGameData(gd);
		Assert.NotNull(save.Winner);
		Assert.True(save.GameOver);

		C7GameData.GameData reloaded = save.ToGameData(fixture.behaviors);
		Assert.NotNull(reloaded.winner);
		Assert.True(reloaded.gameOver);
	}

	[Fact]
	public void LoadedGame_RegistersScoreAndTimeLimitVictories_UsingConfiguredTurnLimit() {
		C7GameData.GameData gd = SaveGameFixture.HydrateSaveGame(fixture.saveGame);

		Assert.Contains(gd.victories, v => v is ScoreVictory);
		Assert.Contains(gd.victories, v => v is TimeLimitVictory);

		// Not yet ending at turn 0 with the default 540-turn limit
		var status = gd.victories.OfType<TimeLimitVictory>().First().Evaluate(gd.players.First(), gd);
		Assert.False(gd.victories.OfType<TimeLimitVictory>().First().HasVictory(status));
	}
}
