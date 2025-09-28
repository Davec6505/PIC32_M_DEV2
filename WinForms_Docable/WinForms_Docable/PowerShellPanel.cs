using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using System.Windows.Forms.Integration;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows.Media;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.AvalonEdit.Editing;
using WinForms_Docable.classes;
using WinForms_Docable.Interfaces;

namespace WinForms_Docable
{
    public class PowerShellPanel : DockContent, IThemedContent
    {
        private readonly RichTextBox _console;
        private readonly PwshProcessHost _pwsh;

        private readonly List<string> _history = new();
        private int _historyIndex = -1;

        private int _inputStart;
        private string? _pendingWorkingDir;

        public PowerShellPanel()
        {
            Text = "PowerShell";
            DockAreas = DockAreas.Document | DockAreas.DockBottom | DockAreas.Float;

            _console = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                ReadOnly = false,
                Multiline = true,
                WordWrap = false,
                HideSelection = false
            };

            Controls.Add(_console);

            _pwsh = new PwshProcessHost();
            _pwsh.Output += OnPwshOutput;
            _pwsh.Error += OnPwshOutput;

            AppendLine("pwsh process session started.");
            _inputStart = _console.TextLength;

            _console.KeyDown += OnConsoleKeyDown;
            _console.MouseDown += OnConsoleMouseDown;
            _console.SelectionChanged += OnConsoleSelectionChanged;
        }

        public void ApplyTheme(bool darkMode)
        {
            var bg = darkMode ? System.Drawing.Color.FromArgb(30, 30, 30) : System.Drawing.Color.White;
            var fg = darkMode ? System.Drawing.Color.Gainsboro : System.Drawing.Color.Black;

            BackColor = bg;
            ForeColor = fg;

            _console.BackColor = bg;
            _console.ForeColor = fg;
            _console.BorderStyle = BorderStyle.None;
        }

        private void OnPwshOutput(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(OnPwshOutput), text); return; }

            Append(text);
            _inputStart = _console.TextLength;
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();

            if (!string.IsNullOrEmpty(_pendingWorkingDir))
            {
                var dir = _pendingWorkingDir;
                _pendingWorkingDir = null;
                ApplyWorkingDirectory(dir!, refreshPrompt: false);
            }
        }

        private void Append(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Append), text); return; }
            _console.AppendText(text);
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
        }

        private void AppendLine(string text) => Append(text + Environment.NewLine);

        private void ClearConsole()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(ClearConsole)); return; }
            _console.Clear();
            _console.SelectionStart = 0;
            _inputStart = 0;
        }

        private string GetInputText()
        {
            var start = _inputStart;
            var len = Math.Max(0, _console.TextLength - start);
            return len == 0 ? string.Empty : _console.Text.Substring(start, len);
        }

        private void ReplaceInput(string text)
        {
            _console.SelectionStart = _inputStart;
            _console.SelectionLength = _console.TextLength - _inputStart;
            _console.SelectedText = text ?? string.Empty;
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
        }

        private void OnConsoleMouseDown(object? sender, MouseEventArgs e)
        {
            if (_console.SelectionStart < _inputStart)
            {
                _console.SelectionStart = _console.TextLength;
                _console.SelectionLength = 0;
            }
        }

        private void OnConsoleSelectionChanged(object? sender, EventArgs e)
        {
            if (_console.SelectionStart < _inputStart)
            {
                _console.SelectionStart = _console.TextLength;
                _console.SelectionLength = 0;
            }
        }

        private void OnConsoleKeyDown(object? sender, KeyEventArgs e)
        {
            if (_console.SelectionStart < _inputStart)
            {
                _console.SelectionStart = _console.TextLength;
                _console.SelectionLength = 0;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var cmd = GetInputText();

                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    _history.Add(cmd);
                    _historyIndex = _history.Count;
                }

                AppendLine("");
                _pwsh.SendLine(cmd);
                _inputStart = _console.TextLength;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                if (_history.Count == 0) return;
                _historyIndex = Math.Max(0, _historyIndex - 1);
                ReplaceInput(_history[_historyIndex]);
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                if (_history.Count == 0) return;
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                ReplaceInput(_historyIndex == _history.Count ? string.Empty : _history[_historyIndex]);
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                e.SuppressKeyPress = true;
                _console.SelectionStart = _inputStart;
                _console.SelectionLength = 0;
                return;
            }
            if (e.KeyCode == Keys.Left)
            {
                if (_console.SelectionStart <= _inputStart)
                {
                    e.SuppressKeyPress = true;
                    _console.SelectionStart = _inputStart;
                    _console.SelectionLength = 0;
                    return;
                }
            }
            if (e.KeyCode == Keys.Back)
            {
                if (_console.SelectionStart <= _inputStart)
                {
                    e.SuppressKeyPress = true;
                    return;
                }
            }
            if (e.KeyCode == Keys.Delete)
            {
                var selStart = Math.Min(_console.SelectionStart, _console.SelectionStart + _console.SelectionLength);
                if (selStart < _inputStart)
                {
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                AppendLine("^C (cancel not available for external pwsh host)");
                _inputStart = _console.TextLength;
                return;
            }

            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                var text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text)) return;
                _console.SelectedText = text;
            }
        }

        public void SetWorkingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

            if (!_pwsh.IsRunning)
            {
                _pendingWorkingDir = path;
                return;
            }

            ApplyWorkingDirectory(path, refreshPrompt: false);
        }

        private static string QuotePwsh(string path) => "'" + path.Replace("'", "''") + "'";

        private void ApplyWorkingDirectory(string path, bool refreshPrompt)
        {
            try
            {
                _pwsh.SendLine($"Set-Location -LiteralPath {QuotePwsh(path)}");
                if (refreshPrompt && string.IsNullOrEmpty(GetInputText()))
                    ReplaceInput(string.Empty);
            }
            catch (Exception ex)
            {
                AppendLine($"Set-Location error: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pwsh?.Dispose();

                _console.KeyDown -= OnConsoleKeyDown;
                _console.MouseDown -= OnConsoleMouseDown;
                _console.SelectionChanged -= OnConsoleSelectionChanged;
            }
            base.Dispose(disposing);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_pwsh.IsRunning)
                _pwsh.Start("-NoLogo -NoExit");
        }
    }
}