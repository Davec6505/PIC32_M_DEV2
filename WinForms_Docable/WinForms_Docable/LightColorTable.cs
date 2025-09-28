namespace WinForms_Docable
{
    public partial class Form1
    {
        private sealed class LightColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => SystemColors.ControlLightLight;
            public override Color MenuItemSelected => Color.FromArgb(221, 236, 254);
            public override Color MenuItemBorder => Color.FromArgb(147, 160, 207);
            public override Color ImageMarginGradientBegin => SystemColors.Control;
            public override Color ImageMarginGradientMiddle => SystemColors.Control;
            public override Color ImageMarginGradientEnd => SystemColors.Control;
            public override Color MenuStripGradientBegin => SystemColors.Control;
            public override Color MenuStripGradientEnd => SystemColors.Control;
        }
    }


}
