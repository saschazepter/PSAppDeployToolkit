using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PSADT.Process
{
    public static class Handle
    {
        #region Native Types & Constants

        private const string networkDevicePrefix = "\\Device\\Mup\\";
        private const int MAX_PATH = 260;

        private enum NTSTATUS : uint
        {
            STATUS_SUCCESS = 0x00000000,
            STATUS_BUFFER_OVERFLOW = 0x80000005,
            STATUS_INFO_LENGTH_MISMATCH = 0xC0000004
        }

        [Flags]
        private enum PROCESS_ACCESS_RIGHTS : uint
        {
            PROCESS_DUP_HANDLE = 0x00000040,
            PROCESS_QUERY_INFORMATION = 0x00000400,
            PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
            PROCESS_VM_READ = 0x0010
        }

        [Flags]
        private enum DUPLICATE_HANDLE_OPTIONS : uint
        {
            DUPLICATE_CLOSE_SOURCE = 0x00000001,
            DUPLICATE_SAME_ACCESS = 0x00000002
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE
        {
            public uint ProcessId;
            public byte ObjectTypeNumber;
            public SYSTEM_HANDLE_FLAGS Flags;
            public ushort Handle;
            public IntPtr Object;
            public uint GrantedAccess;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OBJECT_NAME_INFORMATION
        {
            public UNICODE_STRING Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OBJECT_TYPE_INFORMATION
        {
            public UNICODE_STRING Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECTION_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public uint Attributes;
            public ulong Size;
        }

        [Flags]
        public enum SYSTEM_HANDLE_FLAGS : byte
        {
            PROTECT_FROM_CLOSE = 0x01,
            INHERIT = 0x02
        }

        private enum SYSTEM_INFORMATION_CLASS : uint
        {
            SystemHandleInformation = 16
        }

        private enum SECTION_INFORMATION_CLASS
        {
            SectionBasicInformation = 0,
            SectionImageInformation = 1,
            SectionNameInformation = 2
        }

        #endregion

        #region P/Invoke Declarations

        [DllImport("ntdll.dll")]
        private static extern NTSTATUS NtQuerySystemInformation(
            [In] SYSTEM_INFORMATION_CLASS SystemInformationClass,
            [Out] IntPtr SystemInformation,
            [In] uint SystemInformationLength,
            [Out] out uint ReturnLength
        );

        [DllImport("ntdll.dll")]
        private static extern NTSTATUS NtQueryObject(
            [In] IntPtr Handle,
            [In] uint ObjectInformationClass,
            [Out] IntPtr ObjectInformation,
            [In] uint ObjectInformationLength,
            [Out] out uint ReturnLength
        );

        [DllImport("ntdll.dll")]
        private static extern NTSTATUS NtQuerySection(
            [In] IntPtr SectionHandle,
            [In] SECTION_INFORMATION_CLASS SectionInformationClass,
            [Out] IntPtr SectionInformation,
            [In] uint SectionInformationLength,
            [Out] out uint ResultLength
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(
            [In] PROCESS_ACCESS_RIGHTS dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] uint dwProcessId
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateHandle(
            [In] SafeProcessHandle hSourceProcessHandle,
            [In] IntPtr hSourceHandle,
            [In] SafeProcessHandle hTargetProcessHandle,
            [Out] out SafeFileHandle lpTargetHandle,
            [In] uint dwDesiredAccess,
            [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            [In] DUPLICATE_HANDLE_OPTIONS dwOptions
        );

        [DllImport("kernel32.dll")]
        private static extern SafeProcessHandle GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice(
            string lpDeviceName,
            StringBuilder lpTargetPath,
            int ucchMax
        );

        #endregion

        #region Handle Management

        private static readonly ConcurrentDictionary<byte, string> typeNameCache = new ConcurrentDictionary<byte, string>();
        private static readonly ConcurrentDictionary<string, string> deviceMap = new ConcurrentDictionary<string, string>();

        //private static readonly object LogLock = new object();
        //private static readonly string LogFilePath = "log.txt";

        private static void LogDebug(string message, Exception? ex = null)
        {
            //var debugMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            //if (ex != null)
            //{
            //    debugMessage += $"\nException: {ex.GetType().FullName}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            //    if (ex.InnerException != null)
            //    {
            //        debugMessage += $"\nInner Exception: {ex.InnerException.GetType().FullName}\nMessage: {ex.InnerException.Message}\nStack Trace:\n{ex.InnerException.StackTrace}";
            //    }
            //}

            // Write to console
            Debug.WriteLine(message);

            // Write to log file
            //lock (LogLock)
            //{
            //    try
            //    {
            //        File.AppendAllText(LogFilePath, debugMessage + Environment.NewLine);
            //    }
            //    catch (Exception logEx)
            //    {
            //        // If we can't write to the log file, at least show the error in the console
            //        Debug.WriteLine($"Failed to write to log file: {logEx.Message}");
            //    }
            //}
        }

        public static IEnumerable<ProcessHandleInfo> GetPathHandles(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            LogDebug($"Getting file handles for path: {path}");
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
            bool isDirectory = Directory.Exists(path);
            var handles = new List<ProcessHandleInfo>();

            try
            {
                var systemHandles = HandleCache.GetHandles()
                    .Where(h => !IsProblematicHandle(h))
                    .GroupBy(h => h.ProcessId);

                foreach (var processGroup in systemHandles)
                {
                    uint processId = processGroup.Key;
                    if (!IsProcessAccessible(processId))
                    {
                        LogDebug($"Skipping inaccessible process {processId}");
                        continue;
                    }

                    using var processHandle = OpenProcess(
                        PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION,
                        false,
                        processId
                    );

                    if (processHandle == null || processHandle.IsInvalid)
                        continue;

                    foreach (var handle in processGroup)
                    {
                        try
                        {
                            var duplicatedHandle = DuplicateProcessHandle(processHandle, handle.Handle);
                            if (duplicatedHandle == null || duplicatedHandle.IsInvalid)
                                continue;

                            string? typeName = GetTypeNameFromHandle(duplicatedHandle);
                            if (string.IsNullOrEmpty(typeName))
                            {
                                duplicatedHandle.Dispose();
                                continue;
                            }

                            if (!IsRelevantHandleType(typeName!))
                            {
                                duplicatedHandle.Dispose();
                                continue;
                            }

                            string? objectName = GetObjectNameFromHandle(duplicatedHandle);

                            // If object name is null and type is Section, try to get the section name
                            if (string.IsNullOrEmpty(objectName) && typeName!.Equals("Section", StringComparison.OrdinalIgnoreCase))
                            {
                                objectName = GetSectionNameFromHandle(duplicatedHandle);
                            }

                            if (string.IsNullOrEmpty(objectName))
                            {
                                duplicatedHandle.Dispose();
                                continue;
                            }

                            duplicatedHandle.Dispose();

                            string dosPath = ConvertDevicePathToDosPath(objectName!);
                            string normalizedDosPath = dosPath.TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();

                            bool isMatch = isDirectory
                                ? normalizedDosPath.StartsWith(fullPath)
                                : normalizedDosPath.Equals(fullPath);

                            if (isMatch)
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

                                using var process = System.Diagnostics.Process.GetProcessById((int)processId);

                                var handleInfo = new ProcessHandleInfo
                                {
                                    Process = ProcessHelper.GetProcessExecutableName((int)processId),
                                    ProcessId = (int)processId,
                                    ProcessDescription = ProcessHelper.GetProcessFileVersionInfo((int)processId)?.FileDescription ?? string.Empty,
                                    Path = ProcessHelper.GetProcessFullyQualifiedPath((int)processId),
                                    WorkingDirectory = workingDir,
                                    CommandLine = cmdLine,
                                    User = ProcessHelper.GetProcessOwner((int)processId),
                                    ProcessStartTimeUtc = process.StartTime.ToUniversalTime(),
                                    ProcessStartTimeLocal = process.StartTime,
                                    LockedPath = dosPath,
                                    Handle = $"0x{handle.Handle:X}",
                                    HandleType = typeName,
                                    HandleFlags = GetFlagsString(handle.Flags),
                                    HandleAccessMask = ProcessHelper.GetGrantedAccessString(handle.GrantedAccess)
                                };

                                handles.Add(handleInfo);
                                LogDebug($"Found matching handle: {handleInfo.Handle} in process {handleInfo.ProcessId} ({handleInfo.Process})");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Error processing handle {handle.Handle} in process {processId}", ex);
                        }
                    }
                }

                LogDebug($"Found {handles.Count} matching handles");
                return handles;
            }
            catch (Exception ex)
            {
                LogDebug("Error in GetFileHandles", ex);
                throw new InvalidOperationException($"Failed to get file handles for path: {path}", ex);
            }
        }

        private static string GetFlagsString(SYSTEM_HANDLE_FLAGS flags)
        {
            if (flags == 0)
                return string.Empty;

            return flags.ToString();
        }

        private static bool IsRelevantHandleType(string typeName)
        {
            return typeName.Equals("File", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Equals("Directory", StringComparison.OrdinalIgnoreCase) ||
                   typeName.Equals("Section", StringComparison.OrdinalIgnoreCase);
        }

        private static SafeFileHandle? DuplicateProcessHandle(SafeProcessHandle processHandle, ushort handle)
        {
            bool success = DuplicateHandle(
                processHandle,
                new IntPtr(handle),
                GetCurrentProcess(),
                out SafeFileHandle duplicatedHandle,
                0,
                false,
                DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS
            );

            if (!success || duplicatedHandle == null || duplicatedHandle.IsInvalid)
            {
                // Skipping logging to reduce clutter for expected failures
                return null;
            }

            return duplicatedHandle;
        }

        private static string? GetTypeNameFromHandle(SafeFileHandle handle)
        {
            uint length = 0;
            NTSTATUS status = NtQueryObject(
                handle.DangerousGetHandle(),
                2, // ObjectTypeInformation
                IntPtr.Zero,
                0,
                out length
            );

            if (length == 0 || (status != NTSTATUS.STATUS_SUCCESS && status != NTSTATUS.STATUS_INFO_LENGTH_MISMATCH))
                return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                status = NtQueryObject(
                    handle.DangerousGetHandle(),
                    2, // ObjectTypeInformation
                    buffer,
                    length,
                    out _ // Discard actual return length
                );

                if (status != NTSTATUS.STATUS_SUCCESS)
                {
                    return null;
                }

                var typeInfo = Marshal.PtrToStructure<OBJECT_TYPE_INFORMATION>(buffer);
                if (typeInfo.Name.Buffer == IntPtr.Zero || typeInfo.Name.Length == 0)
                    return null;

                string? typeName = Marshal.PtrToStringUni(typeInfo.Name.Buffer, typeInfo.Name.Length / 2);
                return typeName;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string? GetObjectNameFromHandle(SafeFileHandle handle)
        {
            uint length = 0;
            NTSTATUS status = NtQueryObject(
                handle.DangerousGetHandle(),
                1, // ObjectNameInformation
                IntPtr.Zero,
                0,
                out length
            );

            if (length == 0 || (status != NTSTATUS.STATUS_SUCCESS && status != NTSTATUS.STATUS_INFO_LENGTH_MISMATCH))
                return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                status = NtQueryObject(
                    handle.DangerousGetHandle(),
                    1, // ObjectNameInformation
                    buffer,
                    length,
                    out _ // Discard actual return length
                );

                if (status != NTSTATUS.STATUS_SUCCESS)
                {
                    return null;
                }

                var nameInfo = Marshal.PtrToStructure<OBJECT_NAME_INFORMATION>(buffer);
                if (nameInfo.Name.Buffer == IntPtr.Zero || nameInfo.Name.Length == 0)
                    return null;

                string? name = Marshal.PtrToStringUni(nameInfo.Name.Buffer, nameInfo.Name.Length / 2);
                return name;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string? GetSectionNameFromHandle(SafeFileHandle handle)
        {
            uint length = 0;
            NTSTATUS status = NtQuerySection(
                handle.DangerousGetHandle(),
                SECTION_INFORMATION_CLASS.SectionNameInformation,
                IntPtr.Zero,
                0,
                out length
            );

            if (length == 0 || (status != NTSTATUS.STATUS_SUCCESS && status != NTSTATUS.STATUS_INFO_LENGTH_MISMATCH))
                return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                status = NtQuerySection(
                    handle.DangerousGetHandle(),
                    SECTION_INFORMATION_CLASS.SectionNameInformation,
                    buffer,
                    length,
                    out _ // Discard actual return length
                );

                if (status != NTSTATUS.STATUS_SUCCESS)
                {
                    return null;
                }

                var nameInfo = Marshal.PtrToStructure<OBJECT_NAME_INFORMATION>(buffer);
                if (nameInfo.Name.Buffer == IntPtr.Zero || nameInfo.Name.Length == 0)
                    return null;

                string? name = Marshal.PtrToStringUni(nameInfo.Name.Buffer, nameInfo.Name.Length / 2);
                return name;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsProblematicHandle(SYSTEM_HANDLE handle)
        {
            return (handle.GrantedAccess == 0x120089 && handle.Flags == SYSTEM_HANDLE_FLAGS.INHERIT) ||
                   (handle.GrantedAccess == 0x120189 && (handle.Flags == 0 || handle.Flags == SYSTEM_HANDLE_FLAGS.INHERIT)) ||
                   (handle.GrantedAccess == 0x12019f && (handle.Flags == 0 || handle.Flags == SYSTEM_HANDLE_FLAGS.INHERIT)) ||
                   (handle.GrantedAccess == 0x1a019f && (handle.Flags == 0 || handle.Flags == SYSTEM_HANDLE_FLAGS.INHERIT));
        }

        public static void CloseHandle(uint processId, ushort handle)
        {
            using var sourceProcess = OpenProcess(
                PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE,
                false,
                processId
            );

            if (sourceProcess.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Failed to open process {processId}");
            }

            bool success = DuplicateHandle(
                sourceProcess,
                new IntPtr(handle),
                GetCurrentProcess(),
                out SafeFileHandle targetHandle,
                0,
                false,
                DUPLICATE_HANDLE_OPTIONS.DUPLICATE_CLOSE_SOURCE
            );

            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"Failed to close handle {handle} in process {processId}");
            }

            if (!targetHandle.IsInvalid)
            {
                targetHandle.Dispose();
            }
        }

        #endregion

        #region Device Path Conversion

        private static void EnsureDeviceMap()
        {
            if (deviceMap.IsEmpty)
            {
                BuildDeviceMap();
            }
        }

        private static void BuildDeviceMap()
        {
            StringBuilder targetPath = new StringBuilder(MAX_PATH);

            // Map drive letters
            foreach (string drive in Environment.GetLogicalDrives())
            {
                string driveLetter = drive.Substring(0, 2);
                uint result = QueryDosDevice(driveLetter, targetPath, MAX_PATH);

                if (result != 0)
                {
                    string devicePath = targetPath.ToString();
                    deviceMap.TryAdd(devicePath, driveLetter);
                }
                targetPath.Clear();
            }

            // Add mapping for network paths
            deviceMap.TryAdd(networkDevicePrefix.TrimEnd('\\'), "\\");
        }

        public static string ConvertDevicePathToDosPath(string devicePath)
        {
            EnsureDeviceMap();

            foreach (var mapping in deviceMap)
            {
                if (devicePath.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return devicePath.Replace(mapping.Key, mapping.Value);
                }
            }

            return devicePath;
        }

        #endregion

        #region Handle Cache and Process Accessibility

        private static class HandleCache
        {
            private static readonly object _lock = new object();
            private static List<SYSTEM_HANDLE>? _handles;
            private static DateTime _lastUpdate = DateTime.MinValue;
            private const int CACHE_TIMEOUT_MS = 30000;

            public static List<SYSTEM_HANDLE> GetHandles()
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    if (_handles != null && (now - _lastUpdate).TotalMilliseconds < CACHE_TIMEOUT_MS)
                    {
                        LogDebug("Utilizing cached handles.");
                        return _handles;
                    }

                    try
                    {
                        _handles = QuerySystemHandles();
                        _lastUpdate = now;
                    }
                    catch (Exception ex)
                    {
                        LogDebug("Error querying system handles.", ex);
                        _handles = _handles ?? new List<SYSTEM_HANDLE>();
                    }

                    return _handles;
                }
            }

            private static List<SYSTEM_HANDLE> QuerySystemHandles()
            {
                uint length = 0x10000;  // Start with 64KB buffer
                IntPtr ptr = IntPtr.Zero;

                try
                {
                    while (true)
                    {
                        ptr = Marshal.AllocHGlobal((int)length);

                        if (ptr == IntPtr.Zero)
                            throw new OutOfMemoryException("Failed to allocate memory for handle information");


                        NTSTATUS status = NtQuerySystemInformation(
                            SYSTEM_INFORMATION_CLASS.SystemHandleInformation,
                            ptr,
                            length,
                            out uint returnLength
                        );

                        if (status == NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
                        {
                            Marshal.FreeHGlobal(ptr);
                            ptr = IntPtr.Zero;
                            length = Math.Max(length * 2, returnLength);
                            continue;
                        }

                        if (status != NTSTATUS.STATUS_SUCCESS)
                            throw new Win32Exception($"Failed to query system handles: {status}");

                        int handleCount = Marshal.ReadInt32(ptr);

                        var handles = new List<SYSTEM_HANDLE>(handleCount);
                        IntPtr handlePtr = IntPtr.Add(ptr, IntPtr.Size);
                        int structSize = Marshal.SizeOf<SYSTEM_HANDLE>();

                        for (int i = 0; i < handleCount; i++)
                        {
                            handles.Add(Marshal.PtrToStructure<SYSTEM_HANDLE>(handlePtr));
                            handlePtr = IntPtr.Add(handlePtr, structSize);
                        }

                        return handles;
                    }
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
            }

            public static void Clear()
            {
                lock (_lock)
                {
                    _handles = null;
                    _lastUpdate = DateTime.MinValue;
                }
            }
        }

        private static readonly ConcurrentDictionary<uint, bool> ProcessAccessCache = new ConcurrentDictionary<uint, bool>();
        private static readonly HashSet<uint> CommonInaccessibleProcesses = new HashSet<uint>(new uint[] { 4, 0 }); // System and Idle process

        private static bool IsProcessAccessible(uint processId)
        {
            if (CommonInaccessibleProcesses.Contains(processId))
                return false;

            if (ProcessAccessCache.TryGetValue(processId, out bool isAccessible))
                return isAccessible;

            using (var processHandle = OpenProcess(
                PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION,
                false,
                processId))
            {
                isAccessible = processHandle != null && !processHandle.IsInvalid;

                ProcessAccessCache.TryAdd(processId, isAccessible);

                if (!isAccessible)
                    CommonInaccessibleProcesses.Add(processId);

                return isAccessible;
            }
        }

        public static void ClearCaches()
        {
            ProcessAccessCache.Clear();
            CommonInaccessibleProcesses.Clear();
            HandleCache.Clear();
        }

        #endregion
    }
}
