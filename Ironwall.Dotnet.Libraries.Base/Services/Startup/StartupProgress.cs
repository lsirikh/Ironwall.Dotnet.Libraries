namespace Ironwall.Dotnet.Libraries.Base.Services.Startup;

public readonly record struct StartupProgress(string JobName, string Stage, double TotalFraction);
