# PLAN — t-20261022-104834-b193 (외벽 무늬 22종 · .iw3d 내보내기/가져오기 · 외부 모델 넣기, GIS 쪽)

## 목표
사용자 원문(2026-10-22): 공장 · 발전소 · 군사시설 위주의 외벽 디자인 10종 이상, 3D 지도를 한 개의 파일로 내보내 다른 GIS 에 가져오기, 3D 모습을 다른 곳에서 주입.
계약: `C:\Users\gh\.claude\projects\c--workspace-app-Ironwall-Dotnet-Libraries\handoff-2026-10-21\facade-package-contract.md`(A 외벽 무늬 · B .iw3d).
엔진(map-3d)은 건드리지 않는다. 엔진 상태(시작 때 확인): 8cacc6e = 넣은 모델 API 완료, 외벽 무늬 15종 · `GET /api/facades` 는 미커밋 작업 중(facades.py CATALOG 확인), 패키지 API(`/package` · `/api/jobs/import`)는 아직 없음 → 계약 키대로 GIS 만.
"건물 하나씩 · 한꺼번에 지우기"는 앞 작업(t-20261022-093356-2dae 등)에서 이미 끝남 — 이번엔 손대지 않음.

## 진행
- [x] 1. 외벽 무늬 — `Map3DLook.FacadeCatalog`(22개, 엔진 CATALOG 와 같은 차례 · 이름 · 분류) · `Facades`(콤보 항목) · `GroupText` · `ParseFacades` · `UseEngineFacades` · `NormalizeFacade`(엔진 키도 살림).
      `Map3DClient.ListFacadesAsync`(GET /api/facades, 404 → null). VM `LoadMap3DFacadesAsync`(MapViewModel.BuildingLook — 영역 목록을 받을 때 한 번, 기다리지 않음).
      두 판(「건물 높이」 · 「건물 만들기」) 콤보 = 판마다 ListCollectionView + 분류 머리글(GroupStyle `FacadeGroupStyle`, BuildingHeightPanelStyle.xaml), `IsSynchronizedWithCurrentItem=False`.
      항목을 갈 때 콤보가 되쓰는 null 은 세터가 무시(UserBuildingFacade 에 null 가드 추가).
- [x] 2. .iw3d — `Utils/Map3DPackage.cs`(파일 이름 · 거르개 · GIS 쪽 검사 Inspect · 응답 ParseImport · 쉬운 말 · 진행 글),
      `Map3DClient.DownloadPackageAsync`(GET …/package → 스트림, 진행) · `ImportPackageAsync`(POST /api/jobs/import multipart `file`, 진행).
      VM `MapViewModel.Map3DPackage.cs` — 영역 메뉴 「이 영역 내보내기…」(Map3DAreaItem export 행동, 편집 권한일 때만, 권한 바뀌면 ReapplyMap3DAreas) ·
      지도 메뉴 「3D 영역 가져오기…」(MapView.xaml `GMaps.Map3D.Import`, Visibility=CanMapEdit). 진행 · 결과 · 거절 = 상태 줄. `.part` 에 받고 끝나면 바꿔 놓음.
- [x] 3. 외부 모델 넣기(엔진 8cacc6e 키) — `Utils/Map3DModels.cs` · `Map3DClient` List/Upload/Put/GetFile/DeleteModel · `ModelCommand`(Undo) ·
      VM `MapViewModel.Model3D.cs`(꾸미기 판 안 「넣은 모델」 칸: [모델 넣기…] · 목록 · 이름 · 지우기 · 되돌리기, 3D model.pick → 3D 위 판 · edit.start kind model,
      edit.commit(scale 포함) → PUT, 판 [크게 +][작게 −] = edit.key + −). `Map3DEdit`(TargetModel · Scale · 키 + −) · `Map3DMath`(model.pick → ModelUid).
      꾸미기 항목 고름 · 도구와 모델 고름은 서로 배타. Esc 단계 · Delete 키에 모델 포함.
- [x] 4. 도움말("?") — 「건물 높이」 외관 · 「건물 만들기」 외벽 무늬 문구, 「꾸미기」 판에 「모델 넣기」 절.
- [x] 5. 시험 — 아래.

## 고친 결함(작업 중 발견)
- 외벽 목록 받기를 `RefreshMap3DAreasAsync` 안에서 await 하자, 그 함수를 await 하는 호출자(다시 연결 · 시험)의 뒷부분이 HTTP 응답 스레드에서 이어져
  UI 스레드를 벗어났다(시험: 영역 목록 갱신 뒤 3D 진입이 "2D" 로 남음). 증명: 외벽 목록 로딩을 건너뛰게 한 탐침 시험은 통과, 로딩하면 실패 →
  `_ = LoadMap3DFacadesAsync()`(기다리지 않음)로 바꾼 뒤 같은 탐침 통과(탐침 파일은 지움).

