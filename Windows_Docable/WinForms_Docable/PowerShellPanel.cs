using PIC32_M_DEV.classes;
using PIC32_M_DEV.Interfaces;
using PIC32_M_DEV.Properties;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;

namespace PIC32_M_DEV
{
    public class PowerShellPanel : DockContent, IThemedContent
    {
        private readonly RichTextBox _console;
        private readonly PwshProcessHost _pwsh;

        private readonly List<string> _history = new();
        private int _historyIndex = -1;

        private int _inputStart;
        private string? _pendingWorkingDir;
        private bool _suppressInitialOutput = false; // suppress pwsh banner/appdomain output
        private string? _currentDir; // track current dir for prompt

        // Marker to know when a command finished so we can draw next prompt
        private const string PromptMarker = "[[__PWSH_PROMPT_READY__]]";

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

            // Start with a clean prompt based on the project path (no banner)
            _inputStart = 0;
            _currentDir = GetCurrentDirectory();
            ClearConsole();

            _console.KeyDown += OnConsoleKeyDown;
            _console.MouseDown += OnConsoleMouseDown;
            _console.SelectionChanged += OnConsoleSelectionChanged;
        }

        public void ShowProjectDirectory()
        {
            _currentDir = GetCurrentDirectory();
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

            // Suppress the initial banner/appdomain output and show our own prompt instead
            if (_suppressInitialOutput)
            {
                _suppressInitialOutput = false;
                // After starting, ensure we show the prompt and working dir
                _currentDir = string.IsNullOrWhiteSpace(_pendingWorkingDir) ? GetCurrentDirectory() : _pendingWorkingDir;
                _pendingWorkingDir = null;
                ClearConsole();
                return;
            }

            // If the output contains our marker, split and draw the prompt
            int markerIndex = text.IndexOf(PromptMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                string before = text.Substring(0, markerIndex);
                if (!string.IsNullOrEmpty(before))
                    Append(before);

                // Ensure we end with a single newline before next prompt (handle CR-only edge)
                if (_console.TextLength > 0)
                {
                    char last = _console.Text[_console.TextLength - 1];
                    if (last != '\n')
                        Append(Environment.NewLine);
                }

                WritePrompt();
                return;
            }

            Append(text);
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
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

        private void WritePrompt()
        {
            var dir = _currentDir ?? GetCurrentDirectory();
            var prompt = string.IsNullOrWhiteSpace(dir) ? "> " : $"PS {dir}> ";
            Append(prompt);
            _inputStart = _console.TextLength;
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
        }

        private void ClearConsole()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(ClearConsole)); return; }
            _console.Clear();
            _console.SelectionStart = 0;
            _inputStart = 0;
            _console.ScrollToCaret();
            // Show prompt on the same line as input
            WritePrompt();
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
                    WritePrompt();
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
                                _currentDir = path;
                                // Move to next line (like real console), then change dir and ask for prompt
                                Append(Environment.NewLine);
                                _pwsh.SendLine($"Set-Location -LiteralPath {QuotePwsh(path)}; Write-Host -NoNewline '{PromptMarker}'");
                                return;
                            }
                            else
                            {
                                AppendLine($"The system cannot find the path specified: {path}");
                                WritePrompt();
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

                // Move to next line like the real console does when you press Enter
                Append(Environment.NewLine);
                // Send with a marker that lets us know when to draw the next prompt
                _pwsh.SendLine($"{cmd}; Write-Host -NoNewline '{PromptMarker}'");
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
                WritePrompt();
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
                _currentDir = path;
                return;
            }

            ApplyWorkingDirectory(path, refreshPrompt: true);
        }

        private static string QuotePwsh(string path) => "'" + path.Replace("'", "''") + "'";

        private void ApplyWorkingDirectory(string path, bool refreshPrompt)
        {
            try
            {
                _currentDir = path;
                _pwsh.SendLine($"Set-Location -LiteralPath {QuotePwsh(path)}; Write-Host -NoNewline '{PromptMarker}'");
                // Prompt will be written when we see the marker
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
                // Unsubscribe events
                _console.KeyDown -= OnConsoleKeyDown;
                _console.MouseDown -= OnConsoleMouseDown;
                _console.SelectionChanged -= OnConsoleSelectionChanged;

                // Dispose managed resources
                _pwsh?.Dispose();
                _console?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_pwsh.IsRunning)
            {
                // Suppress the initial banner so we can show our project prompt first
                _suppressInitialOutput = true;
                _pwsh.Start("-NoLogo -NoExit");
                // Make output simple and disable PSReadLine/prompt so we can control it
                _pwsh.SendLine("$PSStyle.OutputRendering = 'PlainText'");
                _pwsh.SendLine("Remove-Module PSReadLine -ErrorAction SilentlyContinue");
                _pwsh.SendLine("function global:prompt {''}");

                var projectDir = GetCurrentDirectory();
                if (!string.IsNullOrWhiteSpace(projectDir) && Directory.Exists(projectDir))
                {
                    _pendingWorkingDir = projectDir;
                    _currentDir = projectDir;
                }
            }
        }

        public string GetCurrentDirectory()
        {
            try
            {
                return Settings.Default.projectPath;
            }
            catch
            {
                return "";
            }
        }
    }
}