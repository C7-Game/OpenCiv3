using Godot;
using static TemporaryPopup;

[Tool]
public partial class CivilopediaButton : Civ3TextureButton {
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		TextureLoader.SetButtonTextures(this, "upper_left_navigation.civilopedia");
		this.Disabled = true;
		this.TooltipText = "Not Implemented yet :/";
		this.Theme = GetToolTipTheme();
	}
}
