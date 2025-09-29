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
using WinForms_Docable.Properties;

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

        public void ShowProjectDirectory()
        {
            ClearConsole();
        }

        public void ApplyTheme(bool darkMode)
        {
            var bg = darkMode ? System.Drawing.Color.FromArgb(3, 3, 3) : System.Drawing.Color.White;
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
            if (_console.InvokeRequired) { _console.BeginInvoke(new Action<string>(Append), text); return; }

            // Simple ANSI color handling (only foreground colors)
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '\x1B' && i + 2 < text.Length && text[i + 1] == '[')
                {
                    int mIndex = text.IndexOf('m', i + 2);
                    if (mIndex > i)
                    {
                        string code = text.Substring(i + 2, mIndex - (i + 2));
                        ApplyAnsiCode(code);
                        i = mIndex + 1;
                        continue;
                    }
                }
                _console.AppendText(text[i].ToString());
                i++;
            }
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
        }

        // Example: Only handles reset and red foreground
        private void ApplyAnsiCode(string code)
        {
            if (code == "0") // reset
            {
                _console.SelectionColor = _console.ForeColor;
            }
            else if (code == "31") // red
            {
                _console.SelectionColor = System.Drawing.Color.Red;
            }
            // Add more codes as needed
        }

        private void AppendLine(string text) => Append(text + Environment.NewLine);

        private void ClearConsole()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(ClearConsole)); return; }
            _console.Clear();
            _console.SelectionStart = 0;
            _inputStart = 0;
            _console.ScrollToCaret();
            Append(GetCurrentDirectory());
            _inputStart = _console.TextLength;
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
          //  if (_console.SelectionStart < _inputStart)
          //  {
          //      _console.SelectionStart = _console.TextLength;
          //      _console.SelectionLength = 0;
          //  }
        }

        private void OnConsoleSelectionChanged(object? sender, EventArgs e)
        {
          //  if (_console.SelectionStart < _inputStart)
          //  {
          //      _console.SelectionStart = _console.TextLength;
          //      _console.SelectionLength = 0;
          //  }
        }

        private void OnConsoleKeyDown(object? sender, KeyEventArgs e)
        {
            // Ignore Ctrl pressed on its own
            if (e.KeyCode == Keys.ControlKey)
                return;

            // Handle Ctrl+C (copy)
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (_console.SelectionLength > 0)
                {
                    Clipboard.SetText(_console.SelectedText);
                }
                e.SuppressKeyPress = true;
                return;
            }

            // Handle Ctrl+V (paste)
            if (e.Control && e.KeyCode == Keys.V)
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                    _console.SelectedText = text;
                e.SuppressKeyPress = true;
                return;
            }

            // Prevent Enter from triggering on Ctrl alone
            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.Control)
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (_console.SelectionStart < _inputStart)
            {
                _console.SelectionStart = _console.TextLength;
                _console.SelectionLength = 0;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                var cmd = GetInputText();


                //copy paste support
                if (e.Control && e.KeyCode == Keys.C)
                {
                    if (_console.SelectionLength > 0)
                    {
                        Clipboard.SetText(_console.SelectedText);
                        e.SuppressKeyPress = true;
                        return;
                    }
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

                // Handle special commands internally
                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    _history.Add(cmd);
                    _historyIndex = _history.Count;

                    if(cmd.Equals("clear", StringComparison.OrdinalIgnoreCase) || cmd.Equals("cls", StringComparison.OrdinalIgnoreCase))
                    {
                        ClearConsole();
                        _inputStart = 0;
                        return;
                    }

                    else if (cmd.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
                         cmd.StartsWith("chdir ", StringComparison.OrdinalIgnoreCase) ||
                         cmd.StartsWith("Set-Location ", StringComparison.OrdinalIgnoreCase) ||
                         cmd.StartsWith("sl ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = cmd.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            var path = parts[1].Trim().Trim('\'', '"');
                            if (Directory.Exists(path))
                            {
                                ApplyWorkingDirectory(path, refreshPrompt: true);
                                AppendLine(path);
                                _inputStart = _console.TextLength;
                                return;
                            }
                            else
                            {
                               // AppendLine(cmd);
                                AppendLine($"The system cannot find the path specified: {path}");
                                _inputStart = _console.TextLength;
                                return;
                            }
                        }
                    }
                    else if (cmd.Equals("ls", StringComparison.OrdinalIgnoreCase) || cmd.Equals("dir", StringComparison.OrdinalIgnoreCase) || cmd.Equals("Get-ChildItem", StringComparison.OrdinalIgnoreCase) || cmd.Equals("gci", StringComparison.OrdinalIgnoreCase))
                    {
                        // Translate 'ls' or 'dir' to 'Get-ChildItem | Format-Table -AutoSize' for better output formatting
                        cmd = "Get-ChildItem | Format-Table -AutoSize";
                    }
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
                _pwsh.SendLine($"Set-Location -LiteralPath {QuotePwsh(GetCurrentDirectory())}");
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
            {
                _pwsh.Start("-NoLogo -NoExit");
                _pwsh.SendLine("$PSStyle.OutputRendering = 'PlainText'");
            }
        }

        public string GetCurrentDirectory()
        {
            try
            {
                return Settings.Default.projectPath;//Directory.GetCurrentDirectory();
            }
            catch
            {
                return "";
            }
        }
    }
}