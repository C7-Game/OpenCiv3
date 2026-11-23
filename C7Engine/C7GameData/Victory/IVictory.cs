using System.Collections.Generic;

namespace C7GameData;

public interface IVictory {
	public string Header();
	public VictoryStatus Evaluate(Player player, GameData gameData);
	public bool HasVictory(VictoryStatus status);
	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses);

	/// Short suffix for the victory announcement, e.g. "city" for a one-city culture win.
	/// Empty by default; victories with more than one way to win can override it.
	public string Detail(VictoryStatus status) => "";
}
