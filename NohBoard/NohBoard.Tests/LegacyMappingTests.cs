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
    using FluentAssertions;
    using ThoNohT.NohBoard.Hooking;
    using ThoNohT.NohBoard.Legacy;
    using Xunit;

    public sealed class LegacyMappingTests
    {
        [Theory]
        [InlineData(1026, KeyType.Mouse, (int)MouseKeyCode.LeftButton)]
        [InlineData(1027, KeyType.Mouse, (int)MouseKeyCode.RightButton)]
        [InlineData(1028, KeyType.MouseMovement, 0)]
        [InlineData(1029, KeyType.MouseScroll, (int)MouseScrollKeyCode.ScrollUp)]
        [InlineData(1030, KeyType.MouseScroll, (int)MouseScrollKeyCode.ScrollDown)]
        [InlineData(1031, KeyType.MouseScroll, (int)MouseScrollKeyCode.ScrollRight)]
        [InlineData(1032, KeyType.MouseScroll, (int)MouseScrollKeyCode.ScrollLeft)]
        [InlineData(1033, KeyType.Mouse, (int)MouseKeyCode.MiddleButton)]
        [InlineData(1034, KeyType.Mouse, (int)MouseKeyCode.X1Button)]
        [InlineData(1035, KeyType.Mouse, (int)MouseKeyCode.X2Button)]
        public void LegacyKeyCodeMapping_MapsKnownLegacyCodes(int legacyCode, KeyType expectedType, int expectedCode)
        {
            var (type, code) = LegacyKeyCodeMapping.Map(legacyCode);
            type.Should().Be(expectedType);
            code.Should().Be(expectedCode);
        }

        [Fact]
        public void LegacyKeyCodeMapping_KeyboardCodesPassThroughUnchanged()
        {
            for (var c = 0; c <= 1025; c++)
            {
                var (type, code) = LegacyKeyCodeMapping.Map(c);
                type.Should().Be(KeyType.Keyboard);
                code.Should().Be(c);
            }
        }

        [Fact]
        public void LegacyKeyCodeMapping_UnknownCode_Throws()
        {
            FluentActions.Invoking(() => LegacyKeyCodeMapping.Map(9999))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData("%/o%", "ø")]
        [InlineData("%mu%", "µ")]
        [InlineData("%up%", "↑")]
        [InlineData("%20%", " ")]
        [InlineData("Hello %ae%!", "Hello æ!")]
        public void LegacyCharacterMapping_ReplacesConstants(string input, string expected)
        {
            LegacyCharacterMapping.Replace(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("%\"A%", "Ä")]
        [InlineData("%'a%", "à")]
        [InlineData("%a'%", "á")]
        [InlineData("%^O%", "Ô")]
        [InlineData("%~e%", "ẽ")]
        public void LegacyCharacterMapping_ReplacesDiacritics(string input, string expected)
        {
            LegacyCharacterMapping.Replace(input).Should().Be(expected);
        }
    }
}
