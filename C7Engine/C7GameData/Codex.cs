using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace C7GameData {
	public class CodexPage {
		public string Title { get; set; }
		public string Body { get; set; }
	}

	public class CodexEntry {
		public string Key { get; set; }
		public string DisplayName { get; set; }
		public List<CodexPage> Pages { get; set; } = new();
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
			CodexPage currentPage = null;
			List<string> currentParagraphLines = null;
			bool inGameConceptKeys = false;

			foreach (string rawLine in content.Split('\n')) {
				string line = rawLine.TrimEnd('\r');

				if (line.StartsWith(';')) {
					continue;
				}

				if (line.StartsWith('#')) {
					string header = line.Substring(1).Trim();
					if (header == "GAME_CONCEPTS_KEYS") {
						FlushParagraph(currentPage, ref currentParagraphLines);
						inGameConceptKeys = true;
						currentPage = null;
						currentParagraphLines = null;
						continue;
					}
					inGameConceptKeys = false;

					FlushParagraph(currentPage, ref currentParagraphLines);

					string key = header.StartsWith("DESC_") ? header.Substring(5).Trim() : header;
					currentEntry = GetOrCreateEntry(key);
					currentPage = new CodexPage();
					currentEntry.Pages.Add(currentPage);
					currentParagraphLines = null;
					continue;
				}

				if (inGameConceptKeys) {
					if (line.Trim().Length > 0) {
						GameConceptKeys.Add(line.Trim());
					}
					continue;
				}

				if (currentPage is null) {
					continue;
				}

				if (line.StartsWith('^')) {
					string text = line.Substring(1);
					if (text.Length == 0) {
						FlushParagraph(currentPage, ref currentParagraphLines);
					} else {
						FlushParagraph(currentPage, ref currentParagraphLines);
						currentParagraphLines = new List<string> { text };
					}
					continue;
				}

				if (line.Trim().Length == 0) {
					continue;
				}

				if (currentParagraphLines is null && currentPage.Body is null && string.IsNullOrEmpty(currentEntry.DisplayName)) {
					currentEntry.DisplayName = line.Trim();
					continue;
				}

				currentParagraphLines ??= new List<string>();
				currentParagraphLines.Add(line);
			}

			FlushParagraph(currentPage, ref currentParagraphLines);

			foreach (string key in Entries.Keys.ToList()) {
				CodexEntry entry = Entries[key];
				if (string.IsNullOrEmpty(entry.DisplayName) && entry.Pages.All(page => page.Title is null && string.IsNullOrEmpty(page.Body))) {
					Entries.Remove(key);
				}
			}
		}

		private static void FlushParagraph(CodexPage page, ref List<string> paragraphLines) {
			if (page is null || paragraphLines is null || paragraphLines.Count == 0) {
				paragraphLines = null;
				return;
			}

			string joined = LinkRegex.Replace(string.Join(' ', paragraphLines), match =>
				$"[{match.Groups[1].Value}](key:{match.Groups[2].Value.Trim()})");

			if (page.Title is null && joined.StartsWith('{') && joined.EndsWith('}') && joined.Length > 2) {
				page.Title = joined.Substring(1, joined.Length - 2).Trim();
			} else {
				page.Body = page.Body is null ? joined : page.Body + "\n\n" + joined;
			}

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
