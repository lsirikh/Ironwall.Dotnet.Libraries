using System;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Streaming.Commands;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:16:58 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    // CommandManager와 연결
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// 파라미터를 받는 제네릭 RelayCommand
/// </summary>
/// <typeparam name="T">커맨드 파라미터 타입</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Predicate<T>? _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null)
            return true;

        // null 처리
        if (parameter == null && typeof(T).IsValueType)
            return false;

        // 타입 체크 및 변환
        if (parameter is T typedParameter)
            return _canExecute(typedParameter);

        // null 허용 타입 처리
        if (parameter == null && !typeof(T).IsValueType)
            return _canExecute(default(T));

        return false;
    }

    public void Execute(object? parameter)
    {
        // 타입 안전한 실행
        if (parameter is T typedParameter)
        {
            _execute(typedParameter);
        }
        else if (parameter == null && !typeof(T).IsValueType)
        {
            _execute(default(T));
        }
    }

    /// <summary>
    /// CanExecute 상태 강제 업데이트
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
