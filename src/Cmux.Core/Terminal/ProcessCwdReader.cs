using System.Runtime.InteropServices;
using System.Text;

namespace Cmux.Core.Terminal;

/// <summary>
/// Reads the current working directory of a remote process via its PEB.
/// Used to track shell CWD for shells that don't emit OSC 7.
/// </summary>
internal static class ProcessCwdReader
{
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    // x64 offsets
    private const int PebProcessParametersOffset = 0x20;
    private const int ProcessParamsCurrentDirectoryOffset = 0x38;
    // UNICODE_STRING on x64: Length (2) + MaxLength (2) + padding (4) + Buffer ptr (8)
    private const int UnicodeStringBufferOffset = 8;

    public static string? GetCurrentDirectory(IntPtr processHandle)
    {
        try
        {
            var status = NtQueryInformationProcess(
                processHandle, 0,
                out var pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _);

            if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
                return null;

            var processParamsPtr = ReadIntPtr(processHandle,
                pbi.PebBaseAddress + PebProcessParametersOffset);
            if (processParamsPtr == IntPtr.Zero)
                return null;

            var cwdAddress = processParamsPtr + ProcessParamsCurrentDirectoryOffset;

            var lengthBytes = new byte[2];
            if (!ReadProcessMemory(processHandle, cwdAddress, lengthBytes, 2, out _))
                return null;
            int length = BitConverter.ToUInt16(lengthBytes, 0);

            if (length == 0 || length > 2048)
                return null;

            var bufferPtr = ReadIntPtr(processHandle, cwdAddress + UnicodeStringBufferOffset);
            if (bufferPtr == IntPtr.Zero)
                return null;

            var stringBytes = new byte[length];
            if (!ReadProcessMemory(processHandle, bufferPtr, stringBytes, length, out _))
                return null;

            var dir = Encoding.Unicode.GetString(stringBytes);
            return NormalizePath(dir);
        }
        catch
        {
            return null;
        }
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var trimmed = path.TrimEnd('\\', '\0');

        // Keep trailing backslash for root paths like C:
        if (trimmed.Length == 2 && trimmed[1] == ':')
            return trimmed + "\\";

        return trimmed;
    }

    private static IntPtr ReadIntPtr(IntPtr processHandle, IntPtr address)
    {
        var buffer = new byte[IntPtr.Size];
        if (!ReadProcessMemory(processHandle, address, buffer, IntPtr.Size, out _))
            return IntPtr.Zero;
        return IntPtr.Size == 8
            ? (IntPtr)BitConverter.ToInt64(buffer, 0)
            : (IntPtr)BitConverter.ToInt32(buffer, 0);
    }
}
