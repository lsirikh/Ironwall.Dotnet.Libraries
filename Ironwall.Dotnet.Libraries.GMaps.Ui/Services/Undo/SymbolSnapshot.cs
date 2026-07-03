using System;
using System.Reflection;
using System.Text.Json;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : ISymbolSnapshot 구현 — 라이브 모델을 System.Text.Json으로 딥클론(POCO 라운드트립).
   Note         : 재-fetch 금지. 마커의 Model(또는 ImageModel) 프로퍼티를 리플렉션으로 얻어 딥클론.
                  enum=숫자 직렬화(기본), 점 리스트 깊은복사 자동. Map_Edit_Undo_Redo FR-02.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class SymbolSnapshot : ISymbolSnapshot
{
    private static readonly JsonSerializerOptions _opts = new() { IncludeFields = false };

    public int Id { get; set; }
    public object Model { get; }
    public Type ModelType { get; }
    public string MarkerTypeName { get; }
    public bool IsImage { get; }

    private SymbolSnapshot(int id, object model, Type modelType, string markerTypeName, bool isImage)
    {
        Id = id;
        Model = model;
        ModelType = modelType;
        MarkerTypeName = markerTypeName;
        IsImage = isImage;
    }

    /// <summary>마커의 라이브 모델을 딥클론해 스냅샷 생성. Model/ImageModel 프로퍼티를 리플렉션으로 취득.</summary>
    public static ISymbolSnapshot? Capture(IEditableMarker marker)
    {
        if (marker == null) return null;
        var (model, isImage) = ExtractModel(marker);
        if (model == null) return null;
        var clone = DeepClone(model);
        if (clone == null) return null;
        return new SymbolSnapshot(marker.Id, clone, clone.GetType(), marker.GetType().Name, isImage);
    }

    /// <summary>모델 객체를 직접 딥클론해 스냅샷 생성(마커타입 미상 시).</summary>
    public static ISymbolSnapshot? Capture(object model, int id, string markerTypeName, bool isImage)
    {
        if (model == null) return null;
        var clone = DeepClone(model);
        if (clone == null) return null;
        return new SymbolSnapshot(id, clone, clone.GetType(), markerTypeName, isImage);
    }

    /// <summary>이 스냅샷의 모델을 다시 딥클론해 반환(복원 시 원본 스냅샷 오염 방지).</summary>
    public object CloneModel() => DeepClone(Model) ?? Model;

    /// <summary>마커에서 Model(일반) 또는 ImageModel(이미지)을 리플렉션으로 추출.</summary>
    private static (object? model, bool isImage) ExtractModel(IEditableMarker marker)
    {
        var t = marker.GetType();
        var imgProp = t.GetProperty("ImageModel", BindingFlags.Public | BindingFlags.Instance);
        var imgVal = imgProp?.GetValue(marker);
        if (imgVal != null) return (imgVal, true);
        var modelProp = t.GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
        return (modelProp?.GetValue(marker), false);
    }

    /// <summary>POCO를 JSON 라운드트립으로 딥클론(런타임 타입 기준).</summary>
    private static object? DeepClone(object src)
    {
        try
        {
            var type = src.GetType();
            var json = JsonSerializer.Serialize(src, type, _opts);
            return JsonSerializer.Deserialize(json, type, _opts);
        }
        catch (Exception ex)
        {
            // 침묵 금지 — 딥클론 실패는 복원 데이터 유실이므로 로그로 표면화(Trace로 무주입).
            System.Diagnostics.Trace.WriteLine($"[SymbolSnapshot] DeepClone 실패({src?.GetType().Name}): {ex.Message}");
            return null;
        }
    }
}
