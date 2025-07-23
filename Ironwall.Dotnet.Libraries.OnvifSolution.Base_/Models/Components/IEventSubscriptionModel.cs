namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IEventSubscriptionModel
    {
        FilterTypeModel Filter { get; set; }
        FilterTypeModel SubscriptionPolicy { get; set; }
    }
}