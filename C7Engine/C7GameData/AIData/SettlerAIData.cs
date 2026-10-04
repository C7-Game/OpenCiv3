using System.Collections.Generic;

namespace C7GameData.AIData {
	/**
	 * I'm playing around with different possibilities for AI here.
	 * I realized that I'd like units/players/etc. to be able to have references to their
	 * AI data.  This is because AI may be stateful.  A Settler has a destination.  A war plan
	 * has a start date.  A war has a goal, e.g. secure Iron.  A transport convoy has a destination.
	 *
	 * Hopefully, keeping track of this will result in a more purposeful AI.  Certain events
	 * may cause it to re-evaluate, "should this worker still be building irrigation on the border
	 * now that there's a war there?", but it's hard to tell a Settler "build a city here"
	 * without a way to store "here".
	 *
	 * This will probably have to be broken into sub-classes over time, based on unit abilities.
	 *
	 * I'm also unsure of how much the logic of figuring out what to do should be in these
	 * classes, versus higher-level ones.
	 */
	public class SettlerAIData : UnitAIData {
		public enum SettlerGoal {
			BUILD_CITY,
			JOIN_CITY
		}
		public SettlerGoal goal;
		public Tile destination;
		public TilePath pathToDestination;
		public MapUnit escort;

		// Tiles the settler could not reach (issue #213). Cleared when the AI is re-created.
		public HashSet<Tile> unreachableDestinations = new();

		public override string ToString() {
			return goal + " at " + destination;
		}
	}
}
