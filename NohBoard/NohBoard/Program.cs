/*
Copyright (C) 2016 by Eric Bataille <e.c.p.bataille@gmail.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace ThoNohT.NohBoard
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Forms;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Logging;

    internal static class Program
    {
        /// <summary>
        /// Indicates whether NohBoard is running in portable mode (data + logs kept next to
        /// the exe instead of <c>%LOCALAPPDATA%\NohBoard\</c>). Mirrors
        /// <see cref="AppPaths.PortableMode"/> but is also accessible from places that already
        /// import <see cref="Program"/>.
        /// </summary>
        public static bool IsPortable => AppPaths.PortableMode;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Parse the CLI before any file I/O so AppPaths sees the correct mode.
            ParseCommandLine(args ?? Array.Empty<string>());

            // Make sure %LOCALAPPDATA%\NohBoard\logs\ exists so the very first Log.Info below
            // can actually persist.
            AppPaths.EnsureUserDataDirExists();

            Log.Info("--------------------------------------------------------------------------");
            Log.Info($"NohBoard {Version.Get} starting on {RuntimeInformation.OSDescription} ({RuntimeInformation.FrameworkDescription})");
            Log.Info($"PortableMode    = {AppPaths.PortableMode}");
            Log.Info($"BaseDirectory   = {AppPaths.BaseDirectory}");
            Log.Info($"UserDataDir     = {AppPaths.UserDataDir}");
            Log.Info($"SettingsPath    = {AppPaths.SettingsPath}");
            Log.Info($"LogsDir         = {AppPaths.LogsDir}");
            Log.Info($"KeyboardsRoots  = [{string.Join(", ", AppPaths.KeyboardsSearchPaths)}]");

            CrashHandler.Protect(() =>
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    Log.Error("WinForms ThreadException", e.Exception);
                    CrashHandler.HandleException(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Log.Error("AppDomain UnhandledException", ex);
                    CrashHandler.HandleException(ex);
                };

                // PerMonitorV2 keeps NohBoard sharp on HiDPI Windows 11 displays.
                // The <ApplicationHighDpiMode> MSBuild property in NohBoard.csproj also generates
                // an equivalent call into the source-generated Main wrapper, but calling it here
                // explicitly makes the intent visible and tolerates external entry points.
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            });

            Log.Info("NohBoard exiting normally.");
        }

        private static void ParseCommandLine(string[] args)
        {
            // Treat both --portable and -portable / /portable as the same flag.
            if (args.Any(a =>
                string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-portable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/portable", StringComparison.OrdinalIgnoreCase)))
            {
                AppPaths.PortableMode = true;
            }
        }
    }
}
