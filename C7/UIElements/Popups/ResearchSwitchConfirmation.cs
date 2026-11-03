using Godot;
using System;
using C7GameData;

/// <summary>
/// Asks the player to confirm abandoning the tech in progress (and its beakers)
/// for a different one picked in the science advisor.
/// </summary>
public partial class ResearchSwitchConfirmation : Popup {
	Player player;
	Tech oldTech;
	Tech newTech;
	int lostBeakers;
	Action onConfirmed;

	public ResearchSwitchConfirmation(Player player, Tech oldTech, Tech newTech, int lostBeakers, Action onConfirmed) {
		alignment = BoxContainer.AlignmentMode.End;
		margins = new Margins(right: 10);
		this.player = player;
		this.oldTech = oldTech;
		this.newTech = newTech;
		this.lostBeakers = lostBeakers;
		this.onConfirmed = onConfirmed;
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(530, 320);

		TextureRect advisorHead = new();
		advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Science, AdvisorHead.Mood.Sad, player.EraIndex());
		advisorHead.SetPosition(new Vector2(375, 0));
		AddChild(advisorHead);

		AddBackground(530, 210, 110);
		AddHeader("Science Advisor", 120);

		Label warningMessage = new();
		warningMessage.Text = $"Switching to {newTech.Name} will lose {lostBeakers} gold of\nresearch already spent on {oldTech.Name}.\nAre you sure?";
		warningMessage.SetPosition(new Vector2(25, 170));
		AddChild(warningMessage);

		AddButton("Yes, change our research.", 245, () => {
			onConfirmed();
			GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		});
		AddButton("No. Let's stay the course.", 275, () => {
			GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		});
	}
}
