namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>제어기(<c>C</c>)가 펜스 어느 끝에 있는가(fence-wiring-editor FR-19 · §1-0b) — 제어기마다 · GIS 로컬.</summary>
public enum FenceControllerEnd
{
    /// <summary>왼쪽 끝(망 0 쪽 · 기본).</summary>
    Left = 0,
    /// <summary>오른쪽 끝(마지막 망 쪽).</summary>
    Right = 1,
}
