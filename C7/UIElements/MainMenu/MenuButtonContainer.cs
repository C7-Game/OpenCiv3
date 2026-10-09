using Godot;

// The list of buttons shown in the main menu.
//
// The buttons themselves live in main_menu.tscn so that the menu can be seen
// and rearranged in the scene editor; this class just holds typed references to
// them so the rest of the code can still wire up their signals.
[Tool]
public partial class MenuButtonContainer : VBoxContainer {
	[Export] public Civ3MenuButton NewGame;
	[Export] public Civ3MenuButton QuickStart;
	[Export] public Civ3MenuButton Tutorial;
	[Export] public Civ3MenuButton LoadGame;
	[Export] public Civ3MenuButton LoadScenario;
	[Export] public Civ3MenuButton HallOfFame;
	[Export] public Civ3MenuButton ToggleGraphics;
	[Export] public Civ3MenuButton Preferences;
	[Export] public Civ3MenuButton AudioPreferences;
	[Export] public Civ3MenuButton Credits;
	[Export] public Civ3MenuButton Exit;
}
