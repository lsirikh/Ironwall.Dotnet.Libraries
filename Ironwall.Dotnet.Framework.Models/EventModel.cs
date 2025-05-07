using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

public abstract class EventModel : IEventModel
{
    #region - Implementations for IEventModel - 
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    #endregion
}
