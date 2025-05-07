using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class ConnectionEventMapper
        : MetaEventMapper
        , IConnectionEventMapper
    {

        public ConnectionEventMapper()
        {

        }

        public ConnectionEventMapper(IConnectionEventModel model) : base(model)
        {

        }
    
    }
}
