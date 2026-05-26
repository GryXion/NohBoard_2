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
    using ThoNohT.NohBoard.Forms;
    using Xunit;

    public sealed class ErrorReporterTests
    {
        [Fact]
        public void BuildUserFacingBody_IncludesMessageAndExceptionSummary()
        {
            var body = ErrorReporter.BuildUserFacingBody(
                "Something went wrong",
                new InvalidOperationException("boom"));

            body.Should().Contain("Something went wrong");
            body.Should().Contain("InvalidOperationException");
            body.Should().Contain("boom");
            body.Should().Contain("copy");
            body.Should().Contain("log");
        }

        [Fact]
        public void BuildUserFacingBody_NoException_StillShowsMessage()
        {
            var body = ErrorReporter.BuildUserFacingBody("just a message", null);
            body.Should().Contain("just a message");
            body.Should().NotContain("Technical details");
        }

        [Fact]
        public void BuildClipboardPayload_IncludesTitleMessageAndStackTrace()
        {
            Exception caught;
            try { throw new InvalidOperationException("clipboard-boom"); }
            catch (Exception ex) { caught = ex; }

            var payload = ErrorReporter.BuildClipboardPayload("Title", "Body", caught);

            payload.Should().StartWith("# Title");
            payload.Should().Contain("Body");
            payload.Should().Contain("InvalidOperationException");
            payload.Should().Contain("clipboard-boom");
            payload.Should().Contain("Log file:");
            payload.Should().Contain("User data dir:");
        }

        [Fact]
        public void BuildClipboardPayload_TraversesInnerExceptions()
        {
            var inner = new ArgumentException("inner-msg");
            var outer = new InvalidOperationException("outer-msg", inner);

            var payload = ErrorReporter.BuildClipboardPayload("T", "M", outer);

            payload.Should().Contain("outer-msg");
            payload.Should().Contain("inner-msg");
            payload.Should().Contain("inner exception");
        }
    }
}
