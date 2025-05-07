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
    public class ContactEventMapper
        : MetaEventMapper
        , IContactEventMapper
    {
        public ContactEventMapper()
        {

        }

        public ContactEventMapper(IContactEventModel model) : base(model)
        {
            ReadWrite = model.ReadWrite;
            ContactNumber = model.ContactNumber;
            ContactSignal = model.ContactSignal;
        }

        public int ReadWrite { get; set; }
        public int ContactNumber { get; set; }
        public int ContactSignal { get; set; }

    }
}
