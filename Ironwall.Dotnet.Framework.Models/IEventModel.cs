using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

public interface IEventModel
{
    int Id { get; set; }
    DateTime DateTime { get; set; }
}
