using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;

public interface ICollector<T> : IEnumerable<T>
{
    void Add(T item);
    void AddRange(IEnumerable<T> items);
    void Remove(T item);
    void RemoveRange(IEnumerable<T> items);
    int Count { get; }
    void Clear();
    void MoveItem(int oldIndex, int newIndex);
}
