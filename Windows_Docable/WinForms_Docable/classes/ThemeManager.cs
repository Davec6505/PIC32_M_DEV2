using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using MediaColor = System.Windows.Media.Color;

namespace PIC32_M_DEV.classes
{
    public enum EditorLanguage
    {
        C,
        Asm,
        Makefile
    }

    public sealed class EditorTheme
    {
        public string Name { get; set; } = "Custom";

        // Editor-surface colors
        public MediaColor EditorBackground { get; set; } = Colors.White;
        public MediaColor EditorForeground { get; set; } = Colors.Black;
        public MediaColor Caret { get; set; } = Colors.Black;
        public MediaColor Selection { get; set; } = MediaColor.FromRgb(173, 214, 255);
        public MediaColor CurrentLine { get; set; } = MediaColor.FromRgb(232, 242, 254);
        public MediaColor LineNumbers { get; set; } = Colors.Black;

        // Syntax named colors override (keys must match NamedHighlightingColors in your XSHD)
        public Dictionary<string, MediaColor> Syntax { get; set; } = new();

        public EditorTheme Clone()
        {
            return new EditorTheme
            {
                Name = this.Name,
                EditorBackground = this.EditorBackground,
                EditorForeground = this.EditorForeground,
                Caret = this.Caret,
                Selection = this.Selection,
                CurrentLine = this.CurrentLine,
                LineNumbers = this.LineNumbers,
                Syntax = this.Syntax.ToDictionary(k => k.Key, v => v.Value)
            };
        }
    }

    public static class ThemeManager
    {
        // Simple presets; extend as needed
        private static readonly Dictionary<EditorLanguage, List<EditorTheme>> Presets = CreatePresets();

        // User custom theme per language (optional, persisted)
        private static readonly Dictionary<EditorLanguage, EditorTheme> Custom = new();

        // Current selection per language (persisted)
        private static readonly Dictionary<EditorLanguage, string> Current = new();

        private static string StorePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "PIC32_M_DEV", "themes.json");

        private sealed class SerializableEditorTheme
        {
            public string Name { get; set; } = "Custom";
            public string EditorBackground { get; set; } = "#FFFFFFFF";
            public string EditorForeground { get; set; } = "#FF000000";
            public string Caret { get; set; } = "#FF000000";
            public string Selection { get; set; } = "#FFADD6FF";
            public string CurrentLine { get; set; } = "#FFE8F2FE";
            public string LineNumbers { get; set; } = "#FF000000";
            public Dictionary<string, string> Syntax { get; set; } = new();
        }

        private sealed class PersistModel
        {
            public Dictionary<EditorLanguage, string> Current { get; set; } = new();
            public Dictionary<EditorLanguage, SerializableEditorTheme> Custom { get; set; } = new();
        }

        static ThemeManager()
        {
            try { Load(); } catch { /* ignore */ }
        }

        public static IEnumerable<EditorTheme> GetThemes(EditorLanguage lang)
        {
            var list = new List<EditorTheme>();
            if (Custom.TryGetValue(lang, out var c)) list.Add(c);
            list.AddRange(Presets[lang]);
            return list;
        }

        public static EditorTheme GetCurrentTheme(EditorLanguage lang)
        {
            var list = GetThemes(lang).ToList();
            if (Current.TryGetValue(lang, out var name))
            {
                var found = list.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
                if (found != null) return found;
            }
            return list[0];
        }

        public static void SetCurrentTheme(EditorLanguage lang, string themeName, bool save = true)
        {
            var exists = GetThemes(lang).Any(t => t.Name == themeName);
            if (!exists) return;

            Current[lang] = themeName;
            if (save) Save();
        }

        public static EditorTheme GetEditableTheme(EditorLanguage lang)
        {
            return GetCurrentTheme(lang).Clone();
        }

        public static void SaveCustomTheme(EditorLanguage lang, EditorTheme theme, bool setCurrent = true)
        {
            if (string.IsNullOrWhiteSpace(theme.Name)) theme.Name = "Custom";
            Custom[lang] = theme.Clone();
            if (setCurrent)
            {
                Current[lang] = theme.Name;
            }
            Save();
        }

