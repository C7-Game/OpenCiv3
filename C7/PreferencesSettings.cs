using C7Engine;

namespace C7.UIElements;

/// <summary>
/// User preferences, stored outside the save file so they stay stable across
/// games and scenarios. Surfaced by the Preferences scene, reachable from the
/// main menu, the in-game GameMenu, and Ctrl-P.
/// </summary>
public static class PreferencesSettings {
	public const string SectionName = "Preferences";
	public const string PromptForResearch = nameof(PromptForResearch);
	public const bool DefaultPromptForResearch = true;
	public const string ShowGrid = nameof(ShowGrid);
	public const bool DefaultShowGrid = false;

	public static bool GetPromptForResearch() {
		return C7Settings.GetBoolOrDefault(SectionName, PromptForResearch, DefaultPromptForResearch);
	}

	public static void SetPromptForResearch(bool value) {
		C7Settings.SetBool(SectionName, PromptForResearch, value);
		C7Settings.SaveSettings();
	}

	public static bool GetShowGrid() {
		return C7Settings.GetBoolOrDefault(SectionName, ShowGrid, DefaultShowGrid);
	}

	public static void SetShowGrid(bool value) {
		C7Settings.SetBool(SectionName, ShowGrid, value);
		C7Settings.SaveSettings();
	}

	public const string AutosaveTurns = nameof(AutosaveTurns);
	public const int DefaultAutosaveTurns = 5;

	public static int GetAutosaveTurns() {
		return C7Settings.GetIntOrDefault(SectionName, AutosaveTurns, DefaultAutosaveTurns);
	}

	public static void SetAutosaveTurns(int value) {
		C7Settings.SetInt(SectionName, AutosaveTurns, value);
		C7Settings.SaveSettings();
	}
}
