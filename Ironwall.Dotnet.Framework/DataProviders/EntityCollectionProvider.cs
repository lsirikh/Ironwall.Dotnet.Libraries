using Ironwall.Dotnet.Framework.Services;
using Ironwall.Dotnet.Libraries.Base.DataProviders;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;

namespace Ironwall.Dotnet.Framework.DataProviders;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/23/2025 7:16:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public abstract class EntityCollectionProvider<T> : ICollector<T>
{
    #region - Ctors -
    /// <summary>
    /// 동기화 방식을 활용한,
    /// Entity Insert Process
    /// https://docs.microsoft.com/ko-kr/dotnet/api/system.windows.data.bindingoperations.enablecollectionsynchronization?view=windowsdesktop-6.0
    /// </summary>
    /// <param name="collection">Generic IEnumerable 형태의 데이터를 받아서 , ObservableCollection 형태로 인스턴스화한다.</param>
    public EntityCollectionProvider(IEnumerable<T> collection)
    {
        CollectionEntity = new ObservableCollection<T>(collection);
        _locker = new object();
        _log = IoC.Get<ILogService>();
        BindingOperations.EnableCollectionSynchronization(CollectionEntity, _locker);
    }

    /// <summary>
    /// 해당 클래스를 상속받는 자녀클래스의 생성시, 동작하며,
    /// Generic 형태의 Type을 갖고 있기 때문에, 반드시 생성시
    /// Concrete Class의 데이터 타입이 있어야 함.
    /// </summary>
    public EntityCollectionProvider()
    {
        CollectionEntity = new ObservableCollection<T>();
        _locker = new object();
        _log = IoC.Get<ILogService>();
        //해당 메소드의 기능
        BindingOperations.EnableCollectionSynchronization(CollectionEntity, _locker);
    }
    #endregion

    #region - Implementations for ICollectionManager -
    public virtual void Add(T item)
    {
        try
        {
            Services.DispatcherService.Invoke((System.Action)(() =>
            {
                lock (_locker)
                {
                    CollectionEntity.Add(item);
                }
            }));
            //Debug.WriteLine($"Provider is Added : {result}");
        }
        catch (Exception ex)
        {
            _log.Error($"Provider Exception : {ex.Message}");
        }

    }

    public virtual void Remove(T item)
    {
        try
        {
            Services.DispatcherService.Invoke((System.Action)(() =>
            {
                var result = false;
                lock (_locker)
                {
                    result = CollectionEntity.Remove(item);
                }
            }));
        }
        catch (Exception ex)
        {
            _log.Error($"Provider Exception : {ex.Message}");
        }
    }

    public virtual void Clear()
    {
        try
        {
            Services.DispatcherService.Invoke((System.Action)(() =>
            {
                lock (_locker)
                {
                    CollectionEntity.Clear();
                }
            }));

        }
        catch (Exception ex)
        {
            _log.Error($"Provider Exception : {ex.Message}");
        }
    }


    /// Question!! SetItem을 활용하려면 현재 구조를 어떻게 바꾸면 좋을까....
    public virtual void MoveItem(int oldIndex, int newIndex)
    {
        try
        {
            Services.DispatcherService.Invoke((System.Action)(() =>
            {
                lock (_locker)
                {
                    CollectionEntity.Move(oldIndex, newIndex);
                }
            }));
        }
        catch (Exception ex)
        {
            _log.Error($"Provider Exception : {ex.Message}");
        }
    }


    public IEnumerator<T> GetEnumerator()
    {
        return CollectionEntity.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return CollectionEntity.GetEnumerator();
    }

    public int Count => CollectionEntity.Count;
    #endregion

    #region - Procedures -
    #endregion

    #region - Properties -
    public ObservableCollection<T> CollectionEntity { get; set; }
    #endregion

    #region - Attributes -
    protected readonly object _locker;
    protected readonly ILogService _log;
    #endregion
}