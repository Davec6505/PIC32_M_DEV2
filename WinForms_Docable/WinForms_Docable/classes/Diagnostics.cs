using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace WinForms_Docable.classes
{
    internal static class Diagnostics
    {
        public static void Run()
        {
            // 1) Verify method existence
            var method = typeof(Mutex).GetMethod(
                "SetAccessControl",
                new[] { typeof(System.Security.AccessControl.MutexSecurity) });
            Debug.WriteLine("Has Mutex.SetAccessControl: " + (method != null));

            // 2) Log loaded assemblies early
            AppDomain.CurrentDomain.AssemblyLoad += (s, e) =>
            {
                try
                {
                    var loc = e.LoadedAssembly.Location;
                    Debug.WriteLine($"Loaded: {e.LoadedAssembly.FullName} @ {loc}");
                }
                catch { Debug.WriteLine($"Loaded: {e.LoadedAssembly.FullName} (dynamic/no location)"); }
            };
        }


        private static IHighlightingDefinition LoadFromFile(string relativePath)
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath);
            using var s = File.OpenRead(path);
            using var reader = new XmlTextReader(s);
            var xshd = HighlightingLoader.LoadXshd(reader);
            return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
        }
        // usage: LoadFromFile(@"Highlighting\Makefile.xshd")

    }


}
