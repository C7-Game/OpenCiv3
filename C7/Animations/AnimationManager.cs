
// AnimationManager's purpose is to store the data associated with Civ 3 animations, for example the contents of each folder in Art/Units. It does lazy
// loading & memoization so each file is loaded only when it's needed and then stored so it is only ever loaded once per game. The main (and only)
// instance of AnimationManager is kept in Game, AnimationTracker holds a reference to it.

// It would be nice to load this data close to where it's used, e.g., have UnitLayer load the FlicSheets, instead of putting all the loading code in
// one detached class like this. That's how things originally worked but I created AnimationManager to solve two issues:
// 1. AnimationTracker and UnitLayer both need to load the unit INIs. So we either have a common place to store the INIs or duplication of work, and I
// think the former is the better choice.
// 2. AnimationTracker needs to know the duration of animations, which awkwardly cannot be determined based on the INI files alone. In order to know
// the duration of an anim you must know how many frames it has, and the only way to know that is to read its flic file.

// The intended usage is to access the animation data through a Civ3Anim object obtained through the "forUnit" or "forEffect" methods. For example:
//   AnimationManager.forUnit("Warrior", MapUnit.AnimatedAction.FORTIFY).playSound()
// To play the warrior's foritfy sound effect.

using System;
using System.Collections.Generic;
using Godot;
using IniParser;
using IniParser.Model;
using C7GameData;
using ConvertCiv3Media;

public partial class AnimationManager {

	public static string BaseAnimationKey(string unitName, MapUnit.AnimatedAction action) {
		return String.Format("{0}_{1}", unitName, action.ToString());
	}

	public static string BaseAnimationKey(MapUnit unit, MapUnit.AnimatedAction action) {
		return BaseAnimationKey(unit.GetArtName(), action);
	}

	public static string AnimationKey(string baseKey, TileDirection direction) {
		return String.Format("{0}_{1}", baseKey, direction.ToString());
	}

	public static string AnimationKey(MapUnit unit, MapUnit.AnimatedAction action, TileDirection direction) {
		return AnimationKey(BaseAnimationKey(unit, action), direction);
	}

	public static string AnimationKey(AnimatedEffect effect, MapUnit.AnimatedAction action) {
		return $"{effect.ToString()}_{action.ToString()}";
	}

	public static readonly Dictionary<string, ImageTexture> AnimationThumbnails = new();
	public static readonly Dictionary<string, ImageTexture> AnimationTintThumbnails = new();

	private const int thumbnailFrame = 0;
	private const TileDirection thumbnailDirection = TileDirection.SOUTHEAST;
	private const MapUnit.AnimatedAction thumbnailAction = MapUnit.AnimatedAction.DEFAULT;

	public SpriteFrames spriteFrames;
	public SpriteFrames tintFrames;

	private Dictionary<string, IniData> iniDatas = new Dictionary<string, IniData>();

	public AnimationManager() {
		this.spriteFrames = new SpriteFrames();
		this.tintFrames = new SpriteFrames();
	}

	public IniData getINIData(string pathKey) {
		if (!iniDatas.TryGetValue(pathKey, out IniData tr)) {
			string fullPath = Util.Civ3MediaPath(pathKey);
			tr = C7Engine.Util.GetFileIniDataParser().ReadFile(fullPath);
			iniDatas.Add(pathKey, tr);
		}
		return tr;
	}

	public static string GetUnitDefaultThumbnailKey(MapUnit unit) {
		return $"{unit.GetArtName()}_{thumbnailDirection}_{thumbnailAction}_{thumbnailFrame}";
	}

	public (ImageTexture baseFrame, ImageTexture tintFrame) GetAnimationFrameAndTintTextures(MapUnit unit) {

		string key = GetUnitDefaultThumbnailKey(unit);

		if (AnimationThumbnails.TryGetValue(key, out ImageTexture baseImage)
		   && AnimationTintThumbnails.TryGetValue(key, out ImageTexture tintImage)) {
			return (baseImage, tintImage);
		}

		string filepath = getUnitFlicFilepath(unit, thumbnailAction);

		Flic flic = Util.LoadFlic(filepath);

		byte[] rawFrame = flic.Images[flicAnimationDirectionToRow(thumbnailDirection), thumbnailFrame];
		// This actually doesn't return the tint frame with the civ color applied. The shader still needs to be applied.
		(ImageTexture baseFrame, ImageTexture tintFrame) = Util.LoadTextureFromFlicData(rawFrame, flic.Palette, flic.Width, flic.Height);
		AnimationThumbnails[key] = baseFrame;
		AnimationTintThumbnails[key] = tintFrame;

		return (baseFrame, tintFrame);
	}

