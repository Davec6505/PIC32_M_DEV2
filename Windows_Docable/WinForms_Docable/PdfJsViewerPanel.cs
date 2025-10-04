using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using PIC32_M_DEV.Interfaces;
using WeifenLuo.WinFormsUI.Docking;

namespace PIC32_M_DEV
{
    /// <summary>
    /// A DockContent panel that hosts Mozilla's PDF.js viewer inside a WebView2 control.
    /// This provides a modern, reliable PDF viewing experience with text selection, search, and more.
    /// </summary>
    public sealed class PdfJsViewerPanel : DockContent, IThemedContent
    {
        private readonly WebView2 _webView;
        private bool _isDarkMode;
        private string? _initialPdfPath;

        // A virtual hostname to serve local files from, avoiding file:/// restrictions.
        private const string VirtualHostName = "pdfjs.app.loc";

        public PdfJsViewerPanel()
        {
            Text = "PDF Viewer";
            _webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);

            // Initialize WebView2 and set up the virtual host mapping.
            _ = InitializeWebViewAsync();
        }

        public PdfJsViewerPanel(string filePath, bool isDarkMode) : this()
        {
            _isDarkMode = isDarkMode;
            _initialPdfPath = filePath; // Store the path to load after initialization.
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);

                // Map the virtual host to the application's base directory.
                // This allows us to access all output files (like pdfjs/* and your pdf) via https://
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    VirtualHostName,
                    AppContext.BaseDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);

                // If a document was requested before initialization, load it now.
                if (!string.IsNullOrEmpty(_initialPdfPath))
                {
                    LoadDocument(_initialPdfPath);
                    _initialPdfPath = null; // Clear it so it doesn't reload
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loads a PDF file into the viewer using the virtual host.
        /// </summary>
        /// <param name="pdfFilePath">The absolute path to the PDF file to load.</param>
        public void LoadDocument(string pdfFilePath)
        {
            if (_webView.CoreWebView2 == null || !File.Exists(pdfFilePath))
            {
                return;
            }

            // The path to viewer.html relative to the AppContext.BaseDirectory.
            var viewerRelativePath = "pdfjs/web/viewer.html";
            var viewerAbsolutePath = Path.Combine(AppContext.BaseDirectory, viewerRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(viewerAbsolutePath))
            {
                MessageBox.Show($"PDF.js not found at '{viewerAbsolutePath}'. Please ensure the 'pdfjs/web/viewer.html' file exists in the output directory.", "Error");
                return;
            }

            // The path to the PDF relative to the AppContext.BaseDirectory.
            // This is needed to construct the correct URL for the virtual host.
            var pdfRelativePath = Path.GetRelativePath(AppContext.BaseDirectory, pdfFilePath)
                                      .Replace('\\', '/'); // Use forward slashes for URLs.

            // Construct the URL for the viewer and the PDF file using the virtual host.
            var viewerUrl = $"https://{VirtualHostName}/{viewerRelativePath}";
            var fileUrl = $"https://{VirtualHostName}/{pdfRelativePath}";

            // The final URL tells viewer.html which file to open.
            var sourceUrl = $"{viewerUrl}?file={Uri.EscapeDataString(fileUrl)}";

            _webView.CoreWebView2.Navigate(sourceUrl);

            Text = Path.GetFileName(pdfFilePath);
            ToolTipText = pdfFilePath;
        }

        /// <summary>
        /// Applies the dark or light theme to the PDF.js viewer.
        /// </summary>
        public void ApplyTheme(bool darkMode)
        {
            _isDarkMode = darkMode;
            if (_webView?.CoreWebView2 == null) return;

            // PDF.js can be themed via JavaScript.
            // 1 = dark, 0 = light, 2 = auto
            var themeValue = darkMode ? 1 : 0;
            _webView.CoreWebView2.ExecuteScriptAsync($"PDFViewerApplication.eventBus.dispatch('switchtheme', {{ theme: {themeValue} }});");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}