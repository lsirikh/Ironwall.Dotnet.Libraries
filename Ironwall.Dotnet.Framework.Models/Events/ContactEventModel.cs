using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Communications.Events;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Mappers;
using Ironwall.Dotnet.Framework.Models.Mappers.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public class ContactEventModel
        : MetaEventModel
        , IContactEventModel
    {
        /*
         *  Row Model
            public int IdGroup { get; set; }
            public int IdController { get; set; }
            public int IdSensor { get; set; }
            public int TypeMessage { get; set; }
            public int Sequence { get; set; }
            public int ReadWrite { get; set; }
            public string SerialNumber { get; set; }
            public int ContactOutNumber { get; set; }
            public int ContactOutSignal { get; set; }
        */

        public ContactEventModel()
        {

        }

        public ContactEventModel(IContactEventMapper model, IBaseDeviceModel device) : base(model, device)
        {
            ReadWrite = model.ReadWrite;
            ContactNumber = model.ContactNumber;
            ContactSignal = model.ContactSignal;
        }

        //public ContactEventModel(IContactOnRequestModel model, IBaseDeviceModel device) : base(model, device)
        //{
        //    ReadWrite = model.Detail.ReadWrite;
        //    ContactNumber = model.Detail.ContactNumber;
        //    ContactSignal = model.Detail.ContactSignal;
        //}

        //public ContactEventModel(IContactOffRequestModel model, IBaseDeviceModel device) : base(model, device)
        //{
        //    ReadWrite = model.Detail.ReadWrite;
        //    ContactNumber = model.Detail.ContactNumber;
        //    ContactSignal = model.Detail.ContactSignal;
        //}

        [JsonProperty("read_write", Order = 6)]
        public int ReadWrite { get; set; }
        [JsonProperty("contact_number", Order = 7)]
        public int ContactNumber { get; set; }
        [JsonProperty("contact_signal", Order = 8)]
        public int ContactSignal { get; set; }
    }
}
