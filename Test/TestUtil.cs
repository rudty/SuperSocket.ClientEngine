namespace Test;
using System;

internal static class TestUtil
{
    /// <summary>
    /// Get Random byte[]
    /// </summary>
    /// <param name="size">random size</param>
    public static byte[] MakeRandomByteArrayPacket(int size = 10)
    {
        var b = new byte[size];
        Random.Shared.NextBytes(b);

        return b;
    }

    public static byte[] ByteArrayMultiply(ArraySegment<byte> b, int repeatCount)
    {
        var multiplyByteArray = new byte[b.Count * repeatCount];
        for (var r = 0; r < repeatCount; ++r)
        {
            Buffer.BlockCopy(b.Array!, b.Offset, multiplyByteArray, r * b.Count, b.Count);
        }

        return multiplyByteArray;
    }
}
