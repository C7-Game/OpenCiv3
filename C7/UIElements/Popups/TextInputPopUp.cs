using System;
using C7.UIElements.Popups;
using C7GameData;
using Serilog;
using static AdvisorHead;
using static Godot.Control;
using static C7.UIElements.Popups.InteractablePopUpController;

public class TextInputPopUp : InteractablePopUp {
	private ILogger log = LogManager.ForContext<TextInputPopUp>();

	public TextInputPopUp(
		ID controllerId, string header, string label, string text, Action<string> callback,
		Advisor advisor, Mood mood, int hSize = 550,
		LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET, Margins margins = null)
		: base(controllerId, header, layoutPreset, margins) {

		this.controllerId = controllerId;
		this.hSize = hSize;
		this.advisorDetails = new AdvisorGraphicsDetails(advisor, mood, this.contollerEraIndex);

		this.lineEditComponent = new LineEditComponent() {
			label = label,
			placeholderText = text,
			callback = callback
		};

		this.hasConfirm = true;
		this.hasCancel = true;
	}
}
