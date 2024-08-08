using Ryujinx.Graphics.Device;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Engine.Threed
{
    /// <summary>
    /// State update callback entry, with the callback function and associated field names.
    /// </summary>
    readonly struct StateUpdateCallbackEntry
    {
        /// <summary>
        /// Callback function, to be called if the register was written as the state needs to be updated.
        /// </summary>
        public Action Callback { get; }

        /// <summary>
        /// Name of the state fields (registers) associated with the callback function.
        /// </summary>
        public string[] FieldNames { get; }

        /// <summary>
        /// Creates a new state update callback entry.
        /// </summary>
        /// <param name="callback">Callback function, to be called if the register was written as the state needs to be updated</param>
        /// <param name="fieldNames">Name of the state fields (registers) associated with the callback function</param>
        public StateUpdateCallbackEntry(Action callback, params string[] fieldNames)
        {
            Callback = callback;
            FieldNames = fieldNames;
        }
    }

    /// <summary>
    /// GPU state update tracker.
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    class StateUpdateTracker<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TState>
    {
        private const int BlockSize = 0xe00;
        private const int RegisterSize = sizeof(uint);

        private readonly byte[] _registerToGroupMapping;
        private readonly Action[] _callbacks;
        private ulong _dirtyMask;

        /// <summary>
        /// Creates a new instance of the state update tracker.
        /// </summary>
        /// <param name="entries">Update tracker callback entries</param>
        public StateUpdateTracker(StateUpdateCallbackEntry[] entries)
        {
            _registerToGroupMapping = new byte[BlockSize];
            _callbacks = new Action[entries.Length];

            var fieldToDelegate = new Dictionary<string, int>();

            for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                var entry = entries[entryIndex];

                foreach (var fieldName in entry.FieldNames)
                {
                    fieldToDelegate.Add(fieldName, entryIndex);
                }

                _callbacks[entryIndex] = entry.Callback;
            }

            var fields = typeof(TState).GetFields();
            int offset = 0;

            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                var field = fields[fieldIndex];

                var currentFieldOffset = (int)Marshal.OffsetOf<TState>(field.Name);
                var nextFieldOffset = fieldIndex + 1 == fields.Length ? Unsafe.SizeOf<TState>() : (int)Marshal.OffsetOf<TState>(fields[fieldIndex + 1].Name);

                int sizeOfField = nextFieldOffset - currentFieldOffset;

                if (fieldToDelegate.TryGetValue(field.Name, out int entryIndex))
                {
                    for (int i = 0; i < ((sizeOfField + 3) & ~3); i += 4)
                    {
                        _registerToGroupMapping[(offset + i) / RegisterSize] = (byte)(entryIndex + 1);
                    }
                }

                offset += sizeOfField;
            }

            Debug.Assert(offset == Unsafe.SizeOf<TState>());
        }

        /// <summary>
        /// Sets a register as modified.
        /// </summary>
        /// <param name="offset">Register offset in bytes</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty(int offset)
        {
            uint index = (uint)offset / RegisterSize;

            if (index < BlockSize)
            {
                int groupIndex = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_registerToGroupMapping), (IntPtr)index);
                if (groupIndex != 0)
                {
                    groupIndex--;
                    _dirtyMask |= 1UL << groupIndex;
                }
            }
        }

        /// <summary>
        /// Forces a register group as dirty, by index.
        /// </summary>
        /// <param name="groupIndex">Index of the group to be dirtied</param>
        public void ForceDirty(int groupIndex)
        {
            if ((uint)groupIndex >= _callbacks.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(groupIndex));
            }

            _dirtyMask |= 1UL << groupIndex;
        }

        /// <summary>
        /// Forces all register groups as dirty, triggering a full update on the next call to <see cref="Update"/>.
        /// </summary>
        public void SetAllDirty()
        {
            Debug.Assert(_callbacks.Length <= sizeof(ulong) * 8);
            _dirtyMask = ulong.MaxValue >> ((sizeof(ulong) * 8) - _callbacks.Length);
        }

        /// <summary>
        /// Check if the given register group is dirty without clearing it.
        /// </summary>
        /// <param name="groupIndex">Index of the group to check</param>
        /// <returns>True if dirty, false otherwise</returns>
        public bool IsDirty(int groupIndex)
        {
            return (_dirtyMask & (1UL << groupIndex)) != 0;
        }

        /// <summary>
        /// Check all the groups specified by <paramref name="checkMask"/> for modification, and update if modified.
        /// </summary>
        /// <param name="checkMask">Mask, where each bit set corresponds to a group index that should be checked</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ulong checkMask)
        {
            ulong mask = _dirtyMask & checkMask;
            if (mask == 0)
            {
                return;
            }

            do
            {
                int groupIndex = BitOperations.TrailingZeroCount(mask);

                _callbacks[groupIndex]();

                mask &= ~(1UL << groupIndex);
            }
            while (mask != 0);

            _dirtyMask &= ~checkMask;
        }
    }
}
