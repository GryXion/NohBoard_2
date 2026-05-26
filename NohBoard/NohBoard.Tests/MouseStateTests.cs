/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using System.Linq;
    using System.Threading;
    using FluentAssertions;
    using ThoNohT.NohBoard.Hooking;
    using Xunit;

    [Collection(nameof(StateCollection))]
    public sealed class MouseStateTests
    {
        public MouseStateTests()
        {
            foreach (var key in MouseState.PressedKeys.ToList())
                MouseState.RemovePressedElement(key, hold: 0);
            Thread.Sleep(5);
            MouseState.CheckKeyHolds(hold: 0);
            _ = MouseState.Updated;
        }

        [Fact]
        public void AddPressedElement_RegistersTheButton()
        {
            MouseState.AddPressedElement(MouseKeyCode.LeftButton);
            MouseState.PressedKeys.Should().Contain(MouseKeyCode.LeftButton);
        }

        [Fact]
        public void RemovePressedElement_AfterElapsedTime_RemovesButton()
        {
            MouseState.AddPressedElement(MouseKeyCode.RightButton);
            Thread.Sleep(10);
            MouseState.RemovePressedElement(MouseKeyCode.RightButton, hold: 0);
            MouseState.PressedKeys.Should().NotContain(MouseKeyCode.RightButton);
        }

        [Fact]
        public void ScrollDirection_IncrementsCounter()
        {
            MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollUp);
            MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollUp);

            var counts = MouseState.ScrollCounts;
            counts[(int)MouseScrollKeyCode.ScrollUp].Should().BeGreaterOrEqualTo(2);
        }

        [Fact]
        public void AverageSpeed_DefaultsToZero()
        {
            var avg = MouseState.AverageSpeed;
            avg.Width.Should().Be(0);
            avg.Height.Should().Be(0);
        }
    }
}
