namespace C7GameData {
	// Old buildings produce more culture. See GetMultiplier.
	public class AgeBonusRules {
		public bool enabled = true;
		public int yearsToDouble = 1000;
		public int multiplier = 2;

		// When true the bonus repeats for each further yearsToDouble years. Civ 3
		// applies a single doubling, no matter how old a building is.
		public bool repeatable = false;

		public bool appliesToWonders = true;
		public bool appliesToImprovements = true;
		public string luaFunctionPath = "gameplay.culture.age_bonus_multiplier";

		public int GetMultiplier(int ageInYears, bool isWonder) {
			if (!enabled || (isWonder ? !appliesToWonders : !appliesToImprovements)) {
				return 1;
			}
			if (multiplier <= 1) {
				return 1;
			}
			if (yearsToDouble <= 0) {
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

	public struct AgeBonusContext(Player player, City city, Building building, int ageInYears, int baseMultiplier) {
		public Player player = player;
		public City city = city;
		public Building building = building;
		public int ageInYears = ageInYears;
		public int baseMultiplier = baseMultiplier;
	}
}
