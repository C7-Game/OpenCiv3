using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class ConquestVictoryTest {

	[Theory]
	[InlineData(0, 0, true)]   // exactly at the limit
	[InlineData(0, 1, false)]  // one rival still standing
	[InlineData(2, 3, false)]  // fewer than limit, but not reached yet
	[InlineData(2, 2, true)]   // at limit, boundary
	[InlineData(2, 1, true)]   // below limit
	public void HasVictory_TrueWhenRivalsAliveAtOrBelowLimit(int limit, int rivalsAlive, bool expected) {
		var victory = new ConquestVictory(limit);
		var status = new VictoryStatus { RivalsAlive = rivalsAlive };

		Assert.Equal(expected, victory.HasVictory(status));
	}

	[Fact]
	public void Evaluate_CountsOnlyUndefeatedNonBarbarianRivals() {
		Player player = new() { civilization = new Civilization("Rome") };
		Player aliveRival = new() { civilization = new Civilization("Greece"), defeated = false };
		Player defeatedRival = new() { civilization = new Civilization("Egypt"), defeated = true };
		Player barbarians = new() { civilization = new Civilization("Barbarians") { isBarbarian = true } };

		C7GameData.GameData gameData = new();
		gameData.players.AddRange(new List<Player> { player, aliveRival, defeatedRival, barbarians });

		var victory = new ConquestVictory(rivalsAliveLimit: 0);
		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(1, status.RivalsAlive);
		Assert.False(victory.HasVictory(status));
	}

	[Fact]
	public void Evaluate_NoRivalsRemaining_YieldsVictory() {
		Player player = new() { civilization = new Civilization("Rome") };
		Player defeatedRival = new() { civilization = new Civilization("Egypt"), defeated = true };

		C7GameData.GameData gameData = new();
		gameData.players.AddRange(new List<Player> { player, defeatedRival });

		var victory = new ConquestVictory(rivalsAliveLimit: 0);
		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(0, status.RivalsAlive);
		Assert.True(victory.HasVictory(status));
	}
}
