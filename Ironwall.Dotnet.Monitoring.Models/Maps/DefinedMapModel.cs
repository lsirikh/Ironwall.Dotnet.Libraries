using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/// <summary>
/// 기존 제공자 지도 모델 (순수 데이터, 메서드 없음)
/// </summary>
public class DefinedMapModel : MapModel, IDefinedMapModel
{
    public DefinedMapModel()
    {
        RequiresApiKey = false;
        TodayUsageCount = 0;
    }

    public DefinedMapModel(IDefinedMapModel model) : base(model)
    {
        GMapProviderName = model.GMapProviderName;
        ProviderGuid = model.ProviderGuid;
        Vendor = model.Vendor;
        Style = model.Style;
        RequiresApiKey = model.RequiresApiKey;
        ApiKey = model.ApiKey;
        ServiceUrl = model.ServiceUrl;
        DailyRequestLimit = model.DailyRequestLimit;
        LicenseInfo = model.LicenseInfo;
        LastAccessedAt = model.LastAccessedAt;
        TodayUsageCount = model.TodayUsageCount;
    }

    public override EnumMapProvider ProviderType => EnumMapProvider.Defined;

    #region - Defined Map Properties (순수 데이터) -
    [JsonProperty("gmap_provider_name", Order = 20)]
    public string GMapProviderName { get; set; } = string.Empty;

    [JsonProperty("provider_guid", Order = 21)]
    public string? ProviderGuid { get; set; }

    [JsonProperty("vendor", Order = 22)]
    public EnumMapVendor Vendor { get; set; }

    [JsonProperty("style", Order = 23)]
    public EnumMapStyle Style { get; set; }

    [JsonProperty("requires_api_key", Order = 24)]
    public bool RequiresApiKey { get; set; }

    [JsonProperty("api_key", Order = 25)]
    public string? ApiKey { get; set; }

    [JsonProperty("service_url", Order = 26)]
    public string? ServiceUrl { get; set; }

    [JsonProperty("daily_request_limit", Order = 27)]
    public int? DailyRequestLimit { get; set; }

    [JsonProperty("license_info", Order = 28)]
    public string? LicenseInfo { get; set; }

    [JsonProperty("last_accessed_at", Order = 29)]
    public DateTime? LastAccessedAt { get; set; }

    [JsonProperty("today_usage_count", Order = 30)]
    public int? TodayUsageCount { get; set; }
    #endregion
}
