using C7GameData;
using Godot;
using Serilog;

namespace C7.Map;

public record struct CityGraphicsDetails(
	// A size rank of 0 is a town, 1 a city, etc.
	int sizeRank,
	int eraIndex,
	bool hasWalls
);

public partial class CityScene : Node2D {
	private ILogger log = LogManager.ForContext<CityScene>();

	private ImageTexture cityTexture;
	private TextureRect cityGraphics = new TextureRect();
	private CityLabelScene cityLabelScene;
	private City city;
	private Rules rules;
	private CityGraphicsDetails cachedDetails;
	private Vector2I tileCenter;

	private AnimatedSprite2D disorderSprite;
	private static SpriteFrames disorderFrames = TextureLoader.LoadAnimation("animations.disorder", "disorder");

	public CityScene(City city) {
		cityLabelScene = new CityLabelScene(city);
		this.city = city;
		this.rules = city.owner.rules;

		cachedDetails = GetCityGraphicsDetails(city);
		ConfigureCityGraphics(cachedDetails);

		AddChild(cityGraphics);
		AddChild(cityLabelScene);

		disorderSprite = new();
		disorderSprite.SpriteFrames = disorderFrames;
		disorderSprite.Animation = "disorder";
		disorderSprite.Position = tileCenter + new Vector2(0, -32);
		AddChild(disorderSprite);
		disorderSprite.Play("disorder");
	}

	public override void _Draw() {
		base._Draw();
		cityLabelScene._Draw();

		if (cachedDetails != GetCityGraphicsDetails(city)) {
			cachedDetails = GetCityGraphicsDetails(city);
			ConfigureCityGraphics(cachedDetails);
		}

		if (city.isInCivilDisorder) {
			disorderSprite.Show();
		} else {
			disorderSprite.Hide();
		}
	}

	public void SetTileCenter(Vector2I tileCenter) {
		this.tileCenter = tileCenter;

		disorderSprite.Position = tileCenter + new Vector2(0, -32);
		cityLabelScene.tileCenter = tileCenter;

		cityGraphics.Position = new(
			tileCenter.X - (float)0.5 * cityTexture.GetWidth(),
			tileCenter.Y - (float)0.5 * cityTexture.GetHeight()
		);
	}

	private CityGraphicsDetails GetCityGraphicsDetails(City c) {
		CityGraphicsDetails result = new() {
			sizeRank = 0,
			hasWalls = c.HasWalls(),
		};
		if (c.residents.Count > rules.MaximumLevel1CitySize) {
			++result.sizeRank;

			// Walls are only displayed for towns, not cities or metropolises
			result.hasWalls = false;
		}
		if (c.residents.Count > rules.MaximumLevel2CitySize) {
			++result.sizeRank;
		}
		result.eraIndex = city.owner.EraIndex();
		return result;
	}

	//TODO: Support multiple city flavors and walls.
	private void ConfigureCityGraphics(CityGraphicsDetails details) {
		cityTexture = TextureLoader.Load("cities", details);

		cityGraphics.MouseFilter = Control.MouseFilterEnum.Ignore;
		cityGraphics.Texture = cityTexture;
	}

	public void HideLabel() => cityLabelScene?.Hide();
	public void ShowLabel() => cityLabelScene?.Show();
}
