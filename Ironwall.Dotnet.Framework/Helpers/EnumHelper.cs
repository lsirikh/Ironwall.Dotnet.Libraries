using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Helpers;

public static class EnumHelper
{
    public static bool GetStatusType(EnumTrueFalse type) =>
    type switch
    {
        EnumTrueFalse.False => false,
        EnumTrueFalse.True => true,
        _ => false
    };

    public static EnumTrueFalse SetStatusType(bool type)
    {
        if (type)
            return EnumTrueFalse.True;
        else
            return EnumTrueFalse.False;
    }

    public static EnumEventType GetEventType(EnumCmdType type)
    {

        switch (type)
        {
            case EnumCmdType.LOGIN_REQUEST:
            case EnumCmdType.LOGIN_REQUEST_FORCE:
            case EnumCmdType.LOGIN_RESPONSE:
                {
                }
                break;
            case EnumCmdType.LOGOUT_REQUEST:
            case EnumCmdType.LOGOUT_REQUEST_FORCE_LOGIN:
            case EnumCmdType.LOGOUT_REQUEST_TIMEOUT:
            case EnumCmdType.LOGOUT_RESPONSE:
                {

                }
                break;
            case EnumCmdType.SESSION_REFRESH_REQUEST:
            case EnumCmdType.SESSION_REFRESH_RESPONSE:
                {

                }
                break;
            case EnumCmdType.USER_ACCOUNT_ADD_REQUEST:
            case EnumCmdType.USER_ACCOUNT_ADD_RESPONSE:
                {

                }
                break;

            case EnumCmdType.USER_ACCOUNT_EDIT_REQUEST:
            case EnumCmdType.USER_ACCOUNT_EDIT_RESPONSE:
                {

                }
                break;
            case EnumCmdType.USER_ACCOUNT_DELETE_REQUEST:
            case EnumCmdType.USER_ACCOUNT_DELETE_RESPONSE:
                {

                }
                break;
            case EnumCmdType.USER_ACCOUNT_INFO_REQUEST:
            case EnumCmdType.USER_ACCOUNT_INFO_RESPONSE:
                {
                }
                break;
            
            case EnumCmdType.EVENT_DETECTION_REQUEST:
            case EnumCmdType.EVENT_DETECTION_RESPONSE:
                {
                    return EnumEventType.Intrusion;
                }
            case EnumCmdType.EVENT_MALFUNCTION_REQUEST:
            case EnumCmdType.EVENT_MALFUNCTION_RESPONSE:
                {
                    return EnumEventType.Fault;
                }
            case EnumCmdType.EVENT_ACTION_REQUEST:
            case EnumCmdType.EVENT_ACTION_RESPONSE:
                {
                    return EnumEventType.Action;
                }
            case EnumCmdType.EVENT_CONNECTION_REQUEST:
            case EnumCmdType.EVENT_CONNECTION_RESPONSE:
                {
                    return EnumEventType.Connection;
                }
            default:
                break;
        }

        throw new InvalidEnumArgumentException($"{type} was not defined yet!");
    }
}
