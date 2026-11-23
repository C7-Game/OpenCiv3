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
	public static string PopHut(GameData gameData, MapUnit unit, Tile tile)
		=> PopHut(gameData, unit.owner, tile, unit);

	/// Pops a hut for a player, e.g. when their borders spread over it. unit
	/// is the unit that entered the hut, if any.
	public static string PopHut(GameData gameData, Player player, Tile tile, MapUnit unit = null) {
		// Like a dispersed barbarian camp, the hut disappears from every
		// player's map, not only from the popper's.
		TerrainImprovement hut = tile.overlays.ImprovementAtLayer(TerrainImprovement.Layer.GoodyHut);
		if (hut != null) {
			tile.overlays.Remove(hut);
		}
		foreach (Player p in gameData.players) {
			p.tileKnowledge.MaybeRemoveTerrainImprovement(tile.Id, Tile.TileOverlays.GOODY_HUT);
		}

		Func<GoodyHutContext, string> pop = gameData.luaBehaviorEngine.ImportFunc<Func<GoodyHutContext, string>>("goody_huts.pop");
		string outcome = pop(new GoodyHutContext(gameData, player, tile, unit));

		log.Information("{player} popped a goody hut at {tile}: {outcome}", player, tile, outcome);
		return outcome;
	}
}
