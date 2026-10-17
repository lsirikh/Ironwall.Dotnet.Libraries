using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

public class MapLayerModel : BaseModel, IMapLayerModel
{
    public MapLayerModel() { }

    public MapLayerModel(IMapLayerModel model) : base(model)
    {
        Name = model.Name;
        LayerType = model.LayerType;
        Category = model.Category;
        IsVisible = model.IsVisible;
        Opacity = model.Opacity;
        ZOrder = model.ZOrder;
        MapId = model.MapId;
        FilePath = model.FilePath;
        CreatedAt = model.CreatedAt;
        UpdatedAt = model.UpdatedAt;
    }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("layer_type")]
    public string LayerType { get; set; } = "Symbol";

    [JsonProperty("category")]
    public string? Category { get; set; }

    [JsonProperty("is_visible")]
    public bool IsVisible { get; set; } = true;

    [JsonProperty("opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonProperty("z_order")]
    public int ZOrder { get; set; }

    [JsonProperty("map_id")]
    public int? MapId { get; set; }

    [JsonProperty("file_path")]
    public string? FilePath { get; set; }

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
