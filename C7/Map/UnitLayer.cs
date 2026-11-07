using System;
using System.Collections.Generic;
using System.Linq;
using C7.Textures;
using C7GameData;
using Godot;

public partial class UnitLayer : LooseLayer {
	private ImageTexture unitMovementIndicators;

	// The unit animations, effect animations, and cursor are all drawn as children attached to the looseView but aren't created and attached in
	// any particular order so we must use the ZIndex property to ensure they're properly layered.
	public const int effectAnimZIndex = 2;
	public const int unitAnimZIndex = 1;
	public const int cursorZIndex = -1;

	public UnitLayer() {
		unitMovementIndicators = TextureLoader.Load("ui.unit_control.movement_indicators");
	}

	public static Color GetHpColor(float remaining, float total) {
		if (remaining >= 0.67f) {
			return Color.Color8(0, 255, 0);
		} else if (remaining >= 0.34f && total > 2) {
			return Color.Color8(255, 255, 0);
		} else {
			return Color.Color8(255, 0, 0);
		}
	}

	// AnimationInstance represents an animation appearing on the screen. It's specific to a unit, action, and direction. AnimationInstances have
	// two components: a ShaderMaterial and a MeshInstance2D. The ShaderMaterial runs the unit shader (created by UnitLayer.getShader) with all
	// the parameters set to a particular texture, civ color, direction, etc. The MeshInstance2D is what's actually drawn by Godot, i.e., what's
	// added to the node tree. AnimationInstances are only active for one frame at a time but they live as long as the UnitLayer. They are
	// retrieved or created as needed by getBlankAnimationInstance during the drawing of units and are hidden & requeued for use at the beginning
	// of each frame.

	// should hold animation players instead of animations
	public partial class AnimationInstance {

		public AnimatedSprite2D sprite;
		public AnimatedSprite2D spriteTint;
		public ShaderMaterial material;

		public void SetPosition(Vector2 position) {
			this.sprite.Position = position;
			this.spriteTint.Position = position;
		}

		public int GetNextFrameByProgress(string animation, float progress) {
			// AnimatedSprite2D has a settable FrameProgress field, which I expected to
			// update the current frame of the animation upon setting, but it did not
			// when I tried it, so instead, calculate what the next frame should be
			// based on the progress.
			int frameCount = this.sprite.SpriteFrames.GetFrameCount(animation);
			int nextFrame = (int)((float)frameCount * progress);
			return nextFrame >= frameCount ? frameCount - 1 : (nextFrame < 0 ? 0 : nextFrame);
		}

		public void SetFrame(int frame) {
			this.sprite.Frame = frame;
			this.spriteTint.Frame = frame;
		}

		public void SetAnimation(string name) {
			this.sprite.Animation = name;
			this.spriteTint.Animation = name;
		}

		public void Show() {
			this.sprite.Show();
			this.spriteTint.Show();
		}

		public void Hide() {
			this.sprite.Hide();
			this.spriteTint.Hide();
		}

		public Vector2 FrameSize(string animation) {
			return this.sprite.SpriteFrames.GetFrameTexture(animation, 0).GetSize();
		}

		public AnimationInstance(LooseView looseView) {
			AnimationManager manager = looseView.mapView.game.animationController.civ3AnimData;

			this.sprite = new AnimatedSprite2D();
			this.sprite.ZIndex = unitAnimZIndex;
			this.sprite.SpriteFrames = manager.spriteFrames;

			this.spriteTint = new AnimatedSprite2D();
			this.spriteTint.ZIndex = unitAnimZIndex;
			this.spriteTint.SpriteFrames = manager.tintFrames;

			this.material = new ShaderMaterial();
			this.material.Shader = GD.Load<Shader>("res://UnitTint.gdshader");
			this.spriteTint.Material = this.material;

			looseView.AddChild(sprite);
			looseView.AddChild(spriteTint);
		}
	}

	private List<AnimationInstance> animInsts = new List<AnimationInstance>();
	private int nextBlankAnimInst = 0;

	// Returns the next unused AnimationInstance or creates & returns a new one if none are available.
	public AnimationInstance getBlankAnimationInstance(LooseView looseView) {
		if (nextBlankAnimInst >= animInsts.Count) {
			animInsts.Add(new AnimationInstance(looseView));
		}
		AnimationInstance inst = animInsts[nextBlankAnimInst];
		nextBlankAnimInst++;
		return inst;
	}

