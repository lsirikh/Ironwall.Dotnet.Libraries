using Ironwall.Dotnet.Libraries.Base.Models;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 6:28:41 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CloseAllMessageModel
{
}

public class CloseDialogMessageModel
{
}

public class ClosePanelMessageModel
{
}

public class ClosePopupMessageModel
{
}

public class ExitProgramMessageModel : IMessageModel
{
}

public class OpenConfirmPopupMessageModel
    : CommonMessageModel
{
}

public class OpenInfoPopupMessageModel
    : CommonMessageModel
{
}

public class OpenProgressPopupMessageModel
    : IMessageModel
{
}

public class StatusMessageModel
{
    public StatusMessageModel()
    {
    }

    public StatusMessageModel(string log)
    {
        Log = log;
    }

    public string? Log { get; set; }
}