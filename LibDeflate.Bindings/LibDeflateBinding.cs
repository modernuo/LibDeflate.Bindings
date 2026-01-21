using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.IO.Compression;

public enum LibDeflateResult
{
    Success = 0,
    BadData = 1,
    ShortOutput = 2,
    InsufficientSpace = 3,
}

public enum LibDeflateCompressionLevel
{
    None = 0,
    VeryLow = 1,
    Low = 3,
    Default = 6,
    High = 9,
    VeryHigh = 12
}

public sealed unsafe class LibDeflateBinding : IDisposable
{
    private readonly nint _compressor;
    private readonly nint _decompressor;

    public LibDeflateBinding(LibDeflateCompressionLevel compressionLevel = LibDeflateCompressionLevel.Default)
    {
        _compressor = NativeMethods.libdeflate_alloc_compressor((int)compressionLevel);
        _decompressor = NativeMethods.libdeflate_alloc_decompressor();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int MaxPackSize(int inputLength) =>
        (int)NativeMethods.libdeflate_zlib_compress_bound(_compressor, (nuint)inputLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Pack(Span<byte> dest, ReadOnlySpan<byte> source)
    {
        fixed (byte* inputPtr = source)
        {
            fixed (byte* outputPtr = dest)
            {
                return (int)NativeMethods.libdeflate_zlib_compress(
                    _compressor,
                    inputPtr,
                    (nuint)source.Length,
                    outputPtr,
                    (nuint)dest.Length
                );
            }
        }
    }

    public LibDeflateResult Unpack(Span<byte> dest, ReadOnlySpan<byte> source, out int uncompressedLength)
    {
        LibDeflateResult result;
        nuint bytesWritten;
        fixed (byte* inputPtr = source)
        {
            fixed (byte* outputPtr = dest)
            {
                result = NativeMethods.libdeflate_zlib_decompress(
                    _decompressor,
                    inputPtr,
                    (nuint)source.Length,
                    outputPtr,
                    (nuint)dest.Length,
                    out bytesWritten
                );
            }
        }

        if (result == LibDeflateResult.Success)
        {
            uncompressedLength = (int)bytesWritten;
            return LibDeflateResult.Success;
        }

        uncompressedLength = 0;
        return result;
    }

    private void ReleaseUnmanagedResources()
    {
        NativeMethods.libdeflate_free_compressor(_compressor);
        NativeMethods.libdeflate_free_decompressor(_decompressor);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~LibDeflateBinding()
    {
        ReleaseUnmanagedResources();
    }
}

internal static unsafe partial class NativeMethods
{
    public const string AssemblyName = "libdeflate";
    public const string WindowsAssemblyName = $"{AssemblyName}.dll";
    public const string OSXAssemblyName = $"{AssemblyName}.dylib";
    public const string UnixAssemblyName = $"{AssemblyName}.so";

    static NativeMethods() => NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != AssemblyName)
        {
            return IntPtr.Zero;
        }

        var assemblyLocation = assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (assemblyDir != null)
            {
                var libName = GetPlatformLibraryName();

                // Try runtimes/{rid}/native/ folder (standard NuGet layout for non-published builds)
                var runtimesPath = Path.Combine(assemblyDir, "runtimes", GetRuntimeIdentifier(), "native", libName);
                if (File.Exists(runtimesPath) && NativeLibrary.TryLoad(runtimesPath, out var runtimesHandle))
                {
                    return runtimesHandle;
                }

                // Try directly next to assembly (published apps)
                var bundledPath = Path.Combine(assemblyDir, libName);
                if (File.Exists(bundledPath) && NativeLibrary.TryLoad(bundledPath, out var bundledHandle))
                {
                    return bundledHandle;
                }
            }
        }

        // macOS ARM64: Try Homebrew path
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            if (NativeLibrary.TryLoad($"/opt/homebrew/lib/{AssemblyName}.dylib", out var homebrewHandle))
            {
                return homebrewHandle;
            }
        }

        // Fall back to default resolution
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
        {
            return handle;
        }

        throw new DllNotFoundException(
            $"Could not load {libraryName}. " +
            (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "On macOS, install via: brew install libdeflate"
                : "Ensure libdeflate is installed on your system."));
    }

    private static string GetPlatformLibraryName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsAssemblyName :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSXAssemblyName :
        UnixAssemblyName;

    private static string GetRuntimeIdentifier()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64"
        };

        return $"{os}-{arch}";
    }

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint libdeflate_alloc_compressor(int compression_level);

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint libdeflate_zlib_compress(
        nint compressor, byte* @in, nuint in_nbytes, byte* @out, nuint out_nbytes_avail
    );

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint libdeflate_zlib_compress_bound(nint compressor, nuint in_nbytes);

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void libdeflate_free_compressor(nint compressor);

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint libdeflate_alloc_decompressor();

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial LibDeflateResult libdeflate_zlib_decompress(
        nint decompressor, byte* @in, nuint in_nbytes, byte* @out, nuint out_nbytes_avail,
        out nuint actual_out_nbytes_ret
    );

    [LibraryImport(AssemblyName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void libdeflate_free_decompressor(nint decompressor);
}
