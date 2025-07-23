namespace Ironwall.Dotnet.Libraries.Sounds.Models;

public interface ISoundSetupModel
{
    string? DirectoryUri { get; set; }
    string? DetectionSound { get; set; }
    string? MalfunctionSound { get; set; }
    string? ActionReportSound { get; set; }
    int DetectionSoundDuration { get; set; }
    int MalfunctionSoundDuration { get; set; }
    int ActionReportSoundDuration { get; set; }
    bool IsDetectionAutoSoundStop { get; set; }
    bool IsMalfunctionAutoSoundStop { get; set; }
    bool IsActionReportAutoSoundStop { get; set; }
    string? AudioDevice { get; set; }
}