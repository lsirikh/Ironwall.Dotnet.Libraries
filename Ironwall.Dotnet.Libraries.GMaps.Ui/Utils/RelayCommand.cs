using System;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/23/2025 9:47:10 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    // 매개변수를 받지 않는 경우의 생성자
    public RelayCommand(Action execute, Func<bool> canExecute = null)
        : this(execute != null ? (p => execute()) : (Action<object>)null,
               canExecute != null ? (p => canExecute()) : (Func<object, bool>)null)
    {
    }

    public bool CanExecute(object parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    public void Execute(object parameter)
    {
        _execute(parameter);
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// 비동기 명령을 위한 RelayCommand
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object, Task> _execute;
    private readonly Func<object, bool> _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    // 매개변수를 받지 않는 경우의 생성자
    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        : this(execute != null ? (p => execute()) : (Func<object, Task>)null,
               canExecute != null ? (p => canExecute()) : (Func<object, bool>)null)
    {
    }

    public bool CanExecute(object parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute(parameter));
    }

    public async void Execute(object parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}