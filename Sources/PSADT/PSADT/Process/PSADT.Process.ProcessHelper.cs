using System;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PSADT.PInvoke;
using System.Text;
using System.Diagnostics;

namespace PSADT.Process
{
    public static class ProcessHelper
    {
        public static string GetWorkingDirectory(uint processId)
        {
            return RemoveTrailingBackslash(ProcessStringReader.GetProcessString(processId, isCommandLine: false));
        }

        public static string GetCommandLine(uint processId)
        {
            return ProcessStringReader.GetProcessString(processId, isCommandLine: true);
        }

        public static string GetProcessExecutableName(int processId)
        {
            string? processName = null;
            System.Diagnostics.Process? process = null;

            try
            {
                process = System.Diagnostics.Process.GetProcessById(processId);
            }
            catch
            {
                return "Unknown";
            }


            using (process)
            {
                try
                {
                    processName = Path.GetFileName(process.MainModule?.FileName);
                }
                catch
                {
                    processName = process.ProcessName + ".exe";
                }
            }

            return processName ?? "Unknown";
        }

        public static string GetProcessOwner(int processId)
        {
            using SafeProcessHandle processHandle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                (uint)processId);

            if (processHandle.IsInvalid)
                return "Unknown";

            SafeAccessToken? tokenHandle = null;
            try
            {
                if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_QUERY, out tokenHandle))
                    return "Unknown";

                using (tokenHandle)
                {
                    NativeMethods.GetTokenInformation(
                        tokenHandle,
                        TOKEN_INFORMATION_CLASS.TokenUser,
                        IntPtr.Zero,
                        0,
                        out int tokenInfoLength);

                    if (tokenInfoLength == 0)
                        return "Unknown";

                    using var tokenInfo = new SafeHGlobalHandle((int)tokenInfoLength);
                    if (NativeMethods.GetTokenInformation(
                        tokenHandle,
                        TOKEN_INFORMATION_CLASS.TokenUser,
                        tokenInfo.DangerousGetHandle(),
                        tokenInfoLength,
                        out tokenInfoLength))
                    {
                        var tokenUser = Marshal.PtrToStructure<TOKEN_USER>(tokenInfo.DangerousGetHandle());

                        var sid = new SecurityIdentifier(tokenUser.User.Sid);
                        try
                        {
                            var account = sid.Translate(typeof(NTAccount)) as NTAccount;
                            return account?.Value ?? "Unknown";
                        }
                        catch
                        {
                            return sid.Value;
                        }
                    }
                }
            }
            catch
            {
                // Ignore any errors and return Unknown
            }

            return "Unknown";
        }

        public static string GetProcessFullyQualifiedPath(int processId)
        {
            string path = string.Empty;

            using SafeProcessHandle processHandle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                (uint)processId);

            if (processHandle.IsInvalid)
            {
                return "Access Denied";
            }

            uint bufferSize = 1024;
            var pathBuilder = new StringBuilder((int)bufferSize);

            uint bufferLength = (uint)pathBuilder.Capacity + 1;
            if (NativeMethods.QueryFullProcessImageName(processHandle, 0, pathBuilder, ref bufferLength))
            {
                path = pathBuilder.ToString(0, (int)bufferLength).TrimEnd('\0');
            }

            return path;
        }

        public static FileVersionInfo? GetProcessFileVersionInfo(int processId)
        {
            string path = GetProcessFullyQualifiedPath(processId);
            if (string.IsNullOrEmpty(path))
                return null;

            return FileVersionInfo.GetVersionInfo(path);
        }

        public static string GetGrantedAccessString(uint grantedAccess)
        {
            // Map the granted access to RW- format
            // For simplicity, we'll map standard access rights
            var access = new StringBuilder(3);

            // Read access
            if ((grantedAccess & 0x120089) != 0)
                access.Append('R');
            else
                access.Append('-');

            // Write access
            if ((grantedAccess & 0x120116) != 0)
                access.Append('W');
            else
                access.Append('-');

            // Delete access
            if ((grantedAccess & 0x10000) != 0)
                access.Append('D');
            else
                access.Append('-');

            return access.ToString();
        }

        private static string RemoveTrailingBackslash(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.TrimEnd('\\');
        }
    }
}
