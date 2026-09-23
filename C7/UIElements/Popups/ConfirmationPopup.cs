using Godot;
using System;
using System.Diagnostics;
using C7GameData;
using Serilog;

public partial class ConfirmationPopup : Popup {
	private ILogger log = LogManager.ForContext<ConfirmationPopup>();

	Stopwatch loadTimer = new Stopwatch();
	string message;
	string yesText;
	string noText;
	Action yesAction;

	public ConfirmationPopup(string message, string yesText, string noText, Action yesAction) {
		alignment = BoxContainer.AlignmentMode.End;
		margins = new Margins(right: 10);
		this.message = message;
		this.yesText = yesText;
		this.noText = noText;
		this.yesAction = yesAction;
	}

	public override void _EnterTree() {
		loadTimer.Start();
	}

	public override void _Ready() {
		base._Ready();

		// Dimensions in-game are 530x320
		// 110px top margin are for the advisor leaderhead, Domestic in this case.
		// For some reason it uses the Happy graphics.

		// Create a transparent texture background of the appropriate size.
		// This is super important as if we just add the children, the parent won't be able to figure
		// out the size of this TextureRect, and it won't be able to align it properly.
		AddTexture(530, 320);

		TextureRect advisorHead = new TextureRect();
		advisorHead.Texture = AdvisorHead.GetPopupImage(AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Surprised, eraIndex: 0);
		//Appears at 400, 110 in game, but leftmost 25px are transparent with default graphics
		advisorHead.SetPosition(new Vector2(375, 0));
		AddChild(advisorHead);

		//TODO: Almost all (135 ms) of the disband confirmation creation, after the first time, is in the
		//background creation.  90 ms or so on the first time is before this point (and is well-cached later on).
		//Since we'll be creating the background for a lot of popups, we should optimize this.
		//Also confirmed the blitting in drawRowOfPopup is essentially free.
		AddBackground(530, 210, 110);

		AddHeader("Domestic Advisor", 120);

		Label warningMessage = new();
		//TODO: General-purpose text breaking up util.  Instead of \n
		//This appears to be the way to do multi line labels, see: https://godotengine.org/qa/11126/how-to-break-line-on-the-label-using-gdscript
		//Maybe there's an awesomer control we can user instead
		warningMessage.Text = message; //"Disband " + unitType + "?  Pardon me but these are OUR people. Do \nyou really want to disband them?";

		var multirow = message.Count(System.Environment.NewLine) >= 2;
		var multirowShift = multirow ? 20 : 0;

		warningMessage.SetPosition(new Vector2(25, 170));
		AddChild(warningMessage);

		AddButton(yesText, 215 + multirowShift, confirmed);
		AddButton(noText, 245 + multirowShift, cancel);

		loadTimer.Stop();
		TimeSpan stopwatchElapsed = loadTimer.Elapsed;
		log.Verbose("Confirmation popup load time: " + Convert.ToInt32(stopwatchElapsed.TotalMilliseconds) + " ms");
	}

	private void confirmed() {
		yesAction();
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}

	private void cancel() {
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}
}
