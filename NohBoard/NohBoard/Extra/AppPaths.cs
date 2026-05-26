/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Extra
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Centralised, typed filesystem locations for NohBoard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NohBoard supports three deployment shapes:
    /// </para>
    /// <list type="bullet">
    /// <item>Default ("installed"): user-writable data lives under
    ///   <c>%LOCALAPPDATA%\NohBoard\</c>; the bundled <c>keyboards/</c> directory next to the
    ///   exe is read-only.</item>
    /// <item>Portable (<c>--portable</c> CLI flag): user-writable data lives next to the exe
    ///   (legacy behavior).</item>
    /// <item>Development (running from <c>bin\Release\net10.0-windows\</c>): walks up from
    ///   <see cref="AppContext.BaseDirectory"/> looking for a sibling <c>keyboards/</c>
    ///   directory so <c>dotnet run</c> / F5 just works.</item>
    /// </list>
    /// </remarks>
    public static class AppPaths
    {
        private const string FolderName = "NohBoard";
        private const string KeyboardsFolderName = "keyboards";
        private const string LogsFolderName = "logs";
        private const string SettingsFileName = "NohBoard.json";

        /// <summary>
        /// When <c>true</c>, all user data (settings, logs, user-edited keyboards) is read
        /// from and written to <see cref="BaseDirectory"/> instead of <c>%LOCALAPPDATA%</c>.
        /// Set by <c>Program.Main</c> when the <c>--portable</c> CLI flag is present.
        /// </summary>
        public static bool PortableMode { get; internal set; }

        /// <summary>
        /// The directory containing the executable. Trim-/single-file-safe.
        /// </summary>
        public static string BaseDirectory =>
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        /// <summary>
        /// The directory that holds user-writable data.
        /// </summary>
        public static string UserDataDir
        {
            get
            {
                if (PortableMode) return BaseDirectory;

                // Honor the LOCALAPPDATA env var first so redirected profiles, sandboxes, and
                // unit-tests all see a consistent location. SpecialFolder.LocalApplicationData
                // bypasses env vars and would surprise those callers.
                var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                if (string.IsNullOrEmpty(localAppData))
                {
                    localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }
                if (string.IsNullOrEmpty(localAppData))
                {
                    // Extreme edge case (e.g. SYSTEM account): fall back to portable layout
                    // rather than crash.
                    return BaseDirectory;
                }

                return Path.Combine(localAppData, FolderName);
            }
        }

        /// <summary>
        /// The directory that holds user-edited keyboard definitions and styles.
        /// </summary>
        public static string UserKeyboardsDir => Path.Combine(UserDataDir, KeyboardsFolderName);

        /// <summary>
        /// The bundled (typically read-only) keyboard directory next to the executable.
        /// </summary>
        public static string BundledKeyboardsDir => Path.Combine(BaseDirectory, KeyboardsFolderName);

        /// <summary>
        /// The full path to the persisted settings file.
        /// </summary>
        public static string SettingsPath => Path.Combine(UserDataDir, SettingsFileName);

        /// <summary>
        /// The legacy settings location (next to the executable). Used only for migration.
        /// </summary>
        public static string LegacySettingsPath => Path.Combine(BaseDirectory, SettingsFileName);

        /// <summary>
        /// The directory where rolling log files are written.
        /// </summary>
        public static string LogsDir => Path.Combine(UserDataDir, LogsFolderName);

        /// <summary>
        /// All existing keyboard search roots in priority order: the user dir wins, then the
        /// bundled dir, then any <c>keyboards/</c> discovered by walking up from the base
        /// directory (developer convenience).
        /// </summary>
        public static IReadOnlyList<string> KeyboardsSearchPaths => ResolveSearchPaths();

        private static IReadOnlyList<string> ResolveSearchPaths()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(capacity: 3);

            void Add(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!Directory.Exists(path)) return;
                var full = Path.GetFullPath(path);
                if (seen.Add(full)) result.Add(full);
            }

            // 1. User-writable directory (per-user override).
            Add(UserKeyboardsDir);

            // 2. Bundled directory next to the exe (production install / portable mode).
            Add(BundledKeyboardsDir);

            // 3. Walk up looking for a sibling `keyboards/` (dev: exe under bin\Release\netX.Y\).
            var dir = new DirectoryInfo(BaseDirectory);
            for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                Add(Path.Combine(dir.FullName, KeyboardsFolderName));
            }

            return result;
        }

        /// <summary>
        /// Ensures the user data directory and its <c>logs/</c> subdirectory exist.
        /// </summary>
        public static void EnsureUserDataDirExists()
        {
            try
            {
                Directory.CreateDirectory(UserDataDir);
                Directory.CreateDirectory(LogsDir);
            }
            catch
            {
                // Swallowed by design: the logger and settings loader handle disk failures
                // independently. Failing here would prevent the app from starting.
            }
        }

        /// <summary>
        /// Ensures the user-writable keyboards directory exists. Returns the full path.
        /// </summary>
        public static string EnsureUserKeyboardsDir()
        {
            Directory.CreateDirectory(UserKeyboardsDir);
            return UserKeyboardsDir;
        }
    }
}
