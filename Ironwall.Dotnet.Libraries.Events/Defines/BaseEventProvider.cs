using Ironwall.Dotnet.Libraries.Base.DataProviders;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Specialized;

namespace Ironwall.Dotnet.Libraries.Events.Defines;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 3:06:41 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseEventProdiver<T> : BaseProvider<T>, ILoadable where T : IBaseEventModel
{

    #region - Ctors -
    protected BaseEventProdiver(ILogService log, EventProvider provider) : base(log)
    {
        _provider = provider;
        _provider.CollectionEntity.CollectionChanged += CollectionEntity_CollectionChanged;
    }
    #endregion
    #region - Implementation of Interface -
    public Task<bool> Initialize(CancellationToken token = default)
    {
        try
        {
            Clear();
            foreach (var item in _provider.OfType<T>().ToList())
            {
                Add(item);
            }

            return Task.FromResult(true);
        }
        catch (System.Exception ex)
        {
            _log?.Error(ex.Message);
            return Task.FromResult(false);
        }
    }

    public void Uninitialize()
    {
        _provider.CollectionEntity.CollectionChanged -= CollectionEntity_CollectionChanged;
        Clear();
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // New items added
                if (e.NewItems == null) return;
                foreach (T newItem in e.NewItems.OfType<T>().ToList())
                {
                    Add(newItem);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (T oldItem in e.OldItems.OfType<T>().ToList())
                {
                    var instance = CollectionEntity.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    if (instance != null)
                        Remove(instance);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                int index = 0;
                if (e.OldItems == null) return;
                foreach (T oldItem in e.OldItems.OfType<T>().ToList())
                {
                    var instance = CollectionEntity.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    if (instance != null)
                    {
                        index = CollectionEntity.IndexOf(instance);
                        Remove(instance);
                    }
                }

                if (e.NewItems == null) return;
                foreach (T newItem in e.NewItems.OfType<T>().ToList())
                {
                    Add(newItem, index);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                CollectionEntity.Clear();
                foreach (T newItem in _provider.OfType<T>().ToList())
                {
                    Add(newItem);
                }
                break;
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private EventProvider _provider;
    #endregion

}