using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;
using static AdvisorHead;
using static C7.UIElements.Popups.InteractablePopUpController;

namespace C7.UIElements.Popups;

public class ConfirmPopUp : InformationPopup {
	private ILogger log = LogManager.ForContext<ConfirmPopUp>();

	public ConfirmPopUp(
		ID controllerId, string header, string message, Advisor advisor, Mood mood,
		string yesText, string noText,
		Action yesAction, Action yesCallback = null,
		Action noAction = null, Action noCallback = null,
		bool waitBeforeYesCallback = true, bool waitBeforeNoCallback = true,
		bool hasConfirm = true, bool hasCancel = true,
		int hSize = 400, Control.LayoutPreset layoutPreset = DEFAULT_LAYOUT_PRESET, Margins margins = null)
		: base(controllerId, header, message, advisor, mood, hSize, layoutPreset, margins) {

		var btnGroup = new ButtonGroup();

		this.buttonActions = new List<ButtonAction>() {
			new ButtonAction() {
				buttonGroup = btnGroup,
                // TODO: we can't support RichTextLabel text using the Civ3MenuButton class yet
                message = yesText,
				action = async () => {
					yesAction();
                    // wait for the previous pop up to close so that we can call the callback
                    if(waitBeforeYesCallback)
						await EngineStorage.WaitForMessageToEngine<MsgUiDisengaged>();
					yesCallback?.Invoke();
				},
			},
			new ButtonAction() {
				buttonGroup = btnGroup,
				message = noText,
				action = async () => {
					noAction?.Invoke();
                    // wait for the previous pop up to close so that we can call the callback
                    if(waitBeforeNoCallback)
						await EngineStorage.WaitForMessageToEngine<MsgUiDisengaged>();
					noCallback?.Invoke();
				},
				pressed = true,
			}
		};

		this.hasConfirm = hasConfirm;
		this.hasCancel = hasCancel;
	}
}
