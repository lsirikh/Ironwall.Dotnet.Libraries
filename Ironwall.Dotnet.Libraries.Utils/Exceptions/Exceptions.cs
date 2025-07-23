using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Utils.Exceptions;

public class SocketSendException : Exception
{
    public SocketSendException(string message, IPEndPoint iPEndPoint)
        : base(message)
    {
        EndPoint = iPEndPoint;
    }

    public IPEndPoint EndPoint { get; set; }
}
public class TcpResponseFailException : Exception
{
    public TcpResponseFailException(bool success, string message) : base(message)
    {
        Success = success;
    }

    public bool Success { get; set; }
}

public class UserRegisterException : Exception
{
    public UserRegisterException(string message)
        : base(message)
    {
    }
}

public class DeviceMatchFailException : Exception
{
    public DeviceMatchFailException(string message, int controller = default, int sensor = default, int device = default) : base(message)
    {
        Controller = controller;
        Sensor = sensor;
        Device = device;
    }

    public int Controller { get; set; }
    public int Sensor { get; set; }
    public int Device { get; set; }
}
