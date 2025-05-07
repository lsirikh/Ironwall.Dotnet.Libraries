using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    public class MalfunctionEventMapper
        : MetaEventMapper, IMalfunctionEventMapper
    {
        public MalfunctionEventMapper()
        {

        }

        public MalfunctionEventMapper(IMalfunctionEventModel model) : base(model)
        {
            Reason = model.Reason;
            FirstStart = model.FirstStart;
            FirstEnd = model.FirstEnd;
            SecondStart = model.SecondStart;
            SecondEnd = model.SecondEnd;
        }

        //public MalfunctionEventMapper(IMalfunctionRequestModel model, IBaseDeviceModel device) 
        //    : base(model, device)
        //{
        //    Reason = model.Detail.Reason;
        //    FirstStart = model.Detail.FirstStart;
        //    FirstEnd = model.Detail.FirstEnd;
        //    SecondStart = model.Detail.SecondStart;
        //    SecondEnd = model.Detail.SecondEnd;
        //}

        public EnumFaultType Reason { get; set; }
        public int FirstStart { get; set; }
        public int FirstEnd { get; set; }
        public int SecondStart { get; set; }
        public int SecondEnd { get; set; }
    }
}
