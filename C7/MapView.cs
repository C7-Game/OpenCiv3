using System.Collections.Generic;
using System;
using C7.Map;
using Godot;
using C7GameData;

public partial class MapView : Node2D {
	// cellSize is half the size of the tile sprites, or the amount of space each tile takes up when they are packed on the grid (note tiles are
	// staggered and half overlap).
	public static readonly Vector2 cellSize = new Vector2(64, 32);
	public Vector2 scaledCellSize {
		get { return cellSize * new Vector2(cameraZoom, cameraZoom); }
	}

	public Game game;

	public MapView() { }

	public int mapWidth { get; private set; }
	public int mapHeight { get; private set; }
	public bool wrapHorizontally { get; private set; }
	public bool wrapVertically { get; private set; }

	private Vector2 internalCameraLocation = new Vector2(0, 0);
	public Vector2 cameraLocation {
		get {
			return internalCameraLocation;
		}
		set {
			setCameraLocation(value);
		}
	}
	public float internalCameraZoom = 1;
	public float cameraZoom {
		get { return internalCameraZoom; }
		set { setCameraZoomFromMiddle(value); }
	}

	private List<LooseView> looseViews = new();

	// Specifies a rectangular block of tiles that are currently potentially on screen. Accessible through getVisibleRegion(). Tile coordinates
	// are "virtual", i.e. "unwrapped", so there isn't necessarily a tile at each location. The region is intended to include the upper left
	// coordinates but not the lower right ones. When iterating over all tiles in the region you must account for the fact that map rows are
	// staggered, see LooseView._Draw for an example.
	public struct VisibleRegion {
		public int upperLeftX, upperLeftY;
		public int lowerRightX, lowerRightY;

		public int getRowStartX(int y) {
			return upperLeftX + (y - upperLeftY) % 2;
		}
	}

	public GridLayer gridLayer { get; private set; }
	public CityLayer cityLayer { get; private set; }
	public TileAssignmentLayer tileAssignmentLayer { get; private set; }

	const float MIN_SCALE = 0.1f;
	const float MAX_SCALE = 4.0f;

	private TransportInfoBox transportInfoBox;
	private LowerRightInfoBox lowerRightInfoBox;
	private MiniMap miniMap;

	public override void _Ready() {
		lowerRightInfoBox = GetNode<LowerRightInfoBox>("/root/C7Game/CanvasLayer/Control/GameStatus/LowerRightInfoBox");
		lowerRightInfoBox.game = game;
		lowerRightInfoBox.CenterCameraOnActiveUnit += OnCenterCameraOnUnit;

		var canvasControl = GetNode<Control>("/root/C7Game/CanvasLayer/Control");

		miniMap = new MiniMap(this);
		canvasControl.AddChild(miniMap);

		transportInfoBox = new TransportInfoBox(game);
		canvasControl.AddChild(transportInfoBox);
	}

	public override void _ExitTree() {
		lowerRightInfoBox.CenterCameraOnActiveUnit -= OnCenterCameraOnUnit;
	}

