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

    /// <summary>
    /// State-machine tests for <see cref="KeyboardState"/>. These run sequentially because
    /// <see cref="KeyboardState"/> uses static fields.
    /// </summary>
    [Collection(nameof(StateCollection))]
    public sealed class KeyboardStateTests
    {
        public KeyboardStateTests()
        {
            // Clear any residual state from prior tests. Removing twice + checking holds with
            // a long enough wait reliably evicts stale entries from the static dictionary.
            foreach (var key in KeyboardState.PressedKeys.ToList())
                KeyboardState.RemovePressedElement(key, hold: 0);
            Thread.Sleep(5);
            KeyboardState.CheckKeyHolds(hold: 0);
            _ = KeyboardState.Updated;
        }

        [Fact]
        public void AddPressedElement_RegistersTheKey_AndUpdatesIsSet()
        {
            const int keyCode = 0x41; // 'A'
            KeyboardState.AddPressedElement(keyCode, hold: 0);

            KeyboardState.PressedKeys.Should().Contain(keyCode);
            KeyboardState.Updated.Should().BeTrue("a new press should flip the Updated flag");
            KeyboardState.Updated.Should().BeFalse("the Updated flag is auto-reset after read");
        }

        [Fact]
        public void RemovePressedElement_AfterElapsedTime_RemovesKey()
        {
            const int keyCode = 0x42; // 'B'
            KeyboardState.AddPressedElement(keyCode, hold: 0);
            _ = KeyboardState.Updated;

            // The stopwatch must advance past the press time for the same-frame removal path
            // to succeed; in practice this is the normal case (presses arrive milliseconds apart).
            Thread.Sleep(10);
            KeyboardState.RemovePressedElement(keyCode, hold: 0);

            KeyboardState.PressedKeys.Should().NotContain(keyCode);
        }

        [Fact]
        public void RemovePressedElement_WithLargeHold_KeepsKeyInListUntilCheck()
        {
            const int keyCode = 0x43; // 'C'
            KeyboardState.AddPressedElement(keyCode, hold: 60_000);
            _ = KeyboardState.Updated;

            KeyboardState.RemovePressedElement(keyCode, hold: 60_000);
            KeyboardState.PressedKeys.Should()
                .Contain(keyCode, "the key should still be drawn until the hold time elapses");
        }

        [Fact]
        public void RemovePressedElement_OnUnknownKey_IsANoOp()
        {
            // The dictionary is empty at this point thanks to the constructor cleanup.
            KeyboardState.RemovePressedElement(0xFE, hold: 0);
            KeyboardState.PressedKeys.Should().NotContain(0xFE);
        }

        [Fact]
        public void PressedKeys_IsAReadOnlySnapshot()
        {
            const int keyCode = 0x44; // 'D'
            KeyboardState.AddPressedElement(keyCode, hold: 0);

            var snapshot = KeyboardState.PressedKeys;

            Thread.Sleep(10);
            KeyboardState.RemovePressedElement(keyCode, hold: 0);

            snapshot.Should().Contain(keyCode, "PressedKeys returns a defensive copy");
            KeyboardState.PressedKeys.Should().NotContain(keyCode);
        }

        [Fact]
        public void CheckKeyHolds_PurgesRemovedEntriesAfterTimeElapses()
        {
            const int keyCode = 0x45; // 'E'
            KeyboardState.AddPressedElement(keyCode, hold: 100);
            _ = KeyboardState.Updated;

            KeyboardState.RemovePressedElement(keyCode, hold: 100);
            KeyboardState.PressedKeys.Should().Contain(keyCode);

            Thread.Sleep(150);
            KeyboardState.CheckKeyHolds(hold: 100);
            KeyboardState.PressedKeys.Should().NotContain(keyCode);
        }
    }

    /// <summary>
    /// xUnit collection used to serialise all tests that touch global static
    /// <c>KeyboardState</c> / <c>MouseState</c> instances.
    /// </summary>
    [CollectionDefinition(nameof(StateCollection), DisableParallelization = true)]
    public sealed class StateCollection
    {
    }
}
