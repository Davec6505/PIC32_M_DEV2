using PIC32_M_DEV.classes;
using PIC32_M_DEV.Interfaces;
using PIC32_M_DEV.Properties;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;

namespace PIC32_M_DEV
{
    /// <summary>
    /// Main application form for the PIC32_M_DEV IDE.
    /// Handles UI initialization, project management, and event routing.
    /// </summary>
    public partial class Form1 : Form
    {
        // DockPanelSuite main docking panel
        private DockPanel dockPanel = null!;
        private DeserializeDockContent _deserializeDockContent;

        // Project explorer panel reference and state
        private ProjectTreePanel? _projectPanel;
        private string? _currentProjectPath;
        private string? _currentTreeSavePath;

        // PowerShell panel reference (to set working directory)
        private PowerShellPanel? _psPanel;

        // Open editors cache by full path
        private readonly Dictionary<string, CodeEditorPanel> _openEditors = new(StringComparer.OrdinalIgnoreCase);

        // THEME STATE
        private bool _darkMode = false; // default light

        private EmptyProjectPanel? _emptyPanel;

        private object rootPath;
        public object Properties { get; private set; }

        /// <summary>
        /// Initializes the main form, UI, and loads last project if available.
        /// </summary>
        public Form1()
        {
            InitializeComponent();

            // Make this the MDI container for DockPanelSuite
            this.IsMdiContainer = true;

            _darkMode = Settings.Default.DarkMode;
            _deserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);

            dockPanel = new DockPanel { Dock = DockStyle.Fill, DocumentStyle = DocumentStyle.DockingSdi };
            Controls.Add(dockPanel);

            dockPanel.Theme = _darkMode ? new VS2015DarkTheme() : new VS2015BlueTheme();
            InitializeMenu();
            ApplyNonDockPanelTheme(_darkMode);

            var layoutPath = Path.Combine(AppContext.BaseDirectory, "layout.xml");
            if (File.Exists(layoutPath))
            {
                try
                {
                    dockPanel.LoadFromXml(layoutPath, _deserializeDockContent);
                    if (_projectPanel != null)
                    {
                        _projectPanel.FileNodeActivated -= ProjectPanel_FileNodeActivated;
                        _projectPanel.FileNodeActivated += ProjectPanel_FileNodeActivated;
                        InitializeEventHandlers();
                    }
                    if (_psPanel != null && !string.IsNullOrEmpty(_currentProjectPath))
                        _psPanel.SetWorkingDirectory(_psPanel.GetCurrentDirectory());

                    ApplyThemeToAllDockContents(_darkMode);
                }
                catch
                {
                    InitializeDocking();
                    ApplyThemeToAllDockContents(_darkMode);
                    InitializeEventHandlers();
                }
            }
            else
            {
                InitializeDocking();
                ApplyThemeToAllDockContents(_darkMode);
                InitializeEventHandlers();
            }
            // Load last project if exists
            loadLastLoaded_Project();
        }

        /// <summary>
        /// Loads the last opened project if available, otherwise shows empty panel.
        /// </summary>
        private void loadLastLoaded_Project()
        {
            if (!string.IsNullOrEmpty(Settings.Default.projectPath) && Directory.Exists(Settings.Default.projectPath))
            {
                _currentProjectPath = Settings.Default.projectPath;
                EnsureProjectPanel();
                _projectPanel!.LoadFromDirectory(_currentProjectPath);
                ApplyThemeToAllDockContents(_darkMode); // Ensure theme after possible recreation
                if (_psPanel != null)
                    _psPanel.SetWorkingDirectory(_currentProjectPath);
                UpdateFileMenuState();
            }
            else
            {
                ShowEmptyPanel();
            }
        }

