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
    public class CpuTest32 : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;

        protected static readonly uint Size = (uint)MemoryBlock.GetPageSize();
#pragma warning disable CA2211 // Non-constant fields should not be visible
        protected static uint CodeBaseAddress = Size;
        protected static uint DataBaseAddress = CodeBaseAddress + Size;
#pragma warning restore CA2211

        private uint _currAddress;

        private MemoryBlock _ram;

        private MemoryManager _memory;

        private ExecutionContext _context;

        private CpuContext _cpuContext;
        private UnicornAArch32 _unicornEmu;

        private bool _usingMemory;

        public CpuTest32(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            Setup();
        }

        public void Setup()
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
            _context.IsAarch32 = true;

            _cpuContext = new CpuContext(_memory, for64Bit: false);

            // Prevent registering LCQ functions in the FunctionTable to avoid initializing and populating the table,
            // which improves test durations.
            Optimizations.AllowLcqInFunctionTable = false;
            Optimizations.UseUnmanagedDispatchLoop = false;

            _unicornEmu = new UnicornAArch32();
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

        protected void ThumbOpcode(ushort opcode)
        {
            _memory.Write(_currAddress, opcode);

            _unicornEmu.MemoryWrite16(_currAddress, opcode);

            _currAddress += 2;
        }

        protected ExecutionContext GetContext() => _context;

        protected void SetContext(uint r0 = 0,
                                  uint r1 = 0,
                                  uint r2 = 0,
                                  uint r3 = 0,
                                  uint sp = 0,
                                  V128 v0 = default,
                                  V128 v1 = default,
                                  V128 v2 = default,
                                  V128 v3 = default,
                                  V128 v4 = default,
                                  V128 v5 = default,
                                  V128 v14 = default,
                                  V128 v15 = default,
                                  bool saturation = false,
                                  bool overflow = false,
                                  bool carry = false,
                                  bool zero = false,
                                  bool negative = false,
                                  int fpscr = 0,
                                  bool thumb = false)
        {
            _context.SetX(0, r0);
            _context.SetX(1, r1);
            _context.SetX(2, r2);
            _context.SetX(3, r3);
            _context.SetX(13, sp);

            _context.SetV(0, v0);
            _context.SetV(1, v1);
            _context.SetV(2, v2);
            _context.SetV(3, v3);
            _context.SetV(4, v4);
            _context.SetV(5, v5);
            _context.SetV(14, v14);
            _context.SetV(15, v15);

            _context.SetPstateFlag(PState.QFlag, saturation);
            _context.SetPstateFlag(PState.VFlag, overflow);
            _context.SetPstateFlag(PState.CFlag, carry);
            _context.SetPstateFlag(PState.ZFlag, zero);
            _context.SetPstateFlag(PState.NFlag, negative);

            _context.Fpscr = (FPSCR)fpscr;

            _context.SetPstateFlag(PState.TFlag, thumb);

            _unicornEmu.R[0] = r0;
            _unicornEmu.R[1] = r1;
            _unicornEmu.R[2] = r2;
            _unicornEmu.R[3] = r3;
            _unicornEmu.SP = sp;

            _unicornEmu.Q[0] = V128ToSimdValue(v0);
            _unicornEmu.Q[1] = V128ToSimdValue(v1);
            _unicornEmu.Q[2] = V128ToSimdValue(v2);
            _unicornEmu.Q[3] = V128ToSimdValue(v3);
            _unicornEmu.Q[4] = V128ToSimdValue(v4);
            _unicornEmu.Q[5] = V128ToSimdValue(v5);
            _unicornEmu.Q[14] = V128ToSimdValue(v14);
            _unicornEmu.Q[15] = V128ToSimdValue(v15);

            _unicornEmu.QFlag = saturation;
            _unicornEmu.OverflowFlag = overflow;
            _unicornEmu.CarryFlag = carry;
            _unicornEmu.ZeroFlag = zero;
            _unicornEmu.NegativeFlag = negative;

            _unicornEmu.Fpscr = fpscr;

            _unicornEmu.ThumbFlag = thumb;
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
                                                uint r0 = 0,
                                                uint r1 = 0,
                                                uint r2 = 0,
                                                uint r3 = 0,
                                                uint sp = 0,
                                                V128 v0 = default,
                                                V128 v1 = default,
                                                V128 v2 = default,
                                                V128 v3 = default,
                                                V128 v4 = default,
                                                V128 v5 = default,
                                                V128 v14 = default,
                                                V128 v15 = default,
                                                bool saturation = false,
                                                bool overflow = false,
                                                bool carry = false,
                                                bool zero = false,
                                                bool negative = false,
                                                int fpscr = 0,
                                                bool runUnicorn = true)
        {
            Opcode(opcode);
            Opcode(0xE12FFF1E); // BX LR
            SetContext(r0, r1, r2, r3, sp, v0, v1, v2, v3, v4, v5, v14, v15, saturation, overflow, carry, zero, negative, fpscr);
            ExecuteOpcodes(runUnicorn);

            return GetContext();
        }

        protected ExecutionContext SingleThumbOpcode(ushort opcode,
                                                     uint r0 = 0,
                                                     uint r1 = 0,
                                                     uint r2 = 0,
                                                     uint r3 = 0,
                                                     uint sp = 0,
                                                     bool saturation = false,
                                                     bool overflow = false,
                                                     bool carry = false,
                                                     bool zero = false,
                                                     bool negative = false,
                                                     int fpscr = 0,
                                                     bool runUnicorn = true)
        {
            ThumbOpcode(opcode);
            ThumbOpcode(0x4770); // BX LR
            SetContext(r0, r1, r2, r3, sp, default, default, default, default, default, default, default, default, saturation, overflow, carry, zero, negative, fpscr, thumb: true);
            ExecuteOpcodes(runUnicorn);

            return GetContext();
        }

        public void RunPrecomputedTestCase(PrecomputedThumbTestCase test)
        {
            foreach (ushort instruction in test.Instructions)
            {
                ThumbOpcode(instruction);
            }

            for (int i = 0; i < 15; i++)
            {
                GetContext().SetX(i, test.StartRegs[i]);
            }

            uint startCpsr = test.StartRegs[15];
            for (int i = 0; i < 32; i++)
            {
                GetContext().SetPstateFlag((PState)i, (startCpsr & (1u << i)) != 0);
            }

            ExecuteOpcodes(runUnicorn: false);

            for (int i = 0; i < 15; i++)
            {
                Assert.Equal(test.FinalRegs[i], GetContext().GetX(i));
            }

            uint finalCpsr = test.FinalRegs[15];
            Assert.Equal(finalCpsr, GetContext().Pstate);
        }

        public void RunPrecomputedTestCase(PrecomputedMemoryThumbTestCase test)
        {
            byte[] testMem = new byte[Size];

            for (ulong i = 0; i < Size; i += 2)
            {
                testMem[i + 0] = (byte)((i + DataBaseAddress) >> 0);
                testMem[i + 1] = (byte)((i + DataBaseAddress) >> 8);
            }

            SetWorkingMemory(0, testMem);

            RunPrecomputedTestCase(new PrecomputedThumbTestCase
            {
                Instructions = test.Instructions,
                StartRegs = test.StartRegs,
                FinalRegs = test.FinalRegs,
            });

            foreach (var (address, value) in test.MemoryDelta)
            {
                testMem[address - DataBaseAddress + 0] = (byte)(value >> 0);
                testMem[address - DataBaseAddress + 1] = (byte)(value >> 8);
            }

            byte[] mem = _memory.GetSpan(DataBaseAddress, (int)Size).ToArray();

            Assert.Equal(testMem, mem);
        }

        protected void SetWorkingMemory(uint offset, byte[] data)
        {
            _memory.Write(DataBaseAddress + offset, data);

            _unicornEmu.MemoryWrite(DataBaseAddress + offset, data);

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

            /// <summary>NZCV flags.</summary>
            Nzcv = (1 << 31) | (1 << 30) | (1 << 29) | (1 << 28),
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
            if (fpSkips != FpSkips.None)
            {
                ManageFpSkips(fpSkips);
            }

            Assert.Equal(_unicornEmu.R[0], _context.GetX(0));
            Assert.Equal(_unicornEmu.R[1], _context.GetX(1));
            Assert.Equal(_unicornEmu.R[2], _context.GetX(2));
            Assert.Equal(_unicornEmu.R[3], _context.GetX(3));
            Assert.Equal(_unicornEmu.R[4], _context.GetX(4));
            Assert.Equal(_unicornEmu.R[5], _context.GetX(5));
            Assert.Equal(_unicornEmu.R[6], _context.GetX(6));
            Assert.Equal(_unicornEmu.R[7], _context.GetX(7));
            Assert.Equal(_unicornEmu.R[8], _context.GetX(8));
            Assert.Equal(_unicornEmu.R[9], _context.GetX(9));
            Assert.Equal(_unicornEmu.R[10], _context.GetX(10));
            Assert.Equal(_unicornEmu.R[11], _context.GetX(11));
            Assert.Equal(_unicornEmu.R[12], _context.GetX(12));
            Assert.Equal(_unicornEmu.SP, _context.GetX(13));
            Assert.Equal(_unicornEmu.R[14], _context.GetX(14));

            if (fpTolerances == FpTolerances.None)
            {
                Assert.Equal(_unicornEmu.Q[0], V128ToSimdValue(_context.GetV(0)));
            }
            else
            {
                ManageFpTolerances(fpTolerances);
            }
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

            Assert.Multiple(() =>
            {
                Assert.Equal((_unicornEmu.CPSR & (1u << 16)) != 0, _context.GetPstateFlag(PState.GE0Flag));
                Assert.Equal((_unicornEmu.CPSR & (1u << 17)) != 0, _context.GetPstateFlag(PState.GE1Flag));
                Assert.Equal((_unicornEmu.CPSR & (1u << 18)) != 0, _context.GetPstateFlag(PState.GE2Flag));
                Assert.Equal((_unicornEmu.CPSR & (1u << 19)) != 0, _context.GetPstateFlag(PState.GE3Flag));
                Assert.Equal(_unicornEmu.QFlag, _context.GetPstateFlag(PState.QFlag));
                Assert.Equal(_unicornEmu.OverflowFlag, _context.GetPstateFlag(PState.VFlag));
                Assert.Equal(_unicornEmu.CarryFlag, _context.GetPstateFlag(PState.CFlag));
                Assert.Equal(_unicornEmu.ZeroFlag, _context.GetPstateFlag(PState.ZFlag));
                Assert.Equal(_unicornEmu.NegativeFlag, _context.GetPstateFlag(PState.NFlag));
            });

            Assert.Equal(_unicornEmu.Fpscr & (int)fpsrMask, (int)_context.Fpscr & (int)fpsrMask);

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
                Skip.If((_unicornEmu.Fpscr & (int)Fpsr.Ufc) != 0, "Underflow test.");
            }

            if (fpSkips.HasFlag(FpSkips.IfOverflow))
            {
                Skip.If((_unicornEmu.Fpscr & (int)Fpsr.Ofc) != 0, "Overflow test.");
            }
        }

        private void ManageFpTolerances(FpTolerances fpTolerances)
        {
            bool IsNormalOrSubnormalS(float f) => float.IsNormal(f) || float.IsSubnormal(f);
            bool IsNormalOrSubnormalD(double d) => double.IsNormal(d) || double.IsSubnormal(d);

            if (_unicornEmu.Q[0] != V128ToSimdValue(_context.GetV(0)))
            {
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
                            Assert.Equal(_unicornEmu.Q[0].GetDouble(0), _context.GetV(0).Extract<double>(0), 1d);
                            Assert.Equal(_unicornEmu.Q[0].GetDouble(1), _context.GetV(0).Extract<double>(1), 1d);
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

        protected static V128 MakeVectorE0E1E2E3(uint e0, uint e1, uint e2, uint e3)
        {
            return new V128(e0, e1, e2, e3);
        }

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
