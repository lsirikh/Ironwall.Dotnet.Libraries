namespace Dotnet.Monitoring.Solution.Models;

/// <summary>
/// SHIM of the host's settings bag (the real <c>SetupModel</c> implements ten setup interfaces and drags in
/// Streaming/GMaps/Redis setup models). The linked host services read only these four members.
/// Values mirror the probe's isolation: GroupNats = unit999, so the host's own ACTION_REPORT / WINDY publishes
/// land on sensorway.unit999.* (and GuardedNatsService refuses anything else anyway).
/// </summary>
internal class SetupModel
{
    public int ModeWindy { get; set; }
    public string NameChannel { get; set; } = "live-nats-pipeline-redis-channel";
    public string? GroupNats { get; set; } = LiveNatsPipeline.Safety.Group;
    public string SystemUuid { get; set; } = "live-nats-pipeline";
    // event timing (host 6721afdb+ reads these for the legacy AI_DETECTION queue path) — auto report OFF, as in the pipeline
    public int TimeDiscardSec { get; set; } = 3600;
    public bool IsAutoEventDiscard { get; set; }
    public int MalfunctionTimeDiscardSec { get; set; } = 3600;
    public bool IsMalfunctionAutoEventDiscard { get; set; }
}
