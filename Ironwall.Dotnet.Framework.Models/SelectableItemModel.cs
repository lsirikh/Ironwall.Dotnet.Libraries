using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

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
