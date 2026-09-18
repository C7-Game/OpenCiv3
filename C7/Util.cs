using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using C7.Textures;
using C7Engine;
using C7Engine.Lua;
using ConvertCiv3Media;
using Godot;
using QueryCiv3;

public partial class Util {
	static public string Civ3Root = GetCiv3Path();

	static public string GetCiv3Path() {
		string path = C7Settings.GetSettingValue("locations", "civ3InstallDir");
		if (path != null) return path;

		return Civ3Location.GetCiv3Path();
	}

	// Checks if a file exists ignoring case on the latter parts of its path. If the file is found, returns its full path re-capitalized as
	// necessary, otherwise returns null. This function is needed for the game to work on Linux & Mac with the .NET Core runtime. It's not needed
	// on Windows, which has a case insensitive filesystem, or when using the Mono runtime, which emulates case insensitivity out of the
	// box. Arguments:
	//   exactCaseRoot: The first part of the file path, not made case-insensitive. This is intended be the root Civ 3 path from GetCiv3Path().
	//   ignoredCaseExtension: The second part of the file path that will be searched ignoring case.
	public static string FileExistsIgnoringCase(string exactCaseRoot, string ignoredCaseExtension) {
		// In debug builds paths that are specified relative to the project directory
		// will start with res://. We want to turn those into real paths.
		if (exactCaseRoot.Contains("res://")) {
			exactCaseRoot = ProjectSettings.GlobalizePath(exactCaseRoot);
		}

		// If we aren't on windows, fix any windows path separators that may
		// appear in the path. This is particularly relevant for scenarios,
		//where the mod path frequently looks like  '..\conquests\Rise of Rome'.
		//
		// This is a bit of a hack, but it fixes scenario loading.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			ignoredCaseExtension = ignoredCaseExtension.Replace('\\', '/');
		}

		// First try the basic built-in File.Exists method since it's adequate
		// in most cases.
		//
		// We use GetFullPath to handle any ../Conquests/<scenario name> bits
		// in the path, which happens with scenario mod paths.
		string fullPath = System.IO.Path.Combine(exactCaseRoot, ignoredCaseExtension);
		fullPath = System.IO.Path.GetFullPath(fullPath);
		if (System.IO.File.Exists(fullPath))
			return fullPath;

