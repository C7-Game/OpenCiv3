using System.Linq;
using C7GameData;
using Godot;
using static C7GameData.TileRandom;

namespace C7.Map;

public class ForestLayer : LooseLayer {
	public static readonly Vector2 forestJungleSize = new Vector2(128, 88);

	private ImageTexture largeJungleTexture;
	private ImageTexture smallJungleTexture;
	private ImageTexture largeForestTexture;
	private ImageTexture largePlainsForestTexture;
	private ImageTexture largeTundraForestTexture;
	private ImageTexture smallForestTexture;
	private ImageTexture smallPlainsForestTexture;
	private ImageTexture smallTundraForestTexture;
	private ImageTexture pineForestTexture;
	private ImageTexture pinePlainsTexture;
	private ImageTexture pineTundraTexture;

	public ForestLayer() {
		largeJungleTexture = TextureLoader.Load("terrain.jungle.large");
		smallJungleTexture = TextureLoader.Load("terrain.jungle.small");
		largeForestTexture = TextureLoader.Load("terrain.forest.large");
		largePlainsForestTexture = TextureLoader.Load("terrain.forest.plains.large");
		largeTundraForestTexture = TextureLoader.Load("terrain.forest.tundra.large");
		smallForestTexture = TextureLoader.Load("terrain.forest.small");
		smallPlainsForestTexture = TextureLoader.Load("terrain.forest.plains.small");
		smallTundraForestTexture = TextureLoader.Load("terrain.forest.tundra.small");
		pineForestTexture = TextureLoader.Load("terrain.pine.forest");
		pinePlainsTexture = TextureLoader.Load("terrain.pine.plains");
		pineTundraTexture = TextureLoader.Load("terrain.pine.tundra");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		bool renderLarge = tile.GetEdgeNeighbors().Count(t => t.IsFlatLand()) > 3;

		if (tile.HasJungle()) {
			// Jungle and forest is always rendered as small, unless its tile neighbors 4 other jungle/forest tiles across its edges (middle of cross)
			ImageTexture jungleTexture;
			int randomJungleRow = 0;
			int randomJungleColumn;

			if (renderLarge) {
				jungleTexture = largeJungleTexture;
				randomJungleColumn = GetRandomInt(tile.Id, gameSeed, 8, 0x542);
				if (randomJungleColumn > 3) {
					randomJungleColumn %= 4;
					randomJungleRow++;
				}
			} else {
				jungleTexture = smallJungleTexture;
				randomJungleColumn = GetRandomInt(tile.Id, gameSeed, 12, 0xb653);
				if (randomJungleColumn > 5) {
					randomJungleColumn %= 6;
					randomJungleRow++;
				}
			}

			Rect2 jungleRectangle = new Rect2(randomJungleColumn * forestJungleSize.X, randomJungleRow * forestJungleSize.Y, forestJungleSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * forestJungleSize + new Vector2(0, -12), forestJungleSize);
			looseView.DrawTextureRectRegion(jungleTexture, screenTarget, jungleRectangle);
		}
		if (tile.HasForest()) {
			int forestRow = 0;
			int forestColumn = 0;
			ImageTexture forestTexture;
			if (tile.isPineForest) {
				if (tile.baseTerrainType.Key == "grassland") {
					forestTexture = pineForestTexture;
				} else if (tile.baseTerrainType.Key == "plains") {
					forestTexture = pinePlainsTexture;
				} else { //Tundra
					forestTexture = pineTundraTexture;
				}
				forestColumn = GetRandomInt(tile.Id, gameSeed, 12, 0xc99);
				if (forestColumn > 5) {
					forestColumn %= 6;
					forestRow++;
				}
			} else {
				if (renderLarge) {
					if (tile.baseTerrainType.Key == "grassland") {
						forestTexture = largeForestTexture;
					} else if (tile.baseTerrainType.Key == "plains") {
						forestTexture = largePlainsForestTexture;
					} else {    //tundra
						forestTexture = largeTundraForestTexture;
					}
					forestColumn = GetRandomInt(tile.Id, gameSeed, 8, 0xa45);
					if (forestColumn > 3) {
						forestColumn %= 4;
						forestRow++;
					}
				} else {
					if (tile.baseTerrainType.Key == "grassland") {
						forestTexture = smallForestTexture;
					} else if (tile.baseTerrainType.Key == "plains") {
						forestTexture = smallPlainsForestTexture;
					} else {    //tundra
						forestTexture = smallTundraForestTexture;
					}
					forestColumn = GetRandomInt(tile.Id, gameSeed, 10, 0x133);
					if (forestColumn > 4) {
						forestColumn %= 5;
						forestRow++;
					}
				}
			}
			Rect2 forestRectangle = new Rect2(forestColumn * forestJungleSize.X, forestRow * forestJungleSize.Y, forestJungleSize);
			Rect2 screenTarget = new Rect2(tileCenter - (float)0.5 * forestJungleSize + new Vector2(0, -12), forestJungleSize);
			looseView.DrawTextureRectRegion(forestTexture, screenTarget, forestRectangle);
		}
	}
}