        /// <summary>
        /// Applies theme to all docked contents and open editors.
        /// </summary>
        private void ApplyThemeToAllDockContents(bool darkMode)
        {
            foreach (var content in dockPanel.Contents)
            {
                if (content is IThemedContent themed)
                    themed.ApplyTheme(darkMode);
            }
            foreach (var ed in _openEditors.Values.ToArray())
                if (!ed.IsDisposed) ed.ApplyTheme(darkMode);
        }

        /// <summary>
        /// Initializes the main menu and its event handlers.
        /// </summary>
        private void InitializeMenu()
        {
            _menuStrip = new MenuStrip();

            // File menu
            _fileMenu = new ToolStripMenuItem("File");
            _openMenuItem = new ToolStripMenuItem("Open", null, OnOpenClicked);
            _newMenuItem = new ToolStripMenuItem("New", null, OnNewClicked);
            _saveMenuItem = new ToolStripMenuItem("Save", null, OnSaveClicked);
            _saveAsMenuItem = new ToolStripMenuItem("Save As", null, OnSaveAsClicked);
            _closeMenuItem = new ToolStripMenuItem("Close All", null, OnCloseProjectClicked);
            _exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => this.Close());

            _fileMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _openMenuItem,
                _newMenuItem,
                new ToolStripSeparator(),
                _saveMenuItem,
                _saveAsMenuItem,
                new ToolStripSeparator(),
                _closeMenuItem,
                new ToolStripSeparator(),
                _exitMenuItem
            });

            // View menu
            _viewMenu = new ToolStripMenuItem("View");
            _darkModeMenuItem = new ToolStripMenuItem("Dark Mode")
            {
                CheckOnClick = true,
                Checked = _darkMode
            };
            _darkModeMenuItem.Click += (s, e) =>
            {
                Settings.Default.DarkMode = _darkModeMenuItem.Checked;
                Settings.Default.Save();
                Application.Restart();
                Environment.Exit(0);
            };
            _viewMenu.DropDownItems.Add(_darkModeMenuItem);

            // Add Open PowerShell menu item
            _openPowerShellMenuItem = new ToolStripMenuItem("Open PowerShell", null, OnOpenPowerShellClicked);
            _viewMenu.DropDownItems.Add(_openPowerShellMenuItem);

            // Mirror project menu
            _mirrorMenu = new ToolStripMenuItem("Mirror Project");
            _mirrorMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Open Mirror", null, OnOpenMirrorClicked),
                new ToolStripMenuItem("Close Mirror", null, OnCloseMirrorClicked)
            });

            // Options menu
            _optionsMenu = new ToolStripMenuItem("Options");
            _optionsMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Save Layout", null, OnSaveLayoutClicked),
                new ToolStripMenuItem("MPLABX",null, OnOpenMPLABXClicked),
                new ToolStripMenuItem("MCC Standalone",null,OnOpenMCCStandaloneClicked),
                new ToolStripMenuItem("VS Code",null,OnOpenVSCodeClicked)
            });

            // Add menus to the menu strip
            _menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenu!, _viewMenu, _mirrorMenu, _optionsMenu });
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            UpdateFileMenuState();
        }

        // --- Event Handlers for Menu Actions ---

        /// <summary>
        /// Launches VS Code and starts monitoring mirror path for changes.
        /// </summary>
        private void OnOpenVSCodeClicked(object? sender, EventArgs e)
        {
            var scr = new scripts();
            scr.launch("vscode");
            scr.alert_changes(Settings.Default.mirrorPath);
        }

        /// <summary>
        /// Launches MCC Standalone and starts monitoring mirror path for changes.
        /// </summary>
        private void OnOpenMCCStandaloneClicked(object? sender, EventArgs e)
        {
            var scr = new scripts();
            scr.launch("startMcc");
            scr.alert_changes(Settings.Default.mirrorPath);
        }

        /// <summary>
        /// Launches MPLABX and starts monitoring mirror path for changes.
        /// </summary>
        private void OnOpenMPLABXClicked(object? sender, EventArgs e)
        {
            var scr = new scripts();
            scr.launch("startMPLABX");
            scr.alert_changes(Settings.Default.mirrorPath);
        }

        /// <summary>
        /// Saves the current layout to disk.
        /// </summary>
        private void OnSaveLayoutClicked(object? sender, EventArgs e)
        {
            var layoutPath = Path.Combine(AppContext.BaseDirectory, "layout.xml");
            dockPanel.SaveAsXml(layoutPath);
            MessageBox.Show("Layout saved.", "Save Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Closes the right panel (mirror project view).
        /// </summary>
        private void OnCloseMirrorClicked(object? sender, EventArgs e)
        {
            var rightPanel = dockPanel.Contents
                .FirstOrDefault(c => c.DockHandler.TabText == "Right Panel") as ToolWindow;
            rightPanel?.Close();
        }

        /// <summary>
        /// Opens a folder in the right panel for mirror project view.
        /// </summary>
        private void OnOpenMirrorClicked(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to display in the Right Panel"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var rightPanel = dockPanel.Contents
                    .OfType<ToolWindow>()
                    .FirstOrDefault(tw => tw.Text == "Right Panel");
                if (rightPanel == null)
                {
                    rightPanel = new ToolWindow("Right Panel");
                    rightPanel.Show(dockPanel, DockState.DockRight);
                    if (rightPanel is IThemedContent themed)
                        themed.ApplyTheme(_darkMode);
                }
                rightPanel.LoadFolderTree(dlg.SelectedPath);
                rightPanel.Activate();

                rightPanel.FileNodeActivated -= RightPanel_FileNodeActivated;
                rightPanel.FileNodeActivated += RightPanel_FileNodeActivated;
                rightPanel.FileNodeCloseRequested -= RightPanel_FileNodeCloseRequested;
                rightPanel.FileNodeCloseRequested += RightPanel_FileNodeCloseRequested;
            }
        }

        /// <summary>
        /// Opens a file in the editor when activated from the right panel.
        /// </summary>
        private void RightPanel_FileNodeActivated(object? sender, string filePath)
        {
            OpenFileInEditor(filePath);
        }

        /// <summary>
        /// Closes an editor when requested from the right panel.
        /// </summary>
        private void RightPanel_FileNodeCloseRequested(object? sender, string filePath)
        {
            if (_openEditors.TryGetValue(filePath, out var editor))
            {
                editor.Close();
            }
        }

        /// <summary>
        /// Updates the enabled state of file menu items based on project state.
        /// </summary>
        private void UpdateFileMenuState()
        {
            bool hasProject = !string.IsNullOrEmpty(_currentProjectPath);
            if (_saveMenuItem != null) _saveMenuItem.Enabled = hasProject;
            if (_saveAsMenuItem != null) _saveAsMenuItem.Enabled = hasProject;
            if (_closeMenuItem != null) _closeMenuItem.Enabled = hasProject;
        }

        /// <summary>
        /// Opens a project folder and loads it into the explorer.
        /// </summary>
        private void OnOpenClicked(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a project folder to load into the Project Explorer",
                SelectedPath = _currentProjectPath?.ToString() ?? @"C:\"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _currentProjectPath = dlg.SelectedPath;
                _currentTreeSavePath = null; // reset save path
                EnsureProjectPanel();
                _projectPanel!.LoadFromDirectory(_currentProjectPath);
                ApplyThemeToAllDockContents(_darkMode); // Ensure theme after possible recreation

                // Point PowerShell to the selected project's folder
                _psPanel?.SetWorkingDirectory(_currentProjectPath);

                UpdateFileMenuState();

                Settings.Default.projectPath = _currentProjectPath;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Creates a new project in a selected parent folder.
        /// </summary>
        private void OnNewClicked(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Select the parent folder for your new project",
                SelectedPath = _currentProjectPath?.ToString() ?? ""
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string parentPath = dialog.SelectedPath;
                    string projectName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Enter a name for your new project:", "New Project", "MyProject");
                    if (string.IsNullOrWhiteSpace(projectName))
                    {
                        System.Windows.Forms.MessageBox.Show("Project name cannot be empty.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                    string newProjectPath = Path.Combine(parentPath, projectName);
                    if (Directory.Exists(newProjectPath))
                    {
                        MessageBox.Show("A project with this name already exists.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                    Directory.CreateDirectory(newProjectPath);
                    Settings.Default.projectPath = newProjectPath;
                    Directory.CreateDirectory(Path.Combine(newProjectPath, "srcs"));
                    Directory.CreateDirectory(Path.Combine(newProjectPath, "incs"));
                    Directory.CreateDirectory(Path.Combine(newProjectPath, "libs"));
                    Directory.CreateDirectory(Path.Combine(newProjectPath, "objs"));
                    Directory.CreateDirectory(Path.Combine(newProjectPath, "other"));
                    File.Copy($"{rootPath}dependancies\\makefiles\\Makefile_Root", Path.Combine(newProjectPath, "", "Makefile"));
                    if (Directory.Exists(Path.Combine(newProjectPath, "srcs")))
                    {
                        File.Copy($"{rootPath}dependancies\\makefiles\\Makefile_Srcs", Path.Combine(newProjectPath, "srcs", "Makefile"));
                        Directory.CreateDirectory(Path.Combine(newProjectPath, "srcs\\startup"));
                        if (Directory.Exists(Path.Combine(newProjectPath, "srcs", "startup")))
                        {
                            File.Copy($"{rootPath}dependancies\\project_files\\startup.S", Path.Combine(newProjectPath, "srcs\\startup", "startup.S"));
                        }
                    }

                    _currentProjectPath = dialog.SelectedPath;
                    _currentTreeSavePath = null; // reset save path
                    EnsureProjectPanel();
                    _projectPanel!.LoadFromDirectory(_currentProjectPath);
                    ApplyThemeToAllDockContents(_darkMode); // Ensure theme after possible recreation
                }
            }
        }

        /// <summary>
        /// Saves the currently active editor file.
        /// </summary>
        private void OnSaveClicked(object? sender, EventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor != null)
            {
                editor.SaveToFile();
            }
            else 
            {
                MessageBox.Show("No file currently open in editor.");
            }
        }

        /// <summary>
        /// Handles Save As for file or folder nodes in the project explorer.
        /// </summary>
        private void OnSaveAsClicked(object? sender, EventArgs e)
        {
            var selectedNode = _projectPanel?.SelectedNode();
            if (selectedNode == null)
            {
                MessageBox.Show("Please select a node in the Project Explorer to save.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Check if the node is a file node
            bool isFileNode = false;
            if (selectedNode != null && _projectPanel != null)
            {
                var method = typeof(ProjectTreePanel).GetMethod("IsFileNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    isFileNode = (bool)method.Invoke(_projectPanel, new object[] { selectedNode });
                }
            }

            if (isFileNode)
            {
                // Save As for file node
                var filePath = selectedNode.Tag as string;
                var editor = GetActiveEditor();
                if (editor == null)
                {
                    MessageBox.Show("No file currently open in editor.");
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Save File As",
                    Filter = "All Files (*.*)|*.*",
                    FileName = Path.GetFileName(editor.FilePath)
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    editor.SaveToFile(dlg.FileName);
                    _projectPanel.LoadFromDirectory(_currentProjectPath);
                }
            }
            else
            {
                // Save As for folder node (project/folder)
                using var fldr = new FolderBrowserDialog()
                {
                    Description = "Select the parent folder for the new project/folder"
                };
                if (fldr.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(fldr.SelectedPath))
                {
                    string parentPath = fldr.SelectedPath;
                    string newFolderName = Microsoft.VisualBasic.Interaction.InputBox(
                        "Enter a name for your new folder:", "Save As Folder", "NewFolder");
                    if (string.IsNullOrWhiteSpace(newFolderName))
                    {
                        MessageBox.Show("Folder name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    string newFolderPath = Path.Combine(parentPath, newFolderName);
                    if (Directory.Exists(newFolderPath))
                    {
                        MessageBox.Show("A folder with this name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    Directory.CreateDirectory(newFolderPath);

                    // Copy contents from the selected node's folder to the new folder
                    string sourceFolder = selectedNode.Tag as string ?? _currentProjectPath ?? "";
                    CopyDirectoryRecursive(sourceFolder, newFolderPath);

                    MessageBox.Show($"Folder saved as: {newFolderPath}");
                    _projectPanel.LoadFromDirectory(newFolderPath);
                }
                else
                {
                    MessageBox.Show("Please select a valid parent folder.", "No Folder Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// Recursively copies a directory and its contents.
        /// </summary>
        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(sourceDir, targetDir));
            }
            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(sourceDir, targetDir), true);
            }
        }

        /// <summary>
        /// Closes the current project and shows the empty panel.
        /// </summary>
        private void OnCloseProjectClicked(object? sender, EventArgs e)
        {
            ShowEmptyPanel();
            _currentProjectPath = null;
            _currentTreeSavePath = null;
            UpdateFileMenuState();
        }

        /// <summary>
        /// Ensures the project panel is created and shown, removing empty panel if present.
        /// </summary>
        private void EnsureProjectPanel()
        {
            // Remove empty panel if present
            if (_emptyPanel != null && !_emptyPanel.IsDisposed)
            {
                _emptyPanel.Close();
                _emptyPanel = null;
            }

            if (_projectPanel == null || _projectPanel.IsDisposed)
            {
                _projectPanel = new ProjectTreePanel
                {
                    DockAreas = DockAreas.DockLeft,
                    AllowEndUserDocking = false,
                    CloseButton = false,
                    TargetRootDirectory = _currentProjectPath ?? string.Empty // Ensure TargetRootDirectory is set
                };
                _projectPanel.FileNodeActivated += ProjectPanel_FileNodeActivated;
                _projectPanel.Show(dockPanel, DockState.DockLeft);
            }
            else
            {
                _projectPanel.TargetRootDirectory = _currentProjectPath ?? string.Empty; // Update if project path changes
            }
        }

        /// <summary>
        /// Shows the empty project panel and removes project panel if present.
        /// </summary>
        private void ShowEmptyPanel()
        {
            // Remove project panel if present
            if (_projectPanel != null && !_projectPanel.IsDisposed)
            {
                _projectPanel.Close();
                _projectPanel = null;
            }
            if (_emptyPanel == null || _emptyPanel.IsDisposed)
            {
                _emptyPanel = new EmptyProjectPanel();
                _emptyPanel.Show(dockPanel, DockState.DockLeft);
                _emptyPanel.ApplyTheme(_darkMode);
            }
        }

        /// <summary>
        /// Toggles the application theme and reloads layout.
        /// </summary>
        private void ToggleTheme(bool darkMode)
        {
            this.SuspendLayout();
            dockPanel.SuspendLayout();

            var layoutPath = Path.Combine(AppContext.BaseDirectory, "layout_tmp.xml");
            dockPanel.SaveAsXml(layoutPath);

            foreach (var content in dockPanel.DocumentsToArray())
                (content as IDockContent)?.DockHandler.Close();
            foreach (var content in dockPanel.Contents.ToArray())
                (content as IDockContent)?.DockHandler.Close();

            dockPanel.Theme = darkMode ? new VS2015DarkTheme() : new VS2015BlueTheme();

            if (File.Exists(layoutPath))
            {
                try { dockPanel.LoadFromXml(layoutPath, _deserializeDockContent); }
                catch { InitializeDocking(); }
            }
            else
            {
                InitializeDocking();
            }

            dockPanel.DockLeftPortion = 0.25;
            dockPanel.DockRightPortion = 0.25;
            dockPanel.DockBottomPortion = 0.25;

            ApplyNonDockPanelTheme(darkMode);

            dockPanel.ResumeLayout(true);
            this.ResumeLayout(true);
            dockPanel.PerformLayout();
            this.PerformLayout();
        }

        /// <summary>
        /// Applies theme to non-dock panel UI elements.
        /// </summary>
        private void ApplyNonDockPanelTheme(bool darkMode)
        {
            dockPanel.BackColor = darkMode ? Color.FromArgb(0, 0, 0) : SystemColors.Control;
            this.BackColor = darkMode ? Color.FromArgb(0, 0, 0) : SystemColors.Control;
            this.ForeColor = darkMode ? Color.FromArgb(0,0,0) : SystemColors.ControlText;
            // ...menu and recursive colors...
        }

        /// <summary>
        /// Gets the currently active code editor panel.
        /// </summary>
        private CodeEditorPanel? GetActiveEditor()
        {
            return dockPanel.ActiveDocument as CodeEditorPanel;
        }

        /// <summary>
        /// Initializes event handlers for project panel actions.
        /// </summary>
        private void InitializeEventHandlers()
        {
            _projectPanel.SaveFileRequested += (s, filePath) => {
                if (_openEditors.TryGetValue(filePath, out var editor) && editor != null)
                    editor.SaveToFile(); // This saves the actual file content
            };

            _projectPanel.SaveFileAsRequested += (s, args) => {
                var editor = GetActiveEditor();
                var (filePath, targetPath) = args;
                if (editor != null)
                {
                    editor.SaveToFile(); // Save current content to filePath
                    File.Copy(filePath, targetPath, true); // Copy to targetPath
                }
                else
                {
                    File.Copy(filePath, targetPath, true);
                }
            };

            _projectPanel.CloseFileRequested += (s, filePath) =>
            {
                if (_openEditors.TryGetValue(filePath, out var editor))
                {
                    editor.Close(); // This will close the AvalonEdit view for the file
                }
            };
        }

        /// <summary>
        /// Form load event handler (optional).
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // You can add startup logic here if needed, or remove this method if not used in designer
        }

        /// <summary>
        /// Loads the default layout for the docking panels.
        /// </summary>
        private void LoadDefaultLayout()
        {
            InitializeDocking();
        }

        /// <summary>
        /// Applies the application theme to all UI elements.
        /// </summary>
        private void ApplyAppTheme(bool darkMode)
        {
            ApplyNonDockPanelTheme(darkMode);
            foreach (var ed in _openEditors.Values.ToArray())
            {
                if (!ed.IsDisposed) ed.ApplyTheme(darkMode);
            }
            ApplyWinFormsThemeRecursive(this, darkMode);
            if (_darkModeMenuItem != null) _darkModeMenuItem.Checked = darkMode;
        }

        /// <summary>
        /// Recursively applies theme to WinForms controls.
        /// </summary>
        private static void ApplyWinFormsThemeRecursive(Control root, bool darkMode)
        {
            if (root is DockPanel) return;
            if (root is ToolStrip ts)
            {
                ts.BackColor = darkMode ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
                ts.ForeColor = darkMode ? Color.Gainsboro : SystemColors.ControlText;
            }
            else
            {
                root.BackColor = darkMode ? Color.FromArgb(37, 37, 38) : SystemColors.Control;
                root.ForeColor = darkMode ? Color.Gainsboro : SystemColors.ControlText;
            }
            foreach (Control child in root.Controls)
                ApplyWinFormsThemeRecursive(child, darkMode);
        }

        /// <summary>
        /// Opens a file in the code editor panel.
        /// </summary>
        private void OpenFileInEditor(string filePath)
        {
            if (!File.Exists(filePath)) return;
            if (_openEditors.TryGetValue(filePath, out var existing))
            {
                existing.Show(dockPanel, DockState.Document);
                existing.Activate();
                return;
            }
            var editor = new CodeEditorPanel(filePath);
            _openEditors[filePath] = editor;
            editor.FormClosed += (s, e) => _openEditors.Remove(filePath);
            editor.Show(dockPanel, DockState.Document);
            editor.Activate();
            editor.ApplyTheme(_darkMode);
        }

        /// <summary>
        /// Resolves dock content from its persist string.
        /// </summary>
        private IDockContent GetContentFromPersistString(string persistString)
        {
            return persistString switch
            {
                "PIC32_M_DEV.ProjectTreePanel" or "Project Explorer" => (_projectPanel ??= CreateProjectPanel()),
                "PIC32_M_DEV.PowerShellPanel" or "PowerShell" => (_psPanel ??= CreatePowerShellPanel()),
                "Right Panel" => new ToolWindow("Right Panel"),
                "Main Document" => new ToolWindow("Main Document"),
                _ => new ToolWindow(persistString)
            };
        }

        /// <summary>
        /// Handles file node activation from the project panel.
        /// </summary>
        private void ProjectPanel_FileNodeActivated(object? sender, string filePath)
        {
            OpenFileInEditor(filePath);
        }

        /// <summary>
        /// Initializes docking panels and their layout.
        /// </summary>
        private void InitializeDocking()
        {
            if (_projectPanel != null && !_projectPanel.IsDisposed)
            {
                _projectPanel.Close();
                _projectPanel = null;
            }
            if (_emptyPanel != null && !_emptyPanel.IsDisposed)
            {
                _emptyPanel.Close();
                _emptyPanel = null;
            }
            if (!string.IsNullOrEmpty(_currentProjectPath) && Directory.Exists(_currentProjectPath))
            {
                _projectPanel = CreateProjectPanel();
                _projectPanel.Show(dockPanel, DockState.DockLeft);
            }
            else
            {
                _emptyPanel = new EmptyProjectPanel();
                _emptyPanel.Show(dockPanel, DockState.DockLeft);
                _emptyPanel.ApplyTheme(_darkMode);
            }
            _psPanel = CreatePowerShellPanel();
            _psPanel.Show(dockPanel, DockState.DockBottomAutoHide);
        }

        /// <summary>
        /// Creates and returns a new project tree panel.
        /// </summary>
        private ProjectTreePanel CreateProjectPanel()
        {
            var panel = new ProjectTreePanel
            {
                DockAreas = DockAreas.DockLeft,
                AllowEndUserDocking = false,
                CloseButton = false,
                TargetRootDirectory = _currentProjectPath ?? string.Empty
            };
            panel.FileNodeActivated -= ProjectPanel_FileNodeActivated;
            panel.FileNodeActivated += ProjectPanel_FileNodeActivated;
            return panel;
        }

        /// <summary>
        /// Creates and returns a new PowerShell panel.
        /// </summary>
        private PowerShellPanel CreatePowerShellPanel()
        {
            var panel = new PowerShellPanel();
            if (!string.IsNullOrEmpty(_currentProjectPath))
            {
                panel.SetWorkingDirectory(_currentProjectPath);
                panel.ShowProjectDirectory();
            }
            panel.ApplyTheme(_darkMode);
            return panel;
        }

        /// <summary>
        /// Launches PowerShell panel within the IDE.
        /// </summary>
        private void OnOpenPowerShellClicked(object? sender, EventArgs e)
        {
            if (_psPanel == null || _psPanel.IsDisposed)
            {
                _psPanel = CreatePowerShellPanel();
                _psPanel.Show(dockPanel, DockState.DockBottomAutoHide);
            }
            else
            {
                _psPanel.Show(dockPanel, DockState.DockBottomAutoHide);
                _psPanel.Activate();
            }
        }
    }
}
