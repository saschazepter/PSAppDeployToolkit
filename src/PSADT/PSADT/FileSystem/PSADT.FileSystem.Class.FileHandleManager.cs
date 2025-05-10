﻿using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PSADT.Execution;
using PSADT.Extensions;
using PSADT.LibraryInterfaces;
using PSADT.SafeHandles;
using PSADT.Types;
using PSADT.Utilities;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Threading;
using Windows.Wdk.Foundation;

namespace PSADT.FileSystem
{
    /// <summary>
    /// Provides methods to manage file handles.
    /// </summary>
    public static class FileHandleManager
    {
        /// <summary>
        /// Retrieves a list of open handles, optionally filtered by path.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public static IReadOnlyList<FileHandleInfo> GetOpenHandles(string? directoryPath = null)
        {
            // Pre-calculate the sizes of the structures we need to read.
            var handleEntryExSize = Marshal.SizeOf<NtDll.SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();
            var handleInfoExSize = Marshal.SizeOf<NtDll.SYSTEM_HANDLE_INFORMATION_EX>();

            // Query the total system handle information.
            using var handleBufferPtr = SafeHGlobalHandle.Alloc(handleInfoExSize + handleEntryExSize);
            var status = NtDll.NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation, handleBufferPtr, out int handleBufferReqLength);
            while (status == NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
            {
                handleBufferPtr.ReAlloc(handleBufferReqLength);
                status = NtDll.NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation, handleBufferPtr, out handleBufferReqLength);
            }

            // Set up required pointers for GetObjectName().
            using var currentProcessHandle = Kernel32.GetCurrentProcess();
            using var objectBufferPtr = SafeHGlobalHandle.Alloc(1024);
            using var hKernel32Ptr = Kernel32.LoadLibrary("kernel32.dll");
            using var hNtdllPtr = Kernel32.LoadLibrary("ntdll.dll");
            var ntQueryObject = Kernel32.GetProcAddress(hNtdllPtr, "NtQueryObject");
            var exitThread = Kernel32.GetProcAddress(hKernel32Ptr, "ExitThread");

            // Build a lookup table of NT device names. This must be built at runtime
            // as the device names are not static and can change between invocations.
            var ntPathLookupTable = FileSystemUtilities.GetNtPathLookupTable();

            // Start looping through all handles.
            var handleCount = handleBufferPtr.ToStructure<NtDll.SYSTEM_HANDLE_INFORMATION_EX>().NumberOfHandles.ToUInt32();
            var entryOffset = handleInfoExSize;
            var openHandles = new List<FileHandleInfo>();
            for (int i = 0; i < handleCount; i++)
            {
                // Read the handle information into a structure, skipping over if it's not a file or directory handle.
                var sysHandle = handleBufferPtr.ToStructure<NtDll.SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(entryOffset + (handleEntryExSize * i));
                if (!ObjectTypeLookupTable.TryGetValue(sysHandle.ObjectTypeIndex, out string? objectType) || (objectType != "File" && objectType != "Directory"))
                {
                    continue;
                }

                // Open the owning process with rights to duplicate handles.
                SafeFileHandle fileProcessHandle;
                try
                {
                    fileProcessHandle = Kernel32.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, false, sysHandle.UniqueProcessId.ToUInt32());
                }
                catch (UnauthorizedAccessException ex) when (ex.HResult == HRESULT.E_ACCESSDENIED)
                {
                    continue;
                }
                catch (ArgumentException ex) when (ex.HResult == HRESULT.E_INVALIDARG)
                {
                    continue;
                }

                // Duplicate the remote handle into our process.
                SafeFileHandle fileDupHandle;
                try
                {
                    using (var fileOpenHandle = new SafeFileHandle((HANDLE)sysHandle.HandleValue, false))
                    {
                        Kernel32.DuplicateHandle(fileProcessHandle, fileOpenHandle, currentProcessHandle, out fileDupHandle, 0, true, DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS);
                    }
                }
                catch (Win32Exception ex) when ((ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_NOT_SUPPORTED) || (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_INVALID_HANDLE))
                {
                    continue;
                }
                catch (UnauthorizedAccessException ex) when (ex.HResult == HRESULT.E_ACCESSDENIED)
                {
                    continue;
                }
                finally
                {
                    fileProcessHandle.Dispose();
                }

