using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using PSADT.PInvoke;

namespace PSADT.Process
{
    /// <summary>
    /// Provides functionality to detect processes that have locks on files or directories using the Restart Manager API.
    /// </summary>
    public static class RmHandle
    {
        /// <summary>
        /// Gets a list of processes that have locks on the specified path.
        /// </summary>
        /// <param name="path">The file or directory path to check.</param>
        /// <param name="options">Options for controlling the search behavior.</param>
        /// <returns>A list of ProcessInfo objects containing details about processes with locks on the path.</returns>
        public static List<ProcessHandleInfo> GetLockingProcessInfo(string path, RmHandleOptions? options = null)
        {
            options ??= new RmHandleOptions();

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var processInfos = new ConcurrentDictionary<int, ProcessHandleInfo>();
            path = Path.GetFullPath(path);

            if (Directory.Exists(path))
            {
                var pathsToCheck = new Queue<Dictionary<string, int>>();
                var initialPath = new Dictionary<string, int> { { path, 0 } };
                pathsToCheck.Enqueue(initialPath);

                while (pathsToCheck.Count > 0)
                {
                    var currentItem = pathsToCheck.Dequeue();
                    var currentPath = currentItem.Keys.First();
                    var depth = currentItem.Values.First();

                    // Check if we've exceeded max depth
                    if (options.MaxDepth != -1 && depth > options.MaxDepth)
                        continue;

                    try
                    {
                        // Check files in current directory
                        foreach (var file in Directory.GetFiles(currentPath))
                        {
                            CheckPathForLocks(file, processInfos);
                        }

                        // Add subdirectories to queue if recursive
                        if (options.Recursive)
                        {
                            foreach (var dir in Directory.GetDirectories(currentPath))
                            {
                                var nextPath = new Dictionary<string, int> { { dir, depth + 1 } };
                                pathsToCheck.Enqueue(nextPath);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
                    {
                        if (!options.ContinueOnAccessDenied)
                            throw;
                    }
                }
            }
            else if (File.Exists(path))
            {
                CheckPathForLocks(path, processInfos);
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {path}");
            }

            return processInfos.Values.ToList();
        }

        private static void CheckPathForLocks(string path, ConcurrentDictionary<int, ProcessHandleInfo> processInfos)
        {
            uint sessionHandle;
            string sessionKey = Guid.NewGuid().ToString();

            int result = NativeMethods.RmStartSession(out sessionHandle, 0, sessionKey);
            if (result != 0)
                return;

            try
            {
                string[] pathArray = { path };
                result = NativeMethods.RmRegisterResources(sessionHandle, 1, pathArray, 0, null!, 0, null!);

                if (result != 0)
                    return;

                uint pnProcInfoNeeded;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = 0;

                result = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null!, ref lpdwRebootReasons);

                if (result == NativeMethods.ERROR_MORE_DATA)
                {
                    var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    result = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

                    if (result == 0)
                    {
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                var rmInfo = processInfo[i];
                                int processId = rmInfo.Process.dwProcessId;

                                processInfos.AddOrUpdate(
                                    processId,
                                    // Add new
                                    _ => CreateProcessInfo(processId, path, rmInfo),
                                    // Update existing
                                    (_, existing) =>
                                    {
                                        if (!existing.LockedPath.Contains(path))
                                        {
                                            existing.LockedPath = string.Join(
                                                Environment.NewLine,
                                                existing.LockedPath,
                                                path
                                            ).Trim();
                                        }
                                        return existing;
                                    }
                                );
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error processing handle info: {ex.Message}");
                            }
                        }
                    }
                }
            }
            finally
            {
                NativeMethods.RmEndSession(sessionHandle);
            }
        }

        private static ProcessHandleInfo CreateProcessInfo(int processId, string lockedPath, RM_PROCESS_INFO rmInfo)
        {
            string workingDir = string.Empty;
            string cmdLine = string.Empty;

            try
            {
                workingDir = ProcessHelper.GetWorkingDirectory((uint)processId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get working directory for PID {processId}: {ex.Message}");
            }

            try
            {
                cmdLine = ProcessHelper.GetCommandLine((uint)processId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get command line for PID {processId}: {ex.Message}");
            }

            var info = new ProcessHandleInfo
            {
                Process = ProcessHelper.GetProcessExecutableName(processId),
                ProcessId = processId,
                ProcessDescription = rmInfo.strAppName,
                Path = ProcessHelper.GetProcessFullyQualifiedPath(processId),
                WorkingDirectory = workingDir,
                CommandLine = cmdLine,
                User = ProcessHelper.GetProcessOwner(processId),
                ProcessStartTimeUtc = rmInfo.Process.ProcessStartTimeUtc,
                ProcessStartTimeLocal = rmInfo.Process.ProcessStartTimeLocal,
                LockedPath = lockedPath,
                // Handle
                // HandleType
                // HandleFlags
                // HandleAccessMask
            };

            return info;
        }

        /// <summary>
        /// Gets a list of processes that have locks on the specified path.
        /// </summary>
        /// <param name="path">The file or directory path to check.</param>
        /// <param name="recursive">Whether to check subdirectories recursively.</param>
        /// <returns>A list of processes that have locks on the specified path.</returns>
        public static List<System.Diagnostics.Process> GetLockingProcesses(string path, bool recursive = false)
        {
            var options = new RmHandleOptions { Recursive = recursive };
            return GetLockingProcessInfo(path, options)
                .Select(pi => System.Diagnostics.Process.GetProcessById(pi.ProcessId))
                .Where(p => p != null)
                .ToList();
        }
    }
}