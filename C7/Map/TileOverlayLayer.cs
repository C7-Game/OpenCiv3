using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;
using static C7GameData.Tile.TileOverlays;

namespace C7.Map {
	public partial class TileOverlayLayer : LooseLayer {
		private readonly ImageTexture roadTexture;
		private readonly ImageTexture railroadTexture;
		private readonly ImageTexture grassIrrigationTexture;
		private readonly ImageTexture desertIrrigationTexture;
		private readonly ImageTexture plainsIrrigationTexture;
		private readonly ImageTexture tundraIrrigationTexture;
		private readonly ImageTexture ruinsTexture;
		private readonly ImageTexture pollutionTexture;
		private readonly ImageTexture cratersTexture;

		private readonly Vector2 tileSize;

		private int rng = 0;
		private Dictionary<string, int> rngs = new();

		private List<TerrainImprovement> terrainImprovements;

		FontFile debugFont = new();

		public TileOverlayLayer() {
			roadTexture = TextureLoader.Load("terrain_improvements.road");
			railroadTexture = TextureLoader.Load("terrain_improvements.railroad");
			tileSize = roadTexture.GetSize() / 16;
			// grid 16x16 tiles
			// assume that roads and railroads textures have the same size

			// Each irrigation.pcx has a 4x4 grid of irrigation tiles, with
			// each tile being 128x64 pixels.
			grassIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.grass");
			desertIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.desert");
			plainsIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.plains");
			tundraIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.tundra");

			ruinsTexture = TextureLoader.Load("terrain_improvements.ruins");
			pollutionTexture = TextureLoader.Load("terrain_improvements.pollution");
			cratersTexture = TextureLoader.Load("terrain_improvements.craters");

			rng = GameData.rng.Next(0, 5000) * 2 + 1;

			terrainImprovements = EngineStorage.gameData.terrainImprovements.ToList();

			debugFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
			debugFont.FixedSize = 12;
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			Rect2 screenTarget = new Rect2(tileCenter - tileSize / 2, tileSize);

			var improvements = RenderableTerrainImprovements(tile);

			foreach (TerrainImprovement ti in improvements.OrderBy(ti => ti.zIndex)) {
				switch (ti.key) {
					case IRRIGATION:
						DrawIrrigation(looseView, tile, screenTarget);
						break;
					case ROAD:
						DrawRoad(looseView, tile, screenTarget);
						break;
					case RAILROAD:
						DrawRailRoad(looseView, tile, screenTarget);
						break;
					case RUINS:
						DrawRuins(looseView, tile, tileCenter);
						break;
					case POLLUTION:
						DrawPollution(looseView, tile, screenTarget);
						break;
					case CRATERS:
						DrawCraters(looseView, tile, screenTarget);
						break;
					default:
						looseView.DrawTexture(TextureLoader.Load($"terrain_improvements.{ti.key}"), screenTarget.Position);
						break;
				}
			}
		}

		private List<TerrainImprovement> RenderableTerrainImprovements(Tile tile) {
			var knownTerrainImprovements = new List<TerrainImprovement>();
			if (!EngineStorage.gameData.observerMode) {
				var knownImprovements = player.tileKnowledge.knownTerrainImprovements;

				foreach (var improvement in terrainImprovements) {
					if (knownImprovements.TryGetValue(tile.Id, out var imprs)) {
						if (imprs.Contains(improvement.key))
							knownTerrainImprovements.Add(improvement);
					}
				}
				return knownTerrainImprovements;
			}
			return tile.overlays.GetImprovements().ToList();
		}

		private void DrawIrrigation(LooseView looseView, Tile tile, Rect2 screenTarget) {
			// Figure out which index into the irrigation texture to use for
			// this tile.
			int irrigationIndex = 0;
			foreach (KeyValuePair<TileDirection, Tile> dirToTile in tile.neighbors) {
				var neighbour = dirToTile.Value;
				if (neighbour.HasIrrigationGraphics()) {
					irrigationIndex |= GetIrrigationFlag(dirToTile.Key);
				}
			}

			// Deserts, plains, and tundra (??) have specific textures for
			// irrigation. Everything else uses the grassland texture.
			ImageTexture texture = tile.baseTerrainType.Key switch {
				"plains" => plainsIrrigationTexture,
				"desert" => desertIrrigationTexture,
				"tundra" => tundraIrrigationTexture,
				_ => grassIrrigationTexture
			};

			// Draw the subtexture of the irrigation texture for this tile.
			looseView.DrawTextureRectRegion(texture, screenTarget, GetIrrigationRect(irrigationIndex));
		}

		private void DrawRoad(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int roadIndex = 0;
			if (tile.HasRailroadGraphics()) return;
			foreach (KeyValuePair<TileDirection, Tile> dirToTile in tile.neighbors) {
				var neighbour = dirToTile.Value;
				if (neighbour.HasRoadGraphics() || neighbour.HasRailroadGraphics()) {
					roadIndex |= GetRoadFlag(dirToTile.Key);
				}
			}
			looseView.DrawTextureRectRegion(roadTexture, screenTarget, GetRoadRect(roadIndex));
		}

