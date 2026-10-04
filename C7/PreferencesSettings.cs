using C7Engine;
using Godot;

namespace C7.UIElements;

/// <summary>
/// User preferences, stored outside the save file so they stay stable across
/// games and scenarios. Not yet surfaced anywhere in the UI.
/// </summary>
public static class PreferencesSettings {
	public static class Game {
		// Lower case on purpose: ini-parser treats section and key names as
		// case-sensitive, and existing C7.ini files already use these names.
		public const string SectionName = "preferences";
		public const string PromptForResearch = "promptForResearch";
		public const bool DefaultPromptForResearch = true;
	}

	public static bool GetPromptForResearch() {
		return C7Settings.GetBoolOrDefault(
			Game.SectionName,
			Game.PromptForResearch,
			Game.DefaultPromptForResearch);
	}

	public static void SetPromptForResearch(bool value) {
		C7Settings.SetBool(Game.SectionName, Game.PromptForResearch, value);
		C7Settings.SaveSettings();
	}
}
