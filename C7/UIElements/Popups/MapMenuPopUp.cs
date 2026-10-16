using System.Collections.Generic;
using C7GameData;
using static AdvisorHead;
using static Godot.Control;
using static C7.UIElements.Popups.InteractablePopUpController;

namespace C7.UIElements.Popups;

public class MapMenuPopUp : OptionsPopUp {
	public MapMenuPopUp(
		ID controllerId, string header, string message, List<ButtonAction> options,
		Advisor advisor, Mood mood, int hSize = 400,
		LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET, Margins margins = null)
		: base(controllerId, header, message, options, advisor, mood, hSize, layoutPreset, margins) {
		this.hasConfirm = false;
	}
}
