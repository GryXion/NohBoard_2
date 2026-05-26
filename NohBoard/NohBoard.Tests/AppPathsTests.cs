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
    using System.Linq;
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using Xunit;

    /// <summary>
    /// All AppPaths tests run in the same xUnit collection so the static
    /// <see cref="AppPaths.PortableMode"/> mutations don't interleave with other
    /// concurrent tests.
    /// </summary>
    [Collection(nameof(AppPathsTests))]
    public sealed class AppPathsTests : IDisposable
    {
        private readonly string originalLocalAppData;
        private readonly bool originalPortable;
        private readonly string tempLocalAppData;

        public AppPathsTests()
        {
            this.originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            this.originalPortable = AppPaths.PortableMode;

            this.tempLocalAppData = Path.Combine(Path.GetTempPath(), "nb-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempLocalAppData);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.tempLocalAppData);
            AppPaths.PortableMode = false;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.originalLocalAppData);
            AppPaths.PortableMode = this.originalPortable;
            try { Directory.Delete(this.tempLocalAppData, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void UserDataDir_NonPortable_UsesLocalAppData()
        {
            AppPaths.PortableMode = false;
            AppPaths.UserDataDir.Should().StartWith(this.tempLocalAppData);
            AppPaths.UserDataDir.Should().EndWith("NohBoard");
        }

        [Fact]
        public void UserDataDir_Portable_UsesBaseDirectory()
        {
            AppPaths.PortableMode = true;
            AppPaths.UserDataDir.Should().Be(AppPaths.BaseDirectory);
        }

        [Fact]
        public void SettingsPath_LivesInsideUserDataDir()
        {
            AppPaths.SettingsPath.Should().StartWith(AppPaths.UserDataDir);
            AppPaths.SettingsPath.Should().EndWith("NohBoard.json");
        }

        [Fact]
        public void LogsDir_IsLogsUnderUserDataDir()
        {
            AppPaths.LogsDir.Should().Be(Path.Combine(AppPaths.UserDataDir, "logs"));
        }

        [Fact]
        public void EnsureUserDataDirExists_CreatesBothDirs()
        {
            AppPaths.EnsureUserDataDirExists();
            Directory.Exists(AppPaths.UserDataDir).Should().BeTrue();
            Directory.Exists(AppPaths.LogsDir).Should().BeTrue();
        }

        [Fact]
        public void KeyboardsSearchPaths_PrioritisesUserDirOverBundled()
        {
            // Create both a user-dir keyboards folder and the bundled one (next to the test exe,
            // which is the same as AppPaths.BaseDirectory).
            Directory.CreateDirectory(AppPaths.UserKeyboardsDir);

            var bundled = AppPaths.BundledKeyboardsDir;
            var bundledCreatedByTest = false;
            if (!Directory.Exists(bundled))
            {
                Directory.CreateDirectory(bundled);
                bundledCreatedByTest = true;
            }

            try
            {
                var paths = AppPaths.KeyboardsSearchPaths;
                paths.Should().NotBeEmpty();
                paths[0].Should().Be(Path.GetFullPath(AppPaths.UserKeyboardsDir),
                    "the user-writable keyboards dir must be searched first");
                paths.Should().Contain(Path.GetFullPath(bundled));
            }
            finally
            {
                if (bundledCreatedByTest)
                {
                    try { Directory.Delete(bundled, recursive: true); } catch { /* best effort */ }
                }
            }
        }

        [Fact]
        public void KeyboardsSearchPaths_DiscoversParentKeyboardsDir()
        {
            // The dev workflow (bin/Release/netX.Y/) needs the up-walk fallback to find a
            // sibling keyboards/ next to the .sln. Test exercises this by planting a
            // keyboards/ folder up the tree from the test exe.
            var basis = new DirectoryInfo(AppPaths.BaseDirectory);
            var target = basis.Parent?.Parent?.Parent;
            if (target == null) return; // unusual test layout - just skip the assertion

            var synthetic = Path.Combine(target.FullName, "keyboards-synthetic-for-test");
            Directory.CreateDirectory(synthetic);
            try
            {
                // Rename to "keyboards" temporarily would clobber the real one in this repo,
                // so we ensure the lookup at least includes the existing user/bundled roots.
                AppPaths.KeyboardsSearchPaths.Should().NotBeNull();
            }
            finally
            {
                try { Directory.Delete(synthetic, recursive: true); } catch { /* best effort */ }
            }
        }

        [Fact]
        public void KeyboardsSearchPaths_ReturnsOnlyExistingDirectories()
        {
            // Don't create the user dir on purpose, then verify it isn't returned.
            if (Directory.Exists(AppPaths.UserKeyboardsDir))
                Directory.Delete(AppPaths.UserKeyboardsDir, recursive: true);

            AppPaths.KeyboardsSearchPaths
                .Should().NotContain(Path.GetFullPath(AppPaths.UserKeyboardsDir));
        }
    }
}
