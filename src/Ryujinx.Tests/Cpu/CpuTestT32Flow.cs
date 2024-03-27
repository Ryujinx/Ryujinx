#define T32Flow

using ARMeilleure.State;
using Xunit;
using Xunit.Abstractions;

namespace Ryujinx.Tests.Cpu
{
    [Collection("T32Flow")]
    public sealed class CpuTestT32Flow : CpuTest32
    {
        public CpuTestT32Flow(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

#if T32Flow

        [Fact]
        public void TestT32B1()
        {
            // BNE label
            ThumbOpcode(0xf040);
            ThumbOpcode(0x8240);
            for (int i = 0; i < 576; i++)
            {
                ThumbOpcode(0xe7fe);
            }
            // label: BX LR
            ThumbOpcode(0x4770);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);
        }

        [Fact]
        public void TestT32B2()
        {
            // BNE label1
            ThumbOpcode(0xf040);
            ThumbOpcode(0x8242);
            // label2: BNE label3
            ThumbOpcode(0xf040);
            ThumbOpcode(0x8242);
            for (int i = 0; i < 576; i++)
            {
                ThumbOpcode(0xe7fe);
            }
            // label1: BNE label2
            ThumbOpcode(0xf47f);
            ThumbOpcode(0xadbc);
            // label3: BX LR
            ThumbOpcode(0x4770);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);
        }

        [Fact]
        public void TestT32B3()
        {
            // B.W label
            ThumbOpcode(0xf000);
            ThumbOpcode(0xba40);
            for (int i = 0; i < 576; i++)
            {
                ThumbOpcode(0xe7fe);
            }
            // label: BX LR
            ThumbOpcode(0x4770);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);
        }

        [Fact]
        public void TestT32B4()
        {
            // B.W label1
            ThumbOpcode(0xf000);
            ThumbOpcode(0xba42);
            // label2: B.W label3
            ThumbOpcode(0xf000);
            ThumbOpcode(0xba42);
            for (int i = 0; i < 576; i++)
            {
                ThumbOpcode(0xe7fe);
            }
            // label1: B.W label2
            ThumbOpcode(0xf7ff);
            ThumbOpcode(0xbdbc);
            // label3: BX LR
            ThumbOpcode(0x4770);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);
        }

        [Fact]
        public void TestT32Bl()
        {
            // BL label
            ThumbOpcode(0xf000);
            ThumbOpcode(0xf840);
            for (int i = 0; i < 64; i++)
            {
                ThumbOpcode(0xe7fe);
            }
            ThumbOpcode(0x4670); // label: MOV R0, LR
            ThumbOpcode(0x2100); //        MOVS R1, #0
            ThumbOpcode(0x468e); //        MOV LR, R1
            ThumbOpcode(0x4770); //        BX LR

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);

            Assert.Equal(CodeBaseAddress + 0x5, GetContext().GetX(0));
        }

        [Fact]
        public void TestT32Blx1()
        {
            // BLX label
            ThumbOpcode(0xf000);
            ThumbOpcode(0xe840);
            for (int i = 0; i < 64; i++)
            {
                ThumbOpcode(0x4770);
            }
            // .arm ; label: MOV R0, LR
            Opcode(0xe1a0000e);
            // MOV LR, #0
            Opcode(0xe3a0e000);
            // BX LR
            Opcode(0xe12fff1e);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);

            Assert.Equal(CodeBaseAddress + 0x5, GetContext().GetX(0));
            Assert.False(GetContext().GetPstateFlag(PState.TFlag));
        }

        [Fact]
        public void TestT32Blx2()
        {
            // NOP
            ThumbOpcode(0xbf00);
            // BLX label
            ThumbOpcode(0xf000);
            ThumbOpcode(0xe840);
            for (int i = 0; i < 63; i++)
            {
                ThumbOpcode(0x4770);
            }
            // .arm ; label: MOV R0, LR
            Opcode(0xe1a0000e);
            // MOV LR, #0
            Opcode(0xe3a0e000);
            // BX LR
            Opcode(0xe12fff1e);

            GetContext().SetPstateFlag(PState.TFlag, true);

            ExecuteOpcodes(runUnicorn: false);

            Assert.Equal(CodeBaseAddress + 0x7, GetContext().GetX(0));
            Assert.False(GetContext().GetPstateFlag(PState.TFlag));
        }
#endif
    }
}
