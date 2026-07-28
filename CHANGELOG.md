# Changelog

<!-- changelog-entries-start -->

## [2.7.1] - 2026-06-04

### Added
- **SymbolUpdate_DispatcherFreeze_Fix PRD** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) · [Plan](docs/plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md))

## [2.7.0] - 2026-06-04

### Added
- **Multisensor_Symbol_Fix PRD** ([PRD](docs/prds/Multisensor_Symbol_Fix-prd.md) · [Plan](docs/plans/Multisensor_Symbol_Fix-prd-plan.md))

## [Unreleased]

### Changed
- **라인·구역·PIDS 그룹 드로잉 HUD 리디자인 + 위치 유지 버그 수정** ([PRD](docs/prds/line-drawing-hud-redesign-prd.md) · [Plan](docs/plans/line-drawing-hud-redesign-prd-plan.md) · [프리뷰](docs/design/line-drawing-hud-redesign-preview.html) · 태그 `before-line-drawing-hud-redesign` · GMaps.Ui 한정 · 메인솔루션 변경 0)
  - **HUD 재디자인(FR-01~07)**: 그리는 중 뜨는 흰색 알약형 플로팅 컨트롤을 **표준 오버레이 패널 패턴**(시안 헤더 + **우상단 X 닫기** + 다크/라이트 카드)으로 교체. 코드-하드코딩 브러시 → 신규 templated `LineDrawingHudControl` + `LineDrawingHudStyle.xaml`이 `DesignTokens.xaml`의 `PanelCloseButtonStyle`/`PanelPrimaryButtonStyle`/`PanelSecondaryButtonStyle`·`DynamicResource` 토큰 재사용 → **런타임 테마 전환 자동 반영**. 헤더=아이콘(VectorLine)+심볼 종류 타이틀(라인/구역/PIDS 그룹)+X. 본문=점 칩+거리 / **완료**(Primary)+**되돌리기**(Secondary). 취소는 헤더 X(Esc). Line·Area·PIDS 그룹 3종 공유 어도너라 한 번에 적용.
  - **위치 유지 수정(FR-08)**: 드래그로 옮긴 HUD가 다음 점 클릭·팬/줌마다 `firstPoint+(20,−50)`로 되돌아가던 버그(`UpdateControlUI`가 저장된 `_controlPosition`을 무시하고 하드코딩 재배치) → `_hasBeenDragged`+절대좌표 고정: 최초는 오프셋 배치, **한 번 드래그하면 그 화면 위치를 그리기 종료까지 유지**(팬/줌 무관), `Clear()`에서 초기화.
  - **지도 표식 토큰화 + 정리(FR-06/09)**: 시작점=StatusNormal·끝점=Accent·꼭짓점=Primary·미리보기=Muted 점선(`TryFindResource` 1회 캐싱), `OnRender` 매 프레임 Pen 재할당 제거(캐싱), 죽은 named 핸들러/`_controlPosition` 제거, `HitTestCore`를 HUD 바운즈로 재타깃(지도 클릭 통과 불변식 보존), `OnMapChanged` Dispatcher 가드. **Esc = 취소로 정렬**(헤더 X와 일치, 완료는 Enter 전용).
  - **완성 심볼 시작점 마커 제거(Track B)**: `LineMarkerStyle.xaml`의 `PART_EndpointMarkers`/`PART_StartPointMarker`(끝점 마커 제거 후 남은 위치 미지정 유령 초록점, IsClosedPath=Area마다 상시 표시)와 관련 트리거·잔여 주석 제거.
  - **검증**: GMaps.Ui 빌드 0오류 · XAML(BAML) 컴파일 OK · 신규 .cs/.xaml UTF-8 BOM. ⚠앱 재빌드 후 런타임 육안(다크/라이트 토글 + Line/Area/PIDS 3종 그리기·완료·취소·되돌리기·드래그 유지).

### Fixed
- **PidsGroup(경계선 그룹) 추가 시 첫 맵 클릭이 무효화되던 버그** (Track B · 태그 `before-pidsgroup-firstclick-fix` · GMaps.Ui 한정 · 메인솔루션 변경 0)
  - **원인**: Fence_Group(PidsGroup)이 단일-점 PIDS 장비와 함께 `EnumMarkerCategory.PIDS_EQUIPMENT` 배치 모드(placement mode) 경로로 분류(`MapViewModel.ExecuteAddSelectedSymbol`)돼, 다점 경계선인데도 **첫 맵 클릭이 "배치 클릭"으로 소비**되어 그리기 시작만 시키고(클릭 좌표 무시) 두 번째 클릭부터 꼭짓점으로 인식됨. Area/Line은 배치 모드 우회·직접 시작이라 무증상.
  - **수정(Option A)**: PIDS_EQUIPMENT 분기에서 `deviceType == Fence_Group`이면 배치 모드 대신 `AddPidsGroupMarker`로 **곧바로 라인 드로잉 시작**(Area/Line과 동일) → 첫 클릭이 첫 꼭짓점. 나머지 단일-점 PIDS 장비는 배치 모드 유지.
  - **검증**: GMaps.Ui 빌드 0오류. ⚠앱 재빌드 후 런타임 육안(PidsGroup 추가 → 첫 클릭부터 점 인식).
- **편집 모드 해제 시 진행 중 라인/구역/PIDS그룹 드로잉이 정리되지 않던 버그** (Track B · 태그 `before-editmode-off-drawing-cancel` · GMaps.Ui 한정 · 메인솔루션 변경 0)
  - **원인**: MapEdit 모드 OFF 경로(`IsEditModeEnabled` 세터 / `GMapCustomControl.SetEditMode(false)`)가 선택·러버밴드·마커편집 드래그·배치 모드는 정리하지만 **`LineDrawingService`(드로잉)는 취소하지 않음** → 드로잉 HUD·맵 클릭 라우팅(`IsLineDrawing` 패스트패스)·Cross 커서·VM 상태가 orphan으로 잔존. aim/배치 전환은 `CancelDrawingAsync`로 취소하는데 편집-OFF 경로만 누락.
  - **수정**: `IsEditModeEnabled` 세터(모든 해제 경로의 단일 게이트) 해제 분기에서 `IsLineDrawing`이면 `CancelDrawingAsync()` 호출 + `IsLineDrawing=false`/`LineDrawingStatus=""` 리셋(이벤트 핸들러 `OnLineDrawingCancelled`는 VM 플래그 미리셋이라 명시).
  - **검증**: GMaps.Ui 빌드 0오류. ⚠앱 재빌드 후 런타임 육안(드로잉 중 편집 모드 OFF → HUD/커서/클릭 라우팅 즉시 정리).
- **레이어 패널 부모(그룹/카테고리) 체크박스가 재오픈 시 자식과 어긋나 다시 체크되던 버그** (Track B · 태그 `before-layerpanel-parent-check-fix` · GMaps.Ui 한정 · 메인솔루션 변경 0 · 연관 [LayerVisibility_Persistence_Fix PRD](docs/prds/LayerVisibility_Persistence_Fix-prd.md) FR-4)
  - **원인(회귀)**: `LayerTreeNode._isChecked` 기본값 true + 부모 팩토리(CreateGroup/CreateCategory/CreateSection)가 IsChecked 미세팅. 패널을 열 때마다 `LoadLayersFromDbAsync`가 트리를 재빌드하는데, 재빌드 후 유일한 집계 `AggregateLeafCheckedFromMarkers`는 필터 `Model.LayerType=="Symbol"`이라 **개별 심볼 leaf(Model==null)를 제외** → 부모 tri-state가 자식(영속된 `symbol.Visible`)으로부터 재계산되지 않고 기본 true로 부활. leaf는 정상 언체크라 desync. **영속 자체는 정상**(부모 체크는 파생 표시값, 비영속). LayerVisibility_Persistence_Fix(2026-06)가 세운 부모 집계를 **개별 심볼 트리노드 기능(`367e6f0`, 2026-06-30)이 회귀시켰고 미검출**(OVERLAY IMAGE만 별도 rollup 유지).
  - **수정**: `LayerTreeNode.RecomputeCheckStateBottomUp()`(부작용 없는 자식→부모 상향 재계산, 세터 우회로 push-down/CheckChanged/Model.IsVisible/DB 영속 미유발) 추가 + `LoadLayersFromDbAsync` 재빌드 직후 1회 호출. PRD §8 "마커→leaf 역방향 금지"와 무관(node↔node 정방향).
  - **검증**: GMaps.Ui 빌드 0오류. ⚠앱 재빌드 후 런타임 육안(PIDS 그룹 언체크→닫기→재오픈 시 그룹=언체크/indeterminate, leaf 숨김 유지).