	public MapView(Game game, int mapWidth, int mapHeight, bool wrapHorizontally, bool wrapVertically) {
		this.game = game;
		this.mapWidth = mapWidth;
		this.mapHeight = mapHeight;
		this.wrapHorizontally = wrapHorizontally;
		this.wrapVertically = wrapVertically;

		// Set up our set of views, and the layers within each view.
		//
		// The drawing order within a view matches the order of `layers`, so
		// here borders will be drawn on top of the terrain.
		//
		// However, because cities and units use child nodes, we need separate
		// LooseView objects to get the ordering correct between textures and
		// nodes. Without this unit health bars (which are textures) would
		// be drawn behind cities (which are child nodes).
		LooseView terrainView = new(this);
		terrainView.layers.Add(new TerrainLayer());
		terrainView.layers.Add(new RiverLayer());
		terrainView.layers.Add(new ForestLayer());
		terrainView.layers.Add(new MarshLayer());
		terrainView.layers.Add(new HillsLayer());
		terrainView.layers.Add(new TntLayer());
		terrainView.layers.Add(new TileOverlayLayer());
		terrainView.layers.Add(new ResourceLayer());
		this.gridLayer = new GridLayer();
		terrainView.layers.Add(this.gridLayer);
		terrainView.layers.Add(new BuildingLayer());
		terrainView.layers.Add(new BorderLayer());

		LooseView cityView = new(this);
		this.cityLayer = new();
		cityView.layers.Add(this.cityLayer);

		LooseView tileAssignmentView = new(this);
		this.tileAssignmentLayer = new();
		tileAssignmentView.layers.Add(this.tileAssignmentLayer);

		LooseView fogOfWarView = new(this);
		fogOfWarView.layers.Add(new FogOfWarLayer());

		LooseView unitView = new(this);
		unitView.layers.Add(new GotoLayer());
		unitView.layers.Add(new BombardLayer());
		LooseView otherView = new(this);
		otherView.layers.Add(new UnitLayer());

		AddChild(terrainView);
		looseViews.Add(terrainView);

		AddChild(cityView);
		looseViews.Add(cityView);

		AddChild(tileAssignmentView);
		looseViews.Add(tileAssignmentView);

		AddChild(fogOfWarView);
		looseViews.Add(fogOfWarView);

		AddChild(unitView);
		looseViews.Add(unitView);
		AddChild(otherView);
		looseViews.Add(otherView);
	}

	public override void _Process(double delta) {
		// Redraw everything. This is necessary so that animations play. Maybe we could only update the unit layer but long term I think it's
		// better to redraw everything every frame like a typical modern video game.
		foreach (LooseView looseView in looseViews) {
			looseView.QueueRedraw();
		}
	}

	// Returns the size in pixels of the area in which the map will be drawn. This is the viewport size or, if that's null, the window size.
	public Vector2 getVisibleAreaSize() {
		return GetViewport() != null ? GetViewportRect().Size : DisplayServer.WindowGetSize();
	}

	public VisibleRegion getVisibleRegion() {
		(int X0, int Y0) = tileCoordsInMainCamera(new Vector2(0, 0));
		Vector2 mapViewSize = new Vector2(2, 4) + getVisibleAreaSize() / scaledCellSize;
		return new VisibleRegion {
			upperLeftX = X0 - 2,
			upperLeftY = Y0 - 2,
			lowerRightX = X0 + (int)mapViewSize.X,
			lowerRightY = Y0 + (int)mapViewSize.Y
		};
	}

	// "center" is the screen location around which the zoom is centered, e.g., if center is (0, 0) the tile in the top left corner will be the
	// same after the zoom level is changed, and if center is screenSize/2, the tile in the center of the window won't change.
	public void setCameraZoom(float newScale, Vector2 center) {
		newScale = Math.Clamp(newScale, MIN_SCALE, MAX_SCALE);

		Vector2 v2NewZoom = new Vector2(newScale, newScale);
		Vector2 v2OldZoom = new Vector2(cameraZoom, cameraZoom);
		if (v2NewZoom != v2OldZoom) {
			internalCameraZoom = newScale;
			foreach (LooseView looseView in looseViews) {
				looseView.Scale = v2NewZoom;
			}
			setCameraLocation((v2NewZoom / v2OldZoom) * (cameraLocation + center) - center);
		}
	}

	// Zooms in or out centered on the middle of the screen
	public void setCameraZoomFromMiddle(float newScale) {
		setCameraZoom(newScale, getVisibleAreaSize() / 2);
	}

	public void moveCamera(Vector2 offset) {
		setCameraLocation(cameraLocation + offset);
	}

