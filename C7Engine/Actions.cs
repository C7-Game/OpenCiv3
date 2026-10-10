using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// The strings for each action correspond to values in project.godot for keyboard shortcuts
public static class C7Action {
	public const string EndTurn = "end_turn";
	public const string Escape = "escape";

	public const string MoveUnitSouthwest = "move_unit_southwest";
	public const string MoveUnitSouth = "move_unit_south";
	public const string MoveUnitSoutheast = "move_unit_southeast";
	public const string MoveUnitWest = "move_unit_west";
	public const string MoveUnitEast = "move_unit_east";
	public const string MoveUnitNorthwest = "move_unit_northwest";
	public const string MoveUnitNorth = "move_unit_north";
	public const string MoveUnitNortheast = "move_unit_northeast";

	public const string ToggleAnimations = "toggle_animations";
	public const string EnableTempAnimations = "enable_temp_animations";
	public const string ToggleGrid = "toggle_grid";
	public const string ToggleCoordinates = "toggle_coordinates";
	public const string ToggleZoom = "toggle_zoom";
	public const string OpenPreferences = "open_preferences"; // Ctrl-P

	public const string ShowDomesticAdvisor = "show_domestic_advisor"; // F1
	public const string ShowTradeAdvisor = "show_trade_advisor";
	public const string ShowMilitaryAdvisor = "show_military_advisor";
	public const string ShowForeignAdvisor = "show_foreign_advisor";
	public const string ShowCulturalAdvisor = "show_cultural_advisor";
	public const string ShowScienceAdvisor = "show_science_advisor"; // F6

	public const string ShowWondersView = "show_wonders_view"; // F7
	public const string ShowVictoryStatusView = "show_victory_status_view";
	public const string ShowPalaceView = "show_palace_view";
	public const string ShowSpaceRaceView = "show_space_race_view";
	public const string ShowDemographicsView = "show_demographics_view"; // F11

	public const string UnitBombard = "unit_bombard";
	public const string UnitBuildCity = "unit_build_city";
	public const string UnitBuildRoad = "unit_build_road";
	public const string UnitBuildMine = "unit_build_mine";
	public const string UnitIrrigate = "unit_irrigate";
	public const string UnitBuildRailroad = "unit_build_railroad";
	public const string UnitBuildFortress = "unit_build_fortress";
	public const string UnitPlantForest = "unit_plant_forest";
	public const string UnitClearForest = "unit_clear_forest";
	public const string UnitClearWetlands = "unit_clear_wetlands";
	public const string UnitClearDamage = "unit_clear_damage";
	public const string UnitBuildAirfield= "unit_build_airfield";
	public const string UnitBuildRadarTower = "unit_build_radar_tower";
	public const string UnitBuildOutpost = "unit_build_outpost";
	public const string UnitBuildBarricade = "unit_build_barricade";
	public const string UnitAutomate = "unit_automate";
	public const string UnitDisband = "unit_disband";
	public const string UnitExplore = "unit_explore";
	public const string UnitFortify = "unit_fortify";
	public const string UnitGoto = "unit_goto";
	public const string UnitHold = "unit_hold";
	public const string UnitSentry = "unit_sentry";
	public const string UnitSentryEnemyOnly = "unit_sentry_enemy_only";
	public const string UnitWait = "unit_wait";
	public const string UnitLoad = "unit_load";
	public const string UnitUnload = "unit_unload";

	private static readonly Dictionary<string, UnitAction> toUnitAction = new() {
		[UnitBuildCity] = UnitAction.BuildCity,
		[UnitBombard] = UnitAction.Bombard,
		[UnitHold] = UnitAction.Hold,
		[UnitLoad] = UnitAction.Load,
		[UnitUnload] = UnitAction.Unload,
		[UnitWait] = UnitAction.Wait,
		[UnitFortify] = UnitAction.Fortify,
		[UnitDisband] = UnitAction.Disband,
		[UnitGoto] = UnitAction.Goto,
		[UnitExplore] = UnitAction.Explore,
		[UnitAutomate] = UnitAction.Automate
	};

	private static readonly Dictionary<string, string> toTooltip = new() {
		[UnitBombard] = "Bombard",
		[UnitBuildCity] = "Build City",
		[UnitBuildRoad] = "Build Road",
		[UnitBuildMine] = "Build Mine",
		[UnitIrrigate] = "Irrigate",
		[UnitBuildRailroad] = "Build Railroad",
		[UnitBuildFortress] = "Build Fortress",
		[UnitPlantForest] = "Plant Forest",
		[UnitClearForest] = "Clear Forest",
		[UnitClearWetlands] = "Clear Wetlands",
		[UnitClearDamage] = "Clear Damage",
		[UnitBuildAirfield] = "Build Airfield",
		[UnitBuildRadarTower] = "Build Radar Tower",
		[UnitBuildOutpost] = "Build Outpost",
		[UnitBuildBarricade] = "Build Barricade",
		[UnitAutomate] = "Automate",
		[UnitDisband] = "Disband",
		[UnitExplore] = "Explore",
		[UnitFortify] = "Fortify",
		[UnitGoto] = "Goto",
		[UnitHold] = "Hold",
		[UnitSentry] = "Sentry",
		[UnitSentryEnemyOnly] = "Sentry Enemy Only",
		[UnitWait] = "Wait",
		[UnitLoad] = "Load",
		[UnitUnload] = "Unload"
	};

	public static UnitAction? ToUnitAction(string action) {
		return toUnitAction.TryGetValue(action, out var result) ? result : null;
	}

	public static string ToActionString(UnitAction unitAction) {
		return toUnitAction.FirstOrDefault(kv => kv.Value == unitAction).Key;
	}

	// This method transforms an action string into a TileDirection.
	// A null value will be returned if the conversion is unsuccessful.
	public static TileDirection? ToTileDirection(string action) {
		return action switch {
			MoveUnitSouthwest => TileDirection.SOUTHWEST,
			MoveUnitSouth => TileDirection.SOUTH,
			MoveUnitSoutheast => TileDirection.SOUTHEAST,
			MoveUnitWest => TileDirection.WEST,
			MoveUnitEast => TileDirection.EAST,
			MoveUnitNorthwest => TileDirection.NORTHWEST,
			MoveUnitNorth => TileDirection.NORTH,
			MoveUnitNortheast => TileDirection.NORTHEAST,
			_ => null,
		};
	}

	public static Terraform? ToTerraform(string action) {
		return EngineStorage.gameData.Terraforms.FirstOrDefault(tf => tf.UIAction == action);
	}

	public static string? ToTooltip(string action) {
		return toTooltip.TryGetValue(action, out var result) ? result : null;
	}
}
