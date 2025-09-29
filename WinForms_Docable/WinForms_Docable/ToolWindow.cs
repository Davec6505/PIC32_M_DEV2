using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using WinForms_Docable.Interfaces;

namespace WinForms_Docable
{
    public class ToolWindow : DockContent, IThemedContent
    {
        private readonly Label _label;
        private readonly TreeView? _treeView;
        private readonly List<TreeNode> _selectedNodes = new();
        private ContextMenuStrip? _contextMenu;

        public event EventHandler<string>? FileNodeActivated;
        public event EventHandler<string>? FileNodeCloseRequested;
        public event EventHandler<IReadOnlyList<string>>? NodesCopyRequested;

        public ToolWindow(string title)
        {
            Text = title;
            DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockBottom | DockAreas.Document | DockAreas.Float;

            if (title == "Right Panel")
            {
                _treeView = new TreeView
                {
                    Dock = DockStyle.Fill,
                    AllowDrop = true
                };
                Controls.Add(_treeView);

                // Multi-select (Ctrl+Click, Shift+Click)
                _treeView.BeforeSelect += (s, e) =>
                {
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                    {
                        if (_selectedNodes.Contains(e.Node))
                            _selectedNodes.Remove(e.Node);
                        else
                            _selectedNodes.Add(e.Node);
                        e.Cancel = true;
                        HighlightSelectedNodes();
                    }
                    else if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift && _selectedNodes.Count > 0)
                    {
                        var last = _selectedNodes.Last();
                        var parent = last.Parent ?? _treeView.Nodes[0];
                        int start = parent.Nodes.IndexOf(last);
                        int end = parent.Nodes.IndexOf(e.Node);
                        if (start > end) (start, end) = (end, start);
                        _selectedNodes.Clear();
                        for (int i = start; i <= end; i++)
                            _selectedNodes.Add(parent.Nodes[i]);
                        e.Cancel = true;
                        HighlightSelectedNodes();
                    }
                    else
                    {
                        _selectedNodes.Clear();
                        _selectedNodes.Add(e.Node);
                    }
                };

                void HighlightSelectedNodes()
                {
                    foreach (TreeNode node in _treeView.Nodes)
                        node.BackColor = Color.Transparent;
                    foreach (var node in _selectedNodes)
                        node.BackColor = Color.LightBlue;
                }

                // Drag-and-drop
                _treeView.ItemDrag += (s, e) =>
                {
                    var paths = GetSelectedPaths(_selectedNodes);
                    if (paths.Count > 0)
                        _treeView.DoDragDrop(paths, DragDropEffects.Copy);
                };

                // Context menu for Copy
                var contextMenu = new ContextMenuStrip();
                var copyItem = new ToolStripMenuItem("Copy", null, (s, e) =>
                {
                    var files = _selectedNodes
                        .Where(n => n.Tag is string path)
                        .Select(n => n.Tag as string)
                        .ToArray();
                    if (files.Length > 0)
                    {
                        var col = new System.Collections.Specialized.StringCollection();
                        col.AddRange(files);
                        Clipboard.SetFileDropList(col);
                    }
                });

                var closeItem = new ToolStripMenuItem("Close", null, (s, e) =>
                {
                    foreach (var node in _selectedNodes.ToList())
                    {
                        if (node.Tag is string filePath)
                        {
                            FileNodeCloseRequested?.Invoke(this, filePath);
                            // Do NOT remove the node from the tree
                        }
                    }
                });

                contextMenu.Items.Add(copyItem);
                contextMenu.Items.Add(closeItem);

                _treeView.ContextMenuStrip = contextMenu;

                _treeView.NodeMouseClick += (s, e) =>
                {
                    _treeView.SelectedNode = e.Node;
                    if ((e.Button == MouseButtons.Right) && e.Node != null)
                    {
                        if (!_selectedNodes.Contains(e.Node))
                        {
                            _selectedNodes.Clear();
                            _selectedNodes.Add(e.Node);
                        }
                        _treeView.ContextMenuStrip?.Show(_treeView, e.Location);
                    }
                };

                _treeView.NodeMouseDoubleClick += (s, e) =>
                {
                    if (e.Node?.Tag is string filePath && File.Exists(filePath))
                        FileNodeActivated?.Invoke(this, filePath);
                };
            }
            else
            {
                _label = new Label
                {
                    Text = $"Content of {title}",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(_label);
            }
        }

        public void LoadFolderTree(string folderPath)
        {
            if (_treeView == null) return;
            _treeView.Nodes.Clear();
            var root = new TreeNode(Path.GetFileName(folderPath)) { Tag = folderPath };
            _treeView.Nodes.Add(root);
            LoadDirectoryRecursive(root, folderPath);
            root.Expand();
        }

        private void LoadDirectoryRecursive(TreeNode parent, string dir)
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var node = new TreeNode(Path.GetFileName(subDir)) { Tag = subDir };
                parent.Nodes.Add(node);
                LoadDirectoryRecursive(node, subDir);
            }
            foreach (var file in Directory.GetFiles(dir))
            {
                var node = new TreeNode(Path.GetFileName(file)) { Tag = file };
                parent.Nodes.Add(node);
            }
        }

        public void ApplyTheme(bool darkMode)
        {
            var bg = darkMode ? Color.FromArgb(37, 37, 38) : SystemColors.ControlLightLight;
            var fg = darkMode ? Color.Gainsboro : SystemColors.ControlText;
            BackColor = bg;
            ForeColor = fg;
            if (_label != null)
            {
                _label.BackColor = bg;
                _label.ForeColor = fg;
            }
            if (_treeView != null)
            {
                _treeView.BackColor = bg;
                _treeView.ForeColor = fg;
            }
        }

        private List<string> GetSelectedPaths(List<TreeNode> nodes)
        {
            var result = new List<string>();
            foreach (var node in nodes)
            {
                if (node.Tag is string path)
                {
                    if (File.Exists(path))
                        result.Add(path);
                    else if (Directory.Exists(path))
                        result.AddRange(GetAllFilesAndFolders(path));
                }
            }
            return result;
        }

        private List<string> GetAllFilesAndFolders(string dir)
        {
            var result = new List<string> { dir };
            foreach (var subDir in Directory.GetDirectories(dir))
                result.AddRange(GetAllFilesAndFolders(subDir));
            result.AddRange(Directory.GetFiles(dir));
            return result;
        }
    }
}