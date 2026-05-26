/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using System;
    using System.IO;
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using Xunit;

    /// <summary>
    /// Verifies <see cref="GlobalSettings.Load"/> migrates a pre-existing settings file from
    /// <c>{ExeDir}/NohBoard.json</c> to <c>%LOCALAPPDATA%/NohBoard/NohBoard.json</c> on first
    /// run, never losing the original until the copy is successful.
    /// </summary>
    [Collection(nameof(AppPathsTests))]
    public sealed class GlobalSettingsMigrationTests : IDisposable
    {
        private readonly string originalLocalAppData;
        private readonly bool originalPortable;
        private readonly string tempLocalAppData;

        public GlobalSettingsMigrationTests()
        {
            this.originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            this.originalPortable = AppPaths.PortableMode;

            this.tempLocalAppData = Path.Combine(Path.GetTempPath(), "nb-tests-mig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempLocalAppData);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.tempLocalAppData);
            AppPaths.PortableMode = false;
        }

        public void Dispose()
        {
            // Clean up any settings file we left next to the test exe between runs.
            try { File.Delete(AppPaths.LegacySettingsPath); } catch { /* ignore */ }
            try { File.Delete(AppPaths.SettingsPath); } catch { /* ignore */ }

            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.originalLocalAppData);
            AppPaths.PortableMode = this.originalPortable;
            try { Directory.Delete(this.tempLocalAppData, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void Load_MovesLegacyFileToUserDataDir()
        {
            var legacy = AppPaths.LegacySettingsPath;
            var target = AppPaths.SettingsPath;

            // Don't run the test when legacy and target are the same path (which would happen
            // if Path.Combine(BaseDirectory, "NohBoard.json") == SettingsPath, e.g. portable).
            if (string.Equals(legacy, target, StringComparison.OrdinalIgnoreCase)) return;

            // Seed a settings file with a unique window title in the legacy location.
            var seed = new GlobalSettings { WindowTitle = "MigrationProbe-" + Guid.NewGuid().ToString("N") };
            FileHelper.Serialize(legacy, seed);

            try
            {
                File.Exists(legacy).Should().BeTrue();
                File.Exists(target).Should().BeFalse();

                GlobalSettings.Load().Should().BeTrue();

                File.Exists(target).Should().BeTrue("the legacy file must be moved to the new location");
                File.Exists(legacy).Should().BeFalse("the legacy file must be removed once the copy succeeds");
                GlobalSettings.Settings.WindowTitle.Should().Be(seed.WindowTitle);
            }
            finally
            {
                try { File.Delete(legacy); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void Load_NoLegacyFile_NoMigrationNeeded()
        {
            try { File.Delete(AppPaths.LegacySettingsPath); } catch { /* ignore */ }
            try { File.Delete(AppPaths.SettingsPath); } catch { /* ignore */ }

            GlobalSettings.Load().Should().BeTrue();
            GlobalSettings.Settings.Should().NotBeNull();
            GlobalSettings.Settings.AutoLoadDefaultOnFirstRun.Should().BeTrue();
            GlobalSettings.Settings.CheckForUpdates.Should().BeTrue();
        }

        [Fact]
        public void Load_LegacyAndNewBothPresent_PreservesNewFile()
        {
            var legacy = AppPaths.LegacySettingsPath;
            var target = AppPaths.SettingsPath;
            if (string.Equals(legacy, target, StringComparison.OrdinalIgnoreCase)) return;

            var legacySeed = new GlobalSettings { WindowTitle = "legacy" };
            var newSeed = new GlobalSettings { WindowTitle = "current" };
            AppPaths.EnsureUserDataDirExists();
            FileHelper.Serialize(legacy, legacySeed);
            FileHelper.Serialize(target, newSeed);

            try
            {
                GlobalSettings.Load().Should().BeTrue();
                GlobalSettings.Settings.WindowTitle.Should().Be("current");
                File.Exists(legacy).Should().BeTrue("legacy file must be left in place when the new file already exists");
            }
            finally
            {
                try { File.Delete(legacy); } catch { /* ignore */ }
                try { File.Delete(target); } catch { /* ignore */ }
            }
        }
    }
}
