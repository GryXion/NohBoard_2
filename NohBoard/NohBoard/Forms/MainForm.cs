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

namespace ThoNohT.NohBoard.Forms
{
    using Extra;
    using Hooking;
    using Hooking.Interop;
    using Keyboard;
    using Keyboard.ElementDefinitions;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Text;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Xml;
    using ThoNohT.NohBoard.Keyboard.Styles;
    using ThoNohT.NohBoard.Logging;
    using Version = NohBoard.Version;

    /// <summary>
    /// The main form.
    /// </summary>
    public partial class MainForm : Form
    {
        #region Fields

        /// <summary>
        /// The back-brushes used for efficient drawing.
        /// </summary>
        private readonly Dictionary<bool, Dictionary<bool, Brush>> backBrushes =
            new Dictionary<bool, Dictionary<bool, Brush>>();

        /// <summary>
        /// The element currently under the cursor.
        /// </summary>
        private ElementDefinition elementUnderCursor = null;

        /// <summary>
        /// The latest version, if it was retrieved from the update site.
        /// </summary>
        private VersionInfo latestVersion = null;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm" /> class.
        /// </summary>
        public MainForm()
        {
            this.InitializeComponent();
            // OptimizedDoubleBuffer + AllPaintingInWmPaint + UserPaint together give
            // tear-free rendering on Windows 11 without the extra cost of the legacy
            // DoubleBuffered alone (which only allocates a back buffer). ResizeRedraw
            // ensures the keyboard rerenders cleanly during DPI changes.
            this.SetStyle(
                ControlStyles.ResizeRedraw |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            this.UpdateStyles();
        }

        #endregion Constructors

        #region Version check

        /// <summary>
        /// URL of the JSON document published on GitHub Gist that exposes the latest released
        /// NohBoard version. Returned shape is documented by <see cref="VersionInfo"/>.
        /// </summary>
        private const string UpdateCheckUrl =
            "https://gist.githubusercontent.com/ThoNohT/3181561f8148fb6b865f88714e975154/raw/nohboard_version.json";

        /// <summary>
        /// Hardened, fire-and-forget background update check. Honors the
        /// <see cref="GlobalSettings.CheckForUpdates"/> toggle, applies a short HTTP timeout so
        /// offline users don't pay startup latency, and routes all exceptions through
        /// <see cref="Log"/> instead of leaving them as unobserved task faults.
        /// </summary>
        public void StartUpdateCheck()
        {
            if (GlobalSettings.Settings is { CheckForUpdates: false })
            {
                Log.Info("Update check skipped (GlobalSettings.CheckForUpdates is false).");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    await using var stream = await client.GetStreamAsync(UpdateCheckUrl).ConfigureAwait(false);
                    using var reader = JsonReaderWriterFactory.CreateJsonReader(
                        stream,
                        Encoding.UTF8,
                        XmlDictionaryReaderQuotas.Max,
                        dictionaryReader => { });

                    var serializer = new DataContractJsonSerializer(typeof(VersionInfo));
                    var versionInfo = (VersionInfo)serializer.ReadObject(reader);

                    if ((versionInfo.Major > Version.Major) ||
                        (versionInfo.Major == Version.Major && versionInfo.Minor > Version.Minor) ||
                        (versionInfo.Major == Version.Major && versionInfo.Minor == Version.Minor
                         && versionInfo.Patch > Version.Patch))
                    {
                        this.latestVersion = versionInfo;
                        Log.Info($"Update check: newer version available -> {versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Patch}.");
                    }
                    else
                    {
                        Log.Info("Update check: up to date.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Update check failed (network/firewall/timeout?).", ex);
                }
            });
        }

        #endregion Version check

        #region Keyboard loading and saving

