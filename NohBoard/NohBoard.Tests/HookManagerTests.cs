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
    using System.Linq;
    using System.Runtime.InteropServices;
    using FluentAssertions;
    using ThoNohT.NohBoard.Hooking;
    using ThoNohT.NohBoard.Hooking.Interop;
    using Xunit;
    using static ThoNohT.NohBoard.Hooking.Interop.Defines;

    /// <summary>
    /// End-to-end tests for the hook dispatch layer. These intentionally avoid installing a real
    /// global hook (which would require a message pump) and instead synthesize the marshalled
    /// payload that Windows would normally hand to our callback.
    /// </summary>
    /// <remarks>
    /// The most important assertion here is the size of <see cref="Structs.KeyboardHookStruct"/>
    /// and <see cref="Structs.MouseLLHookStruct"/>: on 64-bit Windows these must be 24 and 32 bytes
    /// respectively. Before the fix, the managed structs were 20 and 28 bytes (4 bytes short),
    /// which caused marshalling corruption and was the root cause of NohBoard failing on Windows 11.
    /// </remarks>
    [Collection(nameof(StateCollection))]
    public sealed class HookManagerTests
    {
        public HookManagerTests()
        {
            // Clear residual state from prior tests. Two cleanup passes around a small sleep
            // make sure entries still on hold from previous tests are evicted.
            foreach (var key in KeyboardState.PressedKeys.ToList())
                KeyboardState.RemovePressedElement(key, hold: 0);
            foreach (var key in MouseState.PressedKeys.ToList())
                MouseState.RemovePressedElement(key, hold: 0);
            System.Threading.Thread.Sleep(5);
            KeyboardState.CheckKeyHolds(hold: 0);
            MouseState.CheckKeyHolds(hold: 0);
        }

        [Fact]
        public void KeyboardHookStruct_SizeMatchesNativeKBDLLHOOKSTRUCT()
        {
            // Native KBDLLHOOKSTRUCT on x64 is 4*sizeof(DWORD) + sizeof(ULONG_PTR) = 16 + 8 = 24.
            // On x86 it would be 4*4 + 4 = 20.
            var expected = 16 + IntPtr.Size;
            Marshal.SizeOf<Structs.KeyboardHookStruct>().Should().Be(expected);
        }

        [Fact]
        public void MouseLLHookStruct_SizeMatchesNativeMSLLHOOKSTRUCT()
        {
            // Native MSLLHOOKSTRUCT on x64:
            //   POINT pt           (8 bytes, offsets 0-7)
            //   DWORD mouseData    (4 bytes, offsets 8-11)
            //   DWORD flags        (4 bytes, offsets 12-15)
            //   DWORD time         (4 bytes, offsets 16-19)
            //   4 bytes of padding so dwExtraInfo is 8-byte aligned (offsets 20-23)
            //   ULONG_PTR dwExtraInfo (8 bytes, offsets 24-31)
            // -> 32 bytes total on x64, 24 on x86.
            var expected = IntPtr.Size == 8 ? 32 : 24;
            Marshal.SizeOf<Structs.MouseLLHookStruct>().Should().Be(expected);
        }

        [Fact]
        public void ProcessKeyboardMessage_KeyDown_RegistersPressedKey()
        {
            var info = new Structs.KeyboardHookStruct
            {
                VirtualKeyCode = 0x41, // 'A'
                ScanCode = 30,
                Flags = 0,
                Time = 0,
                ExtraInfo = UIntPtr.Zero,
            };

            var (code, trap) = HookManager.ProcessKeyboardMessage(WM_KEYDOWN, info);

            code.Should().Be(0x41);
            trap.Should().BeFalse();
            KeyboardState.PressedKeys.Should().Contain(0x41);
        }

        [Fact]
        public void ProcessKeyboardMessage_KeyUp_RemovesPressedKey()
        {
            var info = new Structs.KeyboardHookStruct { VirtualKeyCode = 0x42, ExtraInfo = UIntPtr.Zero };
            HookManager.ProcessKeyboardMessage(WM_KEYDOWN, info);

            // The "hold" logic in StateBase requires elapsed time > 0 to actually remove a key.
            System.Threading.Thread.Sleep(10);

            HookManager.ProcessKeyboardMessage(WM_KEYUP, info);

            KeyboardState.PressedKeys.Should().NotContain(0x42);
        }

        [Fact]
        public void ProcessKeyboardMessage_ExtendedReturnKey_IsRemappedTo1025()
        {
            var info = new Structs.KeyboardHookStruct
            {
                VirtualKeyCode = VK_RETURN,
                Flags = LLKHF_EXTENDED, // extended -> numpad Enter
                ExtraInfo = UIntPtr.Zero,
            };

            var (code, _) = HookManager.ProcessKeyboardMessage(WM_KEYDOWN, info);

            code.Should().Be(1025, "extended Return is remapped to 1025 to distinguish it from the main Enter key");
            KeyboardState.PressedKeys.Should().Contain(1025);
        }

        [Fact]
        public void ProcessKeyboardMessage_HonoursKeyboardInsertCallback()
        {
            var savedInsert = HookManager.KeyboardInsert;
            try
            {
                HookManager.KeyboardInsert = _ => true;
                var info = new Structs.KeyboardHookStruct { VirtualKeyCode = 0x44, ExtraInfo = UIntPtr.Zero };

                var (_, trap) = HookManager.ProcessKeyboardMessage(WM_KEYDOWN, info);

                trap.Should().BeTrue("KeyboardInsert returning true must trap the key");
            }
            finally
            {
                HookManager.KeyboardInsert = savedInsert;
            }
        }

        [Fact]
        public void ProcessMouseMessage_LeftButtonDown_AddsToState()
        {
            var info = new Structs.MouseLLHookStruct
            {
                Point = new Structs.Point { X = 0, Y = 0 },
                MouseData = 0,
                Flags = 0,
                Time = 0,
                ExtraInfo = UIntPtr.Zero,
            };

            HookManager.ProcessMouseMessage(WM_LBUTTONDOWN, info);

            MouseState.PressedKeys.Should().Contain(MouseKeyCode.LeftButton);
        }

        [Fact]
        public void ProcessMouseMessage_RightButtonUp_RemovesFromState()
        {
            var info = new Structs.MouseLLHookStruct { ExtraInfo = UIntPtr.Zero };
            HookManager.ProcessMouseMessage(WM_RBUTTONDOWN, info);

            HookManager.ProcessMouseMessage(WM_RBUTTONUP, info);

            MouseState.PressedKeys.Should().NotContain(MouseKeyCode.RightButton);
        }

        [Fact]
        public void ProcessMouseMessage_MouseWheel_UpdatesScrollCounts()
        {
            var info = new Structs.MouseLLHookStruct
            {
                MouseData = 120 << 16, // positive wheel tick (scroll up)
                ExtraInfo = UIntPtr.Zero,
            };

            HookManager.ProcessMouseMessage(WM_MOUSEWHEEL, info);

            var counts = MouseState.ScrollCounts;
            counts[(int)MouseScrollKeyCode.ScrollUp].Should().BeGreaterOrEqualTo(1);
        }
    }
}
