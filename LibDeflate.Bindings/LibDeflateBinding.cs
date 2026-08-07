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

    /// <summary>
    /// Highest libdeflate.so.N suffix probed on Linux. libdeflate has been .so.0 everywhere we
    /// checked (Debian, Ubuntu, Fedora, Alpine); the range exists because the digit is per-library
    /// and per-distro, not a convention.
    /// </summary>
    private const int MaxSoVersion = 9;

    static NativeMethods() => NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != AssemblyName)
        {
            return IntPtr.Zero;
        }

        var libName = GetPlatformLibraryName();
        var assemblyLocation = assembly.Location;

        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (assemblyDir != null)
            {
                // Bundled: runtimes/{rid}/native/ (standard NuGet layout for non-published builds)
                var runtimesPath = Path.Combine(assemblyDir, "runtimes", GetRuntimeIdentifier(), "native", libName);
                if (File.Exists(runtimesPath) && NativeLibrary.TryLoad(runtimesPath, out var runtimesHandle))
                {
                    return runtimesHandle;
                }

                // Bundled: directly next to the assembly (published apps)
                var bundledPath = Path.Combine(assemblyDir, libName);
                if (File.Exists(bundledPath) && NativeLibrary.TryLoad(bundledPath, out var bundledHandle))
                {
                    return bundledHandle;
                }
            }
        }

        // The loader's own search path, unversioned. This is what an install with the -dev package
        // resolves on, so it stays ahead of the versioned probe below.
        if (NativeLibrary.TryLoad(libName, assembly, searchPath, out var handle))
        {
            return handle;
        }

        // Linux ships libdeflate.so.N and only the -dev package adds the unversioned symlink the
        // step above needs. Probe versions by bare name so this still goes through the full loader
        // search path (LD_LIBRARY_PATH, /etc/ld.so.conf.d). Descending prefers the newest ABI.
        if (OperatingSystem.IsLinux())
        {
            for (var soVersion = MaxSoVersion; soVersion >= 0; soVersion--)
            {
                if (NativeLibrary.TryLoad($"{AssemblyName}.so.{soVersion}", assembly, searchPath, out handle))
                {
                    return handle;
                }
            }
        }

        // Homebrew on Apple Silicon is off the default dyld search path.
        if (OperatingSystem.IsMacOS() &&
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
            NativeLibrary.TryLoad($"/opt/homebrew/lib/{OSXAssemblyName}", out handle))
        {
            return handle;
        }

        throw new DllNotFoundException(BuildNotFoundMessage());
    }

    private static string BuildNotFoundMessage()
    {
        if (OperatingSystem.IsLinux())
        {
            return $"""
                   Could not load {AssemblyName}. Tried {UnixAssemblyName} and {AssemblyName}.so.0 through {AssemblyName}.so.{MaxSoVersion} on the loader path, and runtimes/{GetRuntimeIdentifier()}/native/ next to the assembly.

                   Install the runtime library. The -dev package is NOT required:
                     Debian/Ubuntu   sudo apt-get install -y libdeflate0
                     Fedora/RHEL     sudo dnf install -y libdeflate
                     Alpine          apk add libdeflate
                   """;
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"Could not load {AssemblyName}. Install it with: brew install libdeflate";
        }

        return $"Could not load {AssemblyName}. The bundled runtimes/{GetRuntimeIdentifier()}/native/{GetPlatformLibraryName()} is missing from the package.";
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
