using C7GameData;
using Godot;

namespace C7.Map;

public class BuildingLayer : LooseLayer {
	private ImageTexture barbCamp;

	public BuildingLayer() {
		barbCamp = TextureLoader.Load("terrain.barbarian_camp");
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.hasBarbarianCamp) {
			Rect2 screenRect = new(tileCenter - 0.5f * barbCamp.GetSize(), barbCamp.GetSize());
			looseView.DrawTextureRect(barbCamp, screenRect, tile: false);
		}
	}
}
