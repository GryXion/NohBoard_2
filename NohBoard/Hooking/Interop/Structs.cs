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
    /// Structs used for low-level keyboard and mouse interop.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: <c>ExtraInfo</c> is <c>ULONG_PTR</c> in the native header, which is pointer-sized
    /// (8 bytes on x64). Declaring it as <c>int</c> (4 bytes), as the original code did, made the
    /// managed struct 4 bytes smaller than its native counterpart on Windows 64-bit, leading to
    /// silent marshalling corruption when calling <see cref="Marshal.PtrToStructure"/>. This was
    /// the root cause of NohBoard failing to register key presses on Windows 11.
    /// </remarks>
    internal static class Structs
    {
        /// <summary>
        /// A point with X and Y coordinates as used by the low-level hook structs.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Low-level mouse hook information (<c>MSLLHOOKSTRUCT</c>).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseLLHookStruct
        {
            /// <summary>
            /// Cursor coordinates in screen space.
            /// </summary>
            public Point Point;

            /// <summary>
            /// Wheel delta for <c>WM_MOUSEWHEEL</c>/<c>WM_MOUSEHWHEEL</c> in the high-order word,
            /// or which X button for <c>WM_XBUTTONDOWN</c>/<c>WM_XBUTTONUP</c>.
            /// </summary>
            public int MouseData;

            /// <summary>
            /// Event-injected flag.
            /// </summary>
            public int Flags;

            /// <summary>
            /// Time stamp for this message.
            /// </summary>
            public int Time;

            /// <summary>
            /// Extra information associated with the message (<c>ULONG_PTR</c>).
            /// </summary>
            public UIntPtr ExtraInfo;
        }

        /// <summary>
        /// Low-level keyboard hook information (<c>KBDLLHOOKSTRUCT</c>).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct KeyboardHookStruct
        {
            /// <summary>
            /// Virtual-key code (1..254).
            /// </summary>
            public int VirtualKeyCode;

            /// <summary>
            /// Hardware scan code for the key.
            /// </summary>
            public int ScanCode;

            /// <summary>
            /// Extended-key flag, event-injected flags, context code, and transition-state flag.
            /// </summary>
            public int Flags;

            /// <summary>
            /// Time stamp for this message.
            /// </summary>
            public int Time;

            /// <summary>
            /// Extra information associated with the message (<c>ULONG_PTR</c>).
            /// </summary>
            public UIntPtr ExtraInfo;
        }
    }
}
