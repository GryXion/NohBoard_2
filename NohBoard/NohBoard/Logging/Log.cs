/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ThoNohT.NohBoard.Extra;

    /// <summary>
    /// Severity levels used by <see cref="Log"/>.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Diagnostic information about normal flow.</summary>
        Info,

        /// <summary>Recoverable problem - the app continues, but something is off.</summary>
        Warn,

        /// <summary>Unexpected failure that the user is likely to notice.</summary>
        Error,
    }

    /// <summary>
    /// Tiny dependency-free logger. Writes a rolling daily file under
    /// <see cref="AppPaths.LogsDir"/> and forwards every line to
    /// <see cref="Debug.WriteLine(string)"/> so VS Output / DebugView pick it up too.
    /// </summary>
    /// <remarks>
    /// Goals:
    /// <list type="bullet">
    /// <item>Never throw from a call site. Disk failures degrade silently to <c>Debug</c>-only.</item>
    /// <item>Be thread-safe with a single lock around the file write.</item>
    /// <item>Be free - no NuGet, no reflection, no allocations on the hot path beyond
    /// the message itself.</item>
    /// </list>
    /// </remarks>
    public static class Log
    {
        private static readonly object writeLock = new object();
        private static bool initialized;
        private static string currentFilePath;
        private static DateTime currentFileDate;

        /// <summary>
        /// Maximum number of daily log files to keep on disk before older ones are pruned.
        /// </summary>
        public const int MaxRetainedFiles = 7;

        /// <summary>
        /// Returns the path of the file the next write would go into. Useful for
        /// diagnostics dialogs and tests. Always reflects the currently configured
        /// <see cref="AppPaths.LogsDir"/>, even if it changed since the last write.
        /// </summary>
        public static string CurrentLogFile
        {
            get
            {
                lock (writeLock) return BuildPathForToday();
            }
        }

        /// <summary>
        /// Convenience wrapper for <see cref="LogLevel.Info"/>.
        /// </summary>
        public static void Info(string message) => Write(LogLevel.Info, message, exception: null);

        /// <summary>
        /// Convenience wrapper for <see cref="LogLevel.Warn"/>.
        /// </summary>
        public static void Warn(string message, Exception exception = null) => Write(LogLevel.Warn, message, exception);

        /// <summary>
        /// Convenience wrapper for <see cref="LogLevel.Error"/>.
        /// </summary>
        public static void Error(string message, Exception exception = null) => Write(LogLevel.Error, message, exception);

        /// <summary>
        /// Returns the last <paramref name="lineCount"/> lines from today's log file (or
        /// fewer if the file is shorter). Returns an empty list on any I/O error.
        /// </summary>
        public static IReadOnlyList<string> TailToday(int lineCount = 200)
        {
            try
            {
                var path = CurrentLogFile;
                if (!File.Exists(path)) return Array.Empty<string>();
                var all = File.ReadAllLines(path);
                if (all.Length <= lineCount) return all;
                return all.Skip(all.Length - lineCount).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Writes a structured line to disk and the debug stream.
        /// </summary>
        public static void Write(LogLevel level, string message, Exception exception = null)
        {
            var line = Format(level, message, exception);

            // Always emit to Debug so devs see it even if the file write fails.
            Debug.WriteLine(line);

            lock (writeLock)
            {
                try
                {
                    EnsureFileForTodayLocked();
                    File.AppendAllText(currentFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Intentional: logging must never break the app.
                }
            }
        }

        private static string Format(LogLevel level, string message, Exception exception)
        {
            var levelTag = level switch
            {
                LogLevel.Info => "INFO ",
                LogLevel.Warn => "WARN ",
                LogLevel.Error => "ERROR",
                _ => "?    ",
            };

            var prefix = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelTag}] ";
            if (exception is null) return prefix + message;

            var sb = new StringBuilder(prefix.Length + message.Length + 256);
            sb.Append(prefix).AppendLine(message);
            sb.Append("    ").AppendLine(exception.GetType().FullName + ": " + exception.Message);
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                foreach (var stackLine in exception.StackTrace.Split('\n'))
                {
                    sb.Append("    ").AppendLine(stackLine.TrimEnd('\r'));
                }
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }

        private static void EnsureFileForTodayLocked()
        {
            var today = DateTime.Now.Date;
            var expectedPath = BuildPathForToday();

            // Re-resolve if either the day has rolled over, or the user data dir relocated
            // (env var changes, portable mode toggled, test fixtures, ...). This costs one
            // string comparison per Write call but keeps the logger correct under reconfig.
            if (initialized
                && currentFileDate == today
                && currentFilePath != null
                && string.Equals(currentFilePath, expectedPath, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(Path.GetDirectoryName(currentFilePath)))
            {
                return;
            }

            var dir = AppPaths.LogsDir;
            Directory.CreateDirectory(dir);

            currentFileDate = today;
            currentFilePath = expectedPath;
            initialized = true;

            PruneOldFilesLocked(dir);
        }

        private static string BuildPathForToday() =>
            Path.Combine(AppPaths.LogsDir, $"nohboard-{DateTime.Now:yyyyMMdd}.log");

        private static void PruneOldFilesLocked(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir, "nohboard-*.log")
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();

                foreach (var stale in files.Skip(MaxRetainedFiles))
                {
                    try { stale.Delete(); } catch { /* skip */ }
                }
            }
            catch
            {
                // Pruning is best-effort.
            }
        }
    }
}
