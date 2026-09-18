using Godot;
using ConvertCiv3Media;

[Tool]
public partial class AdvisorButton : Civ3TextureButton {
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		TextureLoader.SetButtonTextures(this, "upper_left_navigation.advisor");
	}

	public override void _Pressed() {
		base._Pressed();
		ReleaseFocus();
	}
}
