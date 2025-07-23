using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumTcpStateType : int
    {
        STATE = 0,
        DATATYPE = 1,
        MESSAGESIZE = 2,
        MESSAGEDOWNLOAD = 3,
        FILENAMESIZE = 4,
        FILENAME = 5,
        FILESIZE = 6,
        FILEDOWNLOAD = 7
    }
}