        /// <summary>
        /// Loads the keyboard currently defined in the settings.
        /// </summary>
        /// <returns>The list of fonts that are not present on this system and might be downloaded for this keyboard.
        /// </returns>
        private List<SerializableFont> LoadKeyboard()
        {
            if (GlobalSettings.CurrentDefinition == null)
            {
                HookManager.DisableKeyboardHook();
                HookManager.DisableMouseHook();

                return new List<SerializableFont>();
            }
            // Enable the mouse hook only if there are mouse keys on the screen.
            if (GlobalSettings.CurrentDefinition.Elements.Any(x => !(x is KeyboardKeyDefinition)))
                HookManager.EnableMouseHook();
            else
                HookManager.DisableMouseHook();

            // Enable the keyboard hook only if there are keyboard keys on the screen.
            if (GlobalSettings.CurrentDefinition.Elements.Any(x => x is KeyboardKeyDefinition))
                HookManager.EnableKeyboardHook();
            else
                HookManager.DisableKeyboardHook();

            //Prompt to download any fonts we don't have yet.
            var missingFonts = this.CheckMissingFonts();

            GlobalSettings.Settings.InitUndoHistory();

            // Reset all edit mode related fields, as we should be no longer in edit mode.
            if (this.mnuToggleEditMode.Checked)
            {
                this.mnuToggleEditMode.Checked = false;
                this.mnuToggleEditMode_Click(null, null);
            }

            this.currentlyManipulating = null;
            this.highlightedDefinition = null;
            this.selectedDefinition = null;

            this.ClientSize = new Size(GlobalSettings.CurrentDefinition.Width, GlobalSettings.CurrentDefinition.Height);

            this.ResetBackBrushes();
            this.UpdateEditStatusStrip();

            return missingFonts;
        }

        /// <summary>
        /// Checks if there are any fonts that are not on the system, and do have a download link in them. If this is
        /// the case, then those fonts are returned.
        /// </summary>
        /// <returns>The list of fonts that are not present and might be downloaded. Missing fonts without a download
        /// link are also returned.</returns>
        private List<SerializableFont> CheckMissingFonts()
        {
            var style = GlobalSettings.CurrentStyle;
            var usedFonts = style.ElementStyles.Values.OfType<KeyStyle>()
                .SelectMany(s => new[] { s.Loose?.Font, s.Pressed?.Font })
                .Union(new[] { style.DefaultKeyStyle?.Loose?.Font, style.DefaultKeyStyle?.Pressed?.Font })
                .Where(f => f != null).ToList();

            var installedFonts = new InstalledFontCollection();
            var installedFontFamilyNames = new HashSet<string>(installedFonts.Families.Select(f => f.Name));
            var notInstalledUsedFonts = usedFonts.Where(f => !installedFontFamilyNames.Contains(f.FontFamily)).ToList();

            foreach (var font in notInstalledUsedFonts)
            {
                // For now, update the used family to the default. Next time they could have downloaded the font.
                font.AlternateFontFamily = SystemFonts.DefaultFont.FontFamily.Name;
            }

            if (!notInstalledUsedFonts.Any()) return new List<SerializableFont>();

            return notInstalledUsedFonts.OrderBy(f => f.DownloadUrl == null).Distinct(new SerializableFont.FamilyComparer()).ToList();
        }

        /// <summary>
        /// Redraws the back-brushes. These back-brushes are drawn for all possible states of the keys when none of them
        /// are pressed. This prevents having to render each of these keys every time. However, every time anything
        /// about the definition or style changes, the back-brushes have to be re-rendered.
        /// </summary>
        private void ResetBackBrushes()
        {
            GlobalSettings.StyleDependencyCounter++;

            foreach (var brush in this.backBrushes)
            {
                foreach (var b in brush.Value)
                    b.Value.Dispose();

                brush.Value.Clear();
            }
            this.backBrushes.Clear();

            // Fill the back-brushes.
            foreach (var shift in new[] { false, true })
            {
                this.backBrushes.Add(shift, new Dictionary<bool, Brush>());

                foreach (var caps in new[] { false, true })
                {
                    var bmp = new Bitmap(
                        GlobalSettings.CurrentDefinition.Width,
                        GlobalSettings.CurrentDefinition.Height);
                    var g = Graphics.FromImage(bmp);

                    // Render the background image if set.
                    var cs = GlobalSettings.CurrentStyle;
                    if (cs.BackgroundImageFileName != null && FileHelper.StyleImageExists(cs.BackgroundImageFileName))
                    {
                        g.DrawImage(ImageCache.Get(cs.BackgroundImageFileName), this.ClientRectangle);
                    }

                    // Render the individual keys.
                    foreach (var def in GlobalSettings.CurrentDefinition.Elements)
                    {
                        if (def is KeyboardKeyDefinition) ((KeyboardKeyDefinition)def).Render(g, false, shift, caps);

                        if (def is MouseKeyDefinition) ((MouseKeyDefinition)def).Render(g, false, shift, caps);

                        if (def is MouseScrollDefinition) ((MouseScrollDefinition)def).Render(g, 0);

                        // No need to render mouse speed indicators in backbrush.
                    }

                    this.backBrushes[shift].Add(caps, new TextureBrush(bmp));
                }
            }

            this.Refresh();
        }

