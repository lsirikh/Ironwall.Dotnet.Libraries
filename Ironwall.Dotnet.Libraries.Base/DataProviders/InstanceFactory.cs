using System;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;

public static class InstanceFactory
{
    #region - Static Procedures -
    public static T? Build<T>() where T : class, new()
    {
        var instance = Activator.CreateInstance(typeof(T)) as T; // 'as'를 사용하여 null 안전하게 처리
        return instance;
    }
    #endregion
}
