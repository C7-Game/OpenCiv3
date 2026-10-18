using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Godot;
using Serilog;
using static AdvisorHead;
using static Godot.Control;
using static C7.UIElements.Popups.InteractablePopUpController;


namespace C7.UIElements.Popups;

public class OptionsPopUp : InformationPopup {
	private ILogger log = LogManager.ForContext<OptionsPopUp>();

	public OptionsPopUp(
		ID controllerId, string header, string message, List<ButtonAction> options,
		Advisor advisor, Mood mood, int hSize = 400,
		LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET, Margins margins = null)
		: base(controllerId, header, message, advisor, mood, hSize, layoutPreset, margins) {

		var btnGroup = new ButtonGroup();
		var optionsList = new List<ButtonAction>();

		foreach (var option in options) {
			var actionButton = new ButtonAction() {
				buttonGroup = btnGroup,
				message = option.message,
				action = option.action,
			};
			optionsList.Add(actionButton);
		}

		var firstOption = optionsList.First();
		firstOption.pressed = true;

		this.buttonActions = optionsList;

		this.hasConfirm = true;
		this.hasCancel = true;
	}

}