		// If that didn't work, do a case-insensitive search starting at the root path and stepping through each piece of the extension. Skip
		// this step if the root directory doesn't exist or if running on Windows.
		string tr = null;
		if ((!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) &&
			System.IO.Directory.Exists(exactCaseRoot)) {
			tr = exactCaseRoot;

			// We need to update the ignored case extension before doing this
			// search, in case the ignored case extension previously had
			// ../Conquests/<scenario name> in it.
			//
			// If we didn't do this we'd end up with ".." as one of our steps
			// below, which derails the searching logic.
			//
			// We also strip any leading slashes, which can show up if the civ3
			// root doesn't end in a slash.
			ignoredCaseExtension = fullPath.Substring(exactCaseRoot.Length);
			ignoredCaseExtension = ignoredCaseExtension.TrimPrefix("\\").TrimPrefix("/");

			foreach (string step in ignoredCaseExtension.Replace('\\', '/').Split('/')) {
				string goal = System.IO.Path.Combine(tr, step);
				List<string> matches = System.IO.Directory.EnumerateFileSystemEntries(tr, "*")
					.Where(p => p.Equals(goal, StringComparison.CurrentCultureIgnoreCase))
					.ToList();

				if (matches.Count > 0)
					tr = matches[0];
				else {
					tr = null;
					break;
				}
			}
		}
		return tr;
	}

	/// <summary>
	/// Sets the Civ3 legacy mod path.
	/// This is here so Civ3MediaPath can refer to it, without having to grab it from all the places we might need to call
	/// it, which is in 25 places currently.
	/// </summary>
	private static string modPath;
	public static void setModPath(string modPathParam) {
		modPath = modPathParam;
		// Specifically fix up the conquests mod path for case sensitive
		// platforms. If we didn't do this then our path searching logic below
		// would find the default PediaIcons.txt instead of the scenario
		// specific file.
		modPath = modPath.Replace("\\conquests\\", "\\Conquests\\");
	}

	/// <summary>
	/// Pass this function a relative path (e.g. Art/Terrain/xpgc.pcx) and it will grab the correct version
	/// Assumes Conquests/Complete
	/// </summary>
	/// <param name="mediaPath">The media path, e.g. Art/Units/units_32.pcx</param>
	/// <returns>The path to the media on the file system, or an exception if it cannot be found</returns>
	/// <exception cref="ApplicationException"></exception>
	public static string Civ3MediaPath(string mediaPath) {
		//First, check if the file exists via a scenario's mod path
		//For now this is only checked relative to Civ3, not relative to C7.
		if (!string.IsNullOrEmpty(modPath)) {
			string[] paths = modPath.Split(";");
			foreach (string path in paths) {
				string[] tryPaths = new string[] {
					path,
					// Needed for some reason as Steam version at least puts some mod art in Extras instead of Scenarios
					//  Also, the case mismatch is intentional. C3C makes a capital C path, but it's lower-case on the filesystem
					"Conquests/Conquests/" + path, "Conquests/Scenarios/" + path, "civ3PTW/Scenarios/" + path
				};
				for (int i = 0; i < tryPaths.Length; i++) {
					string actualCasePath = CheckForCiv3Media(mediaPath, tryPaths[i]);
					if (actualCasePath != null)
						return actualCasePath;
				}
			}
		}

		// Next, before trying the base Civ paths, see if we have it packaged
		// with C7 and are in standalone mode.
		string c7BasePath = GetC7AssetsPath();
		string c7Path = FileExistsIgnoringCase(c7BasePath, mediaPath);

		if (C7Settings.UseStandaloneMode()) {
			if (c7Path != null) {
				return c7Path;
			} else {
				throw new ApplicationException("Media path not found: " + mediaPath);
			}
		}

		//Next, check the base Civ paths
		string[] basePaths = new string[] {
			"Conquests",
			"civ3PTW",
			""
		};
		for (int i = 0; i < basePaths.Length; i++) {
			string actualCasePath = CheckForCiv3Media(mediaPath, basePaths[i]);
			if (actualCasePath != null)
				return actualCasePath;
		}

		// Finally, use a c7 path even if we aren't in standalone mode, but only
		// if we can't find the path in civ3 graphics. This allows us to toggle
		// to c7 art when we aren't in standalone mode.
		if (c7Path != null) {
			return c7Path;
		}

		throw new ApplicationException("Media path not found: " + mediaPath);
	}

	private static string CheckForCiv3Media(string relPath, string rootPath) {
		// Combine TryPaths[i] and relPath. Make sure not to leave an erroneous forward slash at the start if TryPaths[i] is empty
		string fullPath = rootPath != "" ? rootPath + "/" + relPath : relPath;

		return FileExistsIgnoringCase(Civ3Root, fullPath);
	}

	static public (ImageTexture, ImageTexture) LoadTextureFromFlicData(byte[] image, byte[,] pallete, int width, int height) {
		var (baseImage, tintImage) = PCXToGodot.ByteArrayWithTintToImage(image, pallete, width, height, shadows: true);
		return (ImageTexture.CreateFromImage(baseImage), ImageTexture.CreateFromImage(tintImage));
	}

	private static Dictionary<string, Flic> flicCache = new();

	static public Flic LoadFlic(string path) {
		if (flicCache.ContainsKey(path)) {
			return flicCache[path];
		}
		Flic result = new ConvertCiv3Media.Flic(Util.Civ3MediaPath(path));
		flicCache[path] = result;
		return result;
	}

	static private string GetC7AssetsPath() {
		if (OS.HasFeature("editor")) {
			return "res://Assets";
		}

		return System.IO.Path.Combine(GamePaths.BaseDir, "Assets");
	}

	// Replaces image colors based on a given dictionary
	public static Image TransformColors(Image origin, Dictionary<Color, Color> colorReplacements) {
		Image result = (Image)origin.Duplicate();

		for (int y = 0; y < origin.GetHeight(); ++y) {
			for (int x = 0; x < origin.GetWidth(); ++x) {
				Color origin_color = origin.GetPixel(x, y);

				if (colorReplacements.TryGetValue(origin_color, out Color new_color)) {
					result.SetPixel(x, y, new_color);
				}
			}
		}

		return result;
	}

	// Creates a texture from raw palette data. The data must be 256 pixels by 3 channels. Returns a 16x16 unfiltered RGB texture.
	public static ImageTexture createPaletteTexture(byte[,] raw) {
		if ((raw.GetLength(0) != 256) || (raw.GetLength(1) != 3))
			throw new Exception("Invalid palette dimensions. Palettes must be 256x3.");

		// Flatten palette data since CreateFromData can't accept two-dimensional arrays
		byte[] flatPalette = new byte[3*256];
		for (int n = 0; n < 256; n++)
			for (int k = 0; k < 3; k++)
				flatPalette[k + 3 * n] = raw[n, k];

		var img = Image.CreateFromData(16, 16, false, Image.Format.Rgb8, flatPalette);
		return ImageTexture.CreateFromImage(img);
	}

	// A FlicSheet is a sprite sheet created from a Flic file, with each frame of the animation as its own sprite
	public struct FlicSheet {
		public ImageTexture palette, indices;
		public int spriteOriginalWidth, spriteOriginalHeight;
		public int spriteWidth, spriteHeight;
		public int offsetLeft, offsetTop;
		public int framesPerAnimation, numberOfAnimations;
		public int animationSpeed, animationTime;
	}

	// Loads a Flic and also converts it into a sprite sheet
	public static (FlicSheet, Flic) loadFlicSheet(string filePath) {
		var flic = new Flic(Util.Civ3MediaPath(filePath));

		var texPalette = Util.createPaletteTexture(flic.Palette);

		var countColumns = flic.Images.GetLength(1); // Each column contains one frame
		var countRows = flic.Images.GetLength(0); // Each row contains one animation
		var countImages = countColumns * countRows;

		byte[] allIndices = new byte[countRows * countColumns * flic.Width * flic.Height];
		// row, col loop over the sprites, each one a frame of the animation
		for (int row = 0; row < countRows; row++)
			for (int col = 0; col < countColumns; col++)
				// x, y loop over pixels within each sprite
				for (int y = 0; y < flic.Height; y++)
					for (int x = 0; x < flic.Width; x++) {
						int pixelRow = row * flic.Height + y,
							pixelCol = col * flic.Width + x,
							pixelIndex = pixelRow * countColumns * flic.Width + pixelCol;
						allIndices[pixelIndex] = flic.Images[row, col][y * flic.Width + x];
					}

		var imgIndices = Image.CreateFromData(countColumns * flic.Width, countRows * flic.Height, false, Image.Format.R8, allIndices);
		ImageTexture texIndices = ImageTexture.CreateFromImage(imgIndices);

		return (new FlicSheet {
			palette = texPalette,
			indices = texIndices,
			spriteOriginalWidth = flic.OriginalWidth,
			spriteOriginalHeight = flic.OriginalHeight,
			spriteWidth = flic.Width,
			spriteHeight = flic.Height,
			offsetLeft = flic.OffsetLeft,
			offsetTop = flic.OffsetTop,
			framesPerAnimation = flic.FramesPerAnimation,
			numberOfAnimations = flic.NumAnimations,
			animationSpeed = flic.AnimationSpeed,
			animationTime = flic.AnimationTime
		}, flic);
	}

	// Like LoadWAVFromDisk, but the path is a relative path, not the result of
	// calling Civ3MediaPath.
	//
	// The result may be null if modern graphics are active, as we do not yet
	// have sound replacements.
	public static AudioStreamWav? LoadCiv3WAVFromDisk(string path) {
		try {
			return LoadWAVFromDisk(Civ3MediaPath(path));
		} catch (Exception e) {
			return null;
		}
	}

	public static AudioStreamWav LoadWAVFromDisk(string path) {
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

		byte[] riffBytes = file.GetBuffer(4);
		if (!"RIFF"u8.SequenceEqual(riffBytes)) {
			throw new Exception("Unsupported file, missing 'RIFF' tag");
		}
		uint fileSize = file.Get32();   //minus 8 bytes

		byte[] waveBytes = file.GetBuffer(4);
		if (!"WAVE"u8.SequenceEqual(waveBytes)) {
			throw new Exception("Unsupported file, missing 'WAVE' tag");
		}

		bool formatFound = false;
		bool dataFound = false;

		AudioStreamWav wav = new();

		while (!file.EofReached()) {
			byte[] chunkBytes = file.GetBuffer(4);
			uint chunkSize = file.Get32();
			ulong position = file.GetPosition();

			if (file.EofReached()) {
				//May occur with e.g. an empty junk chunk
				break;
			}

			if ("fmt "u8.SequenceEqual(chunkBytes))    //format chunk
			{
				//There is some disagreement between the C++ and GDScript sources
				//as to which compression codes Godot supports.  The C++ has a comment
				//saying, "Consider revision for engine version 3.0", and noting other
				//formats are not supported in its importer.  The GDScript seems
				//to match up with the current FormatEnum.  I'm going to go out on
				//a limb and say the GDScript is probably more current relative
				//to what AudioStreamWAV supports.  But that could be wrong.
				ushort compressionCode = file.Get16();
				if (compressionCode == 1) {
					wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
				} else if (compressionCode == 0) {
					wav.Format = AudioStreamWav.FormatEnum.Format8Bits;
				} else if (compressionCode == 2) {
					wav.Format = AudioStreamWav.FormatEnum.ImaAdpcm;
				}

				ushort channels = file.Get16();
				if (channels == 2) {
					wav.Stereo = true;
				} else if (channels < 1 || channels > 5) {
					throw new Exception("Only mono and stream WAV files supported");
				}

				uint sampleRate = file.Get32();
				wav.MixRate = (int)sampleRate;

				uint averageBPS = file.Get32(); //unused
				ushort blockAlign = file.Get16();   //unused
				ushort formatBits = file.Get16();

				if (formatBits % 8 != 0 || formatBits == 0) {
					throw new Exception("Format bits must be a multiple of 8");
				}
				formatFound = true;
			} else if ("data"u8.SequenceEqual(chunkBytes)) {
				byte[] allTheData = file.GetBuffer(chunkSize);
				wav.Data = allTheData;
				dataFound = true;
			}

			file.Seek(position + chunkSize);
		}

		if (!formatFound || !dataFound) {
			throw new Exception("Failed to find both the format and data chunks");
		}

		return wav;
	}

	public static AudioStreamMP3? LoadCiv3Mp3FromDisk(string path) {
		try {
			return LoadMp3FromDisk(Civ3MediaPath(path));
		} catch (Exception e) {
			return null;
		}
	}

	public static AudioStreamMP3 LoadMp3FromDisk(string path) {
		AudioStreamMP3 mp3 = AudioStreamMP3.LoadFromFile(path);
		mp3.Loop = true; // TODO: beyond simple path loading: add looping as a property
		return mp3;
	}

	public static AudioStreamOggVorbis? LoadCiv3OggFromDisk(string path) {
		try {
			return LoadOggFromDisk(Civ3MediaPath(path));
		} catch (Exception e) {
			return null;
		}
	}

	public static AudioStreamOggVorbis LoadOggFromDisk(string path) {
		AudioStreamOggVorbis ogg = AudioStreamOggVorbis.LoadFromFile(path);
		ogg.Loop = true;
		return ogg;
	}


	// This method is intended for use within overrides of Godot object _ValidateProperty method.
	// Its purpose is to prevent values of properties listed in validProperties from being saved as
	// part of the scene.  It's useful when using [Tool] scripts to execute code in editor. It
	// allows to load Civ3 textures as part of such scripts, but prevents these textures from being
	// saved into the scene.
	//
	// See Godot docs on _ValidateProperty:
	// https://docs.godotengine.org/en/4.2/classes/class_object.html#class-object-private-method-validate-property
	public static void ApplyNoSaveFlag(Godot.Collections.Dictionary property, HashSet<StringName> validProperties) {
		StringName propertyName = property["name"].AsStringName();

		if (validProperties.Contains(propertyName)) {
			property["usage"] = (int)PropertyUsageFlags.NoInstanceState;
		}
	}

	// Allow clearing the caches, so that scenarios with different files that
	// have the same name can be loaded independently.
	public static void ClearCaches() {
		flicCache.Clear();
		TextureLoader.ClearCache();
		AnimationManager.ClearCache();
		PlayerTextureUtil.ClearCache();
	}
}
