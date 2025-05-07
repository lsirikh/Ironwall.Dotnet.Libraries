using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Helpers;

public static class DebugHelper
{

    public static string GetCallerInfo(object sender)
    {

        // 이전 함수명 
        string? prevFuncName = new StackFrame(1, true)?.GetMethod()?.Name;

        // 이전 Class명
        string? prevClassName = new StackTrace()?.GetFrame(1)?.GetMethod()?.ReflectedType?.Name;
        
        return prevFuncName + " - " + prevClassName; ;

    }

}
