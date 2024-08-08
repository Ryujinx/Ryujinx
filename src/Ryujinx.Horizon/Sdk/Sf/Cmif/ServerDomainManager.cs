using Ryujinx.Horizon.Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.Sf.Cmif
{
    class ServerDomainManager
    {
        private class EntryManager
        {
            public class Entry
            {
                public int Id { get; }
                public Domain Owner { get; set; }
                public ServiceObjectHolder Obj { get; set; }
                public LinkedListNode<Entry> Node { get; set; }

                public Entry(int id)
                {
                    Id = id;
                }
            }

            private readonly LinkedList<Entry> _freeList;
            private readonly Entry[] _entries;

            public EntryManager(int count)
            {
                _freeList = new LinkedList<Entry>();
                _entries = new Entry[count];

                for (int i = 0; i < count; i++)
                {
                    _freeList.AddLast(_entries[i] = new Entry(i + 1));
                }
            }

            public Entry AllocateEntry()
            {
                lock (_freeList)
                {
                    if (_freeList.Count == 0)
                    {
                        return null;
                    }

                    var entry = _freeList.First.Value;
                    _freeList.RemoveFirst();
                    return entry;
                }
            }

            public void FreeEntry(Entry entry)
            {
                lock (_freeList)
                {
                    DebugUtil.Assert(entry.Owner == null);
                    DebugUtil.Assert(entry.Obj == null);
                    _freeList.AddFirst(entry);
                }
            }

            public Entry GetEntry(int id)
            {
                if (id == 0)
                {
                    return null;
                }

                int index = id - 1;

                if ((uint)index >= (uint)_entries.Length)
                {
                    return null;
                }

                return _entries[index];
            }
        }

        private class Domain : DomainServiceObject, IDisposable
        {
            private readonly ServerDomainManager _manager;
            private readonly LinkedList<EntryManager.Entry> _entries;

            public Domain(ServerDomainManager manager)
            {
                _manager = manager;
                _entries = new LinkedList<EntryManager.Entry>();
            }

            public override ServiceObjectHolder GetObject(int id)
            {
                var entry = _manager._entryManager.GetEntry(id);
                if (entry == null)
                {
                    return null;
                }

                lock (_manager._entryOwnerLock)
                {
                    if (entry.Owner != this)
                    {
                        return null;
                    }
                }

                return entry.Obj.Clone();
            }

            public override ServerDomainBase GetServerDomain()
            {
                return this;
            }

            public override void RegisterObject(int id, ServiceObjectHolder obj)
            {
                var entry = _manager._entryManager.GetEntry(id);
                DebugUtil.Assert(entry != null);

                lock (_manager._entryOwnerLock)
                {
                    DebugUtil.Assert(entry.Owner == null);
                    entry.Owner = this;
                    entry.Node = _entries.AddLast(entry);
                }

                entry.Obj = obj;
            }

            public override Result ReserveIds(Span<int> outIds)
            {
                for (int i = 0; i < outIds.Length; i++)
                {
                    var entry = _manager._entryManager.AllocateEntry();
                    if (entry == null)
                    {
                        return SfResult.OutOfDomainEntries;
                    }

                    DebugUtil.Assert(entry.Owner == null);

                    outIds[i] = entry.Id;
                }

                return Result.Success;
            }

            public override ServiceObjectHolder UnregisterObject(int id)
            {
                var entry = _manager._entryManager.GetEntry(id);
                if (entry == null)
                {
                    return null;
                }

                ServiceObjectHolder obj;

                lock (_manager._entryOwnerLock)
                {
                    if (entry.Owner != this)
                    {
                        return null;
                    }

                    entry.Owner = null;
                    obj = entry.Obj;

                    if (obj.ServiceObject is IDisposable disposableObj)
                    {
                        disposableObj.Dispose();
                    }

                    entry.Obj = null;
                    _entries.Remove(entry.Node);
                    entry.Node = null;
                }

                _manager._entryManager.FreeEntry(entry);

                return obj;
            }

            public override void UnreserveIds(ReadOnlySpan<int> ids)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    var entry = _manager._entryManager.GetEntry(ids[i]);

                    DebugUtil.Assert(entry != null);
                    DebugUtil.Assert(entry.Owner == null);

                    _manager._entryManager.FreeEntry(entry);
                }
            }

            public void Dispose()
            {
                foreach (var entry in _entries)
                {
                    if (entry.Obj.ServiceObject is IDisposable disposableObj)
                    {
                        disposableObj.Dispose();
                    }
                }

                _manager.FreeDomain(this);
            }
        }

        private readonly EntryManager _entryManager;
        private readonly Lock _entryOwnerLock;
        private readonly HashSet<Domain> _domains;
        private readonly int _maxDomains;

        public ServerDomainManager(int entryCount, int maxDomains)
        {
            _entryManager = new EntryManager(entryCount);
            _entryOwnerLock = new Lock();
            _domains = new HashSet<Domain>();
            _maxDomains = maxDomains;
        }

        public DomainServiceObject AllocateDomainServiceObject()
        {
            lock (_domains)
            {
                if (_domains.Count == _maxDomains)
                {
                    return null;
                }

                var domain = new Domain(this);
                _domains.Add(domain);
                return domain;
            }
        }

        public static void DestroyDomainServiceObject(DomainServiceObject obj)
        {
            ((Domain)obj).Dispose();
        }

        private void FreeDomain(Domain domain)
        {
            lock (_domains)
            {
                _domains.Remove(domain);
            }
        }
    }
}
