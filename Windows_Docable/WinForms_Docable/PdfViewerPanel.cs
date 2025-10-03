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
    public sealed class PdfViewerPanel : DockContent, IThemedContent
    {
        private readonly WebView2 _webView;
        private readonly string _assetsDir;

        public PdfViewerPanel()
        {
            Text = "PDF Viewer";
            _webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);

            // PDF.js assets under app folder
            _assetsDir = Path.Combine(AppContext.BaseDirectory, "assets", "pdfjs");
        }

        public async Task NavigateToPdfAsync(string pdfFullPath)
        {
            if (!File.Exists(pdfFullPath))
            {
                MessageBox.Show($"PDF not found:\n{pdfFullPath}", "Open PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Validate PDF.js assets presence
            var viewerHtml = Path.Combine(_assetsDir, "web", "viewer.html");
            var pdfJs = Path.Combine(_assetsDir, "build", "pdf.js");
            var workerJs = Path.Combine(_assetsDir, "build", "pdf.worker.js");
            if (!File.Exists(viewerHtml) || !File.Exists(pdfJs) || !File.Exists(workerJs))
            {
                MessageBox.Show(
                    "PDF.js assets missing. Ensure assets/pdfjs contains the prebuilt web/ and build/ from pdfjs-dist.\n" +
                    $"Expected: {viewerHtml}\n{pdfJs}\n{workerJs}",
                    "PDF Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Optional: ship a fixed WebView2 runtime in "WebView2Fixed" for offline installs
            var fixedRuntime = Path.Combine(AppContext.BaseDirectory, "WebView2Fixed");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: Directory.Exists(fixedRuntime) ? fixedRuntime : null,
                userDataFolder: Path.Combine(AppContext.BaseDirectory, "wv2userdata"));

            await _webView.EnsureCoreWebView2Async(env);

            var core = _webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = false;

            // Serve local content over secure virtual hostnames (avoids file:// CORS issues)
            core.SetVirtualHostNameToFolderMapping(
                "appassets", _assetsDir, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping(
                "localfiles", AppContext.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);

            // Build a URL like:
            // https://appassets/web/viewer.html?file=https%3A%2F%2Flocalfiles%2Fdependancies%2Fdatasheets%2Fmy.pdf
            string relPdf = Path.GetRelativePath(AppContext.BaseDirectory, pdfFullPath).Replace('\\', '/');
            string pdfUrl = "https://localfiles/" + relPdf;
            string viewerUrl = "https://appassets/web/viewer.html?file=" + Uri.EscapeDataString(pdfUrl);

            _webView.Source = new Uri(viewerUrl);
            Text = $"Datasheet: {Path.GetFileName(pdfFullPath)}";
        }

        public void ApplyTheme(bool darkMode)
        {
            BackColor = darkMode ? System.Drawing.Color.FromArgb(37, 37, 38) : System.Drawing.SystemColors.Control;
            ForeColor = darkMode ? System.Drawing.Color.Gainsboro : System.Drawing.SystemColors.ControlText;
            _webView.DefaultBackgroundColor = darkMode
                ? System.Drawing.Color.FromArgb(37, 37, 38)
                : System.Drawing.Color.White;
            // PDF.js will follow its own CSS; this keeps the host consistent.
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