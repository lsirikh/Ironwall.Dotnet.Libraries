using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/// <summary>
/// 기존 제공자 지도 인터페이스 (순수 데이터)
/// </summary>
public interface IDefinedMapModel : IMapModel
{
    string GMapProviderName { get; set; }
    string? ProviderGuid { get; set; }
    EnumMapVendor Vendor { get; set; }
    EnumMapStyle Style { get; set; }
    bool RequiresApiKey { get; set; }
    string? ApiKey { get; set; }
    string? ServiceUrl { get; set; }
    int? DailyRequestLimit { get; set; }
    string? LicenseInfo { get; set; }
    DateTime? LastAccessedAt { get; set; }
    int? TodayUsageCount { get; set; }
}