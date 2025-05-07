using System;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Base.Services;

public interface ILogService
{
    void Error(string msg, 
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string filePath = "",
                [CallerLineNumber] int lineNumber = 0);
    void Info(string msg,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string filePath = "",
                [CallerLineNumber] int lineNumber = 0);
    void Warning(string msg,
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string filePath = "",
                [CallerLineNumber] int lineNumber = 0);

    event EventHandler<LogEventArgs> LogEvent;
}