## 시험(이 세션에서 돌린 명령 · 결과)
- `dotnet test tests/GMaps.Ui.Tests/GMaps.Ui.Tests.csproj --filter "FullyQualifiedName~Map3D"` → 942 중 941 통과, 1 실패(새 시험의 record 비교 실수) → 고친 뒤 7 프로젝트 전수에서 확인.
- `dotnet test tests/GMaps.MapEdit.Tests/GMaps.MapEdit.Tests.csproj --filter "FullyQualifiedName~TwinContractScenarioTests"` → 51/51 통과(새 시나리오 7개 포함).
- 7 프로젝트 전수: 결과는 아래 「전수 결과」.
- 새 시험 파일
  - `tests/GMaps.Ui.Tests/Map3DFacadePackageModelTests.cs` — 22개 차례 · 분류, ParseFacades, 엔진 새 키 정규화, 가짜 HTTP 로 /api/facades 받기 · 옛 엔진 · 닿지 않음,
    파일 이름, Inspect(정상 · 다른 zip · manifest 없음 · zip 아님 · 2 GB 초과), import 응답 형태, 내보내기 받기 · 진행 · 옛 엔진, 가져오기 multipart · 거절,
    모델 항목 · 본문 키 · 메시지 · 파일 검사 · 엔드포인트 모양, model.pick · edit.commit scale.
  - `tests/GMaps.MapEdit.Tests/FacadePackageModelScenarioTests.cs`(TwinContractScenarioTests partial, FakeEngine.Extra 고리로 가짜 엔진 확장) —
    엔진 목록 → 두 판 콤보 머리글 · 고른 값 유지 · 엔진 새 키, 옛 엔진 폴백 22개 · 새 키 저장 본문, 내보내기(기본 이름 · 파일 내용 · .part 없음 · 옛 엔진 · 취소),
    가져오기(다른 zip 거름 · multipart · 충돌 문구 · 영역 목록 갱신 · 거절 · 옛 엔진), 권한 없음(메뉴 없음 · 요청 없음),
    모델(틀린 파일 · 넣기 자리 · 이름 · 되돌리기/다시 하기 · 되살린 uid 따라가기 · 3D 고름 · 손잡이 저장 · 영역 밖 원위치 · 판 크기 단추 · 지우기 원본 받기 · 되살리기 · edit.end · 배타 · 옛 엔진).
- 기존 시험 한 줄 변경: `Map3DTwinContractTests` 외벽 7개 고정 → 옛 7개 포함 + 22개(DECISIONS 1).

## 전수 결과(고친 뒤, 이 세션 — `dotnet test tests/<프로젝트>/<프로젝트>.csproj` 차례로, 모두 실패 0)
| 프로젝트 | 통과 |
|---|---|
| GMaps.Ui.Tests | 1859 |
| GMaps.MapEdit.Tests | 916 |
| GMaps.MapNav.Tests | 70 |
| GMaps.MapLayer.Tests | 102 |
| GMaps.MapAux.Tests | 122 |
| GMaps.PropertyPanel.Tests | 436 |
| GMaps.Housing.Tests | 362 |
| (솔루션 안에서 GMaps.Ui 를 참조하는 유일한 프로젝트) Ironwall.Dotnet.Libraries.CameraPopup.Tests | 334 |

GMaps 프로젝트들은 `Ironwall.Dotnet.Libraries.sln` 밖이라 저장소 검사(`dotnet test` sln)는 이 변경을 CameraPopup.Tests 로만 본다.
소리가 나는 Sounds · 다른 PC DB 에 쓰는 Db 시험이 들어 있어 sln 전체 시험은 돌리지 않았다(AGENTS.md 사용자 작업 방식).

## 남은 것 · 이어 갈 사람에게
- 엔진 패키지 API(`GET /api/jobs/{id}/package` · `POST /api/jobs/import`)가 엔진에 들어오면 실제 엔진으로 내보내기 → 다른 데이터 폴더 엔진에 가져오기를 한 번 확인할 것.
  multipart 칸 이름은 `file` 로 가정(DECISIONS 5) — 엔진이 다르면 `Map3DClient.ImportPackageAsync` 의 문자열 하나.
- 화면을 띄우는 실앱 확인(헤디드)은 하지 않았다 — 판 템플릿은 시험이 실제로 그려 확인(AssertRendered).
- 관련 코드: `Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/{Map3DLook,Map3DPackage,Map3DModels,Map3DEdit,Map3DMath}.cs`, `Services/Map3D/{Map3DClient,Map3DAreaItem}.cs`,
  `Services/Undo/Commands/ModelCommand.cs`, `ViewModels/Maps/MapViewModel.{BuildingLook,Map3DPackage,Model3D,Decoration,Edit3D,Map3DAreas,Map3D,UserBuilding}.cs`,
  `Themes/{BuildingHeightPanelStyle,UserBuildingPanelStyle,DecorationPanelStyle}.xaml`, `Views/Maps/MapView.xaml`, `Help/MapsHelp.cs`.
