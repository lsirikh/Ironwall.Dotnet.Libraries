using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class CategoryModel : BaseModel, ICategoryModel
{
    public CategoryModel() { }

    public CategoryModel(ICategoryModel model) : base(model)
    {
        Name = model.Name;
        TypeServer = model.TypeServer;
        Description = model.Description;
        SortOrder = model.SortOrder;
    }

    [JsonProperty("name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type_server", Order = 3)]
    public string TypeServer { get; set; } = string.Empty;

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }

    [JsonProperty("sort_order", Order = 5)]
    public int SortOrder { get; set; }
}
