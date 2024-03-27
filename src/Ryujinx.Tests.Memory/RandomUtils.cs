using System;
using System.Collections.Generic;

namespace Ryujinx.Tests
{
    public static class RandomUtils
    {
        public static bool NextBool(this Random random)
        {
            return random.Next(2) == 1;
        }

        public static ushort NextUShort(this Random random)
        {
            return (ushort)random.Next(ushort.MaxValue);
        }

        public static uint NextUInt(this Random random)
        {
            return (uint)random.NextInt64(uint.MaxValue);
        }

        public static uint NextUInt(this Random random, uint to)
        {
            return (uint)random.NextInt64(to);
        }

        public static uint NextUInt(this Random random, uint from, uint to)
        {
            return (uint)random.NextInt64(from, to);
        }

        public static ulong NextULong(this Random random)
        {
            byte[] buffer = new byte[8];

            random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer);
        }

        public static ulong NextULong(this Random random, ulong from, ulong to)
        {
            byte[] buffer = new byte[8];

            random.NextBytes(buffer);
            // NOTE: The result won't be perfectly random, but it should be random enough for tests
            return BitConverter.ToUInt64(buffer) % (to + 1 - from) + from;
        }

        public static byte NextByte(this Random random)
        {
            return (byte)random.Next(byte.MinValue, byte.MaxValue);
        }

        public static byte NextByte(this Random random, byte to)
        {
            return (byte)random.Next(byte.MinValue, to);
        }

        public static byte NextByte(this Random random, byte from, byte to)
        {
            return (byte)random.Next(from, to);
        }

        public static IEnumerable<uint> NextUIntEnumerable(this Random random, int count)
        {
            List<uint> list = new();

            for (int i = 0; i < count; i++)
            {
                list.Add(random.NextUInt());
            }

            return list.AsReadOnly();
        }
    }
}
