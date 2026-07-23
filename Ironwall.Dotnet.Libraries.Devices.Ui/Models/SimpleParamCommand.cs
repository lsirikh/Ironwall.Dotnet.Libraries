using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Models;
/****************************************************************************
   Purpose      : 파라미터형 비동기 ICommand (Detection_Signal_History FR-11)
                  Events.Ui SimpleParamCommand 미러 — 재진입 방지 포함.
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class SimpleParamCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private bool _isExecuting;

    public SimpleParamCommand(Func<object?, Task> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(parameter); }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