	// Looks up the name of the flic file associated with a given action in an animation INI. If there is no flic file listed for the action,
	// returns instead the file name for the default action, and if that's missing too, throws an exception.
	public string getFlicFileName(IniData iniData, MapUnit.AnimatedAction action) {
		string fileName = iniData["Animations"][action.ToString()];
		if (!string.IsNullOrEmpty(fileName)) {
			return fileName;
		} else if (action != MapUnit.AnimatedAction.DEFAULT) {
			return getFlicFileName(iniData, MapUnit.AnimatedAction.DEFAULT);
		} else {
			throw new Exception("Missing default animation"); // TODO: Add the INI's file name to the error message
		}
	}

	public IniData getUnitINIData(string unitTypeName) {
		return getINIData(string.Format("Art/Units/{0}/{0}.INI", unitTypeName));
	}

	public string GetFlicFilePath(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		return rootPath + "/" + getFlicFileName(iniData, action);
	}

	public string getUnitFlicFilepath(MapUnit unit, MapUnit.AnimatedAction action) {
		string directory = string.Format("Art/Units/{0}", unit.GetArtName());
		IniData ini = getUnitINIData(unit.GetArtName());
		string filename = getFlicFileName(ini, action);
		return directory.PathJoin(filename);
	}

	// The flic loading code parses the animations into a 2D array, where each row is an animation
	// corresponding to a tile direction. flicRowToAnimationDirection maps row number -> direction.
	private static TileDirection flicRowToAnimationDirection(int row) {
		switch (row) {
			case 0: return TileDirection.SOUTHWEST;
			case 1: return TileDirection.SOUTH;
			case 2: return TileDirection.SOUTHEAST;
			case 3: return TileDirection.EAST;
			case 4: return TileDirection.NORTHEAST;
			case 5: return TileDirection.NORTH;
			case 6: return TileDirection.NORTHWEST;
			case 7: return TileDirection.WEST;
		}
		// Rows beyond the 8 real directions are unexpected. Return INVALID rather
		// than guessing a direction, so they don't interfere with the others.
		return TileDirection.INVALID;
	}

	private static int flicAnimationDirectionToRow(TileDirection tileDirection) {
		switch (tileDirection) {
			case TileDirection.SOUTHWEST: return 0;
			case TileDirection.SOUTH: return 1;
			case TileDirection.SOUTHEAST: return 2;
			case TileDirection.EAST: return 3;
			case TileDirection.NORTHEAST: return 4;
			case TileDirection.NORTH: return 5;
			case TileDirection.NORTHWEST: return 6;
			case TileDirection.WEST: return 7;
		}
		return 2;
	}

	public static void loadFlicAnimation(string path, string name, ref SpriteFrames frames, ref SpriteFrames tint) {
		Flic flic = Util.LoadFlic(path);

		for (int row = 0; row < flic.Images.GetLength(0); row++) {
			string direction = flicRowToAnimationDirection(row).ToString();
			string animationName = name + "_" + direction;
			frames.AddAnimation(animationName);
			tint.AddAnimation(animationName);

			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row,col];
				(ImageTexture bl, ImageTexture tl) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				frames.AddFrame(animationName, bl, 0.5f); // TODO: frame duration is controlled by .ini
				tint.AddFrame(animationName, tl, 0.5f);   // TODO: frame duration is controlled by .ini
			}
		}
	}

	public static void loadFlicEffectAnimation(string path, string name, ref SpriteFrames frames, ref SpriteFrames tint) {
		Flic flic = Util.LoadFlic(path);

		for (int row = 0; row < flic.Images.GetLength(0); row++) {
			string animationName = name;
			frames.AddAnimation(animationName);
			tint.AddAnimation(animationName);

			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row,col];
				(ImageTexture bl, ImageTexture tl) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				frames.AddFrame(animationName, bl, 0.5f); // TODO: frame duration is controlled by .ini
				tint.AddFrame(animationName, tl, 0.5f);   // TODO: frame duration is controlled by .ini
			}
		}
	}

	public bool LoadAnimation(MapUnit unit, MapUnit.AnimatedAction action) {
		string name = BaseAnimationKey(unit.GetArtName(), action);
		string testName = AnimationKey(name, TileDirection.NORTH);
		if (spriteFrames.HasAnimation(testName) && tintFrames.HasAnimation(testName)) {
			return false;
		}
		string filepath = getUnitFlicFilepath(unit, action);
		loadFlicAnimation(filepath, name, ref this.spriteFrames, ref this.tintFrames);
		return true;
	}

	public bool LoadAnimation(AnimatedEffect effect, MapUnit.AnimatedAction action, string flicFilePath) {
		string name = AnimationKey(effect, action);
		if (spriteFrames.HasAnimation(name) && tintFrames.HasAnimation(name)) {
			return false;
		}
		loadFlicEffectAnimation(flicFilePath, name, ref this.spriteFrames, ref this.tintFrames);
		return true;
	}

	private Dictionary<string, Util.FlicSheet> flicSheets = new Dictionary<string, Util.FlicSheet>();

	public Util.FlicSheet getFlicSheet(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		Util.FlicSheet tr;
		string pathKey = GetFlicFilePath(rootPath, iniData, action);
		if (!flicSheets.TryGetValue(pathKey, out tr)) {
			(tr, _) = Util.loadFlicSheet(pathKey);
			flicSheets.Add(pathKey, tr);
		}
		return tr;
	}

	public void playSound(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		string fileName = iniData["Sound Effects"][action.ToString()];
		if (fileName.EndsWith(".WAV", StringComparison.CurrentCultureIgnoreCase)) {
			string pathKey = rootPath + "/" + fileName;
			GlobalAudio.manager.PlaySfxAudioByPath(pathKey);
		}
	}

	public C7Animation forUnit(MapUnit unit, MapUnit.AnimatedAction action) {
		return new C7Animation(this, unit, action);
	}

	public C7Animation forEffect(AnimatedEffect effect) {
		return new C7Animation(this, effect);
	}

	public static void ClearCache() {
		AnimationThumbnails.Clear();
		AnimationTintThumbnails.Clear();
	}
}

