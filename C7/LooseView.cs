using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using C7.Map;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;

public partial class LooseView : Node2D {
	public MapView mapView;
	public List<LooseLayer> layers = new List<LooseLayer>();
	private static ILogger log = Log.ForContext<LooseView>();

	protected List<VisibleTile> visibleKnownTiles;
	protected List<VisibleTile> visibleUnKnownTiles;
	public Dictionary<Tile, Vector2> tileCenters { get; private set; } = new Dictionary<Tile, Vector2>();

	public LooseView(MapView mapView) {
		this.mapView = mapView;
	}

	protected struct VisibleTile {
		public Tile tile;
		public Vector2 tileCenter;
	}

	public override void _Draw() {
		base._Draw();

		EngineStorage.ReadGameData((GameData gD) => {
			// Iterating over visible tiles is unfortunately pretty expensive. Assemble a list of Tile references and centers first so we don't
			// have to reiterate for each layer. Doing this improves framerate significantly.
			MapView.VisibleRegion visRegion = mapView.getVisibleRegion();
			visibleKnownTiles = new List<VisibleTile>();
			visibleUnKnownTiles = new List<VisibleTile>();
			GetVisibleTiles(visRegion, gD);

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			foreach (LooseLayer layer in layers.FindAll(L => L.visible && !(L is FogOfWarLayer) && !(L is GridLayer))) {
				layer.onBeginDraw(this, gD);
				foreach (VisibleTile vT in visibleKnownTiles) {
					layer.drawObject(this, gD, vT.tile, vT.tileCenter);
				}

				layer.onEndDraw(this, gD);
			}

			foreach (var layer in layers.Where(layer => layer.visible && layer is GridLayer)) {
				layer.onBeginDraw(this, gD);
				foreach (VisibleTile vT in visibleKnownTiles.Concat(visibleUnKnownTiles)) {
					layer.drawObject(this, gD, vT.tile, vT.tileCenter);
				}

				layer.onEndDraw(this, gD);
			}

			if (stopwatch.ElapsedMilliseconds > 100) {
				log.Warning($"-> End draw: {stopwatch.ElapsedMilliseconds} milliseconds");
			}

			if (!gD.revealMap) {
				foreach (FogOfWarLayer layer in layers.Where(layer => layer is FogOfWarLayer).Cast<FogOfWarLayer>()) {
					for (int Y = visRegion.upperLeftY; Y < visRegion.lowerRightY; Y++) {
						if (gD.map.isRowAt(Y)) {
							for (int X = visRegion.getRowStartX(Y); X < visRegion.lowerRightX; X += 2) {
								Tile tile = gD.map.tileAt(X, Y);
								if (tile != Tile.NONE) {
									VisibleTile invisibleTile = new VisibleTile {
										tile = tile,
										tileCenter = MapView.cellSize * new Vector2(X + 1, Y + 1)
									};
									layer.drawObject(this, gD, tile, invisibleTile.tileCenter);
								}
							}
						}
					}
				}
			}
		});
	}

	private void GetVisibleTiles(MapView.VisibleRegion visRegion, GameData gD) {
		for (int Y = visRegion.upperLeftY; Y < visRegion.lowerRightY; Y++) {
			if (gD.map.isRowAt(Y)) {
				for (int X = visRegion.getRowStartX(Y); X < visRegion.lowerRightX; X += 2) {
					Tile tile = gD.map.tileAt(X, Y);
					Vector2 tileCenter = MapView.cellSize * new Vector2(X + 1, Y + 1);
					if (IsTileKnown(tile, gD)) {
						visibleKnownTiles.Add(new VisibleTile {
							tile = tile,
							tileCenter = tileCenter
						});
					} else {
						visibleUnKnownTiles.Add(new VisibleTile {
							tile = tile,
							tileCenter = tileCenter
						});
					}
					tileCenters[tile] = tileCenter;
				}
			}
		}
	}

	private static bool IsTileKnown(Tile tile, GameData gameData) {
		if (gameData.revealMap) {
			return true;
		}
		TileKnowledge knowledge = gameData.GetFirstHumanPlayer().tileKnowledge;
		return tile != Tile.NONE && knowledge.isTileKnown(tile);
	}

	public bool IsTileCoveredByTileInfo(Tile tile) {
		var target = mapView.game.tileInfo;
		if (target == null) return false;

		return target.IsCovered(tile);
	}

	public bool HasCityLabelToHideFromTileInfo(Tile tile) {
		if (!tile.HasCity()) return false;

		var target = mapView.game.tileInfo;
		if (target == null) return false;

		return target.HasCityLabelToCover(tile);
	}
}
