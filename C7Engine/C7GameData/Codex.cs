using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace C7GameData {
	public class CodexEntry {
		public string Key { get; set; }
		public string DisplayName { get; set; }
		public string Body { get; set; }
		public string Description { get; set; }
	}

	public class Codex {
		private static readonly Encoding Windows1252;
		private static readonly Regex LinkRegex = new(@"\$LINK<([^=<>]+)=([^>]+)>");

		private static readonly ILogger log = Log.ForContext<Codex>();

		public Dictionary<string, CodexEntry> Entries { get; } = new();
		public List<string> GameConceptKeys { get; } = new();

		static Codex() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Windows1252 = Encoding.GetEncoding(1252);
		}

		public Codex() { }

		public Codex(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				log.Warning($"Civilopedia text file '{path}' not found; Codex will show only game-generated content");
				return;
			}

			string content = Windows1252.GetString(File.ReadAllBytes(path));
			ParseInto(content);
		}

		public static Codex Parse(string content) {
			Codex codex = new();
			codex.ParseInto(content);
			return codex;
		}

		public CodexEntry GetEntry(string key) {
			if (key is null) {
				return null;
			}
			Entries.TryGetValue(key.Trim(), out CodexEntry entry);
			return entry;
		}

		private void ParseInto(string content) {
			if (content is null) {
				return;
			}

			CodexEntry currentEntry = null;
			List<string> currentParagraphLines = null;
			bool inGameConceptKeys = false;
			bool inDescription = false;

			foreach (string rawLine in content.Split('\n')) {
				string line = rawLine.TrimEnd('\r');

				if (line.StartsWith(';')) {
					continue;
				}

				if (line.StartsWith('#')) {
					string header = line.Substring(1).Trim();
					if (header == "GAME_CONCEPTS_KEYS") {
						FlushParagraph(currentEntry, inDescription, ref currentParagraphLines);
						inGameConceptKeys = true;
						currentEntry = null;
						currentParagraphLines = null;
						continue;
					}
					inGameConceptKeys = false;

					FlushParagraph(currentEntry, inDescription, ref currentParagraphLines);

					inDescription = header.StartsWith("DESC_");
					string key = inDescription ? header.Substring(5).Trim() : header;
					currentEntry = GetOrCreateEntry(key);
					currentParagraphLines = null;
					continue;
				}

				if (inGameConceptKeys) {
					if (line.Trim().Length > 0) {
						GameConceptKeys.Add(line.Trim());
					}
					continue;
				}

				if (currentEntry is null) {
					continue;
				}

				if (line.StartsWith('^')) {
					string text = line.Substring(1);
					if (text.Length == 0) {
						FlushParagraph(currentEntry, inDescription, ref currentParagraphLines);
					} else {
						FlushParagraph(currentEntry, inDescription, ref currentParagraphLines);
						currentParagraphLines = new List<string> { text };
					}
					continue;
				}

				if (line.Trim().Length == 0) {
					continue;
				}

				if (!inDescription && currentParagraphLines is null && currentEntry.Body is null && string.IsNullOrEmpty(currentEntry.DisplayName)) {
					currentEntry.DisplayName = line.Trim();
					continue;
				}

				currentParagraphLines ??= new List<string>();
				currentParagraphLines.Add(line);
			}

			FlushParagraph(currentEntry, inDescription, ref currentParagraphLines);

			foreach (string key in Entries.Keys.ToList()) {
				CodexEntry entry = Entries[key];
				if (string.IsNullOrEmpty(entry.DisplayName) && string.IsNullOrEmpty(entry.Body) && string.IsNullOrEmpty(entry.Description)) {
					Entries.Remove(key);
				}
			}
		}

		private static void FlushParagraph(CodexEntry entry, bool inDescription, ref List<string> paragraphLines) {
			if (entry is null || paragraphLines is null || paragraphLines.Count == 0) {
				paragraphLines = null;
				return;
			}

			string joined = LinkRegex.Replace(string.Join(' ', paragraphLines), match =>
				$"[{match.Groups[1].Value}](key:{match.Groups[2].Value.Trim()})");

			// Civ3 uses a whole-line {Title} as a subheading within an entry.
			// Column headers also use braces, but they contain tabs, so keep
			// those as body text.
			if (joined.StartsWith('{') && joined.EndsWith('}') && joined.Length > 2 && !joined.Contains('\t')) {
				joined = "## " + joined.Substring(1, joined.Length - 2).Trim();
			}

			string target = inDescription ? entry.Description : entry.Body;
			entry.Description = inDescription ? target is null ? joined : target + "\n\n" + joined : entry.Description;
			entry.Body = inDescription ? entry.Body : target is null ? joined : target + "\n\n" + joined;
			paragraphLines = null;
		}

		private CodexEntry GetOrCreateEntry(string key) {
			string trimmed = key.Trim();
			if (!Entries.TryGetValue(trimmed, out CodexEntry entry)) {
				entry = new CodexEntry { Key = trimmed };
				Entries.Add(trimmed, entry);
			}
			return entry;
		}
	}
}
