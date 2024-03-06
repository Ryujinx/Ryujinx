using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Ryujinx.Ava.UI.Helpers
{
    public sealed class MiniCommand<T> : MiniCommand, ICommand
    {
        private readonly Action<T> _callback;
        private bool _busy;
        private readonly Func<T, Task> _asyncCallback;

        public MiniCommand(Action<T> callback)
        {
            _callback = callback;
        }

        public MiniCommand(Func<T, Task> callback)
        {
            _asyncCallback = callback;
        }

        private bool Busy
        {
            get => _busy;
            set
            {
                _busy = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override event EventHandler CanExecuteChanged;
        public override bool CanExecute(object parameter) => !_busy;

        public override async void Execute(object parameter)
        {
            if (Busy)
            {
                return;
            }
            try
            {
                Busy = true;
                if (_callback != null)
                {
                    _callback((T)parameter);
                }
                else
                {
                    await _asyncCallback((T)parameter);
                }
            }
            finally
            {
                Busy = false;
            }
        }
    }

    public abstract class MiniCommand : ICommand
    {
        public static MiniCommand Create(Action callback) => new MiniCommand<object>(_ => callback());
        public static MiniCommand Create<TArg>(Action<TArg> callback) => new MiniCommand<TArg>(callback);
        public static MiniCommand CreateFromTask(Func<Task> callback) => new MiniCommand<object>(_ => callback());

        public abstract bool CanExecute(object parameter);
        public abstract void Execute(object parameter);
        public abstract event EventHandler CanExecuteChanged;
    }
}
