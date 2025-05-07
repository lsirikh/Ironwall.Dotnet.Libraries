using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class ContactDetailModel
         : IContactDetailModel
    {
        public ContactDetailModel()
        {

        }

        public ContactDetailModel(int readWrite, int contactNumber, int contactSignal)
        {
            ReadWrite = readWrite;
            ContactNumber = contactNumber;
            ContactSignal = contactSignal;
        }

        [JsonProperty("read_write", Order = 1)]
        public int ReadWrite { get; set; }
        [JsonProperty("contact_number", Order = 2)]
        public int ContactNumber { get; set; }
        [JsonProperty("contact_signal", Order = 3)]
        public int ContactSignal { get; set; }
    }
}
