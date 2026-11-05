namespace C7GameData {
	// Configuration for the culture age bonus. All of the behavior lives in the
	// Lua function at luaFunctionPath, which falls back to the default when the
	// save or scenario does not define one.
	public class AgeBonusRules {
		public const string DefaultLuaFunctionPath = "gameplay.culture.age_bonus";

		public string luaFunctionPath = DefaultLuaFunctionPath;
	}
}
