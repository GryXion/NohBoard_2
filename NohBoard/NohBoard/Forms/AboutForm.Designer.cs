/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Forms
{
    partial class AboutForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.titleLabel = new System.Windows.Forms.Label();
            this.runtimeLabel = new System.Windows.Forms.Label();
            this.portableLabel = new System.Windows.Forms.Label();
            this.pathsList = new System.Windows.Forms.ListView();
            this.colLabel = new System.Windows.Forms.ColumnHeader();
            this.colPath = new System.Windows.Forms.ColumnHeader();
            this.githubLink = new System.Windows.Forms.LinkLabel();
            this.copyLogButton = new System.Windows.Forms.Button();
            this.closeButton = new System.Windows.Forms.Button();
            this.hintLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(12, 12);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(120, 25);
            this.titleLabel.Text = "NohBoard";
            // 
            // runtimeLabel
            // 
            this.runtimeLabel.AutoSize = true;
            this.runtimeLabel.Location = new System.Drawing.Point(14, 45);
            this.runtimeLabel.Name = "runtimeLabel";
            this.runtimeLabel.Size = new System.Drawing.Size(60, 15);
            this.runtimeLabel.Text = ".NET";
            // 
            // portableLabel
            // 
            this.portableLabel.AutoSize = true;
            this.portableLabel.Location = new System.Drawing.Point(14, 65);
            this.portableLabel.Name = "portableLabel";
            this.portableLabel.Size = new System.Drawing.Size(60, 15);
            this.portableLabel.Text = "Mode";
            // 
            // pathsList
            // 
            this.pathsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colLabel,
            this.colPath});
            this.pathsList.FullRowSelect = true;
            this.pathsList.GridLines = true;
            this.pathsList.HideSelection = false;
            this.pathsList.Location = new System.Drawing.Point(14, 95);
            this.pathsList.MultiSelect = false;
            this.pathsList.Name = "pathsList";
            this.pathsList.Size = new System.Drawing.Size(560, 160);
            this.pathsList.UseCompatibleStateImageBehavior = false;
            this.pathsList.View = System.Windows.Forms.View.Details;
            this.pathsList.DoubleClick += new System.EventHandler(this.pathsList_DoubleClick);
            // 
            // colLabel
            // 
            this.colLabel.Text = "Location";
            this.colLabel.Width = 160;
            // 
            // colPath
            // 
            this.colPath.Text = "Path";
            this.colPath.Width = 380;
            // 
            // hintLabel
            // 
            this.hintLabel.AutoSize = true;
            this.hintLabel.Location = new System.Drawing.Point(14, 258);
            this.hintLabel.Name = "hintLabel";
            this.hintLabel.Size = new System.Drawing.Size(300, 15);
            this.hintLabel.Text = "Double-click any row to open the location in Explorer.";
            // 
            // githubLink
            // 
            this.githubLink.AutoSize = true;
            this.githubLink.Location = new System.Drawing.Point(14, 285);
            this.githubLink.Name = "githubLink";
            this.githubLink.Size = new System.Drawing.Size(220, 15);
            this.githubLink.TabIndex = 0;
            this.githubLink.TabStop = true;
            this.githubLink.Text = "https://github.com/ThoNohT/NohBoard";
            this.githubLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.githubLink_LinkClicked);
            // 
            // copyLogButton
            // 
            this.copyLogButton.Location = new System.Drawing.Point(14, 320);
            this.copyLogButton.Name = "copyLogButton";
            this.copyLogButton.Size = new System.Drawing.Size(220, 28);
            this.copyLogButton.Text = "Copy last 200 log lines";
            this.copyLogButton.UseVisualStyleBackColor = true;
            this.copyLogButton.Click += new System.EventHandler(this.copyLogButton_Click);
            // 
            // closeButton
            // 
            this.closeButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.closeButton.Location = new System.Drawing.Point(490, 320);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(84, 28);
            this.closeButton.Text = "Close";
            this.closeButton.UseVisualStyleBackColor = true;
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            // 
            // AboutForm
            // 
            this.AcceptButton = this.closeButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeButton;
            this.ClientSize = new System.Drawing.Size(588, 360);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.copyLogButton);
            this.Controls.Add(this.githubLink);
            this.Controls.Add(this.hintLabel);
            this.Controls.Add(this.pathsList);
            this.Controls.Add(this.portableLabel);
            this.Controls.Add(this.runtimeLabel);
            this.Controls.Add(this.titleLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About NohBoard";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label runtimeLabel;
        private System.Windows.Forms.Label portableLabel;
        private System.Windows.Forms.ListView pathsList;
        private System.Windows.Forms.ColumnHeader colLabel;
        private System.Windows.Forms.ColumnHeader colPath;
        private System.Windows.Forms.Label hintLabel;
        private System.Windows.Forms.LinkLabel githubLink;
        private System.Windows.Forms.Button copyLogButton;
        private System.Windows.Forms.Button closeButton;
    }
}
