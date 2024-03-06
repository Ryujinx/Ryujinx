using Ryujinx.Ava.UI.ViewModels;
using System.IO;

namespace Ryujinx.Ava.UI.Models
{
    public class DownloadableContentModel : BaseModel
    {
        private bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;

                OnPropertyChanged();
            }
        }

        public string TitleId { get; }
        public string ContainerPath { get; }
        public string FullPath { get; }

        public string FileName => Path.GetFileName(ContainerPath);

        public DownloadableContentModel(string titleId, string containerPath, string fullPath, bool enabled)
        {
            TitleId = titleId;
            ContainerPath = containerPath;
            FullPath = fullPath;
            Enabled = enabled;
        }
    }
}
