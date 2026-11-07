using C7Engine;

namespace C7.UIElements;

/// <summary>
/// Developer-only settings, stored in C7.ini. The developer flag's sole
/// purpose is to reveal the Dev Tools button on the in-game game menu;
/// it is hand-edited and never flipped from the UI.
/// </summary>
public static class DeveloperSettings {
	public const string SectionName = "Developer";
	// The ini parser is case-sensitive; the key is lowercase to match useStandaloneMode
	public const string IsDeveloper = "isDeveloper";
	public const bool DefaultDeveloper = false;

	public static bool GetDeveloper() {
		return C7Settings.GetBoolOrDefault(SectionName, IsDeveloper, DefaultDeveloper);
	}

	public static void SetDeveloper(bool value) {
		C7Settings.SetBool(SectionName, IsDeveloper, value);
		C7Settings.SaveSettings();
	}
}
