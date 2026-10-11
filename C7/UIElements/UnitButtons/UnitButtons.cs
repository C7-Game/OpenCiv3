using Godot;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Serilog;
using System.Linq;

/*
 UnitButtons contains the buttons at the bottom of the game UI when viewing the
 map, and control unit actions. Clicking buttons triggers Input.ActionPress
 calls which are checked and handled in Game.processActions.
*/

public partial class UnitButtons : VBoxContainer {
	[Signal] public delegate void ActionRequestedEventHandler(string action);

	private ILogger log = LogManager.ForContext<UnitButtons>();

	// A record holding a button and the base tooltip (without any indication
	// of how many turns an action might take).
	public record ButtonAndTooltip(TextureButton button, string baseTooltip);

	private Dictionary<string, ButtonAndTooltip> buttonMap = new();

	[Export]
	HBoxContainer primaryControls;

	[Export]
	HBoxContainer specializedControls;

	[Export]
	HBoxContainer advancedControls;

	private static readonly string[] terraformOrder = [
		C7Action.UnitBuildRoad,
		C7Action.UnitBuildRailroad,
		C7Action.UnitBuildMine,
		C7Action.UnitIrrigate,
		C7Action.UnitClearForest,
		C7Action.UnitClearWetlands,
		C7Action.UnitBuildFortress,
		C7Action.UnitBuildBarricade,
		C7Action.UnitPlantForest,
		C7Action.UnitClearDamage,
	];

	private void SetUpControlButtons() {
		AddNewButton(primaryControls, C7Action.UnitHold);
		AddNewButton(primaryControls, C7Action.UnitWait);
		AddNewButton(primaryControls, C7Action.UnitFortify);
		AddNewButton(primaryControls, C7Action.UnitDisband);
		AddNewButton(primaryControls, C7Action.UnitGoto);
		AddNewButton(primaryControls, C7Action.UnitExplore);
		// AddNewButton(primaryControls, C7Action.UnitSentry);
		// AddNewButton(primaryControls, C7Action.UnitSentryEnemyOnly);

		//   ******* SPECIALIZED CONTROLS *************
		AddNewButton(specializedControls, C7Action.UnitLoad);
		AddNewButton(specializedControls, C7Action.UnitUnload);
		// AddNewButton(specializedControls, "pillage");
		AddNewButton(specializedControls, C7Action.UnitBombard);
		// AddNewButton(specializedControls, "autobombard");
		// AddNewButton(specializedControls, "paradrop");
		//superfortify?
		// AddNewButton(specializedControls, "hurryBuilding");
		// AddNewButton(specializedControls, "upgrade");
		// AddNewButton(specializedControls, "sacrifice");
		// AddNewButton(specializedControls, "scienceAge");
		AddNewButton(specializedControls, C7Action.UnitBuildCity);

		SetUpTerraformButtons();

		AddNewButton(specializedControls, C7Action.UnitAutomate);

		// Row index 4 and later not yet added
	}

	private void SetUpTerraformButtons() {
		var terraformOrderMap = terraformOrder
			.Select((action, index) => new { action, index })
			.ToDictionary(x => x.action, x => x.index);

		var sortedTerraforms = EngineStorage.gameData.Terraforms
			.OrderBy(tf => terraformOrderMap.TryGetValue(tf.UIAction, out var idx) ? idx : int.MaxValue);

		foreach (Terraform tf in sortedTerraforms) {
			AddNewButton(specializedControls, tf.UIAction);
		}
	}

	private void AddNewButton(HBoxContainer row, string action) {
		TextureButton button = new();
		button.Hide();
		TextureLoader.SetButtonTextures(button, "ui.unit_control." + action);
		button.Pressed += () => {
			EmitSignal(SignalName.ActionRequested, action);
			button.ReleaseFocus();
		};

		// Add a tooltip to the button to explain what it does, and ensure that
		// the tooltip is readable.
		string? tooltipText = C7Action.ToTooltip(action);
		if (tooltipText != null) {
			button.TooltipText = tooltipText;

			var customTheme = new Theme();
			customTheme.SetStylebox("panel", "TooltipPanel", TemporaryPopup.TooltipStyleBox());
			customTheme.SetColor("font_color", "TooltipLabel", Colors.White);
			button.Theme = customTheme;
		}

		row.AddChild(button);
		buttonMap[action] = new ButtonAndTooltip(button, button.TooltipText);
	}

	private void OnNoMoreAutoselectableUnits() {
		this.Visible = false;
	}

	private void OnNewUnitSelected(ParameterWrapper<MapUnit> wrappedMapUnit) {
		UpdateButtons(wrappedMapUnit.Value);
	}

	private void OnUnitMoved(ParameterWrapper<MapUnit> wrappedMapUnit) {
		UpdateButtons(wrappedMapUnit.Value);
	}

	private void UpdateButtons(MapUnit unit) {
		if (!unit.CanBeActive()) return;
		// Reset the visibility and tooltip whenever the unit changes.
		foreach (ButtonAndTooltip btt in buttonMap.Values) {
			btt.button.Visible = false;
			btt.button.TooltipText = btt.baseTooltip;
		}

		var unitActions = unit.GetAvailableActions().Select(C7Action.ToActionString);

		// Mark all the buttons corresponding to the unit's available actions
		// as visible. We do this rather than using the unit prototype's actions
		// so that we don't display buttons that do nothing - we don't want to
		// show the "road" button if we can't build a road, etc.
		foreach (string actionKey in unitActions) {
			if (buttonMap.TryGetValue(actionKey, out ButtonAndTooltip value)) {
				value.button.Visible = true;
			} else {
				log.Warning("Could not find button " + actionKey);
			}
		}

		// Do the same thing for each available terraform, but update the button
		// tooltip to include how many turns it will take.
		foreach (Terraform terraform in unit.GetAvailableTerraforms()) {
			string actionKey = terraform.UIAction;
			if (buttonMap.TryGetValue(actionKey, out ButtonAndTooltip value)) {
				value.button.Visible = true;
				value.button.TooltipText = $"{value.baseTooltip} ({unit.TurnsToCompleteTerraform(terraform)} turns)";
			} else {
				log.Warning("Could not find button " + actionKey);
			}
		}

		this.Visible = true;
	}
}
