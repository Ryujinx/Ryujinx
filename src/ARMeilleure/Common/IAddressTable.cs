using System;

namespace ARMeilleure.Common
{
    public interface IAddressTable<TEntry> : IDisposable where TEntry : unmanaged
    {
        /// <summary>
        /// True if the address table's bottom level is sparsely mapped.
        /// This also ensures the second bottom level is filled with a dummy page rather than 0.
        /// </summary>
        bool Sparse { get; }

        /// <summary>
        /// Gets the bits used by the <see cref="Levels"/> of the <see cref="IAddressTable{TEntry}"/> instance.
        /// </summary>
        ulong Mask { get; }

        /// <summary>
        /// Gets the <see cref="AddressTableLevel"/>s used by the <see cref="IAddressTable{TEntry}"/> instance.
        /// </summary>
        AddressTableLevel[] Levels { get; }

        /// <summary>
        /// Gets or sets the default fill value of newly created leaf pages.
        /// </summary>
        TEntry Fill { get; set; }

        /// <summary>
        /// Gets the base address of the <see cref="EntryTable{TEntry}"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException"><see cref="EntryTable{TEntry}"/> instance was disposed</exception>
        IntPtr Base { get; }

        /// <summary>
        /// Determines if the specified <paramref name="address"/> is in the range of the
        /// <see cref="IAddressTable{TEntry}"/>.
        /// </summary>
        /// <param name="address">Guest address</param>
        /// <returns><see langword="true"/> if is valid; otherwise <see langword="false"/></returns>
        bool IsValid(ulong address);

        /// <summary>
        /// Gets a reference to the value at the specified guest <paramref name="address"/>.
        /// </summary>
        /// <param name="address">Guest address</param>
        /// <returns>Reference to the value at the specified guest <paramref name="address"/></returns>
        /// <exception cref="ObjectDisposedException"><see cref="EntryTable{TEntry}"/> instance was disposed</exception>
        /// <exception cref="ArgumentException"><paramref name="address"/> is not mapped</exception>
        ref TEntry GetValue(ulong address);
    }
}
