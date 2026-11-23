using System;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;
using static Game;

/* This class controls the unit selection during the player turn.  It
   handles automatic unit selection in the _Process method, and
   provides methods (SetSelectedUnit, SetNextUnit) for manual
   selection. */
[GlobalClass]
public partial class UnitSelector : Node {
	private ILogger log = LogManager.ForContext<UnitSelector>();

	[Signal] public delegate void NewAutoselectedUnitEventHandler();
	[Signal] public delegate void NoMoreAutoselectableUnitsEventHandler();

	[Export] private Game game;

	//The selected unit.  May be changed by clicking on a unit or the next unit being auto-selected after orders are given for the current one.
	public MapUnit CurrentlySelectedUnit { get; private set; } = MapUnit.NONE;

	// Normally if the currently selected unit (CSU) becomes fortified, we advance to the next autoselected unit. If this flag is set, we won't do
	// that. This is useful so that the unit autoselector can be prevented from interfering with the player selecting fortified units.
	private bool KeepCSUWhenFortified = false;

	private bool NoMoreAutoselectableUnitsEmitted = false;

	// Automatic selection waits until this time (Time.GetTicksMsec), so a
	// message about the current unit can be read before the view moves on.
	private ulong autoselectHeldUntilMsec = 0;

	// Whether there has already been a pause since the current unit was
	// selected (e.g. for a goody hut message), so moving on needs no other.
	private bool pausedForCurrentUnit = false;

	// The next unit, picked but not yet selected while the view pauses
	// before the camera moves to it.
	private MapUnit pendingNextUnit = null;

	// How long to pause before the camera jumps to a distant next unit.
	private const double CameraJumpPauseSeconds = 0.5;

	public override void _Ready() {
		game.PlayerTurnEnd += () => {
			NoMoreAutoselectableUnitsEmitted = false;
			CurrentlySelectedUnit = MapUnit.NONE;
			autoselectHeldUntilMsec = 0;
			pausedForCurrentUnit = false;
			pendingNextUnit = null;
		};
	}

	public override void _Process(double delta) {
		if (game.CurrentState != GameState.PlayerTurn) return;
		if (NoMoreAutoselectableUnitsEmitted) return;
		if (EngineStorage.HasPendingAnimations()) return;
		// Let the UI handle what just happened (e.g. a popped goody hut,
		// which holds autoselection) before deciding to move on.
		if (EngineStorage.HasPendingMessagesToUI()) return;
		if (game.HasVisibleModal()) return;
		if (Time.GetTicksMsec() < autoselectHeldUntilMsec) return;

		// If no unit is selected, move to the next one
		if (CurrentlySelectedUnit == MapUnit.NONE) {
			SetNextUnit();
			return;
		}

		// If the selected unit is unfortified, prepare to autoselect the next one if it becomes fortified
		if (!CurrentlySelectedUnit.isFortified)
			KeepCSUWhenFortified = false;

		if (ShouldSkipUnit(CurrentlySelectedUnit))
			MoveOnToNextUnit();
	}

	// Selects the next unit, first pausing briefly if the camera would have
	// to jump to reach it and there hasn't been a pause already.
	private void MoveOnToNextUnit() {
		if (pendingNextUnit != null) {
			MapUnit next = pendingNextUnit;
			pendingNextUnit = null;
			if (next.movementPoints.canMove && next.hitPointsRemaining > 0) {
				SetSelectedUnit(next);
			} else {
				SetNextUnit();
			}
			return;
		}

		MapUnit candidate = UnitInteractions.getNextSelectedUnit();
		if (!pausedForCurrentUnit && candidate != MapUnit.NONE && game.WouldMoveCameraTo(candidate.location)) {
			pendingNextUnit = candidate;
			HoldAutoselect(CameraJumpPauseSeconds);
			return;
		}
		SetSelectedUnit(candidate);
	}

	private bool ShouldSkipUnit(MapUnit unit) {
		bool outOfMovesOrDead = !unit.movementPoints.canMove || unit.hitPointsRemaining <= 0;
		bool notAttentionWorthy = !game.animationController.animTracker
			.getUnitAppearance(unit)
			.DeservesPlayerAttention();

		bool shouldSkipForMovement = outOfMovesOrDead && notAttentionWorthy;
		bool shouldSkipFortified = unit.isFortified && !KeepCSUWhenFortified;
		bool shouldSkipAutomated = unit.isAutomated;

		return shouldSkipForMovement || shouldSkipFortified || shouldSkipAutomated;
	}

	// Delays automatic selection of the next unit, keeping any longer hold
	// already in place. Selecting a unit manually ends the hold.
	public void HoldAutoselect(double seconds) {
		autoselectHeldUntilMsec = Math.Max(autoselectHeldUntilMsec, Time.GetTicksMsec() + (ulong)(seconds * 1000));
		pausedForCurrentUnit = true;
	}

	public void SetNextUnit() {
		SetSelectedUnit(UnitInteractions.getNextSelectedUnit());
	}

	/**
	 * Currently (11/14/2021), all unit selection goes through here.
	 * Both code paths are in Game.cs for now, so it's local, but we may
	 * want to change it event driven.
	 *
	 * Returns whether the selected unit has remaining moves.
	 **/
	public bool SetSelectedUnit(MapUnit unit) {
		if ((unit.path?.PathLength() ?? -1) > 0) {
			log.Debug("cancelling path for " + unit);
			unit.path = TilePath.NONE;
		}

		// Ask for confirmation to cancel an active worker's job by clicking on the unit.
		if (unit.WorkerJob != null) {
			var turnsToFinish = unit.TurnsToCompleteTerraform(unit.WorkerJob);
			new MsgDisplayStopWorkerActionPopup(unit, unit.WorkerJob, turnsToFinish).send();
			return unit.WorkerJob == null && unit.movementPoints.remaining >= 1.0f;
		}

		// Allow cancellation automation via clicking on the unit.
		if (unit.isAutomated) {
			unit.isAutomated = false;
			unit.currentAI = null;
		}

		if (unit != MapUnit.NONE && !unit.movementPoints.canMove) {
			return false;
		}

		this.CurrentlySelectedUnit = unit;
		this.KeepCSUWhenFortified = unit.isFortified; // If fortified, make sure the autoselector doesn't immediately skip past the unit
		this.autoselectHeldUntilMsec = 0;
		this.pausedForCurrentUnit = false;
		this.pendingNextUnit = null;

		if (unit != MapUnit.NONE) {
			game.ensureLocationIsInView(unit.location);
		}

		// Also emit the signal for a new unit being selected, so other areas such as Game Status and Unit Buttons can update
		if (CurrentlySelectedUnit != MapUnit.NONE) {
			unit.Wake();
			NoMoreAutoselectableUnitsEmitted = false;
			ParameterWrapper<MapUnit> wrappedUnit = new(CurrentlySelectedUnit);
			PreLoadUnitAnimationThumbnail(wrappedUnit.Value);
			EmitSignal(SignalName.NewAutoselectedUnit, wrappedUnit);
			return true;
		}

		if (!NoMoreAutoselectableUnitsEmitted) {
			NoMoreAutoselectableUnitsEmitted = true;
			EmitSignal(SignalName.NoMoreAutoselectableUnits);
		}

		return false;
	}

	private void PreLoadUnitAnimationThumbnail(MapUnit unit) {
		game.animationController.civ3AnimData.GetAnimationFrameAndTintTextures(unit);
	}
}
