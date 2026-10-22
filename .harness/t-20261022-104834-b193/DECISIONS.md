# 갈림길 기록 — t-20261022-104834-b193

## 1. 기존 시험 `Map3DTwinContractTests` 의 "외벽 무늬 = 정확히 옛 7개" 단정
- options: (a) 단정을 새 계약(옛 7개 포함 + 전체 22개)으로 바꾼다 (b) `Map3DLook.Facades` 는 7개로 두고 새 목록을 따로 만든다
- chosen: (a)
- why: 목표가 "모델 · 정규화(Map3DLook.Facades · NormalizeFacade) … 에 새 키"를 직접 지시했다. 옛 7개가 남는지는 그대로 단정하고(Subset), 차례 · 전체는 새 시험이 더 엄격하게 본다. (pattern: follow-recommended)
- effect: `tests/GMaps.Ui.Tests/Map3DTwinContractTests.cs` 한 줄이 두 줄로. 되돌리려면 그 줄만 옛 단정으로.

## 2. 엔진이 내장 목록에 없는 새 무늬 키를 보내면
- options: (a) 엔진 목록의 키도 정규화에서 살린다(받은 키 집합을 기억) (b) 내장 22개만 살리고 엔진 목록은 이름 · 분류만 쓴다
- chosen: (a)
- why: 계약이 "GIS 가 목록을 엔진에서 받아 쓴다"이므로 엔진이 무늬를 더하면 GIS 를 다시 배포하지 않아도 고를 수 있어야 한다. 키 모양(소문자 · 숫자 · 밑줄 40자)만 검사한다. (pattern: broad)
- effect: `Map3DLook.UseEngineFacades` · `NormalizeFacade`. 막으려면 `UseEngineFacades` 호출(MapViewModel.BuildingLook `LoadMap3DFacadesAsync`)을 빼면 된다.

## 3. 옛 7개 무늬의 보이는 이름
- options: (a) 엔진 facades.CATALOG 이름(상가 · 사무 · 공장 · 창고 · 유리 커튼월 · 노출 콘크리트)으로 맞춤 (b) GIS 옛 이름(사무 · 공장 · 유리 · 콘크리트) 유지
- chosen: (a)
- why: 엔진 목록을 받은 경우와 옛 엔진 폴백(내장) 경우에 같은 말이 보이게. industrial 은 엔진처럼 「공장」 분류에 넣었다. (pattern: reuse)
- effect: `Map3DLook.FacadeCatalog` 의 글자만 바꾸면 된다.

## 4. 외벽 무늬 목록을 언제 받나
- options: (a) 영역 목록을 받을 때 한 번(실패 = 다음 영역 목록 때 다시, 옛 엔진 = 내장으로 확정) (b) 판을 열 때마다
- chosen: (a)
- why: 판이 열릴 때 목록이 바뀌면 콤보가 깜빡이고, 영역 목록은 시작 · 다시 연결 · 저장 뒤마다 이미 받는다. 기다리지 않고 따로 받는다(기다리면 그 함수를 await 하는 호출자가 UI 스레드를 벗어났다 — 시험으로 확인). (pattern: speed)
- effect: `MapViewModel.Map3DAreas.RefreshMap3DAreasAsync` 의 `_ = LoadMap3DFacadesAsync()` 한 줄.

## 5. .iw3d 가져오기 multipart 칸 이름 · GIS 쪽 검사
- options: (a) 칸 이름 `file`(엔진 모델 넣기와 같은 관용) + 올리기 전에 GIS 가 zip · manifest.json format 을 본다 (b) 검사 없이 엔진에 맡긴다
- chosen: (a)
- why: 엔진의 패키지 API 는 아직 없다(엔진 저장소 8cacc6e 기준 — 외벽 무늬 작업 중). 계약은 "multipart zip"만 적어 FastAPI 관용 `file` 을 골랐다. 최대 2 GB 를 올리기 전에 엉뚱한 파일을 쉬운 말로 거르는 게 사용자에게 낫다. 해시 · 경로 순회 · 판 검사는 엔진 몫. (pattern: follow-recommended)
- effect: `Map3DClient.ImportPackageAsync`(칸 이름) · `Map3DPackage.Inspect`. 엔진이 다른 이름을 쓰면 그 문자열 하나만 바꾼다.

## 6. 내보내기 · 가져오기에 편집 모드가 필요한가
- options: (a) 지도 편집 권한만(메뉴도 권한이 있을 때만 보임) (b) 편집 모드까지
- chosen: (a)
- why: 목표가 "편집 권한일 때만". 같은 메뉴의 다시 만들기 · 지우기도 편집 모드를 묻지 않는다. (pattern: follow-recommended)
- effect: `MapViewModel.Map3DPackage` 의 `CanEditMap()` 검사 · MapView.xaml `Visibility=CanMapEdit`. 편집 모드를 더하려면 `RequireMapEditMode(notify: true)` 한 줄.

## 7. 진행 표시 방식
- options: (a) 지도 위 상태 줄(SetAimStatus)에 "3D 영역 내보내는 중… 42%" (b) 별도 진행 창
- chosen: (a)
- why: 영역 지우기 · 다시 만들기 · 줌 바꾸기 결과가 모두 이 줄이다. 새 창은 기존 오버레이 창 디자인을 새로 맞춰야 한다. 받는 중에는 `<파일>.part` 에 쓰고 다 받은 뒤 바꿔 놓아 반쪽 파일을 남기지 않는다. (pattern: reuse)
- effect: `PackageProgressReporter`. 창으로 바꾸려면 그 클래스만 바꾸면 된다.

## 8. 넣은 모델 — 처음 자리 · 2D 에서의 모습
- options: (a) 화면 가운데(영역 밖이면 엔진 기본 = 영역 가운데), 2D 지도에는 그리지 않고 판 목록만 (b) 2D 에서 클릭으로 자리 찍기
- chosen: (a)
- why: 목표가 "3D 에서 손잡이로 자리 · 방향 · 크기". 2D 지도 위 새 끌기는 drag-first 규칙상 만들지 않고(꾸미기와 같은 관용), 자리 · 방향 · 크기 숫자 칸도 만들지 않았다 — 버튼 폴백은 3D 판의 ↑↓←→ · ↺↻ · [크게 +][작게 −] · [✓ 확정](엔진 edit.key). (pattern: follow-recommended)
- effect: `MapViewModel.Model3D.AddModelAsync` · DecorationPanelStyle 「넣은 모델」 칸.

## 9. 넣은 모델 지우기의 되돌리기
- options: (a) 지우기 전에 원본(GET …/file)을 받아 편집 기록에 두고, 되돌리면 같은 자리 · 이름 · 크기로 다시 올림(새 uid → 옛 uid 를 따라가는 표) (b) 지우기는 되돌리지 못하게(확인 창)
- chosen: (a)
- why: 목표가 "목록 · 지우기 · 되돌리기". 엔진 uid 는 서버가 만들어 되살리면 바뀌므로, 앞뒤 기록(옮기기 · 이름)이 같은 모델을 가리키게 옛 → 새 uid 를 기억한다. 원본을 못 받으면 지우되 "되돌릴 수 없습니다"를 알린다. 되돌리기 설명은 「꾸미기 · 모델 …」 — 꾸미기 판의 Ctrl+Z · [되돌리기] 가 함께 되돌린다. (pattern: broad)
- effect: `ModelCommand`(AreaStateCommand) · `ApplyModelStateAsync` · `ResolveModelUid`. 파일을 기록에 들고 있으므로(모델당 50 MB 까지) 메모리가 걱정되면 지우기 기록만 빼면 된다.
