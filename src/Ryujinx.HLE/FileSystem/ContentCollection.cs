using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using System;

namespace Ryujinx.HLE.FileSystem
{
    /// <summary>
    /// Thin wrapper around <see cref="Cnmt"/>
    /// </summary>
    public class ContentCollection
    {
        private readonly IFileSystem _pfs;
        private readonly Cnmt _cnmt;

        public ulong Id => _cnmt.TitleId;
        public TitleVersion Version => _cnmt.TitleVersion;
        public ContentMetaType Type => _cnmt.Type;
        public ulong ApplicationId => _cnmt.ApplicationTitleId;
        public ulong PatchId => _cnmt.PatchTitleId;
        public TitleVersion RequiredSystemVersion => _cnmt.MinimumSystemVersion;
        public TitleVersion RequiredApplicationVersion => _cnmt.MinimumApplicationVersion;
        public byte[] Digest => _cnmt.Hash;

        public ulong ProgramBaseId => Id & ~0x1FFFUL;
        public int ProgramIndex => (int)(Id & 0xF);
        public bool IsSystemTitle => _cnmt.Type < ContentMetaType.Application;

        public ContentCollection(IFileSystem pfs, Cnmt cnmt)
        {
            _pfs = pfs;
            _cnmt = cnmt;
        }

        public Nca GetNcaByType(KeySet keySet, ContentType type, int idOffset = 0)
        {
            foreach (var entry in _cnmt.ContentEntries)
            {
                // TODO: Add check for IdOffset as soon as LibHac supports it:
                // && entry.IdOffset == idOffset
                if (entry.Type == type)
                {
                    string ncaId = BitConverter.ToString(entry.NcaId).Replace("-", null).ToLower();

                    return _pfs.GetNca(keySet, $"/{ncaId}.nca");
                }
            }

            return null;
        }
    }
}
