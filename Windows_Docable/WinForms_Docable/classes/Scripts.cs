using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms; // Required for MessageBox

namespace PIC32_M_DEV.classes
{
    /// <summary>
    /// Provides script launching and file system monitoring utilities for the project.
    /// </summary>
    internal class scripts
    {
        /// <summary>
        /// Launches a script for the specified application, optionally passing a project path.
        /// </summary>
        /// <param name="app">The script name (without extension).</param>
        /// <param name="project">Optional project directory to pass to the script.</param>
        internal void launch(string app, string? project = null)
        {
            // Build the scripts directory path
            string scriptsDir = Path.Combine(AppContext.BaseDirectory, "dependancies", "scripts");
            bool isWindows = OperatingSystem.IsWindows();
            bool isLinux = OperatingSystem.IsLinux();

            // Select script extension based on OS
            string scriptExt = isWindows ? ".ps1" : ".sh";
            string scriptPath = Path.Combine(scriptsDir, app + scriptExt);

            // Check if the script file exists
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

            // Try to start the process and handle output/errors
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
                // Fallback to Windows PowerShell if pwsh.exe is unavailable
                if (isWindows)
                {
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

        /// <summary>
        /// Monitors the specified project directory for file changes and alerts the user.
        /// </summary>
        /// <param name="project">The directory to monitor.</param>
        internal void alert_changes(string project)
        {
            // Validate the directory before monitoring
            if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project))
            {
                MessageBox.Show("Invalid project directory for monitoring.", "File Watcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Set up the file system watcher
            using (var watch = new FileSystemWatcher(project))
            {
                watch.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

                // Subscribe to file system events
                watch.Changed += (sender, e) => OnChanged(sender, e, project);
                watch.Created += (sender, e) => OnChanged(sender, e, project);
                watch.Deleted += (sender, e) => OnChanged(sender, e, project);
                watch.Renamed += (sender, e) => OnRenamed(sender, e, project);
                watch.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Handles file change, creation, or deletion events.
        /// </summary>
        private void OnChanged(object sender, FileSystemEventArgs e, string project)
        {
            MessageBox.Show($"File {e.ChangeType}: {e.FullPath}", "Project Change Detected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Handles file rename events.
        /// </summary>
        private void OnRenamed(object sender, RenamedEventArgs e, string project)
        {
            MessageBox.Show($"File Renamed: {e.OldFullPath} -> {e.FullPath}", "Project Change Detected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