		private void DrawRailRoad(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int roadIndex = 0;
			int railroadIndex = 0;
			foreach (KeyValuePair<TileDirection, Tile> dirToTile in tile.neighbors) {
				var neighbour = dirToTile.Value;
				if (neighbour.HasRailroadGraphics()) {
					railroadIndex |= GetRoadFlag(dirToTile.Key);
				} else if (dirToTile.Value.HasRoadGraphics()) {
					roadIndex |= GetRoadFlag(dirToTile.Key);
				}
			}
			if (roadIndex != 0) {
				looseView.DrawTextureRectRegion(roadTexture, screenTarget, GetRoadRect(roadIndex));
			}
			looseView.DrawTextureRectRegion(railroadTexture, screenTarget, GetRoadRect(railroadIndex));
		}

		private void DrawRuins(LooseView looseView, Tile tile, Vector2 tileCenter) {
			int ruinsIndex = GetRadomTextureIndex(tile.Id, 3, RUINS, 0x054F);

			var ruinsSingleTextureSize = new Vector2(167, 95);
			Rect2 screenRect = new(tileCenter - 0.5f * ruinsSingleTextureSize, ruinsSingleTextureSize);

			looseView.DrawTextureRectRegion(ruinsTexture, screenRect, new Rect2(167 * ruinsIndex, 0, ruinsSingleTextureSize));
		}

		private void DrawPollution(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int pollutionIndex = 0;
			foreach (KeyValuePair<TileDirection, Tile> dirToTile in tile.neighbors) {
				var neighbour = dirToTile.Value;
				if (neighbour.HasPollutionGraphics()) {
					pollutionIndex |= GetPollutionIndex(dirToTile.Key);
				}
			}

			// single tile pollution
			if (pollutionIndex == 0) {
				pollutionIndex = GetRadomTextureIndex(tile.Id, 10, POLLUTION, 0x11C8);

				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(pollutionIndex));
			} else {
				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(pollutionIndex - 1, 2));
			}

			// debug mask
			// looseView.DrawString(debugFont, tileCenter, $"{pollutionIndex}", modulate: Colors.Black);
		}

		private void DrawCraters(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int cratersIndex = 0;
			foreach (KeyValuePair<TileDirection, Tile> dirToTile in tile.neighbors) {
				var neighbour = dirToTile.Value;
				if (neighbour.HasCraters()) {
					cratersIndex |= GetCraterIndex(dirToTile.Key);
				}
			}

			// single tile crater
			if (cratersIndex == 0) {
				cratersIndex = GetRadomTextureIndex(tile.Id, 10, CRATERS, 0x24A5);

				looseView.DrawTextureRectRegion(cratersTexture, screenTarget, GetCratersRect(cratersIndex));
			} else {
				looseView.DrawTextureRectRegion(cratersTexture, screenTarget, GetCratersRect(cratersIndex - 1, 2));
			}
		}

		private int GetRadomTextureIndex(ID id, int variations, string layer, int offset) {
			int randomTextureIndex = 0;
			var identifier = $"{id}_{layer}";

			if (rngs.TryGetValue(identifier, out var index)) {
				randomTextureIndex = index;
			} else {
				var rand = new Random(Math.Clamp(int.Parse(id.ToString().Replace("tile-", "")) + offset, int.MinValue, int.MaxValue));
				var customRng = rng + rand.Next() + rand.Next(0, variations);

				randomTextureIndex = rngs[identifier] = customRng % variations;
			}

			return randomTextureIndex;
		}

		// Returns the rectangle within the road texture for a given index,
		// where the index has been constructed by OR'ing together the direction
		// flags for adjacent roads.
		private Rect2 GetRoadRect(int index) {
			int row = index >> 4;
			int column = index & 0xF;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for irrigation.
		private Rect2 GetIrrigationRect(int index) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3
			//  4  5  6  7
			//  ...
			int row = index / 4;
			int column = index % 4;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for pollution
		private Rect2 GetPollutionRect(int index, int offset = 0) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3  4
			//  5  6  7  8  9  
			//  ...
			int row = index / 5 + offset;
			int column = index % 5;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for craters.
		private Rect2 GetCratersRect(int index, int offset = 0) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3  4
			//  5  6  7  8  9  
			//  ...
			int row = index / 5 + offset;
			int column = index % 5;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// The per-neighbor index values that can be OR'd together to get the
		// proper rectangle within the road/railroad texture.
		private static int GetRoadFlag(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHEAST => 0x1,
				TileDirection.EAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTH => 0x8,
				TileDirection.SOUTHWEST => 0x10,
				TileDirection.WEST => 0x20,
				TileDirection.NORTHWEST => 0x40,
				TileDirection.NORTH => 0x80,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}

		// Like getRoadFlag, but for irrigation, which only depends on the
		// diagonal neighbors.
		//
		// Index values taken from ClassicRenderer.java in
		// https://hg.sr.ht/~adj/civ3_cross_platform_editor.
		//
		// Some of these values are probably wrong, but I couldn't figure out the correct pattern.
		// Also, there might not be a perfect pattern, as I can also see inconsistencies
		// in the original editor/game as well.
		private static int GetIrrigationFlag(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHWEST => 0x4,
				TileDirection.SOUTHEAST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
		private static int GetPollutionIndex(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTHWEST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
		private static int GetCraterIndex(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTHWEST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
	}
}
