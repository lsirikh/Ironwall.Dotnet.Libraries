# PLAN — 프리셋 ROI 설정 화면 (t-20261004-094349-5a91)

## 목표
프리셋 관리 창에서 프리셋의 ROI 를 설정할 수 있는 화면. ROI 는 마우스 클릭으로 점 · 선을 찍어 다각형을 만든다.
서버 계약: 카메라 → 프리셋 → ROI → 점 (`/presets/{pid}/rois`, ROI 생성 · PUT 은 points 포함 전체 본문).

## 서버 계약(시험 서버 app/schemas/camera_preset.py · routers/rois.py 로 확인)
- GET  `/presets/{pid}/rois?include_points=true&limit=100` → data `{items,total}`(또는 `[]`), 점은 order 순
- POST `/presets/{pid}/rois` `{name, resolution_width, resolution_height, is_enable, [is_exclusion], points:[{x,y,order}]}` — 점 ≥3, order 유일, **extra=forbid**(모르는 키 422)
- PUT  `/presets/{pid}/rois/{rid}` 같은 본문 = 전체 교체(점까지). PATCH 는 점을 못 바꿔서 쓰지 않는다
- DELETE `/presets/{pid}/rois/{rid}`
- x,y 는 "0~1 정규화 또는 픽셀" — 서버가 정하지 않는다

## 결정
1. **별도 창**(`MappingRoiEditorView/ViewModel`, "프리셋 ROI 설정")을 프리셋 관리 창 폼의 **[ROI 설정…]** 로 연다 —
   프리셋 관리 창(L 720)에 캔버스를 끼우면 너무 좁다. 창을 띄우는 길은 워크벤치 → 프리셋 관리 → ROI 로 `Func<…,Task>` 를 이어 준다(기존 ShowPresetManager 와 같은 방식).
2. 서버 왕복은 새 인터페이스 `IMappingRoiEditor`(기존 `IMappingPresetEditor` 를 넓히지 않는다 — 페이크가 깨진다). `MappingWorkbenchGateway` 가 함께 구현.
3. 좌표: 화면은 **0~1 정규화**로 다룬다. 서버에서 읽은 ROI 의 점이 하나라도 1 을 넘으면 픽셀로 보고 해상도로 나눠 그리고, **저장 때 읽은 단위(픽셀)로 되돌려** 보낸다(다른 소비자가 쓰는 단위를 바꾸지 않는다). 새 ROI 는 정규화.
4. `is_exclusion` 은 신판 서버에만 있다(구판은 extra=forbid 라 422). **읽었을 때만** 같은 값으로 되돌려 싣는다(PUT 전체 교체로 값이 지워지지 않게). 화면 토글은 범위 밖.
5. 해상도: 기존 ROI 는 읽은 값 유지, 새 ROI 기본 1920×1080. 폼에서 고칠 수 있다(캔버스 비율 = 해상도 비율).
6. 그리기: 빈 곳 클릭=점 추가(선 자동 연결, 마지막 점→마우스 미리보기 선) · 첫 점 클릭 또는 더블클릭/Enter=닫기(점 ≥3) ·
   우클릭/Backspace=마지막 점 지우기 · Esc=그리기 취소 · 닫힌 뒤엔 꼭짓점을 **끌어서** 옮긴다(드래그 우선 방침). 선이 서로 겹치는 다각형은 저장 거부.
7. **배경 스냅숏은 이번 범위에서 하지 않는다.** 호스트 프로토콜에 스냅숏 요청이 없고(CameraRequestKind 0~9), 1MB 프레임 한도 ·
   ICameraPtzProvider 확장 · 가짜 카메라 팜 미지원이라 별도 작업이 된다. 캔버스는 해상도 비율의 격자 위에 그린다. (남은 일 참고)
8. 다른 ROI 는 같은 캔버스에 흐리게 그려 겹침을 보게 한다.

## 진행
- [x] 계약 · 기존 코드 조사
- [x] MappingContracts: `MappingRoiPoint` · `MappingRoiDetail` · `MappingRoiDraft` · `IMappingRoiEditor` · `MappingRoiGeometry`(겹침 · 넓이 · 자르기)
- [x] MappingWorkbenchGateway: ROI 왕복(List/Create/Replace(PUT)/Delete) · `RoiBody` · `ReadRoi`(픽셀 판별)
- [x] MappingRoiEditorViewModel(그리기 판정 전부 · 저장 · 삭제 확인 줄)
- [x] MappingRoiCanvas(OnRender, 테마 붓은 DP+DynamicResource) · MappingRoiEditorView(+코드비하인드: 뷰 PreviewKeyDown 이 틀보다 먼저 Esc/Enter/Backspace 를 캔버스에 준다)
- [x] 프리셋 관리 창 [ROI 설정…](편집 권한 없어도 보기 가능, 새 프리셋은 비활성) · 워크벤치 `ShowRoiEditor` → 매니저 전달 · 런처 `RoiEditorSettings`(L 폭, 820 높이) · 도움말 `Events.Mapping.Rois`
- [x] 헤드리스 시험 `Events.Ui/Tests/MappingRoiEditorTests.cs`(27건) — Events.Ui 매핑 시험 71건 통과, Utils HelpContract/ConsoleStyle 113건 통과, 솔루션 빌드 오류 0

## 발견
- 서버 ROI 목록 기본 limit=10 → limit=100 으로 받는다.
- 생성 · 교체 응답은 점을 싣지 않는다(point_count 만) → 점은 보낸 값으로 채운다.
- ConsoleDialogFrame 은 OnPreviewKeyDown 에서 Esc 를 닫기로 먹는다 → 캔버스 키는 뷰(틀의 조상) PreviewKeyDown 에서 먼저 처리.
- 새 파일은 Write 도구가 BOM 없이 쓴다 → 첫 글자에 U+FEFF 를 넣어 UTF-8 BOM 확보(od 로 확인).

## 남은 일 / 검증 못 한 것
- 배경 스냅숏(호스트 GetSnapshot 요청 신설) — 별도 작업.
- 실서버 왕복(시험 서버 /presets/{id}/rois) · 실창 마우스 조작은 이 세션에서 돌리지 못함(사람 없는 실행 · 데스크톱 시험 금지). 헤드리스 VM/본문 시험만.
