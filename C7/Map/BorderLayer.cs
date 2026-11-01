using System.Collections.Generic;
using C7.Textures;
using C7GameData;
using Godot;

namespace C7.Map;

// The layer responsible for drawing civilization borders.
public class BorderLayer : LooseLayer {
	public record struct TextureDetails(
		TileDirection direction,
		bool isHilly
	);

	private readonly Dictionary<(TextureDetails, Color), ImageTexture> textureCache = new();

	// TODO: This method doesn't precisely mirror Civ3 coloring
	private static Color CalcSecondaryColor(Color mainColor) {
		return new Color(mainColor.R * 0.8f, mainColor.G * 0.8f, mainColor.B * 0.8f);
	}

	/// This method retrieves a border texture of a specific index and color.
	/// It works by replacing two base colors in a border texture template with new colors:
	/// - The color of civilization, passed as an argument to this method.
	/// - The secondary color, which is derived from the civilization color.
	/// The resulting texture is cached.
	private ImageTexture GetBorderTexture(Tile tile, TileDirection dir, Color borderColor) {
		TextureDetails textureDetails = new() {
			direction = dir,
			isHilly = TileRandom.HasVirtualHillConnection(tile, dir, gameSeed)
		};

		if (textureCache.TryGetValue((textureDetails, borderColor), out ImageTexture res)) {
			return res;
		}

		ImageTexture texture = TextureLoader.Load("borders", textureDetails);

		Color secondaryColor = CalcSecondaryColor(borderColor);

		Dictionary<Color, Color> colorReplacements = new()
		{
			{ Color.Color8(236, 255, 0), borderColor },
			{ Color.Color8(255, 0, 0), secondaryColor}
		};

		Image image = Util.TransformColors(texture.GetImage(), colorReplacements);

		var newTexture = ImageTexture.CreateFromImage(image);
		textureCache[(textureDetails, borderColor)] = newTexture;

		return newTexture;
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (tile.owningCity is null) {
			return;
		}

		Color borderColor = TextureLoader.LoadColor(tile.owningCity.owner.GetPlayerColor());
		TileDirection[] borderDirections = [TileDirection.NORTHEAST, TileDirection.NORTHWEST, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST];

		foreach (TileDirection dir in borderDirections) {
			if (tile.neighbors[dir].owningCity?.owner != tile.owningCity?.owner) {
				ImageTexture texture = GetBorderTexture(tile, dir, borderColor);
				Vector2 size = texture.GetSize();
				Vector2 offset = size/2;
				// this value were found experimentally to improve alignment with the grid
				offset.Y += size.Y * 0.055f;

				looseView.DrawTexture(texture, tileCenter - offset);
			}
		}
	}
}
