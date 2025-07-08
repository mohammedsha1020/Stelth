using Microsoft.Win32;
using System.Diagnostics;

namespace Stealth.PC
{
    /// <summary>
    /// Manages auto-start functionality for the PC application
    /// </summary>
    public static class AutoStartManager
    {
        private const string AppName = "Stealth";
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Enables auto-start on Windows boot
        /// </summary>
        public static bool EnableAutoStart()
        {
            try
            {
                var exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    Console.WriteLine("Could not determine executable path");
                    return false;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    Console.WriteLine("Could not open registry key");
                    return false;
                }

                key.SetValue(AppName, $"\"{exePath}\" --silent");
                Console.WriteLine("Auto-start enabled successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to enable auto-start: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disables auto-start on Windows boot
        /// </summary>
        public static bool DisableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    Console.WriteLine("Could not open registry key");
                    return false;
                }

                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName);
                    Console.WriteLine("Auto-start disabled successfully");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to disable auto-start: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if auto-start is currently enabled
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key == null) return false;

                var value = key.GetValue(AppName);
                return value != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to check auto-start status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the path to the current executable
        /// </summary>
        private static string GetExecutablePath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // Fallback method
                return System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
        }

        /// <summary>
        /// Creates a scheduled task for auto-start (alternative to registry)
        /// </summary>
        public static bool CreateScheduledTask()
        {
            try
            {
                var exePath = GetExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    return false;
                }

                var taskName = "StealthAutoStart";
                var arguments = $"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" --silent\" /SC ONLOGON /RL HIGHEST /F";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create scheduled task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes the scheduled task
        /// </summary>
        public static bool RemoveScheduledTask()
        {
            try
            {
                var taskName = "StealthAutoStart";
                var arguments = $"/Delete /TN \"{taskName}\" /F";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to remove scheduled task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the scheduled task exists
        /// </summary>
        public static bool ScheduledTaskExists()
        {
            try
            {
                var taskName = "StealthAutoStart";
                var arguments = $"/Query /TN \"{taskName}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enables auto-start using the most appropriate method
        /// </summary>
        public static bool EnableAutoStartBest()
        {
            // Try registry first (simpler)
            if (EnableAutoStart())
            {
                return true;
            }

            // Fallback to scheduled task
            Console.WriteLine("Registry method failed, trying scheduled task...");
            return CreateScheduledTask();
        }

        /// <summary>
        /// Disables auto-start using all methods
        /// </summary>
        public static bool DisableAutoStartAll()
        {
            var success = true;
            
            // Remove from registry
            if (!DisableAutoStart())
            {
                success = false;
            }

            // Remove scheduled task
            if (!RemoveScheduledTask())
            {
                success = false;
            }

            return success;
        }
    }
}
