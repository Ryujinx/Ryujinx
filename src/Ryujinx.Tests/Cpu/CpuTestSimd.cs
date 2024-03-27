#define Simd

using ARMeilleure.State;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("Simd")]
    public sealed class CpuTestSimd : CpuTest
    {
        public CpuTestSimd(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if Simd

        #region "Helper methods"
        private static byte GenLeadingSignsMinus8(int cnt) // 0 <= cnt <= 7
        {
            return (byte)(~(uint)GenLeadingZeros8(cnt + 1));
        }

        private static ushort GenLeadingSignsMinus16(int cnt) // 0 <= cnt <= 15
        {
            return (ushort)(~(uint)GenLeadingZeros16(cnt + 1));
        }

        private static uint GenLeadingSignsMinus32(int cnt) // 0 <= cnt <= 31
        {
            return ~GenLeadingZeros32(cnt + 1);
        }

        private static byte GenLeadingSignsPlus8(int cnt) // 0 <= cnt <= 7
        {
            return GenLeadingZeros8(cnt + 1);
        }

        private static ushort GenLeadingSignsPlus16(int cnt) // 0 <= cnt <= 15
        {
            return GenLeadingZeros16(cnt + 1);
        }

        private static uint GenLeadingSignsPlus32(int cnt) // 0 <= cnt <= 31
        {
            return GenLeadingZeros32(cnt + 1);
        }

        private static byte GenLeadingZeros8(int cnt) // 0 <= cnt <= 8
        {
            if (cnt == 8)
            {
                return 0;
            }

            if (cnt == 7)
            {
                return 1;
            }

            byte rnd = Random.Shared.NextByte();
            sbyte mask = sbyte.MinValue;

            return (byte)(((uint)rnd >> (cnt + 1)) | ((uint)((byte)mask) >> cnt));
        }

        private static ushort GenLeadingZeros16(int cnt) // 0 <= cnt <= 16
        {
            if (cnt == 16)
            {
                return 0;
            }

            if (cnt == 15)
            {
                return 1;
            }

            ushort rnd = Random.Shared.NextUShort();
            short mask = short.MinValue;

            return (ushort)(((uint)rnd >> (cnt + 1)) | ((uint)((ushort)mask) >> cnt));
        }

        private static uint GenLeadingZeros32(int cnt) // 0 <= cnt <= 32
        {
            if (cnt == 32)
            {
                return 0u;
            }

            if (cnt == 31)
            {
                return 1u;
            }

            uint rnd = Random.Shared.NextUInt();
            int mask = int.MinValue;

            return (rnd >> (cnt + 1)) | ((uint)mask >> cnt);
        }
        #endregion

        #region "ValueSource (Types)"
        private static ulong[] _1B1H1S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x000000000000007Ful,
                0x0000000000000080ul, 0x00000000000000FFul,
                0x0000000000007FFFul, 0x0000000000008000ul,
                0x000000000000FFFFul, 0x000000007FFFFFFFul,
                0x0000000080000000ul, 0x00000000FFFFFFFFul,
                0x7FFFFFFFFFFFFFFFul, 0x8000000000000000ul,
                0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1H1S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x0000000000007FFFul,
                0x0000000000008000ul, 0x000000000000FFFFul,
                0x000000007FFFFFFFul, 0x0000000080000000ul,
                0x00000000FFFFFFFFul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _1S_()
        {
            return new[] {
                0x0000000000000000ul, 0x000000007FFFFFFFul,
                0x0000000080000000ul, 0x00000000FFFFFFFFul,
            };
        }

        private static ulong[] _2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _4H_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _4H2S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B4H_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B4H2S_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static ulong[] _8B4H2S1D_()
        {
            return new[] {
                0x0000000000000000ul, 0x7F7F7F7F7F7F7F7Ful,
                0x8080808080808080ul, 0x7FFF7FFF7FFF7FFFul,
                0x8000800080008000ul, 0x7FFFFFFF7FFFFFFFul,
                0x8000000080000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static uint[] _W_()
        {
            return new[] {
                0x00000000u, 0x7FFFFFFFu,
                0x80000000u, 0xFFFFFFFFu,
            };
        }

        private static ulong[] _X_()
        {
            return new[] {
                0x0000000000000000ul, 0x7FFFFFFFFFFFFFFFul,
                0x8000000000000000ul, 0xFFFFFFFFFFFFFFFFul,
            };
        }

        private static IEnumerable<ulong> _1H_F_()
        {
            yield return 0x000000000000FBFFul; // -Max Normal
            yield return 0x0000000000008400ul; // -Min Normal
            yield return 0x00000000000083FFul; // -Max Subnormal
            yield return 0x0000000000008001ul; // -Min Subnormal
            yield return 0x0000000000007BFFul; // +Max Normal
            yield return 0x0000000000000400ul; // +Min Normal
            yield return 0x00000000000003FFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal

            if (!_noZeros)
            {
                yield return 0x0000000000008000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0x000000000000FC00ul; // -Infinity
                yield return 0x0000000000007C00ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0x000000000000FE00ul; // -QNaN (all zeros payload)
                yield return 0x000000000000FDFFul; // -SNaN (all ones  payload)
                yield return 0x0000000000007E00ul; // +QNaN (all zeros payload) (DefaultNaN)
                yield return 0x0000000000007DFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong grbg = Random.Shared.NextUShort();
                ulong rnd1 = GenNormalH();
                ulong rnd2 = GenSubnormalH();

                yield return (grbg << 48) | (grbg << 32) | (grbg << 16) | rnd1;
                yield return (grbg << 48) | (grbg << 32) | (grbg << 16) | rnd2;
            }
        }

        private static IEnumerable<ulong> _4H_F_()
        {
            yield return 0xFBFFFBFFFBFFFBFFul; // -Max Normal
            yield return 0x8400840084008400ul; // -Min Normal
            yield return 0x83FF83FF83FF83FFul; // -Max Subnormal
            yield return 0x8001800180018001ul; // -Min Subnormal
            yield return 0x7BFF7BFF7BFF7BFFul; // +Max Normal
            yield return 0x0400040004000400ul; // +Min Normal
            yield return 0x03FF03FF03FF03FFul; // +Max Subnormal
            yield return 0x0001000100010001ul; // +Min Subnormal

            if (!_noZeros)
            {
                yield return 0x8000800080008000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFC00FC00FC00FC00ul; // -Infinity
                yield return 0x7C007C007C007C00ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFE00FE00FE00FE00ul; // -QNaN (all zeros payload)
                yield return 0xFDFFFDFFFDFFFDFFul; // -SNaN (all ones  payload)
                yield return 0x7E007E007E007E00ul; // +QNaN (all zeros payload) (DefaultNaN)
                yield return 0x7DFF7DFF7DFF7DFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = GenNormalH();
                ulong rnd2 = GenSubnormalH();

                yield return (rnd1 << 48) | (rnd1 << 32) | (rnd1 << 16) | rnd1;
                yield return (rnd2 << 48) | (rnd2 << 32) | (rnd2 << 16) | rnd2;
            }
        }

        private static IEnumerable<ulong> _1S_F_()
        {
            yield return 0x00000000FF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x0000000080800000ul; // -Min Normal
            yield return 0x00000000807FFFFFul; // -Max Subnormal
            yield return 0x0000000080000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x000000007F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0000000000800000ul; // +Min Normal
            yield return 0x00000000007FFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x0000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0x00000000FF800000ul; // -Infinity
                yield return 0x000000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0x00000000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0x00000000FFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x000000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x000000007FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong grbg = Random.Shared.NextUInt();
                ulong rnd1 = GenNormalS();
                ulong rnd2 = GenSubnormalS();

                yield return (grbg << 32) | rnd1;
                yield return (grbg << 32) | rnd2;
            }
        }

        private static IEnumerable<ulong> _1S_F_W_()
        {
            // int
            yield return 0x00000000CF000001ul; // -2.1474839E9f  (-2147483904)
            yield return 0x00000000CF000000ul; // -2.14748365E9f (-2147483648)
            yield return 0x00000000CEFFFFFFul; // -2.14748352E9f (-2147483520)
            yield return 0x000000004F000001ul; //  2.1474839E9f  (2147483904)
            yield return 0x000000004F000000ul; //  2.14748365E9f (2147483648)
            yield return 0x000000004EFFFFFFul; //  2.14748352E9f (2147483520)

            // uint
            yield return 0x000000004F800001ul; // 4.2949678E9f  (4294967808)
            yield return 0x000000004F800000ul; // 4.2949673E9f  (4294967296)
            yield return 0x000000004F7FFFFFul; // 4.29496704E9f (4294967040)

            yield return 0x00000000FF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x0000000080800000ul; // -Min Normal
            yield return 0x00000000807FFFFFul; // -Max Subnormal
            yield return 0x0000000080000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x000000007F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0000000000800000ul; // +Min Normal
            yield return 0x00000000007FFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x0000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0x00000000FF800000ul; // -Infinity
                yield return 0x000000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0x00000000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0x00000000FFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x000000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x000000007FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong grbg = Random.Shared.NextUInt();

                ulong rnd1 = (uint)BitConverter.SingleToInt32Bits((int)Random.Shared.NextUInt());
                ulong rnd2 = (uint)BitConverter.SingleToInt32Bits(Random.Shared.NextUInt());

                ulong rnd3 = GenNormalS();
                ulong rnd4 = GenSubnormalS();

                yield return (grbg << 32) | rnd1;
                yield return (grbg << 32) | rnd2;

                yield return (grbg << 32) | rnd3;
                yield return (grbg << 32) | rnd4;
            }
        }

        private static IEnumerable<ulong> _2S_F_()
        {
            yield return 0xFF7FFFFFFF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x8080000080800000ul; // -Min Normal
            yield return 0x807FFFFF807FFFFFul; // -Max Subnormal
            yield return 0x8000000180000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x7F7FFFFF7F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0080000000800000ul; // +Min Normal
            yield return 0x007FFFFF007FFFFFul; // +Max Subnormal
            yield return 0x0000000100000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFF800000FF800000ul; // -Infinity
                yield return 0x7F8000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFC00000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0xFFBFFFFFFFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FC000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x7FBFFFFF7FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = GenNormalS();
                ulong rnd2 = GenSubnormalS();

                yield return (rnd1 << 32) | rnd1;
                yield return (rnd2 << 32) | rnd2;
            }
        }

        private static IEnumerable<ulong> _2S_F_W_()
        {
            // int
            yield return 0xCF000001CF000001ul; // -2.1474839E9f  (-2147483904)
            yield return 0xCF000000CF000000ul; // -2.14748365E9f (-2147483648)
            yield return 0xCEFFFFFFCEFFFFFFul; // -2.14748352E9f (-2147483520)
            yield return 0x4F0000014F000001ul; //  2.1474839E9f  (2147483904)
            yield return 0x4F0000004F000000ul; //  2.14748365E9f (2147483648)
            yield return 0x4EFFFFFF4EFFFFFFul; //  2.14748352E9f (2147483520)

            // uint
            yield return 0x4F8000014F800001ul; // 4.2949678E9f  (4294967808)
            yield return 0x4F8000004F800000ul; // 4.2949673E9f  (4294967296)
            yield return 0x4F7FFFFF4F7FFFFFul; // 4.29496704E9f (4294967040)

            yield return 0xFF7FFFFFFF7FFFFFul; // -Max Normal    (float.MinValue)
            yield return 0x8080000080800000ul; // -Min Normal
            yield return 0x807FFFFF807FFFFFul; // -Max Subnormal
            yield return 0x8000000180000001ul; // -Min Subnormal (-float.Epsilon)
            yield return 0x7F7FFFFF7F7FFFFFul; // +Max Normal    (float.MaxValue)
            yield return 0x0080000000800000ul; // +Min Normal
            yield return 0x007FFFFF007FFFFFul; // +Max Subnormal
            yield return 0x0000000100000001ul; // +Min Subnormal (float.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000080000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFF800000FF800000ul; // -Infinity
                yield return 0x7F8000007F800000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFC00000FFC00000ul; // -QNaN (all zeros payload) (float.NaN)
                yield return 0xFFBFFFFFFFBFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FC000007FC00000ul; // +QNaN (all zeros payload) (-float.NaN) (DefaultNaN)
                yield return 0x7FBFFFFF7FBFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = (uint)BitConverter.SingleToInt32Bits(
                    (int)Random.Shared.NextUInt());
                ulong rnd2 = (uint)BitConverter.SingleToInt32Bits(
                    Random.Shared.NextUInt());

                ulong rnd3 = GenNormalS();
                ulong rnd4 = GenSubnormalS();

                yield return (rnd1 << 32) | rnd1;
                yield return (rnd2 << 32) | rnd2;

                yield return (rnd3 << 32) | rnd3;
                yield return (rnd4 << 32) | rnd4;
            }
        }

        private static IEnumerable<ulong> _1D_F_()
        {
            yield return 0xFFEFFFFFFFFFFFFFul; // -Max Normal    (double.MinValue)
            yield return 0x8010000000000000ul; // -Min Normal
            yield return 0x800FFFFFFFFFFFFFul; // -Max Subnormal
            yield return 0x8000000000000001ul; // -Min Subnormal (-double.Epsilon)
            yield return 0x7FEFFFFFFFFFFFFFul; // +Max Normal    (double.MaxValue)
            yield return 0x0010000000000000ul; // +Min Normal
            yield return 0x000FFFFFFFFFFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (double.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000000000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFFF0000000000000ul; // -Infinity
                yield return 0x7FF0000000000000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFF8000000000000ul; // -QNaN (all zeros payload) (double.NaN)
                yield return 0xFFF7FFFFFFFFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FF8000000000000ul; // +QNaN (all zeros payload) (-double.NaN) (DefaultNaN)
                yield return 0x7FF7FFFFFFFFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = GenNormalD();
                ulong rnd2 = GenSubnormalD();

                yield return rnd1;
                yield return rnd2;
            }
        }

        private static IEnumerable<ulong> _1D_F_X_()
        {
            // long
            yield return 0xC3E0000000000001ul; // -9.2233720368547780E18d (-9223372036854778000)
            yield return 0xC3E0000000000000ul; // -9.2233720368547760E18d (-9223372036854776000)
            yield return 0xC3DFFFFFFFFFFFFFul; // -9.2233720368547750E18d (-9223372036854775000)
            yield return 0x43E0000000000001ul; //  9.2233720368547780E18d (9223372036854778000)
            yield return 0x43E0000000000000ul; //  9.2233720368547760E18d (9223372036854776000)
            yield return 0x43DFFFFFFFFFFFFFul; //  9.2233720368547750E18d (9223372036854775000)

            // ulong
            yield return 0x43F0000000000001ul; // 1.8446744073709556e19d (18446744073709556000)
            yield return 0x43F0000000000000ul; // 1.8446744073709552E19d (18446744073709552000)
            yield return 0x43EFFFFFFFFFFFFFul; // 1.8446744073709550e19d (18446744073709550000)

            yield return 0xFFEFFFFFFFFFFFFFul; // -Max Normal    (double.MinValue)
            yield return 0x8010000000000000ul; // -Min Normal
            yield return 0x800FFFFFFFFFFFFFul; // -Max Subnormal
            yield return 0x8000000000000001ul; // -Min Subnormal (-double.Epsilon)
            yield return 0x7FEFFFFFFFFFFFFFul; // +Max Normal    (double.MaxValue)
            yield return 0x0010000000000000ul; // +Min Normal
            yield return 0x000FFFFFFFFFFFFFul; // +Max Subnormal
            yield return 0x0000000000000001ul; // +Min Subnormal (double.Epsilon)

            if (!_noZeros)
            {
                yield return 0x8000000000000000ul; // -Zero
                yield return 0x0000000000000000ul; // +Zero
            }

            if (!_noInfs)
            {
                yield return 0xFFF0000000000000ul; // -Infinity
                yield return 0x7FF0000000000000ul; // +Infinity
            }

            if (!_noNaNs)
            {
                yield return 0xFFF8000000000000ul; // -QNaN (all zeros payload) (double.NaN)
                yield return 0xFFF7FFFFFFFFFFFFul; // -SNaN (all ones  payload)
                yield return 0x7FF8000000000000ul; // +QNaN (all zeros payload) (-double.NaN) (DefaultNaN)
                yield return 0x7FF7FFFFFFFFFFFFul; // +SNaN (all ones  payload)
            }

            for (int cnt = 1; cnt <= RndCnt; cnt++)
            {
                ulong rnd1 = (ulong)BitConverter.DoubleToInt64Bits(
                    (long)Random.Shared.NextULong());
                ulong rnd2 = (ulong)BitConverter.DoubleToInt64Bits(
                    Random.Shared.NextULong());

                ulong rnd3 = GenNormalD();
                ulong rnd4 = GenSubnormalD();

                yield return rnd1;
                yield return rnd2;

                yield return rnd3;
                yield return rnd4;
            }
        }

        private static IEnumerable<ulong> _GenLeadingSigns8B_()
        {
            for (int cnt = 0; cnt <= 7; cnt++)
            {
                ulong rnd1 = GenLeadingSignsMinus8(cnt);
                ulong rnd2 = GenLeadingSignsPlus8(cnt);

                yield return (rnd1 << 56) | (rnd1 << 48) | (rnd1 << 40) | (rnd1 << 32) |
                             (rnd1 << 24) | (rnd1 << 16) | (rnd1 << 08) | rnd1;
                yield return (rnd2 << 56) | (rnd2 << 48) | (rnd2 << 40) | (rnd2 << 32) |
                             (rnd2 << 24) | (rnd2 << 16) | (rnd2 << 08) | rnd2;
            }
        }

        private static IEnumerable<ulong> _GenLeadingSigns4H_()
        {
            for (int cnt = 0; cnt <= 15; cnt++)
            {
                ulong rnd1 = GenLeadingSignsMinus16(cnt);
                ulong rnd2 = GenLeadingSignsPlus16(cnt);

                yield return (rnd1 << 48) | (rnd1 << 32) | (rnd1 << 16) | rnd1;
                yield return (rnd2 << 48) | (rnd2 << 32) | (rnd2 << 16) | rnd2;
            }
        }

        private static IEnumerable<ulong> _GenLeadingSigns2S_()
        {
            for (int cnt = 0; cnt <= 31; cnt++)
            {
                ulong rnd1 = GenLeadingSignsMinus32(cnt);
                ulong rnd2 = GenLeadingSignsPlus32(cnt);

                yield return (rnd1 << 32) | rnd1;
                yield return (rnd2 << 32) | rnd2;
            }
        }

        private static IEnumerable<ulong> _GenLeadingZeros8B_()
        {
            for (int cnt = 0; cnt <= 8; cnt++)
            {
                ulong rnd = GenLeadingZeros8(cnt);

                yield return (rnd << 56) | (rnd << 48) | (rnd << 40) | (rnd << 32) |
                             (rnd << 24) | (rnd << 16) | (rnd << 08) | rnd;
            }
        }

        private static IEnumerable<ulong> _GenLeadingZeros4H_()
        {
            for (int cnt = 0; cnt <= 16; cnt++)
            {
                ulong rnd = GenLeadingZeros16(cnt);

                yield return (rnd << 48) | (rnd << 32) | (rnd << 16) | rnd;
            }
        }

        private static IEnumerable<ulong> _GenLeadingZeros2S_()
        {
            for (int cnt = 0; cnt <= 32; cnt++)
            {
                ulong rnd = GenLeadingZeros32(cnt);

                yield return (rnd << 32) | rnd;
            }
        }

        private static IEnumerable<ulong> _GenPopCnt8B_()
        {
            for (ulong cnt = 0ul; cnt <= 255ul; cnt++)
            {
                yield return (cnt << 56) | (cnt << 48) | (cnt << 40) | (cnt << 32) |
                             (cnt << 24) | (cnt << 16) | (cnt << 08) | cnt;
            }
        }
        #endregion

        #region "ValueSource (Opcodes)"
        private static uint[] _SU_Add_Max_Min_V_V_8BB_4HH_()
        {
            return new[]
            {
                0x0E31B800u, // ADDV  B0, V0.8B
                0x0E30A800u, // SMAXV B0, V0.8B
                0x0E31A800u, // SMINV B0, V0.8B
                0x2E30A800u, // UMAXV B0, V0.8B
                0x2E31A800u, // UMINV B0, V0.8B
            };
        }

        private static uint[] _SU_Add_Max_Min_V_V_16BB_8HH_4SS_()
        {
            return new[]
            {
                0x4E31B800u, // ADDV  B0, V0.16B
                0x4E30A800u, // SMAXV B0, V0.16B
                0x4E31A800u, // SMINV B0, V0.16B
                0x6E30A800u, // UMAXV B0, V0.16B
                0x6E31A800u, // UMINV B0, V0.16B
            };
        }

        private static uint[] _F_Abs_Neg_Recpx_Sqrt_S_S_()
        {
            return new[]
            {
                0x1E20C020u, // FABS   S0, S1
                0x1E214020u, // FNEG   S0, S1
                0x5EA1F820u, // FRECPX S0, S1
                0x1E21C020u, // FSQRT  S0, S1
            };
        }

        private static uint[] _F_Abs_Neg_Recpx_Sqrt_S_D_()
        {
            return new[]
            {
                0x1E60C020u, // FABS   D0, D1
                0x1E614020u, // FNEG   D0, D1
                0x5EE1F820u, // FRECPX D0, D1
                0x1E61C020u, // FSQRT  D0, D1
            };
        }

        private static uint[] _F_Abs_Neg_Sqrt_V_2S_4S_()
        {
            return new[]
            {
                0x0EA0F800u, // FABS  V0.2S, V0.2S
                0x2EA0F800u, // FNEG  V0.2S, V0.2S
                0x2EA1F800u, // FSQRT V0.2S, V0.2S
            };
        }

        private static uint[] _F_Abs_Neg_Sqrt_V_2D_()
        {
            return new[]
            {
                0x4EE0F800u, // FABS  V0.2D, V0.2D
                0x6EE0F800u, // FNEG  V0.2D, V0.2D
                0x6EE1F800u, // FSQRT V0.2D, V0.2D
            };
        }

        private static uint[] _F_Add_Max_Min_Nm_P_S_2SS_()
        {
            return new[]
            {
                0x7E30D820u, // FADDP   S0, V1.2S
                0x7E30C820u, // FMAXNMP S0, V1.2S
                0x7E30F820u, // FMAXP   S0, V1.2S
                0x7EB0C820u, // FMINNMP S0, V1.2S
                0x7EB0F820u, // FMINP   S0, V1.2S
            };
        }

        private static uint[] _F_Add_Max_Min_Nm_P_S_2DD_()
        {
            return new[]
            {
                0x7E70D820u, // FADDP   D0, V1.2D
                0x7E70C820u, // FMAXNMP D0, V1.2D
                0x7E70F820u, // FMAXP   D0, V1.2D
                0x7EF0C820u, // FMINNMP D0, V1.2D
                0x7EF0F820u, // FMINP   D0, V1.2D
            };
        }

        private static uint[] _F_Cm_EqGeGtLeLt_S_S_()
        {
            return new[]
            {
                0x5EA0D820u, // FCMEQ S0, S1, #0.0
                0x7EA0C820u, // FCMGE S0, S1, #0.0
                0x5EA0C820u, // FCMGT S0, S1, #0.0
                0x7EA0D820u, // FCMLE S0, S1, #0.0
                0x5EA0E820u, // FCMLT S0, S1, #0.0
            };
        }

        private static uint[] _F_Cm_EqGeGtLeLt_S_D_()
        {
            return new[]
            {
                0x5EE0D820u, // FCMEQ D0, D1, #0.0
                0x7EE0C820u, // FCMGE D0, D1, #0.0
                0x5EE0C820u, // FCMGT D0, D1, #0.0
                0x7EE0D820u, // FCMLE D0, D1, #0.0
                0x5EE0E820u, // FCMLT D0, D1, #0.0
            };
        }

        private static uint[] _F_Cm_EqGeGtLeLt_V_2S_4S_()
        {
            return new[]
            {
                0x0EA0D800u, // FCMEQ V0.2S, V0.2S, #0.0
                0x2EA0C800u, // FCMGE V0.2S, V0.2S, #0.0
                0x0EA0C800u, // FCMGT V0.2S, V0.2S, #0.0
                0x2EA0D800u, // FCMLE V0.2S, V0.2S, #0.0
                0x0EA0E800u, // FCMLT V0.2S, V0.2S, #0.0
            };
        }

        private static uint[] _F_Cm_EqGeGtLeLt_V_2D_()
        {
            return new[]
            {
                0x4EE0D800u, // FCMEQ V0.2D, V0.2D, #0.0
                0x6EE0C800u, // FCMGE V0.2D, V0.2D, #0.0
                0x4EE0C800u, // FCMGT V0.2D, V0.2D, #0.0
                0x6EE0D800u, // FCMLE V0.2D, V0.2D, #0.0
                0x4EE0E800u, // FCMLT V0.2D, V0.2D, #0.0
            };
        }

        private static uint[] _F_Cmp_Cmpe_S_S_()
        {
            return new[]
            {
                0x1E202028u, // FCMP  S1, #0.0
                0x1E202038u, // FCMPE S1, #0.0
            };
        }

        private static uint[] _F_Cmp_Cmpe_S_D_()
        {
            return new[]
            {
                0x1E602028u, // FCMP  D1, #0.0
                0x1E602038u, // FCMPE D1, #0.0
            };
        }

        private static uint[] _F_Cvt_S_SD_()
        {
            return new[]
            {
                0x1E22C020u, // FCVT D0, S1
            };
        }

        private static uint[] _F_Cvt_S_DS_()
        {
            return new[]
            {
                0x1E624020u, // FCVT S0, D1
            };
        }

        private static uint[] _F_Cvt_S_SH_()
        {
            return new[]
            {
                0x1E23C020u, // FCVT H0, S1
            };
        }

        private static uint[] _F_Cvt_S_DH_()
        {
            return new[]
            {
                0x1E63C020u, // FCVT H0, D1
            };
        }

        private static uint[] _F_Cvt_S_HS_()
        {
            return new[]
            {
                0x1EE24020u, // FCVT S0, H1
            };
        }

        private static uint[] _F_Cvt_S_HD_()
        {
            return new[]
            {
                0x1EE2C020u, // FCVT D0, H1
            };
        }

        private static uint[] _F_Cvt_ANZ_SU_S_S_()
        {
            return new[]
            {
                0x5E21C820u, // FCVTAS S0, S1
                0x7E21C820u, // FCVTAU S0, S1
                0x5E21A820u, // FCVTNS S0, S1
                0x7E21A820u, // FCVTNU S0, S1
                0x5EA1B820u, // FCVTZS S0, S1
                0x7EA1B820u, // FCVTZU S0, S1
            };
        }

        private static uint[] _F_Cvt_ANZ_SU_S_D_()
        {
            return new[]
            {
                0x5E61C820u, // FCVTAS D0, D1
                0x7E61C820u, // FCVTAU D0, D1
                0x5E61A820u, // FCVTNS D0, D1
                0x7E61A820u, // FCVTNU D0, D1
                0x5EE1B820u, // FCVTZS D0, D1
                0x7EE1B820u, // FCVTZU D0, D1
            };
        }

        private static uint[] _F_Cvt_ANZ_SU_V_2S_4S_()
        {
            return new[]
            {
                0x0E21C800u, // FCVTAS V0.2S, V0.2S
                0x2E21C800u, // FCVTAU V0.2S, V0.2S
                0x0E21B800u, // FCVTMS V0.2S, V0.2S
                0x0E21A800u, // FCVTNS V0.2S, V0.2S
                0x2E21A800u, // FCVTNU V0.2S, V0.2S
                0x0EA1B800u, // FCVTZS V0.2S, V0.2S
                0x2EA1B800u, // FCVTZU V0.2S, V0.2S
            };
        }

        private static uint[] _F_Cvt_ANZ_SU_V_2D_()
        {
            return new[]
            {
                0x4E61C800u, // FCVTAS V0.2D, V0.2D
                0x6E61C800u, // FCVTAU V0.2D, V0.2D
                0x4E61B800u, // FCVTMS V0.2D, V0.2D
                0x4E61A800u, // FCVTNS V0.2D, V0.2D
                0x6E61A800u, // FCVTNU V0.2D, V0.2D
                0x4EE1B800u, // FCVTZS V0.2D, V0.2D
                0x6EE1B800u, // FCVTZU V0.2D, V0.2D
            };
        }

        private static uint[] _F_Cvtl_V_4H4S_8H4S_()
        {
            return new[]
            {
                0x0E217800u, // FCVTL V0.4S, V0.4H
            };
        }

        private static uint[] _F_Cvtl_V_2S2D_4S2D_()
        {
            return new[]
            {
                0x0E617800u, // FCVTL V0.2D, V0.2S
            };
        }

        private static uint[] _F_Cvtn_V_4S4H_4S8H_()
        {
            return new[]
            {
                0x0E216800u, // FCVTN V0.4H, V0.4S
            };
        }

        private static uint[] _F_Cvtn_V_2D2S_2D4S_()
        {
            return new[]
            {
                0x0E616800u, // FCVTN V0.2S, V0.2D
            };
        }

        private static uint[] _F_Max_Min_Nm_V_V_4SS_()
        {
            return new[]
            {
                0x6E30C800u, // FMAXNMV S0, V0.4S
                0x6E30F800u, // FMAXV   S0, V0.4S
                0x6EB0C800u, // FMINNMV S0, V0.4S
                0x6EB0F800u, // FMINV   S0, V0.4S
            };
        }

        private static uint[] _F_Mov_Ftoi_SW_()
        {
            return new[]
            {
                0x1E260000u, // FMOV W0, S0
            };
        }

        private static uint[] _F_Mov_Ftoi_DX_()
        {
            return new[]
            {
                0x9E660000u, // FMOV X0, D0
            };
        }

        private static uint[] _F_Mov_Ftoi1_DX_()
        {
            return new[]
            {
                0x9EAE0000u, // FMOV X0, V0.D[1]
            };
        }

        private static uint[] _F_Mov_Itof_WS_()
        {
            return new[]
            {
                0x1E270000u, // FMOV S0, W0
            };
        }

        private static uint[] _F_Mov_Itof_XD_()
        {
            return new[]
            {
                0x9E670000u, // FMOV D0, X0
            };
        }

        private static uint[] _F_Mov_Itof1_XD_()
        {
            return new[]
            {
                0x9EAF0000u, // FMOV V0.D[1], X0
            };
        }

        private static uint[] _F_Mov_S_S_()
        {
            return new[]
            {
                0x1E204020u, // FMOV S0, S1
            };
        }

        private static uint[] _F_Mov_S_D_()
        {
            return new[]
            {
                0x1E604020u, // FMOV D0, D1
            };
        }

        private static uint[] _F_Recpe_Rsqrte_S_S_()
        {
            return new[]
            {
                0x5EA1D820u, // FRECPE  S0, S1
                0x7EA1D820u, // FRSQRTE S0, S1
            };
        }

        private static uint[] _F_Recpe_Rsqrte_S_D_()
        {
            return new[]
            {
                0x5EE1D820u, // FRECPE  D0, D1
                0x7EE1D820u, // FRSQRTE D0, D1
            };
        }

        private static uint[] _F_Recpe_Rsqrte_V_2S_4S_()
        {
            return new[]
            {
                0x0EA1D800u, // FRECPE  V0.2S, V0.2S
                0x2EA1D800u, // FRSQRTE V0.2S, V0.2S
            };
        }

        private static uint[] _F_Recpe_Rsqrte_V_2D_()
        {
            return new[]
            {
                0x4EE1D800u, // FRECPE  V0.2D, V0.2D
                0x6EE1D800u, // FRSQRTE V0.2D, V0.2D
            };
        }

        private static uint[] _F_Rint_AMNPZ_S_S_()
        {
            return new[]
            {
                0x1E264020u, // FRINTA S0, S1
                0x1E254020u, // FRINTM S0, S1
                0x1E244020u, // FRINTN S0, S1
                0x1E24C020u, // FRINTP S0, S1
                0x1E25C020u, // FRINTZ S0, S1
            };
        }

        private static uint[] _F_Rint_AMNPZ_S_D_()
        {
            return new[]
            {
                0x1E664020u, // FRINTA D0, D1
                0x1E654020u, // FRINTM D0, D1
                0x1E644020u, // FRINTN D0, D1
                0x1E64C020u, // FRINTP D0, D1
                0x1E65C020u, // FRINTZ D0, D1
            };
        }

        private static uint[] _F_Rint_AMNPZ_V_2S_4S_()
        {
            return new[]
            {
                0x2E218800u, // FRINTA V0.2S, V0.2S
                0x0E219800u, // FRINTM V0.2S, V0.2S
                0x0E218800u, // FRINTN V0.2S, V0.2S
                0x0EA18800u, // FRINTP V0.2S, V0.2S
                0x0EA19800u, // FRINTZ V0.2S, V0.2S
            };
        }

        private static uint[] _F_Rint_AMNPZ_V_2D_()
        {
            return new[]
            {
                0x6E618800u, // FRINTA V0.2D, V0.2D
                0x4E619800u, // FRINTM V0.2D, V0.2D
                0x4E618800u, // FRINTN V0.2D, V0.2D
                0x4EE18800u, // FRINTP V0.2D, V0.2D
                0x4EE19800u, // FRINTZ V0.2D, V0.2D
            };
        }

        private static uint[] _F_Rint_IX_S_S_()
        {
            return new[]
            {
                0x1E27C020u, // FRINTI S0, S1
                0x1E274020u, // FRINTX S0, S1
            };
        }

        private static uint[] _F_Rint_IX_S_D_()
        {
            return new[]
            {
                0x1E67C020u, // FRINTI D0, D1
                0x1E674020u, // FRINTX D0, D1
            };
        }

        private static uint[] _F_Rint_IX_V_2S_4S_()
        {
            return new[]
            {
                0x2EA19800u, // FRINTI V0.2S, V0.2S
                0x2E219800u, // FRINTX V0.2S, V0.2S
            };
        }

        private static uint[] _F_Rint_IX_V_2D_()
        {
            return new[]
            {
                0x6EE19800u, // FRINTI V0.2D, V0.2D
                0x6E619800u, // FRINTX V0.2D, V0.2D
            };
        }

        private static uint[] _SU_Addl_V_V_8BH_4HS_()
        {
            return new[]
            {
                0x0E303800u, // SADDLV H0, V0.8B
                0x2E303800u, // UADDLV H0, V0.8B
            };
        }

        private static uint[] _SU_Addl_V_V_16BH_8HS_4SD_()
        {
            return new[]
            {
                0x4E303800u, // SADDLV H0, V0.16B
                0x6E303800u, // UADDLV H0, V0.16B
            };
        }

        private static uint[] _SU_Cvt_F_S_S_()
        {
            return new[]
            {
                0x5E21D820u, // SCVTF S0, S1
                0x7E21D820u, // UCVTF S0, S1
            };
        }

        private static uint[] _SU_Cvt_F_S_D_()
        {
            return new[]
            {
                0x5E61D820u, // SCVTF D0, D1
                0x7E61D820u, // UCVTF D0, D1
            };
        }

        private static uint[] _SU_Cvt_F_V_2S_4S_()
        {
            return new[]
            {
                0x0E21D800u, // SCVTF V0.2S, V0.2S
                0x2E21D800u, // UCVTF V0.2S, V0.2S
            };
        }

        private static uint[] _SU_Cvt_F_V_2D_()
        {
            return new[]
            {
                0x4E61D800u, // SCVTF V0.2D, V0.2D
                0x6E61D800u, // UCVTF V0.2D, V0.2D
            };
        }

        private static uint[] _Sha1h_Sha1su1_V_()
        {
            return new[]
            {
                0x5E280800u, // SHA1H   S0,    S0
                0x5E281800u, // SHA1SU1 V0.4S, V0.4S
            };
        }

        private static uint[] _Sha256su0_V_()
        {
            return new[]
            {
                0x5E282800u, // SHA256SU0 V0.4S, V0.4S
            };
        }
        #endregion

        private const int RndCnt = 2;

        private static readonly bool _noZeros = false;
        private static readonly bool _noInfs = false;
        private static readonly bool _noNaNs = false;

        [SkippableTheory(DisplayName = "ABS <V><d>, <V><n>")]
        [PairwiseData]
        public void Abs_S_D([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialMemberData(nameof(_1D_))] ulong z,
                            [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x5EE0B800; // ABS D0, D0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ABS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Abs_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                   [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E20B800; // ABS V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ABS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Abs_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E20B800; // ABS V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "ADDP <V><d>, <Vn>.<T>")]
        [PairwiseData]
        public void Addp_S_2DD([CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(1u, 0u)] uint rn,
                               [CombinatorialMemberData(nameof(_1D_))] ulong z,
                               [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x5EF1B800; // ADDP D0, V0.2D
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Add_Max_Min_V_V_8BB_4HH([CombinatorialMemberData(nameof(_SU_Add_Max_Min_V_V_8BB_4HH_))] uint opcodes,
                                               [CombinatorialValues(0u)] uint rd,
                                               [CombinatorialValues(1u, 0u)] uint rn,
                                               [CombinatorialMemberData(nameof(_8B4H_))] ulong z,
                                               [CombinatorialMemberData(nameof(_8B4H_))] ulong a,
                                               [CombinatorialValues(0b00u, 0b01u)] uint size) // <8BB, 4HH>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Add_Max_Min_V_V_16BB_8HH_4SS([CombinatorialMemberData(nameof(_SU_Add_Max_Min_V_V_16BB_8HH_4SS_))] uint opcodes,
                                                    [CombinatorialValues(0u)] uint rd,
                                                    [CombinatorialValues(1u, 0u)] uint rn,
                                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16BB, 8HH, 4SS>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Cls_V_8B_16B([CombinatorialValues(0u)] uint rd,
                                 [CombinatorialValues(1u, 0u)] uint rn,
                                 [CombinatorialMemberData(nameof(_8B_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                 [CombinatorialMemberData(nameof(_GenLeadingSigns8B_))] ulong a,
                                 [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint opcode = 0x0E204800; // CLS V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Cls_V_4H_8H([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_4H_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                [CombinatorialMemberData(nameof(_GenLeadingSigns4H_))] ulong a,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint opcode = 0x0E604800; // CLS V0.4H, V0.4H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Cls_V_2S_4S([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_2S_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                [CombinatorialMemberData(nameof(_GenLeadingSigns2S_))] ulong a,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint opcode = 0x0EA04800; // CLS V0.2S, V0.2S
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLZ <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Clz_V_8B_16B([CombinatorialValues(0u)] uint rd,
                                 [CombinatorialValues(1u, 0u)] uint rn,
                                 [CombinatorialMemberData(nameof(_8B_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                 [CombinatorialMemberData(nameof(_GenLeadingZeros8B_))] ulong a,
                                 [CombinatorialValues(0b0u, 0b1u)] uint q) // <8B, 16B>
        {
            uint opcode = 0x2E204800; // CLZ V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLZ <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Clz_V_4H_8H([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_4H_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                [CombinatorialMemberData(nameof(_GenLeadingZeros4H_))] ulong a,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <4H, 8H>
        {
            uint opcode = 0x2E604800; // CLZ V0.4H, V0.4H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CLZ <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Clz_V_2S_4S([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_2S_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                                [CombinatorialMemberData(nameof(_GenLeadingZeros2S_))] ulong a,
                                [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            uint opcode = 0x2EA04800; // CLZ V0.2S, V0.2S
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <V><d>, <V><n>, #0")]
        [PairwiseData]
        public void Cmeq_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x5EE09800; // CMEQ D0, D0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmeq_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E209800; // CMEQ V0.8B, V0.8B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMEQ <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmeq_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E209800; // CMEQ V0.16B, V0.16B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <V><d>, <V><n>, #0")]
        [PairwiseData]
        public void Cmge_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x7EE08800; // CMGE D0, D0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmge_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E208800; // CMGE V0.8B, V0.8B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGE <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmge_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E208800; // CMGE V0.16B, V0.16B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <V><d>, <V><n>, #0")]
        [PairwiseData]
        public void Cmgt_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x5EE08800; // CMGT D0, D0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmgt_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E208800; // CMGT V0.8B, V0.8B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMGT <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmgt_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E208800; // CMGT V0.16B, V0.16B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLE <V><d>, <V><n>, #0")]
        [PairwiseData]
        public void Cmle_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x7EE09800; // CMLE D0, D0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLE <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmle_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E209800; // CMLE V0.8B, V0.8B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLE <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmle_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E209800; // CMLE V0.16B, V0.16B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLT <V><d>, <V><n>, #0")]
        [PairwiseData]
        public void Cmlt_S_D([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_1D_))] ulong z,
                             [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x5EE0A800; // CMLT D0, D0, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLT <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmlt_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                    [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E20A800; // CMLT V0.8B, V0.8B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CMLT <Vd>.<T>, <Vn>.<T>, #0")]
        [PairwiseData]
        public void Cmlt_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E20A800; // CMLT V0.16B, V0.16B, #0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CNT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Cnt_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_8B_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                             [CombinatorialMemberData(nameof(_GenPopCnt8B_))] ulong a)
        {
            uint opcode = 0x0E205800; // CNT V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "CNT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Cnt_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))][CombinatorialRandomData(Count = RndCnt)] ulong z,
                              [CombinatorialMemberData(nameof(_GenPopCnt8B_))] ulong a)
        {
            uint opcode = 0x4E205800; // CNT V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abs_Neg_Recpx_Sqrt_S_S([CombinatorialMemberData(nameof(_F_Abs_Neg_Recpx_Sqrt_S_S_))] uint opcodes,
                                             [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abs_Neg_Recpx_Sqrt_S_D([CombinatorialMemberData(nameof(_F_Abs_Neg_Recpx_Sqrt_S_D_))] uint opcodes,
                                             [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abs_Neg_Sqrt_V_2S_4S([CombinatorialMemberData(nameof(_F_Abs_Neg_Sqrt_V_2S_4S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                           [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Abs_Neg_Sqrt_V_2D([CombinatorialMemberData(nameof(_F_Abs_Neg_Sqrt_V_2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Add_Max_Min_Nm_P_S_2SS([CombinatorialMemberData(nameof(_F_Add_Max_Min_Nm_P_S_2SS_))] uint opcodes,
                                             [CombinatorialMemberData(nameof(_2S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, z);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Add_Max_Min_Nm_P_S_2DD([CombinatorialMemberData(nameof(_F_Add_Max_Min_Nm_P_S_2DD_))] uint opcodes,
                                             [CombinatorialMemberData(nameof(_1D_F_))] ulong a0,
                                             [CombinatorialMemberData(nameof(_1D_F_))] ulong a1)
        {
            ulong z = Random.Shared.NextULong();

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a0, a1);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cm_EqGeGtLeLt_S_S([CombinatorialMemberData(nameof(_F_Cm_EqGeGtLeLt_S_S_))] uint opcodes,
                                        [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cm_EqGeGtLeLt_S_D([CombinatorialMemberData(nameof(_F_Cm_EqGeGtLeLt_S_D_))] uint opcodes,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cm_EqGeGtLeLt_V_2S_4S([CombinatorialMemberData(nameof(_F_Cm_EqGeGtLeLt_V_2S_4S_))] uint opcodes,
                                            [CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                            [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                            [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cm_EqGeGtLeLt_V_2D([CombinatorialMemberData(nameof(_F_Cm_EqGeGtLeLt_V_2D_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                         [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cmp_Cmpe_S_S([CombinatorialMemberData(nameof(_F_Cmp_Cmpe_S_S_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            V128 v1 = MakeVectorE0(a);

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            SingleOpcode(opcodes, v1: v1, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cmp_Cmpe_S_D([CombinatorialMemberData(nameof(_F_Cmp_Cmpe_S_D_))] uint opcodes,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            V128 v1 = MakeVectorE0(a);

            bool v = Random.Shared.NextBool();
            bool c = Random.Shared.NextBool();
            bool z = Random.Shared.NextBool();
            bool n = Random.Shared.NextBool();

            SingleOpcode(opcodes, v1: v1, overflow: v, carry: c, zero: z, negative: n);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_SD([CombinatorialMemberData(nameof(_F_Cvt_S_SD_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_DS([CombinatorialMemberData(nameof(_F_Cvt_S_DS_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        // Unicorn seems to default all rounding modes to RMode.Rn.
        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_SH([CombinatorialMemberData(nameof(_F_Cvt_S_SH_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                               [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_DH([CombinatorialMemberData(nameof(_F_Cvt_S_DH_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                               [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_HS([CombinatorialMemberData(nameof(_F_Cvt_S_HS_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1H_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_S_HD([CombinatorialMemberData(nameof(_F_Cvt_S_HD_))] uint opcodes,
                               [CombinatorialMemberData(nameof(_1H_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_ANZ_SU_S_S([CombinatorialMemberData(nameof(_F_Cvt_ANZ_SU_S_S_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1S_F_W_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_ANZ_SU_S_D([CombinatorialMemberData(nameof(_F_Cvt_ANZ_SU_S_D_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1D_F_X_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_ANZ_SU_V_2S_4S([CombinatorialMemberData(nameof(_F_Cvt_ANZ_SU_V_2S_4S_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_2S_F_W_))] ulong z,
                                         [CombinatorialMemberData(nameof(_2S_F_W_))] ulong a,
                                         [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvt_ANZ_SU_V_2D([CombinatorialMemberData(nameof(_F_Cvt_ANZ_SU_V_2D_))] uint opcodes,
                                      [CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_1D_F_X_))] ulong z,
                                      [CombinatorialMemberData(nameof(_1D_F_X_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvtl_V_4H4S_8H4S([CombinatorialMemberData(nameof(_F_Cvtl_V_4H4S_8H4S_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_4H_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_4H_F_))] ulong a,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q, // <4H4S, 8H4S>
                                       [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);
            fpcr |= rnd & (1 << (int)Fpcr.Ahp);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvtl_V_2S2D_4S2D([CombinatorialMemberData(nameof(_F_Cvtl_V_2S2D_4S2D_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S2D, 4S2D>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        // Unicorn seems to default all rounding modes to RMode.Rn.
        [SkippableTheory]
        [PairwiseData]
        public void F_Cvtn_V_4S4H_4S8H([CombinatorialMemberData(nameof(_F_Cvtn_V_4S4H_4S8H_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q, // <4S4H, 4S8H>
                                       [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, a);
            V128 v1 = MakeVectorE0E1(a, z);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);
            fpcr |= rnd & (1 << (int)Fpcr.Ahp);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Cvtn_V_2D2S_2D4S([CombinatorialMemberData(nameof(_F_Cvtn_V_2D2S_2D4S_))] uint opcodes,
                                       [CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                       [CombinatorialValues(0b0u, 0b1u)] uint q) // <2D2S, 2D4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, a);
            V128 v1 = MakeVectorE0E1(a, z);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Max_Min_Nm_V_V_4SS([CombinatorialMemberData(nameof(_F_Max_Min_Nm_V_V_4SS_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Ftoi_SW([CombinatorialMemberData(nameof(_F_Mov_Ftoi_SW_))] uint opcodes,
                                  [CombinatorialValues(0u, 31u)] uint rd,
                                  [CombinatorialValues(1u)] uint rn,
                                  [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x0 = (ulong)Random.Shared.NextUInt() << 32;
            uint w31 = Random.Shared.NextUInt();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x0: x0, x31: w31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Ftoi_DX([CombinatorialMemberData(nameof(_F_Mov_Ftoi_DX_))] uint opcodes,
                                  [CombinatorialValues(0u, 31u)] uint rd,
                                  [CombinatorialValues(1u)] uint rn,
                                  [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Ftoi1_DX([CombinatorialMemberData(nameof(_F_Mov_Ftoi1_DX_))] uint opcodes,
                                   [CombinatorialValues(0u, 31u)] uint rd,
                                   [CombinatorialValues(1u)] uint rn,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            V128 v1 = MakeVectorE1(a);

            SingleOpcode(opcodes, x31: x31, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Itof_WS([CombinatorialMemberData(nameof(_F_Mov_Itof_WS_))] uint opcodes,
                                  [CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 31u)] uint rn,
                                  [CombinatorialMemberData(nameof(_W_))] uint wn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            uint w31 = Random.Shared.NextUInt();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);

            SingleOpcode(opcodes, x1: wn, x31: w31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Itof_XD([CombinatorialMemberData(nameof(_F_Mov_Itof_XD_))] uint opcodes,
                                  [CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 31u)] uint rn,
                                  [CombinatorialMemberData(nameof(_X_))] ulong xn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_Itof1_XD([CombinatorialMemberData(nameof(_F_Mov_Itof1_XD_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 31u)] uint rn,
                                   [CombinatorialMemberData(nameof(_X_))] ulong xn)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            ulong x31 = Random.Shared.NextULong();
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0(z);

            SingleOpcode(opcodes, x1: xn, x31: x31, v0: v0);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_S_S([CombinatorialMemberData(nameof(_F_Mov_S_S_))] uint opcodes,
                              [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Mov_S_D([CombinatorialMemberData(nameof(_F_Mov_S_D_))] uint opcodes,
                              [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Recpe_Rsqrte_S_S([CombinatorialMemberData(nameof(_F_Recpe_Rsqrte_S_S_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                       [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Recpe_Rsqrte_S_D([CombinatorialMemberData(nameof(_F_Recpe_Rsqrte_S_D_))] uint opcodes,
                                       [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                       [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Recpe_Rsqrte_V_2S_4S([CombinatorialMemberData(nameof(_F_Recpe_Rsqrte_V_2S_4S_))] uint opcodes,
                                           [CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                           [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                           [CombinatorialValues(0b0u, 0b1u)] uint q, // <2S, 4S>
                                           [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Recpe_Rsqrte_V_2D([CombinatorialMemberData(nameof(_F_Recpe_Rsqrte_V_2D_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                        [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                        [CombinatorialValues(RMode.Rn)] RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            int rnd = (int)Random.Shared.NextUInt();

            int fpcr = (int)rMode << (int)Fpcr.RMode;
            fpcr |= rnd & (1 << (int)Fpcr.Fz);
            fpcr |= rnd & (1 << (int)Fpcr.Dn);

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Ioc | Fpsr.Dzc | Fpsr.Ofc | Fpsr.Ufc | Fpsr.Ixc | Fpsr.Idc);
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_AMNPZ_S_S([CombinatorialMemberData(nameof(_F_Rint_AMNPZ_S_S_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1S_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_AMNPZ_S_D([CombinatorialMemberData(nameof(_F_Rint_AMNPZ_S_D_))] uint opcodes,
                                     [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_AMNPZ_V_2S_4S([CombinatorialMemberData(nameof(_F_Rint_AMNPZ_V_2S_4S_))] uint opcodes,
                                         [CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                         [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                         [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_AMNPZ_V_2D([CombinatorialMemberData(nameof(_F_Rint_AMNPZ_V_2D_))] uint opcodes,
                                      [CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                      [CombinatorialMemberData(nameof(_1D_F_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_IX_S_S([CombinatorialMemberData(nameof(_F_Rint_IX_S_S_))] uint opcodes,
                                  [CombinatorialMemberData(nameof(_1S_F_))] ulong a,
                                  RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_IX_S_D([CombinatorialMemberData(nameof(_F_Rint_IX_S_D_))] uint opcodes,
                                  [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                  RMode rMode)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_IX_V_2S_4S([CombinatorialMemberData(nameof(_F_Rint_IX_V_2S_4S_))] uint opcodes,
                                      [CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_2S_F_))] ulong z,
                                      [CombinatorialMemberData(nameof(_2S_F_))] ulong a,
                                      [CombinatorialValues(0b0u, 0b1u)] uint q, // <2S, 4S>
                                      RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void F_Rint_IX_V_2D([CombinatorialMemberData(nameof(_F_Rint_IX_V_2D_))] uint opcodes,
                                   [CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong z,
                                   [CombinatorialMemberData(nameof(_1D_F_))] ulong a,
                                   RMode rMode)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            int fpcr = (int)rMode << (int)Fpcr.RMode;

            SingleOpcode(opcodes, v0: v0, v1: v1, fpcr: fpcr);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "NEG <V><d>, <V><n>")]
        [PairwiseData]
        public void Neg_S_D([CombinatorialValues(0u)] uint rd,
                            [CombinatorialValues(1u, 0u)] uint rn,
                            [CombinatorialMemberData(nameof(_1D_))] ulong z,
                            [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            uint opcode = 0x7EE0B800; // NEG D0, D0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "NEG <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Neg_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                   [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E20B800; // NEG V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "NEG <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Neg_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                       [CombinatorialValues(1u, 0u)] uint rn,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                       [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                       [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E20B800; // NEG V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "NOT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Not_V_8B([CombinatorialValues(0u)] uint rd,
                             [CombinatorialValues(1u, 0u)] uint rn,
                             [CombinatorialMemberData(nameof(_8B_))] ulong z,
                             [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x2E205800; // NOT V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "NOT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Not_V_16B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x6E205800; // NOT V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RBIT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rbit_V_8B([CombinatorialValues(0u)] uint rd,
                              [CombinatorialValues(1u, 0u)] uint rn,
                              [CombinatorialMemberData(nameof(_8B_))] ulong z,
                              [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x2E605800; // RBIT V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "RBIT <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rbit_V_16B([CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(1u, 0u)] uint rn,
                               [CombinatorialMemberData(nameof(_8B_))] ulong z,
                               [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x6E605800; // RBIT V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV16 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev16_V_8B([CombinatorialValues(0u)] uint rd,
                               [CombinatorialValues(1u, 0u)] uint rn,
                               [CombinatorialMemberData(nameof(_8B_))] ulong z,
                               [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x0E201800; // REV16 V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV16 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev16_V_16B([CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(_8B_))] ulong z,
                                [CombinatorialMemberData(nameof(_8B_))] ulong a)
        {
            uint opcode = 0x4E201800; // REV16 V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV32 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev32_V_8B_4H([CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 0u)] uint rn,
                                  [CombinatorialMemberData(nameof(_8B4H_))] ulong z,
                                  [CombinatorialMemberData(nameof(_8B4H_))] ulong a,
                                  [CombinatorialValues(0b00u, 0b01u)] uint size) // <8B, 4H>
        {
            uint opcode = 0x2E200800; // REV32 V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV32 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev32_V_16B_8H([CombinatorialValues(0u)] uint rd,
                                   [CombinatorialValues(1u, 0u)] uint rn,
                                   [CombinatorialMemberData(nameof(_8B4H_))] ulong z,
                                   [CombinatorialMemberData(nameof(_8B4H_))] ulong a,
                                   [CombinatorialValues(0b00u, 0b01u)] uint size) // <16B, 8H>
        {
            uint opcode = 0x6E200800; // REV32 V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV64 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev64_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E200800; // REV64 V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "REV64 <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Rev64_V_16B_8H_4S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B, 8H, 4S>
        {
            uint opcode = 0x4E200800; // REV64 V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADALP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Sadalp_V_8B4H_4H2S_2S1D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B4H, 4H2S, 2S1D>
        {
            uint opcode = 0x0E206800; // SADALP V0.4H, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADALP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Sadalp_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E206800; // SADALP V0.8H, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDLP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Saddlp_V_8B4H_4H2S_2S1D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B4H, 4H2S, 2S1D>
        {
            uint opcode = 0x0E202800; // SADDLP V0.4H, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SADDLP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Saddlp_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x4E202800; // SADDLP V0.8H, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Addl_V_V_8BH_4HS([CombinatorialMemberData(nameof(_SU_Addl_V_V_8BH_4HS_))] uint opcodes,
                                        [CombinatorialValues(0u)] uint rd,
                                        [CombinatorialValues(1u, 0u)] uint rn,
                                        [CombinatorialMemberData(nameof(_8B4H_))] ulong z,
                                        [CombinatorialMemberData(nameof(_8B4H_))] ulong a,
                                        [CombinatorialValues(0b00u, 0b01u)] uint size) // <8BH, 4HS>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Addl_V_V_16BH_8HS_4SD([CombinatorialMemberData(nameof(_SU_Addl_V_V_16BH_8HS_4SD_))] uint opcodes,
                                             [CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16BH, 8HS, 4SD>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_S_S([CombinatorialMemberData(nameof(_SU_Cvt_F_S_S_))] uint opcodes,
                                 [CombinatorialMemberData(nameof(_1S_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_S_D([CombinatorialMemberData(nameof(_SU_Cvt_F_S_D_))] uint opcodes,
                                 [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            ulong z = Random.Shared.NextULong();
            V128 v0 = MakeVectorE1(z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_V_2S_4S([CombinatorialMemberData(nameof(_SU_Cvt_F_V_2S_4S_))] uint opcodes,
                                     [CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_2S_))] ulong a,
                                     [CombinatorialValues(0b0u, 0b1u)] uint q) // <2S, 4S>
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcodes |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a * q);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void SU_Cvt_F_V_2D([CombinatorialMemberData(nameof(_SU_Cvt_F_V_2D_))] uint opcodes,
                                  [CombinatorialValues(0u)] uint rd,
                                  [CombinatorialValues(1u, 0u)] uint rn,
                                  [CombinatorialMemberData(nameof(_1D_))] ulong z,
                                  [CombinatorialMemberData(nameof(_1D_))] ulong a)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        public static readonly ulong[] RandomZ0 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomZ1 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomA0 =
        {
            Random.Shared.NextULong(),
        };
        public static readonly ulong[] RandomA1 =
        {
            Random.Shared.NextULong(),
        };

        [SkippableTheory]
        [PairwiseData]
        public void Sha1h_Sha1su1_V([CombinatorialMemberData(nameof(_Sha1h_Sha1su1_V_))] uint opcodes,
                                    [CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(RandomZ0))] ulong z0, [CombinatorialMemberData(nameof(RandomZ1))] ulong z1,
                                    [CombinatorialMemberData(nameof(RandomA0))] ulong a0, [CombinatorialMemberData(nameof(RandomA1))] ulong a1)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory]
        [PairwiseData]
        public void Sha256su0_V([CombinatorialMemberData(nameof(_Sha256su0_V_))] uint opcodes,
                                [CombinatorialValues(0u)] uint rd,
                                [CombinatorialValues(1u, 0u)] uint rn,
                                [CombinatorialMemberData(nameof(RandomZ0))] ulong z0, [CombinatorialMemberData(nameof(RandomZ1))] ulong z1,
                                [CombinatorialMemberData(nameof(RandomA0))] ulong a0, [CombinatorialMemberData(nameof(RandomA1))] ulong a1)
        {
            opcodes |= ((rn & 31) << 5) | ((rd & 31) << 0);

            V128 v0 = MakeVectorE0E1(z0, z1);
            V128 v1 = MakeVectorE0E1(a0, a1);

            SingleOpcode(opcodes, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SHLL{2} <Vd>.<Ta>, <Vn>.<Tb>, #<shift>")]
        [PairwiseData]
        public void Shll_V([CombinatorialValues(0u)] uint rd,
                           [CombinatorialValues(1u, 0u)] uint rn,
                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                           [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size, // <shift: 8, 16, 32>
                           [CombinatorialValues(0b0u, 0b1u)] uint q)
        {
            uint opcode = 0x2E213800; // SHLL V0.8H, V0.8B, #8
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);
            opcode |= ((q & 1) << 30);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(q == 0u ? a : 0ul, q == 1u ? a : 0ul);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "SQABS <V><d>, <V><n>")]
        [PairwiseData]
        public void Sqabs_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <B, H, S, D>
        {
            uint opcode = 0x5E207800; // SQABS B0, B0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQABS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Sqabs_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E207800; // SQABS V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQABS <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Sqabs_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E207800; // SQABS V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQNEG <V><d>, <V><n>")]
        [PairwiseData]
        public void Sqneg_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                    [CombinatorialValues(1u, 0u)] uint rn,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                    [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                    [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <B, H, S, D>
        {
            uint opcode = 0x7E207800; // SQNEG B0, B0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQNEG <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Sqneg_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                     [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E207800; // SQNEG V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQNEG <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Sqneg_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E207800; // SQNEG V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTN <Vb><d>, <Va><n>")]
        [PairwiseData]
        public void Sqxtn_S_HB_SH_DS([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_1H1S1D_))] ulong z,
                                     [CombinatorialMemberData(nameof(_1H1S1D_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint opcode = 0x5E214800; // SQXTN B0, H0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Sqxtn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x0E214800; // SQXTN V0.8B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Sqxtn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x4E214800; // SQXTN2 V0.16B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTUN <Vb><d>, <Va><n>")]
        [PairwiseData]
        public void Sqxtun_S_HB_SH_DS([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_1H1S1D_))] ulong z,
                                      [CombinatorialMemberData(nameof(_1H1S1D_))] ulong a,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint opcode = 0x7E212800; // SQXTUN B0, H0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTUN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Sqxtun_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x2E212800; // SQXTUN V0.8B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SQXTUN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Sqxtun_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                             [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x6E212800; // SQXTUN2 V0.16B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SUQADD <V><d>, <V><n>")]
        [PairwiseData]
        public void Suqadd_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                     [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <B, H, S, D>
        {
            uint opcode = 0x5E203800; // SUQADD B0, B0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SUQADD <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Suqadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x0E203800; // SUQADD V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "SUQADD <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Suqadd_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                          [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x4E203800; // SUQADD V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UADALP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Uadalp_V_8B4H_4H2S_2S1D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B4H, 4H2S, 2S1D>
        {
            uint opcode = 0x2E206800; // UADALP V0.4H, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADALP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Uadalp_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E206800; // UADALP V0.8H, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDLP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Uaddlp_V_8B4H_4H2S_2S1D([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                            [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B4H, 4H2S, 2S1D>
        {
            uint opcode = 0x2E202800; // UADDLP V0.4H, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UADDLP <Vd>.<Ta>, <Vn>.<Tb>")]
        [PairwiseData]
        public void Uaddlp_V_16B8H_8H4S_4S2D([CombinatorialValues(0u)] uint rd,
                                             [CombinatorialValues(1u, 0u)] uint rn,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                             [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                             [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <16B8H, 8H4S, 4S2D>
        {
            uint opcode = 0x6E202800; // UADDLP V0.8H, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "UQXTN <Vb><d>, <Va><n>")]
        [PairwiseData]
        public void Uqxtn_S_HB_SH_DS([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_1H1S1D_))] ulong z,
                                     [CombinatorialMemberData(nameof(_1H1S1D_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <HB, SH, DS>
        {
            uint opcode = 0x7E214800; // UQXTN B0, H0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Uqxtn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                           [CombinatorialValues(1u, 0u)] uint rn,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                           [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                           [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x2E214800; // UQXTN V0.8B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "UQXTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Uqxtn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                            [CombinatorialValues(1u, 0u)] uint rn,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                            [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                            [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x6E214800; // UQXTN2 V0.16B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "USQADD <V><d>, <V><n>")]
        [PairwiseData]
        public void Usqadd_S_B_H_S_D([CombinatorialValues(0u)] uint rd,
                                     [CombinatorialValues(1u, 0u)] uint rn,
                                     [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong z,
                                     [CombinatorialMemberData(nameof(_1B1H1S1D_))] ulong a,
                                     [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <B, H, S, D>
        {
            uint opcode = 0x7E203800; // USQADD B0, B0
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "USQADD <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Usqadd_V_8B_4H_2S([CombinatorialValues(0u)] uint rd,
                                      [CombinatorialValues(1u, 0u)] uint rn,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong z,
                                      [CombinatorialMemberData(nameof(_8B4H2S_))] ulong a,
                                      [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8B, 4H, 2S>
        {
            uint opcode = 0x2E203800; // USQADD V0.8B, V0.8B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0(a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "USQADD <Vd>.<T>, <Vn>.<T>")]
        [PairwiseData]
        public void Usqadd_V_16B_8H_4S_2D([CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong z,
                                          [CombinatorialMemberData(nameof(_8B4H2S1D_))] ulong a,
                                          [CombinatorialValues(0b00u, 0b01u, 0b10u, 0b11u)] uint size) // <16B, 8H, 4S, 2D>
        {
            uint opcode = 0x6E203800; // USQADD V0.16B, V0.16B
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn(fpsrMask: Fpsr.Qc);
        }

        [SkippableTheory(DisplayName = "XTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Xtn_V_8H8B_4S4H_2D2S([CombinatorialValues(0u)] uint rd,
                                         [CombinatorialValues(1u, 0u)] uint rn,
                                         [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                         [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                         [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H8B, 4S4H, 2D2S>
        {
            uint opcode = 0x0E212800; // XTN V0.8B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }

        [SkippableTheory(DisplayName = "XTN{2} <Vd>.<Tb>, <Vn>.<Ta>")]
        [PairwiseData]
        public void Xtn_V_8H16B_4S8H_2D4S([CombinatorialValues(0u)] uint rd,
                                          [CombinatorialValues(1u, 0u)] uint rn,
                                          [CombinatorialMemberData(nameof(_4H2S1D_))] ulong z,
                                          [CombinatorialMemberData(nameof(_4H2S1D_))] ulong a,
                                          [CombinatorialValues(0b00u, 0b01u, 0b10u)] uint size) // <8H16B, 4S8H, 2D4S>
        {
            uint opcode = 0x4E212800; // XTN2 V0.16B, V0.8H
            opcode |= ((rn & 31) << 5) | ((rd & 31) << 0);
            opcode |= ((size & 3) << 22);

            V128 v0 = MakeVectorE0E1(z, z);
            V128 v1 = MakeVectorE0E1(a, a);

            SingleOpcode(opcode, v0: v0, v1: v1);

            CompareAgainstUnicorn();
        }
#endif
    }
}
