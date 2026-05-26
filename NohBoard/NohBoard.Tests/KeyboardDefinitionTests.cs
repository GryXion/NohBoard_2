/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Keyboard;
    using Xunit;

    /// <summary>
    /// Loads every bundled keyboard definition through <see cref="FileHelper.Deserialize{T}"/>
    /// to assert that the JSON shipped with the repository still round-trips correctly under
    /// the updated runtime / serializer.
    /// </summary>
    public sealed class KeyboardDefinitionTests
    {
        public static IEnumerable<object[]> BundledKeyboards()
        {
            var repoRoot = FindRepoRoot();
            var keyboardsDir = Path.Combine(repoRoot, "keyboards");
            if (!Directory.Exists(keyboardsDir)) yield break;

            foreach (var file in Directory.EnumerateFiles(keyboardsDir, Constants.DefinitionFilename, SearchOption.AllDirectories))
            {
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(BundledKeyboards))]
        public void EveryBundledKeyboard_DeserializesCleanly(string path)
        {
            File.Exists(path).Should().BeTrue();
            var def = FileHelper.Deserialize<KeyboardDefinition>(path);

            def.Should().NotBeNull();
            def!.Width.Should().BeGreaterThan(0);
            def.Height.Should().BeGreaterThan(0);
            def.Version.Should().BeGreaterOrEqualTo(0);
            def.Elements.Should().NotBeNull();
            def.Elements.Select(e => e.Id).Distinct().Count()
                .Should().Be(def.Elements.Count, "element IDs must be unique within a keyboard");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
                dir = dir.Parent;
            return dir?.FullName ?? System.AppContext.BaseDirectory;
        }
    }
}
