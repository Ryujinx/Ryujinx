using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    abstract class StateCache<T, TDescriptor, THash> : IDisposable where T : IDisposable
    {
        private readonly Dictionary<THash, T> _cache = new();

        protected abstract THash GetHash(TDescriptor descriptor);

        protected abstract T CreateValue(TDescriptor descriptor);

        public void Dispose()
        {
            foreach (T value in _cache.Values)
            {
                value.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public T GetOrCreate(TDescriptor descriptor)
        {
            var hash = GetHash(descriptor);
            if (_cache.TryGetValue(hash, out T value))
            {
                return value;
            }
            else
            {
                var newValue = CreateValue(descriptor);
                _cache.Add(hash, newValue);

                return newValue;
            }
        }
    }
}
