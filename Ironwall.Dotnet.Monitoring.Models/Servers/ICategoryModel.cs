using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public interface ICategoryModel : IBaseModel
{
    string Name { get; set; }
    string TypeServer { get; set; }
    string? Description { get; set; }
    int SortOrder { get; set; }
}
