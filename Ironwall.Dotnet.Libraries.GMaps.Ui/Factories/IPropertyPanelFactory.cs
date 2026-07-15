using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
public interface IPropertyPanelFactory
{
    GMapPropertyBaseControl CreatePropertyPanel(IEditableMarker marker);

    /// <summary>멀티셀렉션(≥2) 공통 속성창 — 동종=해당 타입 패널, 혼합=공통 기본 속성창. (기능 ②)</summary>
    GMapPropertyBaseControl CreateCommonPropertyPanel(System.Collections.Generic.IReadOnlyList<IEditableMarker> markers);
}