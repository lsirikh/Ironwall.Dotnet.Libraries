# REPORT — 최근 기능 전수 시나리오 시험 (t-20261019-193448-b2cc)

## 결론
- 시나리오 약 500행(A 3D 진입/이탈 · B 3D 영역 · C 3D 상호작용 · D 2D 편집 · E 오버레이 이미지 · F map-3d 품질)을 표로 정리했다 → `SCENARIOS.md`(영역별 원본 `areas/A~F.md`).
- 빈 칸에 새 헤드리스 시험 **C# 207건 + map-3d 52건**을 썼다. 시험이 드러낸 **결함 28건을 고쳤다** — 모두 "수정 전 그 시험이 실패 → 수정 후 통과"를 실제로 돌려 확인했다(상세 `DEFECTS.md`).
- 전 프로젝트 실패 0. 기존 시험은 하나도 고치거나 지우지 않았다.
- 헤드리스로 확정할 수 없는 항목은 `HEADED.md`(준비 · 순서 · AutomationId · 기대 · 판정 · 정리).

## 전 프로젝트 시험 결과(수정 전 기준선 → 최종)
| 대상 | 명령 | 기준선 | 최종 |
|---|---|---|---|
| 라이브러리 sln(10 dll) | `dotnet test Ironwall.Dotnet.Libraries.sln` | 2888 통과 | 2888 통과 · 실패 0 · EXIT 0 |
| GMaps.Ui.Tests | `dotnet test tests/GMaps.Ui.Tests` | 1581 | 1613 · 실패 0 |
| GMaps.MapEdit.Tests | `dotnet test tests/GMaps.MapEdit.Tests` | 503 | 667 · 실패 0 |
| GMaps.MapNav.Tests | 〃 | 60 | 67 · 실패 0 |
| GMaps.Housing.Tests | 〃 | 353 | 353 · 실패 0 |
| GMaps.MapAux.Tests | 〃 | 76 | 80 · 실패 0 |
| GMaps.PropertyPanel.Tests | 〃 | 436 | 436 · 실패 0 |
| GMaps.MapLayer.Tests(.sln 밖, D 영역이 닿아 추가로 돌림) | 〃 | 97 | 97 · 실패 0 |
| map-3d pytest(+ node .mjs, pytest 가 부름) | `artifacts/map3d` 에서 `& C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider` | 234 passed | 286 passed · 실패 0 |
| 메인 앱 Dotnet.Monitoring.Solution.Tests(이 worktree 라이브러리로 빌드한 복제본) | `powershell -File .harness/t-20261019-193448-b2cc/run-host-tests.ps1` | 887 통과 · 8 건너뜀 | 887 통과 · 8 건너뜀 · 실패 0 |

로그: `logs/baseline-*.txt` · `logs/merge3-*.txt`. Sounds 시험 · Libraries.Db 시험은 돌리지 않았다(규칙). 창 · WebView2 · 브라우저를 띄우는 새 시험은 없다.

