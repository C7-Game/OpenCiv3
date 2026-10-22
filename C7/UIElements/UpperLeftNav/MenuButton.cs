using C7Engine;
using Godot;
using static TemporaryPopup;

[Tool]
public partial class MenuButton : Civ3TextureButton {
	public override void _Ready() {
		TextureLoader.SetButtonTextures(this, "upper_left_navigation.menu");
		this.TooltipText = "Main Menu";
		this.Theme = GetToolTipTheme();
	}

	public override void _Pressed() {
		new MsgGameMainMenu().send();
		this.FocusMode = FocusModeEnum.None;
	}

}
