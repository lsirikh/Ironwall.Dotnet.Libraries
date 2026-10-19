# 수정 전 실패 증명 — 제품 파일 4개를 기준 커밋(cdc1238c) 판으로 잠시 되돌려 새 시험을 돌리고, 끝나면 현재 판으로 복원한다.
import subprocess, pathlib, sys
root = pathlib.Path(__file__).resolve().parents[2]
files = ["Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.cs",
         "Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3D.cs",
         "Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DMath.cs",
         "Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DGuide.cs"]
keep = {f: (root / f).read_bytes() for f in files}
def run(args):
    r = subprocess.run(args, cwd=root, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout + r.stderr
try:
    for f in files:
        (root / f).write_bytes(subprocess.run(["git", "show", f"cdc1238c:{f}"], cwd=root, capture_output=True).stdout)
    for p in ["tests/GMaps.MapEdit.Tests", "tests/GMaps.MapLayer.Tests"]:
        out = run(["dotnet", "build", p, "-v", "q", "-nologo"])
        print(p, [l for l in out.splitlines() if "오류" in l or " error " in l][:5])
    for p, flt in [("tests/GMaps.MapEdit.Tests", "FullyQualifiedName~should_not_reenter_at_same_place_in_auto|FullyQualifiedName~should_restore_only_failed_members|FullyQualifiedName~should_close_panel_and_cancel_preview_when_empty_ground"),
                   ("tests/GMaps.MapLayer.Tests", "FullyQualifiedName~should_tell_edit_mode_needed")]:
        out = run(["dotnet", "test", p, "--no-build", "-nologo", "--filter", flt])
        lines = out.splitlines()
        for i, l in enumerate(lines):
            if l.strip().startswith(("실패 ", "통과!", "실패!")) or "오류 메시지" in l:
                print(l.strip()[:220])
                if "오류 메시지" in l and i + 1 < len(lines): print("   ", lines[i + 1].strip()[:220])
finally:
    for f, b in keep.items(): (root / f).write_bytes(b)
    print("복원:", run(["git", "diff", "--stat", "HEAD", "--"] + files))
