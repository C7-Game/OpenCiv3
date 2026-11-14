using System;
using C7GameData;
using Serilog;

namespace C7Engine;

public static class GoodyHutInteractions {
	private static readonly ILogger log = Log.ForContext(typeof(GoodyHutInteractions));

	/// Removes the hut from the tile and applies its outcome.
	///
	/// The roll and every outcome live in Lua ("goody_huts.pop" in the
	/// behaviors). Returns the name of the outcome that was applied.
	public static string PopHut(GameData gameData, MapUnit unit, Tile tile) {
		tile.hasGoodyHut = false;

		Func<GoodyHutContext, string> pop = gameData.luaBehaviorEngine.ImportFunc<Func<GoodyHutContext, string>>("goody_huts.pop");
		string outcome = pop(new GoodyHutContext(gameData, unit, tile));

		log.Information("{player} popped a goody hut at {tile}: {outcome}", unit.owner, tile, outcome);
		return outcome;
	}
}
