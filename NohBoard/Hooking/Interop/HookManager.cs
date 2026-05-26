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
    using System.Runtime.InteropServices;
    using static Defines;
    using static FunctionImports;
    using static Structs;

    /// <summary>
    /// A manager containing functionality for managing keyboard and mouse hooks.
    /// </summary>
    /// <remarks>
    /// Based on the global hook example from
    /// http://www.codeproject.com/Articles/7294/Processing-Global-Mouse-and-Keyboard-Hooks-in-C
    /// but rewritten to be 64-bit safe and to fit NohBoard's needs.
    /// </remarks>
    public static partial class HookManager
    {
        /// <summary>
        /// Delegate matching the signature of a low-level Windows hook procedure.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>WPARAM</c> and <c>LPARAM</c> are pointer-sized (<see cref="IntPtr"/>), and the return
        /// value is the result of the next hook in the chain (also pointer-sized). Declaring these
        /// as <see cref="int"/> on x64 would silently truncate values handed to us by the kernel.
        /// </para>
        /// </remarks>
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// If <c>true</c>, keyboard or mouse events will not be propagated to other programs
        /// (when <see cref="TrapKeyboard"/> / <see cref="TrapMouse"/> are also <c>true</c>).
        /// </summary>
        private static bool trapEnabled;

        #region Mouse Hook

        /// <summary>
        /// Strong reference to the delegate so the GC will not collect it while Windows holds
        /// an unmanaged function pointer to it.
        /// </summary>
        private static HookProc mouseDelegate;

        /// <summary>
        /// Handle to the installed mouse hook (<c>HHOOK</c>).
        /// </summary>
        private static IntPtr mouseHookHandle;

        /// <summary>
        /// Callback invoked by Windows for every low-level mouse event.
        /// </summary>
        private static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);

            var info = Marshal.PtrToStructure<MouseLLHookStruct>(lParam);
            ProcessMouseMessage(wParam.ToInt32(), info);

            if (trapEnabled && TrapMouse) return new IntPtr(1);

            return CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Dispatches a marshalled mouse message to the <see cref="MouseState"/>. Split out
        /// so it can be unit-tested without a real hook installed.
        /// </summary>
        internal static void ProcessMouseMessage(int message, MouseLLHookStruct info)
        {
            ushort subCode;
            switch (message)
            {
                case WM_LBUTTONDOWN:
                    MouseState.AddPressedElement(MouseKeyCode.LeftButton);
                    break;
                case WM_LBUTTONUP:
                    MouseState.RemovePressedElement(MouseKeyCode.LeftButton, PressHold);
                    break;
                case WM_RBUTTONDOWN:
                    MouseState.AddPressedElement(MouseKeyCode.RightButton);
                    break;
                case WM_RBUTTONUP:
                    MouseState.RemovePressedElement(MouseKeyCode.RightButton, PressHold);
                    break;
                case WM_MBUTTONDOWN:
                    MouseState.AddPressedElement(MouseKeyCode.MiddleButton);
                    break;
                case WM_MBUTTONUP:
                    MouseState.RemovePressedElement(MouseKeyCode.MiddleButton, PressHold);
                    break;
                case WM_MOUSEWHEEL:
                    subCode = HiWord(info.MouseData);
                    if (subCode == 120) MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollUp);
                    if (subCode == 65416) MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollDown);
                    break;
                case WM_MOUSEHWHEEL:
                    subCode = HiWord(info.MouseData);
                    if (subCode == 120) MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollRight);
                    if (subCode == 65416) MouseState.AddScrollDirection(MouseScrollKeyCode.ScrollLeft);
                    break;
                case WM_XBUTTONDOWN:
                    subCode = HiWord(info.MouseData);
                    if (subCode == XBUTTON1) MouseState.AddPressedElement(MouseKeyCode.X1Button);
                    if (subCode == XBUTTON2) MouseState.AddPressedElement(MouseKeyCode.X2Button);
                    break;
                case WM_XBUTTONUP:
                    subCode = HiWord(info.MouseData);
                    if (subCode == XBUTTON1) MouseState.RemovePressedElement(MouseKeyCode.X1Button, PressHold);
                    if (subCode == XBUTTON2) MouseState.RemovePressedElement(MouseKeyCode.X2Button, PressHold);
                    break;
                case WM_MOUSEMOVE:
                    MouseState.RegisterLocation(new System.Drawing.Point(info.Point.X, info.Point.Y), info.Time);
                    break;
            }
        }

        #endregion Mouse Hook

        #region Keyboard Hook

        /// <summary>
        /// Strong reference to the delegate so the GC will not collect it while Windows holds
        /// an unmanaged function pointer to it.
        /// </summary>
        private static HookProc keyboardDelegate;

        /// <summary>
        /// Handle to the installed keyboard hook (<c>HHOOK</c>).
        /// </summary>
        private static IntPtr keyboardHookHandle;

        /// <summary>
        /// Callback invoked by Windows for every low-level keyboard event.
        /// </summary>
        private static IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);

            var info = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
            var (code, trap) = ProcessKeyboardMessage(wParam.ToInt32(), info);

            if (trap) return new IntPtr(1);

            return CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Dispatches a marshalled keyboard message to the <see cref="KeyboardState"/>. Split out
        /// so it can be unit-tested without a real hook installed.
        /// </summary>
        /// <returns>
        /// The effective key code (after the extended-key normalization) and a flag indicating
        /// whether the event should be trapped (not propagated to other applications).
        /// </returns>
        internal static (int code, bool trap) ProcessKeyboardMessage(int message, KeyboardHookStruct info)
        {
            var extended = (info.Flags & LLKHF_EXTENDED) != 0;
            var code = extended && info.VirtualKeyCode == VK_RETURN ? 1025 : info.VirtualKeyCode;

            switch (message)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    KeyboardState.AddPressedElement(code, PressHold);
                    break;
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    KeyboardState.RemovePressedElement(code, PressHold);
                    if (code == TrapToggleKeyCode) trapEnabled = !trapEnabled;
                    break;
            }

            // Preserve original semantics: when KeyboardInsert is set its return value is the
            // final decision for this key. When it is null, fall back to the trap configuration.
            var trap = KeyboardInsert?.Invoke(code) ?? (trapEnabled && TrapKeyboard);
            return (code, trap);
        }

        #endregion keyboard Hook
    }
}
