using System;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PSADT.PInvoke;

namespace PSADT.Process
{
    internal delegate bool ReadMemoryDelegate(
        SafeProcessHandle hProcess,
        IntPtr lpBaseAddress,
        IntPtr lpBuffer,
        long nSize,
        out long lpNumberOfBytesRead);

    internal class ProcessStringReader
    {
        public static string GetProcessString(uint processId, bool isCommandLine)
        {
            try
            {
                using SafeProcessHandle processHandle = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ,
                    false,
                    processId);

                if (processHandle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                bool isTargetWow64Process = IsWow64Process(processHandle);
                bool isTarget64BitProcess = Environment.Is64BitOperatingSystem && !isTargetWow64Process;

                // All offset values below have been tested on Windows 7, 8, 10, and 11.
                // You can use WinDbg "dt ntdll!_PEB" command and search for ProcessParameters offset to find the values for any OS version.
                long processParametersOffset = isTarget64BitProcess ? 0x20 : 0x10;
                long pebOffset = isTarget64BitProcess ? (isCommandLine ? 0x70 : 0x38) : (isCommandLine ? 0x40 : 0x24);

                return GetProcessString(processHandle, processParametersOffset, pebOffset, isTargetWow64Process) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string? GetProcessString(
            SafeProcessHandle processHandle,
            long processParametersOffset,
            long pebOffset,
            bool isTargetWow64Process)
        {
            try
            {
                ReadMemoryDelegate readMemoryFunc;
                int pointerSize;
                long pebAddress;

                if (isTargetWow64Process)
                {
                    // Current process is 64-bit and target is 32-bit
                    readMemoryFunc = ReadProcessMemoryFunction;
                    pointerSize = 4;

                    // Get the PEB address
                    if (!TryReadPebAddressWow64(processHandle, out pebAddress))
                    {
                        Console.WriteLine("Failed to read PEB address.");
                        return null;
                    }

                    return ReadStringFromProcess<UNICODE_STRING_32>(
                        processHandle,
                        pebAddress,
                        processParametersOffset,
                        pebOffset,
                        readMemoryFunc,
                        pointerSize);
                }
                else if (Is32BitProcess() && TargetIs64Bit(processHandle))
                {
                    // Current process is 32-bit and target is 64-bit
                    readMemoryFunc = ReadVirtualMemory64Function;
                    pointerSize = 8;

                    // Get the PEB address
                    if (!TryReadPebAddressWow64Process64(processHandle, out pebAddress))
                    {
                        Console.WriteLine("Failed to read PEB address.");
                        return null;
                    }

                    return ReadStringFromProcess<UNICODE_STRING_WOW64>(
                        processHandle,
                        pebAddress,
                        processParametersOffset,
                        pebOffset,
                        readMemoryFunc,
                        pointerSize);
                }
                else
                {
                    // Both processes are of the same bitness
                    readMemoryFunc = ReadProcessMemoryFunction;
                    pointerSize = IntPtr.Size;

                    // Get the PEB address
                    if (!TryReadPebAddress(processHandle, out pebAddress))
                    {
                        Console.WriteLine("Failed to read PEB address.");
                        return null;
                    }

                    return ReadStringFromProcess<UNICODE_STRING>(
                        processHandle,
                        pebAddress,
                        processParametersOffset,
                        pebOffset,
                        readMemoryFunc,
                        pointerSize);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }

        public static bool TryReadPebAddress(
            SafeProcessHandle processHandle,
            out long pebAddress)
        {
            // Use NtQueryInformationProcess with ProcessBasicInformation
            int pbiSize = Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION));
            using var pbiHandle = new SafeHGlobalHandle(pbiSize);

            int status = NativeMethods.NtQueryInformationProcess(
                processHandle,
                PROCESSINFOCLASS.ProcessBasicInformation,
                pbiHandle.DangerousGetHandle(),
                (uint)pbiSize,
                out uint returnLength);

            if (status != 0)
            {
                Console.WriteLine($"NtQueryInformationProcess failed with status {status}");
                pebAddress = 0;
                return false;
            }

            object? obj = Marshal.PtrToStructure(pbiHandle.DangerousGetHandle(), typeof(PROCESS_BASIC_INFORMATION));
            if (obj == null)
            {
                Console.WriteLine("Failed to marshal PROCESS_BASIC_INFORMATION.");
                pebAddress = 0;
                return false;
            }
            PROCESS_BASIC_INFORMATION pbi = (PROCESS_BASIC_INFORMATION)obj;
            pebAddress = pbi.PebBaseAddress.ToInt64();
            return true;
        }

        public static bool TryReadPebAddressWow64(
            SafeProcessHandle processHandle,
            out long pebAddress)
        {
            // Use NtQueryInformationProcess with ProcessWow64Information
            using var pebHandle = new SafeHGlobalHandle(IntPtr.Size);
            int status = NativeMethods.NtQueryInformationProcess(
                processHandle,
                PROCESSINFOCLASS.ProcessWow64Information,
                pebHandle.DangerousGetHandle(),
                (uint)IntPtr.Size,
                out uint returnLength);

            if (status != 0)
            {
                Console.WriteLine($"NtQueryInformationProcess failed with status {status}");
                pebAddress = 0;
                return false;
            }

            pebAddress = Marshal.ReadIntPtr(pebHandle.DangerousGetHandle()).ToInt64();
            return true;
        }

        public static bool TryReadPebAddressWow64Process64(
            SafeProcessHandle processHandle,
            out long pebAddress)
        {
            // Use NtWow64QueryInformationProcess64
            int pbiSize = Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION_WOW64));
            using var pbiHandle = new SafeHGlobalHandle(pbiSize);

            int status = NativeMethods.NtWow64QueryInformationProcess64(
                processHandle,
                PROCESSINFOCLASS.ProcessBasicInformation,
                pbiHandle.DangerousGetHandle(),
                (ulong)pbiSize,
                out ulong returnLength);

            if (status != 0)
            {
                Console.WriteLine($"NtWow64QueryInformationProcess64 failed with status {status}");
                pebAddress = 0;
                return false;
            }

            object? obj = Marshal.PtrToStructure(pbiHandle.DangerousGetHandle(), typeof(PROCESS_BASIC_INFORMATION_WOW64));
            if (obj == null)
            {
                Console.WriteLine("Failed to marshal PROCESS_BASIC_INFORMATION_WOW64.");
                pebAddress = 0;
                return false;
            }
            PROCESS_BASIC_INFORMATION_WOW64 pbi = (PROCESS_BASIC_INFORMATION_WOW64)obj;
            pebAddress = pbi.PebBaseAddress;
            return true;
        }

