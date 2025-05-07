using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Base.Services;

public abstract class TaskService : IService
{
    #region - Abstracts -
    protected abstract Task RunTask(CancellationToken token = default);
    protected abstract Task ExitTask(CancellationToken token = default);
    #endregion

    #region - Implementations for IService -
    public virtual async Task ExecuteAsync(CancellationToken token = default)
    {
        try
        {
            await RunTask(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public virtual async Task StopAsync(CancellationToken token = default)
    {
        try
        {
            await ExitTask(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
    #endregion
}