	public void drawUnitAnimFrame(LooseView looseView, MapUnit unit, MapUnit.Appearance appearance, Vector2 tileCenter) {
		AnimationInstance inst = getBlankAnimationInstance(looseView);
		C7Animation unitAnimation = looseView.mapView.game.animationController.civ3AnimData.forUnit(unit, appearance.action);
		unitAnimation.loadSpriteAnimation();

		string animName = AnimationManager.AnimationKey(unit, appearance.action, appearance.direction);

		Vector2 framePosition = GetFramePosition(appearance, unitAnimation, inst, animName, tileCenter);

		inst.SetPosition(framePosition);

		Color civColor = TextureLoader.LoadColor(unit.owner.GetPlayerColor());
		int nextFrame = inst.GetNextFrameByProgress(animName, appearance.progress);
		inst.material.SetShaderParameter("tintColor", new Vector3(civColor.R, civColor.G, civColor.B));

		inst.SetAnimation(animName);
		inst.SetFrame(nextFrame);
		inst.Show();
	}

	private Vector2 GetFramePosition(MapUnit.Appearance appearance, C7Animation unitAnimation, AnimationInstance inst, string animName, Vector2 tileCenter) {
		// 1. Place unit in the center of the tile.
		//	  This places the *center* of the sprite frame at the center point of the tile.
		//
		// 2. Apply animation offset.
		//    This applies the offset of the animation when a unit moves from one tile to another, it "follows" the movement
		//    it's pretty much a noop for the default or other animations that stay in the same tile.
		//
		// 3. Apply the offsets from the flic file to place them correctly in a 240x240 rect
		//    (which starts at the center of the tile extending to the right and bottom).
		//    The offsets we read from the flic file represent the offsets from the top left corner of the frame,
		//    so this is why here we need to add half the width and height since the transforms
		//    in our frames are applied at the center of the image
		//
		// 4. Offset the frame by half the width and height of the original size of the animation (which is usually 240x240 in pixels)
		//    to place it correctly on the tile.
		//
		// 5. Finally add (or subtract if negative in .ini) any custom offset from the ini file

		Vector2 animOffset = MapView.cellSize * new Vector2(appearance.offsetX, appearance.offsetY);
		Vector2I flicOffset = unitAnimation.GetFlicAnimationOffset();
		Vector2 flicOffsetWithAlignedFrame = new ((inst.FrameSize(animName).X / 2) + flicOffset.X,
												  (inst.FrameSize(animName).Y / 2) + flicOffset.Y);
		Vector2I flicOriginalSize = unitAnimation.GetFlicAnimationOriginalSize();

		return tileCenter + animOffset + flicOffsetWithAlignedFrame - (flicOriginalSize / 2);
	}

	public void drawEffectAnimFrame(LooseView looseView, C7Animation anim, float progress, Vector2 tileCenter) {
		AnimationInstance inst = getBlankAnimationInstance(looseView);
		inst.sprite.ZIndex = effectAnimZIndex;
		inst.spriteTint.ZIndex = effectAnimZIndex;
		inst.SetPosition(tileCenter);

		anim.loadEffectAnimation();

		string animName = AnimationManager.AnimationKey(anim.effect, anim.action);
		int nextFrame = inst.GetNextFrameByProgress(animName, progress);

		inst.SetAnimation(animName);
		inst.SetFrame(nextFrame);
		inst.Show();
	}

	private AnimatedSprite2D cursorSprite = null;

	public void drawCursor(LooseView looseView, Vector2 position) {
		// Initialize cursor if necessary
		if (cursorSprite == null) {
			cursorSprite = new AnimatedSprite2D();
			cursorSprite.SpriteFrames = TextureLoader.LoadAnimation("animations.cursor", "cursor");
			cursorSprite.Animation = "cursor";
			looseView.AddChild(cursorSprite);
			cursorSprite.Play("cursor");
		}

		cursorSprite.Position = position;
		cursorSprite.Show();
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		// Reset animation instances
		for (int n = 0; n < nextBlankAnimInst; n++) {
			animInsts[n].Hide();
		}
		nextBlankAnimInst = 0;

		// Hide cursor if it's been initialized
		cursorSprite?.Hide();

		looseView.mapView.game.animationController.updateAnimations();
	}

