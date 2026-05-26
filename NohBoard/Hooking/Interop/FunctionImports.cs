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

    /// <summary>
    /// Native function imports used by the hooking layer.
    /// </summary>
    /// <remarks>
    /// All hook handles use <see cref="IntPtr"/> and the hook procedure parameters use the correct
    /// pointer-sized types (<c>HHOOK</c>, <c>WPARAM</c>, <c>LPARAM</c>, <c>ULONG_PTR</c>). The previous
    /// implementation used <see cref="int"/> for hook handles and the wParam parameter, which caused
    /// silent failures on 64-bit Windows 11 because <c>KBDLLHOOKSTRUCT.dwExtraInfo</c> is
    /// <c>ULONG_PTR</c> (8 bytes on x64) - so the struct size was 4 bytes too small and
    /// <see cref="Marshal.PtrToStructure"/> would either truncate fields or read garbage.
    /// <para>
    /// We use the classic <see cref="DllImportAttribute"/> rather than the newer source-generated
    /// <c>LibraryImport</c>, because the latter requires <c>AllowUnsafeBlocks</c> to be enabled.
    /// For low-frequency entry points like installing/removing a hook, the runtime marshalling
    /// cost is irrelevant; <c>CallNextHookEx</c> is on the per-event hot path but only passes
    /// pointer-sized integers, so there is no measurable difference there either.
    /// </para>
    /// </remarks>
    internal static class FunctionImports
    {
        /// <summary>
        /// Passes the hook information to the next hook procedure in the current hook chain.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        /// <summary>
        /// Installs an application-defined hook procedure into a hook chain.
        /// </summary>
        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode,
            EntryPoint = "SetWindowsHookExW",
            ExactSpelling = true,
            SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int idHook,
            HookManager.HookProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        /// <summary>
        /// Removes a hook procedure installed in a hook chain by <see cref="SetWindowsHookEx"/>.
        /// </summary>
        /// <returns>Non-zero on success, zero on failure (see <see cref="Marshal.GetLastWin32Error"/>).</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// Retrieves the status of the specified virtual key.
        /// </summary>
        /// <remarks>
        /// If the high-order bit of the return value is 1 the key is down, otherwise it is up.
        /// If the low-order bit is 1 the key is toggled (e.g. CAPS LOCK is on).
        /// </remarks>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern short GetKeyState(int vKey);

        /// <summary>
        /// Retrieves the higher order word of the data.
        /// </summary>
        internal static ushort HiWord(int data) => (ushort)((data >> 16) & 0xffff);

        /// <summary>
        /// Retrieves the lower order word of the data.
        /// </summary>
        internal static ushort LoWord(int data) => (ushort)data;
    }
}
