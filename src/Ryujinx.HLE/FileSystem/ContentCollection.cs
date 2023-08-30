using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.HLE.Loaders.Processes.Extensions;

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
        public ulong ApplicationTitleId => _cnmt.ApplicationTitleId;
        public ulong PatchTitleId => _cnmt.PatchTitleId;
        public TitleVersion RequiredSystemVersion => _cnmt.MinimumSystemVersion;
        public TitleVersion RequiredApplicationVersion => _cnmt.MinimumApplicationVersion;
        public byte[] Digest => _cnmt.Hash;

        public ulong ProgramBaseId => Id & ~0xFUL;
        public int ProgramIndex => (int)(Id & 0xF);
        public bool IsSystemTitle => _cnmt.Type < ContentMetaType.Application;

        public ContentCollection(IFileSystem pfs, Cnmt cnmt)
        {
            _pfs = pfs;
            _cnmt = cnmt;
        }

        public Nca GetNcaByType(KeySet keySet, ContentType type)
        {
            foreach (var entry in _cnmt.ContentEntries)
            {
                if (entry.Type == type)
                {
                    return _pfs.GetNca(keySet, $"/{entry.NcaId}");
                }
            }

            return null;
        }
    }
}
