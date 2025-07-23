using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs
{
    public interface IMetadataConfigModel
    {
        bool Analytics { get; set; }
        string CompressionType { get; set; }
        EventSubscriptionModel EventSubscription { get; set; }
        bool GeoLocation { get; set; }
        MultiCastModel MultiCast { get; set; }
        string Name { get; set; }
        PTZFilterModel PTZStatus { get; set; }
        string SessionTimeout { get; set; }
        bool ShapePolygon { get; set; }
        string Token { get; set; }
        int UseCount { get; set; }
    }
}