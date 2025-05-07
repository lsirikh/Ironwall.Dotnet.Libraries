using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Enums;

public enum EnumTcpCommunication
{
    //////////////////////////////////
    IDLE,
    //////////////////////////////////
    MSG_PACKET_RECEPTION_BEGINNIG,
    MSG_PACKET_RECEIVING,
    MSG_PACKET_RECEPTION_COMPLETE,

    MSG_PACKET_SEND_BEGINNING,
    MSG_PACKET_SENDING,
    MSG_PACKET_SEND_COMPLETE,

    //////////////////////////////////
    FILE_PACKET_RECEPTION_BEGINNIG,
    FILE_PACKET_RECEIVING,
    FILE_PACKET_RECEPTION_COMPLETE,

    FILE_PACKET_SEND_BEGINNING,
    FILE_PACKET_SENDING,
    FILE_PACKET_SEND_COMPLETE,

    //////////////////////////////////
    COMMUNICATION_ERROR,
}
