using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;

/// <summary>
/// 선택 칸의 항목 하나.
/// </summary>
/// <param name="Display">목록에 보이는 글.</param>
/// <param name="Text">값을 글로 쓸 때 넘기는 것(코드 · 열거형 이름). 객체로만 쓸 수 있는 칸이면 null.</param>
/// <param name="Value">값을 객체로 쓸 때 넘기는 것(제어기 · 서버 행). 글로 쓰는 칸이면 null.</param>
public sealed record PropertyOption(string Display, string? Text, object? Value = null)
{
    public override string ToString() => Display;
}

/// <summary>
/// 선택 칸의 항목을 대 주는 쪽 — 종류 축 · 부가 축 · 제어기 · 서버처럼 <b>폼이 혼자서는 알 수 없는</b> 목록.
/// 열거형 · 참/거짓은 폼이 스스로 만든다.
/// </summary>
public interface IDevicePropertyOptions
{
    /// <summary>항목이 없으면 빈 목록 — null 을 돌려주지 않는다.</summary>
    IReadOnlyList<PropertyOption> OptionsFor(DevicePropertySpec spec, EnumDeviceCategory category);
}
