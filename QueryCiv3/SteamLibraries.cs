using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QueryCiv3 {
	// Minimal reader for Valve's KeyValues text format (the ".vdf" files
	// Steam keeps its library configuration in). This is deliberately not a
	// general VDF library: it only understands quoted/unquoted strings,
	// nested sections, // comments and [conditional] tags, which is all the
	// library and config files use. It exists so Civ3Location can find a
	// Steam install of Civ3 without taking a new package dependency for two
	// small files.
	public class VdfNode {
		public string Name { get; }
		public string Value { get; } // null for section nodes
		public List<VdfNode> Children { get; } = new List<VdfNode>();

		public VdfNode(string name, string value) {
			Name = name;
			Value = value;
		}

		public IEnumerable<VdfNode> FindAll(string name) {
			return Children.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
		}

		public VdfNode Find(string name) {
			return FindAll(name).FirstOrDefault();
		}
	}

	public static class VdfParser {
		private enum TokenKind { String, OpenBrace, CloseBrace, End }

		private struct Token {
			public TokenKind Kind;
			public string Text;
		}

		public static VdfNode Parse(string text) {
			List<Token> tokens = Tokenize(text ?? "");
			int pos = 0;
			VdfNode root = new VdfNode("", null);
			ParseChildren(tokens, ref pos, root);
			return root;
		}

		private static void ParseChildren(List<Token> tokens, ref int pos, VdfNode parent) {
			while (pos < tokens.Count) {
				Token t = tokens[pos];
				if (t.Kind == TokenKind.CloseBrace || t.Kind == TokenKind.End) {
					return;
				}
				if (t.Kind != TokenKind.String) {
					pos++; // stray brace; skip rather than fail the whole lookup
					continue;
				}
				string key = t.Text;
				pos++;
				if (pos < tokens.Count && tokens[pos].Kind == TokenKind.OpenBrace) {
					VdfNode section = new VdfNode(key, null);
					pos++;
					ParseChildren(tokens, ref pos, section);
					if (pos < tokens.Count && tokens[pos].Kind == TokenKind.CloseBrace) {
						pos++;
					}
					parent.Children.Add(section);
				} else if (pos < tokens.Count && tokens[pos].Kind == TokenKind.String) {
					parent.Children.Add(new VdfNode(key, tokens[pos].Text));
					pos++;
				} else {
					parent.Children.Add(new VdfNode(key, ""));
				}
				// A [conditional] tag may follow the key/value or section; the
				// tokenizer already drops those, so nothing to do here.
			}
		}

		private static List<Token> Tokenize(string text) {
			List<Token> tokens = new List<Token>();
			int i = 0;
			while (i < text.Length) {
				char c = text[i];
				if (char.IsWhiteSpace(c)) {
					i++;
					continue;
				}
				if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') {
					while (i < text.Length && text[i] != '\n') i++;
					continue;
				}
				if (c == '{') {
					tokens.Add(new Token { Kind = TokenKind.OpenBrace });
					i++;
					continue;
				}
				if (c == '}') {
					tokens.Add(new Token { Kind = TokenKind.CloseBrace });
					i++;
					continue;
				}
				if (c == '[') {
					// Conditional tag like [$WIN32]; not meaningful here.
					while (i < text.Length && text[i] != ']') i++;
					if (i < text.Length) i++;
					continue;
				}
				if (c == '"') {
					i++;
					var sb = new System.Text.StringBuilder();
					while (i < text.Length && text[i] != '"') {
						if (text[i] == '\\' && i + 1 < text.Length && (text[i + 1] == '"' || text[i + 1] == '\\')) {
							sb.Append(text[i + 1]);
							i += 2;
						} else {
							sb.Append(text[i]);
							i++;
						}
					}
					if (i < text.Length) i++; // closing quote
					tokens.Add(new Token { Kind = TokenKind.String, Text = sb.ToString() });
					continue;
				}
				// Unquoted string: read to whitespace or a brace.
				int start = i;
				while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}') i++;
				tokens.Add(new Token { Kind = TokenKind.String, Text = text.Substring(start, i - start) });
			}
			tokens.Add(new Token { Kind = TokenKind.End });
			return tokens;
		}
	}

	public class SteamLibrary {
		public string Path { get; set; }
		// App ids listed for this library in libraryfolders.vdf. Empty when
		// the file doesn't say (older formats), which is not proof that an
		// app is absent.
		public HashSet<string> Apps { get; } = new HashSet<string>();
	}

	public static class SteamLibraries {
		public const string Civ3AppId = "3910";

		// Reads <steamRoot>/steamapps/libraryfolders.vdf. Returns an empty
		// list when the file is missing or unusable; a broken config file
		// must never break the Civ3 lookup.
		public static List<SteamLibrary> ReadLibraryFolders(string steamRoot) {
			List<SteamLibrary> result = new List<SteamLibrary>();
			try {
				string file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
				if (!File.Exists(file)) {
					return result;
				}
				VdfNode root = VdfParser.Parse(File.ReadAllText(file));
				VdfNode folders = root.Find("libraryfolders");
				if (folders == null) {
					return result;
				}
				foreach (VdfNode entry in folders.Children) {
					if (entry.Value != null) {
						// Pre-2021 format: plain "BaseInstallFolder_N" values
						// sitting directly under "LibraryFolders".
						if (entry.Name.StartsWith("BaseInstallFolder_", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Value)) {
							result.Add(new SteamLibrary { Path = entry.Value });
						}
						continue;
					}
					VdfNode pathNode = entry.Find("path");
					if (pathNode == null || string.IsNullOrEmpty(pathNode.Value)) {
						continue;
					}
					SteamLibrary lib = new SteamLibrary { Path = pathNode.Value };
					VdfNode apps = entry.Find("apps");
					if (apps != null) {
						foreach (VdfNode app in apps.Children) {
							lib.Apps.Add(app.Name);
						}
					}
					result.Add(lib);
				}
			} catch (Exception) {
				// Intentionally swallow: callers fall back to other locations.
				return new List<SteamLibrary>();
			}
			return result;
		}

		// Legacy fallback from the original issue: older Steam clients list
		// extra install folders as BaseInstallFolder_N entries in
		// <steamRoot>/config/config.vdf under
		// InstallConfigStore/Software/valve/Steam.
		public static List<string> ReadLegacyConfigFolders(string steamRoot) {
			List<string> result = new List<string>();
			try {
				string file = Path.Combine(steamRoot, "config", "config.vdf");
				if (!File.Exists(file)) {
					return result;
				}
				VdfNode node = VdfParser.Parse(File.ReadAllText(file));
				foreach (string step in new[] { "InstallConfigStore", "Software", "valve", "Steam" }) {
					node = node?.Find(step);
					if (node == null) {
						return result;
					}
				}
				foreach (VdfNode child in node.Children) {
					if (child.Value != null
						&& child.Name.StartsWith("BaseInstallFolder_", StringComparison.OrdinalIgnoreCase)
						&& !string.IsNullOrEmpty(child.Value)) {
						result.Add(child.Value);
					}
				}
			} catch (Exception) {
				return new List<string>();
			}
			return result;
		}
	}
}
