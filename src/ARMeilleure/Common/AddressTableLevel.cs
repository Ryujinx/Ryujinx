namespace ARMeilleure.Common
{
    /// <summary>
    /// Represents a level in an <see cref="IAddressTable{TEntry}"/>.
    /// </summary>
    public readonly struct AddressTableLevel
    {
        /// <summary>
        /// Gets the index of the <see cref="Level"/> in the guest address.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the length of the <see cref="AddressTableLevel"/> in the guest address.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the mask which masks the bits used by the <see cref="AddressTableLevel"/>.
        /// </summary>
        public ulong Mask => ((1ul << Length) - 1) << Index;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddressTableLevel"/> structure with the specified
        /// <paramref name="index"/> and <paramref name="length"/>.
        /// </summary>
        /// <param name="index">Index of the <see cref="AddressTableLevel"/></param>
        /// <param name="length">Length of the <see cref="AddressTableLevel"/></param>
        public AddressTableLevel(int index, int length)
        {
            (Index, Length) = (index, length);
        }

        /// <summary>
        /// Gets the value of the <see cref="AddressTableLevel"/> from the specified guest <paramref name="address"/>.
        /// </summary>
        /// <param name="address">Guest address</param>
        /// <returns>Value of the <see cref="AddressTableLevel"/> from the specified guest <paramref name="address"/></returns>
        public int GetValue(ulong address)
        {
            return (int)((address & Mask) >> Index);
        }
    }
}
