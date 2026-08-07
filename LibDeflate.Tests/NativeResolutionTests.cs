using System.IO.Compression;

namespace LibDeflate.Tests;

/// <summary>
/// Runs a fact only when LIBDEFLATE_TEST_MODE matches, otherwise reports it as skipped. xunit v2
/// has no runtime skip, so the decision is made at discovery time.
/// </summary>
public sealed class NativeModeFactAttribute : FactAttribute
{
    public const string ModeVariable = "LIBDEFLATE_TEST_MODE";

    public NativeModeFactAttribute(string mode, string requires)
    {
        if (Environment.GetEnvironmentVariable(ModeVariable) != mode)
        {
            Skip = $"Requires {ModeVariable}={mode} ({requires}).";
        }
    }
}

/// <summary>
/// Native resolution depends entirely on which packages exist on the machine, so these only run
/// inside the CI containers that control that. On a dev box the package bundles libdeflate.dll or
/// libdeflate.dylib and resolution never reaches the versioned-SONAME path, so running them there
/// would prove nothing.
/// </summary>
public class NativeResolutionTests
{
    /// <summary>
    /// The bug this whole change exists to fix: libdeflate.so.0 is installed and loadable, but
    /// DllImport never asks for a versioned SONAME, so only the -dev package's unversioned symlink
    /// made resolution work.
    /// </summary>
    [NativeModeFact("runtime-only", "container with libdeflate0 and no -dev")]
    public void RuntimePackageOnly_ResolvesViaVersionedSoname() => AssertRoundTrips();

    /// <summary>
    /// Regression guard for servers already set up the documented way. With the unversioned symlink
    /// present, resolution must still succeed on the plain-name step and never reach the versioned
    /// probe.
    /// </summary>
    [NativeModeFact("dev", "container with libdeflate-dev")]
    public void DevPackageInstalled_StillResolves() => AssertRoundTrips();

    [NativeModeFact("missing", "container with no libdeflate at all")]
    public void NoNativeLibrary_ThrowsWithRuntimePackageInstructions()
    {
        var exception = Record.Exception(() => new LibDeflateBinding());

        var dllNotFound = Unwrap<DllNotFoundException>(exception);
        Assert.NotNull(dllNotFound);

        // Names the runtime package, so an operator is not sent to install -dev.
        Assert.Contains("libdeflate0", dllNotFound.Message, StringComparison.Ordinal);
        // Says what was actually tried, so this reads as a missing package and not a broken build.
        // Assert the stated range, not a single name -- libdeflate's SONAME is .so.0 and argon2's
        // is .so.1 on the same machine, which is why a range is probed at all.
        Assert.Contains("libdeflate.so.0 through libdeflate.so.9", dllNotFound.Message, StringComparison.Ordinal);
        // Says outright that -dev is not the answer.
        Assert.Contains("-dev", dllNotFound.Message, StringComparison.Ordinal);
    }

    private static void AssertRoundTrips()
    {
        using var binding = new LibDeflateBinding();

        var source = "the quick brown fox jumps over the lazy dog"u8;
        var packed = new byte[binding.MaxPackSize(source.Length)];
        var packedLength = binding.Pack(packed, source);
        Assert.True(packedLength > 0);

        var unpacked = new byte[source.Length];
        var result = binding.Unpack(unpacked, packed.AsSpan(0, packedLength), out var unpackedLength);

        Assert.Equal(LibDeflateResult.Success, result);
        Assert.Equal(source.Length, unpackedLength);
        Assert.True(source.SequenceEqual(unpacked));
    }

    private static T? Unwrap<T>(Exception? exception) where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
