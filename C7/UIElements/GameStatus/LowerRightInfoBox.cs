using Godot;
using C7GameData;
using Serilog;
using C7Engine;

[GlobalClass]
[Tool]
public partial class LowerRightInfoBox : Civ3TextureRect {
	private ILogger log = LogManager.ForContext<LowerRightInfoBox>();

	public Game game { get; set; }

	private const int fontSize = 12;
	private const float offsetUnitThumbnailX = 14;
	private const float offsetUnitThumbnailY = 16;

	private TextureRect boxRightRectangle = new();
	private TextureButton boxRightRectangleButton = new();

	private TextureButton nextTurnButton = new();
	private ImageTexture nextTurnOnTexture;
	private ImageTexture nextTurnOffTexture;
	private ImageTexture nextTurnBlinkTexture;

	private Label unitRank = new();
	private Label unitType = new();
	private Label attackDefenseMovement = new();
	private Label terrainType = new();

	private Label civAndGovt = new();
	private Label yearAndGold = new();
	private Label scienceProgress = new();

	private Label suggestion = new();

	private Sprite2D unitPlaceholder = new();
	private Sprite2D unitTintPlaceholder = new();

	private Timer blinkingTimer = new Timer();
	private bool timerStarted = false;  //This "isStopped" returns false if it's never been started.  So we need this to know if we've ever started it.

	private float extraXOffset = 7;

	[Signal] public delegate void BlinkyEndTurnButtonPressedEventHandler();
	[Signal] public delegate void CenterCameraOnActiveUnitEventHandler();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		unitRank.AddThemeFontSizeOverride("font_size", fontSize);
		unitType.AddThemeFontSizeOverride("font_size", fontSize);
		attackDefenseMovement.AddThemeFontSizeOverride("font_size", fontSize);
		terrainType.AddThemeFontSizeOverride("font_size", fontSize);
		civAndGovt.AddThemeFontSizeOverride("font_size", fontSize);
		yearAndGold.AddThemeFontSizeOverride("font_size", fontSize);
		scienceProgress.AddThemeFontSizeOverride("font_size", fontSize);
		suggestion.AddThemeFontSizeOverride("font_size", fontSize);

