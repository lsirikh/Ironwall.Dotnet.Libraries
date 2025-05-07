using Ironwall.Dotnet.Framework.Models.Communications;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Mappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public class ConnectionEventModel
        : MetaEventModel, IConnectionEventModel
    {
        public ConnectionEventModel()
        {
        }

        public ConnectionEventModel(IConnectionEventMapper model, IBaseDeviceModel device)
            : base(model, device)
        {
        }
     

    }
}
