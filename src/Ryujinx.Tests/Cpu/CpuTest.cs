using ARMeilleure;
using ARMeilleure.State;
using ARMeilleure.Translation;
using Ryujinx.Cpu.Jit;
using Ryujinx.Memory;
using Ryujinx.Tests.Unicorn;
using System;
using Xunit;
using Xunit.Abstractions;
using MemoryPermission = Ryujinx.Tests.Unicorn.MemoryPermission;

namespace Ryujinx.Tests.Cpu
{
    public class CpuTest : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        protected static readonly ulong Size = MemoryBlock.GetPageSize();
#pragma warning disable CA2211 // Non-constant fields should not be visible
        protected static ulong CodeBaseAddress = Size;
        protected static ulong DataBaseAddress = CodeBaseAddress + Size;
#pragma warning restore CA2211

        private static readonly bool _ignoreFpcrFz = false;
        private static readonly bool _ignoreFpcrDn = false;

        private static readonly bool _ignoreAllExceptFpsrQc = false;

        private ulong _currAddress;

        private MemoryBlock _ram;

        private MemoryManager _memory;

        private ExecutionContext _context;

        private CpuContext _cpuContext;

        private UnicornAArch64 _unicornEmu;

        private bool _usingMemory;

        public CpuTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            Setup();
        }

        private void Setup()
        {
            int pageBits = (int)ulong.Log2(Size);

            _ram = new MemoryBlock(Size * 2);
            _memory = new MemoryManager(_ram, 1ul << (pageBits + 4));
            _memory.IncrementReferenceCount();

            // Some tests depends on hardcoded address that were computed for 4KiB.
            // We change the layout on non 4KiB platforms to keep compat here.
            if (Size > 0x1000)
            {
                DataBaseAddress = 0;
                CodeBaseAddress = Size;
            }

            _currAddress = CodeBaseAddress;

            _memory.Map(CodeBaseAddress, 0, Size, MemoryMapFlags.Private);
            _memory.Map(DataBaseAddress, Size, Size, MemoryMapFlags.Private);

            _context = CpuContext.CreateExecutionContext();

            _cpuContext = new CpuContext(_memory, for64Bit: true);

            // Prevent registering LCQ functions in the FunctionTable to avoid initializing and populating the table,
            // which improves test durations.
            Optimizations.AllowLcqInFunctionTable = false;
            Optimizations.UseUnmanagedDispatchLoop = false;

            _unicornEmu = new UnicornAArch64();
            _unicornEmu.MemoryMap(CodeBaseAddress, Size, MemoryPermission.Read | MemoryPermission.Exec);
            _unicornEmu.MemoryMap(DataBaseAddress, Size, MemoryPermission.Read | MemoryPermission.Write);
            _unicornEmu.PC = CodeBaseAddress;
        }

        public void Dispose()
        {
            _unicornEmu.Dispose();
            _unicornEmu = null;

            _memory.DecrementReferenceCount();
            _context.Dispose();
            _ram.Dispose();

            _memory = null;
            _context = null;
            _cpuContext = null;
            _unicornEmu = null;

            _usingMemory = false;
        }

        protected void Reset()
        {
            Dispose();
            Setup();
        }

        protected void Opcode(uint opcode)
        {
            _memory.Write(_currAddress, opcode);

            _unicornEmu.MemoryWrite32(_currAddress, opcode);

            _currAddress += 4;
        }

        protected ExecutionContext GetContext() => _context;

        protected void SetContext(ulong x0 = 0,
                                  ulong x1 = 0,
                                  ulong x2 = 0,
                                  ulong x3 = 0,
                                  ulong x31 = 0,
                                  V128 v0 = default,
                                  V128 v1 = default,
                                  V128 v2 = default,
                                  V128 v3 = default,
                                  V128 v4 = default,
                                  V128 v5 = default,
                                  V128 v30 = default,
                                  V128 v31 = default,
                                  bool overflow = false,
                                  bool carry = false,
                                  bool zero = false,
                                  bool negative = false,
                                  int fpcr = 0,
                                  int fpsr = 0)
        {
            _context.SetX(0, x0);
            _context.SetX(1, x1);
            _context.SetX(2, x2);
            _context.SetX(3, x3);
            _context.SetX(31, x31);

            _context.SetV(0, v0);
            _context.SetV(1, v1);
            _context.SetV(2, v2);
            _context.SetV(3, v3);
            _context.SetV(4, v4);
            _context.SetV(5, v5);
            _context.SetV(30, v30);
            _context.SetV(31, v31);

            _context.SetPstateFlag(PState.VFlag, overflow);
            _context.SetPstateFlag(PState.CFlag, carry);
            _context.SetPstateFlag(PState.ZFlag, zero);
            _context.SetPstateFlag(PState.NFlag, negative);

            _context.Fpcr = (FPCR)fpcr;
            _context.Fpsr = (FPSR)fpsr;

            _unicornEmu.X[0] = x0;
            _unicornEmu.X[1] = x1;
            _unicornEmu.X[2] = x2;
            _unicornEmu.X[3] = x3;
            _unicornEmu.SP = x31;

            _unicornEmu.Q[0] = V128ToSimdValue(v0);
            _unicornEmu.Q[1] = V128ToSimdValue(v1);
            _unicornEmu.Q[2] = V128ToSimdValue(v2);
            _unicornEmu.Q[3] = V128ToSimdValue(v3);
            _unicornEmu.Q[4] = V128ToSimdValue(v4);
            _unicornEmu.Q[5] = V128ToSimdValue(v5);
            _unicornEmu.Q[30] = V128ToSimdValue(v30);
            _unicornEmu.Q[31] = V128ToSimdValue(v31);

            _unicornEmu.OverflowFlag = overflow;
            _unicornEmu.CarryFlag = carry;
            _unicornEmu.ZeroFlag = zero;
            _unicornEmu.NegativeFlag = negative;

            _unicornEmu.Fpcr = fpcr;
            _unicornEmu.Fpsr = fpsr;
        }

        protected void ExecuteOpcodes(bool runUnicorn = true)
        {
            _cpuContext.Execute(_context, CodeBaseAddress);

            if (runUnicorn)
            {
                _unicornEmu.RunForCount((_currAddress - CodeBaseAddress - 4) / 4);
            }
        }

        protected ExecutionContext SingleOpcode(uint opcode,
                                                ulong x0 = 0,
                                                ulong x1 = 0,
                                                ulong x2 = 0,
                                                ulong x3 = 0,
                                                ulong x31 = 0,
                                                V128 v0 = default,
                                                V128 v1 = default,
                                                V128 v2 = default,
                                                V128 v3 = default,
                                                V128 v4 = default,
                                                V128 v5 = default,
                                                V128 v30 = default,
                                                V128 v31 = default,
                                                bool overflow = false,
                                                bool carry = false,
                                                bool zero = false,
                                                bool negative = false,
                                                int fpcr = 0,
                                                int fpsr = 0,
                                                bool runUnicorn = true)
        {
            if (_ignoreFpcrFz)
            {
                fpcr &= ~(1 << (int)Fpcr.Fz);
            }

            if (_ignoreFpcrDn)
            {
                fpcr &= ~(1 << (int)Fpcr.Dn);
            }

            Opcode(opcode);
            Opcode(0xD65F03C0); // RET
            SetContext(x0, x1, x2, x3, x31, v0, v1, v2, v3, v4, v5, v30, v31, overflow, carry, zero, negative, fpcr, fpsr);
            ExecuteOpcodes(runUnicorn);

            return GetContext();
        }

        protected void SetWorkingMemory(ulong offset, byte[] data)
        {
            _memory.Write(DataBaseAddress + offset, data);

            _unicornEmu.MemoryWrite(DataBaseAddress + offset, data);

            _usingMemory = true; // When true, CompareAgainstUnicorn checks the working memory for equality too.
        }

        protected void SetWorkingMemory(ulong offset, byte data)
        {
            _memory.Write(DataBaseAddress + offset, data);

            _unicornEmu.MemoryWrite8(DataBaseAddress + offset, data);

            _usingMemory = true; // When true, CompareAgainstUnicorn checks the working memory for equality too.
        }

        /// <summary>Rounding Mode control field.</summary>
        public enum RMode
        {
            /// <summary>Round to Nearest mode.</summary>
            Rn,
            /// <summary>Round towards Plus Infinity mode.</summary>
            Rp,
            /// <summary>Round towards Minus Infinity mode.</summary>
            Rm,
            /// <summary>Round towards Zero mode.</summary>
            Rz,
        }

        /// <summary>Floating-point Control Register.</summary>
        protected enum Fpcr
        {
            /// <summary>Rounding Mode control field.</summary>
            RMode = 22,
            /// <summary>Flush-to-zero mode control bit.</summary>
            Fz = 24,
            /// <summary>Default NaN mode control bit.</summary>
            Dn = 25,
            /// <summary>Alternative half-precision control bit.</summary>
            Ahp = 26,
        }

        /// <summary>Floating-point Status Register.</summary>
        [Flags]
        protected enum Fpsr
        {
            None = 0,

            /// <summary>Invalid Operation cumulative floating-point exception bit.</summary>
            Ioc = 1 << 0,
            /// <summary>Divide by Zero cumulative floating-point exception bit.</summary>
            Dzc = 1 << 1,
            /// <summary>Overflow cumulative floating-point exception bit.</summary>
            Ofc = 1 << 2,
            /// <summary>Underflow cumulative floating-point exception bit.</summary>
            Ufc = 1 << 3,
            /// <summary>Inexact cumulative floating-point exception bit.</summary>
            Ixc = 1 << 4,
            /// <summary>Input Denormal cumulative floating-point exception bit.</summary>
            Idc = 1 << 7,

            /// <summary>Cumulative saturation bit.</summary>
            Qc = 1 << 27,
        }

        [Flags]
        protected enum FpSkips
        {
            None = 0,

            IfNaNS = 1,
            IfNaND = 2,

            IfUnderflow = 4,
            IfOverflow = 8,
        }

        protected enum FpTolerances
        {
            None,

            UpToOneUlpsS,
            UpToOneUlpsD,
        }

        protected void CompareAgainstUnicorn(
            Fpsr fpsrMask = Fpsr.None,
            FpSkips fpSkips = FpSkips.None,
            FpTolerances fpTolerances = FpTolerances.None)
        {
            if (_ignoreAllExceptFpsrQc)
            {
                fpsrMask &= Fpsr.Qc;
            }

            if (fpSkips != FpSkips.None)
            {
                ManageFpSkips(fpSkips);
            }

#pragma warning disable IDE0055 // Disable formatting
            Assert.Equal(_unicornEmu.X[0], _context.GetX(0));
            Assert.Equal(_unicornEmu.X[1], _context.GetX(1));
            Assert.Equal(_unicornEmu.X[2], _context.GetX(2));
            Assert.Equal(_unicornEmu.X[3], _context.GetX(3));
            Assert.Equal(_unicornEmu.X[4], _context.GetX(4));
            Assert.Equal(_unicornEmu.X[5], _context.GetX(5));
            Assert.Equal(_unicornEmu.X[6], _context.GetX(6));
            Assert.Equal(_unicornEmu.X[7], _context.GetX(7));
            Assert.Equal(_unicornEmu.X[8], _context.GetX(8));
            Assert.Equal(_unicornEmu.X[9], _context.GetX(9));
            Assert.Equal(_unicornEmu.X[10], _context.GetX(10));
            Assert.Equal(_unicornEmu.X[11], _context.GetX(11));
            Assert.Equal(_unicornEmu.X[12], _context.GetX(12));
            Assert.Equal(_unicornEmu.X[13], _context.GetX(13));
            Assert.Equal(_unicornEmu.X[14], _context.GetX(14));
            Assert.Equal(_unicornEmu.X[15], _context.GetX(15));
            Assert.Equal(_unicornEmu.X[16], _context.GetX(16));
            Assert.Equal(_unicornEmu.X[17], _context.GetX(17));
            Assert.Equal(_unicornEmu.X[18], _context.GetX(18));
            Assert.Equal(_unicornEmu.X[19], _context.GetX(19));
            Assert.Equal(_unicornEmu.X[20], _context.GetX(20));
            Assert.Equal(_unicornEmu.X[21], _context.GetX(21));
            Assert.Equal(_unicornEmu.X[22], _context.GetX(22));
            Assert.Equal(_unicornEmu.X[23], _context.GetX(23));
            Assert.Equal(_unicornEmu.X[24], _context.GetX(24));
            Assert.Equal(_unicornEmu.X[25], _context.GetX(25));
            Assert.Equal(_unicornEmu.X[26], _context.GetX(26));
            Assert.Equal(_unicornEmu.X[27], _context.GetX(27));
            Assert.Equal(_unicornEmu.X[28], _context.GetX(28));
            Assert.Equal(_unicornEmu.X[29], _context.GetX(29));
            Assert.Equal(_unicornEmu.X[30], _context.GetX(30));
            Assert.Equal(_unicornEmu.SP, _context.GetX(31));
#pragma warning restore IDE0055

            if (fpTolerances == FpTolerances.None)
            {
                Assert.Equal(_unicornEmu.Q[0], V128ToSimdValue(_context.GetV(0)));
            }
            else
            {
                ManageFpTolerances(fpTolerances);
            }

#pragma warning disable IDE0055 // Disable formatting
            Assert.Equal(_unicornEmu.Q[1], V128ToSimdValue(_context.GetV(1)));
            Assert.Equal(_unicornEmu.Q[2], V128ToSimdValue(_context.GetV(2)));
            Assert.Equal(_unicornEmu.Q[3], V128ToSimdValue(_context.GetV(3)));
            Assert.Equal(_unicornEmu.Q[4], V128ToSimdValue(_context.GetV(4)));
            Assert.Equal(_unicornEmu.Q[5], V128ToSimdValue(_context.GetV(5)));
            Assert.Equal(_unicornEmu.Q[6], V128ToSimdValue(_context.GetV(6)));
            Assert.Equal(_unicornEmu.Q[7], V128ToSimdValue(_context.GetV(7)));
            Assert.Equal(_unicornEmu.Q[8], V128ToSimdValue(_context.GetV(8)));
            Assert.Equal(_unicornEmu.Q[9], V128ToSimdValue(_context.GetV(9)));
            Assert.Equal(_unicornEmu.Q[10], V128ToSimdValue(_context.GetV(10)));
            Assert.Equal(_unicornEmu.Q[11], V128ToSimdValue(_context.GetV(11)));
            Assert.Equal(_unicornEmu.Q[12], V128ToSimdValue(_context.GetV(12)));
            Assert.Equal(_unicornEmu.Q[13], V128ToSimdValue(_context.GetV(13)));
            Assert.Equal(_unicornEmu.Q[14], V128ToSimdValue(_context.GetV(14)));
            Assert.Equal(_unicornEmu.Q[15], V128ToSimdValue(_context.GetV(15)));
            Assert.Equal(_unicornEmu.Q[16], V128ToSimdValue(_context.GetV(16)));
            Assert.Equal(_unicornEmu.Q[17], V128ToSimdValue(_context.GetV(17)));
            Assert.Equal(_unicornEmu.Q[18], V128ToSimdValue(_context.GetV(18)));
            Assert.Equal(_unicornEmu.Q[19], V128ToSimdValue(_context.GetV(19)));
            Assert.Equal(_unicornEmu.Q[20], V128ToSimdValue(_context.GetV(20)));
            Assert.Equal(_unicornEmu.Q[21], V128ToSimdValue(_context.GetV(21)));
            Assert.Equal(_unicornEmu.Q[22], V128ToSimdValue(_context.GetV(22)));
            Assert.Equal(_unicornEmu.Q[23], V128ToSimdValue(_context.GetV(23)));
            Assert.Equal(_unicornEmu.Q[24], V128ToSimdValue(_context.GetV(24)));
            Assert.Equal(_unicornEmu.Q[25], V128ToSimdValue(_context.GetV(25)));
            Assert.Equal(_unicornEmu.Q[26], V128ToSimdValue(_context.GetV(26)));
            Assert.Equal(_unicornEmu.Q[27], V128ToSimdValue(_context.GetV(27)));
            Assert.Equal(_unicornEmu.Q[28], V128ToSimdValue(_context.GetV(28)));
            Assert.Equal(_unicornEmu.Q[29], V128ToSimdValue(_context.GetV(29)));
            Assert.Equal(_unicornEmu.Q[30], V128ToSimdValue(_context.GetV(30)));
            Assert.Equal(_unicornEmu.Q[31], V128ToSimdValue(_context.GetV(31)));

            Assert.Multiple(() =>
            {
                Assert.Equal(_unicornEmu.OverflowFlag, _context.GetPstateFlag(PState.VFlag));
                Assert.Equal(_unicornEmu.CarryFlag, _context.GetPstateFlag(PState.CFlag));
                Assert.Equal(_unicornEmu.ZeroFlag, _context.GetPstateFlag(PState.ZFlag));
                Assert.Equal(_unicornEmu.NegativeFlag, _context.GetPstateFlag(PState.NFlag));
            });

            Assert.Equal(_unicornEmu.Fpcr, (int)_context.Fpcr);
            Assert.Equal(_unicornEmu.Fpsr & (int)fpsrMask, (int)_context.Fpsr & (int)fpsrMask);
#pragma warning restore IDE0055

            if (_usingMemory)
            {
                byte[] mem = _memory.GetSpan(DataBaseAddress, (int)Size).ToArray();
                byte[] unicornMem = _unicornEmu.MemoryRead(DataBaseAddress, Size);

                Assert.Equal(unicornMem, mem);
            }
        }

        private void ManageFpSkips(FpSkips fpSkips)
        {
            if (fpSkips.HasFlag(FpSkips.IfNaNS))
            {
                Skip.If(float.IsNaN(_unicornEmu.Q[0].AsFloat()), "NaN test.");
            }
            else if (fpSkips.HasFlag(FpSkips.IfNaND))
            {
                Skip.If(double.IsNaN(_unicornEmu.Q[0].AsDouble()), "NaN test.");
            }

            if (fpSkips.HasFlag(FpSkips.IfUnderflow))
            {
                Skip.If((_unicornEmu.Fpsr & (int)Fpsr.Ufc) != 0, "Underflow test.");
            }

            if (fpSkips.HasFlag(FpSkips.IfOverflow))
            {
                Skip.If((_unicornEmu.Fpsr & (int)Fpsr.Ofc) != 0, "Overflow test.");
            }
        }

        private void ManageFpTolerances(FpTolerances fpTolerances)
        {
            bool IsNormalOrSubnormalS(float f) => float.IsNormal(f) || float.IsSubnormal(f);
            bool IsNormalOrSubnormalD(double d) => double.IsNormal(d) || double.IsSubnormal(d);

            if (V128ToSimdValue(_context.GetV(0)) != _unicornEmu.Q[0])
            {
                // https://docs.nunit.org/articles/nunit/writing-tests/constraints/EqualConstraint.html#comparing-floating-point-values
                // NOTE: XUnit only allows us to specify a tolerance and a MidpointRounding value
                if (fpTolerances == FpTolerances.UpToOneUlpsS)
                {
                    if (IsNormalOrSubnormalS(_unicornEmu.Q[0].AsFloat()) &&
                        IsNormalOrSubnormalS(_context.GetV(0).As<float>()))
                    {
                        Assert.Multiple(() =>
                        {
                            Assert.Equal(_unicornEmu.Q[0].GetFloat(0), _context.GetV(0).Extract<float>(0), 1f);
                            Assert.Equal(_unicornEmu.Q[0].GetFloat(1), _context.GetV(0).Extract<float>(1), 1f);
                            Assert.Equal(_unicornEmu.Q[0].GetFloat(2), _context.GetV(0).Extract<float>(2), 1f);
                            Assert.Equal(_unicornEmu.Q[0].GetFloat(3), _context.GetV(0).Extract<float>(3), 1f);
                        });

                        _testOutputHelper.WriteLine(fpTolerances.ToString());
                    }
                    else
                    {
                        Assert.Equal(_unicornEmu.Q[0], V128ToSimdValue(_context.GetV(0)));
                    }
                }

                if (fpTolerances == FpTolerances.UpToOneUlpsD)
                {
                    if (IsNormalOrSubnormalD(_unicornEmu.Q[0].AsDouble()) &&
                        IsNormalOrSubnormalD(_context.GetV(0).As<double>()))
                    {
                        Assert.Multiple(() =>
                        {
                            Assert.Equal(_context.GetV(0).Extract<double>(0),
                                _unicornEmu.Q[0].GetDouble(0), 1d);
                            Assert.Equal(_context.GetV(0).Extract<double>(1),
                                _unicornEmu.Q[0].GetDouble(1), 1d);
                        });

                        _testOutputHelper.WriteLine(fpTolerances.ToString());
                    }
                    else
                    {
                        Assert.Equal(_unicornEmu.Q[0], V128ToSimdValue(_context.GetV(0)));
                    }
                }
            }
        }

        private static SimdValue V128ToSimdValue(V128 value)
        {
            return new SimdValue(value.Extract<ulong>(0), value.Extract<ulong>(1));
        }

        protected static V128 MakeVectorScalar(float value) => new(value);
        protected static V128 MakeVectorScalar(double value) => new(value);

        protected static V128 MakeVectorE0(ulong e0) => new(e0, 0);
        protected static V128 MakeVectorE1(ulong e1) => new(0, e1);

        protected static V128 MakeVectorE0E1(ulong e0, ulong e1) => new(e0, e1);

        protected static ulong GetVectorE0(V128 vector) => vector.Extract<ulong>(0);
        protected static ulong GetVectorE1(V128 vector) => vector.Extract<ulong>(1);

        protected static ushort GenNormalH()
        {
            uint rnd;

            do
                rnd = Random.Shared.NextUShort();
            while ((rnd & 0x7C00u) == 0u ||
                   (~rnd & 0x7C00u) == 0u);

            return (ushort)rnd;
        }

        protected static ushort GenSubnormalH()
        {
            uint rnd;

            do
                rnd = Random.Shared.NextUShort();
            while ((rnd & 0x03FFu) == 0u);

            return (ushort)(rnd & 0x83FFu);
        }

        protected static uint GenNormalS()
        {
            uint rnd;

            do
                rnd = Random.Shared.NextUInt();
            while ((rnd & 0x7F800000u) == 0u ||
                   (~rnd & 0x7F800000u) == 0u);

            return rnd;
        }

        protected static uint GenSubnormalS()
        {
            uint rnd;

            do
                rnd = Random.Shared.NextUInt();
            while ((rnd & 0x007FFFFFu) == 0u);

            return rnd & 0x807FFFFFu;
        }

        protected static ulong GenNormalD()
        {
            ulong rnd;

            do
                rnd = Random.Shared.NextULong();
            while ((rnd & 0x7FF0000000000000ul) == 0ul ||
                   (~rnd & 0x7FF0000000000000ul) == 0ul);

            return rnd;
        }

        protected static ulong GenSubnormalD()
        {
            ulong rnd;

            do
                rnd = Random.Shared.NextULong();
            while ((rnd & 0x000FFFFFFFFFFFFFul) == 0ul);

            return rnd & 0x800FFFFFFFFFFFFFul;
        }
    }
}