        public static string? ReadStringFromProcess<TUnicodeString>(
            SafeProcessHandle processHandle,
            long pebAddress,
            long processParametersOffset,
            long stringOffset,
            ReadMemoryDelegate readMemoryFunc,
            int pointerSize) where TUnicodeString : struct
        {
            // Read the address of ProcessParameters
            if (!TryReadPointer(processHandle, pebAddress + processParametersOffset, readMemoryFunc, pointerSize, out long processParametersAddress))
            {
                Console.WriteLine("Failed to read ProcessParameters address.");
                return null;
            }

            // Read the UNICODE_STRING structure
            if (!TryReadStructure<TUnicodeString>(processHandle, processParametersAddress + stringOffset, readMemoryFunc, out TUnicodeString us))
            {
                Console.WriteLine("Failed to read UNICODE_STRING.");
                return null;
            }

            // Extract Buffer and Length from TUnicodeString
            long bufferAddress;
            ushort length;
            if (typeof(TUnicodeString) == typeof(UNICODE_STRING))
            {
                var unicodeString = (UNICODE_STRING)(object)us;
                bufferAddress = unicodeString.Buffer.ToInt64();
                length = unicodeString.Length;
            }
            else if (typeof(TUnicodeString) == typeof(UNICODE_STRING_32))
            {
                var unicodeString = (UNICODE_STRING_32)(object)us;
                bufferAddress = unicodeString.Buffer;
                length = unicodeString.Length;
            }
            else if (typeof(TUnicodeString) == typeof(UNICODE_STRING_WOW64))
            {
                var unicodeString = (UNICODE_STRING_WOW64)(object)us;
                bufferAddress = unicodeString.Buffer;
                length = unicodeString.Length;
            }
            else
            {
                Console.WriteLine($"Unsupported UNICODE_STRING type: {typeof(TUnicodeString).Name}");
                return null;
            }

            if (bufferAddress == 0 || length == 0)
            {
                Console.WriteLine(bufferAddress == 0 ? "Buffer is null" : "Length is zero");
                return null;
            }

            // Read the actual string data
            using var stringHandle = new SafeHGlobalHandle(length);
            if (!readMemoryFunc(
                    processHandle,
                    new IntPtr(bufferAddress),
                    stringHandle.DangerousGetHandle(),
                    length,
                    out long bytesRead))
            {
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"ReadMemoryFunc (string data) failed with error code {errorCode}");
                return null;
            }

