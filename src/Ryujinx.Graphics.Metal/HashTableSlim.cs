using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Metal
{
    interface IRefEquatable<T>
    {
        bool Equals(ref T other);
    }

    class HashTableSlim<TKey, TValue> where TKey : IRefEquatable<TKey>
    {
        private const int TotalBuckets = 16; // Must be power of 2
        private const int TotalBucketsMask = TotalBuckets - 1;

        private struct Entry
        {
            public int Hash;
            public TKey Key;
            public TValue Value;
        }

        private struct Bucket
        {
            public int Length;
            public Entry[] Entries;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Span<Entry> AsSpan()
            {
                return Entries == null ? Span<Entry>.Empty : Entries.AsSpan(0, Length);
            }
        }

        private readonly Bucket[] _hashTable = new Bucket[TotalBuckets];

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (Bucket bucket in _hashTable)
                {
                    for (int i = 0; i < bucket.Length; i++)
                    {
                        yield return bucket.Entries[i].Key;
                    }
                }
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (Bucket bucket in _hashTable)
                {
                    for (int i = 0; i < bucket.Length; i++)
                    {
                        yield return bucket.Entries[i].Value;
                    }
                }
            }
        }

        public void Add(ref TKey key, TValue value)
        {
            var entry = new Entry
            {
                Hash = key.GetHashCode(),
                Key = key,
                Value = value,
            };

            int hashCode = key.GetHashCode();
            int bucketIndex = hashCode & TotalBucketsMask;

            ref var bucket = ref _hashTable[bucketIndex];
            if (bucket.Entries != null)
            {
                int index = bucket.Length;

                if (index >= bucket.Entries.Length)
                {
                    Array.Resize(ref bucket.Entries, index + 1);
                }

                bucket.Entries[index] = entry;
            }
            else
            {
                bucket.Entries = new[]
                {
                    entry,
                };
            }

            bucket.Length++;
        }

        public bool Remove(ref TKey key)
        {
            int hashCode = key.GetHashCode();

            ref var bucket = ref _hashTable[hashCode & TotalBucketsMask];
            var entries = bucket.AsSpan();
            for (int i = 0; i < entries.Length; i++)
            {
                ref var entry = ref entries[i];

                if (entry.Hash == hashCode && entry.Key.Equals(ref key))
                {
                    entries[(i + 1)..].CopyTo(entries[i..]);
                    bucket.Length--;

                    return true;
                }
            }

            return false;
        }

        public bool TryGetValue(ref TKey key, out TValue value)
        {
            int hashCode = key.GetHashCode();

            var entries = _hashTable[hashCode & TotalBucketsMask].AsSpan();
            for (int i = 0; i < entries.Length; i++)
            {
                ref var entry = ref entries[i];

                if (entry.Hash == hashCode && entry.Key.Equals(ref key))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