        /// <summary>
        /// Opens the load keyboard form.
        /// </summary>
        private void mnuLoadKeyboard_Click(object sender, EventArgs e)
        {
            if (GlobalSettings.UnsavedDefinitionChanges || GlobalSettings.UnsavedStyleChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Loading a new keyboard will undo them. Are you sure you want to load a new keyboard?",
                    "Discard changes",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.OK) return;
            }

            this.menuOpen = false;

            using (var manageForm = new LoadKeyboardForm())
            {
                manageForm.DefinitionChanged += (kbDef, kbStyle, globalStyle) =>
                {
                    var backupDef = GlobalSettings.CurrentDefinition;
                    var backupStyle = GlobalSettings.CurrentStyle;

                    var backupCat = GlobalSettings.Settings.LoadedCategory;
                    var backupKb = GlobalSettings.Settings.LoadedKeyboard;
                    var backupKbStyle = GlobalSettings.Settings.LoadedStyle;
                    var backupkbGlobalStyle = GlobalSettings.Settings.LoadedGlobalStyle;

                    // Don't worry about undo history, it will be initialized alter in LoadKeyboard.
                    GlobalSettings.Settings.UpdateDefinition(kbDef, false);
                    GlobalSettings.Settings.UpdateStyle(kbStyle ?? new KeyboardStyle(), false);

                    GlobalSettings.Settings.LoadedCategory = kbDef.Category;
                    GlobalSettings.Settings.LoadedKeyboard = kbDef.Name;
                    GlobalSettings.Settings.LoadedStyle = kbStyle?.Name;
                    GlobalSettings.Settings.LoadedGlobalStyle = globalStyle;

                    try
                    {
                        var missingFonts = this.LoadKeyboard();
                        manageForm.ToggleFontsPanel(missingFonts);
                    }
                    catch (Exception ex)
                    {
                        GlobalSettings.Settings.UpdateDefinition(backupDef, false);
                        GlobalSettings.Settings.UpdateStyle(backupStyle, false);

                        GlobalSettings.Settings.LoadedCategory = backupCat;
                        GlobalSettings.Settings.LoadedKeyboard = backupKb;
                        GlobalSettings.Settings.LoadedStyle = backupKbStyle;
                        GlobalSettings.Settings.LoadedGlobalStyle = backupkbGlobalStyle;

                        this.LoadKeyboard();

                        ErrorReporter.Show(
                            "Could not load keyboard",
                            "Reverted to the previous keyboard.",
                            ex);
                    }
                };

                manageForm.ShowDialog(this);
            }
        }

        /// <summary>
        /// Saves the current definition under its default name.
        /// </summary>
        private void mnuSaveDefinitionAsName_Click(object sender, EventArgs e)
        {
            this.menuOpen = false;

            GlobalSettings.CurrentDefinition.Save();
            GlobalSettings.Settings.LoadedCategory = GlobalSettings.CurrentDefinition.Category;
            GlobalSettings.Settings.LoadedKeyboard = GlobalSettings.CurrentDefinition.Name;
        }

        /// <summary>
        /// Opens a form the save the current definition under a custom name.
        /// </summary>
        private void mnuSaveDefinitionAs_Click(object sender, EventArgs e)
        {
            this.menuOpen = false;

            using (var saveForm = new SaveKeyboardAsForm())
            {
                saveForm.ShowDialog(this);
            }
        }

        #endregion Keyboard loading and saving

        #region Settings

        /// <summary>
        /// Handles the loading of the form, all settings are read, hooks are created and the keyboard is initialized.
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Track whether the settings file existed BEFORE Load() ran so we can detect a first
            // launch and (optionally) auto-load the bundled default keyboard.
            var wasFirstRun = !File.Exists(AppPaths.SettingsPath);

