using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using Serilog;
using C7GameData.Save;
using MoonSharp.Interpreter;
using System.Reflection;
using System.Linq;
using MoonSharp.Interpreter.Loaders;

namespace C7Engine.Lua;

public class GameMode {
	public class Config {
		// The directory with the base scenario definition
		public string baseModeDir;

		// Directories with addon definitions.
		public List<string> addonPaths = [];

		public Config(string baseModeDir, List<string> addonPaths = null) {
			this.baseModeDir = baseModeDir;
			this.addonPaths = addonPaths ?? [];
		}
	}

	internal SaveGame ruleset;
	public BehaviorEngine behaviors;
	public (Script, Table) textures;
	public (Script, Table) audio;

	// Returns a deep copy of the ruleset
	//
	// We don't return the ruleset directly, since SaveGame is
	// modified during game creation (see GameSetup). By returning a
	// copy we make it possible to reuse the ruleset for creating
	// other games
	public SaveGame GetSave() {
		return ruleset.Clone();
	}

	// gameModesDir is a directory where game mode definitions are located.
	// It is used as a prefix to paths specified in GameMode.Config
	public static GameMode Load(string gameModesDir, Config config) {
		return new GameModeLoader(gameModesDir, config).Load();

	}
}

// Loads OpenCiv3 scenario definitions
internal class GameModeLoader {
	enum ScriptType {
		Textures,
		Ruleset,
		Behaviors,
		Audio
	}

	private static ILogger log = Log.ForContext<GameModeLoader>();

	Script lua;
	JsonConverter converter;
	GameMode.Config config;
	string gameModesDir;

	public GameModeLoader(string gameModesDir, GameMode.Config config) {
		lua = ScriptInitializer.Initialize();
		converter = new(lua);

		this.config = config;
		this.gameModesDir = gameModesDir;
	}

	public GameMode Load() {
		var LoadWithAddons = (ScriptType scriptType) => {
			return LoadAddons(LoadBase(scriptType), scriptType);
		};

		Table behaviors = LoadWithAddons(ScriptType.Behaviors).Table;
		Table textures = LoadWithAddons(ScriptType.Textures).Table;
		Table audio = LoadWithAddons(ScriptType.Audio).Table;

		return new() {
			ruleset = LoadRuleset(),
			behaviors = new(lua, behaviors),
			textures = (lua, textures),
			audio = (lua, audio)
		};
	}

	private SaveGame LoadRuleset() {
		// Special case: we allow to store the base ruleset both as Lua and JSON
		DynValue ruleset;

		string jsonPath = Path.Combine(gameModesDir, config.baseModeDir, "ruleset.json");
		if (File.Exists(jsonPath)) {
			ruleset = converter.Decode(File.ReadAllText(jsonPath));
		} else {
			ruleset = LoadBase(ScriptType.Ruleset);
		}

		ruleset = LoadAddons(ruleset, ScriptType.Ruleset);

		// Convert final Lua table to JSON for deserialization
		string json = converter.Encode(ruleset);

		var saveGame = SaveGame.LoadFromJSON(json);
		saveGame.GameModeConfig = config;

		return saveGame;
	}

	private DynValue LoadBase(ScriptType scriptType) {
		string fullBaseModePath = GetScriptPath(config.baseModeDir, scriptType);
		log.Information("Loading base game mode file: {baseModePath}", fullBaseModePath);

		return LoadScript(config.baseModeDir, fullBaseModePath);
	}

	private DynValue LoadAddons(DynValue baseTable, ScriptType scriptType) {
		// Load base scenario from JSON or Lua file
		DynValue current = baseTable;

		// Process through addon scripts
		foreach (string addonPath in config.addonPaths) {
			string scriptPath = GetScriptPath(addonPath, scriptType);

			if (!File.Exists(scriptPath)) {
				continue;
			}

			log.Information("Loading addon: {scriptPath}", scriptPath);

			DynValue addon = LoadScript(addonPath, scriptPath);

			if (addon.Type != DataType.Function)
				throw new InvalidOperationException(
					$"Addon '{scriptPath}' must return a function"
				);

			// Each addon function modifies and returns the scenario data
			current = lua.SafeCall(addon.Function, current);
		}

		return current;
	}

	private string GetScriptPath(string addonDir, ScriptType scriptType) {
		string scriptFile = scriptType switch {
			ScriptType.Textures => "textures.lua",
			ScriptType.Ruleset => "ruleset.lua",
			ScriptType.Behaviors => "behaviors.lua",
			ScriptType.Audio => "audio.lua",
			_ => throw new InvalidOperationException("Unknown script type"),
		};
		return Path.Combine(gameModesDir, addonDir, scriptFile);
	}

	private DynValue LoadScript(string addonDir, string scriptPath) {
		SetLoaderPath(addonDir);

		DynValue script = lua.SafeDoFile(scriptPath);

		UnsetLoaderPath();

		return script;
	}

	private void SetLoaderPath(string addonDir) {
		lua.Options.ScriptLoader = new FileSystemScriptLoader {
			ModulePaths = [
				Path.Combine(gameModesDir, addonDir, "?.lua"),
				Path.Combine(gameModesDir, addonDir, "*", "?.lua")
			]
		};
	}

	private void UnsetLoaderPath() {
		lua.Options.ScriptLoader = new FileSystemScriptLoader {
			ModulePaths = []
		};
	}
}

/// When initializing the Lua state, this class registers all public
/// types in the C7GameData namespace as Moonsharp userdata, which
/// exposes them to the scripting engine.
///
/// The class registers the following Lua globals:
/// - GAME_DATA(): Function returning the current game data instance.
/// - ENUMS:  Table containing definitions of public enums from the C7GameData namespace.
///   Note that nested enums are registered under a name concatenating the outer class and the enum names (e.g., Tile_YieldType)
internal class ScriptInitializer {
	static ILogger log = Log.ForContext<ScriptInitializer>();

	static ScriptInitializer() {
		RegisterTypes();
	}

	static public Script Initialize() {
		Script script = new();
		RegisterEnums(script);
		RegisterGlobals(script);
		return script;
	}

	static void RegisterTypes() {
		var types = Assembly.GetExecutingAssembly()
							.GetTypes()
							.Where(t =>
									(t.IsPublic || t.IsNestedPublic)
									&& t.Namespace == "C7GameData");

		foreach (var type in types) {
			log.Debug("Registering type: {typeName}", type.FullName);
			UserData.RegisterType(type);
		}
	}

	static void RegisterEnums(Script script) {
		var enumTypes = Assembly.GetExecutingAssembly()
							.GetTypes()
							.Where(t =>
								t.IsEnum &&
								(t.IsPublic || t.IsNestedPublic) &&
								t.Namespace == "C7GameData");

		Table enumTable = new(script);

		foreach (var enumType in enumTypes) {
			// For nested enums, concatenate the outer class name with the enum name
			string registrationName = enumType.DeclaringType != null
			? $"{enumType.DeclaringType.Name}_{enumType.Name}"  // e.g., Tile_YieldType
            : enumType.Name;

			log.Debug("Registering enum: {EnumType} as {RegistrationName}", enumType.FullName, registrationName);

			var enumUserData = UserData.CreateStatic(enumType);
			enumTable[registrationName] = enumUserData;
		}

		script.Globals["ENUMS"] = enumTable;
	}

	static void RegisterGlobals(Script script) {
		script.Globals["GAME_DATA"] = () => DynValue.FromObject(script, EngineStorage.gameData);
	}
}
