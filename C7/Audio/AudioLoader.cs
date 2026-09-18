using System;
using Godot;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using Script = MoonSharp.Interpreter.Script;
using C7Engine.Lua;

public static class AudioLoader {

	private static Script lua;
	private static Table audioConfig;

	private static Dictionary<string, AudioStream> configKeyCache = [];

	static AudioLoader() {
		// Initialize when running in the editor
		// In game it is done by GlobalSingleton, but it's not accessible in the editor
		if (Engine.IsEditorHint()) {
			GameMode gameMode = GameMode.Load(GamePaths.GameModesDir, GamePaths.basic);
			var (script, table) = gameMode.audio;
			AudioLoader.SetConfig(script, table);
		}
	}

	public static void SetConfig(Script lua, Table audioConfig) {
		ClearCache();

		AudioLoader.lua = lua;
		AudioLoader.audioConfig = audioConfig;
	}

	/// Returns an audio stream based on the config key.
	/// The config key should be a string separated by dots, representing the path through the
	/// configuration hierarchy (e.g., "menu.main_menu_1").
	public static AudioStream Load(string configKey) {
		if (configKeyCache.TryGetValue(configKey, out AudioStream cachedAudio))
			return cachedAudio;

		object entry = GetEntryByPath(configKey);
		if (entry == null)
			throw new Exception($"Audio config not found for key: {configKey}");

		object entry2 = GetEntryByModPath(configKey);

		AudioStream audioStream = LoadFromLuaObject(entry2 ?? entry);

		configKeyCache[configKey] = audioStream;

		return audioStream;
	}

	private static object GetEntryByModPath(string configKey) {
		object current = audioConfig;

		if (current is not Table table)
			throw new Exception($"Root is not table");

		if (table["map_object_to_sprite"] is not Closure func)
			return null;

		var arg = DynValue.FromObject(lua, configKey);
		object result = lua.SafeCall(func, arg).ToObject();

		return result;
	}

	private static AudioStream LoadFromLuaObject(object entry) {
		return LoadFromPath(ParsePath(entry));
	}

	private static string ParsePath(object entry) {
		if (entry is string simplePath) {
			return simplePath;
		}

		throw new ArgumentException($"Invalid audio config format: {entry?.GetType().Name ?? "null"}");
	}

	private static AudioStream LoadFromPath(string path) {
		string ext = Path.GetExtension(path).ToLowerInvariant();

		return ext switch {
			".wav" => Util.LoadCiv3WAVFromDisk(path),
			".mp3" => Util.LoadCiv3Mp3FromDisk(path),
			".ogg" => Util.LoadCiv3OggFromDisk(path),
			".oga" => Util.LoadCiv3OggFromDisk(path),
			_ => throw new FormatException($"Unknown audio format: {path}"),
		};
	}

	private static object GetEntryByPath(string configKey) {
		string[] parts = configKey.Split('.');
		object current = audioConfig;

		foreach (string part in parts) {
			if (current is Table table && table[part] != null) {
				current = table[part];
			} else {
				return null;
			}
		}

		return current;
	}

	public static void ClearCache() {
		configKeyCache.Clear();
	}
}
