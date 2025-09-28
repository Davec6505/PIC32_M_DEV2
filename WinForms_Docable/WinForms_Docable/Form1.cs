using System;
using System.Drawing; // ADDED
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WeifenLuo.WinFormsUI.Docking;
using WinForms_Docable.classes;
using WinForms_Docable.Interfaces;
using WinForms_Docable.Properties;

namespace WinForms_Docable
{
    public partial class Form1 : Form
    {
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
        private object rootPath;

        public Form1()
        {
            InitializeComponent();

            // Set rootPath to the application's base directory
            rootPath = AppContext.BaseDirectory;
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
                    }
                    if (_psPanel != null && !string.IsNullOrEmpty(_currentProjectPath))
                        _psPanel.SetWorkingDirectory(_currentProjectPath);

                    ApplyThemeToAllDockContents(_darkMode);
                }
                catch
                {
                    InitializeDocking();
                    ApplyThemeToAllDockContents(_darkMode);
                }
            }
            else
            {
                InitializeDocking();
                ApplyThemeToAllDockContents(_darkMode);
            }
            InitializeEventHandlers();

            // Load last project if exists
            loadLastLoaded_Project();
        }

        private void loadLastLoaded_Project()
        {
            if (!string.IsNullOrEmpty(Settings.Default.projectPath) && Directory.Exists(Settings.Default.projectPath))
            {
                _currentProjectPath = Settings.Default.projectPath;
                EnsureProjectPanel();
                _projectPanel!.LoadFromDirectory(_currentProjectPath);
                if (_psPanel != null)
                    _psPanel.SetWorkingDirectory(_currentProjectPath);
                UpdateFileMenuState();
            }
        }

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

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void LoadDefaultLayout()
        {
            // Recreate tool panes; no document placeholder
            InitializeDocking();
        }

        // Add helpers
        private ProjectTreePanel CreateProjectPanel()
        {
            var panel = new ProjectTreePanel
            {
                DockAreas = DockAreas.DockLeft,
                AllowEndUserDocking = false,
                CloseButton = false
            };
            panel.FileNodeActivated -= ProjectPanel_FileNodeActivated;
            panel.FileNodeActivated += ProjectPanel_FileNodeActivated;
            panel.ApplyTheme(_darkMode);
            return panel;
        }

        private PowerShellPanel CreatePowerShellPanel()
        {
            var panel = new PowerShellPanel();
            if (!string.IsNullOrEmpty(_currentProjectPath))
                panel.SetWorkingDirectory(_currentProjectPath);
            panel.ApplyTheme(_darkMode);
            return panel;
        }

        // Update deserializer to use creators (so wiring happens on restore too)
        private IDockContent GetContentFromPersistString(string persistString)
        {
            return persistString switch
            {
                "WinForms_Docable.ProjectTreePanel" or "Project Explorer" => (_projectPanel ??= CreateProjectPanel()),
                "WinForms_Docable.PowerShellPanel" or "PowerShell" => (_psPanel ??= CreatePowerShellPanel()),
                "Right Panel" => new ToolWindow("Right Panel"),
                "Main Document" => new ToolWindow("Main Document"),
                _ => new ToolWindow(persistString)
            };
        }

        private void InitializeDocking()
        {
            // DO NOT recreate dockPanel; it already exists and has Theme set

            _projectPanel = CreateProjectPanel();
            _projectPanel.Show(dockPanel, DockState.DockLeft);

            new ToolWindow("Right Panel").Show(dockPanel, DockState.DockRight);

            _psPanel = CreatePowerShellPanel();
            _psPanel.Show(dockPanel, DockState.DockBottomAutoHide);

            // No "Main Document" placeholder needed in MDI
        }

        private void ProjectPanel_FileNodeActivated(object? sender, string filePath)
        {
            OpenFileInEditor(filePath);
        }

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

            // Ensure new editors follow current theme
            editor.ApplyTheme(_darkMode);
        }



        public object Properties { get; private set; }

        private void InitializeMenu()
        {
            _menuStrip = new MenuStrip();
            //use this to customize the menu strip colors if needed
            // _menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

            // File
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

            // View
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

            // Mirror project for opposite docking panels if needed
            _mirrorMenu = new ToolStripMenuItem("Mirror Project");
            _mirrorMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Sync Panels", null, (s, e) => MessageBox.Show("Sync Panels clicked")),
                new ToolStripMenuItem("Clear Panels", null, (s, e) => MessageBox.Show("Clear Panels clicked"))
            });

            // Add menus to the menu strip
            _menuStrip.Items.AddRange(new ToolStripItem[] { _fileMenu!, _viewMenu, _mirrorMenu });
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            UpdateFileMenuState();
        }



        private void UpdateFileMenuState()
        {
            bool hasProject = !string.IsNullOrEmpty(_currentProjectPath);
            if (_saveMenuItem != null) _saveMenuItem.Enabled = hasProject;
            if (_saveAsMenuItem != null) _saveAsMenuItem.Enabled = hasProject;
            if (_closeMenuItem != null) _closeMenuItem.Enabled = hasProject;
        }

        private void OnOpenClicked(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a project folder to load into the Project Explorer"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _currentProjectPath = dlg.SelectedPath;
                _currentTreeSavePath = null; // reset save path
                EnsureProjectPanel();
                _projectPanel!.LoadFromDirectory(_currentProjectPath);

                // Point PowerShell to the selected project's folder
                _psPanel?.SetWorkingDirectory(_currentProjectPath);

                UpdateFileMenuState();

                Settings.Default.projectPath = _currentProjectPath;
                Settings.Default.Save();
            }
        }

        private void OnNewClicked(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Select the parent folder for your new project",
                SelectedPath = rootPath?.ToString() ?? ""
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

                }
            }



            
        }

        
        private void OnSaveClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProjectPath)) return;
            if (string.IsNullOrEmpty(_currentTreeSavePath))
            {
                OnSaveAsClicked(sender, e);
                return;
            }
            EnsureProjectPanel();
            _projectPanel!.SaveTreeToFile(_currentTreeSavePath!);
        }

        private void OnSaveAsClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProjectPath)) return;
            using var dlg = new SaveFileDialog
            {
                Title = "Save Project Tree As",
                Filter = "Tree Export (*.tree.txt)|*.tree.txt|All Files (*.*)|*.*",
                FileName = "Project.tree.txt"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _currentTreeSavePath = dlg.FileName;
                EnsureProjectPanel();
                _projectPanel!.SaveTreeToFile(_currentTreeSavePath);
            }
        }

        private void OnCloseProjectClicked(object? sender, EventArgs e)
        {
            EnsureProjectPanel();

            var selectedNode = _projectPanel!.SelectedNode();

            if (selectedNode == null)
            {
                MessageBox.Show("Please select a project node to close.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selectedNode.Parent != null)
            {
                MessageBox.Show("Please select a top-level project node.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Remove the selected project node
            _projectPanel.RemoveSelectedNode();

            // Optionally clear related state if the closed project was the current one
            if (_currentProjectPath == selectedNode.Tag as string)
            {
                _currentProjectPath = null;
                _currentTreeSavePath = null;
                UpdateFileMenuState();
            }
        }


        private void EnsureProjectPanel()
        {
            if (_projectPanel == null || _projectPanel.IsDisposed)
            {
                _projectPanel = new ProjectTreePanel
                {
                    DockAreas = DockAreas.DockLeft,
                    AllowEndUserDocking = false,
                    CloseButton = false
                };
                _projectPanel.FileNodeActivated += ProjectPanel_FileNodeActivated;
                _projectPanel.Show(dockPanel, DockState.DockLeft);
            }
        }

        // THEME: Apply dark/light to the whole WinForms app and DockPanel
        private void ApplyAppTheme(bool darkMode)
        {
            // Do NOT set dockPanel.Theme here
            ApplyNonDockPanelTheme(darkMode);

            foreach (var ed in _openEditors.Values.ToArray())
            {
                if (!ed.IsDisposed) ed.ApplyTheme(darkMode);
            }

            ApplyWinFormsThemeRecursive(this, darkMode);
            if (_darkModeMenuItem != null) _darkModeMenuItem.Checked = darkMode;
        }

        private static void ApplyWinFormsThemeRecursive(Control root, bool darkMode)
        {
            // Skip DockPanel as it is themed by DockPanelSuite
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

            // Do NOT change DocumentStyle here
            // dockPanel.DocumentStyle = DocumentStyle.DockingMdi; // remove this line

            if (File.Exists(layoutPath))
            {
                try { dockPanel.LoadFromXml(layoutPath, _deserializeDockContent); }
                catch { LoadDefaultLayout(); }
            }
            else
            {
                LoadDefaultLayout();
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

        private void ApplyNonDockPanelTheme(bool darkMode)
        {
            dockPanel.BackColor = darkMode ? Color.FromArgb(0, 0, 0) : SystemColors.Control;
            this.BackColor = darkMode ? Color.FromArgb(0, 0, 0) : SystemColors.Control;
            this.ForeColor = darkMode ? Color.FromArgb(0,0,0) : SystemColors.ControlText;
            // ...menu and recursive colors...
        }

        private CodeEditorPanel? GetActiveEditor()
        {
            // Find the editor that is currently focused/active
            return dockPanel.ActiveDocument as CodeEditorPanel;
        }

        private void InitializeEventHandlers()
        {
            _projectPanel.SaveFileRequested += (s, filePath) => {
                var editor = GetActiveEditor();
                if (editor != null)
                    editor.SaveToFile();
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

              
           
        // ... other event handlers unchanged ...
        }
    }


}