	// Returns which unit should be drawn from among a list of units. The list is assumed to be non-empty.
	public MapUnit selectUnitToDisplay(LooseView looseView, Tile tile, List<MapUnit> units) {
		// From the list, pick out which units are (1) the strongest defender vs the currently selected unit, (2) the currently selected unit
		// itself if it's in the list, and (3) any unit that is playing an animation that the player would want to see.
		// If every regular defender is in some transport, the best defender _is_ a transport

		MapUnit bestDefender = units.FirstOrDefault(u => u.loadedOnUnitId == null) ?? units[0];
		MapUnit selected = null, transporter = null, doingInterestingAnimation = null;
		var currentlySelectedUnit = looseView.mapView.game.CurrentlySelectedUnit;

		foreach (var u in units) {
			if (u.loadedOnUnitId != null && u != currentlySelectedUnit) continue;

			if (u == currentlySelectedUnit) {
				selected = u;
				break;
			}

			if (looseView.mapView.game.animationController.animTracker.getUnitAppearance(u).DeservesPlayerAttention()) {
				doingInterestingAnimation = u;
				break;
			}

			if (u.HasPriorityAsDefender(bestDefender, currentlySelectedUnit))
				bestDefender = u;
		}

		// On water, show the transport of the best defender (hopefully stable)
		if (tile.IsWater()) {
			foreach (var u in units) {
				if (u.CanTransport() && bestDefender.IsLoadedIn(u))
					transporter = u;
			}
		}

		// Prefer showing the selected unit, then animation, then transport, otherwise show the top defender
		return selected ?? doingInterestingAnimation ?? transporter ?? bestDefender;
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (!UnitsVisible(gameData, looseView.mapView.game.controller, tile)) {
			return;
		}

		if (looseView.IsTileCoveredByTileInfo(tile)) {
			return;
		}

		// First draw animated effects. These will always appear over top of units regardless of draw order due to z-index.
		C7Animation tileEffect = looseView.mapView.game.animationController.animTracker.getTileEffect(tile);
		if (tileEffect != null) {
			(_, float progress, _) = looseView.mapView.game.animationController.animTracker.getCurrentActionAndProgress(tile);
			drawEffectAnimFrame(looseView, tileEffect, progress, tileCenter);
		}

		if (tile.unitsOnTile.Count == 0) {
			return;
		}

		var white = Color.Color8(255, 255, 255);

		MapUnit unit = selectUnitToDisplay(looseView, tile, tile.unitsOnTile);
		MapUnit.Appearance appearance = looseView.mapView.game.animationController.animTracker.getUnitAppearance(unit);
		Vector2 animOffset = new Vector2(appearance.offsetX, appearance.offsetY) * MapView.cellSize;

		// If the unit we're about to draw is currently selected, draw the cursor first underneath it
		if ((unit != MapUnit.NONE) && (unit == looseView.mapView.game.CurrentlySelectedUnit)) {
			drawCursor(looseView, tileCenter + animOffset);
		}

		drawUnitAnimFrame(looseView, unit, appearance, tileCenter);

		// TODO: Figure out how we can draw the unit's HP bar above the unit and the cursor

		// Option A: Support all kind of zoom levels. The downside is at large zoom distances, the HP indicators dominate the screen
		// float cameraZoom = Math.Min(looseView.mapView.cameraZoom, 1.0f);

		// Option B: 2 Zoom levels regular, and double size.
		// At larger distances it stays relatively small, but at this point I don't think we need to see the HP
		float cameraZoom = Math.Clamp(looseView.mapView.cameraZoom, 0.5f, 1.0f);

		float offsetXFromCenter = 26;
		Vector2 hpStartingLocation = tileCenter - new Vector2(offsetXFromCenter, 0) + animOffset;

		int maxHp = unit.maxHitPoints;
		float hpIndHeight = GetHpFractionHeight(maxHp) / cameraZoom;
		float hpIndWidth = 2 / cameraZoom;
		float hpBarTotal = (hpIndHeight * maxHp + (maxHp - 1)/cameraZoom);
		Vector2 movementLedCropping = new Vector2(6, 6);
		Vector2 movementLedSize = movementLedCropping / cameraZoom;
		float fortifiedLineExpand = 0.5f / cameraZoom;
		float lineWidth = 1 / cameraZoom;

		int offsetYFromCenter = 8;
		Rect2 hpIndBackgroundRect = new Rect2(hpStartingLocation - new Vector2(0, offsetXFromCenter), Vector2.One);
		if (unit.unitType.attack > 0 || unit.unitType.defense > 0) {
			hpIndBackgroundRect = new Rect2((hpStartingLocation - new Vector2(0, hpBarTotal) - new Vector2(0, offsetYFromCenter)), new Vector2(hpIndWidth, hpBarTotal));
			float hpFraction = (float)unit.hitPointsRemaining / maxHp;
			looseView.DrawRect(hpIndBackgroundRect, Color.Color8(0, 0, 0));
			Color hpColor = GetHpColor(hpFraction, maxHp);
			for (int i = 0; i < unit.hitPointsRemaining; i++) {
				Rect2 hpContentsRect = new Rect2(hpIndBackgroundRect.Position + new Vector2(0, hpBarTotal) - new Vector2(0, hpIndHeight + (hpIndHeight+lineWidth)*i), new Vector2(hpIndWidth, hpIndHeight));
				looseView.DrawRect(hpContentsRect, hpColor);
			}
			if (unit.isFortified) {
				Rect2 fortifiedRect = hpIndBackgroundRect.Grow(fortifiedLineExpand);
				looseView.DrawRect(fortifiedRect, white, false, width: lineWidth);
			}
		}

		// TODO: Maybe add this is as a player configuration for a "harder" mode,
		// where players can't see how many enemy units there are even in tiles that don't have a city.
		// RightClickMenu functionality would need to be made configurable if this gets implemented in the future.
		if (unit.location.HasCity() && unit.owner != looseView.mapView.game.controller)
			return;

		// Draw movement indicator for our units
		if (looseView.mapView.game.controller == unit.owner) {
			int moveIndIndex = (!unit.movementPoints.canMove) ? 4 : ((unit.movementPoints.remaining >= unit.unitType.movement) ? 0 : 2);
			Vector2 moveIndUpperLeft = new Vector2((1 + 7 * moveIndIndex), 1);
			Rect2 moveIndRect = new Rect2(moveIndUpperLeft, movementLedCropping);
			Rect2 screenRect = new Rect2(hpIndBackgroundRect.Position - (new Vector2(2, 6) / cameraZoom), movementLedSize);
			looseView.DrawTextureRectRegion(unitMovementIndicators, screenRect, moveIndRect);
		}

		float lineMarginFromBar = 3 / cameraZoom;

		// Draw lines to show that there are more units on this tile
		if (tile.unitsOnTile.Count > 1) {
			int lineCount = tile.unitsOnTile.Count;
			// TODO: Make configurable to taste, with cap of 8?
			if (lineCount > 8)
				lineCount = 8;
			for (int n = 0; n < lineCount; n++) {
				Vector2 lineStart = hpStartingLocation - new Vector2(lineWidth, offsetYFromCenter - lineMarginFromBar - lineMarginFromBar*n);
				looseView.DrawLine(lineStart, lineStart + new Vector2(4, 0) / cameraZoom, white, width: lineWidth);
				looseView.DrawLine(lineStart + new Vector2(0, 1) / cameraZoom, lineStart + new Vector2(4, 1) / cameraZoom, Color.Color8(75, 75, 75));
			}
		}
	}

	// Draw smaller pixels for the hp fractions as the max hp grows
	private int GetHpFractionHeight(int h) {
		if (h <= 6)
			return 4;
		if (h <= 12)
			return 2;
		return 1;
	}

	private bool UnitsVisible(GameData gameData, Player player, Tile t) {
		if (gameData.observerMode || gameData.revealMap) {
			return true;
		}

		// Only draw units on active tiles - otherwise if the tile is only known
		// but not actively seen, we can't see units.
		return player.tileKnowledge.isActiveTile(t);
	}
}
