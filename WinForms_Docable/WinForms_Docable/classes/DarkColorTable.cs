using System.Drawing;
using System.Windows.Forms;

namespace WinForms_Docable.classes
{
    public sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(0, 0, 0);
        public override Color MenuItemSelected => Color.FromArgb(30, 30, 30);
        public override Color MenuItemBorder => Color.FromArgb(25, 5, 0);
        public override Color ImageMarginGradientBegin => Color.FromArgb(0, 0, 0);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(0, 0, 0);
        public override Color ImageMarginGradientEnd => Color.FromArgb(0, 0, 0);
        public override Color MenuStripGradientBegin => Color.FromArgb(0, 0, 0);
        public override Color MenuStripGradientEnd => Color.FromArgb(0, 0, 0);
        public override Color SeparatorDark => Color.FromArgb(5, 5, 2);
        public override Color SeparatorLight => Color.FromArgb(10, 10, 10);
    }
}
