using System;
using System.IO;
using C7Engine;
using Xunit;

namespace EngineTests {
	public class LogFilesTest {
		private static readonly DateTime SessionStart = new(2026, 11, 3, 12, 30, 45);

		[Fact]
		public void SessionLogFileName_UsesSortableTimestamp() {
			Assert.Equal("log-20261103-123045.txt", LogFiles.SessionLogFileName(SessionStart));

			// Names sort in the order the sessions started.
			string earlier = LogFiles.SessionLogFileName(SessionStart.AddHours(-2));
			string later = LogFiles.SessionLogFileName(SessionStart.AddMinutes(5));
			Assert.True(string.CompareOrdinal(earlier, LogFiles.SessionLogFileName(SessionStart)) < 0);
			Assert.True(string.CompareOrdinal(LogFiles.SessionLogFileName(SessionStart), later) < 0);
		}

		[Fact]
		public void SessionLogPath_ReturnsTimestampedPathInDirectory() {
			string dir = MakeTempDir();
			string path = LogFiles.SessionLogPath(dir, SessionStart);
			Assert.Equal(Path.Combine(dir, "log-20261103-123045.txt"), path);
			Assert.False(File.Exists(path));
		}

		[Fact]
		public void SessionLogPath_SuffixesWhenNameAlreadyTaken() {
			string dir = MakeTempDir();
			File.WriteAllText(Path.Combine(dir, "log-20261103-123045.txt"), "");
			Assert.Equal(Path.Combine(dir, "log-20261103-123045-2.txt"),
				LogFiles.SessionLogPath(dir, SessionStart));

			File.WriteAllText(Path.Combine(dir, "log-20261103-123045-2.txt"), "");
			Assert.Equal(Path.Combine(dir, "log-20261103-123045-3.txt"),
				LogFiles.SessionLogPath(dir, SessionStart));
		}

		[Fact]
		public void SessionKey_GroupsRolledChunksWithTheirSession() {
			Assert.Equal("log-20261103-123045", LogFiles.SessionKey("log-20261103-123045.txt"));
			Assert.Equal("log-20261103-123045", LogFiles.SessionKey("log-20261103-123045_001.txt"));
			Assert.Equal("log-20261103-123045", LogFiles.SessionKey("log-20261103-123045_002.txt"));
			Assert.Equal("log-20261103-123045-2", LogFiles.SessionKey("log-20261103-123045-2.txt"));

			// The plain file from before per-session logs is one session too.
			Assert.Equal("log", LogFiles.SessionKey("log.txt"));
			Assert.Equal("log", LogFiles.SessionKey("log_001.txt"));
		}

		[Fact]
		public void IsLogFile_OnlyMatchesLogFiles() {
			Assert.True(LogFiles.IsLogFile("log.txt"));
			Assert.True(LogFiles.IsLogFile("log_001.txt"));
			Assert.True(LogFiles.IsLogFile("log-20261103-123045.txt"));
			Assert.True(LogFiles.IsLogFile("log-20261103-123045_001.txt"));

			Assert.False(LogFiles.IsLogFile("C7.ini"));
			Assert.False(LogFiles.IsLogFile("logbook.txt"));
			Assert.False(LogFiles.IsLogFile("log-20261103-123045.sav"));
		}

		[Fact]
		public void PruneOldSessions_KeepsNewestSessionsCountingTheCurrentOne() {
			string dir = MakeTempDir();
			// 12 earlier sessions, oldest first.
			for (int i = 0; i < 12; i++) {
				WriteLog(dir, $"log-20261101-0000{i:00}.txt", SessionStart.AddDays(-2).AddMinutes(i));
			}
			string unrelated = Path.Combine(dir, "C7.ini");
			File.WriteAllText(unrelated, "");
			string notALog = Path.Combine(dir, "logbook.txt");
			File.WriteAllText(notALog, "");

			string current = Path.Combine(dir, LogFiles.SessionLogFileName(SessionStart));
			LogFiles.PruneOldSessions(dir, current, keepSessions: 10);

			// The current session takes one slot even though its file does
			// not exist yet, so the newest 9 earlier sessions stay.
			for (int i = 3; i < 12; i++) {
				Assert.True(File.Exists(Path.Combine(dir, $"log-20261101-0000{i:00}.txt")), $"session {i} should be kept");
			}
			for (int i = 0; i < 3; i++) {
				Assert.False(File.Exists(Path.Combine(dir, $"log-20261101-0000{i:00}.txt")), $"session {i} should be pruned");
			}
			Assert.True(File.Exists(unrelated));
			Assert.True(File.Exists(notALog));
		}

