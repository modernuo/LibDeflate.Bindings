using System.IO.Compression;
using System.Tests;

namespace LibDeflate.Tests;

public class LibDeflateTests
{
    private static readonly LibDeflateBinding _libDeflate = new();

    [Fact]
    public void TestPackAndUnpack()
    {
        var layout =
            "{ page 0 }{ resizepic 0 0 5054 530 437 }{ gumppictiled 10 10 510 22 2624 }{ gumppictiled 10 292 150 45 2624 }{ gumppictiled 165 292 355 45 2624 }{ gumppictiled 10 342 510 85 2624 }{ gumppictiled 10 37 200 250 2624 }{ gumppictiled 215 37 305 250 2624 }{ checkertrans 10 10 510 417 }{ xmfhtmlgumpcolor 10 12 510 20 1044002 0 0 32767 }{ xmfhtmlgumpcolor 10 37 200 22 1044010 0 0 32767 }{ xmfhtmlgumpcolor 215 37 305 22 1044011 0 0 32767 }{ xmfhtmlgumpcolor 10 302 150 25 1044012 0 0 32767 }{ button 15 402 4017 4019 1 0 0 }{ xmfhtmlgumpcolor 50 405 150 18 1011441 0 0 32767 }{ button 270 402 4005 4007 1 0 21 }{ xmfhtmlgumpcolor 305 405 150 18 1044013 0 0 32767 }{ button 270 362 4005 4007 1 0 49 }{ xmfhtmlgumpcolor 305 365 150 18 1044017 0 0 32767 }{ button 15 342 4005 4007 1 0 14 }{ xmfhtmlgumpcolor 50 345 150 18 1044259 0 0 32767 }{ button 270 342 4005 4007 1 0 42 }{ xmfhtmlgumpcolor 305 345 150 18 1044260 0 0 32767 }{ button 270 382 4005 4007 1 0 63 }{ xmfhtmlgumpcolor 305 385 150 18 1061001 0 0 32767 }{ button 15 362 4005 4007 1 0 7 }{ xmfhtmltok 50 365 250 18 0 0 32767 1044022 @0@ }{ button 15 382 4005 4007 1 0 56 }{ xmfhtmltok 50 385 250 18 0 0 32767 1060875 @0@ }{ button 15 60 4005 4007 1 0 28 }{ xmfhtmlgumpcolor 50 63 150 18 1044014 0 0 32767 }{ button 15 80 4005 4007 1 0 1 }{ xmfhtmlgumpcolor 50 83 150 18 1011076 0 0 32767 }{ button 15 100 4005 4007 1 0 8 }{ xmfhtmlgumpcolor 50 103 150 18 1011077 0 0 32767 }{ button 15 120 4005 4007 1 0 15 }{ xmfhtmlgumpcolor 50 123 150 18 1011078 0 0 32767 }{ button 15 140 4005 4007 1 0 22 }{ xmfhtmlgumpcolor 50 143 150 18 1011079 0 0 32767 }{ button 15 160 4005 4007 1 0 29 }{ xmfhtmlgumpcolor 50 163 150 18 1011080 0 0 32767 }{ button 15 180 4005 4007 1 0 36 }{ xmfhtmlgumpcolor 50 183 150 18 1011081 0 0 32767 }{ button 15 200 4005 4007 1 0 43 }{ xmfhtmlgumpcolor 50 203 150 18 1011082 0 0 32767 }{ button 15 220 4005 4007 1 0 50 }{ xmfhtmlgumpcolor 50 223 150 18 1011083 0 0 32767 }{ button 15 240 4005 4007 1 0 57 }{ xmfhtmlgumpcolor 50 243 150 18 1011084 0 0 32767 }{ button 15 260 4005 4007 1 0 64 }{ xmfhtmlgumpcolor 50 263 150 18 1053114 0 0 32767 }{ page 1 }{ button 220 60 4005 4007 1 0 2 }{ xmfhtmlgumpcolor 255 63 220 18 1023913 0 0 32767 }{ button 480 60 4011 4012 1 0 3 }{ button 220 80 4005 4007 1 0 9 }{ xmfhtmlgumpcolor 255 83 220 18 1023911 0 0 32767 }{ button 480 80 4011 4012 1 0 10 }{ button 220 100 4005 4007 1 0 16 }{ xmfhtmlgumpcolor 255 103 220 18 1023915 0 0 32767 }{ button 480 100 4011 4012 1 0 17 }{ button 220 120 4005 4007 1 0 23 }{ xmfhtmlgumpcolor 255 123 220 18 1023909 0 0 32767 }{ button 480 120 4011 4012 1 0 24 }{ button 220 140 4005 4007 1 0 30 }{ xmfhtmlgumpcolor 255 143 220 18 1025115 0 0 32767 }{ button 480 140 4011 4012 1 0 31 }{ button 220 160 4005 4007 1 0 37 }{ xmfhtmlgumpcolor 255 163 220 18 1025187 0 0 32767 }{ button 480 160 4011 4012 1 0 38 }{ button 220 180 4005 4007 1 0 44 }{ xmfhtmlgumpcolor 255 183 220 18 1025040 0 0 32767 }{ button 480 180 4011 4012 1 0 45 }"u8;

        var maxPackSize = _libDeflate.MaxPackSize(layout.Length);
        Span<byte> compressed = stackalloc byte[maxPackSize];
        var compressedLength = _libDeflate.Pack(compressed, layout);
        Assert.True(compressedLength > 0);

        Span<byte> decompressed = stackalloc byte[layout.Length];
        var result = _libDeflate.Unpack(decompressed, compressed[..compressedLength], out var uncompressedLength);

        // Success unpack
        Assert.Equal(LibDeflateResult.Success, result);

        // Correct unpacked length
        Assert.Equal(layout.Length, uncompressedLength);

        // Correct data
        AssertThat.Equal(layout, decompressed[..uncompressedLength]);
    }
}