## 고친 결함(28건 — 원인 · 수정 · 증거는 DEFECTS.md)
| 영역 | 결함 | 고친 곳 |
|---|---|---|
| A | 편집 중 거절된 「이 영역 3D 로 보기」가 돌아갈 자리를 남겨, 나중에 「3D 로 보기」→나가기가 엉뚱한 자리(줌 17)로 감 | MapViewModel.Map3DAreas.cs ViewMap3DArea |
| A | 준비 중 다른 영역 「이 영역 3D 로 보기」를 누르면 처음 자리를 잃음(작업 중 새로 찾음) | 〃 |
| A | 3D 를 펼칠 때 끌던 중이면 벤더 지도 끌기 상태가 남아 나간 뒤 정수 줌 · 위치 이동 · 휠이 안 먹음(10-19 "나왔더니 휠이 안 먹는다" 증상과 겹침) | GMapControl.cs `IsCoreDragging`/`EndCoreDrag()` 추가 · GMapCustomControl.ResetTransientInput |
| A | 자동 모드에서 만들기 취소 · 실패로 끝나면 게이트를 다시 판정하지 않아 지도를 움직이기 전까지 3D 로 안 들어감 | MapViewModel.Map3D.cs EndMap3DBake |
| B | 다시 만드는 중인 영역을 지우면 굽기가 끝난 뒤 지운 영역이 새 장면으로 되살아남 | Map3DAreaItem(메뉴에서 지우기 뺌) · RefuseDeleteWhileRebaking |
| B | 「다시 만들기」 판 [취소] 뒤 다시 만들기 표지가 남음(잠재 누수) | MapViewModel.Map3DBakeOptions.cs |
| C | 1층보다 낮은 건물에서 −1층 · ↓ 가 높이를 3.0 m 로 올림 / 450 m 넘는 건물 +1층이 400 m 로 내림 | Map3DBuilding.StepFloors |
| C | 저장 중 3D 를 나가면 늦은 응답이 되돌리기 칸을 다시 쌓아 다음 3D 에서 앞 세션 저장을 되돌릴 수 있음 | MapViewModel.BuildingHeight.cs `_bhSession` |
| C | 네이티브 창(hwnd) 3D 에서 건물을 누르면 보이지 않는 판이 열려 첫 Esc 를 가로챔 → 판을 열지 않고 안내 | OnMap3DBuildingPicked |
| C | 잘못된 입력("abc")이 남은 채 [적용] 하면 앞 미리보기 값이 저장됨 | `_bhInputError` · ApplyBuildingHeightCommand |
| D | 묶음 회전 미리보기 중 「그룹 잠금」/레이어 자물쇠 → 미리보기 위치가 DB 에 저장되고 Esc 는 화면만 되돌림(DB ≠ 화면) | ExecuteGroupLock · OnSymbolLockChanged · OnLayerLockChanged(먼저 회전 확정) |
| D | 회전 미리보기 중 속성 판 변경 → 미리보기 위치가 DB 에 저장 | OnMarkerPropertyChanged(먼저 확정) |
| D | 회전 확정 · Undo 뒤 그룹 속성 판의 회전 · 감시 방향 칸이 낡음 | RefreshGroupPropertyPanelValues |
| D | 그룹 속성 저장이 실패해도 Undo 기록 | ApplyGroupPropertyChangeAsync |
| D | 레이어 패널 → 지도 선택에 줌 때문에 안 보이는 심볼도 들어감 | MapViewModel.LayerSelection.cs |
| D | 같은 장비를 세 마커가 가진 자료에서 재할당이 한 마커만 비움 | MapViewModel.DeviceReassign.cs |
| E | 자르기 판 안의 Delete · 방향키 · Ctrl+Z/Y 를 지도 창 처리기가 먼저 가로챔(Delete = 선택 그림 영구 삭제 확인창) | OnMapPreviewKeyDownForGroup `IsImageCropPanelKey` |
| E | 판이 떠 있는데 편집 모드를 꺼도 판이 남고, 적용하면 편집 모드 꺼진 채 DB 수정 | IsEditModeEnabled setter `CancelImageCropPanel` |
| E | 다시 자르기 판이 떠 있는 동안 그림이 바뀌면(Undo) 자리 · 크기가 어긋남 | MapViewModel.ImageCrop.cs |
| E | 넓이는 있지만 남는 픽셀이 0개인 다각형 → 완전 투명 PNG 저장 | ImageCropMath.Validate · `HasAnyPixel` 추가 |
| E | 자르기 · 되돌리기 DB 저장 실패를 삼키고 Undo 를 쌓고 "잘랐습니다" | MapViewModel.ImageCrop.cs |
| E | 판이 떠 있을 때 Ctrl+I → 파일까지 고른 뒤 안내 없이 사라짐 | ExecuteLoadImageOverlay |
| E | 「다각형으로 자르기…」 · 「자르기 되돌리기」 메뉴에 AutomationId 없음(헤디드 자동화 불가) | `GMaps.Symbol.ContextMenu.ImageCrop/ImageUncrop` |
| E | 저장 폴더에 못 쓰면 처음 불러오기는 안내 없이 실패, 다시 자르기는 예외가 새어 나감 | MapViewModel.ImageCrop.cs |
| F(뷰어) | 장면 영역 밖 추적 표식이 허공에 뜸 | overlay3d.js |
| F(뷰어) | 영역 밖이라 숨긴 마커 자리를 GIS 카메라 상자 · 조립 카드가 따라감 | overlay3d.js `isOutside` · view.js watchPoint |
| F(뷰어) | 마커 A → 건물 → 마커 A 를 빨리 누르면 더블클릭으로 판정 | overlay-pick.js `clickStep` · view.js |
| F(굽기) | 고위도에서 가정 해 고도가 음수 → shadow_k 음수 → 그림자 높이 전부 하한 | extract.py `SUN_EL_MIN` · pipeline.py |

