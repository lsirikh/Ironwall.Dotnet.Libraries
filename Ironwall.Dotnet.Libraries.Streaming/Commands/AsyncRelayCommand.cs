using System;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Streaming.Commands;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:16:37 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    // Hybrid approach: CommandManager + 개별 이벤트
    public event EventHandler? CanExecuteChanged
    {
        add
        {
            CommandManager.RequerySuggested += value;
            _canExecuteChanged += value;
        }
        remove
        {
            CommandManager.RequerySuggested -= value;
            _canExecuteChanged -= value;
        }
    }

    private event EventHandler? _canExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        CommandManager.InvalidateRequerySuggested();
    }

    public bool CanExecute(object? parameter)
    {
        try
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }
        catch
        {
            return false;
        }
    }

    public async void Execute(object? parameter)
    {
        if (_isExecuting)
            return;

        _isExecuting = true;

        try
        {
            RaiseCanExecuteChanged();
            await _execute();
        }
        catch (Exception ex)
        {
            // 로그 처리 (옵션)
            System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand Execute failed: {ex.Message}");
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
}