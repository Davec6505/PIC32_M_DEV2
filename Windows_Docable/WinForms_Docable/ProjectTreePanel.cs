using PIC32_M_DEV.Interfaces;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;

namespace PIC32_M_DEV
{
    public class ProjectTreePanel : DockContent, IThemedContent
    {
        private readonly TreeView treeView;
        private ContextMenuStrip _nodeContextMenu;

        public event EventHandler<string>? FileNodeActivated;
        public event EventHandler<string>? SaveFileRequested;
        public event EventHandler<(string filePath, string targetPath)>? SaveFileAsRequested;
        public event EventHandler<string>? CloseFileRequested;
        public event EventHandler<string>? DeleteFileRequested;

        // Added property
        public string TargetRootDirectory { get; set; } = string.Empty;

        // Internal clipboard state for cut/copy
        private string? _internalClipboardPath = null;
        private bool _internalClipboardIsCut = false;

        public ProjectTreePanel()
        {
            Text = "Project Explorer";
            DockAreas = DockAreas.DockLeft | DockAreas.Float;

            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowNodeToolTips = true
            };
            Controls.Add(treeView);

            treeView.AllowDrop = true;

            treeView.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(typeof(List<string>)) || e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };

            treeView.DragDrop += (s, e) =>
            {
                List<string> files = e.Data.GetData(typeof(List<string>)) as List<string>
                    ?? (e.Data.GetData(DataFormats.FileDrop) as string[])?.ToList();
                if (files != null)
                {
                    TreeNode dropNode = treeView.GetNodeAt(treeView.PointToClient(Cursor.Position));
                    string targetRoot = TargetRootDirectory;
                    string parentPath = "";
                    if (dropNode != null && dropNode.Tag is string selectedPath && Directory.Exists(selectedPath))
                    {
                        targetRoot = selectedPath;
                        parentPath = selectedPath;
                    }
                    // Detect if Ctrl is pressed for copy, otherwise move
                    bool isCopy = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    foreach (var file in files)
                    {
                        if (isCopy)
                            CopyWithStructure(file, targetRoot, parentPath);
                        else
                            MoveWithStructure(file, targetRoot, parentPath);
                    }
                }
            };

