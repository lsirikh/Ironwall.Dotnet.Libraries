using System;

namespace Ironwall.Dotnet.Libraries.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/19/2025 4:29:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class SelectableItemModel
{
    #region - Properties -
    public int Id { get; set; }
    public string? Name
    {
        get => name;
        set => name = value;
    }

    public bool IsSelected
    {
        get => isSelected;
        set => isSelected = value;
    }
    #endregion

    #region - Attributes -
    private string? name;
    private bool isSelected;
    #endregion
}