	public void setCameraLocation(Vector2 location) {
		// Prevent the camera from moving beyond an unwrapped edge of the map. One complication here is that the viewport might actually be
		// larger than the map (if we're zoomed far out) so in that case we must apply the constraint the other way around, i.e. constrain the
		// map to the viewport rather than the viewport to the map.
		Vector2 visAreaSize = getVisibleAreaSize();
		Vector2 mapPixelSize = new Vector2(cameraZoom, cameraZoom) * (new Vector2(cellSize.X * (mapWidth + 1), cellSize.Y * (mapHeight + 1)));
		if (!wrapHorizontally) {
			float leftLim, rightLim;
			{
				if (mapPixelSize.X >= visAreaSize.X) {
					leftLim = 0;
					rightLim = mapPixelSize.X - visAreaSize.X;
				} else {
					leftLim = mapPixelSize.X - visAreaSize.X;
					rightLim = 0;
				}
			}
			if (location.X < leftLim)
				location.X = leftLim;
			else if (location.X > rightLim)
				location.X = rightLim;
		}
		if (!wrapVertically) {
			// These margins allow the player to move the camera that far off those map edges so that the UI controls don't cover up the
			// map. TODO: These values should be read from the sizes of the UI elements instead of hardcoded.
			float topMargin = 70, bottomMargin = 140;
			float topLim, bottomLim;
			{
				if (mapPixelSize.Y >= visAreaSize.Y) {
					topLim = -topMargin;
					bottomLim = mapPixelSize.Y - visAreaSize.Y + bottomMargin;
				} else {
					topLim = mapPixelSize.Y - visAreaSize.Y;
					bottomLim = 0;
				}
			}
			if (location.Y < topLim)
				location.Y = topLim;
			else if (location.Y > bottomLim)
				location.Y = bottomLim;
		}

		internalCameraLocation = location;
		foreach (LooseView looseView in looseViews) {
			looseView.Position = -location;
		}
	}

	public Vector2 screenLocationOfTileCoords(int X, int Y, bool center = true) {
		// Add one to X & Y to get the tile center b/c in Civ 3 the tile at (X, Y) is a diamond centered on (x+1, y+1).
		Vector2 centeringOffset = center ? new Vector2(1, 1) : new Vector2(0, 0);

		var mapLoc = (new Vector2(X, Y) + centeringOffset) * cellSize;
		return mapLoc * cameraZoom - cameraLocation;
	}

	// Returns the location of tile (X, Y) on the screen, if "center" is true returns the location of the tile center and otherwise returns the
	// upper left. Works even if (X, Y) is off screen or out of bounds.
	public Vector2 screenLocationOfTile(Tile tile, bool center = true) {
		return screenLocationOfTileCoords(tile.XCoordinate, tile.YCoordinate, center);
	}

	// Returns the virtual tile coordinates on screen at the given location. "Virtual" meaning the coordinates are unwrapped and there isn't
	// necessarily a tile there at all.
	public (int, int) tileCoordsInMainCamera(Vector2 screenLocation) {
		Vector2 mapLoc = (screenLocation + cameraLocation) / scaledCellSize;
		return tileCoordsForMapLocation(mapLoc);
	}

	public (int, int) tileCoordsForMapLocation(Vector2 mapLocation) {
		Vector2 intMapLoc = mapLocation.Floor();
		Vector2 fracMapLoc = mapLocation - intMapLoc;
		int X = (int)intMapLoc.X, Y = (int)intMapLoc.Y;
		bool evenColumn = X % 2 == 0, evenRow = Y % 2 == 0;
		if (evenColumn ^ evenRow) {
			if (fracMapLoc.Y > fracMapLoc.X)
				X -= 1;
			else
				Y -= 1;
		} else {
			if (fracMapLoc.Y < 1 - fracMapLoc.X) {
				X -= 1;
				Y -= 1;
			}
		}
		return (X, Y);
	}

	public Tile tileOnScreenAt(GameMap map, Vector2 screenLocation) {
		(int X, int Y) = tileCoordsInMainCamera(screenLocation);
		return map.tileAt(X, Y);
	}

	public void centerCameraOnTile(Tile t) {
		var tileCenter = new Vector2(t.XCoordinate + 1, t.YCoordinate + 1) * scaledCellSize;
		setCameraLocation(tileCenter - (float)0.5 * getVisibleAreaSize());
	}

	public void OnCenterCameraOnUnit() {
		MapUnit currentlySelectedUnit = game.CurrentlySelectedUnit;
		if (!MapUnit.IsMapUnitValid(currentlySelectedUnit))
			return;
		centerCameraOnTile(currentlySelectedUnit.location);
	}
}
