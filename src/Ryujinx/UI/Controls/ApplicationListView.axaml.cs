using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.App.Common;
using System;

namespace Ryujinx.Ava.UI.Controls
{
    public partial class ApplicationListView : UserControl
    {
        public static readonly RoutedEvent<ApplicationOpenedEventArgs> ApplicationOpenedEvent =
            RoutedEvent.Register<ApplicationListView, ApplicationOpenedEventArgs>(nameof(ApplicationOpened), RoutingStrategies.Bubble);

        public event EventHandler<ApplicationOpenedEventArgs> ApplicationOpened
        {
            add { AddHandler(ApplicationOpenedEvent, value); }
            remove { RemoveHandler(ApplicationOpenedEvent, value); }
        }

        public ApplicationListView()
        {
            InitializeComponent();
        }

        public void GameList_DoubleTapped(object sender, TappedEventArgs args)
        {
            if (sender is ListBox listBox)
            {
                if (listBox.SelectedItem is ApplicationData selected)
                {
                    RaiseEvent(new ApplicationOpenedEventArgs(selected, ApplicationOpenedEvent));
                }
            }
        }

        public void GameList_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ListBox listBox)
            {
                (DataContext as MainWindowViewModel).ListSelectedApplication = listBox.SelectedItem as ApplicationData;
            }
        }

        private void SearchBox_OnKeyUp(object sender, KeyEventArgs args)
        {
            (DataContext as MainWindowViewModel).SearchText = (sender as TextBox).Text;
        }
    }
}
