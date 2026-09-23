using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class TimeLimitVictoryTest {

	[Theory]
	[InlineData(200, 199, false)]
	[InlineData(200, 200, true)]  // reaching the limit exactly counts
	[InlineData(200, 201, true)]
	[InlineData(0, 0, true)]
	public void HasVictory_TrueOnceCurrentTurnReachesLimit(int limit, int currentTurn, bool expected) {
		var victory = new TimeLimitVictory(limit);
		var status = new VictoryStatus { CurrentTurn = currentTurn };

		Assert.Equal(expected, victory.HasVictory(status));
	}

	[Fact]
	public void Evaluate_ReportsCurrentTurnFromGameData() {
		var victory = new TimeLimitVictory(turnLimit: 300);
		C7GameData.GameData gameData = new() { turn = 42 };
		Player player = new() { civilization = new Civilization("Rome") };

		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(42, status.CurrentTurn);
		Assert.False(victory.HasVictory(status));
	}
}
