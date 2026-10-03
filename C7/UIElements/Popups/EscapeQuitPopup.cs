// using Godot;
//
// public partial class EscapeQuitPopup : Popup {
//
// 	public EscapeQuitPopup() {
// 		alignment = BoxContainer.AlignmentMode.Center;
// 		margins = new Margins(top: 100);
// 	}
//
// 	public override void _Ready() {
// 		base._Ready();
//
// 		// Dimensions in-game are 270x295, centered at the top
// 		// 100px margin from the top (this is different than the 110px when there's an advisor)
//
// 		// Create a transparent texture background of the appropriate size.
// 		// This is super important as if we just add the children, the parent won't be able to figure
// 		// out the size of this TextureRect, and it won't be able to align it properly.
// 		AddTexture(270, 195);
// 		AddBackground(270, 195);
//
// 		AddHeader("Oh No!", 10);
//
// 		Label warningMessage = new Label();
// 		// TODO: General-purpose text breaking up util. Instead of \n
// 		// This appears to be the way to do multi line labels, see: https://godotengine.org/qa/11126/how-to-break-line-on-the-label-using-gdscript
// 		// Maybe there's an awesomer control we can user instead
// 		warningMessage.Text = "Do you really want to quit?";
//
// 		warningMessage.SetPosition(new Vector2(25, 62));
// 		AddChild(warningMessage);
//
// 		AddButton("No, not really", 88, cancel);
// 		AddButton("Yes, immediately!", 116, quit);
// 	}
//
// 	private void quit() {
// 		GetParent().EmitSignal(PopupOverlay.SignalName.Quit);
// 	}
//
// 	private void cancel() {
// 		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
// 	}
// }
