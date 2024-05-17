namespace System.Tests;

public static class AssertThat
{
    public static void Equal(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual) =>
        Assert.True(
            expected.SequenceEqual(actual),
            $"Expected does not match actual.\nExpected:\t{expected.ToDelimitedHexString()}\nActual:\t\t{actual.ToDelimitedHexString()}"
        );

    public static readonly uint[] m_Lookup32Chars = CreateLookup32Chars();

    private static uint[] CreateLookup32Chars()
    {
        var result = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var s = i.ToString("X2");
            if (BitConverter.IsLittleEndian)
            {
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            else
            {
                result[i] = s[1] + ((uint)s[0] << 16);
            }
        }

        return result;
    }

    public static unsafe string ToDelimitedHexString(this ReadOnlySpan<byte> bytes)
    {
        const uint delimiter = 0x20002C; // ", "
        const char openBracket = '[';
        const char closeBracket = ']';
        var length = Math.Max(2, bytes.Length * 4); // len * 2 + (len - 1) * 2 + 2

        var result = new string((char)0, length);
        fixed (char* resultP = result)
        {
            resultP[0] = openBracket;
            resultP[length - 1] = closeBracket;

            var resultP2 = (uint*)(resultP + 1);
            for (int a = 0, i = 0; a < bytes.Length; a++, i++)
            {
                if (a > 0)
                {
                    resultP2[i++] = delimiter;
                }

                resultP2[i] = m_Lookup32Chars[bytes[a]];
            }
        }

        return result;
    }
}
