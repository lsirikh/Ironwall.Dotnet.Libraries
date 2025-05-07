using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

public abstract class EventMessageModel<T> : IEventMessageModel<T> where T : notnull
{
    public T Value
    {
        get => content;
        set => content = value;
    }

    public string? Command
    {
        get => command;
        set => command = value;
    }

    public virtual int CommandId
    {
        get => commandId;
        set => commandId = value;
    }

    #region - Attributes -
    protected int commandId;
    private T content = default!;
    private string? command;
    #endregion
}
