using System;
using Godot;
using ConvertCiv3Media;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using Script = MoonSharp.Interpreter.Script;
using C7.Map;
using C7Engine.Lua;

public readonly record struct CropRegion(int LeftStart, int TopStart, int CroppedWidth, int CroppedHeight);

/// This class provides methods for loading PCX and PNG textures based on the metadata set up in the Lua configuration file.
///
/// Each Lua configuration file returns a table representing a tree.
/// Any leaf node in this structure can be one of the following:
/// 1) A string representing a path to the texture
///
/// 2) A table with following keys:
///    - Required: "path" (string) - Path to the texture file
///    - Optional: "crop_region" (table) - Sequential table matching the CropRegion record
///
///    For PCX files:
///    - Optional: "shadows" (boolean) - Whether the shadow effect should be simulated (defaults to true)
///    - Optional: "alpha" (string) - Path to an alpha channel texture file
///    - Optional: "alpha_row_offset" (number) - Row offset for alpha blending
///    - Optional: "transparent_color_indexes" (table) - List of color indexes to treat as transparent
///    - Optional: "pure_alpha" - The pcx file only contains transparency information.
///
///    For c7 files:
///    - Optional: "hex_color" (string) - A 6 character hex string for a civ color
///
///    For animations:
///    - Optional: "frame_duration" (float) - duration of each frame
///    - Optional: "animation_rows" (int) - number of rows in an animation sprite sheet. Must be >0 for png animations.
///    - Optional: "animation_cols" (int) - number of columns in an animation sprite sheet. Must be >0 for png animations.
///         - If a png animation is specified, the frame ordering is
///           (row 0, col 0), (row 0,col 1), ... (row 0, col animation_cols-1),
///           (row 1, col 0), ...
///           ...
///           (row animation_rows-1, col 0), ...
///
/// 3) A table containing key "map_object_to_sprite" holding a function that accepts a table it belongs to and a C# object.
/// This function should return a table similar to the one described in the point 2.
public static class TextureLoader {
	private struct ConfigEntry {
		public string Path;
		public CropRegion? CropRegion = null;

		// PCX-specific settings
		public string AlphaPath = null;
		public int AlphaRowOffset = 0;
		public bool PureAlpha = false;
		public PCXToGodot.ColorOptions ColorOptions = PCXToGodot.ColorOptions.Default;

		// For civ-colors, a modern replacement for the 1x1 px pcx images.
		public string HexColor = null;

		// Animation-specific settings
		public float FrameDuration = 0.5f;
		public int AnimationRows = 0;
		public int AnimationCols = 0;

		public ConfigEntry() {
		}

		public readonly bool UseAlpha => AlphaPath != null;
	}

	private static Script lua;
	private static Table textureConfig;

	private static Dictionary<string, ImageTexture> textureCache = [];
	private static Dictionary<string, Pcx> PcxCache = [];
	private static Dictionary<string, Image> PngCache = [];
	private static Dictionary<string, Color> colorCache = [];

	private static Dictionary<string, ImageTexture> configKeyCache = [];
	private static Dictionary<(string configKey, object obj), ImageTexture> objectMappingCache = [];
	private static Dictionary<(string configKey, string animationName), SpriteFrames> animationCache = [];

	static TextureLoader() {
		// Initialize the TextureLoader when running in the editor
		// In game it is done by GlobalSingleton, but it's not accessible in the editor
		if (Engine.IsEditorHint()) {
			GameMode gameMode = GameMode.Load(GamePaths.GameModesDir, GamePaths.basic);

			var (script, textureConfig) = gameMode.textures;
			TextureLoader.SetConfig(script, textureConfig);
		}
	}

	public static void SetConfig(Script lua, Table textureConfig) {
		ClearCache();

		TextureLoader.lua = lua;
		TextureLoader.textureConfig = textureConfig;
	}

	/// Returns a texture based on the config key.
	/// The config key should be a string separated by dots, representing the path through the
	/// configuration hierarchy (e.g., "icons.plus").
	public static ImageTexture Load(string configKey) {
		if (configKeyCache.TryGetValue(configKey, out ImageTexture cachedTexture))
			return cachedTexture;

		object entry = GetEntryByPath(configKey);
		if (entry == null)
			throw new Exception($"Texture config not found for key: {configKey}");

		ImageTexture texture = LoadFromLuaObject(entry);

		configKeyCache[configKey] = texture;

		return texture;
	}