		this.CreateUI();
		PleaseWait();
	}

	private void CreateUI() {
		ImageTexture boxRight = TextureLoader.Load("lower_right_infobox.box");
		boxRightRectangle = new TextureRect();
		boxRightRectangle.Texture = boxRight;
		boxRightRectangle.SetPosition(new Vector2(0, 0));
		AddChild(boxRightRectangle);

		// An "invisible" button covering the inside area of the box so we can register the click
		// and center the camera on the unit or end the turn
		boxRightRectangleButton.SetSize(new Vector2(228, 108));
		boxRightRectangleButton.SetPosition(new Vector2(40, 17));
		// Don't let Enter/Space activate this button via ui_accept; the end_turn
		// input action already handles those keys, otherwise the turn can advance twice.
		boxRightRectangleButton.FocusMode = FocusModeEnum.None;
		AddChild(boxRightRectangleButton);
		boxRightRectangleButton.Pressed += HandleBoxClick;

		nextTurnOffTexture = TextureLoader.Load("lower_right_infobox.next_turn.off");
		nextTurnOnTexture = TextureLoader.Load("lower_right_infobox.next_turn.on");
		nextTurnBlinkTexture = TextureLoader.Load("lower_right_infobox.next_turn.blink");

		nextTurnButton.TextureNormal = nextTurnOffTexture;
		nextTurnButton.TextureHover = nextTurnOnTexture;
		nextTurnButton.SetPosition(new Vector2(0, 0));
		// Don't let Enter/Space activate this button via ui_accept; the end_turn
		// input action already handles those keys, otherwise the turn can advance twice.
		nextTurnButton.FocusMode = FocusModeEnum.None;
		AddChild(nextTurnButton);
		nextTurnButton.Pressed += TurnEnded;


		// Unit info
		unitType.Text = "Settler";
		unitType.HorizontalAlignment = HorizontalAlignment.Right;
		unitType.SetPosition(new Vector2(0, 18));
		unitType.AnchorRight = 1.0f;
		unitType.OffsetRight = -35;
		boxRightRectangle.AddChild(unitType);

		unitRank.Text = "Regular";
		unitRank.HorizontalAlignment = HorizontalAlignment.Right;
		unitRank.SetPosition(new Vector2(0, 32));
		unitRank.AnchorRight = 1.0f;
		unitRank.OffsetRight = -35;
		unitRank.Visible = false;
		boxRightRectangle.AddChild(unitRank);

		attackDefenseMovement.Text = "0.0. 1/1";
		attackDefenseMovement.HorizontalAlignment = HorizontalAlignment.Right;
		attackDefenseMovement.SetPosition(new Vector2(0, 32));
		attackDefenseMovement.AnchorRight = 1.0f;
		attackDefenseMovement.OffsetRight = -35;
		boxRightRectangle.AddChild(attackDefenseMovement);

		terrainType.Text = "Grassland";
		terrainType.HorizontalAlignment = HorizontalAlignment.Right;
		terrainType.SetPosition(new Vector2(0, 46));
		terrainType.AnchorRight = 1.0f;
		terrainType.OffsetRight = -35;
		boxRightRectangle.AddChild(terrainType);

		// Player info
		boxRightRectangle.AddChild(civAndGovt);
		civAndGovt.SetTextAndCenterLabel("Netherlands - Despotism (5.5.0)").AddXOffset(extraXOffset).AddYOffset(80);

		boxRightRectangle.AddChild(yearAndGold);
		yearAndGold.SetTextAndCenterLabel("Turn 0  10 Gold (+0 per turn)").AddXOffset(extraXOffset).AddYOffset(94);

		boxRightRectangle.AddChild(scienceProgress);
		scienceProgress.SetTextAndCenterLabel("").AddXOffset(extraXOffset).AddYOffset(108);

		// End of turn suggestions
		suggestion.HorizontalAlignment = HorizontalAlignment.Right;
		suggestion.SetPosition(new Vector2(0, 25));
		suggestion.AnchorRight = 1.0f;
		suggestion.OffsetRight = -45;
		boxRightRectangle.AddChild(suggestion);

		//Setup up, but do not start, the timer.
		blinkingTimer.OneShot = false;
		blinkingTimer.WaitTime = 0.6f;
		blinkingTimer.Timeout += toggleEndTurnButton;
		AddChild(blinkingTimer);
	}

	private void SetEndOfTurnStatus() {
		UpdateUnitGraphic(MapUnit.NONE);
		HideUnitInfo();
		suggestion.Text = "ENTER or SPACEBAR for next turn";
		suggestion.Visible = true;

		toggleEndTurnButton();

		if (!timerStarted) {
			blinkingTimer.Start();
			log.Debug("Started a timer for blinking");

			timerStarted = true;
		}
	}

	private void toggleEndTurnButton() {
		if (nextTurnButton.TextureNormal == nextTurnOnTexture) {
			nextTurnButton.TextureNormal = nextTurnBlinkTexture;
			suggestion.Visible = true;
		} else {
			nextTurnButton.TextureNormal = nextTurnOnTexture;
			suggestion.Visible = false;
		}
	}

	private void StopToggling() {
		nextTurnButton.TextureNormal = nextTurnOffTexture;
		blinkingTimer.Stop();
		timerStarted = false;
		suggestion.Visible = false;
	}

	private void PleaseWait() {
		HideUnitInfo();
		suggestion.Text = "Please wait...";
		suggestion.Visible = true;
	}

	private void TurnEnded() {
		log.Debug("Emitting the blinky button pressed signal");
		EmitSignal(SignalName.BlinkyEndTurnButtonPressed);
	}

	private void UpdateUnitInfo(MapUnit unit, TerrainType terrain) {
		if (!unit.CanBeActive()) return;

		bool showRank = false;

		terrainType.Text = terrain.DisplayName;
		if (unit.location.HasCity() && unit.owner == unit.location.cityAtTile.owner) {
			terrainType.Text = unit.location.cityAtTile.name;
		}
		if (unit.IsCombatUnit()) {
			showRank = true;
			unitRank.Text = unit.experienceLevel.displayName;
			attackDefenseMovement.SetPosition(new Vector2(0, 46));
			terrainType.SetPosition(new Vector2(0, 60));
		} else {
			attackDefenseMovement.SetPosition(new Vector2(0, 32));
			terrainType.SetPosition(new Vector2(0, 46));
		}
		unitType.Text = unit.GetDisplayName();
		string movementPointsRemaining = unit.movementPoints.canMove ? "" + $"{(unit.movementPoints.getMixedNumber())}" : "0";
		string bombardText = "";
		if (unit.unitType.bombard > 0) {
			bombardText = $"({unit.unitType.bombard})";
		}
		attackDefenseMovement.Text = $"{unit.unitType.attack}{bombardText}.{unit.unitType.defense} {movementPointsRemaining}/{unit.unitType.movement}";

		suggestion.Visible = false;

		ShowUnitInfo(showRank);
	}

	private void HideUnitInfo() {
		terrainType.Visible = false;
		unitType.Visible = false;
		unitRank.Visible = false;
		attackDefenseMovement.Visible = false;
		unitPlaceholder.Visible = false;
		unitTintPlaceholder.Visible = false;
	}
	private void ShowUnitInfo(bool showRank) {
		terrainType.Visible = true;
		unitType.Visible = true;
		unitRank.Visible = showRank;
		attackDefenseMovement.Visible = true;
		unitPlaceholder.Visible = true;
		unitTintPlaceholder.Visible = true;
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint())
			return;

		// Update our information each time we're drawn, just like the tile and
		// city scenes.
		EngineStorage.ReadGameData((GameData gD) => {
			Player player = gD.GetFirstHumanPlayer();

			// Gold per turn and turn indicator.
			{
				int turnNumber = TurnHandling.GetTurnNumber();
				int gold = player.gold;
				int goldPerTurn = player.CalculateGoldPerTurn();

				var turnText = gD.timeOptions.GetDisplayTime(turnNumber);
				var gptText = $"{(goldPerTurn >= 0 ? "+" : "")}{goldPerTurn}";
				yearAndGold.SetTextAndCenterLabel($"{turnText}  {gold} Gold ({gptText} per turn)").AddXOffset(extraXOffset);
			}

			// Tech progress.
			scienceProgress.SetTextAndCenterLabel(player.SummarizeScience(gD)).AddXOffset(extraXOffset);

			// Civ and government.
			civAndGovt.SetTextAndCenterLabel($"{player.civilization.name} - {player.government.name} (5.5.0)").AddXOffset(extraXOffset);
		});

		base._Process(delta);
	}

	private void OnNewUnitSelected(ParameterWrapper<MapUnit> wrappedMapUnit) {
		MapUnit unit = wrappedMapUnit.Value;
		log.Information("Selected unit: " + unit + " at " + unit.location);
		StopToggling();
		UpdateUnitGraphic(unit);
		UpdateUnitInfo(unit, unit.location.overlayTerrainType);
	}

	private void UpdateUnitGraphic(MapUnit unit) {
		if (this.GetChildren().Contains(unitPlaceholder))
			this.RemoveChild(unitPlaceholder);
		if (this.GetChildren().Contains(unitTintPlaceholder))
			this.RemoveChild(unitTintPlaceholder);

		if (!MapUnit.IsMapUnitValid(unit)) {
			return;
		}

		(unitPlaceholder, unitTintPlaceholder) = SpriteUtils.GetUnitSprites(game, unit);

		var unitSpritePosition = new Vector2(
			boxRightRectangle.Texture.GetWidth() / 2f - offsetUnitThumbnailX,
			boxRightRectangle.Texture.GetHeight() / 2f - offsetUnitThumbnailY);

		unitPlaceholder.Position = unitSpritePosition;
		AddChild(unitPlaceholder);

		unitTintPlaceholder.Position = unitSpritePosition;
		AddChild(unitTintPlaceholder);
	}

	private void HandleBoxClick() {
		// When the turn can be ended, the click on the box is like clicking on the blinky button
		if (timerStarted) {
			TurnEnded();
			return;
		}
		// Otherwise we can center the camera to the unit currently active
		EmitSignal(SignalName.CenterCameraOnActiveUnit);
	}

	private void OnUnitMoved(ParameterWrapper<MapUnit> wrappedMapUnit) {
		MapUnit unit = wrappedMapUnit.Value;
		UpdateUnitInfo(unit, unit.location.overlayTerrainType);
	}
}
