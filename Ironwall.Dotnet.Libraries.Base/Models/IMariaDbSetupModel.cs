using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Base.Models;
public interface IMariaDbSetupModel
{
    string IpDbServer { get; set; }
    int PortDbServer { get; set; }
    string DbDatabase { get; set; } 
    string UidDbServer { get; set; }
    string PasswordDbServer { get; set; }
}
