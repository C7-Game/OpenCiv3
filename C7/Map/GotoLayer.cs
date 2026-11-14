using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using static C7GameData.MapUnit;

namespace C7.Map;
// The layer responsible for drawing the cursor when the player is selecting a
// move via the "goto" command.
public class GotoLayer : LooseLayer {
	public GotoLayer() {
		// Load the font we'll use for the goto move counter.
		//
		// We skip the cache so that we can change the size without affecting other
		// code using the same font.
		//
		// We use FixedSize so Godot can calculate the width of the text for centering.
		gotoLabelFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
		gotoLabelFont.FixedSize = 20;

		whiteFontTheme.DefaultFont = gotoLabelFont;
		whiteFontTheme.SetColor("font_color", "Label", Colors.White);
		whiteFontTheme.SetFontSize("font_size", "Label", 20);

		redFontTheme.DefaultFont = gotoLabelFont;
		redFontTheme.SetColor("font_color", "Label", Colors.Red);
		redFontTheme.SetFontSize("font_size", "Label", 20);
	}

	// The GoTo cursor and label fields.
	private AnimatedSprite2D gotoCursorSprite = null;
	private TextureRect staticCursorRect = null;
	private ImageTexture staticCursor = null;
	private Label gotoLabel = null;
	Theme whiteFontTheme = new();
	Theme redFontTheme = new();
	FontFile gotoLabelFont = new();

	public void DrawStaticGoToCursor(LooseView looseView, Vector2 position, int moves, bool attackingMove) {
		gotoCursorSprite?.Hide();
		gotoLabel?.Hide();
		staticCursorRect?.Hide();
		if (staticCursor == null) {
			staticCursor = (ImageTexture)TextureLoader.LoadAnimation("animations.cursor", "cursor").GetFrameTexture("cursor", 1);
			TextureRect tr = new() { Texture = staticCursor};
			staticCursorRect = tr;

			gotoLabel = new() {
				Theme = whiteFontTheme
			};

			looseView.AddChild(staticCursorRect);
			looseView.AddChild(gotoLabel);
		}

		staticCursorRect.Position = position - new Vector2(staticCursor.GetWidth(), staticCursor.GetHeight()) / 2;

		gotoLabel.Theme = whiteFontTheme;
		gotoLabel.Text = moves > 0 || !attackingMove ? moves.ToString() : " ";
		Vector2 labelSize = gotoLabelFont.GetStringSize(gotoLabel.Text);
		gotoLabel.Position = position - labelSize / 2;

		staticCursorRect.Show();
		gotoLabel.Show();
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		// Hide the cursor if it has been initialized
		gotoCursorSprite?.Hide();
		staticCursorRect?.Hide();
		gotoLabel?.Hide();

		looseView.mapView.game.animationController.updateAnimations();
	}

	private GotoInfo lastGotoInfo = null;
	private Intent lastintent = Intent.Disabled;

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		MapUnit unit = looseView.mapView.game.CurrentlySelectedUnit;
		// When no unit is selected, at the end of a turn for example, we don't need to draw anything
		if (looseView.mapView.game.gotoInfo == null || !MapUnit.IsMapUnitValid(unit)) {
			return;
		}

		GotoInfo gotoInfo = looseView.mapView.game.gotoInfo;
		Tile unitOriginTile = unit.location;

		if (gotoInfo.destinationTile == tile) {
			DrawStaticGoToCursor(looseView, tileCenter, 0, true);
		}

		Intent intent = lastintent;

		if (gotoInfo == lastGotoInfo) {
			intent = lastintent;
		} else {
			unit.CanEnterForcefully(gotoInfo.destinationTile, out var outIntent);
			lastintent = intent = outIntent;
			lastGotoInfo = gotoInfo;
		}

		if (gotoInfo.path == null) return;

		if (gotoInfo.path.path.Count <= 1
								   && (intent == Intent.Fight
									   || intent == Intent.WarDeclaration
									   || intent == Intent.NoticeAlliance
									   || intent == Intent.NoticeCity
									   || intent == Intent.NoticeUnit))
			return;

		List<Tile> tiles = new List<Tile>();
		tiles.Add(unitOriginTile);
		tiles.AddRange(gotoInfo.path.path);

		for (int i = 0; i < tiles.Count - 1; i++) {
			Tile currentTile = tiles[i];
			Tile nextTile = tiles[i + 1];

			// Variable width of the line to account for various camera zoom levels.
			// The end result should look pretty much the same to the player on any zoom level.
			float lineWidth = Math.Max(1f / looseView.mapView.cameraZoom, 1f);

			// We draw only the lines between tiles that are in our visible area
			// with one or two tile buffer on both axis. How many is determined in the MapView
			// by the getVisibleRegion().
			// This is not only saving draw calls which is great, but there is a bigger reason.
			// Imagine just loading the game, not moving the camera at all
			// and press G to instruct a unit to move somewhere.
			// The path is precomputed by another module, so we know the route to our destination.
			// But if the path goes outside the visible area + the buffer, there is an issue.
			// The visible region is only what you see at the screen plus the tiny buffer.
			// These are the only tiles the player has seen and calculated the centers of, so far.
			// If we try to draw lines between tiles that are outside this visible region, we will
			// get an error because we don't know yet what these centers are. We either have to move the camera
			// and calculate them, or precompute a huge buffer of tiles which is not practical at all.
			if (looseView.tileCenters.TryGetValue(currentTile, out Vector2 currentTileCenter)
				&& looseView.tileCenters.TryGetValue(nextTile, out Vector2 nextTileCenter)) {
				staticCursorRect?.Hide();
				looseView.DrawLine(currentTileCenter, nextTileCenter, Colors.Red, width: lineWidth);
				DrawStaticGoToCursor(looseView, nextTileCenter, gotoInfo.moveCost, gotoInfo.attackingMove);
			}
		}
	}
}
