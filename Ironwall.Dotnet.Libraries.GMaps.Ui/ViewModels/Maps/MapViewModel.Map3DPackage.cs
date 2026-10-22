using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 영역 내보내기 · 가져오기(2026-10-22 사용자 요청 — "3D 지도는 익스포트 해가지고 다른 곳에 GIS 에다가 임포트", 한 개의 파일 .iw3d,
                  facade-package-contract B).
                  ① 영역 배지 · 「3D 영역 목록」 [이 영역 내보내기…] → 저장 대화상자(.iw3d) → GET /api/jobs/{id}/package 를 그 파일로(진행 %)
                  ② 지도 메뉴 [3D 영역 가져오기…] → 열기 대화상자(.iw3d) → GIS 쪽 검사(속 zip · manifest format) → POST /api/jobs/import(진행 %) → 영역 목록 갱신
   Note         : ★ 지도 편집 권한(CanEditMap)이 있을 때만(메뉴도 권한이 있을 때만 보인다). 편집 모드는 묻지 않는다(다시 만들기 · 지우기와 같은 영역 관리).
                  ★ 진행 · 결과 · 거절 이유는 조준 상태 줄(SetAimStatus)에 쉬운 말로. 받는 중에는 "<파일>.part" 에 쓰고 다 받은 뒤 바꿔 놓는다
                    (끊기면 반쪽 파일을 남기지 않는다). 한 번에 하나만(내보내기 · 가져오기 공용 바쁨).
                  ★ 엔진이 옛 판(그 주소 없음)이면 Map3DPackage.OldEngineText. 실패해도 GIS 는 멈추지 않는다(모든 예외를 여기서 받는다).
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _map3DPackageBusy;
    /// <summary>저장 · 열기 대화상자(시험이 바꿔 끼운다) — 없으면 Windows 대화상자. 저장은 기본 파일 이름을 받는다.</summary>
    private Func<string, string?>? _map3DPackageSavePicker;
    private Func<string?>? _map3DPackageOpenPicker;

    private RelayCommand? _importMap3DAreaCommand;
    /// <summary>지도 메뉴 「3D 영역 가져오기…」.</summary>
    public RelayCommand ImportMap3DAreaCommand => _importMap3DAreaCommand ??= new RelayCommand(_ => _ = ImportMap3DAreaAsync(), _ => !_map3DPackageBusy);

    /// <summary>「이 영역 내보내기…」 — 고른 파일에 그 영역 패키지를 받는다.</summary>
    private async Task ExportMap3DAreaAsync(Map3DAreaItem item)
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (_map3DClient == null) return;
        if (_map3DPackageBusy) { SetAimStatus(PackageBusyText, autoHide: true); return; }
        string? path;
        try { path = (_map3DPackageSavePicker ?? ShowPackageSaveDialog)(Map3DPackage.ExportFileName(item.Title)); }
        catch (Exception ex) { _log?.Error($"[Map3D] 내보내기 대화상자 실패(격리): {ex.Message}"); return; }
        if (string.IsNullOrEmpty(path)) return;
        string part = path + ".part";
        SetMap3DPackageBusy(true, Map3DPackage.ProgressText("내보내는", 0, null));
        _log?.Info($"[Map3D] 영역 내보내기 시작 id={item.Id} → {path}");
        string done;
        try
        {
            await using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await _map3DClient.DownloadPackageAsync(item.Id, file, PackageProgress("내보내는"));
            File.Move(part, path, overwrite: true);
            done = $"3D 영역을 내보냈습니다 — {Path.GetFileName(path)}";
            _log?.Info($"[Map3D] 영역 내보내기 끝 id={item.Id} {new FileInfo(path).Length}B");
        }
        catch (Exception ex)
        {
            done = PackageErrorText("내보내지", ex, "area-export");
            try { File.Delete(part); }
            catch (Exception del) { _log?.Warning($"[Map3D] 내보내기 반쪽 파일 지우기 실패: {del.Message}"); }
        }
        SetMap3DPackageBusy(false, done);
    }

    /// <summary>「3D 영역 가져오기…」 — 고른 .iw3d 를 검사해 엔진에 올리고 영역 목록을 갱신한다.</summary>
    private async Task ImportMap3DAreaAsync()
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (_map3DClient == null) { SetAimStatus(Map3DUnavailableText, autoHide: true); return; }
        if (_map3DPackageBusy) { SetAimStatus(PackageBusyText, autoHide: true); return; }
        string? path;
        try { path = (_map3DPackageOpenPicker ?? ShowPackageOpenDialog)(); }
        catch (Exception ex) { _log?.Error($"[Map3D] 가져오기 대화상자 실패(격리): {ex.Message}"); return; }
        if (string.IsNullOrEmpty(path)) return;
        SetMap3DPackageBusy(true, Map3DPackage.ProgressText("가져오는", 0, null));
        string done;
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            // 큰 업로드 전에 엉뚱한 파일(다른 zip · 그림 등)을 거른다 — 확장자가 아니라 속(manifest)으로
            var check = await Task.Run(() => Map3DPackage.Inspect(file));
            if (check.Error != null)
            {
                _log?.Info($"[Map3D] 영역 가져오기 거름 {path}: {check.Error}");
                done = check.Error;
            }
            else
            {
                file.Position = 0;
                _log?.Info($"[Map3D] 영역 가져오기 시작 {path} ({file.Length}B, 이름={check.Name ?? "-"})");
                var result = await _map3DClient.ImportPackageAsync(file, Path.GetFileName(path), PackageProgress("가져오는"));
                _log?.Info($"[Map3D] 영역 가져오기 끝 id={result.Id} 충돌={result.Conflict ?? "-"}");
                Map3DGoneJobs.Remove(result.Id);   // 지웠던 영역을 다시 들여왔으면 목록이 다시 보이게
                done = Map3DPackage.ImportedText(result);
                _ = RefreshMap3DAreasAsync();
            }
        }
        catch (Exception ex) { done = PackageErrorText("가져오지", ex, "area-import"); }
        SetMap3DPackageBusy(false, done);
    }

    private const string PackageBusyText = "3D 영역을 내보내거나 가져오는 중입니다 — 끝난 뒤 다시 하세요";

    /// <summary>실패 한 줄 — 닿지 않음 · 옛 엔진 · 엔진 거절 문구(형식 · 크기 · 해시) · 파일 오류.</summary>
    private string PackageErrorText(string verb, Exception ex, string cause)
    {
        switch (ex)
        {
            case Map3DServerUnavailableException u:
                _log?.Warning($"[Map3D] {cause} — 서버 닿지 않음: {u.Message}");
                if (!u.IsTimeout) NoteMap3DEngineDown(cause);
                return u.IsTimeout ? $"3D 영역을 {verb} 못했습니다 — 엔진 응답이 너무 늦습니다" : Map3DUnavailableText;
            case Map3DRequestException r when r.Message is "Not Found" or "Method Not Allowed":
                _log?.Warning($"[Map3D] {cause} — 옛 엔진({(int)r.StatusCode})");
                return Map3DPackage.OldEngineText;
            case Map3DRequestException r:
                _log?.Warning($"[Map3D] {cause} — 거절 {(int)r.StatusCode}: {r.Message}");
                return $"3D 영역을 {verb} 못했습니다: {r.Message}";
            case IOException or UnauthorizedAccessException:
                _log?.Warning($"[Map3D] {cause} — 파일 오류: {ex.Message}");
                return $"3D 영역을 {verb} 못했습니다 — 파일을 쓰거나 읽을 수 없습니다(다른 곳을 고르세요)";
            default:
                _log?.Error($"[Map3D] {cause} 실패(격리): {ex.Message}");
                return $"3D 영역을 {verb} 못했습니다.";
        }
    }

    private void SetMap3DPackageBusy(bool busy, string status)
    {
        _map3DPackageBusy = busy;
        SetAimStatus(status, autoHide: !busy);
        ImportMap3DAreaCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// 진행 % 를 상태 줄에 — 글이 바뀔 때만(1% 단위), UI 스레드로. 끝난 뒤 늦게 온 진행은 버린다(끝 한 줄을 덮지 않게).
    /// </summary>
    private IProgress<(long Done, long? Total)> PackageProgress(string verb) => new PackageProgressReporter(this, verb);

    private sealed class PackageProgressReporter : IProgress<(long Done, long? Total)>
    {
        private readonly MapViewModel _vm;
        private readonly string _verb;
        private string? _last;
        public PackageProgressReporter(MapViewModel vm, string verb) { _vm = vm; _verb = verb; }

        public void Report((long Done, long? Total) value)
        {
            var text = Map3DPackage.ProgressText(_verb, value.Done, value.Total);
            if (text == _last) return;
            _last = text;
            _ = OnUiAsync(() => { if (_vm._map3DPackageBusy) _vm.SetAimStatus(text); });
        }
    }

    private static string? ShowPackageSaveDialog(string defaultName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "3D 영역 내보내기", Filter = Map3DPackage.FileFilter, DefaultExt = Map3DPackage.Extension, FileName = defaultName, OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? ShowPackageOpenDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "3D 영역 가져오기", Filter = Map3DPackage.FileFilter, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
