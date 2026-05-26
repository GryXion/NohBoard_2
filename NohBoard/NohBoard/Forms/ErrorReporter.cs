/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Forms
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Logging;
    using Version = NohBoard.Version;

    /// <summary>
    /// Single entry point for reporting unrecoverable failures to the user.
    /// </summary>
    /// <remarks>
    /// Compared to a raw <see cref="MessageBox.Show(string)"/> call this:
    /// <list type="bullet">
    /// <item>Logs every shown error to the rolling log so it ends up in a bug report.</item>
    /// <item>Offers a "Copy details" action so the user can paste the stack trace into a
    ///   GitHub issue without rummaging through the log folder.</item>
    /// <item>Offers an "Open log" action that pops the current log file open in the OS
    ///   default text editor.</item>
    /// </list>
    /// Logic is split out into <see cref="BuildClipboardPayload"/> so it can be unit-tested
    /// without showing a dialog.
    /// </remarks>
    public static class ErrorReporter
    {
        /// <summary>
        /// Logs the error and presents it to the user with optional Copy details / Open log
        /// actions.
        /// </summary>
        /// <param name="title">Window title and log header for the error.</param>
        /// <param name="message">Short, user-facing description of what failed.</param>
        /// <param name="exception">The underlying exception, if any. Logged in full.</param>
        public static void Show(string title, string message, Exception exception = null)
        {
            // Log first so even users who hit "X" without reading still leave a trail.
            Log.Error($"{title}: {message}", exception);

            try
            {
                var detail = BuildClipboardPayload(title, message, exception);

                // Buttons: Yes = Copy details, No = Open log, Cancel = just dismiss.
                var result = MessageBox.Show(
                    BuildUserFacingBody(message, exception),
                    title,
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button3);

                switch (result)
                {
                    case DialogResult.Yes:
                        TryCopyToClipboard(detail);
                        break;
                    case DialogResult.No:
                        TryOpenLog();
                        break;
                }
            }
            catch (Exception ex)
            {
                // Last resort: if even the message box raises (e.g. headless test), just log it.
                Log.Error("ErrorReporter failed to display the error dialog.", ex);
            }
        }

        /// <summary>
        /// Builds the multi-line body shown to the user. Splits the headline from the technical
        /// detail so the dialog stays scannable.
        /// </summary>
        internal static string BuildUserFacingBody(string message, Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message ?? "An unexpected error occurred.");
            if (exception != null)
            {
                sb.AppendLine();
                sb.AppendLine("Technical details:");
                sb.AppendLine(exception.GetType().Name + ": " + exception.Message);
            }
            sb.AppendLine();
            sb.AppendLine("Yes = copy full details to clipboard.");
            sb.AppendLine("No  = open the log file.");
            sb.AppendLine("Cancel = dismiss.");
            return sb.ToString();
        }

        /// <summary>
        /// Builds the rich payload copied to the clipboard. Includes the title, message, the
        /// full exception chain, and the log path so support requests stay self-contained.
        /// </summary>
        internal static string BuildClipboardPayload(string title, string message, Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine(message ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine($"NohBoard {Version.Get}");
            sb.AppendLine($"Log file: {Log.CurrentLogFile}");
            sb.AppendLine($"User data dir: {AppPaths.UserDataDir}");
            sb.AppendLine();

            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                sb.AppendLine(ex.GetType().FullName + ": " + ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.AppendLine(ex.StackTrace);
                }
                if (ex.InnerException != null) sb.AppendLine("---- inner exception ----");
            }

            return sb.ToString().TrimEnd();
        }

        private static void TryCopyToClipboard(string payload)
        {
            try
            {
                Clipboard.SetText(payload);
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to copy error details to clipboard.", ex);
            }
        }

        private static void TryOpenLog()
        {
            try
            {
                var path = Log.CurrentLogFile;
                if (!File.Exists(path))
                {
                    Log.Warn($"Open-log requested but the log file does not exist: {path}.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to open the log file.", ex);
            }
        }
    }
}
