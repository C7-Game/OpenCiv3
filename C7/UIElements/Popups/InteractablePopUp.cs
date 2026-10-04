using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;
using static AdvisorHead;
using static Godot.BoxContainer;
using static Godot.Control;

namespace C7.UIElements.Popups;

public class InteractablePopUp {
	public int hSize { get; init; } // the custom minimum size (X)
	public LayoutPreset layoutPreset { get; init; }
	public Margins margins { get; init; }
	public ID controllerId { get; init; }
	protected Player controller { get; init; }
	public string header { get; init; }
	public string message { get; init; }
	public AdvisorGraphicsDetails advisorDetails { get; init; }
	public LineEditComponent lineEditComponent { get; init; }
	public int contollerEraIndex { get; init; } = 0;
	public List<ButtonAction> buttonActions { get; init; }
	public bool hasConfirm { get; init; }
	public bool hasCancel { get; init; }

	protected InteractablePopUp(ID controllerId, string header, LayoutPreset layoutPreset, Margins margins) {
		this.controllerId = controllerId;
		this.header = header;
		this.layoutPreset = layoutPreset;
		this.margins = margins ?? new Margins();

		Player player = null;
		EngineStorage.ReadGameData(data => {
			player = data.players.First(p => p.id == controllerId);
		});
		this.controller = player;
		this.contollerEraIndex = this.controller.EraIndex();
	}
}

public class ButtonAction {
	public string message { get; init; }
	public Action action { get; init; }
	public ButtonGroup buttonGroup { get; init; }
	public bool pressed { get; set; } = false;
}

public class LineEditComponent {
	public string label;
	public string placeholderText;
	public Action<string> callback;
}
