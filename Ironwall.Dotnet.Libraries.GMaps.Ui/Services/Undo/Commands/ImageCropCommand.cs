using System;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/// <summary>오버레이 이미지 자르기 상태 — 그림 파일 · 경계 · 표시 크기(px).</summary>
public readonly record struct ImageCropState(string FilePath, RectLatLng Bounds, double Width, double Height);

/****************************************************************************
   Purpose      : 오버레이 이미지 「다각형으로 자르기」 · 「자르기 되돌리기」 Undo 1단위 — 그림 파일 · 경계 · 크기를 함께 바꾼다.
   Note         : IUndoApplyContext 에 멤버를 더하지 않으려고(구현이 여럿) 적용 함수를 받는다 — MapViewModel.ApplyImageCropStateAsync.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class ImageCropCommand : IUndoableCommand
{
    private readonly Func<int, ImageCropState, CancellationToken, Task> _apply;
    private readonly int _imageId;
    private readonly ImageCropState _before, _after;

    public ImageCropCommand(string description, int imageId, ImageCropState before, ImageCropState after, Func<int, ImageCropState, CancellationToken, Task> apply)
    {
        Description = description;
        _imageId = imageId;
        _before = before;
        _after = after;
        _apply = apply;
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }
    public Task ExecuteAsync(CancellationToken ct = default) => _apply(_imageId, _after, ct);
    public Task UndoAsync(CancellationToken ct = default) => _apply(_imageId, _before, ct);
}
