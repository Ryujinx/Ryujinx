using System.Diagnostics;

namespace Ryujinx.Cpu.LightningJit.Arm64
{
    static class SysUtils
    {
        public static (uint, uint, uint, uint) UnpackOp1CRnCRmOp2(uint encoding)
        {
            uint op1 = (encoding >> 16) & 7;
            uint crn = (encoding >> 12) & 0xf;
            uint crm = (encoding >> 8) & 0xf;
            uint op2 = (encoding >> 5) & 7;

            return (op1, crn, crm, op2);
        }

        public static bool IsCacheInstEl0(uint encoding)
        {
            (uint op1, uint crn, uint crm, uint op2) = UnpackOp1CRnCRmOp2(encoding);

            return ((op1 << 11) | (crn << 7) | (crm << 3) | op2) switch
            {
                0b011_0111_0100_001 => true, // DC ZVA
                0b011_0111_1010_001 => true, // DC CVAC
                0b011_0111_1100_001 => true, // DC CVAP
                0b011_0111_1011_001 => true, // DC CVAU
                0b011_0111_1110_001 => true, // DC CIVAC
                0b011_0111_0101_001 => true, // IC IVAU
                _ => false,
            };
        }

        public static bool IsCacheInstUciTrapped(uint encoding)
        {
            (uint op1, uint crn, uint crm, uint op2) = UnpackOp1CRnCRmOp2(encoding);

            return ((op1 << 11) | (crn << 7) | (crm << 3) | op2) switch
            {
                0b011_0111_1010_001 => true, // DC CVAC
                0b011_0111_1100_001 => true, // DC CVAP
                0b011_0111_1011_001 => true, // DC CVAU
                0b011_0111_1110_001 => true, // DC CIVAC
                0b011_0111_0101_001 => true, // IC IVAU
                _ => false,
            };
        }
    }
}
