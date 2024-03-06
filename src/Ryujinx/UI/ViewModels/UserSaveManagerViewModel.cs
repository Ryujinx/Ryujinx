using DynamicData;
using DynamicData.Binding;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class UserSaveManagerViewModel : BaseModel
    {
        private int _sortIndex;
        private int _orderIndex;
        private string _search;
        private ObservableCollection<SaveModel> _saves = new();
        private ObservableCollection<SaveModel> _views = new();
        private readonly AccountManager _accountManager;

        public string SaveManagerHeading => LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.SaveManagerHeading, _accountManager.LastOpenedUser.Name, _accountManager.LastOpenedUser.UserId);

        public int SortIndex
        {
            get => _sortIndex;
            set
            {
                _sortIndex = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public int OrderIndex
        {
            get => _orderIndex;
            set
            {
                _orderIndex = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public string Search
        {
            get => _search;
            set
            {
                _search = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public ObservableCollection<SaveModel> Saves
        {
            get => _saves;
            set
            {
                _saves = value;
                OnPropertyChanged();
                Sort();
            }
        }

        public ObservableCollection<SaveModel> Views
        {
            get => _views;
            set
            {
                _views = value;
                OnPropertyChanged();
            }
        }

        public UserSaveManagerViewModel(AccountManager accountManager)
        {
            _accountManager = accountManager;
        }

        public void Sort()
        {
            Saves.AsObservableChangeSet()
                .Filter(Filter)
                .Sort(GetComparer())
                .Bind(out var view).AsObservableList();

            _views.Clear();
            _views.AddRange(view);
            OnPropertyChanged(nameof(Views));
        }

        private bool Filter(object arg)
        {
            if (arg is SaveModel save)
            {
                return string.IsNullOrWhiteSpace(_search) || save.Title.ToLower().Contains(_search.ToLower());
            }

            return false;
        }

        private IComparer<SaveModel> GetComparer()
        {
            return SortIndex switch
            {
                0 => OrderIndex == 0
                    ? SortExpressionComparer<SaveModel>.Ascending(save => save.Title)
                    : SortExpressionComparer<SaveModel>.Descending(save => save.Title),
                1 => OrderIndex == 0
                    ? SortExpressionComparer<SaveModel>.Ascending(save => save.Size)
                    : SortExpressionComparer<SaveModel>.Descending(save => save.Size),
                _ => null,
            };
        }
    }
}