                // Get the handle's name to check if it's a hard drive path.
                string? objectName;
                try
                {
                    objectName = GetObjectName(currentProcessHandle, fileDupHandle, ntQueryObject, exitThread, objectBufferPtr);
                    if (string.IsNullOrWhiteSpace(objectName) || !objectName!.StartsWith("\\Device\\HarddiskVolume"))
                    {
                        continue;
                    }
                }
                finally
                {
                    objectBufferPtr.Clear();
                    fileDupHandle.Dispose();
                }

                // Add the handle information to the list if it matches the specified directory path.
                string objectNameKey = $"\\{string.Join("\\", objectName.Split(['\\'], StringSplitOptions.RemoveEmptyEntries).Take(2))}";
                if (ntPathLookupTable.TryGetValue(objectNameKey, out string? driveLetter) && objectName.Replace(objectNameKey, driveLetter) is string dosPath && (null == directoryPath || dosPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase)))
                {
                    openHandles.Add(new FileHandleInfo(sysHandle, dosPath, objectName, objectType));
                }
            }
            return openHandles.AsReadOnly();
        }

        /// <summary>
        /// Retrieves a list of open handles for the system.
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<FileHandleInfo> GetOpenHandles()
        {
            return GetOpenHandles(null);
        }

        /// <summary>
        /// Closes the specified handles.
        /// </summary>
        /// <param name="handleEntries"></param>
        public static void CloseHandles(NtDll.SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX[] handleEntries)
        {
            // Confirm the provided input isn't null.
            if (null == handleEntries)
            {
                throw new ArgumentNullException(nameof(handleEntries));
            }

            // Open each process handle, duplicate it with close source flag, then close the duplicated handle to close the original handle.
            using (var currentProcessHandle = Kernel32.GetCurrentProcess())
            {
                foreach (var handleEntry in handleEntries)
                {
                    using (var fileProcessHandle = Kernel32.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, false, handleEntry.UniqueProcessId.ToUInt32()))
                    using (var fileOpenHandle = new SafeFileHandle((HANDLE)handleEntry.HandleValue, false))
                    {
                        Kernel32.DuplicateHandle(fileProcessHandle, fileOpenHandle, currentProcessHandle, out var localHandle, 0, false, DUPLICATE_HANDLE_OPTIONS.DUPLICATE_CLOSE_SOURCE);
                        localHandle.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the name of an object associated with a handle.
        /// </summary>
        /// <param name="fileHandle"></param>
        /// <param name="ntQueryObject"></param>
        /// <param name="exitThread"></param>
        /// <param name="objectBuffer"></param>
        /// <returns></returns>
        private static string? GetObjectName(SafeFileHandle currentProcessHandle, SafeFileHandle fileHandle, FARPROC ntQueryObject, FARPROC exitThread, SafeHGlobalHandle objectBuffer)
        {
            if (fileHandle is not object || fileHandle.IsClosed || fileHandle.IsInvalid)
            {
                throw new ArgumentNullException(nameof(fileHandle));
            }
            if (objectBuffer is not object || objectBuffer.IsClosed || objectBuffer.IsInvalid)
            {
                throw new ArgumentNullException(nameof(objectBuffer));
            }

            bool fileHandleAddRef = false;
            bool objectBufferAddRef = false;
            try
            {
                // Start the thread to retrieve the object name and wait for the outcome.
                using (var shellcode = GetObjectTypeShellcode(exitThread, ntQueryObject, fileHandle.DangerousGetHandle(), OBJECT_INFORMATION_CLASS.ObjectNameInformation, objectBuffer.DangerousGetHandle(), objectBuffer.Length))
                {
                    NtDll.NtCreateThreadEx(out var hThread, THREAD_ACCESS_RIGHTS.THREAD_ALL_ACCESS, IntPtr.Zero, currentProcessHandle, shellcode, IntPtr.Zero, 0, 0, 0, 0, IntPtr.Zero);
                    using (hThread)
                    {
                        // Terminate the thread if it's taking longer than our timeout (NtQueryObject() has hung).
                        if (PInvoke.WaitForSingleObject(hThread, (uint)GetObjectNameThreadTimeout.Milliseconds) == WAIT_EVENT.WAIT_TIMEOUT)
                        {
                            NtDll.NtTerminateThread(hThread, NTSTATUS.STATUS_TIMEOUT);
                        }

                        // Get the exit code of the thread and throw an exception if it failed.
                        Kernel32.GetExitCodeThread(hThread, out var exitCode);
                        try
                        {
                            if ((NTSTATUS)ValueTypeConverter<int>.Convert(exitCode) is NTSTATUS res && res != NTSTATUS.STATUS_SUCCESS)
                            {
                                throw ExceptionUtilities.GetExceptionForLastWin32Error((WIN32_ERROR)PInvoke.RtlNtStatusToDosError(res));
                            }
                        }
                        catch (Win32Exception ex) when ((ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_NOT_SUPPORTED) || (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_BAD_PATHNAME) || (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_TIMEOUT) || (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_IO_PENDING))
                        {
                            return null;
                        }
                        catch (UnauthorizedAccessException ex) when (ex.HResult == HRESULT.E_ACCESSDENIED)
                        {
                            return null;
                        }
                        return objectBuffer.ToStructure<OBJECT_NAME_INFORMATION>().Name.Buffer.ToString()?.TrimRemoveNull();
                    }
                }
            }
            finally
            {
                if (fileHandleAddRef)
                {
                    fileHandle.DangerousRelease();
                }
                if (objectBufferAddRef)
                {
                    objectBuffer.DangerousRelease();
                }
            }
        }

        /// <summary>
        /// Retrieves a lookup table of object types.
        /// </summary>
        /// <returns></returns>
        private static ReadOnlyDictionary<ushort, string> GetObjectTypeLookupTable()
        {
            // Pre-calculate the sizes of the structures we need to read.
            var objectTypesSize = NtDll.ObjectInfoClassSizes[OBJECT_INFORMATION_CLASS.ObjectTypesInformation];
            var objectTypeSize = NtDll.ObjectInfoClassSizes[OBJECT_INFORMATION_CLASS.ObjectTypeInformation];

            // Query the system for all object type info.
            using var typesBufferPtr = SafeHGlobalHandle.Alloc(objectTypesSize);
            var status = NtDll.NtQueryObject(SafeBaseHandle.NullHandle, OBJECT_INFORMATION_CLASS.ObjectTypesInformation, typesBufferPtr, out int typesBufferReqLength);
            while (status == NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
            {
                typesBufferPtr.ReAlloc(typesBufferReqLength);
                status = NtDll.NtQueryObject(SafeBaseHandle.NullHandle, OBJECT_INFORMATION_CLASS.ObjectTypesInformation, typesBufferPtr, out typesBufferReqLength);
            }

            // Read the number of types from the buffer and return a built-out dictionary.
            var typesCount = typesBufferPtr.ToStructure<NtDll.OBJECT_TYPES_INFORMATION>().NumberOfTypes;
            var typeTable = new Dictionary<ushort, string>((int)typesCount);
            var ptrOffset = LibraryUtilities.AlignUp(objectTypesSize);
            for (uint i = 0; i < typesCount; i++)
            {
                // Marshal the data into our structure and add the necessary values to the dictionary.
                var typeInfo = typesBufferPtr.ToStructure<NtDll.OBJECT_TYPE_INFORMATION>(ptrOffset);
                typeTable.Add(typeInfo.TypeIndex, typeInfo.TypeName.Buffer.ToString().TrimRemoveNull());
                ptrOffset += objectTypeSize + LibraryUtilities.AlignUp(typeInfo.TypeName.MaximumLength);
            }
            return new ReadOnlyDictionary<ushort, string>(typeTable);
        }

        /// <summary>
        /// The context for the thread that retrieves the object name.
        /// </summary>
        /// <param name="exitThread"></param>
        /// <param name="ntQueryObject"></param>
        /// <param name="fileHandle"></param>
        /// <param name="infoClass"></param>
        /// <param name="infoBuffer"></param>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        private static SafeVirtualAllocHandle GetObjectTypeShellcode(IntPtr exitThread, IntPtr ntQueryObject, IntPtr fileHandle, OBJECT_INFORMATION_CLASS infoClass, IntPtr infoBuffer, int infoBufferLength)
        {
            // Build the shellcode stub to call NtQueryObject.
            var shellcode = new List<byte>();
            switch (ProcessManager.ProcessArchitecture)
            {
                case SystemArchitecture.AMD64:
                    // mov rcx, handle
                    shellcode.Add(0x48); shellcode.Add(0xB9);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)fileHandle));

                    // mov rdx, infoClass
                    shellcode.Add(0x48); shellcode.Add(0xBA);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)(uint)infoClass));

                    // mov r8, buffer
                    shellcode.Add(0x49); shellcode.Add(0xB8);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)infoBuffer));

                    // mov r9, bufferSize
                    shellcode.Add(0x49); shellcode.Add(0xB9);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)infoBufferLength));

                    // sub rsp, 0x28 — shadow space + ReturnLength
                    shellcode.Add(0x48); shellcode.Add(0x83); shellcode.Add(0xEC); shellcode.Add(0x28);

                    // mov qword [rsp + 0x20], 0  (null for PULONG ReturnLength)
                    shellcode.Add(0x48); shellcode.Add(0xC7); shellcode.Add(0x44); shellcode.Add(0x24); shellcode.Add(0x20);
                    shellcode.AddRange(new byte[4]); // 0

                    // mov rax, NtQueryObject
                    shellcode.Add(0x48); shellcode.Add(0xB8);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)ntQueryObject));

                    // call rax
                    shellcode.Add(0xFF); shellcode.Add(0xD0);

                    // mov ecx, eax (exit code)
                    shellcode.Add(0x89); shellcode.Add(0xC1);

                    // mov rax, ExitThread
                    shellcode.Add(0x48); shellcode.Add(0xB8);
                    shellcode.AddRange(BitConverter.GetBytes((ulong)exitThread));

                    // call rax
                    shellcode.Add(0xFF); shellcode.Add(0xD0);
                    break;
                case SystemArchitecture.i386:
                    // push NULL (ReturnLength)
                    shellcode.Add(0x6A);
                    shellcode.Add(0x00);

                    // push bufferSize
                    shellcode.Add(0x68);
                    shellcode.AddRange(BitConverter.GetBytes(infoBufferLength));

                    // push buffer
                    shellcode.Add(0x68);
                    shellcode.AddRange(BitConverter.GetBytes(infoBuffer.ToInt32()));

                    // push infoClass
                    shellcode.Add(0x68);
                    shellcode.AddRange(BitConverter.GetBytes((int)infoClass));

                    // push handle
                    shellcode.Add(0x68);
                    shellcode.AddRange(BitConverter.GetBytes(fileHandle.ToInt32()));

                    // mov eax, NtQueryObject
                    shellcode.Add(0xB8);
                    shellcode.AddRange(BitConverter.GetBytes(ntQueryObject.ToInt32()));

                    // call eax
                    shellcode.Add(0xFF); shellcode.Add(0xD0);

                    // push eax (NTSTATUS)
                    shellcode.Add(0x50);

                    // mov eax, ExitThread
                    shellcode.Add(0xB8);
                    shellcode.AddRange(BitConverter.GetBytes(exitThread.ToInt32()));

                    // call eax
                    shellcode.Add(0xFF); shellcode.Add(0xD0);
                    break;
                case SystemArchitecture.ARM64:
                    // x0 = handle
                    var code = new List<uint>();
                    code.AddRange(NativeUtilities.Load64(0, (ulong)fileHandle.ToInt64()));

                    // x1 = infoClass (zero-extended)
                    code.AddRange(NativeUtilities.Load64(1, (ulong)infoClass));

                    // x2 = buffer
                    code.AddRange(NativeUtilities.Load64(2, (ulong)infoBuffer.ToInt64()));

                    // x3 = bufferSize
                    code.AddRange(NativeUtilities.Load64(3, (ulong)infoBufferLength));

                    // x4 = NULL (for ReturnLength)
                    code.AddRange(NativeUtilities.Load64(4, 0));

                    // x16 = NtQueryObject
                    code.AddRange(NativeUtilities.Load64(16, (ulong)ntQueryObject.ToInt64()));

                    // br x16
                    code.Add(NativeUtilities.EncodeBr(16));

                    // x16 = ExitThread. result is in x0 → already correct for ExitThread
                    code.AddRange(NativeUtilities.Load64(16, (ulong)exitThread.ToInt64()));

                    // br x16
                    code.Add(NativeUtilities.EncodeBr(16));

                    // Convert instruction list to byte array
                    foreach (var instr in code)
                    {
                        shellcode.AddRange(BitConverter.GetBytes(instr));
                    }
                    break;
                default:
                    throw new PlatformNotSupportedException("Unsupported architecture: " + ProcessManager.ProcessArchitecture);
            }
            SafeVirtualAllocHandle mem = Kernel32.VirtualAlloc(IntPtr.Zero, (UIntPtr)shellcode.Count, VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT | VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE);
            mem.Write(shellcode.ToArray());
            return mem;
        }

        /// <summary>
        /// The lookup table of object types.
        /// </summary>
        private static readonly ReadOnlyDictionary<ushort, string> ObjectTypeLookupTable = GetObjectTypeLookupTable();

        /// <summary>
        /// The duration to wait for a hung NtQueryObject thread to terminate.
        /// </summary>
        private static readonly TimeSpan GetObjectNameThreadTimeout = TimeSpan.FromMilliseconds(125);
    }
}
