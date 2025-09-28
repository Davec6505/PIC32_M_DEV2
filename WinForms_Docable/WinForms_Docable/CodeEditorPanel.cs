using WeifenLuo.WinFormsUI.Docking;
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
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
using WinForms_Docable.Properties;

namespace WinForms_Docable
{
    // Ensure only one definition of CodeEditorPanel exists in this namespace.
    public class CodeEditorPanel : DockContent
    {
        // Mark fields as 'readonly' and initialize them in the constructor.
        public string FilePath { get; }
        private readonly ElementHost _host;
        private readonly TextEditor _avalon;
        private static bool _customHighlightingRegistered;

        // Remove any duplicate constructors for CodeEditorPanel in this file.
        public CodeEditorPanel(string filePath)
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

        private static void RegisterCustomHighlightings(bool? darkMode = null)
        {
           
            if (_customHighlightingRegistered) return;
            _customHighlightingRegistered = true;



            IHighlightingDefinition LoadFromResource(string resourceName)
            {
                var asm = Assembly.GetExecutingAssembly();
                using var s = asm.GetManifestResourceStream(resourceName);
                if (s == null)
                    throw new InvalidOperationException($"Missing highlighting resource: {resourceName}");
                using var reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                try
                {
                    var xshd = HighlightingLoader.LoadXshd(reader);
                     return HighlightingLoader.Load(xshd, HighlightingManager.Instance);

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load syntax highlighting: {resourceName}: {ex.Message}","Error in Syntax Highlighting!",MessageBoxButtons.OK,MessageBoxIcon.Error);
                }
                return null;
            }

            // Makefile
            IHighlightingDefinition? makefile = null;
            if (darkMode.HasValue.Equals(true))
            {
                 makefile = LoadFromResource("WinForms_Docable.Highlighting.MakefileDark.xshd");
            }
            else
            {
                 makefile = LoadFromResource("WinForms_Docable.Highlighting.Makefile.xshd");               
            }
            if (makefile != null)
            {
                HighlightingManager.Instance.RegisterHighlighting(
                    "Makefile",
                    new[] { ".mak", ".make", ".mk" },
                    makefile);
            }

            // GAS/ASM
            IHighlightingDefinition? gas = null;
            if (darkMode.HasValue.Equals(true))
            {
                gas = LoadFromResource("WinForms_Docable.Highlighting.GASDark.xshd");
            }
            else
            {
                 gas = LoadFromResource("WinForms_Docable.Highlighting.GAS.xshd");
            }
            if (gas != null)
            {
                HighlightingManager.Instance.RegisterHighlighting(
                "GAS",
                new[] { ".s", ".S", ".asm" },
                gas);
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
        public void SaveToFile()
        {
            File.WriteAllText(FilePath, _avalon.Text);
            Text = Path.GetFileName(FilePath); // clear dirty mark
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
           // _avalon.Background = new SolidColorBrush(darkMode ? System.Windows.Media.Color.FromRgb(30, 30, 30) : System.Windows.Media.Colors.Black);
            // Optional: if you have theme-wide XSHD files, try to apply them.
            TryApplySyntaxTheme(darkMode);
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
    }
}
