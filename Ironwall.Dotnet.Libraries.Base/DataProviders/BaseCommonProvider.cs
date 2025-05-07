using Ironwall.Dotnet.Libraries.Base.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/23/2025 7:15:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseCommonProvider<T> : EntityCollectionProvider<T>
{
    #region - Ctors -
    protected BaseCommonProvider(ILogService log) : base(log)
    {
        ClassName = this.GetType().Name;
    }
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
            _log?.Error($"Raised Exception : {ex.Message}");
        }
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public abstract Task<bool> Finished();
    public abstract Task<bool> InsertedItem(T item);
    public abstract Task<bool> UpdatedItem(T item);
    public abstract Task<bool> DeletedItem(T item);
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public string ClassName { get; set; }
    #endregion
    #region - Attributes -
    public abstract event RefreshItems Refresh;
    public abstract event Insert Inserted;
    public abstract event Update Updated;
    public abstract event Delete Deleted;

    public delegate Task<bool> RefreshItems();
    public delegate Task<bool> Update(T item);
    public delegate Task<bool> Insert(T item);
    public delegate Task<bool> Delete(T item);
    //protected ILogService _log;
    #endregion
}