            byte[] buffer = new byte[length];
            Marshal.Copy(stringHandle.DangerousGetHandle(), buffer, 0, length);

            string? processString = Encoding.Unicode.GetString(buffer);
            return processString?.TrimEnd('\0');
        }

        public static bool TryReadPointer(
            SafeProcessHandle processHandle,
            long address,
            ReadMemoryDelegate readMemoryFunc,
            int pointerSize,
            out long value)
        {
            using var buffer = new SafeHGlobalHandle(pointerSize);
            if (!readMemoryFunc(
                    processHandle,
                    new IntPtr(address),
                    buffer.DangerousGetHandle(),
                    pointerSize,
                    out long bytesRead))
            {
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"ReadMemoryFunc failed with error code {errorCode}");
                value = 0;
                return false;
            }

            if (pointerSize == 4)
                value = Marshal.ReadInt32(buffer.DangerousGetHandle());
            else
                value = Marshal.ReadInt64(buffer.DangerousGetHandle());

            return true;
        }

        public static bool TryReadStructure<T>(
            SafeProcessHandle processHandle,
            long address,
            ReadMemoryDelegate readMemoryFunc,
            out T result) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            using var buffer = new SafeHGlobalHandle(size);

            if (!readMemoryFunc(
                    processHandle,
                    new IntPtr(address),
                    buffer.DangerousGetHandle(),
                    size,
                    out long bytesRead))
            {
                int errorCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"ReadMemoryFunc failed with error code {errorCode}");
                result = default;
                return false;
            }

            object? obj = Marshal.PtrToStructure(buffer.DangerousGetHandle(), typeof(T));
            if (obj == null)
            {
                Console.WriteLine($"Failed to marshal {typeof(T).Name}.");
                result = default;
                return false;
            }
            result = (T)obj;
            return true;
        }

        public static bool Is32BitProcess()
        {
            return IntPtr.Size == 4;
        }

        public static bool TargetIs64Bit(SafeProcessHandle processHandle)
        {
            bool isWow64;
            if (!NativeMethods.IsWow64Process(processHandle, out isWow64))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return !isWow64;
        }

        public static bool IsWow64Process(SafeProcessHandle processHandle)
        {
            bool isWow64;
            if (!NativeMethods.IsWow64Process(processHandle, out isWow64))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return isWow64;
        }

        public static bool ReadProcessMemoryFunction(
            SafeProcessHandle hProcess,
            IntPtr lpBaseAddress,
            IntPtr lpBuffer,
            long nSize,
            out long lpNumberOfBytesRead)
        {
            return NativeMethods.ReadProcessMemory(
                hProcess,
                lpBaseAddress,
                lpBuffer,
                nSize,
                out lpNumberOfBytesRead);
        }

        public static bool ReadVirtualMemory64Function(
            SafeProcessHandle hProcess,
            IntPtr lpBaseAddress,
            IntPtr lpBuffer,
            long nSize,
            out long lpNumberOfBytesRead)
        {
            int status = NativeMethods.NtWow64ReadVirtualMemory64(
                hProcess,
                lpBaseAddress.ToInt64(),
                lpBuffer,
                nSize,
                out lpNumberOfBytesRead);
            return status == 0;
        }
    }
}
