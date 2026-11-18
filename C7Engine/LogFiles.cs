using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace C7Engine;

/// <summary>
/// File naming and retention for the per-session log files written by the
/// Godot layer (see LogManager in the C7 project).
///
/// Each run of the game writes to its own file, named after when the session
/// started, e.g. log-20261103-123045.txt. A session that outgrows the file
/// size limit rolls to log-20261103-123045_001.txt, _002, and so on; those
/// rolled chunks still belong to the same session here.
///
/// This lives in C7Engine rather than next to LogManager so the naming and
/// pruning rules can be unit tested: the C7 project uses the Godot SDK and
/// is not referenced by the test project. Nothing here depends on Godot or
/// on Serilog, it only decides which file the log sink should write to and
/// which old files should be removed.
/// </summary>
public static class LogFiles {
	/// <summary>How many past sessions to keep, including the current one.</summary>
	public const int SessionsToKeep = 10;

	/// <summary>
	/// How many files one session may roll through, including its first file.
	/// Passed to the sink as retainedFileCountLimit, so a single very long
	/// session drops its oldest chunk instead of either stopping at the size
	/// limit or growing without bound.
	/// </summary>
	public const int FilesPerSessionToKeep = 10;

	private static readonly Regex RolledSuffix = new(@"_\d+$", RegexOptions.Compiled);

	public static string SessionLogFileName(DateTime sessionStart) {
		return "log-" + sessionStart.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt";
	}

	/// <summary>
	/// The path for a new session's log file. The file itself is not created,
	/// the log sink does that. If the name is already taken (two sessions
	/// starting in the same second) a -2, -3, ... suffix is added so the new
	/// session never shares or overwrites the earlier session's file.
	/// </summary>
	public static string SessionLogPath(string directory, DateTime sessionStart) {
		string path = Path.Combine(directory, SessionLogFileName(sessionStart));
		if (!File.Exists(path)) {
			return path;
		}

		string stem = path.Substring(0, path.Length - ".txt".Length);
		for (int attempt = 2; ; attempt++) {
			string candidate = stem + "-" + attempt.ToString(CultureInfo.InvariantCulture) + ".txt";
			if (!File.Exists(candidate)) {
				return candidate;
			}
		}
	}

	/// <summary>
	/// Which session a log file belongs to. Rolled chunks lose their _NNN
	/// suffix, so log-20261103-123045_001.txt groups with
	/// log-20261103-123045.txt. The plain log.txt written before per-session
	/// logs existed, and its log_001.txt style chunks, group under "log" and
	/// count as the oldest session.
	/// </summary>
	public static string SessionKey(string fileName) {
		string name = Path.GetFileNameWithoutExtension(fileName);
		return RolledSuffix.Replace(name, "");
	}

	/// <summary>True for files that look like one of our log files.</summary>
	public static bool IsLogFile(string fileName) {
		string name = Path.GetFileName(fileName);
		return name.StartsWith("log", StringComparison.Ordinal)
			&& name.EndsWith(".txt", StringComparison.Ordinal)
			&& (name == "log.txt"
				|| name.StartsWith("log-", StringComparison.Ordinal)
				|| RolledSuffix.IsMatch(Path.GetFileNameWithoutExtension(name)));
	}

	/// <summary>
	/// Delete the files of all but the newest sessions in a directory.
	/// Sessions are ordered by the last write time of their newest file, so
	/// rolled chunks keep their session alive as long as it was active.
	/// The current session counts towards keepSessions even if its file has
	/// not been created yet (this runs at startup, before the new session
	/// writes anything), and its files are never deleted, even if they sort
	/// oldest.
	///
	/// Never throws: pruning is housekeeping, and a locked or disappearing
	/// file must not keep the game from starting.
	/// </summary>
	public static void PruneOldSessions(string directory, string currentLogPath, int keepSessions = SessionsToKeep) {
		try {
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
				return;
			}

			string currentKey = currentLogPath == null ? null : SessionKey(currentLogPath);

			var sessions = Directory.EnumerateFiles(directory)
				.Where(path => IsLogFile(path))
				.GroupBy(path => SessionKey(path))
				.Select(group => new {
					Key = group.Key,
					Files = group.ToList(),
					NewestWrite = group.Max(path => SafeLastWriteTimeUtc(path)),
				})
				.OrderByDescending(session => session.NewestWrite)
				.ThenByDescending(session => session.Key, StringComparer.Ordinal)
				.ToList();

			// The current session always takes one of the kept slots, so
			// other sessions get one fewer than keepSessions between them.
			int keptOthers = 0;
			int otherSessionsToKeep = Math.Max(keepSessions - 1, 0);
			foreach (var session in sessions) {
				if (session.Key == currentKey) {
					continue;
				}
				if (keptOthers < otherSessionsToKeep) {
					keptOthers++;
					continue;
				}
				foreach (string file in session.Files) {
					try {
						File.Delete(file);
					} catch (Exception) {
						// A file we cannot delete now will be retried on the
						// next startup. It is not worth failing over.
					}
				}
			}
		} catch (Exception) {
			// Same reasoning as above: never let log housekeeping break startup.
		}
	}

	private static DateTime SafeLastWriteTimeUtc(string path) {
		try {
			return File.GetLastWriteTimeUtc(path);
		} catch (Exception) {
			return DateTime.MinValue;
		}
	}
}
