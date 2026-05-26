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

namespace ThoNohT.NohBoard.Hooking.Interop
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using static Defines;
    using static FunctionImports;

    /// <summary>
    /// The public interface for the <see cref="HookManager"/> class.
    /// </summary>
    public static partial class HookManager
    {
        #region Properties

        /// <summary>
        /// If <c>true</c> and the trap key is toggled on, mouse events will not propagate further.
        /// </summary>
        public static bool TrapMouse { get; set; }

        /// <summary>
        /// If <c>true</c> and the trap key is toggled on, keyboard events will not propagate further.
        /// </summary>
        public static bool TrapKeyboard { get; set; }

        /// <summary>
        /// When set, every key-code seen by the keyboard hook is passed through this function.
        /// If the function returns <c>true</c> the keycode is trapped (not propagated to other apps).
        /// </summary>
        public static Func<int, bool> KeyboardInsert = null;

        /// <summary>
        /// The keycode that toggles the mouse and/or keyboard trap. Default is Scroll Lock.
        /// </summary>
        public static int TrapToggleKeyCode { get; set; } = VK_SCROLL;

        /// <summary>
        /// The minimum time in milliseconds to keep a scroll key active after a wheel tick.
        /// </summary>
        public static int ScrollHold { get; set; } = 50;

        /// <summary>
        /// The minimum time in milliseconds to hold key presses on screen.
        /// </summary>
        public static int PressHold { get; set; } = 0;

        /// <summary>
        /// <c>true</c> when a low-level mouse hook is currently installed.
        /// </summary>
        public static bool MouseHookInstalled => mouseHookHandle != IntPtr.Zero;

        /// <summary>
        /// <c>true</c> when a low-level keyboard hook is currently installed.
        /// </summary>
        public static bool KeyboardHookInstalled => keyboardHookHandle != IntPtr.Zero;

        #endregion Properties

        #region Methods

        /// <summary>
        /// Enables the global low-level mouse hook. Idempotent.
        /// </summary>
        public static void EnableMouseHook()
        {
            if (mouseHookHandle != IntPtr.Zero) return;

            mouseDelegate = MouseHookProc;
            mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, mouseDelegate, IntPtr.Zero, 0);

            if (mouseHookHandle != IntPtr.Zero) return;

            // Subscription failed - capture last error before clearing the delegate, then throw.
            var error = Marshal.GetLastWin32Error();
            mouseDelegate = null;
            throw new Win32Exception(error, "Failed to install the low-level mouse hook.");
        }

        /// <summary>
        /// Disables the global low-level mouse hook. Idempotent.
        /// </summary>
        public static void DisableMouseHook()
        {
            if (mouseHookHandle == IntPtr.Zero) return;

            var handle = mouseHookHandle;
            mouseHookHandle = IntPtr.Zero;
            var success = UnhookWindowsHookEx(handle);
            mouseDelegate = null;

            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to remove the low-level mouse hook.");
        }

        /// <summary>
        /// Enables the global low-level keyboard hook. Idempotent.
        /// </summary>
        public static void EnableKeyboardHook()
        {
            if (keyboardHookHandle != IntPtr.Zero) return;

            keyboardDelegate = KeyboardHookProc;
            keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardDelegate, IntPtr.Zero, 0);

            if (keyboardHookHandle != IntPtr.Zero) return;

            var error = Marshal.GetLastWin32Error();
            keyboardDelegate = null;
            throw new Win32Exception(error, "Failed to install the low-level keyboard hook.");
        }

        /// <summary>
        /// Disables the global low-level keyboard hook. Idempotent.
        /// </summary>
        public static void DisableKeyboardHook()
        {
            if (keyboardHookHandle == IntPtr.Zero) return;

            var handle = keyboardHookHandle;
            keyboardHookHandle = IntPtr.Zero;
            var success = UnhookWindowsHookEx(handle);
            keyboardDelegate = null;

            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to remove the low-level keyboard hook.");
        }

        #endregion Methods
    }
}