            // Load the settings.
            if (!GlobalSettings.Load())
            {
                ErrorReporter.Show(
                    "Failed to load settings",
                    $"NohBoard could not read its settings file at {AppPaths.SettingsPath}. " +
                    "Starting with defaults.",
                    new Exception(GlobalSettings.Errors));
            }

            // Persist the resolved set of locations on every launch - users frequently ask "where
            // does NohBoard keep its files" and this means the log on disk always knows.
            Log.Info($"First run: {wasFirstRun}. LoadedCategory={GlobalSettings.Settings.LoadedCategory}, " +
                $"LoadedKeyboard={GlobalSettings.Settings.LoadedKeyboard}, LoadedStyle={GlobalSettings.Settings.LoadedStyle}");

            this.Location = new Point(GlobalSettings.Settings.X, GlobalSettings.Settings.Y);
            var title = GlobalSettings.Settings.WindowTitle;

            this.Text = string.IsNullOrWhiteSpace(title) ? $"NohBoard {Version.Get}" : title;

            this.StartUpdateCheck();

            // First-run convenience: if no keyboard was previously loaded and the bundled
            // default exists, auto-load it so the new user immediately sees something.
            if (wasFirstRun
                && GlobalSettings.Settings.AutoLoadDefaultOnFirstRun
                && GlobalSettings.Settings.LoadedCategory == null
                && GlobalSettings.Settings.LoadedKeyboard == null
                && FileHelper.AnyKbsExists("Normal", "us_intl", Constants.DefinitionFilename))
            {
                Log.Info("First run detected, auto-loading Normal/us_intl.");
                GlobalSettings.Settings.LoadedCategory = "Normal";
                GlobalSettings.Settings.LoadedKeyboard = "us_intl";
                GlobalSettings.Settings.AutoLoadDefaultOnFirstRun = false;
            }

            // Load a definition if possible.
            if (GlobalSettings.Settings.LoadedKeyboard != null && GlobalSettings.Settings.LoadedCategory != null)
            {
                try
                {
                    GlobalSettings.Settings.UpdateDefinition(KeyboardDefinition
                        .Load(GlobalSettings.Settings.LoadedCategory, GlobalSettings.Settings.LoadedKeyboard), false);
                    Log.Info($"Loaded keyboard definition {GlobalSettings.Settings.LoadedCategory}/" +
                        $"{GlobalSettings.Settings.LoadedKeyboard}.");
                }
                catch (Exception ex)
                {
                    ErrorReporter.Show(
                        "Could not load keyboard",
                        "There was an error loading the saved keyboard definition file.",
                        ex);
                    GlobalSettings.Settings.LoadedCategory = null;
                    GlobalSettings.Settings.LoadedKeyboard = null;
                }
            }

            // Load a style if possible.
            if (GlobalSettings.CurrentDefinition != null && GlobalSettings.Settings.LoadedStyle != null)
            {
                try
                {
                    GlobalSettings.Settings.UpdateStyle(KeyboardStyle.Load(
                        GlobalSettings.Settings.LoadedStyle,
                        GlobalSettings.Settings.LoadedGlobalStyle), false);
                    this.LoadKeyboard();
                    this.ResetBackBrushes();
                    Log.Info($"Loaded style {GlobalSettings.Settings.LoadedStyle} " +
                        $"(global={GlobalSettings.Settings.LoadedGlobalStyle}).");
                }
                catch (Exception ex)
                {
                    var failedStyleName = GlobalSettings.Settings.LoadedStyle;
                    GlobalSettings.Settings.LoadedStyle = null;
                    ErrorReporter.Show(
                        "Could not load style",
                        $"Failed to load style {failedStyleName}, loading default style.",
                        ex);
                }

                // Enable the mouse/keyboard hooks based on the loaded definition. We do this
                // explicitly (rather than relying on LoadKeyboard) so we can surface install
                // failures via the new error reporter.
                if (GlobalSettings.CurrentDefinition.Elements.Any(x => !(x is KeyboardKeyDefinition)))
                    this.SafelyEnableMouseHook();

                if (GlobalSettings.CurrentDefinition.Elements.Any(x => x is KeyboardKeyDefinition))
                    this.SafelyEnableKeyboardHook();
            }