공개 형식은 더하기만 했다(GMapControl `IsCoreDragging`/`EndCoreDrag` · ImageCropMath `HasAnyPixel` · overlay3d `isOutside` · overlay-pick `clickStep`/`DBLCLICK_MS` · extract `SUN_EL_MIN`).

## 결정 필요(고치지 않음 — 지금 동작을 시험이나 보고서에 기록)
1. 로그인 직후 안내 "나가기 · ESC = 2D" — WPF 안 3D 는 Esc 로 안 나간다. 기존 시험 `Map3DHudTests::should_guide_shown_3d_without_zoom_wording` 이 "ESC" 포함을 고정.
2. 제품이 안 쓰는 `Map3DMath.ShouldLeave3D` · `ExitCamera(bool,…)` · `Map3DGuide.ShouldLeave3DForPanel` 을 기존 시험 여럿이 시험(옛 규칙). 지울지.
3. 수동(단추) 모드에서도 미리 불러오기를 한다.
4. 자동 모드에서 3D 중 편집 · 측정을 켰다 끄면 곧바로 3D 로 다시 들어간다.
5. 보던 3D 영역을 지우면 서버 DELETE 전에 2D 로 나간다(서버에 못 닿아도 이미 2D).
6. 층고 10 m × 200층(2000 m)을 안내 없이 600 m 로 자르고 판에는 60층(기존 시험이 고정).
7. 묶음 회전 확정 저장이 일부 실패해도 Undo 는 남고 화면 ≠ DB.
8. Ctrl+I(이미지 불러오기)는 편집 모드가 아니어도 동작 → 운영 모드에서 자르기 판이 뜰 수 있음.
9. 3D 빈 땅 클릭은 3D 윤곽만 지우고 GIS 건물 높이 판은 열린 채.
10. map-3d: 1×1 · 한 색 그림을 extract 로 구우면 범위 전체 크기 건물 1채(1×1 은 높이 0 m) — 합성 지도에서만인지 미확인.
11. map-3d: 목 폭 0.6 m 미만 건물은 난간 전체가 빠짐 · MBTiles 가 없으면 요청 줌이 경고 없이 16.
12. 층수 입력 직후 비활성 [적용] 을 누르면 LostFocus 가 안 일어나 첫 클릭이 무시될 수 있음 → HEADED 확인 항목.

## 산출물
- `PLAN.md`(진행 · 부록: 시나리오 표) · `SCENARIOS.md` · `areas/A~F.md`(영역 원본) · `DEFECTS.md` · `HEADED.md` · `DECISIONS.md`
- `map3d.patch` — map-3d 변경(제품 5 · 새 시험 4). 원본 `C:\workspace_python\map-3d` 깨끗한 clone 에 `git apply --check` 통과. 적용은 사람이 하네스 수락 때.
- host.patch 없음 — 메인 앱 코드는 바꾸지 않았다(메인 앱 시험은 이 라이브러리로 다시 빌드해 887 통과).
- `patches/A~E.patch` · `patches/*-new/` — 작업자 결과 원본(이미 이 worktree 에 합침).

## 정리 · 남은 것
- 이 worktree 안: `artifacts/map3d`(map-3d 작업 사본) · `.harness-worktrees/host`(메인 앱 복제본)는 git 무시 폴더라 커밋되지 않는다. 확인용 clone 은 지웠다.
- worktree 밖에 작업자용 git worktree 5개가 남아 있다(`C:\workspace_app\Ironwall.Dotnet.Libraries\.claude\worktrees\agent-*` — 그 안의 `_D` · `_e_mirror` 작업 사본 포함). 규칙상 worktree 밖은 지우지 않았다 — 하네스 gc 또는 사람이 지운다.
- 실기(헤디드)는 하지 않았다 → HEADED.md.
