using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;

/// <summary>
/// 비동기 Action을 ICommand로 래핑하는 간단한 커맨드 구현.
/// <para>DataGridScrollEndBehavior 등 XAML Behavior에서 ViewModel 메서드 바인딩에 사용</para>
/// </summary>
public class SimpleCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting;

    public SimpleCommand(Func<Task> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
        }
    }
}
