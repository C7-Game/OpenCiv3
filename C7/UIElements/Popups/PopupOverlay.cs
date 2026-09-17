using Godot;
using C7GameData;
using Serilog;

[GlobalClass]
[Tool]
public partial class PopupOverlay : HBoxContainer {

	private ILogger log = LogManager.ForContext<PopupOverlay>();

	// Meta
	[Signal] public delegate void HidePopupEventHandler();
	[Signal] public delegate void ClickEventHandler();

	// Game events
	[Signal] public delegate void BuildCityEventHandler(string name);
	[Signal] public delegate void DiplomacySelectionEventHandler(ParameterWrapper<ID> opponentPlayer);

	// Menu events
	[Signal] public delegate void SaveGameEventHandler();
	[Signal] public delegate void LoadGameEventHandler();
	[Signal] public delegate void RetireEventHandler();
	[Signal] public delegate void QuitEventHandler();


	Control currentChild = null;

	[Export]
	private Control control;

	public enum PopupCategory {
		Advisor,
		Console,
		Info,    //Sounds similar to the above, but lower-pitched in the second half
		TileInfo
	}

	public void OnHidePopup() {
		Reconnect();
		if (currentChild != null) {
			RemoveChild(currentChild);
			currentChild = null;
		}
		Hide();
	}

	public bool ShowingPopup => currentChild is not null;

	public void PlaySound(AudioStream stream) {
		AudioStreamPlayer player = GetNode<AudioStreamPlayer>("PopupSound");
		player.Stream = stream;
		player.Play();
	}

	public void ShowPopup(Popup child, PopupCategory category) {
		if (child is null) {
			// not necessary if we don't pass null?
			log.Error("Received request to show null popup");
			return;
		}

		Alignment = child.alignment;
		OffsetTop = child.margins.top;
		OffsetBottom = child.margins.bottom;
		OffsetLeft = child.margins.left;
		OffsetRight = child.margins.right;

		var soundFile = category switch {
			PopupCategory.Advisor => "popups.advisor",
			PopupCategory.Console => "popups.console",
			PopupCategory.Info => "popups.info",
			_ => null
		};

		var wav = soundFile == null ? null : AudioLoader.Load(soundFile);

		ShowChild(child);

		if (wav != null) {
			PlaySound(wav);
		}
	}

	public void ShowBlank() {
		ShowChild(new Control());
	}

	private void ShowChild(Control child) {
		AddChild(child);
		currentChild = child;
		Isolate();
		Show();
	}

	/// <summary>
	/// Creates a modal context by preventing UI events outside the popup context.
	/// Inverse of `Reconnect(..)`.
	/// </summary>
	private void Isolate() {
		// 1. Overlay catches mouse events, prevents event propagation
		MouseFilter = MouseFilterEnum.Stop;

		// 2. Stop the world: switch off UI elements
		control.ProcessMode = ProcessModeEnum.Disabled;

		// 3. Ignore all mouse input on UI elements
		control.SetMouseFilterRecursive(MouseFilterEnum.Ignore);
	}

	/// <summary>
	/// Unwind a modal context by allowing UI events outside the popup context.
	/// Inverse of `Isolate(..)`.
	/// </summary>
	private void Reconnect() {
		// 1. Let UI elements catch mouse inputs again, propagating events past the overlay
		control.SetMouseFilterRecursive(MouseFilterEnum.Pass);

		// 2. Restart the world: let UI elements run normal
		control.ProcessMode = ProcessModeEnum.Inherit;
	}

	public override void _GuiInput(InputEvent @event) {
		// Raise an event when popup overlay catches a click that the popup itself misses.
		// This usually means a click outside the popup modal.
		if (Visible && @event is InputEventMouseButton ev && ev.Pressed) {
			EmitSignal(SignalName.Click);
		}
	}

	/**
	 * N.B. Some popups should react to certain keys, e.g. the Build City popup should close without building if you
	 * press escape.  Those popups will have to implement this functionality.
	 *
	 * If we find that the majority of popups should close on Escape, we may want to make that the default,
	 * but so far, 2 out of 3 popups do not close on escape.
	 **/
	public override void _UnhandledInput(InputEvent @event) {
		if (Visible && @event is InputEventKey eventKey && eventKey.Pressed) {
			// As I've added more shortcuts, I've realized checking all of them here could be irksome.
			// For now, I'm thinking it would make more sense to process or allow through the ones that should go through,
			// as most of the global ones should *not* go through here.
			GetViewport().SetInputAsHandled();
		}

		if (@event is InputEventMouseButton ev) {
			// Catch right clicks over UI elements to stop awkward TileInfo renders
			if (ev.ButtonIndex == MouseButton.Right) {
				if (IsOverUI()) {
					AcceptEvent();
					GetViewport().SetInputAsHandled();
				}
			}
		}
	}

	private bool IsOverUI() {
		Control ctrl = GetViewport().GuiGetHoveredControl();
		return ctrl is TextureButton or TextureRect;
	}
}
