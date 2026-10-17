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

        // BufferingForwardingAppender로 래핑 — 호출 스레드 디스크 I/O 블로킹 제거
        var bufferingAppender = new BufferingForwardingAppender();
        bufferingAppender.AddAppender(roller);
        bufferingAppender.BufferSize = 50;
        bufferingAppender.Lossy = false;
        bufferingAppender.Fix = FixFlags.All;
        bufferingAppender.Evaluator = new LevelEvaluator(Level.Error); // ERROR 즉시 flush
        bufferingAppender.ActivateOptions();

        // Appender 추가 및 기본 설정
        hierarchy.Root.AddAppender(bufferingAppender);
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

        // [VF-11] Level.Warn → Level.Error.
        //   이전에는 Error() 가 Warning() 과 동일하게 Warn 으로 방출돼 두 가지가 동시에 깨져 있었다:
        //   ① 코드 어디서도 Level.Error 를 내보내지 않으므로 위 BufferingForwardingAppender 의
        //      Evaluator = new LevelEvaluator(Level.Error)(= "ERROR 즉시 flush") 조건이 영원히 성립하지 않았다
        //      → 실패 라인이 버퍼 50줄이 찰 때까지 디스크에 안 남고, 크래시하면 통째로 유실됐다.
        //   ② 로그에서 ERROR 로 검색하면 0건이라 "에러가 없다"고 오판하게 만들었다
        //      (실증: 이벤트 대시보드 504 실패 3줄이 전부 WARN 으로 기록됨 — log-2026-08-05.txt).
        Log(detailedMsg, typeof(LogService), Level.Error);
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

    private void OnLogEvent(LogEventArgs e)
    {
        LogEvent?.Invoke(this, e);
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
