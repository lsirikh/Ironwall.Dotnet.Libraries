using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using log4net;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Base.Services;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 10/26/2023 2:47:25 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class LogService : ILogService
{

    #region - Ctors -
    public LogService()
    {
        Log4NetSettings();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    private void Log4NetSettings()
    {
        Hierarchy hierarchy = (Hierarchy)log4net.LogManager.GetRepository();
        // 로그 출력 형식 설정
        var patternLayout = new PatternLayout
        {
            ConversionPattern = "%date [%thread] %-5level %logger - %message%newline"
        };
        patternLayout.ActivateOptions();

        // RollingFileAppender 설정 (파일 출력)
        var roller = new RollingFileAppender
        {
            AppendToFile = true,
            File = @"Logs\log-",
            DatePattern = "yyyy-MM-dd'.txt'",
            StaticLogFileName = false,
            Layout = patternLayout,
            MaxSizeRollBackups = 5,
            MaximumFileSize = "50MB",
            RollingStyle = RollingFileAppender.RollingMode.Composite
        };
        roller.ActivateOptions();

        // DebugAppender 설정 (Output 창 출력)
#if DEBUG
        var debugAppender = new DebugAppender
        {
            Layout = patternLayout
        };
        debugAppender.ActivateOptions();
        hierarchy.Root.AddAppender(debugAppender); // Debug 모드에서만 활성화
#endif

        // Appender 추가 및 기본 설정
        hierarchy.Root.AddAppender(roller);
        hierarchy.Root.Level = Level.All;
        hierarchy.Configured = true;

    }

    public void Info(string msg,
                    [CallerMemberName] string memberName = "",
                    [CallerFilePath] string filePath = "",
                    [CallerLineNumber] int lineNumber = 0)
    {
        // 파일 경로에서 파일명만 추출
        var fileName = System.IO.Path.GetFileName(filePath);

        // 호출자 정보 포함한 메시지 생성
        var detailedMsg = $"{msg} (at {fileName}:{lineNumber} in {memberName})";

        // Log 호출
        Log(detailedMsg, typeof(LogService), Level.Info);
    }
    

    public void Warning(string msg,
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string filePath = "",
                        [CallerLineNumber] int lineNumber = 0)
    {
        // 파일 경로에서 파일명만 추출
        var fileName = System.IO.Path.GetFileName(filePath);

        // 호출자 정보 포함한 메시지 생성
        var detailedMsg = $"{msg} (at {fileName}:{lineNumber} in {memberName})";

        // Log 호출
        Log(detailedMsg, typeof(LogService), Level.Warn);
    }

    public void Error(string msg,
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string filePath = "",
                        [CallerLineNumber] int lineNumber = 0)
    {
        // 파일 경로에서 파일명만 추출
        var fileName = System.IO.Path.GetFileName(filePath);

        // 호출자 정보 포함한 메시지 생성
        var detailedMsg = $"{msg} (at {fileName}:{lineNumber} in {memberName})";

        // Log 호출
        Log(detailedMsg, typeof(LogService), Level.Warn);
    }

    private void Log(string msg, Type? type = default, Level? level = null, bool debug = false)
    {
        if (debug)
            Debug.WriteLine(msg);

        type ??= typeof(LogService);
        var effectiveLevel = level ?? Level.Info;

        // 호출자의 Logger를 동적으로 생성
        var dynamicLogger = LogManager.GetLogger(type);
        dynamicLogger.Logger.Log(type, level, msg, null);

        OnLogEvent(new LogEventArgs(msg, level: effectiveLevel));
    }

    private async void OnLogEvent(LogEventArgs e)
    {

        await OnLogEventAsync(e);
    }

    private async Task OnLogEventAsync(LogEventArgs e)
    {
        if (LogEvent != null)
        {
            var eventHandlers = LogEvent.GetInvocationList().Cast<EventHandler<LogEventArgs>>();

            foreach (var handler in eventHandlers)
            {
                try
                {
                    // 비동기로 각 이벤트 핸들러 실행
                    await Task.Run(() => handler.Invoke(this, e));
                }
                catch (TaskCanceledException)
                {
                    // 작업 취소 처리
                    _iLog?.Warn($"Log event handler was canceled: {handler.Method.Name}");
                }
                catch (Exception ex)
                {
                    // 기타 예외 처리
                    _iLog?.Error($"Exception in log event handler ({handler.Method.Name}): {ex.Message}");
                }
            }
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    public event EventHandler<LogEventArgs>? LogEvent;
    private readonly ILog _iLog = LogManager.GetLogger(typeof(LogService));
    #endregion
}

public class LogEventArgs : EventArgs
{
    public LogEventArgs(string message, Level level)
    {
        Message = message;
        LogLevel = level;
    }

    public string Message { get; }
    public Level LogLevel { get; }
}