            InitializeContextMenu();

            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            treeView.KeyDown += TreeView_KeyDown;
        }

        private void InitializeContextMenu()
        {
            _nodeContextMenu = new ContextMenuStrip();
            var saveItem = new ToolStripMenuItem("Save", null, OnSaveNodeClicked);
            var saveAsItem = new ToolStripMenuItem("Save As", null, OnSaveAsNodeClicked);
            var copyItem = new ToolStripMenuItem("Copy", null, OnCopyNodeClicked); // Add Copy
            var cutItem = new ToolStripMenuItem("Cut", null, (s, e) =>
            {
                var node = treeView.SelectedNode;
                if (node != null && node.Tag is string path)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        var col = new System.Collections.Specialized.StringCollection();
                        col.Add(path);
                        Clipboard.SetFileDropList(col);
                        Clipboard.SetData("FileCut", true);
                        _internalClipboardPath = path;
                        _internalClipboardIsCut = true;
                        MessageBox.Show($"Cut: '{path}' added to clipboard.", "Cut", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Cannot cut: '{path}' does not exist.", "Cut Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            });
            var closeItem = new ToolStripMenuItem("Close", null, OnCloseNodeClicked);
            var pasteItem = new ToolStripMenuItem("Paste", null, (s, e) =>
            {
                var col = Clipboard.GetFileDropList();
                bool isCut = Clipboard.ContainsData("FileCut");
                string? pathToPaste = null;
                bool useInternal = false;
                if ((col != null && col.Count > 0) && (isCut || col.Cast<string>().Any(f => File.Exists(f) || Directory.Exists(f))))
                {
                    pathToPaste = col[0];
                }
                else if (!string.IsNullOrEmpty(_internalClipboardPath) && (File.Exists(_internalClipboardPath) || Directory.Exists(_internalClipboardPath)))
                {
                    pathToPaste = _internalClipboardPath;
                    isCut = _internalClipboardIsCut;
                    useInternal = true;
                }
                if (!string.IsNullOrEmpty(pathToPaste))
                {
                    string targetRoot = TargetRootDirectory;
                    string parentPath = "";
                    if (treeView.SelectedNode != null && treeView.SelectedNode.Tag is string selectedPath && Directory.Exists(selectedPath))
                    {
                        targetRoot = selectedPath;
                        parentPath = selectedPath;
                    }
                    if (isCut)
                        MoveWithStructure(pathToPaste, targetRoot, parentPath);
                    else
                        CopyWithStructure(pathToPaste, targetRoot, parentPath);
                    Clipboard.Clear();
                    if (useInternal)
                    {
                        _internalClipboardPath = null;
                        _internalClipboardIsCut = false;
                    }
                }
                else
                {
                    MessageBox.Show("Clipboard does not contain any valid files or folders to paste.", "Paste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
            var deleteItem = new ToolStripMenuItem("Delete", null, OnDeleteNodeClicked);

            _nodeContextMenu.Items.AddRange(new ToolStripItem[] {
                saveItem, saveAsItem, copyItem, cutItem, closeItem, pasteItem, deleteItem
            });

            treeView.ContextMenuStrip = _nodeContextMenu;
            treeView.NodeMouseClick += TreeView_NodeMouseClick_ShowMenu;
        }

        private void TreeView_NodeMouseClick_ShowMenu(object? sender, TreeNodeMouseClickEventArgs e)
        {
            treeView.SelectedNode = e.Node;
            // Show menu for file nodes OR root nodes (parent == null)
            if ((IsFileNode(e.Node) || e.Node.Parent == null) && e.Button == MouseButtons.Right)
            {
                _nodeContextMenu.Show(treeView, e.Location);
            }
        }

        private bool IsFileNode(TreeNode node)
        {
            return node.Tag is string filePath && File.Exists(filePath);
        }

        private void OnSaveNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && IsFileNode(node))
            {
                SaveFileRequested?.Invoke(this, node.Tag as string ?? string.Empty);
            }
        }

        private void OnSaveAsNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && IsFileNode(node))
            {
                using var dlg = new SaveFileDialog { FileName = Path.GetFileName(node.Tag as string) };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var src = node.Tag as string;
                    var target = dlg.FileName;
                    if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(target))
                        SaveFileAsRequested?.Invoke(this, (src, target));
                }
            }
        }

        private void OnCloseNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && IsFileNode(node))
            {
                CloseFileRequested?.Invoke(this, node.Tag as string ?? string.Empty);
            }
            else if (node != null && node.Parent == null) // If it's a root node (folder)
            {
                treeView.Nodes.Remove(node);
            }
        }

        private void OnDeleteNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && node.Tag is string path)
            {
                if (File.Exists(path))
                {
                    DeleteFileRequested?.Invoke(this, path);
                    File.Delete(path);
                    node.Remove();
                }
                else if (Directory.Exists(path))
                {
                    // Confirm folder delete
                    var result = MessageBox.Show($"Are you sure you want to delete the folder '{path}' and all its contents?", "Delete Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result == DialogResult.Yes)
                    {
                        Directory.Delete(path, true); // recursive delete
                        node.Remove();
                    }
                }
            }
        }

        public void ApplyTheme(bool darkMode)
        {
            var bg = darkMode ? Color.FromArgb(37, 37, 38) : SystemColors.Window;
            var fg = darkMode ? Color.Gainsboro : SystemColors.WindowText;
            var line = darkMode ? Color.FromArgb(62, 62, 64) : SystemColors.GrayText;

            BackColor = bg;
            ForeColor = fg;

            treeView.BackColor = bg;
            treeView.ForeColor = fg;
            treeView.LineColor = line;
            // Optional: Owner-draw for custom selection colors if desired.
        }

        private void TreeView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && treeView.SelectedNode != null)
                TryActivateFileNode(treeView.SelectedNode);
        }

        private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
            => TryActivateFileNode(e.Node);

        private void TryActivateFileNode(TreeNode node)
        {
            if (node?.Tag is string path && File.Exists(path))
                FileNodeActivated?.Invoke(this, path);
        }

        public void ClearProject()
        {
            treeView.Nodes.Clear();
            Text = "Project Explorer";
        }

        public void LoadFromDirectory(string path)
        {
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                var root = new TreeNode(Path.GetFileName(path)) { Tag = path, ToolTipText = path };
                treeView.Nodes.Add(root);
                LoadDirectoryRecursive(root, path);
                root.Expand();
                Text = $"Project Explorer - {Path.GetFileName(path)}";
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private void LoadDirectoryRecursive(TreeNode parent, string dir)
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var folderNode = new TreeNode(Path.GetFileName(subDir)) { Tag = subDir, ToolTipText = subDir };
                parent.Nodes.Add(folderNode);
                LoadDirectoryRecursive(folderNode, subDir);
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                var fileNode = new TreeNode(Path.GetFileName(file)) { Tag = file, ToolTipText = file };
                parent.Nodes.Add(fileNode);
            }
        }

        public void SaveTreeToFile(string filePath)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            foreach (TreeNode node in treeView.Nodes)
                WriteNode(node, writer, 0);
        }

        private void WriteNode(TreeNode node, StreamWriter writer, int indent)
        {
            writer.WriteLine(new string(' ', indent * 2) + node.Text);
            foreach (TreeNode child in node.Nodes)
                WriteNode(child, writer, indent + 1);
        }

        public void RemoveSelectedNode()
        {
            if (treeView.SelectedNode != null && treeView.SelectedNode.Parent == null)
            {
                treeView.Nodes.Remove(treeView.SelectedNode);
                treeView.SelectedNode = null;
            }
        }

        public TreeNode? SelectedNode()
        {
            return treeView.SelectedNode;
        }

        // Helper to find a node by its path
        private TreeNode? FindNodeByPath(string path)
        {
            foreach (TreeNode node in treeView.Nodes)
            {
                var found = FindNodeByPathRecursive(node, path);
                if (found != null) return found;
            }
            return null;
        }

        private TreeNode? FindNodeByPathRecursive(TreeNode node, string path)
        {
            if (node.Tag is string nodePath && string.Equals(nodePath, path, StringComparison.OrdinalIgnoreCase))
                return node;
            foreach (TreeNode child in node.Nodes)
            {
                var found = FindNodeByPathRecursive(child, path);
                if (found != null) return found;
            }
            return null;
        }

        private void AddNodeRecursive(string path, string parentPath = "")
        {
            TreeNode? parentNode = string.IsNullOrEmpty(parentPath) ? null : FindNodeByPath(parentPath);

            if (File.Exists(path))
            {
                var node = new TreeNode(Path.GetFileName(path)) { Tag = path };
                if (parentNode != null)
                    parentNode.Nodes.Add(node);
                else
                    treeView.Nodes.Add(node);
            }
            else if (Directory.Exists(path))
            {
                var dirNode = new TreeNode(Path.GetFileName(path)) { Tag = path };
                if (parentNode != null)
                    parentNode.Nodes.Add(dirNode);
                else
                    treeView.Nodes.Add(dirNode);
                foreach (var subDir in Directory.GetDirectories(path))
                    AddNodeRecursive(subDir, path);
                foreach (var file in Directory.GetFiles(path))
                    AddNodeRecursive(file, path);
            }
        }

        private void CopyWithStructure(string sourcePath, string targetRoot, string parentPath = "")
        {
            treeView.BeginUpdate();
            if (File.Exists(sourcePath))
            {
                var targetFile = Path.Combine(targetRoot, Path.GetFileName(sourcePath));
                // Prevent copying to the same location
                if (string.Equals(sourcePath, targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Cannot copy '{sourcePath}' to the same location.", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    treeView.EndUpdate();
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                try
                {
                    File.Copy(sourcePath, targetFile, true);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Could not copy file '{sourcePath}'. It may be open in another program.\n\n{ex.Message}", "File Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    treeView.EndUpdate();
                    return;
                }
                AddNodeRecursive(targetFile, parentPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                var folderName = Path.GetFileName(sourcePath);
                var targetDir = Path.Combine(targetRoot, folderName);
                // Prevent copying to the same location
                if (string.Equals(sourcePath, targetDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Cannot copy folder '{sourcePath}' to the same location.", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    treeView.EndUpdate();
                    return;
                }
                CopyDirectoryRecursive(sourcePath, targetDir);
                AddNodeRecursive(targetDir, parentPath);
            }
            treeView.EndUpdate();
            var parentNode = FindNodeByPath(parentPath);
            if (parentNode != null)
                parentNode.Expand();
            treeView.Refresh();
        }

        private void MoveWithStructure(string sourcePath, string targetRoot, string parentPath = "")
        {
            treeView.BeginUpdate();
            TreeNode? sourceNode = FindNodeByPath(sourcePath);
            bool moved = false;
            if (File.Exists(sourcePath))
            {
                var targetFile = Path.Combine(targetRoot, Path.GetFileName(sourcePath));
                if (string.Equals(sourcePath, targetFile, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Cannot move '{sourcePath}' to the same location.", "Move Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    treeView.EndUpdate();
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                try
                {
                    File.Move(sourcePath, targetFile, true);
                    moved = true;
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Could not move file '{sourcePath}'. It may be open in another program.\n\n{ex.Message}", "File Move Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    treeView.EndUpdate();
                    return;
                }
                AddNodeRecursive(targetFile, parentPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                var folderName = Path.GetFileName(sourcePath);
                var targetDir = Path.Combine(targetRoot, folderName);
                if (string.Equals(sourcePath, targetDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Cannot move folder '{sourcePath}' to the same location.", "Move Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    treeView.EndUpdate();
                    return;
                }
                try
                {
                    Directory.Move(sourcePath, targetDir);
                    moved = true;
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Could not move folder '{sourcePath}'. It may be open in another program.\n\n{ex.Message}", "Folder Move Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    treeView.EndUpdate();
                    return;
                }
                AddNodeRecursive(targetDir, parentPath);
            }
            if (moved && sourceNode != null)
            {
                sourceNode.Remove();
                MessageBox.Show($"Moved '{sourcePath}' successfully.", "Move Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            treeView.EndUpdate();
            var parentNode = FindNodeByPath(parentPath);
            if (parentNode != null)
                parentNode.Expand();
            treeView.Refresh();
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                try
                {
                    File.Copy(file, targetFile, true);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Could not copy file '{file}'. It may be open in another program.\n\n{ex.Message}", "File Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, targetSubDir);
            }
        }

        private void OnCopyNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && node.Tag is string path)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    var col = new System.Collections.Specialized.StringCollection();
                    col.Add(path);
                    Clipboard.SetFileDropList(col);
                    _internalClipboardPath = path;
                    _internalClipboardIsCut = false;
                    MessageBox.Show($"Copy: '{path}' added to clipboard.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Cannot copy: '{path}' does not exist.", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


    }
}