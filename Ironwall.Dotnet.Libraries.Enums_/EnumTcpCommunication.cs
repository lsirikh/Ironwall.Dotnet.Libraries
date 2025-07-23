using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumTcpCommunication : int
    {
        //////////////////////////////////
        IDLE = 0,
        //////////////////////////////////
        MSG_PACKET_RECEPTION_BEGINNIG = 1,
        MSG_PACKET_RECEIVING = 2,
        MSG_PACKET_RECEPTION_COMPLETE = 3,

        MSG_PACKET_SEND_BEGINNING = 4,
        MSG_PACKET_SENDING = 5,
        MSG_PACKET_SEND_COMPLETE = 6,

        //////////////////////////////////
        FILE_PACKET_RECEPTION_BEGINNIG = 11,
        FILE_PACKET_RECEIVING = 12,
        FILE_PACKET_RECEPTION_COMPLETE = 13,

        FILE_PACKET_SEND_BEGINNING = 14,
        FILE_PACKET_SENDING = 15,
        FILE_PACKET_SEND_COMPLETE = 16,

        //////////////////////////////////
        COMMUNICATION_ERROR = 400,
    }
}
