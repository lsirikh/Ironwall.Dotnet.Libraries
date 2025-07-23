using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/19/2025 4:52:51 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class EventMessageModel<T> : IEventMessageModel<T>
{
    public T Value
    {
        get => content;
        set => content = value;
    }

    public string Command
    {
        get => command;
        set => command = value;
    }

    public virtual int CommandId
    {
        get => commandId;
        set => commandId = value;
    }

    #region - Attributes -
    protected int commandId;
    private T content;
    private string command;
    #endregion
}