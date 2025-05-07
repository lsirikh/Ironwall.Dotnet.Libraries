using System.Timers;

namespace Ironwall.Dotnet.Libraries.Base.Services;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 2/29/2024 10:37:00 AM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public abstract class TimerService: IDisposable
{

    #region - Ctors -
    #endregion
    #region - Implementation of Interface -
    public void InitTimer(int interval = 1000)
    {
        if (_timer != null)
            _timer.Close();

        _timer = new System.Timers.Timer();
        _timer.Elapsed += Tick;

        SetTimerInterval(interval);
    }

    public void SetTimerEnable(bool value)
    {
        _timer.Enabled = value;
    }
    public bool GetTimerEnable() => _timer.Enabled;
    public void SetTimerInterval(int interval)
    {
        _timer.Interval = interval;
    }
    public double GetTimerInterval() => _timer.Interval;
    public void SetTimerStart()
    {
        SetTimerEnable(true);
        _timer.Start();
    }
    public void SetTimerStop()
    {
        _timer.Stop();
    }
    public void DisposeTimer()
    {
        if (_timer == null) return;

        _timer.Elapsed -= Tick;

        if (GetTimerEnable())
            _timer.Stop();

        _timer.Close();
        _timer.Dispose();
        _disposed = true;
    }
    #endregion
    #region - Overrides -
    protected abstract void Tick(object? sender, ElapsedEventArgs e);

    public void Dispose()
    {
        if (_disposed) return;

        DisposeTimer();
        _disposed = true;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private System.Timers.Timer _timer = new System.Timers.Timer();
    private bool _disposed;
    #endregion
}
