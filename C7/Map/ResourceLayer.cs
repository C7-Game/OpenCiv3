using C7GameData;
using Godot;
using Resource = C7GameData.Resource;
using Serilog;

namespace C7.Map {
	public partial class ResourceLayer : LooseLayer {
		private ILogger log = LogManager.ForContext<ResourceLayer>();

		private static readonly Vector2 resourceSize = new Vector2(50, 50);

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			Resource resource = tile.Resource;
			if (resource == Resource.NONE) {
				return;
			}

			if (!ResourceVisible(gameData, tile)) {
				return;
			}

			var texture = TextureLoader.Load("resources.large", resource, useCache: true);

			looseView.DrawTexture(texture, tileCenter - 0.5f * texture.GetSize());
		}

		private bool ResourceVisible(GameData gameData, Tile t) {
			if (gameData.observerMode || gameData.revealMap) {
				return true;
			}
			return gameData.GetFirstHumanPlayer().KnowsAboutResource(t.Resource);
		}
	}
}
