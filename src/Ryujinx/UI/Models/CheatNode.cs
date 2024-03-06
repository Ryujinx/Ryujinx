using Ryujinx.Ava.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Ryujinx.Ava.UI.Models
{
    public class CheatNode : BaseModel
    {
        private bool _isEnabled = false;
        public ObservableCollection<CheatNode> SubNodes { get; } = new();
        public string CleanName => Name[1..^7];
        public string BuildIdKey => $"{BuildId}-{Name}";
        public bool IsRootNode { get; }
        public string Name { get; }
        public string BuildId { get; }
        public string Path { get; }
        public bool IsEnabled
        {
            get
            {
                if (SubNodes.Count > 0)
                {
                    return SubNodes.ToList().TrueForAll(x => x.IsEnabled);
                }

                return _isEnabled;
            }
            set
            {
                foreach (var cheat in SubNodes)
                {
                    cheat.IsEnabled = value;
                    cheat.OnPropertyChanged();
                }

                _isEnabled = value;
            }
        }

        public CheatNode(string name, string buildId, string path, bool isRootNode, bool isEnabled = false)
        {
            Name = name;
            BuildId = buildId;
            Path = path;
            IsEnabled = isEnabled;
            IsRootNode = isRootNode;

            SubNodes.CollectionChanged += CheatsList_CollectionChanged;
        }

        private void CheatsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