	/// Returns a texture based on the config key and a C# object.
	/// This overload uses the "map_object_to_sprite" function in the
	/// config entry to dynamically determine which texture to load
	/// based on the provided object's properties.
	///
	/// This method optionally allows to cache the resulting texture,
	/// using (configKey, obj) as key.  Note, that caching shouldn't
	/// be used for objects whose texture-affecting properties can
	/// change.
	///
	/// Note that the type of the object passed to the method should
	/// be registered as Moonsharp userdata.
	public static ImageTexture Load(string configKey, object obj, bool useCache = false) {
		var cacheKey = (configKey, obj);

		if (useCache && objectMappingCache.TryGetValue(cacheKey, out ImageTexture cachedTexture))
			return cachedTexture;

		object entry = GetEntryByPath(configKey);
		if (entry is not Table table)
			throw new Exception($"Table expected for key: {configKey}");

		if (table["map_object_to_sprite"] is not Closure func)
			throw new Exception("Custom mapping function expected");

		object result = lua.SafeCall(func, table, DynValue.FromObject(lua, obj)).ToObject();

		ImageTexture texture = LoadFromLuaObject(result);

		if (useCache)
			objectMappingCache[cacheKey] = texture;

		return texture;
	}

	// Allows to load the texture directly by its file path, bypassing the Lua config.
	// Supports both PCX and PNG textures
	public static ImageTexture LoadByPath(string path) {
		string ext = Path.GetExtension(path).ToLowerInvariant();

		return ext switch {
			".png" => LoadFromPNG(path),
			".pcx" => LoadFromPCX(path),
			_ => throw new FormatException($"Unknown texture format: {path}"),
		};
	}

	/// Returns the list of textures making up an animation.
	///
	/// The config key should be a string separated by dots, representing the path through the
	/// configuration hierarchy (e.g., "animations.cursor").
	///
	/// The animation name is what the resulting animation will be stored as in the result.
	public static SpriteFrames LoadAnimation(string configKey, string animationName) {
		var cacheKey = (configKey, animationName);
		if (animationCache.TryGetValue(cacheKey, out SpriteFrames cachedAnimation))
			return cachedAnimation;

		object entry = GetEntryByPath(configKey);
		if (entry == null)
			throw new Exception($"Texture config not found for key: {configKey}");

		SpriteFrames animation = LoadAnimationFromConfigEntry(ParseConfigEntry(entry), animationName);

		animationCache[cacheKey] = animation;

		return animation;
	}

	/// Gets a color given a "civ index".
	///
	/// This exists in the TextureLoader because civ3 implements civ colors
	/// as 1x1 pixel pcx files.
	public static Color LoadColor(int civIndex) {
		string key = $"civ_colors.color_{civIndex}";
		if (colorCache.TryGetValue(key, out Color cachedColor))
			return cachedColor;

		// Load the 1x1 pixel file and get the color, or use the modern hex color.
		ConfigEntry config = ParseConfigEntry(GetEntryByPath(key));
		Color color;
		if (config.HexColor != null) {
			color = new(code: config.HexColor);
		} else {
			ImageTexture texture = LoadFromConfigEntry(config);
			color = texture.GetImage().GetPixel(0, 0);
		}

		colorCache[key] = color;
		return color;
	}

	/// An utility method for setting textures of a button node.
	/// Accepts a button and a config key. The config key should lead
	/// to a table containing config entries with "normal", "pressed"
	/// and "hover" keys.
	public static void SetButtonTextures(TextureButton button, string configKey) {
		object entry = GetEntryByPath(configKey);

		if (entry is not Table table)
			throw new Exception($"Table expected for key: {configKey}");

		button.TextureNormal = LoadFromLuaObject(table["normal"]);
		button.TexturePressed = LoadFromLuaObject(table["pressed"]);
		button.TextureHover = LoadFromLuaObject(table["hover"]);
	}

	private static ImageTexture LoadFromLuaObject(object entry) {
		return LoadFromConfigEntry(ParseConfigEntry(entry));
	}