        public static void ApplyTo(TextEditor editor, EditorLanguage lang)
        {
            var theme = GetCurrentTheme(lang);

            // Editor surface
            editor.Background = new SolidColorBrush(theme.EditorBackground);
            editor.Foreground = new SolidColorBrush(theme.EditorForeground);
            editor.TextArea.Background = editor.Background;
            editor.TextArea.Foreground = editor.Foreground;

            editor.TextArea.Caret.CaretBrush = new SolidColorBrush(theme.Caret);
            editor.TextArea.SelectionBrush = new SolidColorBrush(theme.Selection);
            editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(theme.CurrentLine);
            editor.TextArea.TextView.CurrentLineBorder = null;
            editor.LineNumbersForeground = new SolidColorBrush(theme.LineNumbers);

            // Syntax named colors
            var def = editor.SyntaxHighlighting;
            if (def != null && theme.Syntax.Count > 0)
            {
                foreach (var kv in theme.Syntax)
                {
                    var named = def.GetNamedColor(kv.Key);
                    if (named != null)
                    {
                        named.Foreground = new SimpleHighlightingBrush(kv.Value);
                        // You can also set named.Background if your XSHD uses it.
                    }
                }
            }
            editor.TextArea.TextView.Redraw(); // ensure visuals refresh
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                var model = new PersistModel { Current = Current };

                foreach (var kv in Custom)
                {
                    model.Custom[kv.Key] = ToSerializable(kv.Value);
                }

                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StorePath, json);
            }
            catch { /* ignore */ }
        }

        public static void Load()
        {
            if (!File.Exists(StorePath)) return;
            try
            {
                var json = File.ReadAllText(StorePath);
                var model = JsonSerializer.Deserialize<PersistModel>(json);
                if (model != null)
                {
                    Current.Clear();
                    foreach (var kv in model.Current) Current[kv.Key] = kv.Value;

                    Custom.Clear();
                    foreach (var kv in model.Custom)
                    {
                        Custom[kv.Key] = FromSerializable(kv.Value);
                    }
                }
            }
            catch { /* ignore */ }
        }

        private static SerializableEditorTheme ToSerializable(EditorTheme t)
        {
            return new SerializableEditorTheme
            {
                Name = t.Name,
                EditorBackground = ToHex(t.EditorBackground),
                EditorForeground = ToHex(t.EditorForeground),
                Caret = ToHex(t.Caret),
                Selection = ToHex(t.Selection),
                CurrentLine = ToHex(t.CurrentLine),
                LineNumbers = ToHex(t.LineNumbers),
                Syntax = t.Syntax.ToDictionary(k => k.Key, v => ToHex(v.Value))
            };
        }

        private static EditorTheme FromSerializable(SerializableEditorTheme s)
        {
            return new EditorTheme
            {
                Name = s.Name,
                EditorBackground = FromHex(s.EditorBackground),
                EditorForeground = FromHex(s.EditorForeground),
                Caret = FromHex(s.Caret),
                Selection = FromHex(s.Selection),
                CurrentLine = FromHex(s.CurrentLine),
                LineNumbers = FromHex(s.LineNumbers),
                Syntax = s.Syntax.ToDictionary(k => k.Key, v => FromHex(v.Value))
            };
        }

        private static string ToHex(MediaColor c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        private static MediaColor FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
            if (hex[0] == '#') hex = hex[1..];
            if (hex.Length == 6)
            {
                // assume opaque
                var r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return MediaColor.FromRgb(r, g, b);
            }
            if (hex.Length == 8)
            {
                var a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                var r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return MediaColor.FromArgb(a, r, g, b);
            }
            return Colors.Transparent;
        }

        private static Dictionary<EditorLanguage, List<EditorTheme>> CreatePresets()
        {
            var dict = new Dictionary<EditorLanguage, List<EditorTheme>>();

            // C - Light
            var cLightSyntax = new Dictionary<string, MediaColor>();
            cLightSyntax["Keyword"] = MediaColor.FromRgb(0, 0, 192);
            cLightSyntax["Comment"] = MediaColor.FromRgb(0, 128, 0);
            cLightSyntax["String"] = MediaColor.FromRgb(163, 21, 21);
            cLightSyntax["Number"] = MediaColor.FromRgb(128, 0, 128);
            cLightSyntax["Preprocessor"] = MediaColor.FromRgb(128, 128, 128);
            cLightSyntax["Type"] = MediaColor.FromRgb(43, 145, 175);
            cLightSyntax["Operator"] = MediaColor.FromRgb(0, 0, 0);
            cLightSyntax["Function"] = MediaColor.FromRgb(0, 0, 0);

            var cLight = new EditorTheme
            {
                Name = "C - Light",
                EditorBackground = Colors.White,
                EditorForeground = Colors.Black,
                Caret = Colors.Black,
                Selection = MediaColor.FromRgb(173, 214, 255),
                CurrentLine = MediaColor.FromRgb(232, 242, 254),
                LineNumbers = Colors.Black,
                Syntax = cLightSyntax
            };

            // C - Dark
            var cDarkSyntax = new Dictionary<string, MediaColor>();
            cDarkSyntax["Keyword"] = MediaColor.FromRgb(86, 156, 214);
            cDarkSyntax["Comment"] = MediaColor.FromRgb(87, 166, 74);
            cDarkSyntax["String"] = MediaColor.FromRgb(214, 157, 133);
            cDarkSyntax["Number"] = MediaColor.FromRgb(181, 206, 168);
            cDarkSyntax["Preprocessor"] = MediaColor.FromRgb(146, 146, 146);
            cDarkSyntax["Type"] = MediaColor.FromRgb(78, 201, 176);
            cDarkSyntax["Operator"] = MediaColor.FromRgb(220, 220, 220);
            cDarkSyntax["Function"] = MediaColor.FromRgb(220, 220, 170);

            var cDark = new EditorTheme
            {
                Name = "C - Dark",
                EditorBackground = Colors.Black,
                EditorForeground = Colors.White,
                Caret = Colors.White,
                Selection = MediaColor.FromRgb(62, 94, 138),
                CurrentLine = MediaColor.FromRgb(45, 45, 48),
                LineNumbers = MediaColor.FromRgb(190, 190, 190),
                Syntax = cDarkSyntax
            };

            dict[EditorLanguage.C] = new List<EditorTheme> { cLight, cDark };

            // ASM - Light
            var asmLightSyntax = new Dictionary<string, MediaColor>();
            asmLightSyntax["Directive"] = MediaColor.FromRgb(0, 0, 192);
            asmLightSyntax["Comment"] = MediaColor.FromRgb(0, 128, 0);
            asmLightSyntax["String"] = MediaColor.FromRgb(163, 21, 21);
            asmLightSyntax["Number"] = MediaColor.FromRgb(128, 0, 128);
            asmLightSyntax["Label"] = MediaColor.FromRgb(43, 145, 175);
            asmLightSyntax["Operator"] = MediaColor.FromRgb(0, 0, 0);

            var asmLight = new EditorTheme
            {
                Name = "ASM - Light",
                EditorBackground = Colors.White,
                EditorForeground = Colors.Black,
                Caret = Colors.Black,
                Selection = MediaColor.FromRgb(173, 214, 255),
                CurrentLine = MediaColor.FromRgb(232, 242, 254),
                LineNumbers = Colors.Black,
                Syntax = asmLightSyntax
            };

            // ASM - Dark
            var asmDarkSyntax = new Dictionary<string, MediaColor>();
            asmDarkSyntax["Directive"] = MediaColor.FromRgb(86, 156, 214);
            asmDarkSyntax["Comment"] = MediaColor.FromRgb(87, 166, 74);
            asmDarkSyntax["String"] = MediaColor.FromRgb(214, 157, 133);
            asmDarkSyntax["Number"] = MediaColor.FromRgb(181, 206, 168);
            asmDarkSyntax["Label"] = MediaColor.FromRgb(78, 201, 176);
            asmDarkSyntax["Operator"] = MediaColor.FromRgb(220, 220, 220);

            var asmDark = new EditorTheme
            {
                Name = "ASM - Dark",
                EditorBackground = Colors.Black,
                EditorForeground = Colors.White,
                Caret = Colors.White,
                Selection = MediaColor.FromRgb(62, 94, 138),
                CurrentLine = MediaColor.FromRgb(45, 45, 48),
                LineNumbers = MediaColor.FromRgb(190, 190, 190),
                Syntax = asmDarkSyntax
            };

            dict[EditorLanguage.Asm] = new List<EditorTheme> { asmLight, asmDark };

            // Makefile - Light
            var mkLightSyntax = new Dictionary<string, MediaColor>();
            mkLightSyntax["Keyword"] = MediaColor.FromRgb(0, 0, 192);
            mkLightSyntax["Comment"] = MediaColor.FromRgb(0, 128, 0);
            mkLightSyntax["String"] = MediaColor.FromRgb(163, 21, 21);
            mkLightSyntax["Variable"] = MediaColor.FromRgb(128, 0, 128);
            mkLightSyntax["Target"] = MediaColor.FromRgb(43, 145, 175);

            var mkLight = new EditorTheme
            {
                Name = "Makefile - Light",
                EditorBackground = Colors.White,
                EditorForeground = Colors.Black,
                Caret = Colors.Black,
                Selection = MediaColor.FromRgb(173, 214, 255),
                CurrentLine = MediaColor.FromRgb(232, 242, 254),
                LineNumbers = Colors.Black,
                Syntax = mkLightSyntax
            };

            // Makefile - Dark
            var mkDarkSyntax = new Dictionary<string, MediaColor>();
            mkDarkSyntax["Keyword"] = MediaColor.FromRgb(86, 156, 214);
            mkDarkSyntax["Comment"] = MediaColor.FromRgb(87, 166, 74);
            mkDarkSyntax["String"] = MediaColor.FromRgb(214, 157, 133);
            mkDarkSyntax["Variable"] = MediaColor.FromRgb(181, 206, 168);
            mkDarkSyntax["Target"] = MediaColor.FromRgb(78, 201, 176);

            var mkDark = new EditorTheme
            {
                Name = "Makefile - Dark",
                EditorBackground = Colors.Black,
                EditorForeground = Colors.White,
                Caret = Colors.White,
                Selection = MediaColor.FromRgb(62, 94, 138),
                CurrentLine = MediaColor.FromRgb(45, 45, 48),
                LineNumbers = MediaColor.FromRgb(190, 190, 190),
                Syntax = mkDarkSyntax
            };

            dict[EditorLanguage.Makefile] = new List<EditorTheme> { mkLight, mkDark };

            return dict;
        }
    }
}