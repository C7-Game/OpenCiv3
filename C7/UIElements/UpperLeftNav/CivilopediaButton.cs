using Godot;
using static TemporaryPopup;

[Tool]
public partial class CivilopediaButton : Civ3TextureButton {

	[Export]
	private PopupOverlay popupOverlay;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		TextureLoader.SetButtonTextures(this, "upper_left_navigation.civilopedia");
		this.TooltipText = "Codex";
		this.Theme = GetToolTipTheme();
	}

	public override void _Pressed() {
		base._Pressed();
		popupOverlay.ShowPopup(new CodexPopup(), PopupOverlay.PopupCategory.Info);
		ReleaseFocus();
	}
}
