using Godot;
using ConvertCiv3Media;

[Tool]
public partial class MenuButton : Civ3TextureButton {

	[Export]
	private PopupOverlay popupOverlay;

	public override void _Ready() {
		ImageTexture menuTexture = TextureLoader.Load("upper_left_navigation.menu");
		this.TextureNormal = menuTexture;
	}

	public override void _Pressed() {
		popupOverlay.ShowPopup(new GameMenu(), PopupOverlay.PopupCategory.Info);
		ReleaseFocus();
	}

}
