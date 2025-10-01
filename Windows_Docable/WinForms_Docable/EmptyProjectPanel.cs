using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using PIC32_M_DEV.Interfaces;

namespace PIC32_M_DEV
{
    public class EmptyProjectPanel : DockContent, IThemedContent
    {
        public EmptyProjectPanel()
        {
            Text = "Project Explorer";
            var label = new Label
            {
                Text = "No project loaded.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Italic)
            };
            Controls.Add(label);
        }
        public void ApplyTheme(bool darkMode)
        {
            BackColor = darkMode ? Color.FromArgb(37, 37, 38) : SystemColors.Control;
            ForeColor = darkMode ? Color.Gainsboro : SystemColors.ControlText;
            if (Controls.Count > 0 && Controls[0] is Label label)
            {
                label.ForeColor = ForeColor;
                label.BackColor = BackColor;
            }
        }
    }
}
