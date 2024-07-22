using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Device
{
    public class DeviceState<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TState> : IDeviceState where TState : unmanaged
    {
        private const int RegisterSize = sizeof(int);

        public TState State;

        private static uint Size => (uint)(Unsafe.SizeOf<TState>() + RegisterSize - 1) / RegisterSize;

        private readonly Func<int>[] _readCallbacks;
        private readonly Action<int>[] _writeCallbacks;

        private readonly Dictionary<uint, string> _fieldNamesForDebug;
        private readonly Action<string> _debugLogCallback;

        public DeviceState(IReadOnlyDictionary<string, RwCallback> callbacks = null, Action<string> debugLogCallback = null)
        {
            _readCallbacks = new Func<int>[Size];
            _writeCallbacks = new Action<int>[Size];

            if (debugLogCallback != null)
            {
                _fieldNamesForDebug = new Dictionary<uint, string>();
                _debugLogCallback = debugLogCallback;
            }

            var fields = typeof(TState).GetFields();
            int offset = 0;

            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                var field = fields[fieldIndex];

                var currentFieldOffset = (int)Marshal.OffsetOf<TState>(field.Name);
                var nextFieldOffset = fieldIndex + 1 == fields.Length ? Unsafe.SizeOf<TState>() : (int)Marshal.OffsetOf<TState>(fields[fieldIndex + 1].Name);

                int sizeOfField = nextFieldOffset - currentFieldOffset;

                for (int i = 0; i < ((sizeOfField + 3) & ~3); i += 4)
                {
                    int index = (offset + i) / RegisterSize;

                    if (callbacks != null && callbacks.TryGetValue(field.Name, out var cb))
                    {
                        if (cb.Read != null)
                        {
                            _readCallbacks[index] = cb.Read;
                        }

                        if (cb.Write != null)
                        {
                            _writeCallbacks[index] = cb.Write;
                        }
                    }
                }

                if (debugLogCallback != null)
                {
                    _fieldNamesForDebug.Add((uint)offset, field.Name);
                }

                offset += sizeOfField;
            }

            Debug.Assert(offset == Unsafe.SizeOf<TState>());
        }

        public int Read(int offset)
        {
            uint index = (uint)offset / RegisterSize;

            if (index < Size)
            {
                uint alignedOffset = index * RegisterSize;

                var readCallback = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_readCallbacks), (IntPtr)index);
                if (readCallback != null)
                {
                    return readCallback();
                }
                else
                {
                    return GetRefUnchecked<int>(alignedOffset);
                }
            }

            return 0;
        }

        public void Write(int offset, int data)
        {
            uint index = (uint)offset / RegisterSize;

            if (index < Size)
            {
                uint alignedOffset = index * RegisterSize;
                DebugWrite(alignedOffset, data);

                GetRefIntAlignedUncheck(index) = data;

                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_writeCallbacks), (IntPtr)index)?.Invoke(data);
            }
        }

        public void WriteWithRedundancyCheck(int offset, int data, out bool changed)
        {
            uint index = (uint)offset / RegisterSize;

            if (index < Size)
            {
                uint alignedOffset = index * RegisterSize;
                DebugWrite(alignedOffset, data);

                ref var storage = ref GetRefIntAlignedUncheck(index);
                changed = storage != data;
                storage = data;

                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_writeCallbacks), (IntPtr)index)?.Invoke(data);
            }
            else
            {
                changed = false;
            }
        }

        [Conditional("DEBUG")]
        private void DebugWrite(uint alignedOffset, int data)
        {
            if (_fieldNamesForDebug != null && _fieldNamesForDebug.TryGetValue(alignedOffset, out string fieldName))
            {
                _debugLogCallback($"{typeof(TState).Name}.{fieldName} = 0x{data:X}");
            }
        }

        public ref T GetRef<T>(int offset) where T : unmanaged
        {
            if ((uint)(offset + Unsafe.SizeOf<T>()) > Unsafe.SizeOf<TState>())
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            return ref GetRefUnchecked<T>((uint)offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref T GetRefUnchecked<T>(uint offset) where T : unmanaged
        {
            return ref Unsafe.As<TState, T>(ref Unsafe.AddByteOffset(ref State, (IntPtr)offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref int GetRefIntAlignedUncheck(ulong index)
        {
            return ref Unsafe.Add(ref Unsafe.As<TState, int>(ref State), (IntPtr)index);
        }
    }
}
