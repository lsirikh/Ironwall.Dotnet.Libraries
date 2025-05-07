using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Collections;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;

/****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 9/18/2023 11:49:28 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class EntityListProvider<T> : IEnumerable<T>, IEnumerable
{
    protected readonly object _locker;

    public int Count => CollectionEntity.Count;

    public List<T> CollectionEntity { get; set; }

    public event EventHandler? ListChanged;

    public EntityListProvider(IEnumerable<T> collection)
    {
        CollectionEntity = new List<T>(collection);
        _locker = new object();
    }

    public EntityListProvider()
    {
        CollectionEntity = new List<T>();
        _locker = new object();
    }

    public virtual void Add(T item)
    {
        try
        {
            lock (_locker)
            {
                CollectionEntity.Add(item);
                ListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception)
        {
        }
    }

    public virtual void Remove(T item)
    {
        try
        {
            lock (_locker)
            {
                CollectionEntity.Remove(item);
                ListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception)
        {
        }
    }

    public virtual void Clear()
    {
        try
        {
            lock (_locker)
            {
                CollectionEntity.Clear();
                ListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception)
        {
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
}

