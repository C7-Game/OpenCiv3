namespace C7GameData {
	// Rules for the age bonus: a building that has been around long enough
	// produces more culture than a newly built one. Civ 3 doubles the culture
	// of a building once it is a millennium old.
	public class AgeBonusRules {
		public bool enabled = true;

		// How old a building must be, in years, before it earns the bonus.
		public int yearsToDouble = 1000;

		// The factor applied to the building's culture once it is old enough.
		public int multiplier = 2;

		// When false the bonus applies only once, no matter how old the
		// building gets (the Civ 3 behavior). When true the building earns the
		// bonus again for every further period of yearsToDouble years.
		public bool repeatable = false;

		public bool appliesToWonders = true;
		public bool appliesToImprovements = true;

		// Path to an optional Lua function that computes the multiplier for a
		// building. The function is given an AgeBonusContext and returns the
		// multiplier to use. When the game mode does not define the function
		// the rules above are used.
		public string luaFunctionPath = "gameplay.culture.age_bonus_multiplier";

		// The culture multiplier for a building of the given age.
		public int GetMultiplier(int ageInYears, bool isWonder) {
			if (!enabled || (isWonder ? !appliesToWonders : !appliesToImprovements)) {
				return 1;
			}
			if (multiplier <= 1) {
				return 1;
			}
			if (yearsToDouble <= 0) {
				// Without a threshold every building qualifies right away.
				return multiplier;
			}
			if (ageInYears < yearsToDouble) {
				return 1;
			}

			int doublings = repeatable ? 1 + (ageInYears - yearsToDouble) / yearsToDouble : 1;

			int result = 1;
			for (int i = 0; i < doublings; i++) {
				result *= multiplier;
			}
			return result;
		}
	}

	// The information a Lua age bonus function gets to work with.
	public struct AgeBonusContext {
		public Player player;
		public City city;
		public Building building;
		public int ageInYears;
		public int baseMultiplier;
	}
}
