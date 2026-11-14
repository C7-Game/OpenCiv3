using C7GameData;
using Godot;
using Serilog;
using static C7GameData.TileRandom;

namespace C7.Map {
	/// <summary>
	/// Displays terrain yield overlays (from the tnt.pcx file).  These are most well known for letting you know where
	/// there are bonus grasslands.
	/// Note: I don't know why it's called tnt.
	/// </summary>
	public partial class TntLayer : LooseLayer {
		private ILogger log = LogManager.ForContext<TntLayer>();

		private static readonly Vector2 tntSize = new Vector2(128, 64);
		private ImageTexture tntTexture;

		// Each row corresponds to a terrain. For now, we're only adding one, maybe someday we'll add full TNT support
#pragma warning disable CS0414
		private readonly int GRASSLAND_ROW = 0;
		private readonly int BONUS_GRASSLAND_ROW = 1;
		private readonly int PLAINS_ROW = 2;
		private readonly int DESERT_ROW = 3;
		private readonly int BONUS_GRASSLAND_TNT_OFF_ROW = 3;
		private readonly int TUNDRA_ROW = 4;
		private readonly int FLOOD_PLAIN_ROW = 5;
#pragma warning restore CS0414

		public TntLayer() {
			tntTexture = TextureLoader.Load("terrain.tnt");
		}
		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			if (tile.overlayTerrainType.Key == "grassland" && tile.isBonusShield) {
				var grasslandBonusIndex = GetRandomInt(tile.Id, gameSeed, 3, 0x5f3);
				Rect2 tntRectangle = new Rect2(grasslandBonusIndex * tntSize.X, BONUS_GRASSLAND_TNT_OFF_ROW * tntSize.Y, tntSize);
				Rect2 screenTarget = new Rect2(tileCenter - 0.5f * tntSize, tntSize);
				looseView.DrawTextureRectRegion(tntTexture, screenTarget, tntRectangle);
			}
		}
	}
}