            // Larger default window for the empty-state hint when no keyboard is loaded.
            if (GlobalSettings.CurrentDefinition == null)
            {
                this.ClientSize = new Size(480, 240);
            }

            this.UpdateTimer.Interval = GlobalSettings.Settings.UpdateInterval;
            this.UpdateTimer.Enabled = true;
            this.KeyCheckTimer.Enabled = true;

            this.Activate();
            this.ApplySettings();
            Log.Info($"MainForm loaded. KeyboardHookInstalled={HookManager.KeyboardHookInstalled}, " +
                $"MouseHookInstalled={HookManager.MouseHookInstalled}.");
        }

        /// <summary>
        /// Installs the keyboard hook, surfacing any Win32 error via <see cref="ErrorReporter"/>
        /// instead of bubbling an unhandled exception.
        /// </summary>
        private void SafelyEnableKeyboardHook()
        {
            try
            {
                HookManager.EnableKeyboardHook();
                Log.Info("Keyboard hook installed.");
            }
            catch (Exception ex)
            {
                Log.Error("EnableKeyboardHook failed.", ex);
                ErrorReporter.Show(
                    "Could not install keyboard hook",
                    "NohBoard could not subscribe to global keyboard events. Key presses will not be shown.",
                    ex);
            }
        }

        /// <summary>
        /// Installs the mouse hook, surfacing any Win32 error via <see cref="ErrorReporter"/>
        /// instead of bubbling an unhandled exception.
        /// </summary>
        private void SafelyEnableMouseHook()
        {
            try
            {
                HookManager.EnableMouseHook();
                Log.Info("Mouse hook installed.");
            }
            catch (Exception ex)
            {
                Log.Error("EnableMouseHook failed.", ex);
                ErrorReporter.Show(
                    "Could not install mouse hook",
                    "NohBoard could not subscribe to global mouse events. Mouse buttons will not be shown.",
                    ex);
            }
        }

        /// <summary>
        /// Handles the moving of the form. Stores the current position for future use.
        /// </summary>
        private void MainForm_Move(object sender, EventArgs e)
        {
            if (GlobalSettings.Settings != null && this.WindowState == FormWindowState.Normal)
            {
                GlobalSettings.Settings.X = this.Location.X;
                GlobalSettings.Settings.Y = this.Location.Y;
            }
        }

        /// <summary>
        /// Handles the closing of the form. Hooks are disabled and the settings are saved before closing.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ((GlobalSettings.UnsavedDefinitionChanges || GlobalSettings.UnsavedStyleChanges) && !CrashHandler.Crashed)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. If you exit now you will lose them. Are you sure you want to exit?",
                    "Discard changes",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
            }

            HookManager.DisableMouseHook();
            HookManager.DisableKeyboardHook();

            GlobalSettings.Save();
        }

        /// <summary>
        /// Applies the currently stored settings.
        /// </summary>
        private void ApplySettings()
        {
            HookManager.TrapKeyboard = GlobalSettings.Settings.TrapKeyboard;
            HookManager.TrapMouse = GlobalSettings.Settings.TrapMouse;
            HookManager.TrapToggleKeyCode = GlobalSettings.Settings.TrapToggleKeyCode;
            HookManager.ScrollHold = GlobalSettings.Settings.ScrollHold;
            HookManager.PressHold = GlobalSettings.Settings.PressHold;

            var title = GlobalSettings.Settings.WindowTitle;
            this.Text = string.IsNullOrWhiteSpace(title) ? $"NohBoard {Version.Get}" : title;

            this.LoadKeyboard();
        }

        /// <summary>
        /// Opens the settings form.
        /// </summary>
        private void mnuSettings_Click(object sender, EventArgs e)
        {
            this.menuOpen = false;

            using (var settingsForm = new SettingsForm())
            {
                var result = settingsForm.ShowDialog(this);

                if (result == DialogResult.Cancel)
                    return;

                // Re-initialize with the new settings.
                this.ApplySettings();
            }
        }

        /// <summary>
        /// Populates the main menu. Elements are visible based on which definitions and styles are loaded, and whether
        /// actions on a specifically pointed element are possible.
        /// </summary>
        private void MainMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.menuOpen = true;

            this.mnuSaveDefinition.Enabled = GlobalSettings.CurrentDefinition != null;
            if (GlobalSettings.CurrentDefinition != null)
            {
                this.mnuSaveDefinitionAsName.Text =
                    $"Save &To '{GlobalSettings.CurrentDefinition.Category}/{GlobalSettings.CurrentDefinition.Name}'";

                var mousePos = this.PointToClient(Cursor.Position);
                this.elementUnderCursor =
                    GlobalSettings.CurrentDefinition.Elements.FirstOrDefault(x => x.Inside(mousePos));

                // Set the highlighted definition only if we're in edit mode, and there is not an already selected definition.
                if (this.mnuToggleEditMode.Checked && this.selectedDefinition == null)
                {
                    this.highlightedDefinition = this.elementUnderCursor;
                    this.highlightedDefinition?.StartManipulating(mousePos, false);
                }

                var relevantElement = this.selectedDefinition ?? this.elementUnderCursor;
                this.mnuEditElementStyle.Enabled = relevantElement != null;
                this.mnuElementProperties.Enabled = relevantElement != null;
            }

            // Only allow editing of properties/styles in edit mode.
            this.mnuKeyboardProperties.Visible = this.mnuToggleEditMode.Checked;
            this.mnuUpdateTextPosition.Visible = this.mnuToggleEditMode.Checked;
            this.mnuElementProperties.Visible = this.mnuToggleEditMode.Checked;
            this.mnuEditKeyboardStyle.Visible = this.mnuToggleEditMode.Checked;
            this.mnuEditElementStyle.Visible = this.mnuToggleEditMode.Checked;
            this.MainMenuSep1.Visible = this.mnuToggleEditMode.Checked;

            this.mnuSaveStyleToName.Text = $"Save &To '{GlobalSettings.CurrentStyle.Name}'";
            this.mnuSaveStyleToName.Visible = !GlobalSettings.Settings.LoadedGlobalStyle;
            this.mnuSaveToGlobalStyleName.Text = $"Save To &Global '{GlobalSettings.CurrentStyle.Name}'";
            this.mnuSaveToGlobalStyleName.Enabled = GlobalSettings.CurrentStyle.IsGlobal;
            this.mnuSaveToGlobalStyleName.Visible = GlobalSettings.Settings.LoadedGlobalStyle;

            this.mnuToggleEditMode.Enabled = GlobalSettings.CurrentDefinition != null;

            if (this.latestVersion != null && !this.mnuUpdate.Visible)
            {
                this.mnuUpdate.Text = $"New version available: {this.latestVersion.Format()}.";
                this.mnuUpdate.Visible = true;
                this.mnuUpdate.Click += (s, ea) => { Process.Start(new ProcessStartInfo { FileName = "https://github.com/ThoNohT/NohBoard/releases", UseShellExecute = true }); };
            }

            this.mnuMoveElement.Visible = this.relevantDefinition != null;

            var highlightedSomething = this.mnuToggleEditMode.Checked && this.relevantDefinition != null;

            // Edit mode related menu items.
            this.mnuAddBoundaryPoint.Visible = highlightedSomething &&
                this.relevantDefinition.RelevantManipulation.Type == ElementManipulationType.MoveEdge;

            this.mnuRemoveBoundaryPoint.Visible = highlightedSomething &&
                this.relevantDefinition.RelevantManipulation.Type == ElementManipulationType.MoveBoundary;

            this.mnuRemoveElement.Visible = highlightedSomething;
            this.mnuAddElement.Visible = this.mnuToggleEditMode.Checked && this.relevantDefinition == null;
        }

        /// <summary>
        /// Handles setting the menu open variable to false when the form loses focus.
        /// </summary>
        private void MainForm_Deactivate(object sender, EventArgs e)
        {
            // Deactivating the form also closes the menu.
            this.menuOpen = false;
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void mnuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #endregion Settings

        #region Rendering

        /// <summary>
        /// Paints the keyboard on the screen.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(GlobalSettings.CurrentStyle.BackgroundColor);

            if (GlobalSettings.CurrentDefinition == null || !this.backBrushes.Any())
            {
                this.DrawEmptyStateHint(e.Graphics);
                return;
            }

            // Fill the appropriate back brush.
            e.Graphics.FillRectangle(
                this.backBrushes[KeyboardState.ShiftDown][KeyboardState.CapsActive],
                new Rectangle(0, 0, GlobalSettings.CurrentDefinition.Width, GlobalSettings.CurrentDefinition.Height));

            // Render all keys.
            KeyboardState.CheckKeyHolds(GlobalSettings.Settings.PressHold);
            var kbKeys = KeyboardState.PressedKeys;
            var mouseKeys = MouseState.PressedKeys.Select(k => (int)k).ToList();
            MouseState.CheckKeyHolds(GlobalSettings.Settings.PressHold);
            MouseState.CheckScrollAndMovement();
            var scrollCounts = MouseState.ScrollCounts;
            var allDefs = GlobalSettings.CurrentDefinition.Elements;
            foreach (var def in allDefs)
            {
                this.Render(e.Graphics, def, allDefs, kbKeys, mouseKeys, scrollCounts, false);
            }

            // Draw the element being manipulated
            if (this.currentlyManipulating == null)
            {
                if (this.highlightedDefinition != this.selectedDefinition)
                    // Draw highlighted only if it is not also selected.
                    this.highlightedDefinition?.RenderHighlight(e.Graphics);

                if (this.selectedDefinition != null)
                {
                    this.Render(e.Graphics, this.selectedDefinition, allDefs, kbKeys, mouseKeys, scrollCounts, true);
                    this.selectedDefinition.RenderSelected(e.Graphics);
                }
            }
            else
            {
                this.currentlyManipulating.Value.definition.RenderEditing(e.Graphics);
            }

            base.OnPaint(e);
        }

        /// <summary>
        /// Renders a single element definition.
        /// </summary>
        /// <param name="g">The GDI+ surface to render on.</param>
        /// <param name="def">The element definition to render.</param>
        /// <param name="allDefs">The list of all element definition.</param>
        /// <param name="kbKeys">The list of keyboard keys that are pressed.</param>
        /// <param name="mouseKeys">The list of mouse keys that are pressed.</param>
        /// <param name="scrollCounts">The list of scroll counts.</param>
        /// <param name="scrollCounts">If <c>true</c>, the key will always render, regardless of whether it is
        /// different from the background.</param>
        private void Render(
            Graphics g,
            ElementDefinition def,
            List<ElementDefinition> allDefs,
            IReadOnlyList<int> kbKeys,
            List<int> mouseKeys,
            IReadOnlyList<int> scrollCounts,
            bool alwaysRender)
        {
            if (def is KeyboardKeyDefinition kkDef)
            {
                var pressed = true;
                if (!kkDef.KeyCodes.Any() || !kkDef.KeyCodes.All(kbKeys.Contains)) pressed = false;

                if (kkDef.KeyCodes.Count == 1
                    && allDefs.OfType<KeyboardKeyDefinition>()
                        .Any(d => d.KeyCodes.Count > 1
                        && d.KeyCodes.All(kbKeys.Contains)
                        && d.KeyCodes.ContainsAll(kkDef.KeyCodes))) pressed = false;

                if (!pressed && !alwaysRender) return;

                kkDef.Render(g, pressed, KeyboardState.ShiftDown, KeyboardState.CapsActive);
            }
            if (def is MouseKeyDefinition mkDef)
            {
                var pressed = mouseKeys.Contains(mkDef.KeyCodes.Single());
                if (pressed || alwaysRender)
                    mkDef.Render(g, pressed, KeyboardState.ShiftDown, KeyboardState.CapsActive);
            }
            if (def is MouseScrollDefinition msDef)
            {
                var scrollCount = scrollCounts[msDef.KeyCodes.Single()];
                if (scrollCount > 0 || alwaysRender) msDef.Render(g, scrollCount);
            }
            if (def is MouseSpeedIndicatorDefinition)
            {
                ((MouseSpeedIndicatorDefinition)def).Render(g, MouseState.AverageSpeed);
            }
        }

        /// <summary>
        /// Draws the first-launch hint that explains where to click to load a keyboard.
        /// Visible only when no keyboard definition is currently loaded.
        /// </summary>
        private void DrawEmptyStateHint(Graphics g)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var titleFont = new Font(SystemFonts.DefaultFont.FontFamily, 14f, FontStyle.Bold);
            using var bodyFont = new Font(SystemFonts.DefaultFont.FontFamily, 10f, FontStyle.Regular);
            using var brush = new SolidBrush(InvertForContrast(GlobalSettings.CurrentStyle.BackgroundColor));

            var titleText = $"NohBoard {Version.Get}";
            const string bodyText = "Right-click anywhere to load a keyboard.";

            var titleSize = g.MeasureString(titleText, titleFont);
            var bodySize = g.MeasureString(bodyText, bodyFont);

            var totalHeight = titleSize.Height + 6f + bodySize.Height;
            var startY = (this.ClientSize.Height - totalHeight) / 2f;
            var titleX = (this.ClientSize.Width - titleSize.Width) / 2f;
            var bodyX = (this.ClientSize.Width - bodySize.Width) / 2f;

            g.DrawString(titleText, titleFont, brush, titleX, startY);
            g.DrawString(bodyText, bodyFont, brush, bodyX, startY + titleSize.Height + 6f);
        }

        /// <summary>
        /// Picks black or white text depending on the brightness of <paramref name="background"/>
        /// so the empty-state hint stays readable regardless of the current style.
        /// </summary>
        private static Color InvertForContrast(Color background)
        {
            var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
            return luminance > 0.5 ? Color.Black : Color.White;
        }

        /// <summary>
        /// Forces an update if any of the key or mouse states have changed.
        /// </summary>
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (KeyboardState.Updated || MouseState.Updated)
                this.Refresh();
        }

        /// <summary>
        /// Periodically checks that no keys got stuck.
        /// </summary>
        private void KeyCheckTimer_Tick(object sender, EventArgs e)
        {
            MouseState.CheckKeys(GlobalSettings.Settings.PressHold);
            KeyboardState.CheckKeys(GlobalSettings.Settings.PressHold);
        }

        #endregion Rendering

        /// <summary>
        /// Crashes NohBoard, in order to generate a crash log.
        /// This seems strange, but can be an easy way for someone to serialize all their settings into a single file
        /// to be sent for support reasons.
        /// </summary>
        private void mnuGenerateLog_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("This will crash NohBoard in order to generate a log, are you sure you want to do this?", "Generate crash log", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                throw new Exception("A crash log was requested.");
            }
        }

        #region Help menu

        /// <summary>
        /// Opens the user data folder in Explorer.
        /// </summary>
        private void mnuHelpOpenDataFolder_Click(object sender, EventArgs e)
        {
            OpenFolder(AppPaths.UserDataDir);
        }

        /// <summary>
        /// Opens the logs folder in Explorer.
        /// </summary>
        private void mnuHelpOpenLogsFolder_Click(object sender, EventArgs e)
        {
            OpenFolder(AppPaths.LogsDir);
        }

        /// <summary>
        /// Shows the About dialog with version, runtime, paths, and a "copy last log lines" button.
        /// </summary>
        private void mnuHelpAbout_Click(object sender, EventArgs e)
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        }

        /// <summary>
        /// Opens <paramref name="path"/> in the OS file manager, creating it first if necessary.
        /// Failures are logged but never thrown.
        /// </summary>
        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to open folder {path}.", ex);
                ErrorReporter.Show("Could not open folder", $"NohBoard tried to open {path} but failed.", ex);
            }
        }

        #endregion Help menu

        #region Edit status strip

        /// <summary>
        /// Updates the visibility and content of the edit-mode status strip based on the
        /// current toggle. Hidden in normal capture mode so OBS scenes are not affected.
        /// </summary>
        private void UpdateEditStatusStrip()
        {
            if (this.EditStatusStrip == null || this.EditStatusLabel == null) return;

            var on = this.mnuToggleEditMode != null && this.mnuToggleEditMode.Checked;
            this.EditStatusStrip.Visible = on;

            if (!on) return;

            var s = GlobalSettings.Settings;
            var kbLabel = (s.LoadedCategory != null && s.LoadedKeyboard != null)
                ? $"{s.LoadedCategory}/{s.LoadedKeyboard}"
                : "(no keyboard loaded)";
            var styleLabel = string.IsNullOrEmpty(s.LoadedStyle)
                ? "default style"
                : (s.LoadedGlobalStyle ? $"global:{s.LoadedStyle}" : s.LoadedStyle);

            this.EditStatusLabel.Text = $"Edit mode    |    {kbLabel}    |    {styleLabel}";
        }

        #endregion Edit status strip
    }
}