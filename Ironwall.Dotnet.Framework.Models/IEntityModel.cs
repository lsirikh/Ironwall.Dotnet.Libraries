using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models;

public interface IEntityModel
{
    #region - Properties -
    int Id { get; set; }
    string NameArea { get; set; }
    int TypeDevice { get; set; }
    string NameDevice { get; set; }
    int IdController { get; set; }
    int IdSensor { get; set; }
    int TypeShape { get; set; }
    double X1 { get; set; }
    double Y1 { get; set; }
    double X2 { get; set; }
    double Y2 { get; set; }
    double Width { get; set; }
    double Height { get; set; }
    double Angle { get; set; }
    int Map { get; set; }
    bool Used { get; set; }
    bool Visibility { get; set; }
    #endregion
}
