using System;

namespace Ironwall.Dotnet.Libraries.Base.Services;

/// <summary>
/// 헤드리스 테스트용 최소 스텁 — 실제 LogEventArgs 는 Base/Services/LogService.cs 안에 있어 log4net 의존이 따라온다.
/// ILogService.cs(실코드 링크)의 event 시그니처만 만족시키는 목적이며 테스트 어셈블리 밖으로 나가지 않는다.
/// </summary>
public class LogEventArgs : EventArgs
{
    public LogEventArgs(string message) => Message = message;
    public string Message { get; }
}