		[Fact]
		public void PruneOldSessions_DeletesEveryChunkOfAPrunedSession() {
			string dir = MakeTempDir();
			DateTime old = SessionStart.AddDays(-2);
			WriteLog(dir, "log-20261101-000000.txt", old);
			WriteLog(dir, "log-20261101-000000_001.txt", old.AddMinutes(1));
			WriteLog(dir, "log-20261101-000000_002.txt", old.AddMinutes(2));
			// A newer session, chunks included, recent enough to stay.
			WriteLog(dir, "log-20261102-000000.txt", SessionStart.AddDays(-1));
			WriteLog(dir, "log-20261102-000000_001.txt", SessionStart.AddDays(-1).AddMinutes(1));

			string current = Path.Combine(dir, LogFiles.SessionLogFileName(SessionStart));
			LogFiles.PruneOldSessions(dir, current, keepSessions: 2);

			Assert.False(File.Exists(Path.Combine(dir, "log-20261101-000000.txt")));
			Assert.False(File.Exists(Path.Combine(dir, "log-20261101-000000_001.txt")));
			Assert.False(File.Exists(Path.Combine(dir, "log-20261101-000000_002.txt")));
			Assert.True(File.Exists(Path.Combine(dir, "log-20261102-000000.txt")));
			Assert.True(File.Exists(Path.Combine(dir, "log-20261102-000000_001.txt")));
		}

		[Fact]
		public void PruneOldSessions_NeverDeletesTheCurrentSession() {
			string dir = MakeTempDir();
			// The current session's file exists but has the oldest timestamp
			// of the lot (clock skew, a copied file, ...).
			string current = WriteLog(dir, "log-20261103-123045.txt", SessionStart.AddDays(-30));
			for (int i = 0; i < 10; i++) {
				WriteLog(dir, $"log-20261102-0000{i:00}.txt", SessionStart.AddDays(-1).AddMinutes(i));
			}

			LogFiles.PruneOldSessions(dir, current, keepSessions: 10);

			Assert.True(File.Exists(current));
			// 9 of the other sessions stay, the oldest one goes.
			Assert.False(File.Exists(Path.Combine(dir, "log-20261102-000000.txt")));
			Assert.True(File.Exists(Path.Combine(dir, "log-20261102-000001.txt")));
		}

		[Fact]
		public void PruneOldSessions_TreatsLegacyPlainLogAsOldestSession() {
			string dir = MakeTempDir();
			DateTime old = SessionStart.AddDays(-10);
			WriteLog(dir, "log.txt", old);
			WriteLog(dir, "log_001.txt", old.AddMinutes(1));
			WriteLog(dir, "log-20261102-000000.txt", SessionStart.AddDays(-1));

			string current = Path.Combine(dir, LogFiles.SessionLogFileName(SessionStart));
			LogFiles.PruneOldSessions(dir, current, keepSessions: 2);

			Assert.False(File.Exists(Path.Combine(dir, "log.txt")));
			Assert.False(File.Exists(Path.Combine(dir, "log_001.txt")));
			Assert.True(File.Exists(Path.Combine(dir, "log-20261102-000000.txt")));
		}

		[Fact]
		public void PruneOldSessions_MissingDirectoryIsANoOp() {
			string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			LogFiles.PruneOldSessions(dir, Path.Combine(dir, "log-20261103-123045.txt"));
			LogFiles.PruneOldSessions(null, null);
		}

		private static string MakeTempDir() {
			return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
		}

		private static string WriteLog(string dir, string fileName, DateTime lastWriteUtc) {
			string path = Path.Combine(dir, fileName);
			File.WriteAllText(path, "log line");
			File.SetLastWriteTimeUtc(path, lastWriteUtc);
			return path;
		}
	}
}
