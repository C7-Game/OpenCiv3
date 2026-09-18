using Godot;
using ConvertCiv3Media;

[Tool]
public partial class CivilopediaButton : Civ3TextureButton {

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		ImageTexture menuTexture = TextureLoader.Load("upper_left_navigation.civilopedia");
		this.TextureNormal = menuTexture;
	}

	public override void _Pressed() {
		base._Pressed();
		ReleaseFocus();
	}
}
