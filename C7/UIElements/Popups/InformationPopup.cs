using C7GameData;
using Serilog;
using static AdvisorHead;
using static Godot.Control;
using static C7.UIElements.Popups.InteractablePopUpController;

namespace C7.UIElements.Popups;

/// <summary>
/// Supports: an advisor head, a text element and a confirm button. Good for simple informational popups.
/// </summary>
public class InformationPopup : InteractablePopUp {
	private ILogger log = LogManager.ForContext<InformationPopup>();

	public InformationPopup(
		ID controllerId, string header, string message,
		Advisor advisor, Mood mood, int hSize = 400,
		LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET, Margins margins = null)
		: base(controllerId, header, layoutPreset, margins) {
		this.controllerId = controllerId;
		this.hSize = hSize;
		this.message = message;
		this.advisorDetails = new AdvisorGraphicsDetails(advisor, mood, this.contollerEraIndex);

		this.hasConfirm = true;
	}
}
