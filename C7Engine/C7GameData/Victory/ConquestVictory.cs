using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

public class ConquestVictory : IVictory {
	private readonly int _rivalsAliveLimit;

	public ConquestVictory(int rivalsAliveLimit) {
		_rivalsAliveLimit = rivalsAliveLimit;
	}

	public string Header() => "Conquest";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		int rivalsAlive = gameData.GetRivals(player).Count(p => !p.defeated);

		return new VictoryStatus {
			Player = player,
			RivalsAlive = rivalsAlive
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.RivalsAlive <= _rivalsAliveLimit;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return ConquestPrint(status, rivalStatuses);
	}

	private string[] ConquestPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		return [
			"Eliminate all rivals",
			"",
			"",
			"",
			"Rivals still alive:",
			$"{status.RivalsAlive}"
		];
	}
}
