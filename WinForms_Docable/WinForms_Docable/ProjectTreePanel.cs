using System;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using WinForms_Docable.Interfaces;

namespace WinForms_Docable
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

            InitializeContextMenu();

            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            treeView.KeyDown += TreeView_KeyDown;
        }

        private void InitializeContextMenu()
        {
            _nodeContextMenu = new ContextMenuStrip();
            var saveItem = new ToolStripMenuItem("Save", null, OnSaveNodeClicked);
            var saveAsItem = new ToolStripMenuItem("Save As", null, OnSaveAsNodeClicked);
            var closeItem = new ToolStripMenuItem("Close", null, OnCloseNodeClicked);
            var deleteItem = new ToolStripMenuItem("Delete", null, OnDeleteNodeClicked);

            _nodeContextMenu.Items.AddRange(new ToolStripItem[] {
                saveItem, saveAsItem, closeItem, deleteItem
            });

            treeView.ContextMenuStrip = _nodeContextMenu;
            treeView.NodeMouseClick += TreeView_NodeMouseClick_ShowMenu;
        }

        private void TreeView_NodeMouseClick_ShowMenu(object? sender, TreeNodeMouseClickEventArgs e)
        {
            treeView.SelectedNode = e.Node;
            if (IsFileNode(e.Node) && e.Button == MouseButtons.Right)
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
        }

        private void OnDeleteNodeClicked(object? sender, EventArgs e)
        {
            var node = treeView.SelectedNode;
            if (node != null && IsFileNode(node))
            {
                var filePath = node.Tag as string;
                if (filePath != null && File.Exists(filePath))
                {
                    DeleteFileRequested?.Invoke(this, filePath);
                    File.Delete(filePath);
                    node.Remove();
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
    }
}