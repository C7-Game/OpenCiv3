using System.Collections.Generic;

namespace QueryCiv3.Steam;

/// <summary>
/// Minimal reader for Valve's KeyValues text format (the ".vdf" files
/// Steam keeps its library configuration in). This is deliberately not a
/// general VDF library: it only understands quoted/unquoted strings,
/// nested sections, // comments and [conditional] tags, which is all the
/// library and config files use. It exists so SteamInstallFinder can find a
/// Steam install of Civ3 without taking a new package dependency for two
/// small files.
/// </summary>
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
