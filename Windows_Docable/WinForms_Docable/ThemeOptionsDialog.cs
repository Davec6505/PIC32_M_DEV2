using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using PIC32_M_DEV.classes;

namespace PIC32_M_DEV
{
    public class ThemeOptionsDialog : Form
    {
        private readonly ComboBox _languageCombo;
        private readonly ComboBox _themeCombo;
        private readonly Button _btnBg;
        private readonly Button _btnFg;
        private readonly Button _btnCaret;
        private readonly Button _btnSelection;
        private readonly Button _btnCurrentLine;
        private readonly Button _btnLineNumbers;
        private readonly ListBox _syntaxList;
        private readonly Button _btnSyntaxColor;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        private EditorLanguage _lang;
        private EditorTheme _working;

        public ThemeOptionsDialog()
        {
            Text = "Editor Theme Options";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            _languageCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            _languageCombo.Items.AddRange(new object[] { EditorLanguage.C, EditorLanguage.Asm, EditorLanguage.Makefile });
            _languageCombo.SelectedIndexChanged += (s, e) => LoadThemesForLanguage();

            _themeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _themeCombo.SelectedIndexChanged += (s, e) => LoadSelectedTheme();

            _btnBg = MakeColorButton("Editor Background", () => Pick(ref _working, nameof(EditorTheme.EditorBackground)));
            _btnFg = MakeColorButton("Editor Foreground", () => Pick(ref _working, nameof(EditorTheme.EditorForeground)));
            _btnCaret = MakeColorButton("Caret", () => Pick(ref _working, nameof(EditorTheme.Caret)));
            _btnSelection = MakeColorButton("Selection", () => Pick(ref _working, nameof(EditorTheme.Selection)));
            _btnCurrentLine = MakeColorButton("Current Line", () => Pick(ref _working, nameof(EditorTheme.CurrentLine)));
            _btnLineNumbers = MakeColorButton("Line Numbers", () => Pick(ref _working, nameof(EditorTheme.LineNumbers)));

            _syntaxList = new ListBox { Width = 240, Height = 160 };
            _btnSyntaxColor = new Button { Text = "Set Syntax Color", AutoSize = true };
            _btnSyntaxColor.Click += (s, e) => PickSyntaxColor();

            _btnSave = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
            _btnSave.Click += (s, e) => SaveCurrent();
            _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };

            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int row = 0;
            layout.Controls.Add(new Label { Text = "Language:", AutoSize = true }, 0, row);
            layout.Controls.Add(_languageCombo, 1, row++);
            layout.Controls.Add(new Label { Text = "Theme:", AutoSize = true }, 0, row);
            layout.Controls.Add(_themeCombo, 1, row++);

            layout.Controls.Add(_btnBg, 0, row++);
            layout.Controls.Add(_btnFg, 0, row++);
            layout.Controls.Add(_btnCaret, 0, row++);
            layout.Controls.Add(_btnSelection, 0, row++);
            layout.Controls.Add(_btnCurrentLine, 0, row++);
            layout.Controls.Add(_btnLineNumbers, 0, row++);

            layout.Controls.Add(new Label { Text = "Syntax categories:", AutoSize = true }, 0, row++);
            layout.Controls.Add(_syntaxList, 0, row);
            layout.SetColumnSpan(_syntaxList, 2);
            row++;
            layout.Controls.Add(_btnSyntaxColor, 0, row++);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.AddRange(new Control[] { _btnSave, _btnCancel });
            layout.Controls.Add(buttons, 0, row);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);

            _languageCombo.SelectedIndex = 0;
        }

        private Button MakeColorButton(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true };
            b.Click += (s, e) => onClick();
            return b;
        }

        private void LoadThemesForLanguage()
        {
            _lang = (EditorLanguage)_languageCombo.SelectedItem!;
            var names = ThemeManager.GetThemes(_lang).Select(t => t.Name).ToArray();
            _themeCombo.Items.Clear();
            _themeCombo.Items.AddRange(names);
            _themeCombo.SelectedIndex = 0;
        }

        private void LoadSelectedTheme()
        {
            if (_themeCombo.SelectedItem == null) return;
            var name = _themeCombo.SelectedItem.ToString()!;
            ThemeManager.SetCurrentTheme(_lang, name, save: false);
            _working = ThemeManager.GetEditableTheme(_lang);
            RefreshSyntaxList();
        }

        private void RefreshSyntaxList()
        {
            _syntaxList.Items.Clear();
            foreach (var key in _working.Syntax.Keys.OrderBy(k => k))
            {
                _syntaxList.Items.Add(key);
            }
            if (_syntaxList.Items.Count > 0) _syntaxList.SelectedIndex = 0;
        }

        private void Pick(ref EditorTheme theme, string propertyName)
        {
            var prop = typeof(EditorTheme).GetProperty(propertyName);
            if (prop == null) return;
            var current = (System.Windows.Media.Color)prop.GetValue(theme)!;
            using var dlg = new ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var c = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                prop.SetValue(theme, c);
            }
        }

        private void PickSyntaxColor()
        {
            if (_syntaxList.SelectedItem == null) return;
            var key = _syntaxList.SelectedItem.ToString()!;
            var current = _working.Syntax.TryGetValue(key, out var c) ? c : Colors.Black;
            using var dlg = new ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var cNew = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                _working.Syntax[key] = cNew;
            }
        }

        private void SaveCurrent()
        {
            // Allow rename
            var newName = Microsoft.VisualBasic.Interaction.InputBox("Theme name:", "Save Theme", _working.Name);
            if (!string.IsNullOrWhiteSpace(newName)) _working.Name = newName;
            ThemeManager.SaveCustomTheme(_lang, _working, setCurrent: true);
        }
    }
}
