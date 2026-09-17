using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;

public partial class TileInfoPopup : Popup {
	private readonly Game _game;
	private readonly Tile _tile;
	private readonly Vector2 _position;
	private readonly float _zoom;

	private TextureRect boxTextureRect = new();
	private TextureButton closeButton = new();

	private Label terrainLabel = new();
	private Label overlayLabel = new();
	private Label resourceLabel = new();

	private Label foodLabel = new();
	private Label shieldLabel = new();
	private Label goldLabel = new();

	private List<Label> _labels = new();

	private PopupOverlay _overlay;

	public TileInfoPopup(Game game, Tile tile, Vector2 position, float zoom) {
		_game = game;
		_tile = tile;
		_position = position;
		_zoom = zoom;

		margins = new Margins();
		alignment = BoxContainer.AlignmentMode.Begin;

		_labels.AddRange([terrainLabel, overlayLabel, resourceLabel, foodLabel, shieldLabel, goldLabel]);

		InitBoxTexture();
		InitCloseButton();
	}

	private void InitBoxTexture() {
		ImageTexture boxTexture = TextureLoader.Load("tileinfo.box");
		boxTextureRect = new TextureRect();
		boxTextureRect.Texture = boxTexture;
	}

	private void InitCloseButton() {
		ImageTexture xTexture = TextureLoader.Load("ui.cancel.normal");
		ImageTexture xHover = TextureLoader.Load("ui.cancel.hover");
		ImageTexture xPressed = TextureLoader.Load("ui.cancel.pressed");

		closeButton.TextureNormal = xTexture;
		closeButton.TextureHover = xHover;
		closeButton.TexturePressed = xPressed;

		closeButton.ActionMode = BaseButton.ActionModeEnum.Release;
		closeButton.Pressed += Close;
	}

	private void DrawTileInfoBox(Vector2 tileCenter) {
		var scale = new Vector2(_zoom, _zoom);

		// Re-position box
		var boxWidth = boxTextureRect.Texture.GetWidth() * _zoom;
		var boxHeight = boxTextureRect.Texture.GetHeight() * _zoom;
		var offset = new Vector2(0, 32 * _zoom); // tileHeight / 2
		var center = new Vector2(boxWidth / 2f, boxHeight / 2f);
		boxTextureRect.SetPosition(tileCenter - center + offset);
		boxTextureRect.SetScale(scale);

		// Re-position close button
		var buttonWidth = closeButton.TextureNormal.GetWidth() * _zoom;
		var buttonOffset = new Vector2(boxWidth - buttonWidth, 0);
		var buttonMargin = 10 * _zoom;
		closeButton.SetPosition(boxTextureRect.Position + buttonOffset + new Vector2(-buttonMargin, buttonMargin));
		closeButton.SetScale(scale);

		// Re-position labels
		var xStep = 100 * _zoom;
		var yStep = 15 * _zoom;
		var contentOffset = new Vector2(25, 105) * _zoom;
		var contentAnchor = boxTextureRect.Position + contentOffset;
		terrainLabel.SetPosition(contentAnchor + new Vector2(0, 0));
		overlayLabel.SetPosition(contentAnchor + new Vector2(0, yStep));
		resourceLabel.SetPosition(contentAnchor + new Vector2(0, 2 * yStep));
		foodLabel.SetPosition(contentAnchor + new Vector2(xStep, 0));
		shieldLabel.SetPosition(contentAnchor + new Vector2(xStep, yStep));
		goldLabel.SetPosition(contentAnchor + new Vector2(xStep, 2 * yStep));

		var fontSize = (int) Math.Ceiling(12 * _zoom);
		foreach (var label in _labels) {
			label.AddThemeFontSizeOverride("font_size", fontSize);
		}

		// Assemble
		AddChild(boxTextureRect);
		AddChild(closeButton);
		foreach (var label in _labels)
			AddChild(label);

		// Show elements
		boxTextureRect.Show();
		closeButton.Show();
		foreach (var label in _labels)
			label.Show();
	}

	private void SetTileInfoContent(Tile tile, GameData gameData) {
		var player = gameData.GetFirstHumanPlayer();
		var isObserverMode = gameData.observerMode;

		if (player.tileKnowledge.isTileKnown(tile) || isObserverMode) {
			terrainLabel.Text = tile.baseTerrainType?.DisplayName ?? "";
			overlayLabel.Text = tile.overlayTerrainType?.DisplayName ?? "";
			resourceLabel.Text = tile.Resource?.Name ?? "";
			foodLabel.Text = $"Food: {tile.FoodYield(player).baseYield}";
			shieldLabel.Text = $"Shields: {tile.ProductionYield(player).baseYield}";
			goldLabel.Text = $"Gold: {tile.CommerceYield(player).baseYield}";

			if (tile.baseTerrainType == tile.overlayTerrainType)
				overlayLabel.Text = "";

			if (tile.Resource != null && !player.KnowsAboutResource(tile.Resource) && !isObserverMode)
				resourceLabel.Text = "";

		} else {
			terrainLabel.Text = "";
			overlayLabel.Text = "No information available";
			resourceLabel.Text = "";
			foodLabel.Text = "";
			shieldLabel.Text = "";
			goldLabel.Text = "";
		}
	}

	private void Close() {
		_game.HideTileInfo();
	}

	public override void _Ready() {
		base._Ready();

		DrawTileInfoBox(_position);

		EngineStorage.ReadGameData((GameData gameData) => {
			SetTileInfoContent(_tile, gameData);
		});

		var overlay = GetParent<PopupOverlay>();
		_overlay = overlay;
		overlay.Click += Close; // a click outside the box means close
	}

	public override void _ExitTree() {
		if (_overlay != null && IsInstanceValid(_overlay)) {
			_overlay.Click -= Close;
		}
		base._ExitTree();
	}

	public override void _GuiInput(InputEvent @event) {
		// ignore clicks inside box
		if (Visible && @event is InputEventMouseButton ev && ev.Pressed) {
			GetViewport().SetInputAsHandled();
		}
	}
}
