using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Infrastructure.Native;

public static unsafe class NativeSystemBridge
{
    private static readonly Lazy<bool> _isNativeAvailable = new(CheckNativeAvailable);

    public static bool IsAvailable => _isNativeAvailable.Value;

    private static bool CheckNativeAvailable()
    {
        try
        {
            // Probe with invalid pointers/args just to ensure library loader finds appwatch_native.dll
            _ = NativeMethods.native_unblock_file(null);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets PIDs of processes holding a lock on the given file path using Restart Manager.
    /// </summary>
    public static IReadOnlyList<int> GetLockingProcesses(string filePath)
    {
        if (!IsAvailable || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Array.Empty<int>();

        try
        {
            Span<uint> pids = stackalloc uint[64];
            uint count = 0;

            fixed (char* pPath = filePath)
            fixed (uint* pPids = pids)
            {
                int res = NativeMethods.native_get_locking_processes((ushort*)pPath, pPids, (uint)pids.Length, &count);
                if (res != 0 || count == 0)
                    return Array.Empty<int>();

                var result = new List<int>((int)count);
                for (int i = 0; i < count; i++)
                {
                    result.Add((int)pids[i]);
                }
                return result;
            }
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    /// <summary>
    /// Inspects PE headers and Authenticode signatures using zero-copy memmap and WinVerifyTrust.
    /// </summary>
    public static bool TryInspectPe(string filePath, out SystemArchitecture architecture, out bool isSigned, out bool isTrusted, out long fileSize)
    {
        architecture = SystemArchitecture.Unknown;
        isSigned = false;
        isTrusted = false;
        fileSize = 0;

        if (!IsAvailable || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            NativePeInfo info = default;
            fixed (char* pPath = filePath)
            {
                int res = NativeMethods.native_inspect_pe((ushort*)pPath, &info);
                if (res != 0)
                    return false;

                fileSize = (long)info.file_size;
                isSigned = info.is_signed;
                isTrusted = info.is_trusted;

                if (info.is_arm64)
                    architecture = SystemArchitecture.Aarch64;
                else if (info.is_64bit)
                    architecture = SystemArchitecture.X86_64;
                else
                    architecture = SystemArchitecture.X86;

                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes the Zone.Identifier Alternate Data Stream (Mark-of-the-Web).
    /// </summary>
    public static bool UnblockFile(string filePath)
    {
        if (!IsAvailable || string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            fixed (char* pPath = filePath)
            {
                return NativeMethods.native_unblock_file((ushort*)pPath) == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Atomically replaces destinationFile with replacedByFile.
    /// </summary>
    public static bool ReplaceFile(string destinationFile, string replacedByFile, string? backupFile = null)
    {
        if (!IsAvailable || string.IsNullOrEmpty(destinationFile) || string.IsNullOrEmpty(replacedByFile))
            return false;

        try
        {
            fixed (char* pDest = destinationFile)
            fixed (char* pRepl = replacedByFile)
            fixed (char* pBack = backupFile)
            {
                ushort* backPtr = backupFile != null ? (ushort*)pBack : null;
                return NativeMethods.native_replace_file((ushort*)pDest, (ushort*)pRepl, backPtr) == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}
