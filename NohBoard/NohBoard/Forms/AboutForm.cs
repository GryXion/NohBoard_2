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
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Logging;
    using Version = NohBoard.Version;

    /// <summary>
    /// "Help -> About..." dialog. Surfaces every piece of information the user might need to
    /// open a bug report: version, .NET runtime, resolved data directories, and a one-click
    /// "copy last 200 lines of log" button.
    /// </summary>
    public partial class AboutForm : Form
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AboutForm"/> class.
        /// </summary>
        public AboutForm()
        {
            this.InitializeComponent();
            this.PopulateInfo();
        }

        private void PopulateInfo()
        {
            this.titleLabel.Text = $"NohBoard {Version.Get}";
            this.runtimeLabel.Text =
                $"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.OSDescription}";

            this.pathsList.BeginUpdate();
            this.pathsList.Items.Clear();
            AddPath("Executable folder", AppPaths.BaseDirectory);
            AddPath("User data folder", AppPaths.UserDataDir);
            AddPath("Settings file", AppPaths.SettingsPath);
            AddPath("Logs folder", AppPaths.LogsDir);
            foreach (var root in AppPaths.KeyboardsSearchPaths)
            {
                AddPath("Keyboards search root", root);
            }
            this.pathsList.EndUpdate();

            this.portableLabel.Text = AppPaths.PortableMode
                ? "Running in --portable mode (data lives next to the exe)."
                : "Running in standard mode (data lives under %LOCALAPPDATA%).";
        }

        private void AddPath(string label, string path)
        {
            var item = new ListViewItem(label);
            item.SubItems.Add(path);
            item.Tag = path;
            this.pathsList.Items.Add(item);
        }

        private void pathsList_DoubleClick(object sender, EventArgs e)
        {
            if (this.pathsList.SelectedItems.Count == 0) return;
            var path = this.pathsList.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to open {path} from About dialog.", ex);
            }
        }

        private void githubLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ThoNohT/NohBoard",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to open GitHub URL.", ex);
            }
        }

        private void copyLogButton_Click(object sender, EventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"NohBoard {Version.Get}");
                sb.AppendLine($"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Log file: {Log.CurrentLogFile}");
                sb.AppendLine();
                sb.AppendLine("=== last 200 log lines ===");
                foreach (var line in Log.TailToday(200))
                {
                    sb.AppendLine(line);
                }
                Clipboard.SetText(sb.ToString());
                this.copyLogButton.Text = "Copied!";
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to copy log to clipboard.", ex);
                this.copyLogButton.Text = "Copy failed";
            }
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
