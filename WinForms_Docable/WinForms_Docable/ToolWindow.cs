using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using WinForms_Docable.Interfaces;

namespace WinForms_Docable
{
    public class ToolWindow : DockContent, IThemedContent
    {
        private readonly Label _label;

        public ToolWindow(string title)
        {
            Text = title;
            DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockBottom | DockAreas.Document | DockAreas.Float;

            _label = new Label
            {
                Text = $"Content of {title}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_label);
        }

        public void ApplyTheme(bool darkMode)
        {
            var bg = darkMode ? Color.FromArgb(37, 37, 38) : SystemColors.ControlLightLight;
            var fg = darkMode ? Color.Gainsboro : SystemColors.ControlText;
            BackColor = bg;
            ForeColor = fg;
            _label.BackColor = bg;
            _label.ForeColor = fg;
        }
    }
}
