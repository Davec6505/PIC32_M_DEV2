using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIC32_M_DEV.classes
{
    internal class scripts
    {
        internal void launch(string app, string? project = null)
        {
            string scriptsDir = Path.Combine(AppContext.BaseDirectory, "dependancies", "scripts");
            bool isWindows = OperatingSystem.IsWindows();
            bool isLinux = OperatingSystem.IsLinux();

            string scriptExt = isWindows ? ".ps1" : ".sh";
            string scriptPath = Path.Combine(scriptsDir, app + scriptExt);

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show($"Script not found:\n{scriptPath}", "Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            ProcessStartInfo psi;
            if (isWindows)
            {
                // Prefer PowerShell 7 if present; fall back to Windows PowerShell
                psi = new ProcessStartInfo
                {
                    FileName = "pwsh.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = scriptsDir
                };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-ExecutionPolicy");
                psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(scriptPath);
                if (!string.IsNullOrWhiteSpace(project))
                {
                    psi.ArgumentList.Add("-Project");
                    psi.ArgumentList.Add(project!);
                }
            }
            else if (isLinux)
            {
                // Use bash explicitly so the script need not be executable
                psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = scriptsDir
                };
                psi.ArgumentList.Add("bash");
                psi.ArgumentList.Add(scriptPath);
                if (!string.IsNullOrWhiteSpace(project))
                {
                    psi.ArgumentList.Add("--project");
                    psi.ArgumentList.Add(project!);
                }
            }
            else
            {
                MessageBox.Show($"Unsupported OS for launch(): {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                    "Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        MessageBox.Show("Failed to start shell process.", "Launcher",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        MessageBox.Show(error, "Launcher",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (isWindows)
                {
                    // Fallback to Windows PowerShell if pwsh.exe is unavailable
                    psi.FileName = "powershell.exe";
                    try
                    {
                        using (var process = Process.Start(psi))
                        {
                            if (process == null)
                            {
                                MessageBox.Show("Failed to start powershell.exe.", "Launcher",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                                return;
                            }
                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                MessageBox.Show(error, "Launcher",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show($"Failed to start PowerShell:\n{ex2.Message}", "Launcher",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Launch failed:\n{ex.Message}", "Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        internal void alert_changes(string project)
        {
            if(string.IsNullOrWhiteSpace(project) || !Directory.Exists(project))
            {
                MessageBox.Show("Invalid project directory for monitoring.", "File Watcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using (var watch = new FileSystemWatcher(project))
            {
                watch.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

                watch.Changed += (sender, e) => OnChanged(sender, e, project);
                watch.Created += (sender, e) => OnChanged(sender, e, project);
                watch.Deleted += (sender, e) => OnChanged(sender, e, project);
                watch.Renamed += (sender, e) => OnRenamed(sender, e, project);
                watch.EnableRaisingEvents = true;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e, string project)
        {
            MessageBox.Show($"File {e.ChangeType}: {e.FullPath}", "Project Change Detected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnRenamed(object sender, RenamedEventArgs e, string project)
        {
            MessageBox.Show($"File Renamed: {e.OldFullPath} -> {e.FullPath}", "Project Change Detected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}
