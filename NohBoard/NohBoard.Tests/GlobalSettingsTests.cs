/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using Xunit;

    public sealed class GlobalSettingsTests
    {
        [Fact]
        public void Serialize_Roundtrip_PreservesValues()
        {
            var settings = new GlobalSettings
            {
                WindowTitle = "TestTitle",
                MouseSensitivity = 75,
                ScrollHold = 100,
                MouseFromCenter = true,
                PressHold = 250,
                UpdateInterval = 50,
                TrapKeyboard = true,
                TrapMouse = false,
                TrapToggleKeyCode = 0x91,
                LoadedCategory = "Normal",
                LoadedKeyboard = "uk",
                LoadedStyle = "default",
                LoadedGlobalStyle = false,
                X = 100,
                Y = 200,
            };

            var json = FileHelper.Serialize(settings);
            var roundTripped = DeserializeFromString<GlobalSettings>(json);

            roundTripped.WindowTitle.Should().Be("TestTitle");
            roundTripped.MouseSensitivity.Should().Be(75);
            roundTripped.ScrollHold.Should().Be(100);
            roundTripped.MouseFromCenter.Should().BeTrue();
            roundTripped.PressHold.Should().Be(250);
            roundTripped.UpdateInterval.Should().Be(50);
            roundTripped.TrapKeyboard.Should().BeTrue();
            roundTripped.TrapMouse.Should().BeFalse();
            roundTripped.TrapToggleKeyCode.Should().Be(0x91);
            roundTripped.LoadedCategory.Should().Be("Normal");
            roundTripped.LoadedKeyboard.Should().Be("uk");
            roundTripped.LoadedStyle.Should().Be("default");
            roundTripped.LoadedGlobalStyle.Should().BeFalse();
            roundTripped.X.Should().Be(100);
            roundTripped.Y.Should().Be(200);
        }

        [Fact]
        public void UpdateInterval_ClampsTooSmallValues()
        {
            var settings = new GlobalSettings { UpdateInterval = 1 };
            settings.UpdateInterval.Should().Be(5, "the minimum allowed update interval is 5 ms (200 fps)");
        }

        [Fact]
        public void UpdateInterval_ClampsTooLargeValues()
        {
            var settings = new GlobalSettings { UpdateInterval = 1_000_000 };
            settings.UpdateInterval.Should().Be(60_000, "the maximum allowed update interval is 60 s");
        }

        [Fact]
        public void UpdateInterval_DefaultIs33()
        {
            new GlobalSettings().UpdateInterval.Should().Be(33);
        }

        [Fact]
        public void DefaultMouseSensitivity_Is50()
        {
            new GlobalSettings().MouseSensitivity.Should().Be(50);
        }

        private static T DeserializeFromString<T>(string json) where T : class
        {
            var path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(path, json);
                return FileHelper.Deserialize<T>(path);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }
    }
}
