using ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RiivolutionIsoBuilder
{
    // Ventana principal: es el mismo motor de parcheo del proyecto original (RiivoDisc, Dolpatcher,
    // Extensions y wit.exe no se tocan), solo que ahora todo se controla con una interfaz gráfica
    // en vez de la consola. La consola sigue "existiendo" por dentro (wit.exe es un programa de
    // consola), pero su salida se redirige al cuadro de texto de abajo.
    public class MainForm : Form
    {
        // --- Controles principales ---
        TextBox txtIso, txtXml, txtOut, txtTitleId, txtGameName;
        Button btnBrowseIso, btnBrowseXml, btnBrowseOut, btnBuild;
        FlowLayoutPanel pnlOptions;
        RichTextBox txtLog;
        ProgressBar progressBar;
        CheckBox chkKeepExtracted, chkIgnoreWarnings, chkIgnoreErrors;
        Label lblStatus;

        // --- Estado del mod cargado ---
        RiivoDisc disc;
        string gameID = "", region = "", maker = "";
        // Cada entrada: la Option del XML + el control (ComboBox de varias opciones, o CheckBox de una sola)
        class OptionBinding
        {
            public Option Option;
            public Control Control;
            public OptionBinding(Option o, Control c) { Option = o; Control = c; }
        }
        List<OptionBinding> optionControls = new List<OptionBinding>();

        public MainForm()
        {
            BuildUi();
            Console.SetOut(new LogWriter(this));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* si no hay icono, se usa el genérico de Windows */ }
        }

        // Redirige todo lo que el motor (RiivoDisc/Dolpatcher/wit.exe) escriba con Console.WriteLine
        // hacia el cuadro de log de la ventana, sin tener que tocar ese código.
        class LogWriter : System.IO.TextWriter
        {
            MainForm form;
            public LogWriter(MainForm f) { form = f; }
            public override Encoding Encoding => Encoding.UTF8;
            public override void Write(char value) { }
            public override void WriteLine(string value) { form.Log(value ?? ""); }
            public override void Write(string value) { if (!string.IsNullOrEmpty(value)) form.Log(value); }
        }

        public void Log(string text)
        {
            if (txtLog.InvokeRequired) { txtLog.Invoke(new Action(() => Log(text))); return; }
            txtLog.AppendText(text + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        void SetStatus(string text)
        {
            if (lblStatus.InvokeRequired) { lblStatus.Invoke(new Action(() => SetStatus(text))); return; }
            lblStatus.Text = text;
        }

        void SetProgress(int value)
        {
            if (progressBar.InvokeRequired) { progressBar.Invoke(new Action(() => SetProgress(value))); return; }
            progressBar.Value = Math.Max(0, Math.Min(100, value));
        }

        void SetBusy(bool busy)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetBusy(busy))); return; }
            btnBuild.Enabled = !busy;
            btnBrowseIso.Enabled = !busy;
            btnBrowseXml.Enabled = !busy;
            btnBrowseOut.Enabled = !busy;
            pnlOptions.Enabled = !busy;
        }

        // ============================= INTERFAZ =============================
        static readonly Color BgColor = Color.FromArgb(245, 246, 248);
        static readonly Color AccentColor = Color.FromArgb(0, 99, 177);

        void BuildUi()
        {
            Text = "Riivolution ISO Builder";
            Width = 860;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(760, 620);
            BackColor = BgColor;
            Padding = new Padding(12);

            // --- Encabezado ---
            var header = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(2, 0, 0, 0) };
            var lblTitle = new Label
            {
                Text = "Riivolution ISO Builder",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                Top = 0,
                Left = 0
            };
            var lblSubtitle = new Label
            {
                Text = "Patch Wii ISOs with Riivolution mods",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(110, 110, 110),
                AutoSize = true,
                Top = 26,
                Left = 2
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);

            // --- Grupo: archivos ---
            var grpFiles = new GroupBox { Text = "Files", Dock = DockStyle.Top, Height = 140, Padding = new Padding(10, 8, 10, 10) };
            var fileGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            for (int i = 0; i < 3; i++) fileGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));

            txtIso = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 6, 6, 6) };
            btnBrowseIso = new Button { Text = "Browse...", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 2, 4) };
            btnBrowseIso.Click += (s, e) => BrowseFile(txtIso, "ISO/WBFS files|*.iso;*.wbfs|All files|*.*");

            txtXml = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 6, 6, 6) };
            btnBrowseXml = new Button { Text = "Browse...", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 2, 4) };
            btnBrowseXml.Click += (s, e) =>
            {
                if (BrowseFile(txtXml, "Riivolution XML|*.xml|All files|*.*"))
                    LoadXml(txtXml.Text);
            };

            txtOut = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 6, 6, 6) };
            btnBrowseOut = new Button { Text = "Save as...", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 2, 4) };
            btnBrowseOut.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog { Filter = "ISO|*.iso|WBFS|*.wbfs|All files|*.*", DefaultExt = "wbfs" })
                {
                    if (dlg.ShowDialog() == DialogResult.OK) txtOut.Text = dlg.FileName;
                }
            };

            fileGrid.Controls.Add(FieldLabel("ISO/WBFS to patch:"), 0, 0);
            fileGrid.Controls.Add(txtIso, 1, 0);
            fileGrid.Controls.Add(btnBrowseIso, 2, 0);
            fileGrid.Controls.Add(FieldLabel("Riivolution XML (from mod):"), 0, 1);
            fileGrid.Controls.Add(txtXml, 1, 1);
            fileGrid.Controls.Add(btnBrowseXml, 2, 1);
            fileGrid.Controls.Add(FieldLabel("Save result to:"), 0, 2);
            fileGrid.Controls.Add(txtOut, 1, 2);
            fileGrid.Controls.Add(btnBrowseOut, 2, 2);
            grpFiles.Controls.Add(fileGrid);

            // --- Grupo: opciones del mod ---
            var grpOptions = new GroupBox { Text = "Mod Options", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10), Margin = new Padding(0, 10, 0, 10) };
            pnlOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                WrapContents = false,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            var lblOptionsHint = new Label
            {
                Text = "First select an XML file to see the available options.",
                AutoSize = true,
                ForeColor = Color.FromArgb(130, 130, 130),
                Padding = new Padding(8)
            };
            pnlOptions.Controls.Add(lblOptionsHint);
            grpOptions.Controls.Add(pnlOptions);

            // --- Grupo: ajustes avanzados ---
            var grpAdvanced = new GroupBox { Text = "Advanced Settings (optional)", Dock = DockStyle.Top, Height = 118, Padding = new Padding(10, 8, 10, 8) };
            var advGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var titlePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            titlePanel.Controls.Add(FieldLabel("New TitleID (6 characters, use dots to keep original):"));
            txtTitleId = new TextBox { Width = 150, MaxLength = 6, Margin = new Padding(2, 2, 0, 0) };
            titlePanel.Controls.Add(txtTitleId);

            var namePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            namePanel.Controls.Add(FieldLabel("Game name (as it will appear in the menu):"));
            txtGameName = new TextBox { Width = 320, Margin = new Padding(2, 2, 0, 0) };
            namePanel.Controls.Add(txtGameName);

            advGrid.Controls.Add(titlePanel, 0, 0);
            advGrid.Controls.Add(namePanel, 1, 0);
            chkKeepExtracted = new CheckBox { Text = "Keep extracted folder after completion", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            advGrid.Controls.Add(chkKeepExtracted, 0, 1);
            advGrid.SetColumnSpan(chkKeepExtracted, 2);
            chkIgnoreWarnings = new CheckBox { Text = "Ignore warnings", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            chkIgnoreErrors = new CheckBox { Text = "Ignore warnings and errors (advanced)", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            advGrid.Controls.Add(chkIgnoreWarnings, 0, 2);
            advGrid.Controls.Add(chkIgnoreErrors, 1, 2);
            grpAdvanced.Controls.Add(advGrid);

            // --- Zona de construcción ---
            var buildPanel = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(0, 10, 0, 4) };
            btnBuild = new Button
            {
                Text = "▶  Build Patched ISO",
                Left = 0,
                Top = 8,
                Width = 260,
                Height = 38,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnBuild.FlatAppearance.BorderSize = 0;
            btnBuild.Click += async (s, e) => await RunBuildAsync();

            lblStatus = new Label
            {
                Text = "Ready.",
                Left = 272,
                Top = 0,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 90, 90)
            };
            progressBar = new ProgressBar { Left = 272, Top = 18, Width = 400, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            buildPanel.Controls.Add(btnBuild);
            buildPanel.Controls.Add(lblStatus);
            buildPanel.Controls.Add(progressBar);

            // --- Registro ---
            var grpLog = new GroupBox { Text = "Log", Dock = DockStyle.Bottom, Height = 150, Padding = new Padding(8) };
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(28, 28, 30),
                ForeColor = Color.FromArgb(210, 210, 210),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F)
            };
            grpLog.Controls.Add(txtLog);

            // Orden de acoplado (Dock): el primer control agregado ("Fill") ocupa lo que sobra
            // después de los demás; entre los que comparten el mismo lado, el añadido más
            // tarde queda más pegado al borde. Por eso el orden real no es de arriba hacia abajo.
            Controls.Add(grpOptions);
            Controls.Add(grpFiles);
            Controls.Add(header);
            Controls.Add(grpAdvanced);
            Controls.Add(buildPanel);
            Controls.Add(grpLog);
        }

        static Label FieldLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(4, 6, 4, 2),
            ForeColor = Color.FromArgb(60, 60, 60)
        };

        bool BrowseFile(TextBox target, string filter)
        {
            using (var dlg = new OpenFileDialog { Filter = filter })
            {
                if (dlg.ShowDialog() == DialogResult.OK) { target.Text = dlg.FileName; return true; }
            }
            return false;
        }

        // ============================= CARGA DEL XML =============================
        void LoadXml(string path)
        {
            pnlOptions.Controls.Clear();
            optionControls.Clear();

            try
            {
                disc = RiivoDisc.ParseString(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not read the mod XML:\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                disc = null;
                return;
            }

            if (disc.sections.Count == 1 && disc.sections[0].options.Count == 1 && disc.sections[0].options[0].choices.Count == 1)
            {
                pnlOptions.Controls.Add(new Label { Text = "This mod has no options to choose from; it will be applied directly.", AutoSize = true, Padding = new Padding(4) });
                return;
            }

            foreach (Section section in disc.sections)
            {
                var box = new GroupBox { Text = section.name, Width = pnlOptions.ClientSize.Width - 30, AutoSize = true, Padding = new Padding(8), Margin = new Padding(6) };
                var inner = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Width = box.Width - 20 };

                foreach (Option option in section.options)
                {
                    if (option.choices.Count > 1)
                    {
                        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false };
                        row.Controls.Add(new Label { Text = option.name + ":", AutoSize = true, Padding = new Padding(0, 6, 8, 0), Width = 220 });
                        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
                        combo.Items.Add("(None)");
                        foreach (Choice c in option.choices) combo.Items.Add(c.name);
                        int def = (int)option.selectedChoice;
                        combo.SelectedIndex = (def >= 0 && def < option.choices.Count) ? def + 1 : 0;
                        row.Controls.Add(combo);
                        inner.Controls.Add(row);
                        optionControls.Add(new OptionBinding(option, combo));
                    }
                    else if (option.choices.Count == 1)
                    {
                        var chk = new CheckBox { Text = option.name + " (" + option.choices[0].name + ")", AutoSize = true, Checked = true };
                        inner.Controls.Add(chk);
                        optionControls.Add(new OptionBinding(option, chk));
                    }
                }

                box.Controls.Add(inner);
                pnlOptions.Controls.Add(box);
            }
        }

        // ============================= CONSTRUCCIÓN =============================
        async Task RunBuildAsync()
        {
            if (string.IsNullOrWhiteSpace(txtIso.Text) || !System.IO.File.Exists(txtIso.Text))
            {
                MessageBox.Show("Select a valid ISO/WBFS file.", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (disc == null || string.IsNullOrWhiteSpace(txtXml.Text) || !System.IO.File.Exists(txtXml.Text))
            {
                MessageBox.Show("Select a valid Riivolution XML file.", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtOut.Text))
            {
                MessageBox.Show("Choose where to save the patched file.", "Missing information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string isoPath = txtIso.Text, xmlPath = txtXml.Text, outPath = txtOut.Text;
            string newTitleID = txtTitleId.Text.Trim();
            string newGameName = txtGameName.Text.Trim();
            bool deleteISO = !chkKeepExtracted.Checked;
            int ignoreLevel = chkIgnoreErrors.Checked ? 2 : (chkIgnoreWarnings.Checked ? 1 : 0);

            if (newTitleID != "" && newTitleID.Length != 6)
            {
                MessageBox.Show("The TitleID must be exactly 6 characters (use dots to keep the originals).", "Invalid TitleID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Reunir los patches elegidos en la interfaz (equivalente a lo que antes se preguntaba por consola)
            List<Patch> patches = new List<Patch>();
            foreach (var binding in optionControls)
            {
                Option option = binding.Option;
                Control control = binding.Control;
                Choice chosen = null;
                ComboBox combo = control as ComboBox;
                CheckBox chk = control as CheckBox;
                if (combo != null)
                {
                    if (combo.SelectedIndex <= 0) continue; // "(None)"
                    chosen = option.choices[combo.SelectedIndex - 1];
                }
                else if (chk != null)
                {
                    if (!chk.Checked) continue;
                    chosen = option.choices[0];
                }
                if (chosen == null) continue;

                foreach (PatchReference patchref in chosen.patchReferences)
                {
                    int idx = FindPatchIndexByName(patchref.id);
                    if (idx < 0)
                    {
                        Log("Patch not found: " + patchref.id);
                        if (ignoreLevel < 1)
                        {
                            MessageBox.Show("Patch \"" + patchref.id + "\" not found. Enable \"Ignore warnings\" if you want to continue anyway.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        continue;
                    }
                    patches.Add(disc.patches[idx]);
                }
            }
            if (disc.sections.Count == 1 && disc.sections[0].options.Count == 1 && disc.sections[0].options[0].choices.Count == 1)
            {
                foreach (PatchReference patchref in disc.sections[0].options[0].choices[0].patchReferences)
                {
                    int idx = FindPatchIndexByName(patchref.id);
                    if (idx >= 0) patches.Add(disc.patches[idx]);
                }
            }

            SetBusy(true);
            txtLog.Clear();
            SetProgress(0);
            SetStatus("Working...");

            try
            {
                bool ok = await Task.Run(() => DoBuild(isoPath, xmlPath, outPath, patches, newTitleID, newGameName, deleteISO, ignoreLevel));
                if (ok)
                {
                    SetStatus("Done!");
                    SetProgress(100);
                    MessageBox.Show("The file was built successfully:\r\n" + outPath, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("Stopped due to an error. Check the log.");
                }
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                SetStatus("An error occurred. Check the log.");
                MessageBox.Show("An unexpected error occurred:\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Es la misma lógica que "doStuff" del Program.cs original (extraer -> copiar archivos/carpetas ->
        // parchear memoria -> reconstruir), solo que ya recibe los patches elegidos y reporta progreso a la GUI
        // en vez de preguntar por consola.
        bool DoBuild(string isoPath, string xmlPath, string outPath, List<Patch> patches, string newTitleID, string newGameName, bool deleteISO, int ignoreLevel)
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string extDir = appDir + Path.GetFileNameWithoutExtension(xmlPath) + "-" + new string(Enumerable.Repeat(chars, 6).Select(c => c[random.Next(c.Length)]).ToArray());
            string rootPath = Path.GetFullPath(Path.Combine(xmlPath, @"..\..\"));

            List<byte> id = new List<byte>();
            List<byte> name = new List<byte>();
            using (FileStream iso = new FileStream(isoPath, FileMode.Open))
            {
                string header = "";
                for (int i = 0; i < 4; i++)
                {
                    char c = (char)iso.ReadByte();
                    header += c;
                    if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                    {
                        Log("ERROR: This file is not a Nintendo Wii ROM");
                        if (ignoreLevel < 2) return false;
                    }
                }
                if (header == "WBFS")
                {
                    iso.Position = 0x200;
                    for (int i = 0; i < 8; i++) id.Add((byte)iso.ReadByte());
                    iso.Position = 0x220;
                    while (true) { byte b = (byte)iso.ReadByte(); if (b == 0) break; name.Add(b); }
                }
                else
                {
                    iso.Position = 0;
                    for (int i = 0; i < 8; i++) id.Add((byte)iso.ReadByte());
                    iso.Position = 0x20;
                    while (true) { byte b = (byte)iso.ReadByte(); if (b == 0) break; name.Add(b); }
                }
            }

            string titleID = Encoding.UTF8.GetString(id.ToArray(), 0, 6);
            string gameName = Encoding.UTF8.GetString(name.ToArray(), 0, name.Count);
            gameID = titleID.Substring(0, 3);
            region = titleID.Substring(3, 1);
            maker = titleID.Substring(4, 2);

            Log("TitleID found: " + titleID + " - Game name: " + gameName);

            if (disc.gameFilter.game != gameID)
            {
                Log("Warning: this mod only applies to the game with TitleID " + disc.gameFilter.game + ". Yours is " + gameID + ".");
                if (ignoreLevel < 1) return false;
            }
            if (disc.gameFilter.regions.Count > 0 && !disc.gameFilter.regions.Contains(region))
            {
                Log("Unsupported region (" + region + ").");
                if (ignoreLevel < 1) return false;
            }
            if (newTitleID == "")
                Log("No new TitleID specified: the mod will use the same save data as the original game.");

            SetStatus("Extracting ISO...");
            SetProgress(10);
            string isoArg = isoPath.Contains(" ") ? "\"" + isoPath + "\"" : isoPath;
            RunCommand("tools\\wit.exe", "extract -s " + isoArg + " -1 -n " + titleID + " . \"" + extDir + "\" --psel=DATA -ovv");
            Log("ISO extracted.");
            SetProgress(30);

            SetStatus("Applying patches...");
            using (var copy = new Process())
            {
                copy.StartInfo.FileName = "cmd.exe";
                copy.StartInfo.UseShellExecute = false;
                copy.StartInfo.RedirectStandardOutput = true;
                copy.StartInfo.WorkingDirectory = appDir;

                Dolpatcher dp = new Dolpatcher(extDir + "\\sys\\main.dol", true);

                int patchIndex = 0;
                foreach (Patch patch in patches)
                {
                    patchIndex++;
                    if (patch.root.StartsWith("\\")) patch.root = patch.root.Substring(1);

                    Log("Applying " + patch.id + " (" + patch.filePatches.Count + " files, " + patch.folderPatches.Count + " folders, " + patch.memoryPatches.Count + " memory patches)");
                    SetStatus("Applying " + patch.id + " (" + patchIndex + "/" + patches.Count + ")");
                    SetProgress(30 + (int)(40.0 * patchIndex / Math.Max(1, patches.Count)));

                    foreach (RiivolutionIsoBuilder.File filePatch in patch.filePatches)
                    {
                        DoStringTIDReplacements(ref filePatch.external);
                        string file = rootPath + patch.root + "\\" + filePatch.external;
                        string extPath = extDir + "\\files" + filePatch.disc;

                        if (string.IsNullOrEmpty(filePatch.disc) || Directory.Exists(extPath))
                        {
                            if (!string.IsNullOrEmpty(filePatch.disc))
                            {
                                copy.StartInfo.Arguments = "/C xcopy /b \"" + file + "\" \"" + extPath + "\"";
                            }
                            else if (Directory.Exists(file))
                            {
                                string foundFile = ProcessDirectory(extDir + "\\files\\", Path.GetFileName(file));
                                if (foundFile != "")
                                    copy.StartInfo.Arguments = "/C copy /b \"" + file + "\" \"" + foundFile + "\"";
                                else { Log("Not found " + file + " on the disc"); continue; }
                            }
                            else continue;

                            copy.Start();
                            copy.StandardOutput.ReadToEnd();
                            copy.WaitForExit();
                        }
                    }

                    foreach (Folder folderPatch in patch.folderPatches)
                    {
                        DoStringTIDReplacements(ref folderPatch.external);
                        string path = rootPath + patch.root + "\\" + folderPatch.external;
                        string extPath = extDir + "\\files" + ((folderPatch.disc == "root") ? "" : folderPatch.disc);

                        if (folderPatch.create && !Directory.Exists(extPath))
                            RunCommand("cmd.exe", "/C mkdir \"" + extPath + "\"");

                        if (string.IsNullOrEmpty(folderPatch.disc) || Directory.Exists(extPath))
                        {
                            if (!string.IsNullOrEmpty(folderPatch.disc))
                            {
                                string recursive = folderPatch.recursive ? " /E" : "";
                                copy.StartInfo.Arguments = "/C xcopy \"" + path + "\" \"" + extPath + "\"" + recursive + " /C /I /Y";
                            }
                            else if (Directory.Exists(path))
                            {
                                foreach (string file in Directory.GetFiles(path))
                                {
                                    string foundFile = ProcessDirectory(extDir + "\\files\\", Path.GetFileName(file));
                                    if (foundFile != "")
                                    {
                                        copy.StartInfo.Arguments = "/C copy /b \"" + file + "\" \"" + foundFile + "\"";
                                        copy.Start();
                                        copy.StandardOutput.ReadToEnd();
                                        copy.WaitForExit();
                                    }
                                    else Log("Not found " + file + " on the disc");
                                }
                                continue;
                            }
                            else continue;

                            copy.Start();
                            copy.StandardOutput.ReadToEnd();
                            copy.WaitForExit();
                        }
                    }

                    foreach (Memory memoryPatch in patch.memoryPatches)
                    {
                        if (memoryPatch.valueFile == "")
                        {
                            dp.doMemoryPatch(memoryPatch.offset, memoryPatch.value.ToArray(), memoryPatch.original.ToArray());
                        }
                        else
                        {
                            DoStringTIDReplacements(ref memoryPatch.valueFile);
                            string memFilePath = rootPath + patch.root + "\\" + memoryPatch.valueFile;
                            if (System.IO.File.Exists(memFilePath))
                                dp.doMemoryPatch(memoryPatch.offset, System.IO.File.ReadAllBytes(memFilePath), memoryPatch.original.ToArray());
                        }
                    }
                }

                if (disc.hasSaveGamePatches)
                    Log("Notice: this mod uses save-game patches, which cannot be integrated into the ISO. The same save data as the original game will be used unless you change the TitleID.");
                if (disc.badMemPatches)
                    Log("Notice: some memory patches may cause problems in USB Loaders (there should be no problem in Dolphin).");

                dp.saveDol();
            }

            SetStatus("Rebuilding...");
            SetProgress(80);

            if (newTitleID != "")
            {
                char[] oldttid = titleID.ToCharArray();
                char[] newttid = newTitleID.ToCharArray();
                for (int i = 0; i < 6; i++) { if (newttid[i] != '.') oldttid[i] = newttid[i]; }
                titleID = new string(oldttid);
                gameID = titleID.Substring(0, 3);
            }
            gameName = newGameName != "" ? newGameName : gameName + " [MODDED]";

            RunCommand("tools\\wit.exe", "copy \"" + extDir + "\" \"" + outPath + "\" -ovv --disc-id=" + titleID + " --tt-id=" + gameID + " --name \"" + gameName + "\"");

            if (deleteISO)
            {
                Log("Deleting temporary folder...");
                RunCommand("cmd.exe", "/C rmdir \"" + extDir + "\" /s /q");
            }

            Log("All done!");
            return true;
        }

        // --- Auxiliares (idénticos en función a los del Program.cs original) ---
        static string ProcessDirectory(string targetDirectory, string wantedFile)
        {
            foreach (string fileName in Directory.GetFiles(targetDirectory))
                if (Path.GetFileName(fileName).Equals(wantedFile, StringComparison.OrdinalIgnoreCase)) return fileName;
            foreach (string subdirectory in Directory.GetDirectories(targetDirectory))
            {
                string found = ProcessDirectory(subdirectory, wantedFile);
                if (found != "") return found;
            }
            return "";
        }

        void DoStringTIDReplacements(ref string str)
        {
            if (str.Contains("{$__region}")) str = str.Replace("{$__region}", region);
            if (str.Contains("{$__gameid}")) str = str.Replace("{$__gameid}", gameID);
            if (str.Contains("{$__maker}")) str = str.Replace("{$__maker}", maker);
        }

        int FindPatchIndexByName(string name)
        {
            for (int i = 0; i < disc.patches.Count; i++)
                if (disc.patches[i].id == name) return i;
            return -1;
        }

        void RunCommand(string executable, string arguments)
        {
            using (var command = new Process())
            {
                command.StartInfo.FileName = executable;
                command.StartInfo.Arguments = arguments;
                command.StartInfo.UseShellExecute = false;
                command.StartInfo.RedirectStandardOutput = true;
                command.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                command.Start();
                while (!command.StandardOutput.EndOfStream)
                    Log(command.StandardOutput.ReadLine());
                command.WaitForExit();
            }
        }
    }
}
