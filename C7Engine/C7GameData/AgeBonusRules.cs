namespace C7GameData {
	// Configuration for the culture age bonus. The timing and multiplier live in
	// the Lua function at luaFunctionPath; enabled and repeatable stay here so
	// scenarios can turn the bonus off or make it compound without writing Lua.
	public class AgeBonusRules {
		public const string DefaultLuaFunctionPath = "gameplay.culture.age_bonus";

		public bool enabled = true;
		public bool repeatable = false;
		public string luaFunctionPath = DefaultLuaFunctionPath;
	}
}
