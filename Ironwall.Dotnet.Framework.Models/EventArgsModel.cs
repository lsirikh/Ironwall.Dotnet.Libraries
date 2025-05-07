using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

public sealed class EventArgsModel<T> : EventArgs
{
    #region - Abstracts -
    public T? Value { get; set; }
    #endregion
}
