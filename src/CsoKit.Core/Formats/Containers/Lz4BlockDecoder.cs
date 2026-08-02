namespace CsoKit.Core.Formats.Containers;

internal static class Lz4BlockDecoder
{
    public static bool TryDecode(
        ReadOnlySpan<byte> input,
        Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;
        int inputOffset = 0;

        try
        {
            while (inputOffset < input.Length && bytesWritten < output.Length)
            {
                byte token = input[inputOffset++];
                int literalLength = token >> 4;

                if (!TryReadExtendedLength(input, ref inputOffset, ref literalLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                if (literalLength > input.Length - inputOffset ||
                    literalLength > output.Length - bytesWritten)
                {
                    bytesWritten = 0;
                    return false;
                }

                input.Slice(inputOffset, literalLength).CopyTo(output.Slice(bytesWritten, literalLength));
                inputOffset += literalLength;
                bytesWritten += literalLength;

                if (inputOffset == input.Length || bytesWritten == output.Length)
                {
                    bool success = bytesWritten == output.Length && HasOnlyZeroPadding(input[inputOffset..]);

                    if (!success)
                    {
                        bytesWritten = 0;
                    }

                    return success;
                }

                if (input.Length - inputOffset < 2)
                {
                    bytesWritten = 0;
                    return false;
                }

                int matchOffset = input[inputOffset] | (input[inputOffset + 1] << 8);
                inputOffset += 2;

                if (matchOffset == 0 || matchOffset > bytesWritten)
                {
                    bytesWritten = 0;
                    return false;
                }

                int matchLength = token & 0x0F;

                if (!TryReadExtendedLength(input, ref inputOffset, ref matchLength) ||
                    matchLength > int.MaxValue - 4)
                {
                    bytesWritten = 0;
                    return false;
                }

                matchLength += 4;

                if (matchLength > output.Length - bytesWritten)
                {
                    bytesWritten = 0;
                    return false;
                }

                int copyFrom = bytesWritten - matchOffset;

                for (int index = 0; index < matchLength; index++)
                {
                    output[bytesWritten++] = output[copyFrom + index];
                }
            }

            bool decoded = bytesWritten == output.Length && HasOnlyZeroPadding(input[inputOffset..]);

            if (!decoded)
            {
                bytesWritten = 0;
            }

            return decoded;
        }
        catch (IndexOutOfRangeException)
        {
            bytesWritten = 0;
            return false;
        }
    }

    private static bool TryReadExtendedLength(
        ReadOnlySpan<byte> input,
        ref int inputOffset,
        ref int length)
    {
        if (length != 15)
        {
            return true;
        }

        while (inputOffset < input.Length)
        {
            byte value = input[inputOffset++];

            if (length > int.MaxValue - value)
            {
                return false;
            }

            length += value;

            if (value != 255)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOnlyZeroPadding(ReadOnlySpan<byte> input)
    {
        foreach (byte value in input)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }
}