	private static ConfigEntry ParseConfigEntry(object entry) {
		if (entry is string simplePath) {
			return new() {
				Path = simplePath,
			};
		}

		if (entry is Table table) {
			if (table["path"] == null) {
				throw new ArgumentException("Texture configuration missing required 'path' property");
			}

			HashSet<int> transparentColorIndexes = new();
			if (table["transparent_color_indexes"] == null) {
				transparentColorIndexes = new PCXToGodot.ColorOptions().transparentColorIndexes;
			} else {
				if (table["transparent_color_indexes"] is not Table) {
					throw new ArgumentException($"'transparent_color_indexes' must be a table.");
				}

				foreach (DynValue d in ((Table)table["transparent_color_indexes"]).Values) {
					// Note: Convert.ToInt32 doesn't work for DynValue.
					transparentColorIndexes.Add((int)d.CastToNumber());
				}
			}

			return new() {
				Path = table["path"].ToString(),
				AlphaPath = table["alpha"]?.ToString(),
				PureAlpha = Convert.ToBoolean(table["pure_alpha"] ?? false),
				CropRegion = ExtractCropRegion(table),
				ColorOptions = new PCXToGodot.ColorOptions() {
					transparentColorIndexes = transparentColorIndexes,
					shadows = Convert.ToBoolean(table["shadows"] ?? true),
				},
				AlphaRowOffset = Convert.ToInt32(table["alpha_row_offset"] ?? 0),
				HexColor = table["hex_color"]?.ToString(),

				FrameDuration = (float)Convert.ToDouble(table["frame_duration"] ?? 0.5),
				AnimationRows = Convert.ToInt32(table["animation_rows"] ?? 0),
				AnimationCols = Convert.ToInt32(table["animation_cols"] ?? 0),
			};
		}

		throw new ArgumentException($"Invalid texture config format: {entry?.GetType().Name ?? "null"}");
	}

	private static ImageTexture LoadFromConfigEntry(ConfigEntry config) {
		string ext = Path.GetExtension(config.Path).ToLowerInvariant();

		return ext switch {
			".png" => LoadFromPNG(config.Path, config.CropRegion),
			".pcx" when config.PureAlpha => PCXToGodot.getPureAlphaFromPCX(new Pcx(Util.Civ3MediaPath(config.Path)), config.ColorOptions.transparentColorIndexes),
			".pcx" when config.UseAlpha => LoadWithAlphaBlend(config.Path, config.AlphaPath!, config.CropRegion, config.AlphaRowOffset),
			".pcx" => LoadFromPCX(config.Path, config.CropRegion, config.ColorOptions),
			_ => throw new FormatException($"Unknown texture format: {config.Path}"),
		};
	}

	private static SpriteFrames LoadAnimationFromConfigEntry(ConfigEntry config, string animationName) {
		string ext = Path.GetExtension(config.Path).ToLowerInvariant();
		SpriteFrames result = new();
		result.AddAnimation(animationName);

		if (ext == ".flc") {
			Flic flic = Util.LoadFlic(config.Path);

			const int row = 0;
			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row, col];
				// The ignored variable is the "tint" image, which would get the civ
				// specific color applied to it if it was a unit animation.
				(ImageTexture bl, _) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				result.AddFrame(animationName, bl, config.FrameDuration);
			}

