using System.Collections.Generic;
using C7GameData;
using Godot;

namespace C7.Map;

public class CityLayer : LooseLayer {
	private Dictionary<City, CityScene> citySceneLookup = new();

	public CityLayer() {
	}

	public void UpdateAfterCityDestruction(City city) {
		EraseCity(city);
	}

	private void EraseCity(City city) {
		citySceneLookup.Remove(city, out CityScene cityScene);
		if (cityScene != null) {
			cityScene.Hide();
		}
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.cityAtTile is null) {
			return;
		}

		if (looseView.IsTileCoveredByTileInfo(tile)) {
			EraseCity(tile.cityAtTile);
			return;
		}

		City city = tile.cityAtTile;
		Vector2I tileCenter2I = new((int)tileCenter.X, (int)tileCenter.Y);

		if (!citySceneLookup.ContainsKey(city)) {
			CityScene cityScene = new CityScene(city);
			cityScene.SetTileCenter(tileCenter2I);
			looseView.AddChild(cityScene);
			citySceneLookup[city] = cityScene;
		} else {
			CityScene scene = citySceneLookup[city];
			scene.SetTileCenter(tileCenter2I);
			scene._Draw();

			if (looseView.HasCityLabelToHideFromTileInfo(tile))
				scene.HideLabel();
			else
				scene.ShowLabel();
		}
	}
}
