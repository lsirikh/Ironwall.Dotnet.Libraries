using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Base.DataProviders;

public interface ICollector<T> : IEnumerable<T>
{
    void Add(T item);
    void Remove(T item);
    int Count { get; }
    void Clear();
    void MoveItem(int oldIndex, int newIndex);
}