			return result;
		}

		if (ext == ".png") {
			ImageTexture fullImage = LoadFromPNG(config.Path, config.CropRegion);
			if (config.AnimationRows == 0 || config.AnimationCols == 0) {
				throw new ArgumentException($"Expected non-zero anim rows and cols for {config.Path}");
			}

			int frameWidth = fullImage.GetWidth() / config.AnimationCols;
			int frameHeight = fullImage.GetHeight() / config.AnimationRows;
			for (int r = 0; r < config.AnimationRows; r++) {
				for (int c = 0; c < config.AnimationCols; c++) {
					Rect2I frameRegion = new(c * frameWidth, r * frameHeight, frameWidth, frameHeight);
					result.AddFrame(animationName,
						ImageTexture.CreateFromImage(fullImage.GetImage().GetRegion(frameRegion)),
						config.FrameDuration);
				}
			}

			return result;
		}

		return null;
	}

	// Helper method to extract crop region from a table
	private static CropRegion? ExtractCropRegion(Table table) {
		object cropRegionObj = table["crop_region"];
		if (cropRegionObj is Table cropRegion) {
			try {
				int x = Convert.ToInt32(cropRegion[1]);
				int y = Convert.ToInt32(cropRegion[2]);
				int w = Convert.ToInt32(cropRegion[3]);
				int h = Convert.ToInt32(cropRegion[4]);

				return new CropRegion(x, y, w, h);
			} catch (Exception ex) {
				throw new FormatException($"Invalid crop_region format: {ex.Message}", ex);
			}
		}

		return null;
	}

	// Helper method to handle alpha blend loading
	private static ImageTexture LoadWithAlphaBlend(string path, string alphaPath, CropRegion? cropRegion, int alphaRowOffset) {
		Pcx pcx = LoadPCX(path);
		Pcx alphaPcx = LoadPCX(alphaPath);

		return cropRegion.HasValue
			? PCXToGodot.getImageFromPCXWithAlphaBlend(pcx, alphaPcx, cropRegion.Value, alphaRowOffset)
			: PCXToGodot.getImageFromPCXWithAlphaBlend(pcx, alphaPcx);
	}

	private static object GetEntryByPath(string configKey) {
		string[] parts = configKey.Split('.');
		object current = textureConfig;

		foreach (string part in parts) {
			if (current is Table table && table[part] != null) {
				current = table[part];
			} else {
				return null;
			}
		}

		return current;
	}

	private static ImageTexture LoadFromPCX(string relPath, CropRegion? cropRegion = null, PCXToGodot.ColorOptions? colorOptions = null) {
		return GetOrAddTexture(relPath, cropRegion, () => {
			Pcx pcx = LoadPCX(relPath);
			return cropRegion is null
				? PCXToGodot.getImageTextureFromPCX(pcx)
				: PCXToGodot.getImageTextureFromPCX(pcx, cropRegion.Value, colorOptions ?? PCXToGodot.ColorOptions.Default);
		});
	}

	private static ImageTexture LoadFromPNG(string relPath, CropRegion? cropRegion = null) {
		return GetOrAddTexture(relPath, cropRegion, () => {
			Image image = LoadPNG(relPath);
			if (cropRegion != null) {
				var region = cropRegion.Value;
				return ImageTexture.CreateFromImage(
					image.GetRegion(new Rect2I(region.LeftStart, region.TopStart, region.CroppedWidth, region.CroppedHeight))
				);
			}
			return ImageTexture.CreateFromImage(image);
		});
	}

	private static string MakeCacheKey(string relPath, CropRegion? cropRegion) {
		if (cropRegion is null) return relPath;

		var region = cropRegion.Value;
		return $"{relPath}-{region.LeftStart}-{region.TopStart}-{region.CroppedWidth}-{region.CroppedHeight}";
	}

	private static ImageTexture GetOrAddTexture(string relPath, CropRegion? cropRegion, Func<ImageTexture> loader) {
		string key = MakeCacheKey(relPath, cropRegion);

		if (textureCache.TryGetValue(key, out ImageTexture cached))
			return cached;

		ImageTexture texture = loader();
		textureCache[key] = texture;
		return texture;
	}

	/**
	 * Utility method for loading PCX files that will cache them, so we don't have to load them from disk so often.
	 **/
	public static Pcx LoadPCX(string relPath) {
		if (PcxCache.TryGetValue(relPath, out Pcx value)) {
			return value;
		}
		Pcx thePcx = new(Util.Civ3MediaPath(relPath));
		PcxCache[relPath] = thePcx;
		return thePcx;
	}

	private static Image LoadPNG(string relPath) {
		if (PngCache.TryGetValue(relPath, out Image value)) {
			return value;
		}
		Image png = Image.LoadFromFile(Util.Civ3MediaPath(relPath));
		PngCache[relPath] = png;
		return png;
	}

	public static void ClearCache() {
		PcxCache.Clear();
		PngCache.Clear();
		textureCache.Clear();
		configKeyCache.Clear();
		objectMappingCache.Clear();
		animationCache.Clear();
		colorCache.Clear();
	}

	public static Vector2 Center(this ImageTexture tex) {
		return new Vector2(tex.GetWidth(), tex.GetHeight()) / 2;
	}
}
