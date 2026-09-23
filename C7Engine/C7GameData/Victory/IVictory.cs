using System.Collections.Generic;

namespace C7GameData;

public interface IVictory {
	public string Header();
	public VictoryStatus Evaluate(Player player, GameData gameData);
	public bool HasVictory(VictoryStatus status);
	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses);
}
