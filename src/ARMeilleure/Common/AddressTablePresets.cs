namespace ARMeilleure.Common
{
    public static class AddressTablePresets
    {
        private static readonly AddressTableLevel[] _levels64Bit =
            new AddressTableLevel[]
            {
                new(31, 17),
                new(23,  8),
                new(15,  8),
                new( 7,  8),
                new( 2,  5),
            };

        private static readonly AddressTableLevel[] _levels32Bit =
            new AddressTableLevel[]
            {
                new(31, 17),
                new(23,  8),
                new(15,  8),
                new( 7,  8),
                new( 1,  6),
            };

        private static readonly AddressTableLevel[] _levels64BitSparse =
            new AddressTableLevel[]
            {
                new(23, 16),
                new( 2, 21),
            };

        private static readonly AddressTableLevel[] _levels32BitSparse =
            new AddressTableLevel[]
            {
                new(22, 10),
                new( 1, 21),
            };

        public static AddressTableLevel[] GetArmPreset(bool for64Bits, bool sparse)
        {
            if (sparse)
            {
                return for64Bits ? _levels64BitSparse : _levels32BitSparse;
            }
            else
            {
                return for64Bits ? _levels64Bit : _levels32Bit;
            }
        }
    }
}
