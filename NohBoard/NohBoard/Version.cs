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

namespace ThoNohT.NohBoard
{
    using System.Reflection;

    /// <summary>
    /// Class that exposes the current version of NohBoard.
    /// </summary>
    /// <remarks>
    /// The version information comes from the assembly metadata that is set in the <c>NohBoard.csproj</c> file.
    /// This replaces the previous <c>gotri.exe</c> build-time templating workflow.
    /// </remarks>
    public static class Version
    {
        private static readonly System.Version AssemblyVersion =
            Assembly.GetExecutingAssembly().GetName().Version ?? new System.Version(1, 0, 0, 0);

        private static readonly string InformationalVersion =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}";

        /// <summary>
        /// Gets the version as a formatted display string (e.g. "v1.2.3" or "v1.2.3-beta+abcdef").
        /// </summary>
        public static string Get
        {
            get
            {
                // Strip a "+commit" suffix if present, then ensure the string has a leading "v".
                var v = InformationalVersion;
                if (string.IsNullOrEmpty(v)) v = $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}";
                return v.StartsWith("v") ? v : "v" + v;
            }
        }

        /// <summary>
        /// Gets the major version.
        /// </summary>
        public static int Major => AssemblyVersion.Major;

        /// <summary>
        /// Gets the minor version.
        /// </summary>
        public static int Minor => AssemblyVersion.Minor;

        /// <summary>
        /// Gets the patch version.
        /// </summary>
        public static int Patch => AssemblyVersion.Build < 0 ? 0 : AssemblyVersion.Build;
    }
}
