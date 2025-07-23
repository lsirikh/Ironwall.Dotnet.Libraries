using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/23/2025 7:16:25 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseProvider<T> : EntityCollectionProvider<T>
{
    #region - Ctors -
    protected BaseProvider() {}

    protected BaseProvider(ILogService log) =>  _log = log;

    #endregion
    #region - Implementation of Interface -
    public virtual void Add(T item, int index)
    {
        try
        {
            DispatcherService.Invoke((System.Action)(() =>
            {
                lock (_locker)
                {
                    CollectionEntity.Insert(index, item);
                }
            }));
        }
        catch (Exception ex)
        {
            _log?.Error($"Failed to add an item for the reason({ex.Message}).");
        }
    }
    #endregion
    #region - Overrides -
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
    protected ILogService? _log;
    #endregion
}