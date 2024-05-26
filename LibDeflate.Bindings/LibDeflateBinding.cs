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
    private const string DllName = "libdeflate";

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint libdeflate_alloc_compressor(int compression_level);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint libdeflate_zlib_compress(
        nint compressor, byte* @in, nuint in_nbytes, byte* @out, nuint out_nbytes_avail
    );

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint libdeflate_zlib_compress_bound(nint compressor, nuint in_nbytes);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void libdeflate_free_compressor(nint compressor);

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nint libdeflate_alloc_decompressor();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial LibDeflateResult libdeflate_zlib_decompress(
        nint decompressor, byte* @in, nuint in_nbytes, byte* @out, nuint out_nbytes_avail,
        out nuint actual_out_nbytes_ret
    );

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void libdeflate_free_decompressor(nint decompressor);
}
