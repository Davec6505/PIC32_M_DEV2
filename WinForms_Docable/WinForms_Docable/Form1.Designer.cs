namespace WinForms_Docable
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;


        // File menu (Open, Save, Save As, Close, Exit)
        private MenuStrip? _menuStrip;
        private ToolStripMenuItem? _fileMenu;
        private ToolStripMenuItem? _openMenuItem;
        private ToolStripMenuItem? _newMenuItem;
        private ToolStripMenuItem? _saveMenuItem;
        private ToolStripMenuItem? _saveAsMenuItem;
        private ToolStripMenuItem? _closeMenuItem;
        private ToolStripMenuItem? _exitMenuItem;

        // View menu (Dark Mode toggle)
        private ToolStripMenuItem? _viewMenu;              // ADDED
        private ToolStripMenuItem? _darkModeMenuItem;      // ADDED

        // Mirror menu (Sync, Clear)
        private ToolStripMenuItem? _mirrorMenu;
        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion


    }
}
