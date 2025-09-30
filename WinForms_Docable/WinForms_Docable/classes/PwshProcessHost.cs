using System;
using System.Diagnostics;
using System.Text;

namespace PIC32_M_DEV.classes
{
    // Out-of-proc host: runs full pwsh.exe and streams I/O
    public sealed class PwshProcessHost : IDisposable
    {
        private Process? _proc;

        public event Action<string>? Output;
        public event Action<string>? Error;

        public bool IsRunning => _proc is { HasExited: false };

        public void Start(string arguments = "-NoLogo -NoExit")
        {
            if (IsRunning) return;

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh.exe", // falls back to Windows PowerShell if you change to powershell.exe
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Output?.Invoke(e.Data + Environment.NewLine); };
            _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Error?.Invoke(e.Data + Environment.NewLine); };

            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
        }

        public void SendLine(string command)
        {
            if (!IsRunning) throw new InvalidOperationException("pwsh is not running.");
            _proc!.StandardInput.WriteLine(command);
            _proc!.StandardInput.Flush();
        }

        public void Dispose()
        {
            try
            {
                if (IsRunning)
                {
                    _proc!.StandardInput.WriteLine("exit");
                    _proc!.StandardInput.Flush();
                    if (!_proc!.WaitForExit(1500))
                        _proc.Kill(true);
                }
            }
            catch { /* best effort */ }
            finally
            {
                _proc?.Dispose();
                _proc = null;
            }
        }
    }
}