using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Threading;
using System.Timers;

namespace Ironwall.Dotnet.Framework.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/8/2025 11:10:24 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class TokenGenerator
{
    #region - Ctors -
    public TokenGenerator()
    {
        Init();
        Run();
    }
    public TokenGenerator(ILogService log): this()
    {
        _log = log;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    

    public void Init()
    {
        timer.Elapsed += new ElapsedEventHandler(TokenTimeout);
    }

    public void SetTimerEnable(bool value)
    {
        timer.Enabled = value;
    }

    public void Generate()
    {
        CreateToken();
        SetExpire();
        _log?.Info($"============Token was generated at {Expire.ToString(format:"yyyy-MM-dd HH:mm:ss.ff")}============");
    }

    public void CreateToken()
    {
        byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
        byte[] key = Guid.NewGuid().ToByteArray();
        string token = Convert.ToBase64String(time.Concat(key).ToArray());
        Token = token;
    }

    private void SetExpire()
    {
        Expire = DateTimeHelper.GetCurrentTimeWithoutMS() + TimeSpan.FromMinutes(Session);
    }

    public bool SetSession(int time = Period)
    {
        if (time == 0)
            return false;
        //Input Value will be minutes
        timer.Interval = TimeSpan.FromMinutes(time).TotalMilliseconds; ;
        Session = time;

        return true;
    }

    public void Run()
    {
        SetSession();
        SetTimerEnable(true);
        Generate();
    }
    private void TokenTimeout(object? sender, ElapsedEventArgs e)
    {
        Generate();
        TokenTimeoutEvent?.Invoke();
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public string? Token
    {
        get { return _token; }
        private set
        {
            _token = value;
        }
    }

    public DateTime Expire
    {
        get { return _expire; }
        private set
        {
            _expire = value;
        }
    }

    public int Session
    {
        get { return _session; }
        private set
        {
            _session = value;
        }
    }
    #endregion
    #region - Attributes -
    private string? _token;
    private DateTime _expire;
    private int _session;
    public const int Period = 60; //Expire Duration 1 Hour By Default
    private System.Timers.Timer timer = new System.Timers.Timer();
    private ILogService? _log;

    public delegate void TokenTimeoutHandler();
    public event TokenTimeoutHandler? TokenTimeoutEvent;
    #endregion
}