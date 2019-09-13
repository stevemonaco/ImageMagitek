using System;
using System.Diagnostics;
using System.Windows.Input;

namespace TileShop.WPF.Helpers
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// Implementation from Maple https://github.com/Insire/Maple/blob/ba074ffa50796697cb47e5183f1d4f09c3e06d2f/src/Maple.Core/Commands/RelayCommand.cs
    /// </remarks>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute = null;
        private readonly Func<bool> _canExecute = null;

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="methodToExecute">The method to execute.</param>
        /// <exception cref="System.ArgumentNullException">methodToExecute</exception>
        public RelayCommand(Action methodToExecute)
        {
            _execute = methodToExecute ?? throw new ArgumentNullException(nameof(methodToExecute), $"{nameof(methodToExecute)}");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="methodToExecute">The method to execute.</param>
        /// <param name="canExecuteEvaluator">The can execute evaluator.</param>
        /// <exception cref="System.ArgumentNullException">canExecuteEvaluator</exception>
        public RelayCommand(Action methodToExecute, Func<bool> canExecuteEvaluator)
            : this(methodToExecute)
        {
            _canExecute = canExecuteEvaluator ?? throw new ArgumentNullException(nameof(canExecuteEvaluator), $"{nameof(canExecuteEvaluator)}");
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute.Invoke();
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        [DebuggerStepThrough]
        public void Execute(object parameter)
        {
            _execute();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute = null;
        private readonly Predicate<T> _canExecute = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        public RelayCommand(Action<T> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute), $"{nameof(execute)}");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        /// <exception cref="System.ArgumentNullException">execute</exception>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
            : this(execute)
        {
            _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute), $"{nameof(canExecute)}");
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;

            if (parameter == null && typeof(T).IsValueType)
                return _canExecute.Invoke(default(T));

            if (parameter == null || parameter is T)
                return (_canExecute.Invoke((T)parameter));

            return false;
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        [DebuggerStepThrough]
        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}
