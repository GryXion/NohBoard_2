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
    using System.IO;
    using System.Linq;
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Logging;
    using Xunit;

    /// <summary>
    /// Log tests share the same collection as AppPaths because they also depend on
    /// the LOCALAPPDATA env var redirection.
    /// </summary>
    [Collection(nameof(AppPathsTests))]
    public sealed class LogTests : IDisposable
    {
        private readonly string originalLocalAppData;
        private readonly bool originalPortable;
        private readonly string tempLocalAppData;

        public LogTests()
        {
            this.originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            this.originalPortable = AppPaths.PortableMode;

            this.tempLocalAppData = Path.Combine(Path.GetTempPath(), "nb-tests-log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempLocalAppData);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.tempLocalAppData);
            AppPaths.PortableMode = false;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", this.originalLocalAppData);
            AppPaths.PortableMode = this.originalPortable;
            try { Directory.Delete(this.tempLocalAppData, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void Info_WritesLineToTodayFile()
        {
            var sentinel = "test sentinel " + Guid.NewGuid().ToString("N");
            Log.Info(sentinel);

            File.Exists(Log.CurrentLogFile).Should().BeTrue();
            File.ReadAllText(Log.CurrentLogFile).Should().Contain(sentinel);
        }

        [Fact]
        public void Error_IncludesExceptionTypeAndMessage()
        {
            var sentinel = "boom " + Guid.NewGuid().ToString("N");
            Log.Error("explosion", new InvalidOperationException(sentinel));

            var content = File.ReadAllText(Log.CurrentLogFile);
            content.Should().Contain("explosion");
            content.Should().Contain("InvalidOperationException");
            content.Should().Contain(sentinel);
        }

        [Fact]
        public void TailToday_ReturnsAtMostN()
        {
            for (int i = 0; i < 15; i++) Log.Info("line " + i);
            var tail = Log.TailToday(10);
            tail.Should().HaveCountLessOrEqualTo(10);
            tail.Last().Should().Contain("line 14");
        }

        [Fact]
        public void CurrentLogFile_LivesInsideLogsDir()
        {
            Log.Info("warm-up");
            Log.CurrentLogFile.Should().StartWith(AppPaths.LogsDir);
            Log.CurrentLogFile.Should().EndWith(".log");
        }
    }
}