### Added
- **지도 측정 툴 — 길이/넓이 재기 (Top 메뉴 아이콘 + 지오데식 계산)** ([PRD](docs/prds/Measure_Tools-prd.md) · [Plan](docs/plans/Measure_Tools-prd-plan.md) · [스토리보드](docs/design/Measure_Tools_Storyboard.html) · 태그 `before-measure-tools` · worktree `feature/measure-tools` · GMaps.Ui 한정 · 메인솔루션 변경 0)
  - **기능**: 툴바 "측정" 그룹에 길이(Ruler)·넓이(VectorSquare) 토글. 지도 클릭으로 점 추가, 더블클릭/Enter 완료, ESC 취소, Backspace/Ctrl+Z 마지막 점 취소. 길이=폴리라인+구간 거리라벨, 넓이=닫힌 다각형 채움+면적 중심라벨. 임시 오버레이(DB 미저장·마커 미생성). aim/배치/라인드로잉과 상호배제(편집 모드 독립).
  - **계산(FR-01/02)**: 위경도 도메인 — 거리=Haversine, 면적=지오데식 구면초과 shoelace(Google computeArea 방식), R=6378137(GMap.NET Axis). 화면 픽셀 shoelace 금지(MBTiles EPSG:3857 이중 왜곡 회피). 단위 자동전환 m/km·m²/ha/km².
  - **디자인(테마 대응)**: 모든 색을 **Tactical Command 테마 토큰**(`TryFindResource`)으로 해석해 라이트/다크 자동 대응 — 채움=`TintAccentBrush`, 라벨칩=`SurfaceTranslucentBrush`, 선/정점=`PrimaryBrush`. 수치 리드아웃 HUD는 `MapFloatingPanelStyle`+`DynamicResource`(스케일바/줌/좌표 HUD와 동일 패턴·테마 자동스왑).
  - **구현**: `Utils/MeasureMath`·`MeasureFormat`(순수)+`Adorners/MeasureAdorner`(맵 어도너, 완전 클릭스루 불변식#3)+`Services/MeasureController`(수명주기·리드아웃 통지)+`GMapCustomControl` 라우팅(IsMeasuring 분기·Start/Stop/Finish/Undo)+`MapViewModel.Measure`(토글 명령·HUD 바인딩·윈도우 키후킹)+`MapView.xaml`(측정 툴바 그룹+리드아웃 HUD).
  - **검증**: GMaps.Ui 빌드 0오류 · `MeasureMathTests` 16/16 통과(known-value 거리·평면근사 면적 0.5%·와인딩 무관·임계 포맷). ⚠앱 재빌드 후 런타임 검증(클릭 점추가·줌/팬 앵커 고정·라이트/다크 색·완료/취소 키).
- **카메라 RTSP 팝업 통합 제어 허브 — 드래그 이동 + 위치 기억 + 개별/전체 제어** ([PRD](docs/prds/CameraPopup_ControlHub-prd.md) · [Plan](docs/plans/CameraPopup_ControlHub-prd-plan.md) · [스토리보드](docs/design/CameraPopup_ControlHub_Storyboard.html) · 태그 `before-camerapopup-controlhub` · GMaps.Ui+GMaps.Db)
  - **기능**: 맵 우하단 카운터 위젯을 **드래그로 옮기고 위치가 기억되는** 플로팅 허브 CustomControl로 교체. 접힌 pill(그립+CCTV+개수 뱃지+chevron) 클릭 → 플라이아웃(열린 카메라 리스트): **행 클릭=이동/포커스**(맨앞+선택), **행 ✕=개별 닫기**, **하단=모두 닫기**(표준 확인팝업). 0개면 허브 숨김.
  - **드래그+위치 영속**: 본체 드래그(8px 데드존·경계 clamp) → 종료 시 화면 좌표를 GMapDb 저장, 재시작 복원(RTSP 팝업 위치 기억 방식 답습). 화면 고정 좌표(맵 팬/줌 불변). 전부 라이브러리 — 메인솔루션 변경 0.
  - **구현**: `CameraPopupControlHub`(CustomControl)+`CameraPopupControlHubStyle`(유리질 Tactical·self-contained 색·WPF Popup 플라이아웃=airspace 위)+`ICameraPopupHubPositionStore`/`Store`(Semaphore+인메모리 폴백)+`CameraPopupHubMath`(순수)+GMapDb `CameraPopupHubPosition` 1행 테이블·DAL+MapViewModel `Focus`/`Close`/`SaveHub` 커맨드. 리스트/개수/모두닫기/상태는 기존 `CameraPopups` 자산 재사용(위 3건 PRD의 카운터 위젯 계승·확장).
  - **검증**: GMaps.Db+GMaps.Ui 빌드 0 · 테스트 **217/217**(`CameraPopupHubMathTests` 10 신규 소스링크). ⚠앱 재빌드 후 런타임 검증(드래그·기억·이동·개별닫기·모두닫기).
- **카메라 영상 팝업 3건 — 상하 패닝 추종 버그 + 카운터 위젯(전체 닫기) + ONVIF 프리셋/PTZ 반응성** ([PRD](docs/prds/CameraPopup_PanClamp_Badge_OnvifPtz-prd.md) · [Plan](docs/plans/CameraPopup_PanClamp_Badge_OnvifPtz-prd-plan.md) · [분석](docs/analyses/CameraPopup_PanClamp_Badge_OnvifPtz-analysis.md) — Explore 3기→architect→code-reviewer 적대검증 체인 · 태그 `before-camerapopup-3fix` · worktree `feature/camerapopup-3fix` · GMaps.Ui 한정, ⚠`IPtzController` 확장=메인솔루션 재빌드 필요)
  - **패닝 추종(FR-A)**: `CanvasTop` 세터 하한0 클램프(58b3fd7)가 맵 팬/줌 추종 경로까지 적용돼 위로 패닝 시 팝업이 상단에 붙어 딸려오던 버그 — 세터 클램프 제거(1줄). 드래그 경계 보호는 컨트롤 레벨 유지+하한을 `MinCanvasTop` 상수 참조로 단일화(OQ-1b). 과거 클램프로 저장된 앵커는 유지(재드래그 시 자연 교정, OQ-2a).
  - **카운터 위젯(FR-B)**: 맵 우하단(줌 컨트롤 좌측) 카메라 아이콘+열린 팝업 수 뱃지, 0개면 숨김(DataTrigger — 컨버터 불필요로 계획 대비 단순화). ✕ → 표준 확인 팝업(`OpenConfirmPopupMessageModel`+`CallCloseAllCameraPopupsProcessMessageModel` 콜백, raw MessageBox 금지) → `CloseAllCameraPopupsAsync` 순차 닫기(Hub Lease/PTZ 정지 포함). 기존 인라인 전체닫기 3곳(OnDeactivate/강제로그아웃/심볼 Reset)은 동작 차이가 의도적이라 미통합(OQ-B2).
  - **ONVIF 프리셋(FR-C)**: 프리셋 탭이 로컬 DB(CameraPtzPresets) 전용이라 카메라(장비) 저장 프리셋이 안 보이던 설계 불일치 → **ONVIF 전용 전환**(OQ-4a). `IPtzController`에 GetPresets/Goto/Set/Remove/SetHome/GotoHome 6메서드(전부 `ctx.Gate` 직렬 I-05, 워밍 ctx 재사용) + `OnvifPresetDisplayModel` 표시 어댑터(기존 XAML `PresetName` 바인딩·`IPtzPresetModel` 이벤트 시그니처 유지, 빈 이름 "프리셋 {token}" 폴백) + Home=ONVIF 전용 슬롯(행별 IsHome 폐기, [Home 지정/이동] 버튼=SetHome/GotoHome, OQ-6a) + 빈 목록 상태 문구 3종(준비 중/미지원/없음/조회 실패 — FR-C3, 기존 무음 빈 목록 제거). DB 경로(`PtzPresetStore`)는 코드 유지·팝업에서만 분리(OQ-5a 롤백 안전).
  - **PTZ 반응성 Phase 1(FR-L)**: 적대 검증으로 1차 가설(워밍 Gate 대기) 반증 — 패드는 `IsPtzCapable`까지 비활성이라 성립 불가. 수정 확정 인과 반영: ①**Stop LWW 확장**(FR-L1) — 뗌 Stop에 제스처 토큰 전달, 재누름이 Gate 대기 중 직전 Stop을 드롭(ONVIF §5.3.2 자동 대체)해 "매 누름이 직전 Stop SOAP 왕복을 기다리던" 지연 제거 + 이동 실패(비취소) 시 보상 Stop(R-1, 카메라 미정지 방지) ②**capable 시점 정렬**(FR-L2) — Onvif 소스모드는 in-flight GetStreamUri 완료 후 패드 활성("활성=즉시 이동 가능") ③**Stop 진단로그**(FR-L3) — gateWait/WCF 계측(Digest 401 재왕복 정량화 → Phase 2 판단 근거).
  - **검증**: 신규 `CameraPopupPanClampPresetTests` 10케이스 포함 in-project 253/254(실패 1=EditRecorder, v2.6 기준선 동일 red=회귀 0)·외부 GMaps.Ui.Tests 207/207 green·빌드 0오류·터치 파일 UTF-8 BOM 보정 7파일. ([PRD](docs/prds/Overlay_Title_ZoomStyle-prd.md) · [Plan](docs/plans/Overlay_Title_ZoomStyle-prd-plan.md) · [분석](docs/analyses/Overlay_Title_ZoomStyle-analysis.md) — 시뮬 1차 108건+2차 69건 근거 · 태그 `before-overlay-title` · worktree `feature/overlay-title` · GMaps.Ui/GMaps.Db/Monitoring.Models · 4스테이지 독립 커밋)
  - **줌 안정화(FR-01~04)**: `LabelAdorner`가 시각 footprint를 같은 프레임에 자체 투영(이미지=지오바운즈 Bearing 회전 AABB, 라인=RuntimePoints bbox, 점심볼=현행 유지) — 모델 W/H 역주입 의존 제거로 1프레임 stale 점프(최대 1,600px)·시작 시 오배치 해소. 이미지 오프셋은 **정규화 U/V**(하프익스텐트 비율, `Images.LabelOffsetU/V`)로 드래그된 라벨이 줌에서 상대위치 유지(시뮬 I 24/30 FAIL→0). 드래그 상한 `3·max(hw,hh)` 등방·줌 불변.
  - **스타일(FR-05~09)**: TitleColor/TitleBackground(packed ARGB int, DDL 부호 리터럴)·TitleFontFamily·TitleBold/Italic — 계약/마커/모델/DB(Symbols+Images CREATE+컬럼별 멱등 ALTER)/속성패널(색 팔레트 콤보·시스템 폰트 열거·그룹 pending)/undo 3중 스위치. 기본값=종전 하드코딩과 시각 동일(무변화 업그레이드). 심볼 영속=전용 부분 UPDATE(`UpdateSymbolLabelStyleAsync`, 판별자 오염 회피 선례).
  - **글자색/배경색 콤보 v2.3 수정(`1ce2614`)**: 초기 hex 편집 콤보가 `PropertyComboBox` 템플릿 편집파트 부재로 값 표시 깨짐+선택 즉시 미반영 → **기존 채우기/테두리 색 콤보 구조 그대로 차용**(스와치 40×12+이름, 글자 11색/배경 10색 팔레트 — 투명·반투명 칩 포함, 기본값=첫 항목).
  - **폰트 크래시 + 디자인 정합 v2.4(`04731a7`)**: 글꼴 콤보의 시스템 전체 폰트 열거(`Fonts.SystemFontFamilies`)가 앱 크래시 유발 → **큐레이션 12종**(한글 폰트=한글명·영문=영문명, 자기 폰트 미리보기, 미탑재=무음 폴백). 라벨 6행을 BASIC 중간에서 **COLOR 뒤 전용 LABEL 섹션**(구분선+대문자 헤더, 기존 섹션 컨셉 동일)으로 이동 — 속성창 디자인 이질감 해소.
  - **색 콤보 = 심볼 색상 콤보 재사용 v2.5(`7ad5255`)**: 글자색/배경색을 int ARGB → `EnumColorType`(FillColor 파이프라인·공유 AvailableColors 콤보 그대로) 전면 전환. DB VARCHAR(20), 초기 INT 스키마 MODIFY+숫자잔존 정리.
  - **제목 저장 전수감사 + 재시작 리셋 근본원인 2종(`d0bd165`·`0003332`)**: ①JOIN 타입 6종의 타입행 부재 시 전체 롤백→base Title 무음소실 → 자가치유 INSERT ②`OnMarkerPropertyChanged` async void 무가드 → try/catch ③파생 매퍼 6종이 스타일 컬럼 미매핑 → 저장돼도 재시작 리셋 → 8매퍼 전체 보정 ④제목 TextBox LostFocus→PropertyChanged+Delay(타이핑 후 맵클릭 시 커밋 누락 해소).
  - **라인/PidsGroup 라벨 오프셋 줌 고정 v2.6(`48493fe`, OQ-1 해소)**: 드래그한 라인/PidsGroup 라벨이 재시작엔 유지되나 줌 시 그룹 대비 어긋나던 문제 → 이미지와 동일 정규화(U/V, footprint 비율)로 전환해 줌 불변 고정. `Symbols.LabelOffsetX/Y`를 라인계열에서 비율로 재해석, 레거시 px(|v|>3) 1회 초기화(AREA_BOUNDARY·PIDS_GROUP). 점 심볼은 px 유지. 8타입 영속 왕복 전수감사(워크플로 9에이전트) 실결함 0 확인.
  - **폭 WYSIWYG(FR-13)**: `TitleMaxWidth`(px·기본 200=종전 값) — 편집모드 라벨 칩 좌/우 가장자리(`min(6px,25%)`) 드래그 **edge-pinned** 리사이즈(커서 추종+반대편 고정, 오프셋 Δ폭/2 보상, 40~800 클램프, 점선 최대폭 가이드) + (오프셋,폭) 원자 undo(`TitleWidthResizeCommand`) + 속성패널 숫자 입력.
  - **이미지 영속화(FR-10~11)**: 이미지 TitleSize/ShowTitle/오프셋이 재시작마다 리셋되던 P0(무음 유실) 해소 — `Images` 4+6컬럼, `GMapImageMarker` 필드→모델 위임+INPC(undo 후 즉시 재렌더), DB 실패 시 오프셋/폭 롤백.
  - **성능(FR-08/12)**: FormattedText/브러시/정적 Typeface 캐시 + PropertyName 필터(포함 20종 확정 — Bearing/ImageBounds 포함) + `LabelAdornerService` CollectionChanged 증분 O(1)+재진입 가드 + `GMapBaseMarker.Dispose` TOCTOU/무음 catch 정리.
  - **검증**: 라벨 테스트 37(수식 12=시뮬 이식·스타일/폭 13·가시성 12) green, 전체 242 중 241 green(1 red=FOVColor CMD-02, v2.6 기존 red 상속·무관). 빌드 0오류. 신규 파일 UTF-8 BOM.
- **세션 관리 패널 정리 — 표시 포맷 · 사용자 전체 세션 종료 · 기본 동작** ([Plan](docs/plans/GOP_SessionPanel_Cleanup-prd-plan.md) · 태그 `before-session-panel-cleanup` · worktree `feature/session-panel-cleanup` · Accounts.Api/Accounts.Ui/Utils · 라이브러리 한정) — **API 서버 무변경**(기존 서버 지원만 소비).
  - **표시**: 세션 날짜 컬럼(만료/로그아웃/로그인시각)이 `string?` ISO(+09:00)라 raw로 뜨던 것 → 신규 `IsoDateStringConverter`(Utils, `DateTime.TryParse`·파싱실패 시 원문 폴백)로 `yyyy-MM-dd HH:mm` 표시.
  - **기능**: **사용자 전체 세션 종료** 배선 — `IAccountApiService.ForceLogoutAllUserSessionsAsync`(**DIM `=> throw`라 테스트 스텁 5곳 무수정**) + `AccountApiService`(`DELETE /user-sessions/user/{userId}`) + VM 확인팝업·`HandleAsync`(성공→page1 재조회 / **409 ADMIN 전원잠금 가드 안내**) + 뷰 행별 버튼. 서버 기존 엔드포인트 소비(무변경).
  - **동작**: 기본을 **전체 표시**(활성만 언체크)로 전환 + **자동 갱신**(20s `System.Threading.Timer`, `OnActivate` 시작·`OnDeactivate` 폐기, 가드=`page1 && !loading && !teardown`이라 무한스크롤 방해 없음, teardown TOCTOU 하드닝).
  - **검증**: code-review(opus) **READY**(P0/P1 0 — 타이머 수명·크로스스레드 심층검토 후 P1 teardown 하드닝 반영). 신규 테스트 8(전체종료·기본전체·자동갱신 가드·teardown·컨버터 2). Accounts.Ui.Tests **44**·Accounts.Api green·빌드0.
- **심볼 우클릭 메뉴 — 뷰 모드 표시 + 잠금 게이트 v2** ([PRD](docs/prds/Symbol_ContextMenu_ViewMode_Lock-prd.md) · [Plan](docs/plans/Symbol_ContextMenu_ViewMode_Lock-prd-plan.md) · 태그 `before-symbol-contextmenu-v2` · worktree `feature/symbol-contextmenu-v2` · GMaps.Ui 1파일) — 편집모드에서만 심볼 우클릭 메뉴가 뜨던 문제 해소.
  - 웹 의존 3종(장치페이지/상세/수정)=항상 표시+웹서버 OFF 시 비활성(disable 모델, 기존 편집모드 활성-데드링크도 해소) · 스피커 음원/TTS/Stop=웹서버 게이트 제거(NATS 기반인데 웹 조건 오결합) · **잠긴 심볼=메뉴 전체 미표시**(양 모드) · ZOrder=편집모드 전용 유지. 구 `ContextMenu_DisplayRules-prd.md`(6/10) Superseded.
- **세션 관리 + 권한부여 — 무한 스크롤 페이지네이션** ([PRD](docs/prds/GOP_SessionGrant_Pagination-prd.md) · [Plan](docs/plans/GOP_SessionGrant_Pagination-prd-plan.md) · 태그 `before-session-grant-pagination` · worktree `feature/session-grant-pagination` · Accounts.Api/Accounts.Ui · 라이브러리 한정) — 감사로그 무한스크롤 패턴을 자매 패널 2곳에 이식.
  - **세션 관리**: `GetUserSessionsAsync`가 page/limit 파라미터 없어 서버 기본(≤100건)만 표시되던 갭 → `(page, limit, isActive?, ct)` 확장(스텁 5 CS0535) + 무한스크롤 + **활성/전체 토글**(is_active). **서버가 세션엔 날짜 필터 미지원**(라이브 모니터링 설계)이라 날짜피커 제외. 기본=활성만(토글로 전체).
  - **권한부여**: 100건 초과 시 `"N건 중 100건만 표시(페이지네이션 필요)"` 경고 팝업만 뜨고 나머지 열람 불가하던 갭 → 무한스크롤로 대체. `/grants`는 top-level `total`만 주고 `TotalPages=0`(F-1)이라 VM이 `Ceiling(total/size)`로 파생.
  - **공유**: 감사 때 만든 `Utils.Behaviors.DataGridScrollEndBehavior` + `AsyncRelayCommand` 재사용(신규 UI 컴포넌트 없음). 두 VM 공통 `res.Pagination`·`DispatcherService.Invoke`·`BasePanelViewModel` 관리토큰·이중 로딩가드.
  - **검증**: code-review(opus) READY(P0/P1 0, F-1 실서버 JSON 바인딩 확인·기존 J섹션 테스트 충실 재작성 +9체크). 신규 테스트 9(세션 5·권한부여 4) + FakeGopServer 세션 실페이징. Accounts.Ui.Tests **36**·Accounts.Api **136** green·빌드0.
  - **후속(범위 밖)**: 로그인 이력(UserLoginLog 성공+실패, 날짜필터) 패널 — 서버 데이터 有·클라 패널 無.
- **탐지 신호 이력 (Detection Signal History) — 신호 크기 표면화 + 센서별 신호 추이 다이얼로그** ([PRD](docs/prds/Detection_Signal_History-prd.md) · [Plan](docs/plans/Detection_Signal_History-prd-plan.md) · [설계](docs/design/Detection_Signal_History_Storyboard.html) · 태그 `before-detection-signal-history` · worktree `feature/detection-signal-history` · Monitoring.Models/Events.Ui/ViewModel/GMaps.Ui/Devices.Ui — 라이브러리 + ⚠메인솔루션 Shell 배선 별도)
  - **배경**: NATS DETECT·이력 API 모두 `detail.signal`을 보내지만 `DtoToModelHelper`가 Detail을 버려 UI 미표시. 서버 API(`GetDetectionEventsAsync` sensor+기간 필터)는 기지원이라 **서버 변경 0**.
  - **배관(FR-01~03)**: `IDetectionEventModel`/`DetectionEventModel.Signal(int?)` + 매핑 2오버로드 `Detail?.Signal` + 역방향/`ToDetectionEventReplaceDto` Detail 재구성 — **서버 PUT은 detail "전체 교체"라 미전송 시 소실**(api-test-server `schemas/event.py:157` 실측)되던 함정 봉인. 라이브 카드(NatsDomainService)도 동일 헬퍼라 자동 수혜.
  - **표면화(FR-04~07)**: 이력 그리드 신호 컬럼(숫자 N0+로드목록 최대 기준 상대 미니바·최대행 critical·null/0="—") + **Message Type 컬럼 제거**(패널이 타입별 구분이라 중복 — 사용자 결정). 카드 앞면 신호 바+값(`HasSignal` 게이트) + 뒷면 "신호" 행, **"Intrusion" 줄 제거**.
  - **진입점(FR-10/11)**: 맵 감지센서 심볼 우클릭 "탐지 이력"(웹서버/편집모드 게이트 미적용 상시 노출, 미연동 비활성) + `SensorDevicePanelView` 행 우클릭(BindingProxy 미러, Draft 차단). 오픈 메시지 `OpenDetectionHistoryDialogMessageModel`은 **ViewModel/CommonMessages.cs**(참조 실측: Events.Ui→Devices.Ui라 Devices.Ui가 Events.Ui 참조 불가=순환 → 공용 배치).
  - **다이얼로그(FR-09/12/13/14/15)**: `DetectionHistoryDialogViewModel`/`View`(Events.Ui, 단일 인스턴스·Initialize 컨텍스트 교체) — 기간 프리셋(1h/24h기본/7d/30d/기간지정) · 페이지 순회 ≤500건(초과 경고+최신 500) · Result 칩(AI=신호 전무 기본 off)+미조치만 토글 · 통계 5종 · **자체 OnRender 차트 `SignalChartControl`**(외부 패키지 0, X=시간/Y=1-2-5 나이스 스케일, 미조치=critical 포인트, hover 툴팁, 클릭→그리드 행 동기) · 미조치 행 우클릭→기존 조치보고 다이얼로그(OQ-1=(b) 순차 — DialogShell=Conductor OneActive 실측). 색상 전부 DynamicResource 토큰(다크/라이트, ThemeAssist 하드코딩 금지).
  - **메인솔루션 배선**: `ConductorControl` `IHandle<OpenDetectionHistoryDialogMessageModel>` + 래퍼 `DetectionHistoryHostDialogViewModel/View`(DialogHost+Card 880×640) + Bootstrapper 등록(기존 조치보고 배선 미러).
  - **런타임 피드백 반영(사용자 실측 5차)**: ①필터 칩 잘림→**툴바 2행 분리 + WrapPanel 줄바꿈 + 칩 라벨 축약**(`_SENSOR` 제거·풀네임 ToolTip) ②detail 전체 표면화 미흡→**모델 확장(AiModel/InferenceMs/Thumbnail/FrameWidth/FrameHeight/Objects+신규 `DetectionObjectModel`)** + 공용 "탐지 속성"(조치보고·이벤트정보 다이얼로그 공유)에 신호/AI/객체/썸네일 읽기전용 행 ③차트 조회구간 미반영→**X축=조회 구간(RangeStart/End) + 휠 줌(커서고정)·드래그 팬·더블클릭 리셋** ④닫기 UX→하단 버튼 제거·**헤더 우상단 ✕**.
  - **적대 감사(opus 워크플로 21에이전트) 반영**: ①**차트 크래시 봉인** — 조회 구간<1분이면 `Math.Clamp(min>max)` ArgumentException(휠/드래그 시 앱 크래시)이던 것을 OnRender 최소 1분 보장 + SetView/OnMouseWheel 가드 ②**드래그 고착 해소** — `OnLostMouseCapture` 추가(Alt-Tab 등 캡처 상실 시 팬 상태 안전 종료) ③**PUT frame_width/height 소실 봉인** — signal과 동일 클래스 함정(왕복 대칭 완성) ④복사생성자 Objects **깊은 복사** ⑤멀티셀렉트 detail "(다중 선택)" 게이팅(첫 항목 대표값 오인 방지) ⑥`_loadCts` OnDeactivate Dispose ⑦BOM 보정(SignalChartControl·DtoToModelHelper).
  - **검증**: 신규 `DetectionSignalTests` **16/16** green(매핑/Replace 보존·frame 왕복·깊은복사·500 상한·실패 빈상태·필터/통계 null-안전). 회귀 0(기준선 red 동일). 신규 파일 UTF-8 BOM. 라이브러리+메인솔루션 빌드 0오류.
  - **⚠ 후속(선택/현장)**: V-02 프록시 detail.signal 실기입은 스크린샷(신호 1,500)으로 확인. Phase 3 후보 — 썸네일 이미지 미리보기(현재 URL 텍스트)·from_event full detail 서버 내성 검증·로컬 Events.Db detail 미영속·조치보고 후 이력 복귀. 카드 뒷면/대시보드 퀵뷰 클리핑=런타임 육안.
- **감사 로그 뷰어 — 날짜 필터 + 무한 스크롤 페이지네이션** ([PRD](docs/prds/GOP_AuditLog_DateFilter_Pagination-prd.md) · [Plan](docs/plans/GOP_AuditLog_DateFilter_Pagination-prd-plan.md) · 태그 `before-auditlog-datefilter` · worktree `feature/auditlog-datefilter` · Accounts.Api/Accounts.Ui/Utils · 라이브러리 한정) — PRD-GOP-05 FR-SS-03 미완성분 완성.
  - **배경**: 감사 로그 패널이 최신 100건 1회 조회뿐(무한스크롤·날짜필터·페이지네이션 UI 없음) → 100건 초과 과거 로그 UI 접근 불가. 서버 `/api/audit-logs`는 `start_date`/`end_date`+페이지네이션을 이미 완전 지원(§9.6.2)이라 격차는 **100% 클라 측**(삼각검증: 스펙·실행서버·DB `created_at` 인덱스).
  - **API**: `IAccountApiService`/`AccountApiService.GetAuditLogsAsync`에 `startDate`/`endDate` 추가(ct 맨 뒤 유지) + `start_date`/`end_date` 쿼리 조립(`Uri.EscapeDataString`). 구현 스텁 5곳 시그니처 동기화(CS0535 — 인터페이스 파라미터 추가는 기본값과 무관하게 전 구현 매칭 필요).
  - **UI**: `AuditLogPanelViewModel` 날짜범위(기본 최근 7일) + 무한스크롤(`LoadNextPageAsync`·`res.Pagination` 소비·`DispatcherService` 마셜·`BasePanelViewModel` 관리 토큰). 종료일 `T23:59:59` 상향(날짜-only DatePicker라 당일 포함). `AuditLogPanelView` `md:DatePicker`×2(시작/종료) + 검색 + 로드/전체 건수 + `DataGridScrollEndBehavior`(스크롤 하단→append).
  - **공유 승격**: `DataGridScrollEndBehavior`를 Events.Ui→`Utils.Behaviors` 공유 정본으로 승격(타 패널 재사용). `AsyncRelayCommand`(재진입 가드 async ICommand) 신설.
  - **검증**: architect(설계 seam) + code-reviewer(opus) 통과. FakeGopServer audit 실페이징(서버 계약 미러) + `AuditLogPanelTests` 7케이스. Accounts.Ui.Tests **27**·Accounts.Api **136** green·빌드0. **v2.6 FF 머지·런타임 육안(다크 `md:DatePicker`)=사용자 대기**.
- **내정보 본인 프로필 사진 삭제 배선 (UI-only→서버 삭제)** ([PRD](docs/prds/MyPage_SelfPhoto_Delete_Fix-prd.md) · Accounts.Api/Accounts/Accounts.Ui/ViewModel · 라이브러리 한정)
  - **근본**: 내정보 '사진 제거하기'(`MyPagePanelViewModel.ClickClearPicture`)가 `ViewModel.Image=null`(UI만)이라 서버 미삭제 → 재조회 시 사진 부활. 본인 삭제 API/게이트웨이 미구현(업로드만 존재), PUT /users/me 도 photo_url 무시(C-5).
  - **수정(클라만, 서버 `DELETE /me/photo` 기존)**: `IAccountApiService.DeleteMyPhotoAsync`+`AccountApiService`(DELETE users/me/photo, idempotent) · `IProfileGateway.DeletePhotoAsync`(본인)+`ApiAccountGateway`/`DbAccountGateway`(false) · `CallDeletePhotoProcessMessageModel`+`MyPagePanelViewModel` 확인팝업→서버삭제(관리자 EditorDialog 패턴 미러).
  - **NFR**: 본인 `users/me/photo` 고정(관리자 `{id}` 금지 — 토큰소유자 오염 방지). 파괴적 확인 팝업(EventAggregator 표준).
  - **검증**: SelfPhotoDeleteContractTests +3(엔드포인트·{id}금지·실패graceful). Accounts.Api **136/136**·Accounts.Ui.Tests **20/20** green.
- **관리자 타 계정 프로필 사진 업로드/삭제 — EditorDialog `{id}` 배선** ([PRD](docs/prds/Admin_Photo_Upload-prd.md) · [Plan](docs/plans/Admin_Photo_Upload-prd-plan.md) · 태그 `before-admin-photo-upload` · worktree `feature/admin-photo-upload` · Accounts.Api/Accounts/Accounts.Ui/ViewModel · 라이브러리 한정)
  - **배경**: 2026-07-13 오염 사고(관리자가 타 계정 편집 중 본인 `POST /me/photo` 재사용 → 로그인 관리자 사진 오염, `6842db5`로 차단)의 후속. 서버 `v6.3-admin_photo_upload`(`POST/DELETE /api/users/{id}/photo`, users:edit+base-ADMIN 상승가드+actor≠target 감사 via log_action_async) 배포로 클라 완결.
  - **FR-01/02**: `AccountApiService.UploadUserPhotoAsync(id)`(`POST users/{id}/photo`, multipart `file`)·`DeleteUserPhotoAsync(id)`(`DELETE users/{id}/photo`, idempotent). 인터페이스는 default-impl(throw)로 기존 테스트 스텁 무수정.
  - **FR-03**: `{id}` 사진 메서드를 self `IProfileGateway`가 아닌 관리자-타깃 `IUserDirectoryGateway`(default-impl null/false)에 배치 → self(오염원)/admin-target 경로를 **타입 레벨 분리**. `ApiAccountGateway` 오버라이드, `DbAccountGateway`=기본 상속(DB 모드 미지원 null).
  - **FR-04/05**: `EditorDialogViewModel.ClickAddPicture` 차단 스텁 → **대상 `ViewModel.Model.Id`** 업로드(ProfileImageHelper 검증만=로컬 orphan 방지, 실패 시 표시 원복+graceful 팝업, 즉시커밋↔취소 비대칭 문서화) + `ClickDeletePicture`→확인 팝업→`HandleAsync`(영구삭제 확인 후 default 아바타 복귀). View에 삭제 버튼 추가.
  - **🔧 후속 런타임 수정** (사용자 실측, worktree `feature/admin-photo-fix`): ① **삭제 '안 됨' = Confirm 팝업 소프트락** — `HandleAsync(사진삭제)`가 팝업을 안 닫아(`ClosePopupMessageModel` 누락, 시블링 핸들러엔 있음) '확인'해도 팝업 잔존 → 진입 청산+결과 안내 추가. ② **업로드/삭제 후 목록 미반영** — `AccountManagerPanelViewModel.HandleAsync(RefreshAccountsMessageModel)`가 인메모리 provider 재구성만 하고 서버 재조회를 안 함(편집/초기화도 동일한 기존 버그, 사진이라 노출) → 서버 재조회(SSOT) 추가. ③ **허용 형식 서버 정렬** — `ProfileImageHelper`/파일필터에서 bmp 제거·webp/gif 추가(서버는 jpeg/png/webp/gif만, bmp는 400). 검증: Accounts.Ui.Tests **20/20**(+3)·Accounts.Api **132**·빌드0. (업로드 자체는 정상 — 앞선 400은 사용자가 올린 잘림/손상/5MB초과 파일 때문, 서버 실측 200 OK로 확인)
  - **🖼 사진 UI 개선** (사용자 실측 — "삭제 버튼 안 보임·등록 여부 식별 불가", worktree `feature/photo-preview`): EditorDialog 사진 행을 **URL 텍스트박스 → 미리보기(56×56, `ImageConverter`·http photo_url 직접렌더·없으면 기본 아바타)** + **라벨 버튼(변경/삭제, `Delete` 아이콘)** 으로 교체(마이페이지 패턴 정렬). `ImageConverter` 로컬 선언, 미참조 `EditorImage` TextBox 제거. 빌드0 → 관리자가 대상 계정 사진을 눈으로 확인하며 변경/삭제.
  - **검증**: 신규 계약 테스트 4(NFR-01 회귀=업로드/삭제 `users/{id}/photo` 타깃·`/me` 아님) + Accounts.Api **132/132**·Accounts.Ui.Tests **17/17** green, 빌드 0오류. **code-review(opus)**: P0 재오염 없음(타입+대상Id 추적 확증), P1 2건(삭제 확인 팝업·업로드 실패 orphan/원복) 반영. ⚠FR-06(버튼 권한 게이팅)=패널 진입 게이팅+서버 집행과 중복이라 보류. 런타임(타계정 업로드/삭제·감사기록·비-ADMIN 403)은 앱 재빌드 후.
- **권한 부여 검증 + F-1(절단경고) + grant 만료 실시간 컷오프(FR-GS)** ([PRD](docs/prds/GrantList_TopLevelTotal_Fix-prd.md) · [PRD](docs/prds/Grant_LiveCutoff_Client-prd.md) · 태그 `before-grant-verification` · Accounts.Api/Accounts.Ui/Messages · 라이브러리 한정)
  - **검증**: GrantManagementPanelViewModel 119시나리오×2회 + AccountApiService 계약 14건(FakeGopServer=api-test-server grants 규칙 전사, 재현성 확인). 서버/클라 시간기반 집행 분석 MD 2종(`docs/analyses/Grant_Enforcement_{Server,Client}_Analysis.md`, 서버본은 api-test-server/docs 전달).
  - **F-1**: 서버 GET /grants 는 total 을 top-level 로 반환하나 클라가 pagination.total 만 읽어 절단경고(100건 초과)가 도달 불가였음 → `ApiListResponse.Total`(int?) 수신 + VM 소비. events 등 pagination 객체 경로 무영향(additive).
  - **FR-GS-01/02/03 (grant 만료 컷오프)**: `PermissionsSnapshotDto` + `IPermissionService.Refresh`(role/loginId/name 유지·clockSkew(server_time) 보정) + `GetMyPermissionsAsync`(/me/permissions) + `PermissionRefreshService`(valid_until 타이머·체인 재무장·fail-safe=권한확대 없음). 로그아웃 없이 만료 시 UI 권한 실시간 재게이팅(서버 403 권위 유지). 6c4ed0a(FR-GS-01/02) 설계 계승. **NATS 실시간 push(FR-GS-04)=서버 3-게이트 합동 Phase 2**(서버 NOTIFY §3 확정).
  - **검증**: Accounts.Api **129/129** + Accounts.Ui.Tests **17/17** green(신규 테스트 +28). 검증 하네스+F-1 커밋 `bf23fb2`.
- **장비 CRUD → DeviceProvider 캐시/패널 정합 (경로 B-패널, FR-D)** ([PRD](docs/prds/DeviceStatusSync_ActionReportPropagation-prd.md) · [Plan](docs/plans/DeviceStatusSync_ActionReportPropagation-prd-plan.md) · 태그 `before-devicesync-actionreport` · Devices.Ui/Events.Ui · 라이브러리 한정) — DeviceStatusSync PRD Phase 1. 장비 추가/삭제(SYNC_DEVICE)가 DeviceProvider 캐시를 넘어 파생 상태·열린 패널까지 정합되도록 4개 갭 해소.
  - **FR-D2 🔴 (DeviceCount stale)**: `DeviceProviderService.RemoveDeviceByIdAsync`가 삭제 전 소속 그룹 스냅샷으로 `DeviceGroupProvider.DeviceCount`를 1씩 감소(음수가드·다중그룹·미발견 skip). 서버 재조회 전 그룹 카운트 과대표시 제거.
  - **FR-D3 🟡 (그룹 멤버십 변경 미감지)**: `UpdateDeviceProperties`의 `DeviceGroups = new List` 재할당을 **기존 List Clear+AddRange**(참조 보존)로 교체 → UI 컬렉션 변경 감지.
  - **FR-D1 🟡 (열린 패널 미반영)**: Controller/Sensor 장비패널에 **provider→panel 역방향 동기화** 신설 — `_deviceProvider.CollectionEntity.CollectionChanged` 구독으로 열린 DataGrid에 add/remove 즉시 반영. 단일 재진입 플래그(`_isSyncingFromProvider`)로 순방향↔역방향 상호 억제(무한루프/이중행 차단), Id 기준 멱등(Draft Id≤0 격리), 구독 수명은 순방향과 동일 3지점(OnDeactivate·DataInitialize -/+) 토글. ⚠나머지 4패널(Camera/Speaker/Enclosure/Lamp)은 재활성화 반영 유지(후속).
  - **FR-D4 🟢 (삭제 device 참조 카드)**: `EventCardViewModel`의 Device 접근부는 이미 전부 null-safe(`?.`/가드) 확인 → null 계약·EQM 자동정리 규칙 XML-doc 명문화 + 회귀 테스트.
  - **검증**: Devices.Ui **122/122**(FR-D2 5·FR-D1 3 신규) + Events.Ui FR-D4 테스트 green, 빌드 0오류.
  - **경로 C-2 (원격 ACTION_REPORT → 활성 카드 종결)**: 다중 GIS/서브시스템 이벤트 공유 대비 — `EventCardListPanelViewModel.CloseCardByEventId(int)` 신설(개별 조치보고와 동일 EntryId 폴백+EQM Dequeue 재사용, 카드 부재 시 멱등 no-op) + 메인 `NatsDomainService.ProcessActionAsync` override(`from_event.id` 직접 파싱, from 무관 전량 처리·자기 echo 멱등 흡수). **설계 교정**: `from`은 전부 "GIS" 리터럴이라 자기/타 GIS 구분 불가 → from-skip 배제(다중 GIS 공유가 깨짐), 멱등으로 자기 echo 흡수. BatchActionReportTests 16/16 green.
  - **경로 B (하드닝)**: **FR-B2** `SymbolEventManager.SyncDeviceStatus`가 복합 키 미스 시 기존 `TryResolveDevice` Id-폴백 사용(재등록 DeviceType 변경 시 심볼 동기화 지속) + **FR-B3** 메인 `ProcessSyncDeviceAsync` fetch-null 진단(무음 누락 제거) + **FR-B1** SSOT 리팩터로 폐기된 SyncFromDevice→EventStatus stale 테스트 2종을 SSOT 계약(OperationState만)으로 교체. SymbolEventManager/DeviceSymbolLookupModelSync green.
  - **경로 A (AI 탐지 EQM 일원화) — 보류(사용자 결정 2026-07-15)**: 5-agent 추적 결과 현행 설계(서버 `cmd="DETECT"`)에선 AI 탐지가 이미 `DetectionNatsSyncService`→EQM로 처리되어 PRD 전제("AI가 EQM 우회")가 틀림 → `ProcessDetectionMode` 376줄 `ProcessDeviceEvent`는 중복(자기교정). 서버 실제 발행 cmd(DETECT vs 레거시 AI_DETECTION)가 레포에 없어 런타임 미확정 → 라이브 탐지 경로 미변경(보류). [[project_pathb_works_pathc2_actionreport_gap]]
  - ⚠ 메인 솔루션(C-2/B3)은 앱 실행 중 DLL 잠금으로 컴파일만 검증(0 CS) — full build/런타임 E2E는 앱 재시작 후. 롤백 태그 `before-devicesync-actionreport`(lib v2.6@05c457f, main v0.5@d602c63).
- **카메라 팝업 RTSP 소스 우선순위 — URL조회/Onvif조회 설정 토글** ([PRD](docs/prds/CameraPopup_RtspSource_Priority-prd.md) · [Plan](docs/plans/CameraPopup_RtspSource_Priority-prd-plan.md) · 태그 `before-camerapopup-rtsp-source` · worktree `feature/camerapopup-rtsp-source` · Streaming(.Base)+GMaps.Ui)
  - **기능**: EventSetup 팝업 설정에서 연결 소스 선택 — `Url`(기본, 현행 무변경: 수동 RtspSub→RtspMain) / `Onvif`(계정·비번으로 ONVIF `GetStreamUri` 프로파일별 URL 조회 → 자격증명 URL-인코딩 임베드 재생, RTSP Basic/Digest-MD5는 LibVLC 자동 협상). **실패/타임아웃(12s) 시 URL조회 자동 폴백**, 둘 다 없으면 팝업 닫기.
  - **설계**: 계층 원칙(Streaming=메커니즘/GMaps.Ui=정책) — Streaming 변경은 설정 키 1개(default interface 구현 → 메인솔루션 미반영 시 Url 모드로 자립·컴파일 무파괴) + 플레이어 **late-bind 연결**(ConnectionInfo DP change 콜백, OnLoaded와 이중 연결 방지 가드). ONVIF 조회는 `PtzController` 워밍 재사용(이중 초기화 0, Gate 직렬화 I-05) + 조회 URL 캐시(Release 시 무효화). 신규 순수 헬퍼 `OnvifProfileSelector`(**해상도 최소→비오디오 우선→원 순서** — cam66 실측 8프로파일서 video1s 서브 정확 선택)·`OnvifRtspUrlComposer`(userinfo 치환·특수문자 인코딩). 팝업 "영상 주소 조회 중…(ONVIF)" 배지. MapViewModel 분기는 신규 partial(`MapViewModel.CameraPopupSource.cs`).
  - **검증**: VER-01 실카메라 SOAP 실측(GetStreamUri=자격증명 없음·호스트 그대로 → 치환 불필요 확정) · 빌드 0 · 테스트 **194/194**(신규 Composer 7·Selector 7, 실코드 소스링크) · **code-review(opus) MERGE_WITH_FIXES 5건 반영**(CTS-Close 배선·멱등 가드·Gate 주석). ⚠ 설정 UI(EXT-01~03)는 메인솔루션 몫(사전 통지 후 별도) — 미반영 시 Url 모드 유지. 실카메라 런타임 검증은 앱 재빌드 후.
- **GMap 툴바 CPU/GPU/RAM 사용량 표시 — 우측 정렬 아이콘+% 칩** ([PRD](docs/prds/GMap_SystemResource_Indicator-prd.md) · [Plan](docs/plans/GMap_SystemResource_Indicator-prd-plan.md) · 태그 `before-sysres-indicator` · worktree `feature/sysres-indicator` · 신규 라이브러리 `Ironwall.Dotnet.Libraries.SystemResources` + GMaps.Ui)
  - **신규 라이브러리 `SystemResources`**(net8.0-windows, Base만 의존, 재사용 가능): OS 네이티브 PDH(pdh.dll)+kernel32로 CPU/GPU/RAM 사용률 취득. **보안**: LibreHardwareMonitor(WinRing0 커널드라이버=Defender 악성탐지 CVE-2020-14979) 배제 — 커널드라이버·관리자권한·서드파티 네이티브 바이너리 불요.
  - **로케일 독립**: `PdhAddEnglishCounterW`(Perflib 인덱스 기반)로 ko-KR Windows에서도 영문 카운터 해석. CPU=`% Processor Time`(busy 시간%·0~100, Processor Information>64코어 우선·클래식 Processor 폴백; ⚠`% Processor Utility`는 Turbo 주파수 배율을 포함해 고주파 머신서 100% 고정·작업관리자와 괴리→배제), GPU=`\GPU Engine(*)` 와일드카드→(luid,phys,eng) 집계 busiest(멀티GPU 블렌딩 방지), RAM=`GlobalMemoryStatusEx.dwMemoryLoad`.
  - **설계**: `ISystemResourceMonitor : IDisposable`(IService 미구현=이중권위 회피). **WPF 비의존**(NFR-06) — 모니터는 타이머를 소유하지 않고 소비자(MapViewModel)의 UI-스레드 `DispatcherTimer`가 `Sample()` 구동 → 락/크로스스레드 마샬/재진입 원천 소멸(백그라운드 타이머+네이티브 핸들의 P0 UAF 크래시 회피). Fail-safe(PDH 실패=전체 N/A, UI 미전파, 1회 로깅). 히스테리시스 색 전환(정상 시안/경고 앰버/위험 빨강, 62-57/87-82 데드밴드).
  - **UI**: `MapView.xaml` DockPanel 우측 `Dock=Right` 3칩(아이콘 `Cpu32Bit`/`Gpu`/`Memory` + 고정폭 수치 + 절대값 툴팁), GPU 부재 시 Hidden(공간 예약), 전부 DynamicResource(테마 스왑). 모니터는 활성 시 Start(멱등)·비활성 시 타이머만 정지(핸들 유지=워밍업 보존, 모든 deactivate 경로 대칭).
  - **검증**: SystemResources 빌드 0 + **단위테스트 22/22**(GpuAggregator·Hysteresis·Monitor fail-safe/워밍업/생명주기) · GMaps.Ui 전체 빌드 0오류 · **실기 PDH 실측 정합**(CPU/GPU/RAM 실값). ⚠PackIcon 렌더·MonoFont·narrow창·통합 시각(VER-03/04/07·MANUAL-01)은 앱 재빌드 후 런타임 검증.
- **Line/Area 심볼 리사이즈 — 어도너 박스 리사이즈로 폴리라인·폴리곤 크기조절** ([PRD](docs/prds/LineArea_Symbol_Resize-prd.md) · [Plan](docs/plans/LineArea_Symbol_Resize-prd-plan.md) · 태그 `before-linearea-resize` · worktree `feature/linearea-resize` · GMaps.Ui)
  - **근본원인**: Line/Area(`IsClosedPath` 닫힌폴리곤)는 `LinePoints`(위경도) 기반이라 크기가 파생값(`UpdateLineGeometry`가 매 리드로우 W/H 재계산) → 어도너 W/H 리사이즈 무효 + line엔 핸들 미렌더. **해법(🅐)=박스 리사이즈로 점을 중심 기준 스케일**.
  - **FR-01**: `LineGeometryUtils.Scale`(순수·퇴화가드 ε/IsFinite/부호반전) + `ILineEditableMarker.ApplyGeometry` seam(GMapLineMarker/PidsGroup, SyncModelPoints 4단계 규약).
  - **FR-02/03**: 어도너 핸들 노출(코너=모든 line·변=닫힌폴리곤) + `GetHandleBounds`=ActualLineBounds 통일(짧은선 불일치 해소), `ProcessLineScale`(시작 bbox 대비 절대배율·줌 stale 방지, TransformToAncestor로 map 좌표, Position=새 bbox중심).
  - **FR-04 P0**: line 스케일은 W/H 불변→`HasChanges=false`→기존 Undo가 **미기록+즉시영속 파괴적**이던 결함을 신규 스냅샷 `LineGeometryCommand`(점+Position 복원, isImage=false)+`RecordLineGeometry`(HasChanges 우회)로 정합. **FR-05** ESC=스냅샷 점 복원. **FR-07** line 라벨 상한=절대 픽셀(파생 W/H 부작용 차단).
  - **시뮬레이션**: 승인 후 4도메인 40+시나리오 사이드이펙트 시뮬(§5-C) → PRD v2.0 보강(지오앵커·퇴화가드·Position정합·P0 undo 재기술). 정점편집(🅑)=후속.
  - **검증**: GMaps.Ui 빌드 0 · 단위 `LineScaleTests` 8/8 + `LineGeometryUndoTests` 4/4 + 회귀 `UndoRedoTests` 34/34.
  - **런타임 검증·수정(사용자 "잘된다" 확인)**: ①리사이즈 좌표 요동(먹통↔갑툭튀)=드래그시작 어도너-로컬→**맵공간 앵커** 고정 + 스케일 중 Position 상수(재앵커 제거) ②Area 가이드박스 심볼 밖=Position≠bbox중심→**리사이즈 시작 1회 Position 재앵커** ③**리사이즈 미반영/Undo 어긋남/팬텀 중복 근본원인**=`GMapMarkerLineControl/PidsGroupControl.OnMarkerPropertyChanged`가 점(RuntimePoints) 변경을 무시(IsVisible만 반응)→`ApplyGeometry`로 점 바꿔도 `UpdateLineGeometry` 미실행(stale 렌더)→**점 변경도 재실행하도록 수정** ④**Undo/Redo 버튼 단축키 툴팁** `ToolTipService.ShowOnDisabled`(비활성 시에도 hover 표시)+Redo Ctrl+Shift+Z 표기. 진단로그([LINE-RESIZE]/[UNDO-DIAG]) 원인확정 후 제거. [[project_line_marker_render_trigger]]
- **통합웹 접속 트리거 메시지 `CallWebApiProcessMessageModel`** ([PRD](docs/prds/LeftMenu_IntegratedWeb_Button-prd.md) · [Plan](docs/plans/LeftMenu_IntegratedWeb_Button-prd-plan.md) · ViewModel.Models/CommonMessages.cs) — LeftMenu "통합웹" 버튼의 확인 팝업 '확인' 시 발행되어, 크롬 앱 모드로 통합 웹 대시보드(`http://{웹서버IP}:{포트}`)를 여는 트리거(`IMessageModel` 마커 타입). 순수 추가(기존 타입 무변경). 소비측=메인 Monitoring `LeftMenuSectionViewModel`(권한 게이팅→웹설정 `IsWebServerEnabled` 게이팅으로 교체, DATABASE 메뉴→통합웹). ⚠앱 재빌드 후 반영.
- **맵 심볼 제어 단축키 — Delete(확인삭제) / Ctrl+C(복사) / Ctrl+V(붙여넣기)** ([PRD](docs/prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md) · [Plan](docs/plans/MapSymbol_Shortcut_CopyPasteDelete-prd-plan.md) · 태그 `before-mapsymbol-shortcuts` · worktree `feature/mapsymbol-shortcuts` · GMaps.Ui)
  - **Delete(FR-05)**: 선택 심볼/이미지 삭제(단일·그룹)를 단일 진입점 `ExecuteDeleteSelected`(EventAggregator 표준 확인팝업)로 통일. **P0 갭 수정** — 단일 Delete 키가 원래 동작하지 않던 문제(어도너 `RequestMarkerDeletion`이 no-op 스텁인데 `e.Handled=true`로 키를 삼킴)를 `OnMapPreviewKeyDownForGroup` 단일 분기 추가 + 어도너 Delete case 제거로 해소(이중처리 차단).
  - **Ctrl+C/Ctrl+V(FR-02/03)**: 인메모리 클립보드 — 단일/멀티 선택 복사, 붙여넣기는 **마우스 커서 위치**(멀티는 앵커 기준 상대 간격 유지). 배치 Undo(`BeginBatch` 1매크로) + 트리 1회 리빌드 + 결과 자동선택. 반복 붙여넣기 가능. 오버레이 이미지는 v1 제외(삭제는 포함). 커서 추적=`GMapCustomControl.GetLastCursorLatLng`(맵 밖 폴백=뷰 중앙).
  - **복제 코어 통합 + P0 버그 수정(FR-01)**: `DuplicateSelectedMarker`의 300줄 타입별 switch를 `CreateSymbolCopyAsync`(스냅샷 딥클론 재사용) 코어로 추출 — Duplicate(오프셋)/Paste(커서) 공유. **PIDS 복제 버그 2건 수정**: ①`duplicatedSymbol = pidsSymbol`(Fetch Id 유실→Undo 누락) 제거, DB 발급 Id 사용 ②`LinkedDeviceId + 1000`(실장비 오참조) 제거 → PIDS·PidsGroup 붙여넣기는 미링크(0). 붙여넣기 제목은 `_Copy` 미부가(복제 버튼은 유지).
  - **검증**: GMaps.Ui 빌드 0오류. 신규 단위테스트 `SymbolCopyTransformTests` 7/7 통과(미링크·Id리셋·재배치·LinePoints 평행이동·제목정책) + 회귀 `UndoRedoTests` 34/34 통과. ⚠전체 복사/붙여넣기/삭제 플로우 및 키 후킹은 앱 재빌드 후 런타임 검증(V-01~V-06).

### Removed
- **`group_device`(deprecated 단일 그룹) 죽은 코드 정리** (태그 `before-remove-group-device` · Devices.Api/Ui) — `device_groups`(N:N EventMapping) 전환으로 deprecated된 `group_device`의 잔재 제거. `IDeviceApiService`/`DeviceApiService` 6개 조회 메서드의 **미사용 쿼리 필터 파라미터** `int? groupDevice` + XML doc + `MockDeviceApiService` 시그니처 정합. **DTO/Model엔 이미 프로퍼티 없음**(전환 완료·제거 대상 없음). 호출자 30곳 전부 named-arg/무인자라 무영향(빌드 검증: Devices.Api/Ui 0오류). 메인 솔루션 참조 0. (Messages/Tests 하위호환 JSON 픽스처는 유지 — 구서버 group_device 수신 시 무시됨을 검증.)

### Fixed
- **라인/구역(PidsGroup) 라벨 위치가 재시작 시 초기화되는 데이터손실 수정** (태그 `before-label-offset-reset-fix` · GMaps.Db 1파일 · 6-에이전트 병렬 워크플로 근본원인 확정)
  - **근본원인**: `GMapDbSymbolService.BuildSchemeAsync`의 "1회 정리"라 주석된 마이그레이션(`UPDATE Symbols SET LabelOffsetX/Y=0 WHERE Category IN('AREA_BOUNDARY','PIDS_GROUP') AND |offset|>3`)이 **멱등 가드 없이 매 부팅 재실행**. 라벨 드래그 비율 저장(`LabelAdorner` L358-365)이 유클리드 길이로만 상한을 걸고 축별 비율로 저장 → 비등방(세로로 긴) 구역/라인 footprint에서 라벨을 옆으로 정상 드래그하면 한 축 비율이 3 초과 → 그 유효값이 위 UPDATE에 걸려 **재시작마다 0으로 소거**(admin 무관). 점 심볼(카메라 등)은 Category가 달라 무영향이고 저장/로드 배선은 8타입 전수 정상(무결).
  - **수정**: 해당 매-부팅 리셋 UPDATE **제거**. 레거시 px 정리는 전환 코드 배포 직후 이미 완료됐고, 이후 부팅에서 걸리는 `>3` 값은 사실상 정상 드래그 비율값이므로 계속 파괴만 하던 코드. 제거로 모든 크기의 유효 드래그값 보존(UX·스키마 변화 0). ⚠이미 0으로 지워진 위치는 복구 불가 — 재빌드 후 한 번 재드래그하면 이후 재시작에도 유지.
- **첫 실행 시 심볼 타이틀 라벨 미표시(줌/팬 후에야 나타남) 회귀 수정** (태그 `before-label-firstpaint-fix` · GMaps.Ui 3파일 · 6-에이전트 병렬 워크플로 근본원인 그라운딩)
  - **근본원인**: 게이트(mapZoom<markerZoom)가 아니라 **LabelAdorner 무효화(재렌더) 공급의 구조적 누락**. 라벨은 WPF `AdornerLayer`의 독립 Visual이라 생성 후 재렌더 트리거가 3종뿐(`OnMapZoomChanged`/`OnMapDrag`/마커 `_renderProps` 변경). 부팅 시 `Attach`는 `_layer.Add`만 하고 강제 무효화가 없어 "단 1회" 자동 첫 렌더가 **지도 정착 前 상태**(홈줌은 컨트롤 Loaded 전 조용히 세팅→`OnMapZoomChanged` 미발화, `RestoreLayerVisibility`의 `MainMap.InvalidateVisual()`은 `GMapControl`만 무효화·AdornerLayer 미도달)로 그려진 뒤 stale 고착. 줌 "또는 팬"이 고치는 이유 = 임계값이 아니라 그때 비로소 `InvalidateVisual`이 공급되기 때문. PidsGroup(구역)만 먼저 보이던 건 배치 줌이 낮아 `markerZoom`이 작아 전이 저줌 첫 프레임에서도 게이트를 통과한 상태로 렌더됐기 때문. → 라벨을 마커 컨트롤 내부에서 별도 AdornerLayer로 분리(Symbol_Label_Decouple/Overlay_Title)하며 유입된 **회귀**.
  - **수정**(게이트/좌표 로직 불변): ①`LabelAdornerService.RefreshAll()` 추가(부착된 전 라벨 직접 `InvalidateVisual` — `_renderProps` Visible/IsVisible 이름 불일치 우회) ②`RestoreLayerVisibility` 뒤 ApplicationIdle에서 `RefreshLabelsWhenReady()` 1회 — 투영(ViewArea) 유효 시 즉시, 아니면 첫 `OnTileLoadComplete` 후 1회(오버레이 초기화와 동일 패턴, one-shot) ③`LabelAdorner`가 `OnPositionChanged`도 구독 — 프로그램적 "홈 이동"·앵커 정착 등 줌·팬 아닌 뷰포트 이동에서도 라벨 갱신(관련 잠재갭 동시 차단).
  - **검증**: GMaps.Ui 빌드 0오류 · 한글 mojibake 0. ⚠앱 재빌드 후 런타임 확인(첫 실행에 카메라·스피커 등 심볼 타이틀이 팬/줌 없이 즉시 표시).
- **카메라 팝업 A/B 동일 영상 버그 — 스트림 공유 키의 쿼리스트링 누락 수정** ([분석](docs/analyses/Rtsp_Popup_Streaming_Ptz-analysis.md) · 태그 `before-camerakey-query-fix` · Streaming(.Base))
  - **근본원인**: `RtspConnectionInfo.GetCameraKey()`가 쿼리를 버리고 `host:port/path`만 키로 사용 → `?channel=`류로만 구분되는 서로 다른 카메라가 Hub(및 폴백 SharedSession) **같은 디코더로 병합** → 두 팝업이 같은 WriteableBitmap 공유(먼저 연 쪽 영상이 둘 다 표시).
  - **수정**: 키 파생을 순수 헬퍼 `RtspCameraKey.Derive`로 추출(테스트 소스링크)하고 **쿼리 포함**(`host:port/path[?query]`, 자격증명만 제외 — 무쿼리 URL 키는 기존 포맷 그대로라 회귀 0) + Hub `AcquireAsync`에 **키 동일·URL 상이 경고 로그**(데이터 중복 K2 즉시 가시화, 마스킹) + `CreateEntry` 로그 자격증명 평문 노출 마스킹(보안).
  - **검증**: 신규 단위 `RtspCameraKeyTests` 6/6 포함 GMaps.Ui.Tests **180/180** · Streaming/GMaps.Ui 빌드 0오류. ⚠앱 재빌드 후 실카메라 A/B 동시 오픈 런타임 검증.
- **GIS NATS Stage 0 — PTZ_STATUS 수신 복구 + ACTION_REPORT device_groups/geolocation + DETECT frame 필드** ([PRD](docs/prds/GIS_Nats_Full_Integration-prd.md) · [검증](docs/analyses/GIS_Nats_Simulation_Verification.md) · 태그 `before-gis-nats-stage0` · Events.Ui/Messages)
  - **배경**: GIS.md v1.5(REST v4.6) 스펙 대비 26개 NATS 메시지 **225 시나리오 전수 시뮬 검증**(SIM 발행/이벤트/상태/SYNC/REQ-RSP) → 활성 21중 🔴11 결함. Stage 0=긴급·라이브러리 한정 4건(통합 PRD 6단계 중 1단계).
  - **FR-01 PTZ_STATUS subject 복구(🔴 실운용 전면 미수신)**: `CameraPtzNatsSyncService`가 구 subject `nvr_manager.ptz-status`만 필터 → 스펙 v1.5 subject `gis.ptz-status`(§3.6)로 오는 메시지를 **전량 드롭**했음(형제 `TrackingStatusNatsSyncService`가 `gis.tracking-status`로 정상 동작 → 브로커가 `gis.*` 전달함이 지상 증명). **두 subject 병행 수용**(`IsPtzStatusSubject` 추출)으로 서버 버전 무관 무회귀 복구.
  - **FR-02 ACTION_REPORT/DETECT device_groups(라우팅 키)**: `ConvertDeviceToDto`가 device_groups 미채움 → 수신자(NVR/방송/경광등/VMS) N:N EventMapping **라우팅 키 결손**. 모델 그룹 id(List<int>)로 `device_groups`(DeviceGroupDto) 채움. (name/description/device_count 이름 보강은 Stage 1 DeviceGroupProvider seam으로 분리 — 라우팅은 id로 즉시 동작.)
  - **FR-03 DETECT frame_width/frame_height**: `DetectionDetailDto`에 AI bbox 좌표 스케일 해석용 프레임 해상도 필드(optional) 추가.
  - **FR-04 geolocation 전필드**: `ConvertDeviceToDto`가 위경도만 채우던 것을 location/altitude/heading 포함 전필드로(`BuildGeolocationDto`).
  - **검증**: Messages 빌드 0오류·`DetectionDetailDtoTests` 4/4 · Events.Ui 빌드 0오류·`DtoToModelHelperTests`+`CameraPtzSubjectFilterTests` 15/15 통과(신규: device_groups+geolocation 왕복, PTZ subject Theory 5케이스, frame 왕복). ⚠앱 재빌드 후 런타임 반영. **D-4**: 서버가 실제 `gis.ptz-status`로 발행하는지는 배포 전 실 NATS 확인 권장(병행 수용으로 무회귀 보장).
- **GIS NATS Stage 1 — device_groups 이름 보강 + SYNC cmd enum 인식 + 사문 서비스 제거** ([PRD](docs/prds/GIS_Nats_Full_Integration-prd.md) · Events.Ui/Enums, 메인 솔루션 무변경)
  - **FR-02b device_groups 이름 보강**: `ConvertDeviceToDto`가 그룹 id만 채우던 것을 `IoC.Get<DeviceGroupProvider>()`로 name/description/device_count까지 보강(`EventCardViewModel.DeviceGroupsText` 패턴 재사용, provider 미가용 시 id-only fallback). 라이브러리 한정.
  - **FR-05 SYNC cmd enum 인식**: `EnumGopCommand`에 SYNC_EVENT_MAPPING(15)·SYNC_PRESET(16)·SYNC_SERVER/CATEGORY/FILE_GROUP/CAMERA_SETTING/PROXY_SETTING(17~21) 추가. `ResolveCommand`가 이름 매칭(Enum.TryParse ignoreCase)이라 재번호 불필요(PTZ_AIM_LOCATION=14 유지).
  - **FR-08 사문 DeviceNatsSyncService 제거**: `As<IService>` 미등록으로 StartService가 호출되지 않던 사문 서비스+인터페이스 삭제 + EventUiModule 등록 제거. SYNC_DEVICE는 메인 `NatsDomainService.ProcessSyncDeviceAsync`가 전 action 처리(권위 경로), 메인 참조 0 확인.
  - **범위 확정(실측)**: SYNC_EVENT_MAPPING·SYNC_PRESET 캐시/핸들러(FR-06/07)는 **GIS 소비처 부재**(EventMapping=수신자가 조회, Preset=Stage 2 마스킹 미구현) → **enum 인식만** 두고 추후 GIS 자동연동 전면 구현 시 도입. **FR-09(DELETED 심볼 제거)는 기각** — 심볼 생명주기는 장비 CRUD와 비결합(자동생성 안 하므로 자동삭제도 안 함).
  - **검증**: Enums 빌드 0오류 · Events.Ui 15/15(FR-02 보강 후 회귀 없음). ⚠앱 재빌드 후 반영.
- **GIS NATS Stage 3 — WINDY REQ/RSP 무응답 로직(실패·타임아웃 시 알림 + 서버 재동기화)** ([PRD](docs/prds/GIS_Nats_Full_Integration-prd.md) · 메인 솔루션 `NatsDomainService` 단일 파일)
  - **결함**: WINDY 풍량 모드 변경 REQ가 RSP 무응답(타임아웃/연결없음)·서버거부 시 **로그만** 남기고 라디오 버튼은 낙관적으로 이동한 채 방치(롤백 없음). WINDY는 시스템 **유일의 라이브 NATS REQ/RSP**(`RequestAsync` 호출처=WINDY뿐, 3-agent 실측).
  - **FR-15 무응답 로직**: `NatsDomainService.HandleAsync(SendWindyModeMessage)`가 `reply==null`(무응답)·`rsp.Success==false`(서버거부)·성공을 구별 → 실패 시 (1)`OpenInfoPopupMessageModel` 표준 팝업 알림(raw MessageBox 금지) + (2)`FetchProxySettingsAsync`로 서버 WindyMode 재조회→`ChangeModeWindyMessageModel` 발행→`WindyPanelViewModel` 라디오를 **서버 진실로 복원**. 로컬 롤백 대신 서버 재조회라 "타임아웃이지만 실제 적용됨" 케이스까지 정합. 타임아웃 5s→3s.
  - **범위 확정(실측)**: LAMP_OFF REQ(FR-16)=트리거 전무(DTO만)=speculative→보류 · PTZ UI(FR-18)=ONVIF 직결로 NATS REQ 무관→보류 · ProcessResponseAsync 정리(FR-17)=테스트 결합("Step 3 마이그레이션" 소유)→드롭 · 결과타입/설정값화(FR-13/14)=단일 호출처엔 과설계→인라인. **라이브러리 무변경**.
  - **검증**: 메인 솔루션 빌드 0오류(경고 995 기존분). ⚠앱 재빌드 후 런타임 E2E(실패 팝업 + 라디오 복원) 필요.
- **GIS NATS Stage 2 — PtzStatusBodyDto v4.6 감시금지구역 필드(마스킹 적용은 보류)** ([PRD](docs/prds/GIS_Nats_Full_Integration-prd.md) · Messages)
  - **FR-10**: `PtzStatusBodyDto`에 `current_preset`(int?)·`is_restricted`(bool) 추가 — v4.6 메시지 계약 완성(기존엔 파싱 시 **silent 손실**됐음). Messages 빌드 0오류.
  - **마스킹(FR-11/12) 보류(사용자 결정)**: is_restricted는 **Preset 감시금지구역 속성 종속** → Preset 이벤트 맵핑/캐시(Stage 1 FR-07 보류)가 선결. 현재 GIS의 double-click→RTSP **단순 뷰 구조**가 관리형 마스킹에 부적합. **마스킹 인프라(`PlaybackState.Restricted`·🚫 오버레이·`IImprovedRtspStreamingService.RestrictStream`/`UnrestrictStreamAsync`·`IsHubMode` airspace 회피)는 이미 완비·미배선**(호출처 0) — Preset 인프라 도입 시 `cameraId→stream contextId` 라우팅만 추가하면 활성화.
- **이벤트 카드 "구역" 표시 — 그룹 Id 숫자 → 구역 이름(N:N)** (태그 `before-event-card-zone-name` · Events.Ui)
  - **결함**: 탐지/장애 이벤트 카드의 "구역" 칸이 구역 **이름** 대신 값이 이상하게 표시됨 — 장애 카드는 그룹 **DB Id 숫자**(`Device.DeviceGroupsText` = `string.Join(", ", List<int>)` 모델 구현)를 "1, 116"처럼 노출, 탐지 카드는 존재하지 않는 `Device.Name` 바인딩으로 **빈칸**. 근본원인=`DtoToModelHelper`가 서버 DTO의 그룹 `name`을 버리고 `id`만 매핑(`Select(g => g.Id)`) + 카드가 이름 변환 없는 모델 속성에 직접 바인딩.
  - **수정**: `EventCardViewModel<T>`에 이름 변환 `DeviceGroupsText` 속성 추가(`DeviceGroupProvider`로 Id→`DeviceGroupModel.Name` 조회, 미발견 시 Id fallback — 장비 관리 패널 `BaseDeviceViewModel` 패턴 재사용). 두 카드 뷰 바인딩을 VM `DeviceGroupsText`로 통일. N:N 다중 그룹="구역 1, 10", 단일="구역 1".
  - **검증**: Events.Ui 빌드 0오류. 카드 회귀 테스트 2종(`Detection/MalfunctionEventCardView_ZoneBindsToViewModelDeviceGroupsText`) 통과. ⚠앱 재빌드 후 반영.
- **이벤트 카드 제어기 필드 테스트 정정 — 낡은 `ControllerId` 기대값** (Events.Ui/Tests · 커밋 HEAD부터 red였던 사전 실패)
  - **원인**: Phase19 테스트(`Detection/MalfunctionEventCardView_ControllerId_BindsToViewModelProperty`)가 카드 XAML에 `"ControllerId"` 문자열을 기대했으나, 카드 설계가 진화 — **탐지 카드=`ControllerDeviceNumber`(제어기 번호)**, **장애 카드=`ControllerDisplay`/`SensorDisplay`(장애타입 인지 표시)**. 바인딩은 전부 유효한 VM 속성(깨진 `Device.Controller.*` 경로 없음) → **XAML 정상, 테스트만 낡음**.
  - **수정**: 두 테스트를 `..._ControllerField_BindsToViewModelProperty`로 정정 — 탐지=`ControllerDeviceNumber` 검증, 장애=`ControllerDisplay`+`SensorDisplay` 검증(+`Device.Controller.*` 부재 유지). 카드 관련 8/8 green.
  - **힌트 문구 정정**: 제어기/센서 필드가 실제로 **번호**(DeviceNumber)를 표시하는데 힌트가 "아이디"였음 → 탐지·장애 4곳 `"…아이디"`→`"제어기 번호"/"센서 번호"`로 실측 정합. 빌드 0오류.
  - **데이터 경로 확인(버그 없음)**: 이벤트 로딩(`EventProviderService`)이 `ToXxxEventModel(_deviceProvider)` 오버로드 사용 → provider의 실제 device 반환, `DeviceProviderService`가 센서를 `includeController:true`로 로드(단건/대량 모두) → 제어기 정상 채워짐. **갭 정리(완료)**: Insert/Update 반환 매핑 6곳(`EventProviderService`)을 `null`→`_deviceProvider` 오버로드로 통일 → 생성/수정 반환 모델도 provider 조회로 Controller 포함. Insert/Update 테스트 6/6 green.
- **ACTION_REPORT NATS 발행 계약 복구 — from_event/device 누락 + from 오류** ([PRD](docs/prds/Action_Report_Nats_FullDto_Contract-prd.md) · [Plan](docs/plans/Action_Report_Nats_FullDto_Contract-prd-plan.md) · 태그 `before-action-report-fulldto` · Monitoring.Models/Events.Ui + 메인 솔루션 NatsDomainService)
  - **결함**: 조치보고 NATS `ACTION_REPORT` body가 `{content,user}`만 → `id`·`type_event`·`from_event`(이벤트/장비 식별자) 통째 누락 + `from`=SystemUuid(`"gis-monitoring"`). 수신자(NVR 카메라홈복귀/방송종료/경광등해제/VMS)가 대상 장비 식별 불가 → 복귀동작 마비. 원인=FR-01 "transport adapter" 리팩터가 Full DTO 채우던 로직 제거(설계 §2.4 Pattern1·§6.4 위반).
  - **수정**: `SendActionRequestMessage`에 `OriginEvent`(IExEventModel)+`ActionId` 추가 → 발행 5지점(수동 탐지/장애·배치·자동조치·자동복구)에서 채움 → `NatsDomainService`가 `ActionEventModel.ToActionEventDto()`로 from_event(device.id 포함) 구성 + `from="GIS"`. OriginEvent null 시 기존 최소 body fallback(하위호환).
  - **§6.4 device 필드 확장**: `ConvertDeviceToDto`에 `status`·`version`·`geolocation`(위·경도)·`controller_id`(`BaseDeviceDto` 신규 필드) 매핑 추가. 헤드리스 테스트가 `from="GIS"`+`device.id/status/version/controller_id` 검증.
  - ⚠ **잔여 명세 차이(공용 DTO 구조 결정 대기)**: `device_groups`는 우리 `DeviceGroupDto`가 `name`(≠명세 `name_group`)+추가필드라 미포함(수신자 EventMapping 라우팅 키) · `group_device`는 의미/출처 미확정 · geolocation은 위·경도만(고도/설명 없음). ⚠ 메인 솔루션 재빌드(앱 종료 후) 필요.

### Added
- **보고서(Report) 기능 — 표준/템플릿 생성·목록·템플릿 CRUD·미리보기(WebView2)·PDF** (Messages·Reports.Api·Reports.Ui 신규 + 외부 Monitoring 배선 · 태그 `before-report-feature` · `c896a63`/`f58add4`)
  - **신규 라이브러리**: `Reports.Api`(IReportApiService 14메서드 — 카탈로그/상태/템플릿 CRUD/생성/이력/삭제/미리보기HTML/PDF, EventApiModule 패턴 Bearer 파이프라인) + `Reports.Ui`(콘솔 셸 + 목록/생성/템플릿/편집/미리보기 VM·View, LiveCharts). DTO=Messages/Dto/Reports/*.
  - **생성**: '표준 전체(STANDARD, 전 섹션)' 또는 '템플릿 기반(CUSTOM — 저장 템플릿 드롭다운 선택)' + 제목/기간 → 202 요청 → 1.5s 폴링(원형 프로그레스 + 상태 뱃지). (혼동되던 정형/비정형 용어 폐기.)
  - **CRUD**: 목록(상태 뱃지·검색·삭제·PDF다운로드·미리보기), 템플릿(추가 POST/수정 PATCH/삭제, 편집 다이얼로그=이름·설명·공개·기간·컴포넌트, 전체조회로 구성 로드), 생성이력 삭제.
  - **미리보기 = WebView2 embed**: 서버 자립형 HTML(`/api/reports/preview/{id}`, 인라인 Chart.js/CSS)을 콘솔 오버레이 WebView2에 NavigateToString → 실제 차트 **오프라인** 렌더 + 확대/축소(ZoomFactor)·스크롤. (Playwright=Chromium 동일 엔진 렌더 실증.)
  - **외부 Monitoring 배선**: LeftMenu **REPORTS** 버튼(reports:view 게이팅)→ConductorControl `IHandle<OpenReportPanelMessageModel>`→ReportConsoleViewModel · Bootstrapper `ReportUiModule` 등록 + `Microsoft.Web.WebView2` 패키지(네이티브 로더 복사).
  - **함정 기록**: ①WebView2=HwndHost 고유크기0→오버레이 확정높이 필요 · ②미리보기 단일 네비게이트(로딩HTML 레이스 제거)+15s 타임아웃 · ③DataGrid RowHeight 토큰=30 한글짤림→명시 38 · ④저장 후 목록새로고침=선택해제→id 재선택 · ⑤ReportUiModule ApiSetupModel 하드캐스트→복사생성자. **서버 verb-RBAC 집행 확인**(무권한 403·admin bypass·무토큰 401, 무인증 preview PII 봉합). ⚠앱 재빌드 후 E2E(생성→미리보기 차트→템플릿 CRUD→삭제) 필요.
- **권한 그룹 관리 — 그룹 CRUD·계정 배정 + 권한부여 계정 미표시 수정** ([PRD](docs/prds/GOP_Permission_Group_Management-prd.md) · 태그 `before-permission-group-management` · Accounts.Api/Ui·Messages · `59632ec`/`d1edcda`/`bda89b0`/`4502a61`)
  - **① 권한부여 계정 미표시(버그 수정)**: `GrantManagementPanel`이 `GetUsers(limit=200)` 요청 → 서버 `users` limit 상한(`le=100`) 초과 **422** → 계정 콤보 공백(에러가 로그로만 삼켜짐). `200→100` + 로드 실패 시 팝업 안내(무증상 재발 방지).
  - **② 권한 그룹 CRUD/계정 할당(OQ-PG-01 Option A→B 확장)**: 임의 권한그룹 **생성·이름/설명 수정·삭제** + 권한 **매트릭스 편집**(기존) + **계정→그룹 상시 배정**(user.group_id 추가/해제). 예약 5등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)은 삭제·개명 금지(매트릭스 편집만). 전 기능 ADMIN(콘솔 탭 + 서버 require_admin 최종방어).
  - **구현**: Messages `UserGroupCreateDto`/`UserGroupUpdateDto`/`UserGroupAssignDto` · Accounts.Api 그룹 CRUD 5메서드(Create/Update/Delete UserGroup·GetUserGroupUsers·AssignUserGroup) · Accounts.Ui `PermissionMatrixPanel` 3화면(목록+CRUD툴바/매트릭스/구성원 배정). **서버 무변경**(user-groups CRUD 완비). 빌드0 · Accounts.Api **93/93**(신규 10). ⚠**구성원 해제 서버 제약**: `update_user`가 `group_id:null`을 무시(`users.py:535` `if group_id is not None`) → 현재 no-op. 클라 정직성 가드(`bda89b0`, 미반영 시 안내)로 무증상 실패 방지, **서버 수정 대기**(PRD V-03, 서버세션 이관). 배정(add)·그룹 CRUD·매트릭스는 정상. ⚠앱 재빌드 후 E2E(그룹 생성→계정 배정→매트릭스 저장→삭제) 필요.
  - **v5.4 Role Simplification 정합(`4502a61`)**: 서버가 등급 role 5→2(ADMIN/USER) 축소 + ADMIN/GUEST 등급그룹 DROP + 나머지 `Preset - X` rename(편집 허용). 패널의 하드코딩 예약 5등급 로직 제거 — 전 그룹(팀/Preset) 편집·삭제 허용, `Preset` 접두 인식(팀→Preset 정렬). (원장 L151 A→B 요청 대응.)
- **심볼/이미지 잠금 + 심볼 이름변경 싱크** ([PRD](docs/prds/Symbol_Lock_And_RenameSync-prd.md) · 태그 `before-symbol-lock-rename` · Monitoring.Models/GMaps.Db/GMaps.Ui · `6156d10`)
  - **잠금(FR-01~03, DB 영속)**: 레이어 패널의 각 심볼·이미지 leaf 앞 **자물쇠 토글 아이콘**(열림 `LockOpenVariantOutline`/잠김 빨강 `Lock`) + **우클릭 잠금/잠금해제 메뉴**(상태별 헤더·아이콘 플립). 잠긴 심볼은 맵에서 **클릭 불가**(`GetMarkerAtScreen`에서 제외 → 좌·우클릭 + `OnMapMarkerClicked` 가드로 편집모드 ON 포함 차단). `LayerTreeNode.IsLocked` 단일소스 → VM이 마커 `IsLocked` 적용 + DB 영속. 패널·메뉴·맵·DB 4곳 싱크.
  - **모델·DB**: `ISymbolModel`/`IImageModel`(+구현·복사생성자)에 `IsLocked`. `Symbols`·`Images` 테이블 `IsLocked` 컬럼(CREATE inline + 멱등 `ALTER ADD COLUMN` 마이그레이션, 기존 행 `DEFAULT FALSE`) + 7종 심볼·이미지 INSERT/UPDATE/SELECT·DTO 매핑. 재시작 후 잠금 유지.
  - **이름변경 싱크(FR-04)**: 심볼 leaf rename 활성 + 컨텍스트 메뉴 '이름 바꾸기' + 인라인 편집. `symbol.Title` → `UpdateSymbolAsync`(공통 Symbols 행, 타입무관) 영속 + 마커/속성창 싱크(Overlay Image 패턴). 맵편집 권한(`CanEditMap`) 게이트 — 잠금 변경도 동일 게이트.
  - **초기화**: 심볼 leaf=`CreateSymbolLeaf(symbol.IsLocked)`, 이미지 leaf=`InitIsLocked`(마커 기준, `LockChanged` 미발화로 DB 재기록·피드백 루프 방지).
  - GMaps.Ui 빌드0 · LayerTree 단위 **33/33**(잠금/이름 4종 신규) · code-reviewer(opus) H3/M2/L2 반영(복사생성자 누락·이미지 초기화 갭·권한 게이트·이미지 CREATE 컬럼·rename UI 어포던스). ⚠앱 재빌드 후 E2E(잠금→클릭차단→**재시작 유지**, rename→**재시작 유지**) 필요.
- **레이어 패널 재설계 v1 — 카테고리별 개별 Overlay 심볼 트리노드 + 드래그 리사이즈** ([PRD](docs/prds/LayerPanel_SymbolNesting_Resize-prd.md) · [스토리보드](Docs/reports/LayerPanel_SymbolNesting_Resize_Storyboard_Wireframe.html) · 태그 `before-layerpanel-symbol-nesting` · GMaps.Ui)
  - **개별 심볼 노드화(FR-01~04)**: SYMBOLS 섹션의 카테고리(카메라/센서/군사 등)를 펼침 노드로 승격하고 그 아래에 `_symbolProvider`의 개별 심볼을 자식 노드로 노출(비균일 4단계, PIDS=Section›Group›Category›Symbol). 개별 심볼 체크박스로 마커 가시성 토글(`ShowShape`/`IsLayerEnabled`, 런타임), 우클릭 '중앙으로 이동'으로 맵 팬. tri-state 카테고리→그룹→섹션 4단 전파.
  - **모델 이음새 해소**: 개별 심볼 리프는 `IMapLayerModel`이 아닌 신규 `ISymbolModel? Symbol` 페이로드 + 신규 `NodeType.Category` + 심볼 전용 이벤트(`SymbolVisibilityChanged`/`SymbolNavigateRequested`). 기존 `CanDelete = Model?.LayerType != "Symbol"`가 Model=null에서 TRUE로 역전되던 잠재버그를 leaf-kind 게이팅으로 차단. 카테고리 일괄 cascade로 tri-state O(n²) 재계산 제거.
  - **카테고리 조인**: 비PIDS는 `EnumMarkerCategory` 직매핑(**VEHICLES 보강**), PIDS는 `DeviceType`로 6 하위카테고리 분기, 미매핑은 '기타' 폴백, Title 공백/중복은 `{카테고리} #{Id}` 폴백.
  - **드래그 리사이즈(FR-05/06)**: E/S/SE Thumb 그립, 250×420→375×630(각 +50%), 좌상단 앵커 고정, Canvas 경계 2차 클램프, 높이 초기 Auto+MaxHeight 캡(off-canvas 차단). 크기는 세션 내 기억(세션 간 영속=v2).
  - GMaps.Ui 빌드0 · 단위테스트 **148/148**(신규 19: DeviceType 분기·심볼 중첩·폴백·cascade·레거시 호환) · 설계검증 wf_d15d5365(4 렌즈+2 적대비평) 반영. **v2 보류**: 개별 심볼 삭제/이름변경·전면 가상화·검색바·세션간 크기 영속. ⚠앱 재빌드 후 E2E 런타임 검증 필요.

### Fixed
- **3rd Party(Gateway) 이벤트 그룹 쓰레기값("1, 116") 근본수정 — 레거시 `Group` 컬럼 부활 차단** ([PRD](docs/prds/GatewayEvent_Group_Resurrection_Fix-prd.md) · [Plan](docs/plans/GatewayEvent_Group_Resurrection_Fix-prd-plan.md) · Gateway lib · `0275776`)
  - 증상: 설정 > 3rd Party 이벤트 설정의 "그룹" 컬럼에 의도치 않은 그룹 ID가 섞여 표시(예: Event_A가 `116`이어야 하는데 `1, 116`). 원인은 **DataGrid가 아니라 DB 프로바이더 측**.
  - 근본 원인: `GatewayDbService.BuildSchemeAsync`의 레거시 이행 쿼리(`INSERT IGNORE SELECT Id,Group WHERE Group>0`)가 **매 앱 시작마다** 실행되어, N:N 전환 후에도 남은 단일 `GatewayEvents.Group` 값을 연결 테이블로 **부활(resurrection)**시킴. `UpdateGatewayEventAsync`가 레거시 컬럼을 비우지 않아 영구 반복. (선행 `GatewayEvent_Group_NtoN_Migration` PRD가 "다음 릴리스"로 연기한 컬럼 DROP의 부작용.)
  - 수정: 상시 이행 쿼리 제거 + `information_schema`로 레거시 컬럼 존재 시에만 **1회 실행되는 자기비활성화 마무리 블록**(`FinalizeLegacyGroupColumnAsync`) — ①최종 안전 이행 → ②좀비 정리(그룹 2개 이상 이벤트에서 `GroupId=레거시 Group` 행 삭제, count>1 가드로 유일값 보존) → ③`Group` 컬럼·`IX_Group` DROP. 신규 설치 DDL에서도 레거시 컬럼 제거. 외부 솔루션 무변경(`NatsDomainService` 이미 Intersect).
  - 검증: 통합 5종(좀비 제거·Group=0 무영향·유일값 보존·컬럼 DROP·이중실행 멱등) + 실 `monitor_DB` 덤프 사본 E2E(실 코드 경로 Event_A→`116`/Event_B→`117`·컬럼 제거·멱등) 통과. MariaDB 12.2 `DROP COLUMN`→`IX_Group` 동반삭제 실측. 빌드0.

### Added
- **강제 로그아웃 전파 — GIS(클라) Phase 1** ([PRD](docs/prds/GOP_Force_Logout_Propagation-prd.md) · [보고서](docs/reports/GOP_Force_Logout_Client_Phase1-report.md) · Accounts.Api/GMaps.Ui · `b15359b`)
  - 단일 멱등 진입점 `ISessionLifecycle.ForceLogoutOnce`(Interlocked once-guard — NATS/401/수동 수렴) + TokenStorage jti·세대가드(refresh 부활 차단, FR-FL-05) + `BearerAuthHandler` 401폴백 배선 + GMaps `ForceLogoutRequested` 구독→PTZ정지·팝업(스트림)해제(FR-FL-07).
  - 검증: 단위 신규6→Accounts.Api 73/73, GMaps.Ui 통합빌드0, **E2E(라이브) 강제로그아웃 후 access·refresh 401 무효화 확인**.
  - 후속(서버계약/메인솔루션): NATS 즉시푸시(FR-FL-02)·유휴 하트비트(06)·셸 가림막/로그인 전환(08)·서명(10)·session_id 정밀매칭.

### Fixed
- **PTZ 응답성(큐잉 제거) + 팬틸트/줌 속도 반영** ([PRD](docs/prds/CameraPopup_PTZ_Responsiveness_Speed-prd.md) · GMaps.Ui · 머지 `4ebb93a`)
  - **큐잉 지연 해소(A)**: 방향 패드·줌 버튼 핸들러가 `BeginPtzGesture` 취소토큰을 `ContinuousMoveAsync`에 미전달해 Gate 큐가 무한 누적되던 버그 수정 — 이제 새 제스처가 직전 대기명령을 LWW 취소(드래그/휠과 동일). `BeginPtzGesture`를 `ConcurrentDictionary.AddOrUpdate` 원자 교체로(경합 수정).
  - **속도 미반영("날아감") 해소(B)**: `ContinuousPanTilt/ZoomVelocitySpace`의 범위(XRange/YRange)를 캡처하지 않아 raw `[-1,1]×speed`를 보내 카메라 범위와 안 맞으면 속도 슬라이더가 무효화되던 버그 수정 — `PtzVelocityMath.ScaleToRange`로 정규화 속도를 카메라 연속속도 범위로 스케일(0=정지 보존, 부호별 풀스케일). 드래그·패드·줌버튼·휠 **전 경로**가 단일지점 통과 → PanTilt/Zoom 속도 실반영. `[-1,1]` 표준 카메라는 항등(회귀0).
  - Stop-before-fire는 미적용(ONVIF §5.3.2 ContinuousMove 자동대체, 추가 시 ~100ms 역효과). 진단로그에 `norm→scaled`+카메라 범위 노출(필드 진단). 단방향/비대칭 범위 1회 경고.
  - 설계검증 wf_7ad8437b(architect+code-reviewer) + opus 리뷰 MERGE. 빌드0·단위테스트 168(PtzVelocityMath 24 신규). 롤백 `before-ptz-latency-speed`.
  - ✅ **E2E 통과**(라이브 카메라, 사용자 승인): 범위 [-1,1] 표준 / v=0.2 vs 0.8 → ~6.7배 속도차 실증 → 카메라가 velocity magnitude 정상 존중 확인. 즉 증상의 진짜 원인은 큐잉(A)였고 LWW가 해소(B 스케일은 [-1,1] 카메라엔 항등·비표준엔 방어). 앱 런타임 체감은 사용자 최종 확인.

### Added
- **카메라 팝업 줌·포커스 Press-Hold 제어** ([PRD](docs/prds/CameraPopup_PressHold_PtzZoomFocus-prd.md) · [Plan](docs/plans/CameraPopup_PressHold_PtzZoomFocus-prd-plan.md) · GMaps.Ui · 머지 `0fbb261`)
  - 줌 ±·포커스 ± 버튼을 클릭=펄스 → **누르면 연속(ContinuousMove/ContinuousFocus)·떼면 정지** press-hold로 전환(방향 패드 패턴 통일). 통합 Tag 메커니즘(`PtzGestureTag`) + CaptureMouse 릴리즈 보장 안전망 + `_activeGesture` 타입별 정지 라우팅.
  - 🔴 포커스 정지는 ImagingClient 별도 경로(신규 `StartFocusAsync`/`StopFocusAsync`) — PTZ `StopAsync`(StopPTZ)로는 포커스 모터가 안 멈춤. 정상릴리즈/캡처유실(Alt+Tab)/팝업닫기/제스처전환 **4경로 모두** 올바른 모터로 라우팅(code-review 치명결함 해소).
  - **FR-PH-10**: 연속 포커스 속도를 카메라 `GetMoveOptions` ContinuousFocus 범위로 클램프(`PtzFocusMath`) — 0.7이 범위밖이라 무동작하던 F항목 해소. 포커스 게이팅을 외곽 `IsPtzCapable` 밖으로 분리(`IsImagingCapable` 독립) — 영상전용 고정카메라 수동포커스 도달가능(E항목 해소).
  - v2.6 PTZ 권한 게이팅(cam:control) 통합: ZoomHold/FocusHold=`CanControlCamera` 게이팅 · FocusStop=무게이팅(정지 항상 허용). 휠 줌 펄스 유지, 펄스 잔재(`MoveFocusAsync`·OnCameraPopupFocus·줌/포커스 Command) 제거.
  - 설계검증 wf_da23975b(architect SOUND_WITH_CHANGES + code-reviewer MERGE_WITH_FIXES). 빌드0·테스트 **144**(PtzGestureTag/PtzFocusMath 신규). 롤백태그 `before-presshold-ptz-zoomfocus`.
- **권한 실제집행 — 클라 게이팅 (GOP_Permission_Enforcement, 서버독립분 완료)** (라이브러리 GMaps.Ui/Devices.Ui/Events.Ui)
  - **FR-EN-06 PTZ**(`663c45e`): MapViewModel PTZ 제어 핸들러 10곳 `CanControl("cameras")` — 서버 ONVIF 미중계라 클라 단독 권위집행, 안전정지(Stop) 제외, EnsurePtzReady IsPtzCapable 이중방어.
  - **FR-EN-09 장비**(`a4e63f1`): 7패널 CRUD `CanEdit/CanDelete("devices")` + `DevicePermissionGate` 공유 헬퍼.
  - **FR-EN-10 이벤트**(`3282de8`): ACK=`CanControl`·CRUD=`CanEdit/CanDelete`("events") **독립 게이팅**(FR-PG-08), 배치 가드(_batchReportGate 이전), 자동경로 제외.
  - **FR-EN-11**(장비/이벤트): `PermissionsChanged` 구독 → `Execute.OnUIThread` 버튼/커맨드 재평가(역할강등 즉시 반영).
  - 주입=GMaps/Devices/Events.Ui→Accounts.Api ProjectReference + `IoC.Get<IPermissionService>()` lazy(미등록 전체허용 폴백, V-EN-11). 빌드0·GMaps.Ui 통합빌드0·Accounts.Api 62/62.
  - **FR-EN-05 모듈(`b1037f5`)·FR-EN-07 방송·FR-EN-08 맵편집(`ff4c0d7`) 완료**: 서버 enum이 map/broadcast 수용 확인 + §6-1 등급별 값 API 시드 후 게이팅. 방송 발행 2곳 + 맵 심볼/오버레이/ROI/레이어 13곳, FR-PG-11(로컬렌더 허용·DB영속만 게이트). GMaps PTZ 강등 재평가(`f353f28`)·사용자수 실수정(`c693ddb`)도 포함. → **클라 집행 PRD(FR-EN-05~11) 전체 완료.** ⏸ 남음=AUTH_MODE=token 전환 시 클라 Bearer 배선(FR-EN-03③)+GOP-07 가림막.
- **카메라 PTZ "특정 위치 확인" → NATS 좌표 발행** ([PRD](docs/prds/Camera_PTZ_AimLocation_Nats-prd.md) · [Plan](docs/plans/Camera_PTZ_AimLocation_Nats-prd-plan.md) · GMaps.Ui)
  - 맵에서 **PTZ 카메라 심볼 우클릭 → "특정 위치 확인" → 커서가 조준(Cross)으로 바뀌고 카메라 중심 반경 30m 원 표시 → 영역 안 클릭 → 해당 좌표를 NATS PUB로 발행**(카메라 회전 요청 + 좌표). 클라는 직접 회전하지 않고 좌표만 전달하며, 실제 PTZ 회전(지리 방위→pan/tilt)은 서버/NVRManager가 수행. 영역 밖 클릭·ESC·우클릭 = 취소.
  - PTZ 전용 노출: `ICameraDeviceModel.Category == EnumCameraType.PTZ`인 카메라에서만 메뉴 항목 표시(동기 판정, DB 스키마 변경 없음).
  - 신규 NATS 발행 서비스 `ICameraAimControlService`/`CameraAimControlService`(`BroadcastControlService` 패턴 + 경계검증·try/catch·`ConfigureAwait(false)`·`CancellationToken` 보강) — subject `{Domain}.{Group}.nvr_manager.camera-aim`, cmd `CAMERA_AIM_LOCATION`(신규 `EnumGopCommand`=14), body `CameraAimLocationBodyDto`(camera_id·타겟/카메라 lat·lng·distance_m·bearing_deg·requested_by).
  - 반경 판정은 지오 도메인(`CameraAimMath.IsWithinRadius`=`HaversineMeters ≤ R`, 줌 무관)으로 분리하고 화면 원은 `GMapCustomControl.OnRender`에서 지오 앵커로 그려 팬/줌/디지털줌에 자동 추종. 좌클릭 가로채기는 `OnMouseLeftButtonDown`의 라인드로잉 분기와 동급 위치(`base` 전 `e.Handled`)로 팬·마커선택·이미지편집·더블클릭 차단 + 모드 상호배제.
  - 반경 설정값 `ITrackingSetupModel.CameraAimRadiusMeters`(기본 30m, appsettings `Tracking` 섹션). 순수 로직(`CameraAimMath`/`CameraAimRequestBuilder`) xUnit 15종 신규. GMaps.Ui 빌드0·테스트 124/124. 분석=Explore×4+architect+code-reviewer 체인.
- **권한 모델 일원화: 역할(등급) = 권한 단위 (PRD-GOP-01 OQ-PG-01 = Option A)** (라이브러리 + 서버 `api-test-server`)
  - 기존엔 "구분(역할)"과 "권한 그룹(매트릭스)"이 따로 떠 있고(레벨/역할/그룹 3중), 실제 집행은 역할 하나뿐 + 그룹 매트릭스는 사용자 배정 경로조차 없는 고아 설정이었음. → **역할(등급)을 단일 권한 단위로 통합.**
  - **서버**: `initialize_database`에 `ensure_role_permission_groups` 추가 — 5개 역할명 등급그룹(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)을 idempotent 보장(PRD §6-1 기반 기본 8×4 매트릭스, 기존 팀그룹 비파괴). 로그인 권한 유도를 `group_id` → **`user.role` 명 등급그룹 매트릭스**로 변경(`auth.py`) — 역할이 권한을 결정. 도커 재빌드+재기동, 라이브 검증(admin이 빈 권한 대신 FULL 매트릭스 수신).
  - **클라**: `PermissionService` **ADMIN 무조건 통과**(서버가 ADMIN 권한 비워 보내 발생하던 잠복 차단 버그 수정). 권한 화면을 "권한 그룹"→**"권한 등급"**으로(5등급만 등급순 표시·한글 라벨, 임의 그룹 추가/삭제 제거). 계정의 "구분"이 곧 권한 등급. 빌드0·Accounts.Api 62/62.
- **권한 그룹 편집저장 (PRD-GOP-01 IMPL-06) — 서버 권한수정 API 신설 + 클라 매트릭스 편집** (라이브러리 + 서버 `api-test-server`)
  - **서버**: 신규 `POST /api/user-groups/{id}/permissions`(ADMIN 전용 `require_admin`). 일반 PUT이 권한상승 방지로 `permissions`를 차단(v4.8 Phase 12-7a)하던 것을 ADMIN 전용 경로로 재개. `PermissionsSchema` strict 검증(미정의 모듈/verb→422) + `PERMISSION_CHANGED` 감사(append-only). pytest 3종(200/404/422)·라이브 E2E(admin 저장·422·404·audit) 통과. swagger=route docstring(원천), 도커 이미지 재빌드+컨테이너 재기동. 안전점 `pre-perm-edit-endpoint`.
  - **클라**: `IAccountApiService.UpdateGroupPermissionsAsync`(기본 인터페이스 구현=테스트 스텁 무영향) + `AccountApiService` POST 호출. `PermissionMatrixPanel` 상세 매트릭스 편집가능화(체크박스 `OneWay→TwoWay`, `ModulePermRowViewModel`→`PropertyChangedBase`) + [저장] 버튼 활성·`OnClickSave`(Modules→`PermissionsDto`, device_groups 보존, 성공 시 재조회·목록복귀). 빌드0·Accounts.Api 62/62. 안전점 `before-perm-edit-save`.

### Fixed
- **카메라 팝업 3종 수정** (Track B · 커밋 `4900093`/`58b3fd7`/`5edbfb1`)
  - **66번 영상 프리즈**: Hub 플레이어(`CameraStreamEntry`)가 오디오 미차단 → 오디오 포함 스트림(`…/video1+audio1`)만 vmem 비디오 콜백 stall로 정지. `media.AddOption(":no-audio")`로 해결(비디오 전용 스트림 무영향). VLC `--no-audio` 캡처로 카메라 스트림 라이브 확인.
  - **팝업이 윈도우 타이틀바 침범**: `CameraStreamPopupViewModel.CanvasTop` 세터에 `MinCanvasTop`(=0) 하한 클램프 — 상단 카메라 팝업이 `ClipToBounds=False` 캔버스에서 위로 넘쳐 MahApps 타이틀바(최대/최소/닫기)를 덮던 문제(MahApps 아닌 앱 오버레이가 원인).
  - **PTZ 속도 슬라이더 부동소수 노출**: `PanTiltSpeed`/`ZoomSpeed`를 `Math.Round(.,1)`로 0.1 단위 반올림 + 슬라이더/표시 0.1 스냅(`0.2999…`→`0.3`).

### Changed
- **API·SVMS HTTP→HTTPS 전환** (라이브러리 `5edbfb1` · 메인 `162547f` · [영향분석](docs/analyses/Http_To_Https_Migration_Impact-analysis.md))
  - 데이터 수신 API 베이스 URL(appsettings `Url`) http→https. 모든 도메인 API가 공유 `ApiService`(단일 HttpClient) 경유. mkcert 사내 root CA 신뢰 등록으로 .NET 기본 검증 통과 — **인증서 코드 변경 0**. 끝단 검증 `https://localhost:8000/`→200·TLS OK.
  - SVMS 장비상세 링크 스킴 https(`DeviceDetailUrlService` `WebScheme` const, 테스트 25/25). ⚠ SVMS 서버도 https listen 필요.

### Added
- **Tracking Playback 데이터소스 토글 (로컬 DB ↔ 서버 API)** ([PRD](docs/prds/Tracking_Playback_DataSource_Toggle-prd.md) · [Plan](docs/plans/Tracking_Playback_DataSource_Toggle-prd-plan.md) · 커밋 `6024a71`/`5a662ad`/`0d0948a`, 브랜치 `feature/track-datasource-toggle`, 태그 `before-track-datasource-toggle`)
  - Playback reader를 설정에서 **로컬 DB / 서버 API** 선택(라이브 토글, 무재시작). `ITrackPointReader` seam만 — 라이브 오버레이·write(인제스트) 경로 무변경. 기본=Local(스테이션 #2까지).
  - 신규 `Tracking.Api`(Events.Api 미러: cursor loop `GET /api/tracking/points`) + `TrackPointApiReader`/`TrackDataSourceSelector`(FetchAsync마다 분기) + `EnumTrackDataSource`(Local/Api; Hybrid v2) + `TrackPointDto`/`TrackApiListResponse`/`TrackCursorDto`(cursor envelope) + `TrackPointDtoMapper`(KST `+09:00`→UTC, AssumeUniversal) + 설정 콤보 UI(`EnumDisplayNameConverter`) + DataSource 영속(MapSettingsHelper — 기존 누락 MaxPlaybackHours/RetentionDays도 보강).
  - 분석 체인(Explore×3→architect→**code-reviewer FLAWED 판정**) 적대검증 수정 전부 반영: `GetRequestAsync` ct無·첫페이지 cursor omit·`AsImplementedInterfaces`(ExecuteAsync 트리거)·`ITrackPointReader` 중복바인딩0·nullable 매퍼·webSetup Url부재→ApiSetupModel 명시. 빌드0·매퍼 7테스트·전체 회귀0(126/126).
  - 🔲 후속: 메인솔루션 Bootstrapper `GMapUiModule(..., trackingApiSetup: GOP ApiSetupModel)` 연동(**사전통지**)·v2.6 머지·앱 재빌드 런타임. **서버측(별도 repo `api-test-server`)** GET /api/tracking/{points·sessions·health} + `gis-ingest` 워커 = 배포·mock E2E 완료(차수 v4.11/v4.12).
- **Tracking GIS 시각화 + 트레일 + TTL + 설정 (P1~P3) — Foundation 착수** ([PRD](docs/prds/Tracking_GIS_Visualization_Playback-prd.md) · [Plan](docs/plans/Tracking_GIS_Visualization_Playback-prd-plan.md))
  - **계약 확정(V-CONTRACT-1)**: `Gop_Message_Broker_연동설계.md §8.3.7`(권위 SoT)로 TRACKING_STATUS 메시지 검증 — `targets[]`/`track_id`/`observed_at`/`threat_level`/`location` 전부 필수. 설계 보강 6건(복합키·lost/idle 제거·Unknown 클라폴백·소문자변환·ttl기본5·car·vehicle) PRD 반영.
  - **P1 Foundation(빌드0·테스트 183/183)**: ① `EnumThreatLevel`(NORMAL/CAUTION/THREAT+Unknown 클라폴백)·`EnumTargetType`·`TrackingEnumExtensions`(안전파싱+ToColorType, 토큰스캔으로 `armed_person`→Person 견고화) 신설(`.Enums`) ② `IClock`/`SystemClock` 신설(`.Base`, 규칙 I-02) ③ **DTO 전면 교체**(`.Messages`): `TrackingStatusBodyDto` 단수 `target`→다중 `targets[]`+`ttl_sec`/`frame_w·h`, 신규 `TrackingTargetDto`. Phase26 단위테스트 재작성(역직렬화/idle/enum폴백).
  - 롤백 태그 `before-tracking-gis`(@62dd557), worktree `v2.13.0`. 계약 인터페이스=Events.Ui 배치(비순환), Enum=`.Enums`·마커=`GMaps.Ui/GMapSymbols`(경로 정정).
- **UI Modern Dark/Light 테마 디자인 시스템 — Phase 1 완료** ([PRD](docs/prds/UI_ModernTheme_DesignSystem-prd.md) · [Plan](docs/plans/UI_ModernTheme_DesignSystem-prd-plan.md))
  - 신규 leaf 어셈블리 `Ironwall.Dotnet.Libraries.Theme`(net8, MD/Colors 5.2.1 + MahApps 2.4.10, GMap/*.Ui 무참조). 토큰 딕셔너리 5종 — `Tokens.Light`(현재 출고 byte-identical, AD-6) / `Tokens.Dark`(Modern Dark) / `Tokens.Shared`(radius·density·font) / `Converters` / `Theme.Current`(스왑 컨테이너).
  - `IThemeService`/`ThemeService`: Add-new→Remove-old 토큰 dict 원자 스왑 + PaletteHelper(MD) + ThemeManager(MahApps) 듀얼 엔진 단일 Dispatcher 패스 + `ThemeChanged`(비-WPF 경로 재색칠) + R-17 중복차단 + 영속화 seam(`IThemeSettingsStore`). 토큰 팩토리/MergedDictionaries 주입형으로 헤드리스 테스트 가능.
  - `ThemeKeyLinter`(RISK-03): 참조-vs-정의 키 검증 + Light≡Dark 파리티 게이트. **빌드 0에러 · 테스트 11/11**(ThemeService 7 + 린터 4). 롤백 태그 `before-modern-theme-migration`, worktree `v2.12.0`.
  - **Phase 2 라이브러리측 진행(IMPL-08·IMPL-09·TEST-12)**: `Accounts.Ui`/`Devices.Ui`/`Events.Ui`/`Sounds.Ui` csproj에 Theme 어셈블리 ProjectReference 배선(AD-1 순환 없음 — leaf 확인, 4개 전부 빌드 0에러). 토글 스모크 `ThemeToggleSmokeTests`(Toggle 라운드트립·ThemeChanged 시퀀스·dict 누수 가드) 3종 추가 → **테스트 14/14**. GMaps.Ui `Generic.xaml` 토큰 병합 설계안 문서화([docs/design/IMPL-09_GMaps_Generic_Merge_Design.md](docs/design/IMPL-09_GMaps_Generic_Merge_Design.md)) — 실제 편집은 Phase 6(동시 PTZ 세션 게이트). **외부 메인앱 배선(EXT-01/02/06)은 사전통지 후 진행 예정.**
  - **Phase 4 — Events.Ui SkiaSharp 차트 theme-aware(IMPL-21, FR-13)**: LiveChartsCore 페인트는 WPF 토큰 미도달 → 신규 `ChartThemeProvider`로 일원화. 축/범례/툴팁 텍스트 = theme-aware(`TextColor`: Light #1C1B1F·Dark #EDF1F6, 기존 흰축라벨↔어두운범례 모순 해소) · 세그먼트 위 라벨/스트로크 = 고정백(`OnSeriesFixed`) · 한글 타입페이스 일원화(`KoreanTypeface`, Malgun 보존; Noto 통일은 EXT-07). `EventInfoViewModel`에 **IThemeService guarded-optional 주입**(EXT-02 등록 전이면 null→Light 기본, graceful) + `ThemeChanged` 구독 시 열린 차트 rebuild + 구독해제. `DataChartPanelViewModel` 범례 중앙화. **V-07: 하드코딩 SKColor white·FromFamilyName 잔여 0**(provider 외), Events.Ui 빌드0. ⚠ 라이브 recolor 활성·V-08 시각검증은 EXT-02(IThemeService 등록)+앱 렌더 후.
  - **Phase 4 전파 — Events.Ui in-pattern 토큰화(IMPL-20 부분)**: 이벤트 카드/리포트의 확정 패턴 색 이관 — 카드 테두리·구분선 `#33000000`→`DividerBrush`·카드 배경 `White`→`SurfaceBrush`(byte-identical) · ColorZone 헤더 텍스트 `White`→`OnPrimaryFixedBrush`(byte-identical, on-primary 고정백) · 탐지/오류 severity `#FFDD2C00`·`Crimson`→`StatusCriticalBrush`(정규화). ② **신규 틴트/악센트 토큰화 완료**: KPI/severity 반투명 틴트 6색 → 신규 토큰 `TintInfo`/`TintSuccess`/`TintCritical`/`TintWarning`/`TintAccent`/`SurfaceTranslucent`(**Light=byte-identical 기존 hex**, Dark=다크표면 대비 별도값, storyboard/V-11 정밀화) · `DodgerBlue` 툴바→`PrimaryBrush` · 악센트 `#40C4FF`→`AccentBrush` · `WhiteSmoke`→`OnPrimaryFixedBrush` · muted `#88000000`→`TextSecondaryBrush`. EventInfo/CameraEventInfo/EventCardList/MalfunctionEventCard 등 6파일. **신규 토큰 Light≡Dark 파리티 린터 통과(테스트 18/18)**, 빌드0. 의미색·틴트 정규화는 V-11 시각 사인오프 대상.
  - **Phase 4 전파 — Sounds.Ui/Devices.Ui 토큰화(IMPL-18/19)**: Sounds.Ui 비활성 텍스트 `Gray`×8(SoundSettingView 7 + Resources.xaml 1)→`TextMutedBrush`. Devices.Ui AddSensorDialog 흰 테두리/제목 `White`×4→`OnPrimaryFixedBrush`(byte-identical #FFFFFF, white-on-colored 헤더). 둘 다 빌드0. (`SystemChrome*` 색 클론·Sounds Resources 폰트/converter 허브 통합은 Plan상 "부분"이라 이연. Devices `#FFE0B2` draft-row는 현 코드 부재.)
  - **Phase 4 파일럿 — Accounts.Ui 토큰화(IMPL-16/17)**: 7개 뷰(Login/Logout/MyPage/AccountManager/AccountSetup Panel + Editor/Register Dialog)의 하드코딩 색을 DynamicResource 토큰으로 이관. **byte-identical(AD-6)**: `#33000000` 구분선 12곳→`DividerBrush`(동일 hex) · `#88000000` 모달 스크림→`ScrimModalBrush`(동일 hex). **의도적 severity 정규화(FR-08, V-11 시각 사인오프 대상)**: 로그인 실패 테두리/결과 `Red`→`StatusCriticalBrush`(#C0392B) · 성공 `Green`→`StatusNormalBrush`(#2E9E5B) · 비활성 `Gray`→`TextMutedBrush`(#999999). 도입 토큰 5종 전부 Theme 정의 해소 확인(린터), 빌드0. ⚠ **런타임 해소는 앱이 Theme.Current 병합(EXT-01) 후 — 머지 시 EXT-01 선행 필수.** VER-11a 픽셀 diff는 앱 렌더 필요→이연. (다음: Sounds/Devices/Events 동일 패턴 전파 전 파일럿 패턴 확정)
  - **Phase 3 공용 컨트롤 스타일셋 완료(IMPL-12/13/14/15)**: Theme 어셈블리 내 keyed 스타일 3파일 신설 — `Styles.Controls.xaml`(Button Primary/Secondary/Danger/Text·TextBox·PasswordBox·ComboBox·Body/Mono TextBlock) · `Styles.Containers.xaml`(DataGrid 헤더/셀/행 hover·selected·Card·Dialog 패널/헤더) · `Styles.Nav.xaml`(툴바 버튼/토글·NavRail 탭·StatusBadge Critical/Warning/Normal/Info severity hue-lock). **implicit 금지(keyed only) → AD-6 Light byte-identical 보존**, MD BasedOn 유지+색/radius/font 만 DynamicResource 토큰. `Theme.Current.xaml`에 병합(소비자 단일 진입점). IMPL-15 OnPrimaryFixedBrush(양 테마 #FFFFFF white-on-blue 헤더)는 토큰 기존재+ModernDialogHeader로 실현. 검증: 스타일 참조 토큰 전수 정의 확인 테스트(`StyleTokenReferenceTests`) → **빌드 0 · 테스트 18/18**. (VER-06 시각 렌더는 파일럿 Accounts.Ui/Phase 4에서 재확인)
- **Accounts.Ui 라이브러리 추출 — Phase 0 착수** ([PRD](docs/prds/Accounts_Ui_Library_Extraction-prd.md) R3 · [Plan](docs/plans/Accounts_Ui_Library_Extraction-prd-plan.md))
  - 외부 Monitoring 솔루션에 산재한 계정 UI(VM 11 + View 8 = **27파일**)를 신규 WPF 라이브러리 `Ironwall.Dotnet.Libraries.Accounts.Ui`로 이관 준비. **Gateway seam**(`IAuthGateway`/`IUserDirectoryGateway`/`IProfileGateway` + `DbAccountGateway` 어댑터)으로 데이터 접근 역전 → 후속 GOP API 연동(GOP-00) 시 VM 재편집 0.
  - 동반 결함 수정 예정: C-1 공유싱글톤 Clear, H-4 중복확인 경쟁, async void/ct 미전달, DeleteAccount 2중루프 버그, L115 토큰 평문 로깅·`"12345678"` 하드코딩 제거. 롤백 태그 `before-accounts-ui-extraction`, worktree `v2.9.22`.
  - **Phase 1 완료(빌드 0, 테스트 10/10)**: 신규 프로젝트 `Ironwall.Dotnet.Libraries.Accounts.Ui` + **Gateway seam**(`IAuthGateway`/`IUserDirectoryGateway`/`IProfileGateway` + `AuthResult` in `Accounts/Gateways`, `DbAccountGateway` 어댑터) + `SessionConfigService`/`ProfileImageService`(+`ProfileImageHelper`) + `AccountUiModule`(`useDbAuth` 플래그). 별도 `.Accounts.Ui.Tests`(10 테스트). P-CHK-2(Framework 충돌) 무해 확인.
- **맵 위 이동식 RTSP 스트리밍 팝업** ([PRD](docs/prds/Rtsp_Map_Popup-prd.md) v1.3 · [Plan](docs/plans/Rtsp_Map_Popup-prd-plan.md))
  - 맵 카메라 심볼 **더블클릭** → 카메라 우상단(중점 +오른쪽100/위100)에 **이동식 RTSP 영상 팝업**. Geo 앵커로 팬/줌 추종, 드래그 이동 위치를 **카메라별 DB 영속**(`CameraPopupPositions`, 다중 클라 공유)해 재오픈 시 복원. 멀티 팝업 + 중복=기존 포커스, **심볼 제거 시 자동 닫기(FR-13)**, 크게보기 384↔640.
  - 참조 솔루션 `Dotnet.Rtsp.Viewer.Ui`의 `Streaming(.Base)`(LibVLCSharp 3.9.4, **Hub 공유 디코더**) 이식. 맵 오버레이 airspace hole 회피 위해 **Hub WriteableBitmap(IsHubMode)** 경로. `CameraStreamPopupControl`/Style(관심지역·레이어 창 답습)+VM, `CameraConnectionAdapter`(Urls.RtspSub→RtspMain→Ip), 더블클릭 배선(GMapCustomControl 편집/일반 공통), `CameraPopupPositionStore`(DB+인메모리폴백).
  - NFR-07 패키지 정합(Caliburn.Micro 5.0.258→4.0.230, Autofac 8.4.0→8.3.0). 네이티브 libvlc/plugins 배포. code-review(opus). ※메인솔루션 Bootstrapper에 `StreamingModule` 등록 필요(미등록 시 팝업 비활성).
- **MBTiles 베이스맵 빈 영역 기본 타일(no-data tile)** ([PRD](docs/prds/BaseMap_NoData_DefaultTile-prd.md) · [Plan](docs/plans/BaseMap_NoData_DefaultTile-prd-plan.md))
  - MBTiles 베이스맵 커버리지 밖으로 팬/줌 시 흰 화면 대신 **깔끔/모던 기본 타일**(라이트 뉴트럴 #EEF1F5 + 우/하 1px #DFE3E8 hairline)을 맵 격자에 정렬해 타일링 표시.
  - `MBTilesMapProvider.DefaultTileBytes`(byte[]) 추가 + `GetTileImage`가 **정상 줌 & 타일 없음일 때만** 기본 타일 반환(맵 미로드·줌 범위 밖은 null 유지 → 로드실패 은폐/부모타일 폴백 보존). 공유 인스턴스 금지(`GetTileImageFromArray` 매 요청 새 PureImage → use-after-dispose 회피).
  - 신규 WPF 헬퍼 `DefaultTileImageFactory`(256×256, 96DPI/Pbgra32, Freeze, Dispatcher 마샬링, 1회 캐시). `MapViewModel` Init/Switch에서 UI 스레드 주입. Core(WPF 비의존)는 byte[]만 보관.
  - architect+code-reviewer(opus) 검증, xUnit 회귀 6케이스(결정 테이블 a/b/c). ※GMap.NET.Core는 미추적 벤더(고아 서브모듈)라 해당 1파일은 git 외 — 수동 백업 `MBTilesMapProvider.cs.bak-before-basemap-nodata-tile`.
  - **각 타일 정중앙에 센서웨이 로고** 배치(가로·세로 가운데 정렬, 타일 반복 → 빈 영역 워터마크 패턴). `sensorway.png`(150×50)를 GMaps.Ui Resources에 임베드(pack URI), `DefaultTileImageFactory`가 로드 실패 시 격자만 표시(graceful). 로고 크기/투명도/여백 상수화.
- **함체 임계값(Threshold) 설정 다이얼로그 — P1 라이브러리** ([PRD](docs/prds/EnclosureThresholdDialog-prd.md))
  - 함체 임계값(온/습도 상하한·진동) 편집 다이얼로그 신설 — 카메라 상세(Conductor.Collection.OneActive) 패턴 복제: `EnclosureThresholdDialogViewModel`+`EnclosureThresholdSettingViewModel`/View, `OpenEnclosureThresholdDialogMessageModel`, 함체 SelectionView '임계값' 버튼(단일선택).
  - **매핑 보강(BLOCKER급)**: `DtoToModelHelper`가 threshold_config(JObject)↔`EnclosureThresholdConfigModel` 양방향 매핑(이전 드롭). `EnclosureDeviceDto.ThresholdConfig` NullValueHandling.Ignore. `DeviceEquals`에 임계값 비교(null=빈객체 동등), `UpdateDeviceProperties` 임계값 복사. xUnit 7. (P2 메인솔루션 wrapper/핸들러/등록 별도)
- **스피커 방송서버(server_id) 배정** ([PRD](docs/prds/SpeakerServerAssignment-prd.md) v1.1 · [Plan](docs/plans/SpeakerServerAssignment-prd-plan.md))
  - 스피커 속성패널에 방송서버 드롭다운(ServerProvider) — 선택/변경 + 신규 추가 시 첫 서버 자동배정(서버 0개면 Inform). 12-Agent opus 시뮬레이션(5블로커/1High) 반영.
  - 매핑 비대칭 정합: 쓰기=`server_id`(int?, ShouldSerialize 차단으로 nested 미전송) ↔ 읽기=nested `server`. DeviceEquals server_id 비교 추가, UpdateDeviceProperties Server 복사(유령서버 차단), ServerProvider 신설(startup 적재+새로고침).
- **장비 속성패널 레이아웃 재설계 — Phase 1 (레이아웃+스크롤)** ([PRD](docs/prds/DevicePropertyPanel_Layout_Redesign-prd.md) v1.1 · [Plan](docs/plans/DevicePropertyPanel_Layout_Redesign-prd-plan.md))
  - 6 SelectionView(제어기/센서/카메라/스피커/함체/경광등)를 **4구역(장비공통·장비별·위치·그룹)** 으로 통일 + 헤더·**적용버튼 고정**, **속성영역만 세로 스크롤**(GroupBox 내부 2행 Grid: ScrollViewer/footer).
  - 신규 `Utils/Behaviors/BubbleMouseWheelBehavior` — 내부 ListBox(Groups) 스크롤 한계 시에만 부모로 휠 재전파(Groups 기존 세로스크롤 보존 + 중첩 휠 갇힘 해소).
  - 6차원 Agent 시뮬레이션(머지블로커 4 식별) 반영한 PRD v1.1. 바인딩/PasswordBox/ItemsSource/BindingProxy 1:1 보존(code-review opus: Critical/High 0).
- **장비 속성패널 — Phase 2 (Bearing/Altitude 왕복)**
  - 6패널 위치구역에 **Bearing(방위각 0~360°)·Alt(고도)** 편집 추가. `BaseDeviceViewModel.Bearing`(→Heading, set mod360 정규화)/`Altitude`, 6 SelectionViewModel(`CommonOrNullNullable` 공통값 + RefreshAll + ApplyButton HasValue 가드), 6 DeviceEquals(Heading/Altitude 비교).
  - **매핑 핫픽스(BLOCKER-1)**: `DtoToModelHelper.MapGeolocationToDto`가 Heading·Altitude를 실제 API 전송(이전 누락 → 방위각 저장 무효). `GeolocationDto.Altitude`→`double?`. `BaseDeviceModel` Altitude + 복사생성자 Heading/Altitude 복사.
  - **심볼 FOV 갱신**: `DeviceProviderService.UpdateDeviceProperties`에 Heading/Altitude 복사 + `SymbolEventManager.RegisterDeviceSymbol`에 `SetUpdate()` → Bearing 저장 시 지도 심볼 부채꼴 재렌더링.
  - **버그수정**: `CameraSelectionViewModel` ctor `RefreshAll()` 누락 복구. xUnit 7케이스(왕복/null보존/가드/직렬화) — 전체 81 통과.
- **Client/Server API v4.6 정합 — Phase 0 (S1~S5)** ([PRD](docs/prds/Client_API_v46_Conformance-prd.md) v3.1 · [Plan](docs/plans/Client_API_v46_Conformance-prd-plan.md))
  - **FR-0**: `IApiService.DeleteRequestAsync<T>(endpoint, body)` body-DELETE 오버로드 신설(벌크해제 공통 인프라, `PatchRequestAsync` 패턴)
  - **FR-1**: ActionEvent 1:N — `GetDetection/MalfunctionActionsAsync`가 `/{id}/actions`(복수) + `ApiListResponse<ActionEventDto>` 배열 반환(기존 단수 `/action`+단건 → 404/역직렬화 위험 제거)
  - **FR-5**: `DeviceGroupBulkRemoveResultDto` + `IDeviceApiService.RemoveDevicesFromGroupAsync`(body-DELETE 벌크 제거) — VM 1콜 교체는 후속(WIP 정리 후)
  - **FR-6**: `EventMappingCameraDto`/`SpeakerDto`에 `is_enable` 추가(Speaker PUT 422 해소)
  - **FR-7**: `GeolocationDto.heading`(0~360 FOV 방위) + `CameraPresetDto.is_restricted_zone`(감시금지구역) 추가 (restricted_actions는 v4.6 폐기로 미추가)
  - 검증: 5개 프로젝트 빌드 0에러, Messages 단위테스트 174/174 통과, code-review 무차단. PARK(후속): FR-1(b) RowDetails·FR-2·FR-3·FR-8·FR-10~17(NATS/외부의존) — Plan 참조
- **디지털 줌 (Digital Zoom)** ([PRD](docs/prds/DigitalZoom_RenderTransform-prd.md) · [Plan](docs/plans/DigitalZoom_RenderTransform-prd-plan.md))
  - MaxZoom 초과 시 `GMapCustomControl.RenderTransform = ScaleTransform`으로 타일+마커+오버레이 균일 소프트 확대(1.5x/2.0x). ScaleMode/Zoom/_core 불변, 히트테스트 무보정(WPF 자동 역변환)
  - 휠/슬라이더/버튼 라우팅(`DigitalZoomLevel`/`SliderValue` DP), 축척바 거리 숫자 ÷배율, 맵 전환 시 리셋
  - UX: 슬라이더 라벨 "18+"/"18++"(오렌지 구분, 고정 너비), AdornerDecorator ClipToBounds로 윈도우 컨트롤 침범 차단
- **OverlayImage 회전 편집** ([PRD](docs/prds/OverlayImage_Rotation_Editing-prd.md) · [Plan](docs/plans/OverlayImage_Rotation_Editing-prd-plan.md))
  - `GMapCustomImage` 회전 핸들(초록 원) 드래그 편집 + 중심 이동 핸들. `UserRotation`/`MapCorrectionRotation`/`EffectiveRotation` 분리(맵 회전이 사용자 편집값 파괴 방지)
  - 좌표계 단일화: 렌더·히트·드래그 델타가 동일 `RotateTransform(EffectiveRotation)` 공유(`InverseRotateMouse`)
  - 마커 회전 속성 UI: TextBox → Slider + 직접입력 TextBox + ° (MarkerBearing 동기화)
- **줌 동작 개선** ([PRD](docs/prds/GMap_Zoom_Improvements-prd.md))
  - 슬라이더 눈금/Max 동기화(ZoomMax/ZoomMin INPC 래퍼 + 정수 스냅), MaxZoom 경계 휠 점프 차단
  - 편집모드 오브젝트 위 휠줌/드래그팬 통과(`IgnoreMarkerOnMouseWheel`, MarkerEditAdorner `HitTestCore` 핸들 한정)

### Fixed
- **디지털 줌 활성 시 카메라 RTSP 팝업/연결선 좌표 어긋남 수정** ([PRD](docs/prds/CameraPopup_DigitalZoom_Alignment-prd.md) v1.0 · [Plan](docs/plans/CameraPopup_DigitalZoom_Alignment-prd-plan.md))
  - 증상: 디지털 줌(1.5/2.0x) 시 팝업·빨간 연결선이 카메라 심볼에서 떨어짐(화면 중심에서 멀수록·줌 클수록 선형↑). 디지털 줌만 인/아웃 시 팝업 미추종(제자리 고정).
  - 근본원인: 디지털 줌 = `GMapCustomControl.RenderTransform=ScaleTransform(s,s,W/2,H/2)`. 마커는 transform '안'(WPF가 `RenderTransform.Inverse` 자동 적용), 팝업은 형제 `PropertyPanelCanvas`(transform '밖')에서 `FromLatLngToLocal` raw inner 좌표를 그대로 사용 → **좌표 도메인 비대칭(RC-1)**. 디지털 줌은 `_core.Zoom` 불변이라 `OnMapZoomChanged` 미발화 → `RefreshCameraPopupPositions` 미호출(RC-2).
  - 수정: `GMapCustomControl`에 **팝업 전용** `InnerToOuter`/`OuterToInner`(중심 W/2,H/2 기준 ScaleTransform 정/역, `ActualWidth<=0`·`scale=1` 항등 가드) 신설 → 팝업 최초/추종 위치·연결선 끝점(`OpenCameraStreamPopupAsync`/`RefreshCameraPopupPositions`)을 outer 보정, 드래그 저장은 `OuterToInner`로 역보정 후 `FromLocalToLatLng`. `OnMapDigitalZoomLevelChanged`·`MainMap_SizeChanged`에 `RefreshCameraPopupPositions` 연결(RC-2). **scale=1이면 항등 → 디지털 줌 미사용 회귀 0**, 마커/격자/스냅 무손상(이중보정 회피, 불변식 준수).
  - 검증: 회전 독립 합성 확인(V-01: `ApplyMapRotation`은 `Bearing`만 변경), GMaps.Ui 빌드 0에러, 격리 단위테스트 61통과(DigitalZoom 7 신규: 항등/왕복/중심불변/보정정확/단조성/배율테이블), code-review(opus) **MERGE**(Critical/High/Medium 0). ※메인솔루션 재빌드 후 런타임 검증 대기.
- **장비 패널 CRUD Temp-state 통일 (Phase 1, PR-A/B/C)** ([PRD](docs/prds/DevicePanel_TempState_Unification-prd.md) v1.1 · [Plan](docs/plans/DevicePanel_TempState_Unification-prd-plan.md))
  - 설계 전환(사용자 결정): 직전 DeviceGroup B모델(추가 즉시 Create)을 **Temp-state로 통일 환원** — 서버가 미완성 placeholder 거부(422)·Sensor는 controller_id FK 필요로 즉시등록 불가 → 7패널(Controller/Camera/Sensor/Speaker/Enclosure/Lamp + DeviceGroup) 일관. 5-Agent PRD 검토 + opus 코드리뷰(머지차단 2 + High 3) 반영.
  - **Temp-state 베이스 템플릿**(`BaseDataGridMultiPanelViewModel`): `ExecuteCreateAsync`(필수필드 사전검증→보류)/`ExecuteSaveUpdatesAsync`/`NotifySaveResultAsync`(sanitize 통지)/`SanitizeDetails`(민감정보 마스킹)/`ShouldProjectToProvider`. 추가=로컬 Draft(Id≤0) Add(서버 미호출), Save=일괄 Create+Update+재조회+생존자(실패/보류) 복원.
  - **Draft 격리**(근본해결): CollectionChanged.Add Id≤0 미투영 → 공유/타입드 provider 오염·이중행 동시 차단. Insert는 `_processGate`로 직렬화(Save 진행 중 경합 방지).
  - **Id≤0 게이팅**(Critical 버그 차단 — 미저장 그룹 `POST groups/0/devices`·낙관적 desync): 그룹 장비추가 버튼 비활성(`CanAddAssign`)+메서드 가드, 배정 다이얼로그 groupId/후보/newId 가드, verify-after-success(응답 `AssignedDeviceIds` 기준), 센서 제어기 드롭다운·PIDS 픽커 Temp(Id≤0) 제외.
  - **UX/보안**: 미저장 행 시각마커(`IsDraft` 배경/툴팁) + 화면전환 시 미저장 손실 비차단 알림. `Uninitialize` 빈 catch 로깅화, Controller 전체 DTO 로깅 제거, 생존자 행 Index 재부여.
  - 검증: Devices.Ui/GMaps.Ui/Events.Ui 빌드 0에러, 불변식 grep 7/7. 후속(Phase 2/3): Speaker `server_id` FK, Enclosure ThresholdConfig UI, Camera RTSP.
- **제어기 추가 HTTP 422 수정 + API 422 응답 본문 보존/진단 로깅** ([Report](docs/reports/Controller_422_Fix-report.md)) — 머지 `881ac5a`
  - 근본원인(4각도 Workflow 조사): `ControllerDeviceModel` ctor가 Camera/Speaker/Enclosure/Lamp와 달리 `DeviceType` 미설정 → `ToControllerDeviceDto`가 `type_device="NONE"` 전송 → 서버 enum 검증 422. **제어기에서만** 발생한 이유.
  - **수정**: `ControllerDeviceModel` ctor에 `DeviceType = EnumDeviceType.Controller`(peer 4모델과 동일 패턴 누락 보완).
  - **진단 인프라**: `ApiMessageHelper`의 3개 비성공 분기가 FastAPI `{"detail":[...]}`를 `MissingMemberHandling.Ignore`로 빈 객체 역직렬화→**본문 폐기**하던 결함 수정 → 표준 envelope면 그대로, 아니면 본문 전문을 `Error.Details`에 보존(FR-8 보완) + `ApiResponse.StatusCode` 추가. Controller Insert에 요청 DTO/실패 422본문 로깅.
  - 검증: Devices.Ui·Events.Ui 빌드 0에러, Messages 182/182(신규 `ApiMessageHelperErrorTests` 3건 포함) 통과, code-review(opus) 머지차단 0.
  - **차기 422(머지 `a916d59`)**: 로그가 다음 거부 필드 표출 — `{"field":"ip_port","message":"Input should be greater than or equal to 1"}`. 즉시-POST가 `ip_port=0` 전송. → Controller/Camera/Lamp Insert에 유효 placeholder 포트 **502**(통합테스트 검증값) 부여(`ip_address=""`는 서버 허용). Camera/Lamp/Speaker/Enclosure Insert에 실패 `Error.Details` 로깅 추가(Camera/Lamp 비번 보유→요청본문 미로깅). 후속: Sensor ctor DeviceType 미설정(다중 서브타입→별도 설계).
- **DataGridPanel CRUD 라이프사이클 베이스 통일 — Phase 1 (베이스 + Event 4패널)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Plan](docs/plans/DataGridPanel_Delete_Centralization-prd-plan.md))
  - 2개 Workflow 전수 감사(삭제+저장)로 확정: Event 4패널이 **삭제 시 Id≤0(Draft) 무가드 → Id=0 DELETE(404/유령행)**, **Save가 Create 후 Id write-back 없이 Insert 루프 → 재Save 유령중복**. code-review(opus) 반영(H1/H2).
  - **베이스 `ExecuteDeleteAsync` 공통 헬퍼**(`BaseDataGridMultiPanelViewModel<T>`): Id≤0 로컬만 제거(API 미호출) / Id>0 DELETE + 성공 시에만 제거(verify-after-success) / 취소(OCE) 재던지기 / 부분실패 누적 후 **InfoPopup 통지(중앙화)**. abstract·virtual 계약 변경 없음 → device 6패널 무영향(증분 안전).
  - **Event 4패널**(Detection/Malfunction/Connection/Action): 무가드 삭제 루프 → `ExecuteDeleteAsync`. Save Insert 루프에서 `created.Id`를 model에 **write-back**(재Save 재Insert·유령중복 차단).
  - 검증: ViewModel/Events.Ui/Devices.Ui 빌드 0에러, Events.Ui 테스트 300통과/11실패=baseline(3069ba1) 동일(**회귀 0**).
- **DataGridPanel CRUD 통일 — Phase 2 (Device 6패널 B모델)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Report](docs/reports/DataGridPanel_CRUD_Phase2_3-report.md)) — 머지 `a84dab6`
  - architect(opus) 정밀 맵: 6패널 구조 동일(A모델). `CreateXxxAsync` 래퍼가 `Task<bool>`로 서버 `ApiResponse<Dto>.Data`(Id) 폐기 = 유령중복(#13) 직접원인.
  - **Insert(5패널 Camera/Speaker/Enclosure/Lamp/Controller)**: Draft(Id=0) → `_processGate` 안 즉시 `CreateXxxAsync` → **FetchAllDevicesAsync+DataInitialize로만 반영**(코드리뷰 C1: 타입드 provider=공유 단방향 투영이라 수동 Add 시 FetchAll 투영과 이중행 → 수동 Add 제거).
  - **Save(6패널)**: insertList(Id≤0 Create) 루프 제거 → `FetchXxxAsync` complete 가드 + `Where(Id>0)` Update 전용(유령중복 차단). 5패널 Fetch 전페이지+complete 튜플화.
  - **Delete(6패널)**: 베이스 `ExecuteDeleteAsync`(Id≤0 로컬/Id>0 verify-after-success/부분실패 통지) + `_processGate` 직렬화.
  - **Sensor 특수**: controller_id FK라 즉시 Create 불가 → Insert는 Draft 유지, Save에서 `Controller.Id>0` Draft만 커밋 후 로컬 Draft 제거(코드리뷰 H1: 이중행 방지)+FetchAll 서버본 반영, 미선택은 보류+안내(Draft 보존).
  - code-review(opus) 2라운드: C1(이중행)/H1(Sensor 커밋 이중행)/M1 수정 후 머지차단 0 검증. 빌드 0에러. ⚠ 런타임 검증 필요(즉시 POST 동작).
- **DataGridPanel CRUD 통일 — Phase 3 (조치보고 멱등)** ([PRD](docs/prds/DataGridPanel_Delete_Centralization-prd.md) v2.0 · [Report](docs/reports/DataGridPanel_CRUD_Phase2_3-report.md)) — 머지 `bc53164`
  - 수동 조치보고(ReportDialog→EventCard.SendAction)가 자동/자동복구/배치의 in-flight 가드(`_inFlightEventIds`)에 미참여 → 수동×자동 동일 EventId 중복 ActionEvent 위험.
  - **싱글톤 `IActionReportGuard` 추출**: EventCardListPanel 3경로 + 수동 2경로(Detection/Malfunction SendAction)가 동일 인스턴스 공유. 진행 중이면 수동 스킵(중복 차단). 가드 의미론 `eventId>0` 기준 통일(AutoRecovery Id≤0 키 점유 제거 — 의도).
  - code-review(opus): Critical/High 0, 3경로 동치 확인. 빌드 0에러, 테스트 300통과/11실패=baseline(회귀 0).
- **장비 패널 CRUD↔API 연동 정합 — DeviceGroup B모델 + Controller 페이지네이션 (Phase 1)** ([PRD](docs/prds/DevicePanel_CRUD_API_Sync-prd.md) v1.1)
  - 16-Agent 분석 + 2-Round 시뮬로 식별: 그룹 추가/삭제/수정이 API와 desync(추가후 사라짐·삭제후 잔존·수정 시 무관 그룹 중복생성). 원인=클라이언트 pending(Id=0)+Save 일괄 diff 모델(서버/API는 정상). 10-Agent 코드리뷰 반영.
  - **DeviceGroup B모델 전환**: 추가=즉시 `CreateDeviceGroupAsync`(반환 서버 Id 반영, pending 미발생) / 삭제=API 성공 시에만 provider 제거(verify-after-success) / Save=Update 전용(Insert·Delete diff 루프 제거 → 유령 중복생성 차단) / Reload=서버 재조회 swap-on-success(유령 청소)
  - **Controller**: Save 비교 fetch `limit:20` 단건 → 전 페이지 루프(20건 초과 편집 소실 해소)
  - **견고화**: 그룹/제어기 fetch 페이지네이션 + 완전성 신호(>100 누락·중간페이지 실패 시 Save 보류), OnActivate/삭제를 `_processGate`로 직렬화(초기로딩 경합·공유 CTS 조기취소 방지), 실패 시 InformDialog 통지
  - 멤버십(`AssignDevicesToGroupAsync`)은 정상이라 미변경. 후속(Phase 2): 나머지 6개 장비 패널 일관 적용.
- **탐지/장애 이벤트 처리 오염 수정 — Phase 1** ([PRD](Docs/prds/EventProcess_ContaminationFix-prd.md) v1.1 · [Plan](docs/plans/EventProcess_ContaminationFix-prd-plan.md))
  - 구현 전 13-Agent 재대조 검증(§11): EB7 false positive 제외, 선행 DeviceApi C1(NATS DELETED) 미구현 확인 → EB3 메서드만(트리거 dormant). 10-Agent 적대 코드리뷰 반영(머지차단 결함 0).
  - **EA2(CRITICAL)**: `DetectionEventPanelViewModel.DataInitialize` 가 fetch 전 기존 이벤트를 Remove/Clear + 실패 시 빈 결과(예외 미전파)로 화면 전체 공백·복구불가되던 것을 **swap-on-success**로 수정(`PagedResult.Success` 플래그로 API실패 vs 빈결과 구분, 실패 시 기존 보존 + 팝업)
  - **EC2/EC5**: 조치보고 Auto/AutoRecovery/Batch 3경로가 동일 EventId 동시 호출로 서버 중복 조치보고/NATS 중복발행 → `_inFlightEventIds` 멱등 가드(수동 다이얼로그 경로는 후속)
  - **EA3**: 조치보고 API 실패해도 다이얼로그가 닫혀 성공 오인식 → `SendAction`→`Task<bool>`, `ClickOk` 결과 검사(실패 시 다이얼로그 유지 + 오류 팝업)
  - **EA7**: 저장 배치 한 건 실패로 전체 중단·무응답 → per-item 부분실패 수집 + 실패 시 재로드 생략(편집 보존) + 팝업
  - **EB1**: `NatsSyncService`(Detection/Malfunction) `IService` 미등록으로 종료 시 NATS 구독 미해제 → `As<IService>()` + 멱등 구독(`-=` 후 `+=`)
  - **EB2**: 표시 카드 무한 증가 → `MAX_EVENT_CARDS=500` 하드캡(오래된 카드 제거)
  - **EB3**: `EventQueueManager.RemoveByDevice` 추가(장비 삭제 시 고아 이벤트 정리, Dequeue 재사용) — 트리거는 선행 DeviceApi PRD C1 소관(현재 dormant)
  - 후속(별도): 수동 조치보고 멱등 통합, Malfunction swap-on-success, DataInitialize 구독토글 동시성, Phase 2(EC1/EC7/EB6/EA1)·Phase 3
- **격자 스냅 정확도 수정 — 픽셀 도메인 + 라인/교점 차등 가중치** ([PRD](docs/prds/GridSnap_System-prd.md) v1.2 · [Plan](docs/plans/GridSnap_System-prd-plan.md))
  - **RC-1**: 시각 격자(화면 픽셀 원점)와 스냅 수학(지리 0° 원점) 불일치로 보이는 선/교점에서 최대 `gridPx-1 px` 어긋나던 "중점 이탈 스냅" 버그 수정 → `SnapGridOverlayService.ComputeOrigin`/`Snap` 단일 원점으로 통일(시각 격자 = 스냅 격자)
  - **RC-2**: `MarkerEditAdorner._grabOffset`(그랩지점−중심) 도입 → 클릭 지점이 아닌 마커 **중심**이 격자에 스냅(12px 점프 제거)
  - **RC-3**: 교점 가중치(`rCross=gridPx×0.25`) > 라인 가중치(`rLine=max(gridPx×0.15,3px)`) — 교점이 라인보다 넓게/강하게 흡착(데드존 없는 합집합)
  - **RC-4**: `GMapCustomImage` Move 시 `SnapBoundsCenter`로 AABB 중심 픽셀 스냅(기존 미구현)
  - 맵 회전(`MapRotation`≠0) 시 스냅 비활성 가드. DigitalZoom 역보정 불필요(`TransformToAncestor`가 컨트롤 RenderTransform 제외 → 이중보정 방지)
  - **RC-5(v1.3)**: 스냅 대상을 **Adorner 중앙 이동핸들**(`RenderSize/2`)로 교정. 이동핸들은 라벨 포함 bbox 중앙이고 Position 앵커는 아이콘 중앙(`Offset=-_model/2`)이라 라벨 시 둘이 달라 핸들이 격자에서 이탈하던 것을, 핸들 화면위치를 스냅한 뒤 `Position += (handleTarget−handleNow)` 델타 보정으로 핸들이 정확히 격자선/교점에 안착하도록 수정
  - **RC-6(v1.4)**: 우측 영역 **세로줄 누락** 버그 수정. `MAX_GRID_LINES=100`이 폭 > `100×gridPx`(예 gridPx=16→1600px)에서 세로선을 우측에서 잘라내던 것을, 축별 동적 가드(`min(ceil(dim/gridPx)+2, 2000)`)로 교체해 화면 전체 커버
  - **RC-7(v1.4)**: 격자를 **맵에 고정**(geo-anchored). 화면 고정 원점(`gridPx-(w%gridPx)`)을 고정 지오 앵커 `FromLatLngToLocal` 화면좌표 위상정렬(`ComputeOrigin(ctrl,gridPx)`)로 교체 → 패닝 시 격자가 맵과 1:1 이동, 줌은 픽셀크기 유지. DrawGrid·마커스냅·이미지스냅 3경로 동일 원점(시각=스냅 단일원천 유지)
- **마커/이미지 히트테스트 AABB 통일** ([PRD](docs/prds/MarkerHitTest_AABB_Fix-prd.md))
  - `GetMarkerAtScreen` 원형 반경(`Math.Max(W,H)/2+8`) → Width×Height AABB + `-Bearing` 역회전 보정 + screenWidth 캐시
  - `GetImageAtScreen` Opacity≤0 클릭 차단 + 회전 보정
- **GMapImageMarker 이중 회전 제거** (줌→회전 누적 버그) — `GMapMarkerImageControl.OnRender`의 `PushTransform(Bearing)`이 base RenderTransform과 중복되어 2배 과회전 + 줌 위치 드리프트 발생하던 것 제거

- **OverlayImage ZOrder 독립 영속화** ([PRD](docs/prds/OverlayImage_ZOrder_Independence-prd.md) · [Plan](docs/plans/OverlayImage_ZOrder_Independence-plan.md))
  - `IImageModel` / `ImageModel`: `ZOrder` 런타임 프로퍼티 추가 (DB 컬럼 없음, `MapLayers.ZOrder` SSOT)
  - `GMapImageMarker`: 생성자 `ZIndex=5` 하드코딩 제거, `IEditableMarker.ZOrder` setter `_imageModel` 동기화
  - `RestoreLayerVisibility`: `Panel.SetZIndex` 동기화 (`IEditableMarker.ZOrder` setter 경유)
  - `GMapDbSymbolService.BuildSchemeAsync`: 심볼 ZOrder band 시프트 마이그레이션 (`UPDATE Symbols SET ZOrder = ZOrder + 1000 WHERE ZOrder < 1000`)
  - `MapViewModel.SaveMarkerZOrderAsync`: 이미지→`UpdateMapLayerAsync`, 심볼→`UpdateSymbolZOrderAsync` 타입 분기
  - `EnsureUniqueZOrder` / `NormalizeAllZOrder`: band-aware 분리 (이미지 0~999 / 심볼 1000+)
  - `MoveMarkerUp/Down/ToTop/ToBottom`: 동일 band 내에서만 스왑/이동
  - `RefreshPropertyPanelZOrder`: 선택 마커 band 기준 순위 표시

### Refactored
- **ZIndex → ZOrder 네이밍 전사 통일** ([PRD](docs/prds/ZOrder_Naming_Unification-prd.md))
  - `ISymbolModel`, `SymbolModel`: `ZIndex` → `ZOrder` 프로퍼티 변경
  - `IEditableMarker`, `GMapBaseMarker`: 인터페이스 멤버 및 명시적 구현 `ZOrder`로 변경
  - `GMapImageMarker`: `IEditableMarker.ZOrder` 명시적 구현 추가 (GMap.NET `ZIndex`에 위임)
  - `IGMapDbSymbolService` / `GMapDbSymbolService`: `UpdateSymbolZOrderAsync`, `BatchUpdateZOrderAsync` 메서드명 변경 + DB 마이그레이션(`ZOrder` 컬럼 추가, `ZIndex` 이관)
  - `MapViewModel`: 내부 메서드명(`EnsureUniqueZOrder`, `NormalizeAllZOrder` 등) 전체 변경
  - `GMapPropertyBaseControl` / `BasePropertyStyle.xaml`: `MarkerZOrderDisplay` DP 및 바인딩 경로 변경



### Fixed
- **RDP/원격 환경 패닝 시 심볼 위치 점프 버그 수정** ([PRD](docs/prds/RemoteDesktop_PanFollowBug_Fix-prd.md) · [Plan](docs/plans/RemoteDesktop_PanFollowBug_Fix-prd-plan.md))
  - `GMapControl.cs`: `PositionChanged()` — 드래그 중 `ForceUpdateOverlays()` 무조건 호출 제거 (`!_core.IsDragging` 가드 추가). RDP 마우스 이벤트 압축 환경에서 심볼/타일 불일치 근본 원인 해소
  - `MapViewModel.cs`: `MainMap_OnCurrentPositionChanged()` — `MainMap.Position = point` 불필요 재설정 제거. `PositionChangedCallBack` 재진입으로 인한 `ForceUpdateOverlays()` + `RefreshVisibleTiles()` 이중 호출 차단
  - `GMapCustomControl.cs` / `MGRSGridOverlayService.cs`: `FormattedText` 생성 시 `pixelsPerDip=96` 하드코딩 제거 → `PixelsPerDip` 프로퍼티 기반 실시간 DPI 조회. 125%/150% 스케일 환경에서 라벨 크기 정상화
  - `GMapCustomControl.cs`: `OnInitialized`에 `RenderCapability.Tier` 진단 로그 추가 — RDP/소프트웨어 렌더링 환경 자동 감지

### Changed
- **OverlayMap MBTiles Provider 전환** ([PRD](docs/prds/OverlayMap_MBTiles_Provider-prd.md) · [Plan](docs/plans/OverlayMap_MBTiles_Provider-prd-plan.md))
  - `MBTilesOverlayMapProvider` 신규 — 인스턴스별 독립 SQLiteConnection, TMS 좌표 변환, 파일 검증
  - `TileGenerationService`: PNG 폴더 → MBTiles(SQLite) 단일 파일 쓰기 전환, 트랜잭션 배치(1000개)
  - `LruTileCache`: `BitmapImage` → `ImageSource`, thread-safe lock 추가, LoadTileImageSource에 실제 결선
  - `CustomMapService` / `CustomMapOverlayService`: StorageType 분기(MBTiles/PngDirectory), LRU 캐시 활성화
  - `GMapDbService`: `MbtilesPath`, `StorageType` 컬럼 idempotent ALTER 마이그레이션 추가
  - MBTiles 저장 경로: `C:\ProgramData\Sensorway\PIDS\maps\` → `{exe}\maps\` (상대 경로 통일)
  - `TileDirectory` 설정 항목 완전 제거 (`IGMapSetupModel`, `GMapSetupModel`, `MapViewModel`, 소비측 포함)

### Fixed
- **버스트 이벤트 시 지도 패닝 스턱 제거** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) · [Plan](docs/plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md))
  - `DetectionNatsSyncService.cs` / `MalfunctionNatsSyncService.cs`: `PublishOnUIThreadAsync(Normal=9)` → `Dispatcher.InvokeAsync(Background=4) + PublishOnCurrentThreadAsync` — 탐지/장애 이벤트마다 Normal 콜백이 Input 기아 유발하는 근본 원인 제거. NFR-01 검증 완료
  - `DeviceSymbolLookupModel.cs:60` `MarshalUpdate()` `DispatcherPriority.Normal(9)` → `Background(4)` — `_isFlushPending` 코얼레싱으로 35이벤트 버스트에서도 최대 1콜백만 큐잉됨
  - `GMapMarkerPidsControl.cs:447` `UpdateFOVPath` `BeginInvoke` Background 우선순위 명시 — Input(5) 마우스 기아 제거
  - `EventUiModule.cs:117-118` `OnDeviceFirstEvent += SetDeviceDetecting` / `OnDeviceEmpty += RestoreDeviceSymbol` 이중 경로 제거
  - `DeviceSymbolLookupModel.cs:109-113` `ProcessEvent(Fault)` `FaultedDetecting` 보존 가드 추가
  - `RedisBrokerService.cs` `ParseMessageItems` 이식 — JObject 형식 Redis 메시지 무음 소실 수정
- **PulseRing Canvas 중앙 정렬 수정** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md))
  - `PidsMarkerStyle.xaml` Canvas Zero-size anchor(`Width=0/Height=0 + HA/VA=Center`) 패턴 적용
  - Canvas.Left/Top `-25` → `-40` (80px 링 정확한 아이콘 중심 정렬, 마커 크기 무관)
  - StrokeThickness `2` → `6` (가시성 개선)
- **Multisensor(Multi/SmartMultisensor2) 심볼 미동작 + 레이어 Hide 불량** ([PRD](docs/prds/Multisensor_Symbol_Fix-prd.md))
  - `MapViewModel.MatchMarkerToCategory("PidsSensor")`: `Multi`, `SmartMultisensor2` 추가 — 레이어 숨김/표시·카운트·줌 재적용 3곳 동시 수정 (BUG-1)
  - `DetectionNatsSyncService` / `MalfunctionNatsSyncService`: DeviceType 파싱 실패 시 `Fence` 오분류 fallback 제거 → Error 로그 + 이벤트 드롭 (BUG-2)
  - `NatsBrokerService.GetDevice(BrkDectection/BrkMalfunction)`: `SmartMultisensor2` switch case 추가 — 이벤트 무음 손실 방지 (BUG-3)
  - `DeviceFilterHelper`: `SmartMultisensor2` 독립 필터 case 추가
  - `GMapMarkerPidsControl.GetSizeForDeviceType`: `Multi`, `SmartMultisensor2` 32px 명시
  - `SymbolEventManager`: FenceGroup 전용 센서 `미등록 Device` WARN 제거 (로그 노이즈 감소)
  - `MapViewModel.InitializeDeviceSymbolIntegration`: 시작 시 심볼 등록 집계 Info 로그 추가
- **전체 조치보고 후 심볼 Detecting 고착 수정** ([PRD](docs/prds/BatchReport_SymbolRestore_Fix-prd.md))
  - `EventCardListPanelViewModel.ExecuteBatchReportAsync`: EntryId null 시 `_pendingEntries` 폴백 → `FindEntryByDevice` 2차 안전망 — EQM.Dequeue 보장
  - `HandleAsync(Detection/MalfunctionReportedMessageModel)`: 동일 폴백 패턴 적용
  - `SemaphoreSlim _batchReportGate` 추가 — 더블클릭 중복 실행 방지
- **이벤트 카드 10 events/sec 성능 개선** ([PRD](docs/prds/EventCardPerformance-prd.md) · [Plan](docs/plans/EventCardPerformance-prd-plan.md))
  - `EventCardListPanelView.xaml`: `<ListBox.ItemsPanel>` StackPanel 블록 제거 → WPF 기본 VirtualizingStackPanel 복원. `ScrollUnit=Pixel` 추가. Add당 378ms O(n) → 3ms 고정
  - `EventCardListPanelViewModel.cs`: `EnqueueCard()` 공개 메서드 추가 — NATS 스레드 Dispatcher 동기 블로킹 제거, 배치버퍼 경유
  - `EventCardBaseViewModel.cs`: `Dispose()`에 `Cts?.Cancel(); Cts?.Dispose(); Cts = null;` 추가 — Win32 WaitHandle 600개/분 누수 제거
  - `EventCardListPanelViewModel.cs`: `FlushPendingCards` `async void` → `void` 래퍼 + `async Task FlushPendingCardsAsync` 분리 — 타이머 콜백 예외 크래시 방지
  - `EventCardListPanelViewModel.cs`: `_cardByEntryId` `Dictionary` → `ConcurrentDictionary`, `.Remove()` 5곳 → `.TryRemove()` — 멀티스레드 경쟁 조건 제거
  - `DataGridSelectedItemsBehavior.cs`: `OnSelectionChanged` 전체 재생성 → 델타 HashSet 교체 — 1000개 선택 시 O(N) List 생성 제거
  - `DataGridScrollEndBehavior.cs`: `OnDataGridLoaded` unsubscribe-before-subscribe — 탭 전환 시 ScrollChanged 이중구독 방지
- **앱 종료 로그 노이즈 제거** (`a7d0762`)
  - `CustomMapOverlayService.Dispose()`: `OperationCanceledException` 분리 — Dispatcher 셧다운 취소 무시
  - `GMapDbService` / `GMapDbSymbolService` / `GatewayDbService`: `IOException` 분리 — MySQL close 패킷 소켓 실패 무시
- `TileGenerationService.GenerateTilesFromTifAsync`: `using var insertCmd` 스코프 오류 → `File.Move` IOException 수정
- `MapRegistrationStyle.xaml`: 진행률 표시 `{0:F0}` → `{0:F0}%` (% 기호 누락 수정)
- **이벤트 카드 제어기 번호 미표시** (`b69dc5b`, `72865ab`)
  - `MalfunctionEventCardView` / `DetectionEventCardView`: `ControllerId`(DB PK) → `ControllerDeviceNumber`(논리 번호) 바인딩 교체
  - `DeviceProviderService.FetchSingleSensorAsync`: `includeController:false` → `includeController:true` — SYNC_DEVICE 수신 후 Controller 링크 파괴 수정
  - `MalfunctionEventCardViewModel`: `ControllerDisplay` / `SensorDisplay` 프로퍼티 추가 — 장애 타입별 제어기/센서 표시 로직 명시화
- **Pulse 소나 애니메이션 미동작** (`fe17f0a`)
  - ScaleTransform(Freezable) `TargetName` 타겟팅 → FrameworkElement PropertyPath 방식으로 변경
  - `(UIElement.RenderTransform).(ScaleTransform.ScaleX/Y)` — WPF ControlTemplate 이름 스코프 불안정 문제 해결

### Performance
- **탐지 펄스 소나 패턴 확대** ([PRD](docs/prds/DetectionPulse_Ripple_Enlargement-prd.md) · [Plan](docs/plans/DetectionPulse_Ripple_Enlargement-prd-plan.md))
  - `PidsMarkerStyle.xaml`: `PulseEllipse`(30px) 제거 → `PulseRing1~3`(80px, `Stroke="#CCFF0000"`, `Fill="Transparent"`) 교체
  - Storyboard: 3개 링 0.4s 스태거(`BeginTime` 0s/0.4s/0.8s), 각 1.2s `CubicEase EaseOut` 팽창 → 3-링 소나 패턴

## [2.6.3] - 2026-05-27

### Fixed
- **SplashModule 아키텍처 결함 수정** ([PRD](docs/prds/PRD_SplashScreen_Startup_Gating.md)) — Option A: 기동 인프라 항상 등록 보장
  - `BootstrapCoordinator`, `ConnectionWatchdog`, 기동 Job 3개를 `SplashModule`에서 `ParentBootstrapper.RegisterBaseType()`으로 이전
  - `SplashModule` 삭제 — splash UI 유무와 무관하게 기동 인프라 항상 등록됨
  - `ISplashViewModelBase` 미등록 시에도 NATS 연결 게이팅(`IBootstrapCoordinator`) 정상 동작
  - 소비 프로젝트: `builder.RegisterModule(new SplashModule())` 제거 필요

---

### Added
- **이벤트 파이프라인 전체 성능 최적화** ([PRD](docs/prds/Event_Performance_Optimization-prd.md) · [Plan](docs/plans/Event_Performance_Optimization-prd-plan.md)) — IMPL-01~09 완료, 18/21

### Fixed
- **장애/탐지 이벤트 카드 제어기 번호 바인딩 수정** ([PRD](docs/prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) · [Plan](docs/plans/MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md))
  - `MalfunctionEventCardView.xaml` L269 / `DetectionEventCardView.xaml` L294: `ControllerId`(DB PK) → `ControllerDeviceNumber`(논리 번호) 바인딩 교체
  - `EventCardViewModel.ControllerDeviceNumber`: DeviceNumber=0 → null 반환 방어 코드 추가 (Fallback 모델 "0" 표시 방지)
  - 테스트 2개 갱신 (`ControllerId` → `ControllerDeviceNumber` 검증으로 업데이트)
- **UI 동결(30초 드레인) 근본 수정** ([PRD](docs/prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) · [Plan](docs/plans/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md))
  - `GlobalSymbolUpdateManager` 신규 도입 — `DispatcherTimer(Background, 80ms)` dirty-set flush로 `InvokeAsync(Normal=9)` per-symbol 콜백 제거 → DataBind/Render/Input 기아 해소
  - `DeviceSymbolLookupModel.MarshalUpdate()` → `GlobalSymbolUpdateManager.MarkDirty()` 위임, `_isFlushPending` Interlocked 제거
  - `SymbolEventManager`: 그룹 전용 Fence 장비 WARN → INFO 강등 3개소 (false alarm 제거)
  - `RedisBrokerService.MessageSelector`: `JToken` 타입 체크로 `JsonReaderException` 폭탄 제거 (JSON Object → 조기 리턴)
- `ApplyCompositeStatus` — NATS 스레드→WPF STA 위반 (`MarshalUpdate` 코얼레싱 적용)
- `DetectionNatsSyncService` / `MalfunctionNatsSyncService` — EA BackgroundThread ObservableCollection 접근 Blocker (`PublishOnUIThreadAsync` 전환)
- `SoundAlarmController.OnQueueCleared` — `State`/`_lastEventTime` 미리셋으로 인한 Playing 고착
- `HandleAutoReport` / `HandleAutoRecovery` — `async void` → `async Task` + ContinueWith (미처리 예외 크래시, `AutoReportInFlight` 미리셋)
- `HandleAutoReport` API 실패 시 `NextRetryAfter` 미설정으로 인한 무한 재시도 (backoff 30s 추가)

### Performance
- **MapSymbol Pulse 애니메이션 성능 최적화** ([PRD](docs/prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) · [Plan](docs/plans/MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md))
  - `PidsMarkerStyle.xaml`: `PART_EventStatusIndicator` DropShadowEffect 제거 (GPU off-screen pass 차단 해소)
  - `PidsMarkerStyle.xaml`: `PulseEllipse` BitmapCache 제거, 크기 반전(5→30px, Scale 1→0.17), Storyboard `HandoffBehavior=SnapshotAndReplace`
  - `PidsMarkerStyle.xaml`: ErrorBlink 6개 DoubleAnimation → `PART_IconContainer` 단일 애니메이션 (Animation Clock 6N→N 감소)
  - `GMapPidsMarker.PidsModel_Update`: FOV PropChanged 조건부 발화 (값 비교 캐시 + NaN sentinel, `CompositionTarget.Rendering` 누적 방지)
  - `GMapMarkerPidsControl.OnEventStatusChanged`: `UpdateMarkerAppearance()` 역방향 setter 제거, `EventStatus` 바인딩 OneWay 전환
  - `GMapMarkerPidsControl.OnFOVParameterChanged`: 0.5° 미만 변동 임계값 가드 추가
  - `GMapMarkerPidsControl.UpdateFOVPath`: 6× `GetTemplateChild` → `OnApplyTemplate` 캐시 필드 직접 참조
  - `DeviceSymbolLookupModel`: 핫패스 Info 로그 제거 (GC 압박 해소)
- `EventCardListPanelViewModel._cardByEntryId` — O(1) 인덱스 (기존 O(N) `ViewModelProvider.FirstOrDefault` 대체)
- `SymbolEventManager._deviceLookupById` — O(1) 보조 인덱스 + `TryResolveDevice` (O(N) fallback 3곳 제거)
- `EventQueueManager.Dequeue` — `_scratchPrevGroupStates.Clear()` 재사용 (매회 `new Dictionary<>()` 할당 제거)
- `FindEntryByDevice` — LINQ `OrderBy().FirstOrDefault()` → foreach min 단일 패스

---

## [2.6.2] - 2026-05-22

### Added
- **자동조치보고 이중경로 통합** ([PRD](docs/prds/AutoActionReport_DualPath_Fix-prd.md)) — Path A 타이머 제거, Path B 단일화

### Fixed
- `EventCardBaseViewModel` per-card `System.Timers.Timer` 완전 제거 — 이중 API 호출 / 20초 무한 재시도 / `GC.Collect()` 안티패턴
- `OnAutoReport` 구독자 없음 → UI 카드 영구 좀비화 (와이어링 완성)
- `EventEntry.EventId` 필드 추가 — 서버 이벤트 ID 수신 시점 보관

---

## [2.6.1] - 2026-05-20

### Added
- **사운드 타입 즉시 전환** ([PRD](docs/prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md))

### Fixed
- 탐지↔장애 타입 전환 시 이전 사운드 미중지 (`StopAndPlayAsync` + `_switchSemaphore`)
- `SoundAlarmController` 3-Action → 1-Action `stopAndPlay` 리팩터

---

## [2.6.0] - 2026-05-19

### Added
- **Malfunction 복합 상태 및 FenceGroup 시각화 아키텍처 PRD** ([PRD](docs/prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md))

## [2.6.1] - 2026-05-19

### Added
- **배치 조치보고 이중 INSERT 및 Malfunction 심볼 복원 불가 수정 PRD** ([PRD](docs/prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md))

## [2.6.0] - 2026-05-19

### Added
- **배치 조치보고 이중 INSERT 및 Malfunction 심볼 복원 불가 수정 PRD** ([PRD](docs/prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md))

## [2.5.1] - 2026-05-15

### Added
- **GMapCustomControl 이미지 드래그/리사이즈 버그 수정 PRD** ([PRD](docs/prds/GMapCustomControl_ImageDrag_BugFix-prd.md))

## [2.5.0] - 2026-05-14

### Added
- **LayerPanel ContextMenu Enhancement PRD** ([PRD](docs/prds/LayerPanel_ContextMenu_Enhancement-prd.md) · [Plan](docs/plans/LayerPanel_ContextMenu_Enhancement-prd-plan.md))

## [2.4.0] - 2026-05-13

### Added
- **PRD_ImageOverlay_FileCopy_On_Register.md**

