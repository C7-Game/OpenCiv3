using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Serilog;

namespace C7Engine.Lua;

/// A rules engine that loads game behaviors defined in a Lua script.
///
/// Works with Lua scripts that return a nested table where the leaves
/// are functions.
///
/// The intended use case is during the game initialization (e.g.,
/// when loading a JSON save), to transform given Lua function paths
/// into C# delegates.
public class BehaviorEngine {
	static ILogger log = Log.ForContext<BehaviorEngine>();
	Script script = new();
	Table rules;

	private Dictionary<string, Delegate> funcCache = new Dictionary<string, Delegate>();

	public BehaviorEngine(Script script, Table rules) {
		this.script = script;
		this.rules = rules;
	}

	/// Loads a Lua function from a rules table by a given path and
	/// transforms it into C# delegate of the given generic type.
	///
	/// The path to the function should be a dot-separated string
	/// representing the path through the rules table
	/// (e.g. "building.production_rules.must_be_coastal")
	///
	///  If specified, the method will return the cached Delegate
	/// associated with the functionPath string.
	public T ImportFunc<T>(string functionPath) where T : Delegate {
		if (funcCache.TryGetValue(functionPath, out Delegate func)) {
			return (T)(object)func;
		}

		if (script == null || rules == null)
			throw new InvalidOperationException("Engine is not initialized");
		if (functionPath == null)
			throw new InvalidOperationException("Non-null function path expected");

		Closure closure = ResolveFunctionPath(functionPath);
		Delegate del = DelegateConverter.CreateDelegate(script, closure, typeof(T));

		funcCache[functionPath] = del;

		return (T)(object)del;
	}

	/// Loads a Lua function the same way as ImportFunc, but returns false
	/// instead of throwing when the path does not resolve to a function, so
	/// callers can make a function optional.
	public bool TryImportFunc<T>(string functionPath, out T func) where T : Delegate {
		if (string.IsNullOrEmpty(functionPath)) {
			func = null;
			return false;
		}

		if (funcCache.TryGetValue(functionPath, out Delegate cachedFunc)) {
			func = (T)(object)cachedFunc;
			return true;
		}

		if (script == null || rules == null) {
			func = null;
			return false;
		}

		Closure closure;
		try {
			closure = ResolveFunctionPath(functionPath);
		} catch (ArgumentException) {
			func = null;
			return false;
		}

		Delegate del = DelegateConverter.CreateDelegate(script, closure, typeof(T));

		funcCache[functionPath] = del;

		func = (T)(object)del;
		return true;
	}

	Closure ResolveFunctionPath(string functionPath) {
		string[] parts = functionPath.Split('.');
		Table current = rules;

		for (int i = 0; i < parts.Length; i++) {
			string part = parts[i];
			DynValue value = current.Get(part);

			if (value.Type == DataType.Function && i == parts.Length - 1)
				return value.Function;

			if (value.Type == DataType.Table)
				current = value.Table;
			else
				throw new ArgumentException($"Unexpected type at '{part}': '{value.Type}'");
		}

		throw new ArgumentException($"Function path '{functionPath}' did not resolve to a function.");
	}
}
