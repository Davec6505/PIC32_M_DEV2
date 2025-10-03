using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using PIC32_M_DEV.Properties;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Xml;
using WeifenLuo.WinFormsUI.Docking;
using PIC32_M_DEV.classes; // for ThemeManager and EditorLanguage

namespace PIC32_M_DEV
{
    // Ensure only one definition of CodeEditorPanel exists in this namespace.
    public class CodeEditorPanel : DockContent
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            private set => _filePath = value;
        }

        private readonly ElementHost _host;
        private readonly TextEditor _avalon;
        private static bool _customHighlightingRegistered;

        // Remove any duplicate constructors for CodeEditorPanel in this file.
        public CodeEditorPanel(string? filePath = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Text = Path.GetFileName(FilePath);

            RegisterCustomHighlightings(Settings.Default.DarkMode);

            _host = new ElementHost { Dock = DockStyle.Fill };
            _avalon = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            _avalon.Options.ConvertTabsToSpaces = true;
            _avalon.Options.IndentationSize = 4;
            _avalon.Options.EnableHyperlinks = false;
            _avalon.Options.EnableEmailHyperlinks = false;
            _avalon.Options.HighlightCurrentLine = true;

            _host.Child = _avalon;
            Controls.Add(_host);

            if (File.Exists(FilePath))
            {
                _avalon.Text = File.ReadAllText(FilePath);
            }

            ApplySyntaxHighlighting();

            // Apply user-selected theme for this file type
            ApplyLanguageThemeFromManager();

            // Right-click context menu for Save / Close
            InitializeEditorContextMenu();

            _avalon.TextChanged += (s, e) =>
            {
                if (!Text.EndsWith("*", StringComparison.Ordinal))
                    Text += "*";
            };

            this.FormClosed += (s, e) =>
            {
                _host.Dispose();
            };
        }

        private void InitializeEditorContextMenu()
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var saveItem = new System.Windows.Controls.MenuItem { Header = "Save" };
            saveItem.Click += (s, e) =>
            {
                try
                {
                    SaveToFile();
                    if (Text.EndsWith("*", StringComparison.Ordinal))
                        Text = Text.TrimEnd('*');
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var themeParent = new System.Windows.Controls.MenuItem { Header = "Theme" };
            PopulateThemeMenu(themeParent);

            var closeItem = new System.Windows.Controls.MenuItem { Header = "Close" };
            closeItem.Click += (s, e) =>
            {
                Close();
            };

            menu.Items.Add(saveItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(themeParent);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(closeItem);

            _avalon.TextArea.ContextMenu = menu;
        }

        private void PopulateThemeMenu(System.Windows.Controls.MenuItem themeParent)
        {
            themeParent.Items.Clear();
            var lang = DetectLanguage();
            var current = ThemeManager.GetCurrentTheme(lang).Name;

            foreach (var t in ThemeManager.GetThemes(lang))
            {
                var item = new System.Windows.Controls.MenuItem { Header = t.Name, IsCheckable = true, IsChecked = string.Equals(t.Name, current, StringComparison.Ordinal) };
                item.Click += (s, e) =>
                {
                    ThemeManager.SetCurrentTheme(lang, t.Name);
                    ApplyLanguageThemeFromManager();
                    // Refresh checkmarks
                    PopulateThemeMenu(themeParent);
                };
                themeParent.Items.Add(item);
            }
        }

        private static void RegisterCustomHighlightings(bool? darkMode = null)
        {
            if (_customHighlightingRegistered) return;
            _customHighlightingRegistered = true;

            IHighlightingDefinition? LoadFromResource(string pathOrName)
            {
                string path = Path.IsPathRooted(pathOrName)
                    ? pathOrName
                    : Path.Combine(AppContext.BaseDirectory, "Highlighting", pathOrName);

                if (!File.Exists(path))
                    throw new InvalidOperationException($"Missing highlighting file: {path}");

                using var s = File.OpenRead(path);
                using var reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var xshd = HighlightingLoader.LoadXshd(reader);
                return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }

            // Makefile
            var name = darkMode == true ? "MakefileDark.xshd" : "Makefile.xshd";
            var makefile = LoadFromResource(name);
            if (makefile != null)
            {
                HighlightingManager.Instance.RegisterHighlighting(
                    "Makefile",
                    new[] { ".mak", ".make", ".mk" },
                    makefile);
            }

            // GAS/ASM
            var gas = LoadFromResource(darkMode == true ? "GASDark.xshd" : "GAS.xshd");
            if (gas != null)
            {
                HighlightingManager.Instance.RegisterHighlighting(
                    "GAS",
                    new[] { ".s", ".S", ".asm" },
                    gas);
            }

            // C/C++ Highlighting
            var c = LoadFromResource(darkMode == true ? "CDark.xshd" : "C.xshd");
            if (c != null)
            {
                HighlightingManager.Instance.RegisterHighlighting(
                    "C/C++",
                    new[] { ".c", ".h" },
                    c);
            }
        }

        private static string EscapeWords(string spaceSeparatedWords)
        {
            return spaceSeparatedWords;
        }

        private void ApplySyntaxHighlighting()
        {
            var ext = Path.GetExtension(FilePath);
            var fileName = Path.GetFileName(FilePath);
            IHighlightingDefinition? def = null;

            if (!string.IsNullOrEmpty(ext))
                def = HighlightingManager.Instance.GetDefinitionByExtension(ext);

            // Handle Makefiles with no extension
            if (def == null &&
                (fileName.Equals("Makefile", StringComparison.OrdinalIgnoreCase) ||
                 fileName.Equals("GNUmakefile", StringComparison.OrdinalIgnoreCase)))
            {
                def = HighlightingManager.Instance.GetDefinition("Makefile");
            }

            // Fallback to built-in C# mapping
            if (def == null && ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                def = HighlightingManager.Instance.GetDefinition("C#");

            _avalon.SyntaxHighlighting = def;
        }

        // Basic brace-based formatter (no external packages).
        public void FormatDocument()
        {
            var text = _avalon.Text;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(text.Length + 256);
            int indent = 0;

            foreach (var raw in lines)
            {
                var trimmed = raw.Trim();

                if (trimmed.Length == 0)
                {
                    sb.AppendLine();
                    continue;
                }

                if (trimmed.StartsWith("}", StringComparison.Ordinal))
                    indent = Math.Max(0, indent - 1);

                sb.Append(new string(' ', indent * _avalon.Options.IndentationSize));
                sb.AppendLine(trimmed);

                if (trimmed.EndsWith("{", StringComparison.Ordinal))
                    indent++;
            }

            _avalon.Text = sb.ToString();
        }

        // Optional helper to save current content back to disk.
        public void SaveToFile(string? newPath = null)
        {
            var path = newPath ?? FilePath;
            File.WriteAllText(path, _avalon.Text);
            // Optionally update FilePath if saving as a new file
            if (newPath != null)
                FilePath = newPath;
        }

        // THEME: Apply dark/light to the embedded AvalonEdit editor and host.
        public void ApplyTheme(bool darkMode)
        {
            // WinForms host surface (prevents white flashes around WPF content)
            _host.BackColor = darkMode
                ? System.Drawing.Color.FromArgb(0, 0, 0)
                : System.Drawing.Color.White;

            // Editor surface
            var bg = darkMode ? Colors.Black : Colors.White;
            var fg = darkMode ? Colors.White : Colors.Black;

            _avalon.Background = new SolidColorBrush(bg);
            _avalon.Foreground = new SolidColorBrush(fg);

            _avalon.TextArea.Background = _avalon.Background;
            _avalon.TextArea.Foreground = _avalon.Foreground;

            // Caret, selection, current line
            _avalon.TextArea.Caret.CaretBrush = new SolidColorBrush(darkMode ? Colors.White : Colors.Black);
            _avalon.TextArea.SelectionBrush = new SolidColorBrush(darkMode
                ? System.Windows.Media.Color.FromRgb(62, 94, 138)
                : System.Windows.Media.Color.FromRgb(173, 214, 255));
            _avalon.TextArea.SelectionForeground = null; // default contrast
            _avalon.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(darkMode
                ? System.Windows.Media.Color.FromRgb(45, 45, 48)
                : System.Windows.Media.Color.FromRgb(232, 242, 254));
            _avalon.TextArea.TextView.CurrentLineBorder = null;

            _avalon.LineNumbersForeground = new SolidColorBrush(darkMode ? System.Windows.Media.Color.FromRgb(190, 190, 190) : System.Windows.Media.Colors.Black);
            // Optional: if you have theme-wide XSHD files, try to apply them.
            TryApplySyntaxTheme(darkMode);

            // Finally, re-apply user overrides for the detected language (wins over global light/dark)
            ApplyLanguageThemeFromManager();
        }

        private void TryApplySyntaxTheme(bool darkMode)
        {
            // Looks for Highlighting\DarkTheme.xshd or Highlighting\LightTheme.xshd next to the app
            var themeFile = darkMode ? @"Highlighting\DarkTheme.xshd" : @"Highlighting\LightTheme.xshd";
            var path = Path.Combine(AppContext.BaseDirectory, themeFile);
            if (!File.Exists(path)) return;

            using var s = File.OpenRead(path);
            using var reader = new XmlTextReader(s);
            var xshd = HighlightingLoader.LoadXshd(reader);
            var def = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            _avalon.SyntaxHighlighting = def;
        }

        private EditorLanguage DetectLanguage()
        {
            var fileName = Path.GetFileName(FilePath);
            var ext = Path.GetExtension(FilePath).ToLowerInvariant();

            if (ext is ".c" or ".h") return EditorLanguage.C;
            if (ext is ".s" or ".asm" or ".s64" or ".s32" or ".s16" || ext == ".S") return EditorLanguage.Asm;
            if (ext is ".mak" or ".make" or ".mk") return EditorLanguage.Makefile;
            if (fileName.Equals("Makefile", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("GNUmakefile", StringComparison.OrdinalIgnoreCase))
                return EditorLanguage.Makefile;

            // Default to C if unknown
            return EditorLanguage.C;
        }

        private void ApplyLanguageThemeFromManager()
        {
            var lang = DetectLanguage();
            ThemeManager.ApplyTo(_avalon, lang);
        }
    }
}