public partial class C7Animation {
	public AnimationManager animationManager { get; private set; }
	public string folderPath { get; private set; } // For example "Art/Units/Warrior" or "Art/Animations/Trajectory"
	public string iniFileName { get; private set; }
	private MapUnit unit;
	public AnimatedEffect effect;
	public MapUnit.AnimatedAction action { get; private set; }

	public C7Animation(AnimationManager civ3AnimData, MapUnit unit, MapUnit.AnimatedAction action) {
		this.animationManager = civ3AnimData;
		this.folderPath = "Art/Units/" + unit.GetArtName();
		this.iniFileName = unit.GetArtName() + ".ini";
		this.action = action;
		this.unit = unit;
	}

	public static readonly Dictionary<AnimatedEffect, string> effectCategories = new Dictionary<AnimatedEffect, string>
	{
		{ AnimatedEffect.Hit      , "Trajectory" },
		{ AnimatedEffect.Hit2     , "Trajectory" },
		{ AnimatedEffect.Hit3     , "Trajectory" },
		{ AnimatedEffect.Hit5     , "Trajectory" },
		{ AnimatedEffect.Miss     , "Trajectory" },
		{ AnimatedEffect.WaterMiss, "Trajectory" }
	};

	public static readonly Dictionary<AnimatedEffect, string> effectINIFileNames = new Dictionary<AnimatedEffect, string>
	{
		{ AnimatedEffect.Hit      , "hit.ini" },
		{ AnimatedEffect.Hit2     , "hit2.ini" },
		{ AnimatedEffect.Hit3     , "hit3.ini" },
		{ AnimatedEffect.Hit5     , "hit5.ini" },
		{ AnimatedEffect.Miss     , "miss.ini" },
		{ AnimatedEffect.WaterMiss, "water miss.ini" }
	};

	public C7Animation(AnimationManager civ3AnimData, AnimatedEffect effect) {
		this.animationManager = civ3AnimData;
		this.folderPath = "Art/Animations/" + effectCategories[effect];
		this.iniFileName = effectINIFileNames[effect];
		this.action = MapUnit.AnimatedAction.DEATH;
		this.effect = effect;
	}

	public IniData getINIData() {
		return animationManager.getINIData(folderPath + "/" + iniFileName);
	}

	public Util.FlicSheet getFlicSheet() {
		return animationManager.getFlicSheet(folderPath, getINIData(), action);
	}

	public void loadSpriteAnimation() {
		this.animationManager.LoadAnimation(this.unit, this.action);
	}

	public void loadEffectAnimation() {
		var path = animationManager.GetFlicFilePath(folderPath, getINIData(), action);
		this.animationManager.LoadAnimation(this.effect, this.action, path);
	}

	public void playSound() {
		animationManager.playSound(folderPath, getINIData(), action);
	}

	public double getDuration() {
		Util.FlicSheet flicSheet = getFlicSheet();
		return flicSheet.animationSpeed * flicSheet.framesPerAnimation;
	}

	public Vector2I GetFlicAnimationOffset() {
		return new Vector2I(getFlicSheet().offsetLeft, getFlicSheet().offsetTop);
	}

	public Vector2I GetFlicAnimationOriginalSize() {
		return new Vector2I(getFlicSheet().spriteOriginalWidth, getFlicSheet().spriteOriginalHeight);
	}
}
