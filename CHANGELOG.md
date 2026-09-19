# Changelog

<!-- changelog-entries-start -->

## [3.11.0] - 2026-09-19

### Added
- **device-console-v8 — 완료 리포트 (2026-09-19)** ([PRD](docs/prds/device-console-v8-prd.md) · [Plan](docs/plans/device-console-v8-prd-plan.md) · [Report](docs/reports/device-console-v8-report.md))



## [3.10.0] - 2026-09-18

### Added
- **PidsGroup 우클릭 — 등록 센서 정보 오버레이 + 그룹 탐지 이력 PRD** ([PRD](docs/prds/pidsgroup-rightclick-prd.md) · [Plan](docs/plans/pidsgroup-rightclick-prd-plan.md))



## [2.7.1] - 2026-06-04

### Added
- **SymbolUpdate_DispatcherFreeze_Fix PRD** ([PRD](docs/prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) · [Plan](docs/plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md))

## [2.7.0] - 2026-06-04

### Added
- **Multisensor_Symbol_Fix PRD** ([PRD](docs/prds/Multisensor_Symbol_Fix-prd.md) · [Plan](docs/plans/Multisensor_Symbol_Fix-prd-plan.md))

## [Unreleased]

### Added
- **부품 조립기 · 프리셋 · 프리셋으로 등록 — 레고처럼 끌어 조립한다** (봉투 [all-windows-console-redesign](docs/charters/all-windows-console-redesign-charter.md) N-03 · 브랜치 `v2.13.0` · [PRD](docs/prds/device-assembly-preset-prd.md) · [Plan](docs/plans/device-assembly-preset-prd-plan.md) · [테스트](docs/tests/device-assembly-preset-test-result.md))
  - 배경: 부품(`hardware_spec.components[]`)을 쓰는 제품 코드가 0건이었다 — 7.0+ 서버에서 부품 없이 만든 장비는 문 · 히터 · 접점 상태를 영영 못 받는다. 사용자 지시: "장비를 조립해서 프리셋을 만들고 그걸로 등록" · "각 부품을 레고처럼 드래그 드롭".
  - **조립기**(`Consoles/Assembly`): 팔레트(카탈로그를 카테고리의 `applies_to` 로 거른다) → 조립 보드 → 속성. 팔레트 블록을 끌어 놓은 자리에 끼우고, 핸들로 순서를 바꾸고, "빼는 곳"에 끌어 빼고, 되돌린다. 키보드: 팔레트 Enter = 끝에 달기 · 보드 Alt+↑↓ · Delete. 블록은 **색이 아니라 형태로** 가족을 가른다(감지 = 홈 · 구동 = 잘린 모서리 · 전원/환경 = 육각 · 네트워크 = 마름모 · 광학 = 원 표지). 조립은 끝까지 Draft — 끌어 놓기 중 서버 호출 0.
  - 보드 규칙(순수 모델 `Model/AssemblyBoard`): key 자동 제안(`door` → `door_2`) · 형식 · 중복 즉시 표시 · **순서만 바꾼 것은 미저장 변경으로 세지 않는다**(서버에 순서 계약이 없다) · 반복 펼치기(16채널을 16번 끌지 않는다) · 채널 다시 번호 · 되돌리기 · 차이(더함/뺌/고침).
  - 속성 칸: 장비별 사실만 고친다. **카탈로그 사실(states · commands · produces)은 회색 글자** — 입력 칸으로 만들면 보내게 되고 그건 422 다. 재정의(`component_overrides`)는 부품 칸과 갈라 놓았다 — `enabled` 는 부품에 실으면 422, 재정의에서만 정상이다.
  - **프리셋**(`Presets/DevicePresetStore`): `%LocalAppData%\Ironwall\device-assembly-presets.json` · 구조만 담는다(번호 · 이름 · 접속 · 일련번호는 안 담는다) · 임시 파일에 쓰고 교체 · 깨진 파일은 덮지 않고 옮긴다 · 더 새 판은 읽기 전용 · 모르는 카테고리의 항목은 그대로 되써 준다 · 내보내기/가져오기 · 목업의 본보기 5종.
  - **프리셋으로 등록**(`Register`): 개체 정보만 넣고 → 나갈 본문을 그대로 보고 → **POST 1건**(`AllowComponentsWrite` — 빠지면 서버가 부품 없이 만들고 성공이라 답한다 → 이름 있는 회귀 테스트). **기존 장비의 부품 바꾸기**: 보내기 직전에 다시 받아 비교 → 바뀌었으면 멈춤 → PATCH 1건(배열 통째 + 뺀 key 의 재정의 `null`).
  - 입구: 장비 콘솔 툴바 [조립기] · [프리셋으로 등록] · [프리셋…] + 상세 칸 [부품 구성 바꾸기]. **6.3 에서는 통째로 감춘다.** 창은 `IWindowManager` 로 라이브러리가 직접 연다(호스트 코드 변경 0).
  - 적대 검토 3렌즈로 잡은 것: **적용 PATCH 가 스피커 · 경광등의 설명을 `null` 로 보내 지운다**(7 카테고리 전수 점검으로 접속 · 카메라 modes · 함체 thresholds 누락까지 찾아 고침) · 등록/적용 서비스에 판본 검사가 없어 6.3 에서 빈 본문이 나갈 수 있다 · 적용에 부대 찍기 누락 · 순서를 바꿀 때마다 선택과 속성 칸이 풀린다 · 조립기 입구의 의존이 안 풀리면 장비 창 전체가 안 열린다 → 전부 수정.
  - 검증: `Devices.Ui` 778/778 · `Messages` 281/281(DTO 무변경) · 호스트 빌드 오류 0 · 미리보기 캡처 16장(`tools/device-console-preview -- --assembly`). **실제 마우스 끌어 놓기 · 호스트 앱에서 창 열기 · 살아 있는 서버 등록/적용은 미확인.**
- **장비 콘솔 — 레일 · 목록 · 상세 3단 + 명세에서 만들어지는 속성 폼 + 장비 → 그룹 끌어 놓기** (봉투 [all-windows-console-redesign](docs/charters/all-windows-console-redesign-charter.md) N-02 · 브랜치 `v2.12.0` · [PRD](docs/prds/device-console-redesign-prd.md) · [Plan](docs/plans/device-console-redesign-prd-plan.md) · [테스트](docs/tests/device-console-redesign-test-result.md))
  - 배경: 직전 사이클(device-console-v8)은 데이터 항목만 옮기고 와이어프레임의 구조를 빠뜨렸다. 이번에는 PRD 의 요구사항마다 목업 줄 번호를 달고, 콘솔 커널(N-01) 위에 창을 다시 짰다.
  - 화면(`DeviceDashboardView`): 레일 8(그룹 + 제어기 · 센서 · 카메라 · 스피커 · 함체 · 경광등 · 통문, 축 계약이면 + 부품으로 찾기) · 배지 `▲장애 · 합계` · 툴바 [추가][삭제][갱신] + 검색 + [열 n/m] · 기본 6열 + 핸들 열 · 상태 띠(건수 + 그룹 칩) · 상세 고정 적용 막대. 요약 280 · 레일 390 · 목록 260 고정 높이와 패널마다 있던 [저장] 을 없앴다.
  - **정규화된 속성 명세**(`Consoles/Properties`): `DevicePropertyCatalog` — 속성 한 개 = 한 줄(키 · 라벨 · API 경로 · 절 · 편집기 · 쓰기 가능 여부 + **못 쓰는 까닭** · 카테고리 · 생성 시 필수 · 다중 편집 · 어휘 출처 · 계약 세대). 7 카테고리 전 속성. 뷰모델과 어긋나면 리플렉션 가드 테스트가 빌드 단계에서 잡는다.
  - **만들어지는 폼**(`Consoles/Forms`): 명세 한 줄 = 칸 하나. 칸 입력은 행을 건드리지 않고 [적용] 때 **손댄 칸만** 쓴다. 검증에 하나라도 걸리면 아무 행에도 쓰지 않는다. 여러 개 = `— 여러 값 —` + 식별 칸 잠금. 그룹도 같은 폼으로 고친다.
  - **전송 경로는 새로 만들지 않았다**(`IDeviceConsoleSource`): [적용] · [등록] · [삭제] · [갱신] 은 패널 뷰모델의 기존 경로(완전성 확인 · 종류 축 차단 · 부대 찍기 · 재조회)를 그대로 부른다. [추가] 의 Draft 는 [등록] 전까지 목록에서 떼어 둔다. 저장이 끝나면 같은 Id 의 새 행을 다시 고르고, 쓴 값과 서버 값을 맞춰 본다.
  - 레일 배지는 프로바이더 변경을 구독해 즉시 갱신 — `Task.Delay(100)`×14 당겨 세기와 `Tag` 문자열 스위치(`OnActiveTab`)를 없앴다. 미적용 변경이 있으면 행 · 레일 이동을 막고 바닥 막대가 까닭을 말한다.
  - **장비 → 그룹 끌어 놓기**(`Consoles/Groups`): 핸들로 N건을 끌어 그룹 칩에 놓는다 — 그룹당 호출 1회(`AssignDevicesToGroupAsync`), 서버가 넣었다고 답한 것만 로컬 반영, 저장 전 · 이미 속한 장비는 보내지 않고 결과를 한 줄로, [되돌리기](일괄 제거 1회). 칩을 누르는 것이 키보드 폴백(같은 경로).
  - 열(`Consoles/Lists`): `DeviceColumnCatalog` 에서 생성 · "열" 메뉴 설정은 레일마다 따로 기억. 부품으로 찾기(`Consoles/ByComponent`, 읽기 전용 · 축 계약 전용).
  - 설정 축 쓰기 통로(`Messages/Dto/Devices`): 7 DTO 공통 · 플래그 게이트(`AllowDeviceConfigWrite`) · **꺼지면 바이트 동일**. N-03(조립기)의 선행.
  - 미리보기 `tools/device-console-preview`: 진짜 뷰 + 진짜 뷰모델을 가짜 데이터 위에(`--snapshot` · `--legacy` · `--dark`). 캡처를 보고 고친 것: 다크에서 선택 행이 회색 칸으로 갈라짐(코드로 추가한 열에 MDIX 가 제 셀 스타일을 물린다) · [되돌리기] 뒤 남는 "손댄 칸" 표지 · Draft 카테고리 `None`.
  - 적대 검토 3렌즈(정확성 · 화면/드래그 · 회귀/테스트 정직성)로 잡은 것: **패널 7종은 삭제 완료를 작업 스레드에서 알린다**(재조회를 `ConfigureAwait(false)` 로 기다린 뒤 `UpdateAction`) → 콘솔이 그리드를 만지다 교차 스레드 예외로 죽고 진행 팝업이 안 닫힌다 → 어댑터가 끝남을 UI 스레드로 옮긴다 · **패널이 말없이 거절한 저장**(권한 없음 · 처리 중)이 다음 아무 끝남에서 "등록했다"로 읽힘 → 시작한 저장만 건다, 삭제는 아무것도 걸지 않는다 · 쓰는 도중 거절되면 반쯤 고친 행이 남음 → 전부 아니면 전무 · 레일 전환 재진입 → 직렬화 · 검색에 가려진 행을 쥔 채 [삭제] 가 켜짐 · 세 상태 체크박스 어긋남 · 그룹에 넣은 뒤 목록이 안 바뀜. 6.3 쓰기 본문은 글자 하나까지 같음을 확인(검토 + 테스트).
  - 사문 다이얼로그 제거: `AddControllerDialogView` · `AddSensorDialogView`.
  - 검증: `Devices.Ui` 468/468 · `Messages` 281/281 · 프로젝트별 빌드 8/8 · 호스트 빌드 오류 0 · `Devices.Api` 실패 86건은 기준선과 같은 집합(서버 필요). **호스트 앱 안 표시 · 실제 마우스 끌어 놓기 · 살아 있는 서버 쓰기는 미확인.**
- **콘솔 커널 — 전 창 재구성의 공용 토대 (T1 틀 + 드래그 커널)** (봉투 [all-windows-console-redesign](docs/charters/all-windows-console-redesign-charter.md) N-01 · 브랜치 `v2.11.0` · [PRD](docs/prds/console-kernel-prd.md) · [Plan](docs/plans/console-kernel-prd-plan.md) · [분석](docs/analyses/all-windows-console-redesign-analysis.md) · [테스트](docs/tests/console-kernel-test-result.md) · 사용자 "봉투 승인" 2026-09-19 · 롤백 `before-console-kernel`) — **기존 창은 하나도 옮기지 않았다**(공용 자산만)
  - 배경: 직전 사이클(device-console-v8)이 와이어프레임의 3단 구조 · 상세 여섯 상태 · 드래그를 빠뜨렸다는 사용자 평가. 목록 드래그 · 삽입선 · 드롭존 자산이 레포에 0건이라 창마다 만들면 툴바 복사 결함을 13번 되풀이한다 → 먼저 한 번만 세운다.
  - 콘솔 틀(`Utils/Console` + `Themes/Generic.xaml`): `ConsoleShell`(레일 184/56 · 목록 · 상세 340, 300~480 · 폭별 도킹/서랍/접힘 · 경계 끌기) · `ConsoleRail`(개수/장애 배지 · 3px 선택 바) · `ConsoleToolbar`(꺼진 버튼은 늘 사유) · `ConsoleDetailHost`(여섯 상태 겉모습 + 고정 적용 막대) · `ConsoleSection`/`ConsoleField`/`NotReceivedBox` · `ConsoleColumns`("열" 메뉴 — 기본 6열 · 전체 열 · 계약 미지원 열 감춤) · `ConsolePrefs`(`appsettings.json` 밖 · 임시 파일에 쓰고 교체).
  - 드래그 커널(`Utils/Behaviors/Drag`, OLE 미사용): `DragHandle`(Thumb) · `CaptureDragBehavior`(데드존 8 · 다중 선택 · 단일 종료 경로 · 끄는 도중 나타난 드롭존도 판정) · `DropZone`/`DropZoneChrome`(가능 = 파선 / 불가 = 사선 해치 / 호버 = 굵은 윤곽) · 고스트 · 가로 삽입선 · `ReorderKeyboardBehavior`(Alt+↑↓) · `DragMath`(순수 함수).
  - 뷰모델(`ViewModel/ViewModels/Consoles`): 상세 상태기계 · `DirtyFieldTracker`(손댄 칸만) · `NavigationGuard`(미적용 이동 차단) · `DraftTrayViewModel`(N회 호출은 쌓았다가 [적용], 4분류 요약) · `ConsoleDetailPresenter`.
  - 테마: `Styles.Console.xaml`(`Console.DataGrid` 등 — 전부 키 있는 스타일, 암시적 0). 갤러리 `tools/console-gallery`(가짜 데이터 · `--snapshot` 으로 입력 없이 PNG 20장 + 드래그 재현 로그).
  - 적대 검토로 잡은 것: **`Thumb` 의 이동량은 손잡이 기준 좌표**라 경계 손잡이처럼 같이 움직이면 증분이 된다(폭이 되돌아가며 떨림) → 움직이지 않는 기준으로 직접 잰다 · 끌기 시작 때 찍은 드롭존 목록만 믿어 도중에 나타난 드롭존이 Hover 로 굳던 것 · 템플릿 재적용 때 클릭 구독이 쌓이던 것 · Alt+↓ 를 행 안 콤보에서 빼앗던 것.
  - 검증: `Utils.Tests`(신설) 100/100 · `ViewModel.Tests` 52/52 · `Theme.Tests` 18/18 · 프로젝트별 14/14 빌드 · 호스트 빌드 오류 0 · 재현 16행 기대값 일치. **실제 마우스 입력 · 호스트 앱 안 동작 · RDP 는 미검증.**
- **장비 관리 창 v8 축 전환 1차 — 판별자 7탭 · 종류축 카탈로그 콤보 · 상세 9절 · 통문 패널** (Track C · 브랜치 `v2.10.0` · [PRD](docs/prds/device-console-v8-prd.md) v1.2 Approved · [Plan](docs/plans/device-console-v8-prd-plan.md) · [시나리오 분석](docs/analyses/device-console-v8-scenario-analysis.md) v1.3 · [실측](docs/tests/device-console-v8-verification.md) · [Phase 1 게이트](docs/tests/device-console-v8-phase1-gate.md) · 사용자 승인 2026-09-19 · 롤백 `before-device-console-v8`) — **Phase 1(기반) 머지 `19c6eaae` · 화면 구현 완료 · 실기(화면) 미검증**
  - 기반(화면 무변경): 통문 Api CRUD 3종·`GateDeviceProvider`·삭제 메시지·위치 저장 · 판별자 해석기 공용화(`Messages/Helpers/DeviceTypeResolver`) · 축 묶음 모델(`IBaseDeviceModel.Axes` + `CategoryDevice`·`TypeAxisCode`·`UnitId`) + 수신 원본 읽기 매핑(`DeviceAxesMapper`) · 재조회 병합 키 `(Id, 판별자)` + 통문 분기 + 축 복사 · 카탈로그 캐시(`ICatalogService`) · 계약 게이트 + "판본 미확정" 배너 · DB `Enum.Parse`→비예외 헬퍼.
  - 화면 ① 통문 패널: 대시보드 7번째 탭·타일(`BoomGate`) + 목록/상세/추가 · Temp-state CRUD 정본 그대로.
  - 화면 ② 종류축: 7 패널 공통 "종류" 열 + 필터 콤보 — 선택지는 카탈로그(`/api/devices/spec`)에서만 온다(`TypeAxisPanelSupport`). 카탈로그에 없는 값은 저장 전에 막고, 모르는 수신값은 원문 그대로 보인다. 카메라 legacy 3열은 축 계약에서 숨김.
  - 화면 ③ 상세 9절: 판별자 배지(편집 불가) · 접속 · 형상 · 부품 · 상태 · 설정 · 응답 프로필(`DeviceAxisSectionsViewModel` + `DeviceAxisHeadView`/`DeviceAxisTailView`). **미수신 ≠ 빈 값** — 응답에 실리지 않은 절은 "이 응답에 실리지 않았습니다". 방위각 입력은 방향성 장비(카메라·스피커·센서)만.
  - 형상·부품·상태·설정 절은 **읽기 전용**(서버가 `components` 를 통째 교체). 접속 방식·상위 장비·채널은 읽기 표시만 — 편집은 이번에 열지 않았다(PRD FR-11② 축소 기록).
  - 6.3.2 운영에서는 옛 화면 무변경(`LegacyContractSnapshotTests`). 장비 할당 다이얼로그용 `CategoryLabel`·`TypeAxisLabel` 제공(메인 솔루션 `v0.6.0`).
- **GOP 서버 API 계약 동기화 (6.3.2 / 8.0.1 동시 대응)** (Track C · [PRD](docs/prds/gop-api-contract-sync-prd.md) v3.0 · [Plan](docs/plans/gop-api-contract-sync-prd-plan.md) · [분석](docs/analyses/gop-api-contract-sync-analysis.md) · [실연동 검증](docs/tests/gop-api-contract-sync-live-verification.md) · 사용자 사전 승인 2026-09-18)
  - **판본 런타임 분기**(`IServerContractProbe` + `ServerContractBootService`, `Order = -1000`) — 맞출 대상이 **셋**이다(운영 `6.3.2` · 개발 `8.0.1` · 명세 `v8.0`). 장비 쓰기는 `type_device`(6.3 **필수**) vs `additionalProperties:false`(7.0+ **금지**)로 **한 본문 양립 불가**라 페이로드 성형만으로는 못 맞춘다. `GET {root}/openapi.json` 의 `info.version` 을 1회 확보해 캐시하고, **판정 실패 시 `V6_3` 폴백**(운영이 6.3.2라 틀렸을 때 손해가 가장 작다). 비교는 **항상 `>=`** — 세션 중 로컬이 `7.0.1 → 8.0.1` 로 올라간 것이 `==` 분기의 위험을 실증했다.
  - **장비 축(axis) 전환** — `type_controller`/`type_sensor`/`type_camera`/`type_speaker`/`type_enclosure` + `connection`/`hardware_spec`/`device_config`. DTO 를 판본마다 복제하지 않고 `UseAxisWrite` 플래그 + `ShouldSerializeXxx()` 조건 직렬화로 **한 DTO 가 두 계약을 표현**한다. `version` → `hardware_spec.firmware` 역투영도 쓰기 방향으로 이었다(종전엔 드롭만 돼 펌웨어 수정이 서버에 전달되지 않았다).
  - **목록 계약 전환** — `include_sensors`/`include_controller` → `?include=` · `?view=full`(14곳). ⚠ **제거된 필터를 판본별 실존으로 판정**: 운영 6.3.2 에도 있는 `group_id`(제어기·센서·카메라)·`server_id`(스피커) **4곳**은 게이트에서 빼 `AddLegacySafeFilter` 로 분리했다(명세 변경이력만 보고 게이트를 걸어 멀쩡한 운영 필터를 무증상으로 잃고 있었다).
  - **권한 저장 = 전체 교체** — 세 집합이 전부 다르고(서버 enum 15/16 ≠ 우리 카탈로그 12 ≠ 그룹 저장분 12) **서버 문서의 처방("GET 원본에 병합")도 422** 다. `원본 ∪ 카탈로그`(`BuildMergedModules`)가 유일 해법이며, 세션 중 서버가 `units` 를 backfill 해 **13 ∪ 12 = 16** 으로 정확히 맞아떨어졌다. 권한 권위는 서버로 이관하고 하드코딩 오류 3건(`devices`/`users` control, `broadcast` edit)을 제거.
  - **부대 편제(`/api/units` 7경로, `>= V8_0` 한정)** — `unit_id` 를 조건 없이 보내면 6.3/7.0 쓰기 스키마가 `extra="forbid"` 라 **즉시 422**. `IUnitScopeService` + `UnitScopeGate` 로 쓰기 **14곳을 단일 관문**에 모았다. 부대 코드 정규식(`^[a-z0-9][a-z0-9_-]{0,31}$`, NATS subject 토큰)·`include` 어휘·계층/인접 규칙은 `UnitRules` 에서 **서버 왕복 전에** 검증한다.
  - **이벤트 어휘 내성** — 서버는 어휘를 **늘린다**. `Enum.Parse` 10곳을 `TryParse` 폴백(`ParseOrDefault`)으로 바꿨다. 종전에는 신설값 `Alert` 하나가 **탐지 목록 로딩 전체를 죽였다**. `EnumEventType` 에 서버 확장 어휘 `Alert`(160)·`Operation`(161) 추가 — **PIDS 프로토콜 바이트가 아니다**. `action_reported` 도 `== "True"` 문자열 비교에서 관용 bool 판정으로.
  - **문 개폐 채널 전환(REST → NATS)** — `POST .../control` 은 6.3.16 에서 제거되고 7.0/8.0 에서 **410 묘비**다. 회피만 하면 기능이 사라지므로 클라 → 매니저 `GATE_DOOR_SET` 직행으로 전환(`DoorControlService`). 통문 `…all.gate-door` / 함체 `…all.enclosure-door`. **명령은 상태를 바꾸지 않는다.**
  - **NATS 전역 자원 구독** — 전역 카탈로그류는 부대 토큰이 아니라 **`global`** 로 발행된다(`SYNC_CATALOG`/`SYNC_CATEGORY`/`SYNC_ACTION_REPORT_TEMPLATE`/`SYNC_FILE_GROUP`/`SYNC_UNIT`). `{domain}.global.>` 를 추가 구독 — 종전에는 부대 와일드카드로도 **못 받았다**.
  - **공통 계약 출구** — 에러코드 16종(`ApiErrorCodes`)·`ApiStatusHelper`(409/410/422/401/403 분기)·`ApiErrorTextHelper`(운영자가 읽는 한 줄, 날 JSON·`HTTP 0:` 오표기 차단)·목록 절단 감지(`limit` 상한 100 초과 페이지 순회)·`ResponseWarningDto`.
  - **조치보고 문구 템플릿 7경로 + `/reorder`**(단일 트랜잭션) · **보고서 구성 개수**는 목록 응답이 `components` 를 안 싣고 `component_count` 만 줘서 `EffectiveComponentCount` 로 둘을 흡수(종전엔 전 템플릿이 0개로 보였다).
  - **장비 부품 축 1급화** — `by-component`(문 상태 일괄) · `component-status` PATCH · `/config` 3종 · `/spec` 카탈로그 2종. `component` 와 `component_type` 배타는 지역 검사로 **422 선제 차단**.
  - **세션 식별자 명시 전달** — `session_id` 를 응답 본문 값으로 전달(종전엔 JWT `sid` 클레임 포착에만 의존해 서버가 클레임을 빼면 '내 세션' 판정이 계정 근사 폴백으로 조용히 격하됐다).
  - **실연동 검증**(2026-09-18, 라이브러리 실물 · 모의 0) — 관문 **104개 중 OK 96**. 판본 판정이 같은 바이너리에서 로컬 `V8_0/8.0.1` · 운영 `V6_3/6.3.2` 로 갈림 · `Alert` POST(201)→GET→`MessageType=Alert(160)` · **장비 7종 쓰기 전면 통과**(422 없음) · 권한 전체 교체 16종 무손실 · NATS `GATE_DOOR_SET` 브로커 도달 + `global.>` 실수신 + `requested_at` aware ISO-8601. ❌ **실기 WPF 화면 · 운영 6.3.2 인증 경로 · FR-24 `unit_id` 실주입 쓰기는 미검증**.
- **PIDS 심볼 상세 보기 + 통문·함체 개폐 제어** (Track C · [PRD](docs/prds/symbol-detail-and-door-control-prd.md) · [Plan](docs/plans/symbol-detail-and-door-control-prd-plan.md) · [리포트](docs/reports/symbol-detail-and-door-control-report.md) · 사용자 승인 2026-09-08)
  - **속성창 개폐 UI** — 접점 체크박스 제거 → `문 상태` 세그먼트(열림/닫힘/**명령 대기 중**/상태 미수신). **명령은 상태를 바꾸지 않는다** — 확정 상태는 `OPERATION_EVENT` 보고로만 전이한다(낙관적 갱신 시 구동 실패하면 화면이 거짓말을 한다). 타임아웃도 상태를 지어내지 않는다.
  - **상세 보기 창** — 운영 모드 우클릭 맨 위 진입. 좌측 3D 프리뷰(**45°×8단 자동 회전**, 드래그 자유 회전 + 점선 구 가이드, 놓으면 1.5초 뒤 재개) / 우측 정보 탭 / 하단 액션 바.
    **오빗 카메라는 지도 심볼과 분리**했다 — 지도 하우징은 피치 35° 고정으로 지면 정합이 걸려 있어 각도를 못 바꾼다. 메시·색표(`HousingPalette` 신설)만 공유하고 카메라는 별도. 독립성은 픽셀로 단언(프리뷰 각도를 바꿔도 지도 심볼 비트맵 불변).
  - **액션 바 = 컨텍스트 메뉴 단일 출처** — 메뉴의 인라인 람다 7종을 명명 메서드로 추출해 양쪽이 같은 메서드를 부른다. 이 과정에서 `탐지 이력` 노출 조건 불일치(메뉴 `SmartSensor` 만 / 규칙 감지센서 14종)를 발견해 통일.
  - **GPS 좌표 표기**(`GeoFormat`) — 서버 `geolocation{location,latitude,longitude,altitude,heading}` 를 DD+DMS 병기로. **(0,0)은 좌표가 아니라 "미등록"**, DMS 초 반올림 60 자리올림, 심볼 배치 좌표와의 거리(Haversine)를 20 m 이상이면 경고색으로 — `현재위치 적용` 누락을 눈에 띄게 한다.
  - **Gate REST 경로** — `GateDeviceDto`/`GateDeviceModel` + `IDeviceApiService` 4종 + `SYNC_DEVICE` Gate 분기(메인 `NatsDomainService`).
  - **`BROADCAST_STATUS` 구독**(`BroadcastStatusNatsSyncService`) — 종전엔 자기가 보낸 방송만 로컬 타이머로 표시해 다른 자리에서 시작한 방송이 화면에 없었다. 모르는 status 값은 지어내지 않고 무시한다.
  - **마이크 PTT** — 배선 완료 후 `IsMicCommandSupported=false` 하나로 잠금. 서버 규격 회신([요청서](docs/coordination/server-broadcast-mic-request-2026-09-08.md)) 후 상수만 바꾸면 열린다.

### Fixed
- **축 계약(7.0+)에서 장비 저장이 접속 축·부품을 무경고로 지우던 문제** — 8.0.1 실측: 서버 `PUT` 은 본문의 축 문서를 통째 교체하는데 클라 DTO 의 축은 평면 필드 재조립본이라, 패널 저장(전부 PUT) 한 번에 `connection.type`→`IP_DIRECT`·`channel` 소실, 카메라는 `components[]` 전부 소실(200·경고 없음). → `DeviceApiService.WriteExistingDeviceAsync`: 7.0+ 는 `PATCH`(객체 병합), 6.3 은 종전 `PUT`. + `HardwareSpecDto.AllowComponentsWrite`(기본 false). 운영 6.3.2 미발현. (device-console-v8 FR-15)
- **재조회·`SYNC_DEVICE` 가 통문을 갱신하지 않고, 종류축이 바뀐 장비를 중복 추가하던 문제** — 병합 키 `(Id, DeviceType)`→`(Id, 판별자)`, 통문 분기 신설. `SmartMultisensor2` 통지 무시 · 스피커 삭제 통지 영구 미매칭(`"Speaker"≠IpSpeaker`)도 판별자 라우팅으로 해소.
- **v7.0+ 통문의 개폐가 항상 거부되던 문제** — 응답에 `type_device` 가 없어 `DeviceType=NONE` 이 됐다. 판별자에서 복원.
- **Devices.Ui 테스트 flaky 2건**(Caliburn `IoC` 정적 상태 실행 순서 의존) — `TestIoCScope` + 컬렉션 직렬화.
- **신설 이벤트 어휘가 탐지 목록 전체를 죽임** — `Enum.Parse` 가 미지 문자열에 `ArgumentException` 을 던져, 서버가 `Alert` 를 detection 카테고리로 보내기 시작하자 목록 로딩이 통째로 실패했다. 관용 파싱(`TryParse` + default 폴백)으로 교체 — **어휘가 늘어도 화면이 죽지 않는다.**
- **`ShouldSerializeDevice() => false` 가 NATS 본문을 파손** — REST 쓰기 422 를 막으려 `device` 직렬화를 끈 것이 **`ACTION_REPORT` 의 `from_event.device` 까지 지웠다**(`ShouldSerializeXxx` 는 경로가 아니라 **타입 전역**에 적용된다). 경로별 차단은 플래그 게이트(`SuppressDeviceOnRestWrite`, REST 쓰기 구간에서만 켜고 `finally` 로 복원)로 해결. 회귀 테스트 2건으로 고정.
- **분기를 만들어 놓고 실제로 켜지지 않던 죽은 코드 2건** — `ResolveAsync()` 호출부가 **0건**이었고, Autofac 팩토리가 프로브를 **전달하지 않아** 모든 판본 분기가 무력했다. 빌드·테스트는 전부 통과하는 상태로 숨어 있었다.
- **이벤트 장비가 전부 '센서'로 폴백** — 판본별 `device` shape 차이(운영 6.3.2 = 전문 객체 + `type_device` / 개발 8.0.1 = `DeviceReference` `{id, category_device}` 두 키)를 반영해 복원 순서를 `type_device` → `category_device` → `'알 수 없음'` 으로. ⚠ `sensor` 카테고리는 **의도적 미매핑**(Fence·Multi·PIR·SmartSensor 가 모두 들어 있어 하나를 고르면 틀린 종류를 단정한다).
- **함체 임계치 경계 오판** — 7개 경계를 서버 실측값에 맞춤.
- **희소 DTO 로 부분 수정(PATCH) 시 `controller_id: 0` 전송 → 404** — `SensorDeviceDto.ControllerId` 는 생성 필수라 비-nullable `int` 로 재선언돼 있어 값형 기본값 `0` 이 그대로 나가고, 서버가 `NOT_FOUND "Controller with id 0 not found"` 를 돌려줬다. `ShouldSerializeControllerId() => ControllerId > 0` 로 게이트(0 은 어느 판본에서도 유효하지 않아 드롭이 항상 안전 · 전체 교체 PUT 무영향). **실연동 와이어 로그로 발견**. ⚠ `number_device`/`status`/`is_enable` 도 희소 PATCH 에서 함께 실리므로 **부분 수정에는 완전한 DTO 를 넘긴다**는 계약을 문서로 남겼다.
- **목록 `limit` 서버 상한(100) 미클램프로 침묵 실패** — `limit=200` 을 그대로 흘려 서버가 422 로 거절하면 목록이 **빈 응답**으로 보여 "데이터 없음"으로 오진됐다. `DeviceApiService.ClampLimit` 신설, 목록 **7곳**(제어기·센서·카메라·스피커·함체·통문·경광등)에 적용 + 범위 밖이면 경고 로그. **실연동에서 발견**.
- **계정 목록 101번째부터 조용히 절단** — 서버 `limit` 상한 100 이라 단일 호출로는 전량을 못 받는다 → page 순회.
- **통문(Gate) 부팅 벌크 로드 누락** — `DeviceProviderService.FetchAllDevicesAsync` 가 제어기·센서·카메라·스피커·함체·경광등은 가져오는데 **통문만 빠져 있었다**. 단건 조회는 `SYNC_DEVICE` 전용이라 부팅을 못 덮어, 통문 심볼의 `LinkedDevice` 가 영원히 null → **개폐 버튼 항상 잠김 + 부팅 시 문 상태 복원 무력**. 실기 로그로 확인(`Gates loaded: 2 items` — 서버에 있었는데 안 불러오고 있었다).
- **장비 객체가 없는데 "연결됨"으로 표시** — `LinkedDeviceId > 0` 로 판정해 장비 목록에 그 장비가 없어도 탭이 전부 활성이고 값만 전부 `—` 였다. **객체 유무**로 판정하고 사유를 두 갈래로 구분(`연결되지 않음` / `장비(#id) 정보를 찾지 못함`).
- **명령류 권한 fail-closed 누락** — `cameras:control` 이 `?? true` 로 남아 있었다. 정책을 `PermissionGate` 한 곳으로 모아 **명령류만 fail-closed / 조회·편집은 fail-open** 비대칭을 테스트로 고정.
- **상세 창 재개봉 시 `⟲ 정면` 사망 · 숨은 창 3D 타이머 잔존** — 창이 Visibility 토글로 닫혀 `Unloaded` 가 오지 않는데 구독을 끊고 있었다. 구독 해제는 진짜 소멸 때만, 타이머는 `IsVisibleChanged` 로.

### Changed
- **억제 스케줄 생성 폼 비주얼 리디자인 — 스토리보드 ①~⑥ 적용** (Track B · Events.Ui · [스토리보드](docs/design/suppression-schedule-form-redesign.html) · 사용자 승인 2026-08-07)
  - **① 입력 언어 통일** — 기존엔 TextBox/ComboBox 는 **Material 밑줄**, `mah:DateTimePicker` 는 **MahApps 박스**라 한 폼에 디자인 언어가 두 개였다. 세 컨트롤을 **박스형 36px**로 통일(`FormTextBox`/`FormComboBox` = `MaterialDesignOutlined*` 기반, `FormDateTimePicker`).
  - **② 좌우 바닥선 정렬** — 좌측 필드를 **2열 × 3행**(작업명·대상유형 / 억제범위·대상측 / 시간창 span2)으로 재배치해 우측 트레이와 높이를 맞춤. 대상 유형 라디오도 36px 박스로 감싸 같은 리듬.
  - **③ 주 액션 승격** — `＋ 억제 창 생성`을 **Primary 채움 버튼**(`PrimaryActionButton`)으로. 비활성은 38% 불투명 + **`ToolTipService.ShowOnDisabled="True"` + `CreateHintText`** 로 *왜 못 누르는지*를 호버로 안내(권한/작업명/기간상한/시각역전/대상미선택 5분기).
  - **④ 액션 바 신설** — 카드 하단에 구분선 + 우측 정렬 `[초기화][＋ 억제 창 생성]`. 기존엔 생성 버튼이 필드 사이 허공에 떠 있었다. (`ResetForm` 을 public 으로 승격)
  - **⑤ 빈 트레이** — 실선 사각형 대신 **점선 테두리 + 아이콘 + 2줄 안내**(`Rectangle StrokeDashArray`, 선택이 있으면 실선으로 전환). 전체 대상 모드는 경고색 아이콘 + 안내로 트레이 자리를 채움.
  - **⑥ 목록 선택행** — 회색 블록(`SurfacePressedBrush`) → **좌측 3px 시안 바 + 틴트 배경**(`SuppressionRowStyle`).
  - **⚠ 스타일은 전부 뷰 로컬 등록** — `Events.Ui/Resources/Resources.xaml` 는 앱 리소스 트리에 **병합되지 않는다**(앱은 자체 복제본만 머지). 라이브러리 Resources 에 넣으면 빌드는 통과하고 **런타임 XamlParseException** 이 난다.
  - **검증**: 빌드 오류 0 · `EventSuppressionScheduleTests` **31/31 통과** · **StaticResource/DynamicResource 41키 + PackIconKind 8종 정적 전수 검증 → 미해석 0** (XAML 리소스 미해석은 빌드가 못 잡고 런타임에만 터지므로 별도 스크립트로 확인). ⚠ **실기 렌더 미검증** — 특히 Outlined 스타일의 floating hint 가 Height=36 에서 어떻게 보이는지는 눈으로 확인 필요.
  - **🔴 후속 수정(같은 날, 실기 스크린샷에서 발견)** — 위 ①에서 준 `Height="36"` 강제가 **입력 컨트롤 3종을 세로로 클리핑**시켰다. 작업명 힌트·억제 범위 값·장비 추가 힌트가 전부 글자가 아니라 '실선'처럼 뭉개졌다.
    **원인**: MDIX Outlined 템플릿은 **floating hint(테두리 노치로 떠오르는 라벨)용 세로 공간을 내부에서 예약**하는데, `Height` 고정이 그 공간까지 눌렀다. 힌트를 주지 않은 ComboBox 도 같은 템플릿이라 함께 깨졌다.
    **수정**: `Height` → **`MinHeight="36"`(하한만)** + **`md:HintAssist.IsFloating="False"`**(노치 자체를 없애고 플레이스홀더로 동작). 대상 유형 라디오 Border 도 같은 원칙으로 `MinHeight` 전환.
    **교훈**: MDIX Outlined 계열에 `Height` 를 강제하지 말 것. 스타일 키가 존재해도(정적 검증 통과) **레이아웃은 실기에서만 드러난다**.
  - **⑦ 목록 '작업명/대상' 열 통합은 미적용** — 기존 열 구성 변경이라 사용자 확인 대기.
- **억제 스케줄 생성 폼 레이아웃 재구성 — 칩이 늘어도 폼이 밀리지 않는 고정 구조** (Track B · Events.Ui · 롤백태그 `before-suppression-form-layout` · 사용자 지시 2026-08-07)
  - **증상**: 장비를 선택할수록 칩이 쌓이며 뒤 필드(대상 측·범위·시간창·생성 버튼)를 줄바꿈으로 계속 밀어낸다. 장비가 100개면 폼이 화면을 덮는다.
  - **원인**: 폼 전체가 `<WrapPanel Orientation="Horizontal">` 이고 그 안의 장비 칩 `ItemsControl` 에 **크기 상한이 없었다**(`MinHeight="22"` 만 존재). 칩이 늘면 그 칸의 폭이 자라 바깥 WrapPanel 이 재배치 → 후속 필드가 밀린다. 칩 폭도 장비명 길이에 끌려가 제각각이었다.
  - **수정 — 3층 고정화**:
    ① **바깥 WrapPanel → 명시적 2컬럼 Grid**(좌 = 입력 필드 `*`, 우 = 대상 선택 트레이 `356` 고정). 좌측은 3열 × 3행 고정 배치라 **모드를 바꿔도 필드 자리가 이동하지 않는다**.
    ② **칩 크기 정규화** — `WrapPanel ItemWidth="148" ItemHeight="26"` + 칩 내부 `TextTrimming="CharacterEllipsis"` + 전체 이름은 `ToolTip`. 이름 길이와 무관하게 격자로 정렬된다.
    ③ **트레이 고정 높이 104 + 내부 세로 스크롤** — 2열 × 4행(8개) 노출, 그 이상은 트레이 안에서만 스크롤. **칩이 몇 개든 폼 높이가 불변**이다.
  - **부가**: 트레이 헤더에 `선택 N개` + **[모두 지우기]**(`ClearDevices`/`ClearGroups` 신설) — 100개를 하나씩 ✕ 누르지 않아도 된다. 빈 트레이 안내 문구 추가. 장비/그룹 트레이는 상호배타라 **같은 자리에 겹쳐 배치** → 모드 전환 시 흔들림 없음. 전체 대상 모드는 트레이 자리를 안내 카드로 채워 빈 칸을 없앰.
  - **🔴 동반 버그 수정 — `대상 측(side)` 표시 조건이 정반대였다**: 내장 `BooleanToVisibilityConverter` 는 **`ConverterParameter` 를 무시한다.** 기존 `Visibility="{Binding IsDeviceMode, Converter={StaticResource BoolToVis}, ConverterParameter=invert}"` 는 반전이 걸리지 않아, 주석(`대상 측(group/all)`)·VM 문서(`감지/감시 필터(group·all)`)와 반대로 **장비 모드에서 노출되고 그룹/전체 모드에서 숨겨졌다.** `utils:BoolToInverseVisibleConverter` 로 교체하고, 컨버터 선언부에 재발 방지 주석을 남김.
  - **자동화 계약**: 기존 AutomationId 전부 보존 + 신설 — `ClearDevicesButton`/`ClearGroupsButton`, `SelectedDeviceCountText`/`SelectedGroupCountText`, `DeviceChipTray`/`GroupChipTray`, `AllTargetNoticeText`, 그리고 반복 요소는 바인딩식 ID(`DeviceChip.{0}`/`DeviceChipRemove.{0}`/`GroupChip.{0}`/`GroupChipRemove.{0}`).
  - **검증**: `Events.Ui` 빌드 오류 0 · `EventSuppressionScheduleTests` **31/31 통과**. ⚠ **실기 렌더 미검증**(UI 배치 확인은 데스크톱 독점 필요).

### Fixed
- **억제 창 생성 완료 팝업이 대상 목록을 전부 나열해 잘려서 안 읽히던 문제** (Track B · Events.Ui · 사용자 제보 2026-08-07)
  - **증상**: 생성 직후 팝업이 `[장비 6개]` + 장비를 **한 줄에 하나씩 전량 나열** → 정보 팝업이 고정 높이라 위아래가 잘려 `[장비 6개]` 헤더도, 마지막 항목도 안 보인다. 장비 6개만 돼도 발생.
  - **수정**: `BuildTargetEcho` 를 **"앞 3개 이름 + 외 N개" 한 문장**으로 접음 — 예: `정문, 주차장, 외곽_북측 외 3개 장비에 억제 창을 생성했습니다.` 3개 이하면 `정문, 주차장(장비 2개)` 형태. 전체 대상은 side 를 한글로(`전체 대상 · 감지+감시`).
  - §6(대상 오지정 사고 방지)의 안전 목적은 **총 개수를 문장에 유지**하고 "아래 목록에서 확인하세요" 안내를 붙여 보존. 전체 목록은 스케줄 표의 '대상' 열에서 확인한다. (기존 동작은 **잘려서 확인 자체가 불가능**했으므로 순수 개선.)
- **억제 스케줄 상태/대상 필터 콤보가 빈칸이고 "전체"를 고를 수 없던 버그** (Track B · Events.Ui · 스크린샷에서 발견 2026-08-07)
  - **원인**: `FilterStatus`/`FilterTargetType` setter 가 `""`→`null` 로 정규화하는데 "전체" `ComboBoxItem` 의 `Tag` 는 **빈 문자열**이다. `SelectedValue=null` 은 어떤 항목과도 매칭되지 않아 ① 초기 표시가 빈칸이 되고 ② "전체"를 골라도 setter 가 즉시 null 로 바꿔 **선택이 곧바로 풀린다**.
  - **수정**: 바인딩 값은 원문(`""`) 그대로 두고, **null 정규화는 API 호출 직전에만**(`ApiFilterStatus`/`ApiFilterTargetType`) 수행.
- **보고서: 새로 만든 템플릿이 생성 탭 콤보에 안 뜨는 버그 — 탭 전환이 활성화를 일으키지 않는 구조** (Track B · Reports.Ui · 롤백태그 `before-report-template-combo-refresh` · 사용자 제보 2026-08-07)
  - **증상**: 템플릿을 새로 만든 뒤 바로 [생성] 탭으로 가면 방금 만든 템플릿이 드롭다운에 없다. 콘솔을 닫았다 다시 열어야 나타난다.
  - **원인**: 콘솔 탭 호스트가 `Conductor.OneActive` 가 아니라 **평범한 `TabControl`**(`ReportConsoleView.xaml:37-47`)이라 **탭을 바꿔도 Caliburn 의 Activate/Deactivate 가 발생하지 않는다.** 세 탭 VM 은 콘솔을 열 때 `ReportConsoleViewModel.OnActivateAsync:44-46` 에서 한 번에 활성화되고, `ReportCreateViewModel.LoadTemplatesAsync` 의 호출부도 그 활성화 하나뿐이다 → **콤보는 콘솔을 연 순간의 스냅샷**. 저장 후 처리(`OnEditSaved`)는 `TemplateViewModel.LoadAsync()` 만 불러 템플릿 탭 목록만 갱신했다.
  - **같은 뿌리의 미보고 결함 2건 동시 수정**:
    ① **삭제한 템플릿이 생성 콤보에 잔존** — 선택·생성까지 가능(서버 404/422 유발). `ReportTemplateViewModel` 에 `TemplatesChanged` 이벤트를 신설해 삭제 성공 시 발화, 콘솔이 구독.
    ② **재적재 시 콤보가 빈칸이 되는 잠복 버그** — `Templates.Clear()` 가 ComboBox 바인딩을 통해 `SelectedTemplate` 을 null 로 되돌리고 재적재 항목은 **다른 인스턴스**라 참조 비교가 깨진다. **Clear 이전에 Id 를 확보해 Id 로 복원**하도록 변경(기존 `SelectedTemplate is null` 가드로는 stale 참조가 살아남아 복원되지 않았다).
  - **부수 회귀 방지**: `SelectedTemplate` setter 가 템플릿 기본기간으로 `SelectedPeriod` 를 덮어쓰는데, 새로고침은 **같은 Id 를 재대입**하므로 그대로 두면 사용자가 고른 기간이 갱신마다 초기화된다 → **Id 가 실제로 바뀔 때만** 기간을 동기화하도록 수정.
  - **변경 파일 3**: `ReportConsoleViewModel.cs`(구독/해제 + `RefreshCreateTemplatesAsync`) · `ReportTemplateViewModel.cs`(`TemplatesChanged`) · `ReportCreateViewModel.cs`(Id 기반 선택 복원 + setter 가드).
  - **빌드**: `Reports.Ui` 오류 0. ⚠ **실기 검증 미실행** · Reports.Ui 에는 테스트 프로젝트가 없어 회귀 테스트 미작성.
  - 부수: 세 VM 파일이 한글을 담고도 BOM 없이 있던 상태를 UTF-8 BOM 으로 교정.

### Removed
- **보고서 템플릿 '공개(is_public)' UI 제거 — 서버 미집행 필드** (Track B · Reports.Ui · [분석](docs/analyses/report-template-is-public-analysis.md) · 롤백태그 `before-remove-template-ispublic-ui` · 사용자 지시 2026-08-07)
  - **근거**: `is_public` 은 "템플릿을 읽기 전용으로 공유"하도록 설계됐으나 **클라·API·서버 3계층 전부 미집행**이다. 서버 목록 GET 에 WHERE 절 자체가 없고(`reports.py:268-276`), 생성 시 `owner_id` 를 설정하지 않아 영구 NULL(`:303-310`) — `owner_id == me` 조건이 성립할 토대가 없다. 서버 레포도 이 갭을 2026-02-02 에 표로 기록해 뒀고 6개월간 미해소.
  - **제거 대상 2곳**: 목록 `DataGridCheckBoxColumn Header="공개"`(`ReportTemplateView.xaml:43`, 원래 `IsReadOnly=True` 표시 전용) · 편집 다이얼로그 `CheckBox Content="공개(is_public)"`(`ReportTemplateEditView.xaml:33`, 최종 사용자 화면에 raw 스키마명이 노출되고 있었다). VM 프로퍼티 `IsPublic` 및 로드/초기화 대입도 삭제.
  - **🔴 함정 회피 — PATCH 에서 `is_public` 전송 자체를 생략**: 체크박스만 지우고 본문을 그대로 두면 수정 저장마다 `is_public:false` 가 실려 **서버 저장값을 덮어쓴다**(시드 STANDARD 4종은 `true`). `ReportTemplateUpdateDto.IsPublic` 은 `bool?` + `NullValueHandling.Ignore` 이므로 **미지정으로 두어 필드를 아예 빼는 것**이 정답. 기존 코드는 이 부분수정 의미를 쓰지 않고 항상 non-null 로 채우고 있었다(lost update 소지).
  - **생성(POST)은 무변화**: `ReportTemplateCreateDto.IsPublic` 은 non-nullable `bool` 이라 미지정 시 `false` 로 직렬화되며, 이는 서버 DB 기본값(`is_public BOOLEAN NOT NULL DEFAULT FALSE`)과 동일하다.
  - **와이어 계약 무변경**: DTO 3종의 `is_public` 필드는 그대로 유지(응답 역직렬화·향후 복구 대비). UI 표면만 제거했다.
  - **서버 준비도(참고)**: 인프라는 이미 갖춰져 있다 — `reports.py:62` 가 전 엔드포인트에 토큰을 강제하고 필요한 의존성도 import 완료. 구현 시 코드는 ~10줄. 남은 실차단은 ① 기존 행 `owner_id` 전량 NULL(필터 켜는 순간 비공개 행이 관리자에게도 소멸 — ADMIN bypass 가 목록 WHERE 에 없다) ② 클라 계약 breaking change(목록 건수 감소 + 신규 403, 계약서에 403 정의 0건). 서버 구현 시 이 UI 를 복구한다.
  - **부수 정리**: `ReportTemplateEditView.xaml` · `ReportTemplateEditViewModel.cs` 가 한글을 담고도 BOM 없이 있던 상태를 UTF-8 BOM 으로 교정.
  - **빌드**: `Reports.Ui` 오류 0. ⚠ **실기 검증 미실행**(앱 기동 확인 필요).

### Added
- **GMap 스키마 마이그레이션 멱등화 — 부팅 예외 26건 제거 + 데이터 파괴 뇌관 해체** (Track C · GMaps.Db · [PRD](docs/prds/GMap_Schema_Migration_Idempotency-prd.md) · [Plan](docs/plans/GMap_Schema_Migration_Idempotency-prd-plan.md) · 롤백태그 `before-gmap-schema-guard` · 사용자 지시 2026-08-04)
  - **출발점**: 앱 시작 시 디버거 출력창에 `MySqlException` 22건 + `NotSupportedException` 4건. 전량 `GMapDbSymbolService.BuildSchemeAsync` 한 메서드에서 발생하며 bare catch 로 삼켜져 부팅에는 영향이 없었다. 그러나 그 소음 뒤에 **사문(dead) 마이그레이션 3건과 데이터 파괴 뇌관**이 있었다.
  - **원인 확정(런타임 재현)**: ① 22건 = `CREATE TABLE` 에 이미 있는 컬럼을 매 부팅 `ALTER TABLE ADD COLUMN` 으로 재실행 → error 1060. ② 4건 = `conn.ExecuteAsync(sql, token)` 이 CancellationToken 을 Dapper 의 `param` 슬롯에 넘기는데, 정규식 `'^-?[0-9]+$'` 안의 홀로 선 `?` 가 Dapper `smellsLikeOleDb` 에 매치되어 파라미터 필터링이 꺼지고 `The member None of type System.Threading.CancellationToken cannot be used as a parameter value` 로 죽는다 — **SQL 이 서버에 도달조차 못 했다**. 색상 정리 UPDATE 4문은 배포 이후 한 번도 실행된 적이 없다.
  - **🔴 파괴 뇌관**: 같은 try 에 묶인 `UPDATE Symbols SET ZOrder = ZIndex` 는 바로 앞 ADD COLUMN 의 1060 예외 덕분에 영구 미실행이었다. **예외를 없애는 순간 이 문장이 처음 실행된다.** 실측 `monitor_db` 는 Symbols **102행 전부** `ZIndex=10` / `ZOrder=1000~1106`(불일치 102/102, `zindex_distinct=1`) — 전 행 ZOrder 가 10 으로 평탄화되고 band shift 가 전부 1010 으로 밀어 상대 순서가 소실, 지도 로드 시 `EnsureUniqueZOrder` 가 붕괴 순서를 `BatchUpdateZOrderAsync` 로 DB 에 확정 기록한다 → **코드 롤백으로 복구 불가**.
  - **사전 검토**: 수정안의 사이드 이펙트를 **188 시나리오 × 2라운드**(2차=적대적 반증)로 시뮬레이션 — ISSUE 78(P0 11)·WATCH 82·**2차에서 46건 판정 뒤집힘**. 대전제 하나가 반증됨: "Dapper 를 고치면 취소가 살아난다"는 **거짓**(MySql.Data 9.3.0 은 명령 실행 중 CancellationToken 미관측, 취소 가능 지점은 `OpenAsync` 뿐 — IL 전수 + `SELECT SLEEP(5)` 프로브).
  - **수정**: ① 위험 UPDATE·레거시 `ZIndex ADD COLUMN` **삭제**(가드 전환보다 먼저) ② `COLUMN_SPECS` 선언적 사양표(22컬럼) + `LoadColumnMetaAsync` 로 `INFORMATION_SCHEMA` 선조회 가드 — 테이블당 1회, `AND TABLE_SCHEMA=DATABASE()` 필수, `OrdinalIgnoreCase`, **0행=판정불능→스킵+Warning**, `CommandTimeout` 10s ③ MODIFY 는 `COLUMN_TYPE`/`IS_NULLABLE` 로 분리 판정(ADD 가드에 접으면 v2.2 INT 스키마 교정 경로가 영구 정지) ④ catch 는 '좁히기'가 아니라 **'분류하기'**(`OperationCanceledException` 최우선 rethrow → 1060/1091 스킵 → 그 외 Warning; 좁히면 `NotSupportedException` 이 탈출해 심볼 0건 부팅) ⑤ 요약 로그(`적용/스킵/실패/타입교정/band행`) + band shift **반환 행수 수신** ⑥ `LabelOffsetX`/`Y` 독립 판정(공유 try 소멸).
  - **MariaDB 전용 `ADD COLUMN IF NOT EXISTS` 는 채택하지 않음** — MySQL 서버에서 1064 로 전 마이그레이션이 죽고(서버 판별 코드 0줄), 적용/스킵 여부를 앱이 알 수 없어 요약 로그를 만들 수 없다.
  - **검증**: 운영 덤프를 격리 DB(`monitor_test_db`)로 복원한 **수렴 DB** 기준 `SchemaGuardMigrationTests` **4/4 통과**(ZOrder 102행 전량 동일 · 2회 부팅 멱등 · 정상 색상값 미변경 · 마이그레이션 컬럼 실재). 부팅 로그 `적용 0 / 스킵 22 / 실패 0` — **예외 0건**. ⚠ 기존 픽스처는 매번 DropTables 를 타 항상 최신 스키마만 만나므로 이 파괴를 **구조적으로 관측할 수 없다**(빈 DB 그린은 안전 근거가 아님).
  - **반증 실험**: 위험 UPDATE 를 일시 되살리자 S2 가 즉시 실패 — `(48,1036),(49,1035),(50,1034)…` → **전부 `1010` 으로 평탄화**. 시뮬레이션 예측과 정확히 일치. 원복 후 4/4 재통과 = 테스트가 실제로 이 버그를 잡는다.
  - **롤백 자산**: `git tag before-gmap-schema-guard` · `monitor_db` 전체 덤프 · 표적 스냅샷 `symbols_zorder_backup_20260804`(102행)·`images_title_backup_20260804` · `ROLLBACK.sql`(전후 검증 쿼리 포함) — `C:\workspace_app\_db_backups\20260804_gmap_schema_guard\`. **코드 롤백만으로는 데이터가 복구되지 않으므로 백업이 유일 수단.**
  - **부수(FR-15 부분)**: `GMaps.Db.csproj` 에 `IncludeTests`(기본 false) 도입 — 기본 빌드에서 `Tests/**` 컴파일 제외 + 테스트 패키지 `PrivateAssets=all`. **출하 어셈블리에서 DROP TABLE 픽스처 코드 제거 실증**. 잔존 `testhost.dll` 은 `Monitoring.Models` 등 **24개 출하 프로젝트가 무조건부로 xunit 을 참조**하는 리포지토리 전역 결함 → 별도 PRD 필요.
  - 잔여: 실앱 E2E · 빈 DB(S1)/레거시 DB(S3) 시나리오 · 현장 DB 수집(V-01/05/06/07/08) · 심볼 0건 사용자 가시화(FR-14).
- **탐지 수정 저장 — PUT 실패 후 실제 저장 여부 되읽기 검증** (Track B · Events.Ui · 사용자 지시 2026-08-04)
  - **문제(사용자 실측)**: 신호를 고쳐 [적용]→[저장] 하면 `1건 저장에 실패했습니다 … Internal server error` 팝업. **그런데 값은 DB에 저장돼 있다.** 사용자는 실패로 알고 재시도/포기 → 화면과 데이터가 어긋난다.
  - **근거 3중**: ① 사용자 로그 시간순 — `UpdateDetectionEventAsync started(33,534)` → 서버 `SYNC_DETECTION resource_id=52412(33,569)` → `Internal server error(33,581)`. 서버는 **UPDATE 커밋 후에만** SYNC를 발행하므로 쓰기는 끝난 뒤 응답 조립에서 터진 것. ② 서버 재조회 시 `signal`이 편집값과 동일. ③ 서버의 `detail`이 7키 전체 형태 = 클라 `BuildDetectionDetail` 산출물(NATS 유래 이벤트는 2키) → 클라 페이로드가 그대로 DB에 반영됨.
  - **수정** `EventProviderService.UpdateDetectionEventAsync` — PUT 실패 시 `GetDetectionEventByIdAsync` 로 **실제 서버 상태를 되읽어** 보낸 페이로드(type_event·result·detail 7키)와 **전 필드 대조**. 일치할 때만 성공으로 간주해 모델을 반환하고 `WARN 서버 응답 오류였으나 저장은 확인됨` 기록. 불일치·조회 실패·`OperationCanceledException` 은 **원래 실패를 그대로 전파**. Draft(Id≤0)는 검증 대상 아님.
  - **거짓 성공이 아니다**: 실패를 숨기는 게 아니라 실제 상태를 확인하는 것이라 진짜 실패는 여전히 실패로 뜬다. 서버가 고쳐지면 PUT이 성공하므로 이 경로는 **아예 타지 않는다**(정상 경로 되읽기 0회를 테스트로 고정).
  - **패널 무변경**: 예외 대신 모델이 반환되므로 기존 성공 집계·dirty 해제·목록 재조회 로직이 그대로 동작(파급 최소).
  - **실서버 검증(2026-08-04 01:2x)**: 폴백과 동일 순서를 실 API로 재현 — `PUT → 500` → 되읽기 `signal 2563` = 보낸 값 **일치 → 성공 처리**. 검증 후 원값 2556 원복. 동시각 `PUT /events/malfunctions/{id}` 는 200(탐지만 결함).
  - **테스트**: `DetectionSaveVerificationTests` 5종(저장됨→성공 / 값 다름→실패 / 조회 실패→실패 / result 불일치→실패 / 정상 경로 되읽기 0회). 전체 **444통과/15실패**(실패는 기존 베이스라인과 동일 목록, 2회 반복 일치) = **회귀 0**. 빌드 0오류 · BOM · mojibake 0.
  - 범위: 연결(Connection) PUT 도 같은 서버 결함이나 편집 UI 자체가 없어(PRD DEF-04) 사용자 영향 없음 → 미적용. 근본 해결은 서버 `4f0e875` 재배포.
- **탐지 속성창 편집 상태 정규화 — `DetectionEditModel` 도입** (Track C · Events.Ui · PRD `docs/prds/Event_Edit_Save_Pipeline-prd.md` FR-01/02 후속 · 사용자 지시 2026-08-03)
  - **문제**: `DetectionSelectionViewModel`의 편집 상태가 **세 가지 형식으로 흩어져** 있었다 — ① 공통값 필드(`MessageType`/`Result`/`Status`/`DateTime`)는 **무-알림 auto-property**, ② 상세 필드는 `*Edit` 별도 명명 + 별도 알림, ③ 적용 경로도 둘(`item.X = X ?? item.X` 루프 vs `ApplyDetailEdits`)로 갈라져 한쪽이 모델 직접 대입이었다.
  - **정규화**: 편집 상태를 **모델 하나(`Models/DetectionEditModel.cs`)**로 모으고 규칙을 통일 — ① 모든 필드 `null` = "변경 없음", 값 있음 = "이 값으로 변경" ② 모든 필드 PropertyChanged 발생(무-알림 auto-property 제거 = DEF-18 잠복 결함 해소) ③ 적용 경로는 `ApplyTo(row, includeDetail)` **하나**뿐이며 **행 ViewModel 세터만** 사용.
  - **데이터 흐름 일원화**: 속성창 입력 → `DetectionEditModel` → `ApplyTo` → **DataGrid 항목(행 VM)** → `IsEdited` → 저장 루프 → `ToDetectionEventReplaceDto` → PUT. 중간에 모델 직접 대입 지점이 남아 있지 않다.
  - **뷰 무변경**: `DetectionSelectionView.xaml`이 타 세션 편집 중이라 기존 바인딩 이름(`MessageType`/`Result`/`SignalEdit`…)을 **얇은 위임 래퍼**로 유지 — XAML 0줄 수정.
  - **부수**: `AiModel` 공백 입력을 세터에서 `null`(변경 없음)로 정규화 + Trim. `DetectionEventPanelViewModel.RefreshSignalScale()` 신설 — 신호 편집이 목록 최대값을 바꿔도 미니바 기준이 갱신되도록(행 증감이 없으면 `MaxSignal`이 재계산되지 않던 문제). 죽은 헬퍼(`CommonOrNullValue`/`CommonOrNullString`) 제거.
  - **서버 계약 명문화**: 기존 행(Id&gt;0)에서 실제 변경 가능한 것은 `result` 와 `detail` 뿐 — `type_event`/`created_at`/`action_reported` 는 행 VM의 `IsDraft` 가드가 차단(Draft 에서만 반영). 테스트로 고정.
  - **테스트**: 정규화 계약 7종 추가(시딩·다중선택 상세 미시딩·공백 정규화·Trim·불변필드 차단·result 반영·공통필드 다중적용) → 신규 파일 **24종 전건 통과**. 전체 **439통과/15실패**(실패는 기존 베이스라인과 동일 목록, 2회 반복 일치) = **회귀 0**. 빌드 0오류 · UTF-8 BOM · mojibake 0.
  - **실서버 실측(2026-08-03 24:35)**: `PUT /events/detections/{id}` 는 **여전히 500** 이나 **DB에는 값이 반영됨**(signal 982→983 확인 후 원복). 같은 시각 `PUT /events/malfunctions/{id}` 는 **200** → 서버 lazy-load 수정 커밋 `4f0e875` 가 아직 미배포임이 재확인. 클라이언트는 500을 정상적으로 실패로 표시하므로 **화면상 "저장 실패"는 서버 배포 전까지 계속** 뜬다(값은 저장됨).
- **이벤트 편집→적용→저장 파이프라인 정합성 (탐지 detail 무음 실패 수정)** (Track C · Events.Ui · [PRD](docs/prds/Event_Edit_Save_Pipeline-prd.md) · 롤백태그 `before-event-edit-dirty-fix` · 사용자 지시 2026-08-03)
  - **문제**: 속성창에서 탐지 상세(신호·AI 모델·추론ms·프레임 W·H)만 고치고 [적용]→[저장] 하면 **오류도 성공도 없이 저장되지 않음**. 결과 콤보를 함께 바꾼 날만 저장돼 "됐다 안 됐다" 하는 간헐 버그로 보였으나 실제로는 100% 결정적.
  - **근본원인**: `ApplyDetailEdits()`가 행 ViewModel 세터를 우회해 모델에 직접 대입 → `IsEdited`(dirty) 미설정 → 저장 루프 `Where(vm => vm.IsEdited && Id>0)`에서 **행 자체가 탈락**(PUT 0건). Events.Ui 전체에 `SetProperty(` 호출이 0건이라 dirty 경로는 `SetModelProperty` **하나뿐**임을 Grep으로 확정.
  - **분석**: 7에이전트 워크플로(4축 분석 → 58시나리오 시뮬레이션 → 종합)로 **결함 20건(DEF-01~20)** 도출, FR 15개·회귀위험 12건·테스트 22종 산출. 이번 적용은 **배치 1(FR-01~06)** — 나머지는 타 세션 편집 중 파일(배치 2)·서버 계약 확인 선행(배치 3)으로 분리.
  - **수정 4건**: ① `DetectionEventViewModel`에 detail 세터 5종(`Signal`/`AiModel`/`InferenceMs`/`FrameWidth`/`FrameHeight`) 신설 — 전부 `SetModelProperty` 경유(장애 도메인 `MalfunctionEventViewModel`과 동일 패턴으로 수렴), `Signal` 세터는 `SignalText`/`HasSignal`까지 알림 → 그리드 신호 컬럼·미니바 즉시 갱신(DEF-16 동시 해소). ② `ApplyDetailEdits()`가 `FirstModel`(모델)이 아니라 `_selection[0]`(행 VM)에 대입. ③ Draft 수집 소스를 `_eventProvider`→`ViewModelProvider`로 일원화(나머지 3패널과 동일 — 첫 조회 실패 시 구독 미부착으로 POST가 무음 누락되던 DEF-03). ④ 저장 결과 **항상 통지** — "변경된 내용이 없습니다." / "n건을 저장했습니다."(0건 저장과 성공이 화면상 동일해 무음 실패를 감지할 수 없던 DEF-08).
  - **불변 유지**: 서버발 갱신(NATS SYNC·썸네일 후속 수신)은 **모델 직접 갱신 유지** = dirty 미설정(거짓 PUT·감사로그 오염 방지). 그리드 detail 컬럼 OneWay 유지. "빈칸=변경 없음" 계약 유지. 썸네일/objects는 `BuildDetectionDetail` 전량 재구성으로 보존.
  - **테스트**: `EventEditSavePipelineTests` **17종 신설**(dirty 경로 11 · 속성창 적용 4 · 저장 파이프라인 2) 전건 통과. **판별력 입증**: 수정을 일시 되돌리자 `should_mark_row_as_edited_when_apply_called_with_detail_edit` · `should_collect_draft_from_viewmodel_provider_when_provider_not_mirrored` **정확히 2건만 실패** → 복원 후 통과. 부수 수정: `BatchActionReportTests`가 IoC 스텁 클래스 중 유일하게 `[Collection("IoC-Dependent")]` 누락 → 전역 정적 `IoC.GetInstance` 경쟁으로 간헐 실패 유발, 직렬화 컬렉션에 편입.
  - **회귀 0**: 베이스라인(내 변경만 stash 격리) 415통과/15실패 → 적용 후 **432통과/15실패**(실패 목록 동일, 3회 반복 일치 — 기존 실패는 DeviceSymbolLookup 줌 산술 6 · DtoToModelHelper datetime 4 · XAML 바인딩 4 · NATS 1로 본 변경과 무관). 빌드 0오류 · 한글 UTF-8 BOM · mojibake 0.
  - 🔲 서버 `PUT /events/detections|connections` 500(lazy-load, 커밋 `4f0e875` 미배포) 해소 후 실사용 E2E. 배치 2(불변 필드 UI 게이팅·연결 편집 경로 확정·캐시 복귀 시 dirty 보존)는 타 세션 XAML 커밋 후 착수.
- **UI 자동화 하네스 1단계 — AutomationId 계측(라이브러리 82건)** (Track C · Accounts.Ui + Events.Ui + Reports.Ui + GMaps.Ui · [PRD](docs/prds/UI_Automation_FlaUI_Smoke-prd.md) v1.0 Approved · [Plan](docs/plans/UI_Automation_FlaUI_Smoke-prd-plan.md) · 태그 `before-ui-automation` · 브랜치 `v2.9.23` · 사용자 승인 2026-08-03)
  - FlaUI(UIA3) 스모크 테스트를 위한 `AutomationProperties.AutomationId` 부여. 명명 규칙 `도메인.패널.요소`. **기존 `x:Name` 불변**(Caliburn.Micro 컨벤션 바인딩 지시자 — AutomationId 가 UIA 노출을 이기고 x:Name 은 보존됨, 실측). 렌더링·바인딩 무영향(첨부 속성).
  - 대상: `LoginPanelView`(10) · `EventSuppressionSchedulePanelView`(25, 행 체크박스는 `{Binding Id, StringFormat=…}` 바인딩식) · Reports 5개 뷰(43+WebView2 `Reports.Preview.Browser` 1 — `HwndHostAutomationPeer` 실측 확인) · `MapView`(3).
  - 대응 테스트 하네스는 메인 솔루션 `Dotnet.Monitoring.Solution.UiTests` (Solution CHANGELOG 참조).
- **프로그램 종료 시 강제 로그아웃 (Logout-on-Exit)** (Track C · Accounts.Api · [PRD](docs/prds/logout-on-exit-prd.md) · [분석](docs/analyses/logout-on-exit-analysis.md) · 롤백태그 `before-logout-on-exit` · 사용자 지시 2026-08-03)
  - **문제/동인**: 앱 종료 시 서버 세션이 정리되지 않아 세션 목록에 종료된 클라 세션이 만료/다음 로그인 evict_all 전까지 활성 잔존. 요구=정상 종료 경로에서 서버 로그아웃 + 토큰 폐기.
  - **훅**: `Ironwall.Dotnet.Libraries.Base/ParentBootstrapper.cs` `OnExit`이 모든 `IService.StopAsync`를 10초 예산으로 병렬(디스패처 밖) 호출 → 컨테이너 dispose. 메인앱 `Bootstrapper : ParentBootstrapper<ShellViewModel>`은 `base.OnExit`만 호출 → **DI 모듈 등록만으로 메인앱 코드 변경 0(재빌드만)**.
  - **신규 `LogoutOnExitService : IService`**(Accounts.Api): `StopAsync`에서 `IsAuthenticated` 게이트 → `IAccountApiService.LogoutAsync`(순수 POST `/auth/logout`) **`Task.WhenAny`+`Delay(4s)` 실데드라인** best-effort → 방어적 `ITokenStorageService.Clear`. 예외 삼킴·무팝업·무이벤트. `AccountApiModule`에 `As<IService>` Order=`_count+2` 등록, `AccountUiModule` 회계 `_count += 3`.
  - **architect 독립검증 반영**: I-11 `IAuthGateway.LogoutAsync`는 `ForceLogoutRequested` 발화(종료 중 UI 데드락)라 **미사용**·순수 seam 채택 / I-12 4초 CTS가 HTTP 계층 미관측→`WhenAny` 실상한 / I-13 Order 오프바이원 회계 수정 / I-14 와치독 정상종료 통지(Alt+F4·X 누락)는 후속.
  - **커버 불가(문서화)**: 하드킬/크래시/정전/OS로그오프(SessionEnding 핸들러 부재) → 서버 세션 만료 + evict_all 완화. 크래시 핸들러 로그아웃은 실익<위험으로 제외.
  - **테스트**: `LogoutOnExitServiceTests` 4종(인증→로그아웃+폐기 / 미인증 skip / 예외 삼킴 / hang 데드라인 반환) + resolution 가드 1종 → Accounts.Api **144통과/0실패**. 빌드 0오류·0경고·한글 BOM.
  - **남은 항목**: I-2(서버) `/auth/logout`의 세션 행 비활성화 여부 확인. 🔲 앱 재빌드 후 종료→세션목록 비활성 E2E.
- **제어기 고장 자동복구 확장 (소속 센서 탐지 + 통신 복구)** (Track C · Events.Ui + 메인솔루션 · [PRD](docs/prds/Controller_Fault_AutoRecovery_Extension-prd.md) · [Plan](docs/plans/Controller_Fault_AutoRecovery_Extension-prd-plan.md) · 롤백태그 `before-ctrl-autorecovery-20260801` · 사용자 지시 2026-08-01)
  - **문제/동인**: 기존 자동복구(`Device_CompositeState_SSOT_And_FaultAutoRecovery` REQ-01)는 **동일 센서**(same deviceKey) Fault↔Detection만. 제어기 고장(그룹 검정) 상태에서 **(a)소속 센서 탐지** 또는 **(b)제어기 통신 복구(SYNC_DEVICE ACTIVATED)** 시 제어기 Fault를 자동 조치보고("etc 자동복구")하고 검정 라인을 해제하는 cross-device 자동복구가 없었음(V-05·분석§4에서 명시적 제외). 근거 검증 workflow `wf_30337696-d9d`(spec↔impl 갭 4-리더+적대검증).
  - **원리**: REQ-01 근거("장애 장비가 탐지=경로 생존")를 제어기로 확장. FAULT_CONTROLLER는 EQM 단일 `(controllerId,Controller)` 엔트리라 센서 탐지 `(sensorId,Fence)`가 same-deviceKey로 절대 매칭 안 됨 → **controller-ownership 정밀 매칭** 신설(그룹 공유돼도 오매칭 없음).
  - **라이브러리**: `EventEntry.OwningControllerId(int?)` 추가 · `EventQueueManager.Enqueue`에 제어기-소유 자동복구(Intrusion의 `OwningControllerId`==블랙아웃 Fault의 `DeviceId` 매칭, 제거는 **제어기 자신 deviceKey**로 정리) · `TryAutoRecoverController(controllerId)` 공개 API(Dequeue+`OnAutoRecovery` 재사용, 멱등) · `DetectionNatsSyncService`에 `DeviceProvider` 주입→`sensor.Controller.Id(>0)` 태깅 + `EventUiModule` 팩토리 배선.
  - **메인솔루션**: `NatsDomainService.ProcessSyncDeviceAsync`에 `IEventQueueManager` 주입 + `DeviceType==Controller && Status==ACTIVATED` 가드 → `TryAutoRecoverController`(오소거 방지: 여전히 고장이면 미발동). 자동복구는 기존 `HandleAutoRecoveryAsync`(서버 조치보고+카드제거+NATS) 재사용.
  - **테스트/검증**: `EventQueueManagerControllerAutoRecoveryTests` 5종(트리거a·트리거b·V-03 오매칭방지·멱등·V-05 null스킵) 통과. EQM **65/65**. 라이브러리 빌드0·메인 csc0. **회귀 0 입증**: worktree baseline(HEAD)에 내 4개 기능파일만 격리적용 → 39/39 통과(작업트리 전체 실패 15 = baseline 사전실패 11 + 타 세션 미커밋 XAML 4, 내 기능 무관).
  - 범위 밖: 부팅前 제어기 고장(EQM 엔트리 부재)=`GMap_Controller_Blackout_Runtime_Fix` 조치보고폴백 소관. 🔲 앱 종료 후 재빌드 런타임 E2E.
- **탐지/장애 이벤트 UI enum 한글화 (표시 전용 Converter)** (Track C · Events.Ui · [PRD](docs/prds/Event_Enum_Korean_Display-prd.md) · [Plan](docs/plans/Event_Enum_Korean_Display-prd-plan.md) · 태그 `before-enum-korean-display` · 사용자 지시 2026-08-01)
  - **문제/동인**: 탐지·장애 이벤트 패널/카드/편집기/이력 다이얼로그에 영어 enum(`Fault`/`Intrusion`/`Fence`/`CABLE_CUTTING`/`FAULT_CONTROLLER`/`True`/`False`)이 그대로 노출. **하드 제약(사용자 명시): 표시 계층에서만 한글화, 메시지/포맷·enum 정의·DTO·NATS·DB·SelectedItem 저장/전송 값은 절대 불변.**
  - **신규**: `Converters/EnumKoreanMap.cs`(enum 값→한글 SSOT 딕셔너리, 미매핑=`ToString()` 폴백) + `Converters/EnumToKoreanConverter.cs`(`IValueConverter` **단방향**, `ConvertBack` 미지원). `Resources/Resources.xaml`·`DetectionHistoryDialogView.xaml`에 등록.
  - **적용(표시만)**: 탐지/장애 카드(Result·MessageType·Reason), 탐지/장애 패널 그리드 컬럼 + 편집 콤보 ItemTemplate, Detection/Malfunction 편집기 콤보(Type/Status/Result/Reason) + `DeviceTypeText`(종류), 탐지 이력 다이얼로그(Result 컬럼·필터칩·통계 TopResult·차트 라벨). **ComboBox `SelectedItem`은 raw enum 유지**(ItemTemplate만 변환) → 저장/전송 값 불변. 이력 칩 필터/정렬/차트 데이터는 `Result`(enum) 기준 유지.
  - **Status(EnumTrueFalse)**: 이벤트 문맥(`IsActioned = Status == True`) → "조치완료/미조치" 표기(사용자 재확인 대상).
  - **테스트**: `EnumToKoreanConverterTests` 10종(매핑/폴백/ConvertBack throw/null) 신설 → 10/10 통과. `DetectionSignalTests` 1건(TopResultText 표시 한글화)만 새 기대값으로 갱신. Events.Ui 빌드 0오류 · 한글 UTF-8 BOM. 잔여 테스트 실패 11건=전부 기존 기준선(DeviceSymbolLookup 6·DetectionNatsSync 1) + 동시세션 datetime WIP 4(`Z`↔`+00:00`) → **회귀 0**.
  - 범위 P0+P1(연결/조치 탭 P2 제외). 🔲 앱 재빌드 후 런타임 육안(카드/패널/다이얼로그 한글 표시).
- **탐지 SYNC_DETECTION 썸네일 갱신 (실시간 카드)** (Track C · Events.Ui + Enums · [PRD](docs/prds/detection-sync-thumbnail-prd.md) · [Plan](docs/plans/detection-sync-thumbnail-prd-plan.md) · 태그 `before-detection-sync-thumbnail` · 근거 `docs/coordination/GOP_Server_API_detection_sync_NOTIFY.md` · 사용자 지시 2026-07-31)
  - **문제/동인**: 서버(DBApi)가 탐지 UPDATE 시 `SYNC_DETECTION{action,resource_id}`를 `all.sync.detection`으로 발행(PTZ 회전 후 유효 썸네일). GIS는 **현재 EQM에 등록(활성)된 탐지**에 한해 재조회→실시간 탐지 카드 썸네일을 갱신해야 함. 그런데 코드 실태상 EQM/지도심볼/카드에 **썸네일이 없었음**(썸네일은 이력 패널·선택편집기 2곳뿐) → 갱신 대상(실시간 카드 썸네일 UI)을 신설. **사용자 결정: 대상=실시간 카드 · DELETED=로그만(UPDATED 전용) · 배치=앞면 히어로.**
  - **신규 `DetectionSyncNatsService`(라이브러리 Path A)**: 공유 NATS 팬아웃(`NatsSubscribeEventAsync`) 멱등 구독, 로그인 게이트, `cmd=="SYNC_DETECTION"`+`action=="UPDATED"`만. `EventQueueManager.FindEntryByEventId(resource_id)`로 **활성 게이트**(비활성이면 REST 미호출) → `IEventApiService.GetDetectionEventByIdAsync`(GET `/events/detections/{id}`, 404=`Success=false`→로그만) → `detail.thumbnail/frame_*` 추출 → **UI 스레드**에서 `DetectionThumbnailSyncedMessage` 발행. GET는 NATS 펌프 비블로킹으로 분리(fire-and-forget+내부예외흡수).
  - **카드 UI**: `DetectionEventCardViewModel`에 `ThumbnailUri`(캐시버스트 `_thumbVersion`으로 같은-URL·새바이트 재로딩)·`HasThumbnail`·`ApplyThumbnailUpdate` 추가. `DetectionEventCardView.xaml` 앞면에 **히어로 썸네일 행**(HasThumbnail=false면 Collapsed→행 0px→기존 카드 무영향). `EventCardListPanelViewModel`이 `IHandle<DetectionThumbnailSyncedMessage>`로 `EventId` 매칭 카드에 in-place 반영(없으면 멱등 no-op).
  - **배선**: `EnumGopCommand.SYNC_DETECTION=29`(이름매칭, 라우터 Unknown 경고 회피). `EventUiModule` 수동팩토리 등록(`As<IService>` Order `_count+4`, optional deps 명시) + `RegisterBuildCallback` StartService. **WIP 회피**: `EventProviderService`/`DtoToModelHelper`/`ThumbnailUriResolver` 미수정(타 세션 동시작업) — `IEventApiService` 직접 사용.
  - **카드 크기 = 장애 카드와 동일**(사용자 지시 "탐지/장애 카드 가로세로 맞춰줘"): 앞면 220×200 / 뒷면 250×200 고정(장애 카드와 일치, 카드 확장 없음). 썸네일은 그 안에 들어가는 **상단 밴드(Height 62, HasThumbnail=false면 Collapsed→행 0px→기존 카드 무영향)**. (초기 "히어로 확장 200→300" 결정을 크기 일치 요청으로 수정.)
  - **적대적 코드리뷰(wf 18에이전트) CONFIRMED 6건 수정**: ①[High] 카드 조회 id충돌 — `FirstOrDefault(Id==) as Detection`가 혼재 컬렉션에서 장애 카드 먼저 매칭→유실 → `OfType<DetectionEventCardViewModel>()` 선(先)필터. ②[Med] `ApplyThumbnailUpdate`가 null 썸네일로 기존 유효 썸네일 소거 → `!IsNullOrEmpty` 가드(빈값=재로딩 생략). ③[Med] `FindEntryByEventId`가 EventType 미판별(탐지/장애 독립 id 시퀀스 충돌) → `EnumEventType` 인자 추가(Intrusion 게이트). ④[Med] `await dispatcher.InvokeAsync(async…)` 내부 Task 미언랩(예외 유실) → `.Task.Unwrap()`. ⑤[Med] 같은 id 동시 GET 순서 역전 → resource_id별 seq 가드(newest-wins). ⑥[Med] flip 높이 점프 → (크기 일치로 해소). (F5 low=테스트 시드 경합은 단일메시지 테스트라 무영향, 보류.)
  - **테스트**: `DetectionSyncNatsServiceTests` 9종(UPDATED→GET→발행 / 비활성→GET없음 / DELETED무시 / 비-cmd무시 / 미인증드롭 / 404무발행 / **순서역전 stale폐기** / **EQM 타입판별 2종**) + `DetectionEventCardViewModelTests` 3종(**썸네일 보존 2 + 교체 1**) = **12/12 통과**. Events.Ui 빌드 0오류(신규 경고 0), 사이드 78/79 통과(1실패=기존 기준선 `DetectionNatsSyncService.OnNatsDetection_ShouldPublish` headless, 회귀 0) · 한글 BOM. ⚠**카드 영역 동시 세션 작업 중** — 커밋은 내 파일만 명시(git add -A/-a 금지). 🔲 앱 재빌드 후 런타임 E2E(실 SYNC_DETECTION→카드 썸네일 갱신)=사용자.
- **장애 이벤트 자동조치보고 독립 설정** (Track C · Events + Events.Ui · [PRD](docs/prds/malfunction-autoreport-setting-prd.md) · [Plan](docs/plans/malfunction-autoreport-setting-prd-plan.md) · 태그 `before-malfunction-autoreport-setting` · 사용자 지시 2026-07-31)
  - **문제**: `이벤트설정`의 탐지 항목 1개(`탐지 이벤트 해제` 토글+초)가 탐지·장애 **양쪽** 자동조치보고 타이머·활성을 동시 결정.
  - **라이브러리(이 repo)**: `IEventSetupModel`·`EventSetupModel`에 장애 전용 필드 `IsMalfunctionAutoEventDiscard`/`MalfunctionTimeDiscardSec` 추가(복사 생성자 포함). `MalfunctionNatsSyncService`가 장애 Enqueue 시 신규 필드를 읽도록 변경(탐지 경로 `DetectionNatsSyncService`는 무변경). 장애 토글 OFF → 엔트리 `IsAutoReportEnabled=false` → `EventQueueManager` tick skip → 장애 자동조치보고 미발송.
  - **테스트**: `MalfunctionNatsSyncServiceTests`(필드 매핑·off 게이팅·탐지 독립성 4종) + `EventSetupModelTests`(복사 생성자 1종) 신설. TDD Red→Green. Events.Ui 340 통과/7 실패(전부 기존 기준선=DeviceSymbolLookupModel·DetectionNatsSyncService, 회귀 0) · 한글 BOM.
  - **⚠ 메인 솔루션 잔여([외부 솔루션] EXT)**: `SetupModel`(C# 기본값 true/20)·`appsettings.json`·`EventSetupViewModel`·`EventSetupView.xaml`(`장애 이벤트 해제` 항목 신설) — 사용자 통지 후 진행 예정.
- **탐지 이벤트 DataGrid 썸네일 컬럼** (Track C · Events.Ui · 사용자 지시 2026-07-31)
  - `DetectionEventPanelView` DataGrid에 "썸네일" 컬럼 추가(Device 다음). **Row 높이 이내로 축소**(Grid 36 / Image MaxHeight 34), **마우스 호버 시 원본 크기 툴팁**(MaxWidth 360, InitialShowDelay 300ms). 썸네일 **부재/로드 실패 시 기본 이미지**(`ImageOffOutline`) — Image를 기본 아이콘 위에 겹쳐 로드 실패 시 뒤 레이어 노출(코드비하인드 불요).
  - **공통 헬퍼** `Helpers/ThumbnailUriResolver` 신설 — 썸네일 원본 경로→절대 URI(절대 그대로 / 상대는 API base host 결합, `IApiSetupModel` IoC 조달). `DetectionEventViewModel`(행)·`DetectionSelectionViewModel`(속성 편집기)가 공유(기존 중복 로직 제거).
  - `DetectionEventViewModel`에 `ThumbnailUri`/`HasThumbnail` 추가(신호 컬럼과 동일 패턴). 속성 편집기(SelectionView)는 이미 썸네일 연결됨 → URI 로직만 공통화.
  - **검증**: Events.Ui 빌드 0오류 · 한글 BOM. ⚠앱 재빌드 후 런타임 육안.

### Fixed
- **장애 카드 flip 뒷장 제어기/센서 번호 공란 견고화** (Track C · Events.Ui + Devices.Ui · 태그 `before-malfunction-card-controller-sensor-fix` · [Plan](docs/plans/malfunction-card-controller-sensor-prd-plan.md) · 사용자 지시 2026-07-31)
  - **근본원인**: 장애 전문(`BaseDeviceDto`)은 컨트롤러 표시번호를 싣지 않고 `controller_id`(FK)만 준다 — 제어기 필드는 `Controller.DeviceNumber`(중첩 nav)에 바인딩되므로 Provider 로컬 해석이 유일 소스인데, 클라 변환이 받은 `controller_id`조차 버려 Provider 참조가 온전치 않은 순간(폴백/미하이드레이션) 복구 불가·공란. 로그 실측상 Provider 재연결(`72865ab`)은 정상(`Controller(1351, DeviceNumber=1)`)이나 그 외 경로에 안전망 부재(검증 워크플로 H4 CONFIRMED).
  - **D1(중심)**: `EventCardViewModel.ControllerDeviceNumber` — 중첩 `Controller.DeviceNumber`가 0이면 `Controller.Id`로 `DeviceProvider`에서 컨트롤러 번호 재해석(생성 경로 무관 복구).
  - **D1/D3**: `Events.Ui/DtoToModelHelper.ConvertDeviceFromDto` 폴백(장비 Provider 미스)에서 `controller_id`로 Provider 컨트롤러 연결(없으면 FK id 보존) + `Devices.Ui/DtoToModelHelper.ToSensorDeviceModel`이 중첩 controller 부재 시 `ControllerId` seed(NavigationMapping 재링크).
  - **D2(정본 교정, 커밋 `0548d81`)**: 실측 전문에서 **FAULT_CABLE_CUTTING이 제어기가 아니라 Fence 센서에 실려 옴**(device.type_device="Fence", controller_id=제어기FK) 확인 → 카드 `ControllerDisplay/SensorDisplay`가 **사유(reason)로 장비타입을 단정**하던 오배정을 **장비타입 기준(`Device is ISensorDeviceModel`)**으로 교정: 센서장비=제어기(ControllerDeviceNumber)+센서(자기번호), 제어기장비=제어기(자기번호)+센서(null). 사유 무관. (직전 화이트→블랙리스트 시도는 이걸로 대체 — reason은 장비타입을 나타내지 못함.) **왜 탐지만 정상**: 탐지 카드는 reason 게이트 없이 `ControllerDeviceNumber`/`Device.DeviceNumber` raw 바인딩이라 항상 정상.
  - **detail null**: `detail`이 null이면 first/second 4값은 `ToMalfunctionEventModel`이 이미 `?? 0`로 0,0,0,0 처리(추가 수정 불요, 회귀 테스트로 고정).
  - **테스트**: `MalfunctionCardDisplayTests` 장비타입 기준 재작성(FAULT_CABLE_CUTTING-on-sensor 포함) + Provider 폴백·DTO 변환 링크. Events.Ui 46 통과/0실패 · Devices.Ui 27 통과/0실패 · 빌드 0오류 · 한글 BOM. 커밋 `196fd59`(견고화)+`0548d81`(장비타입 교정). 메인솔루션 무변경 — **앱 반영엔 메인솔루션 재빌드·재배포 필요**.

### Changed
- **조치보고 다이얼로그 "탐지 속성(detail)" 레이아웃 정리 + 썸네일 이미지화** (Track B · Events.Ui · 태그 `before-detection-detail-layout-thumbnail` · 사용자 지시 2026-07-31)
  - **레이아웃**: 10열 불규칙 그리드(값 시작 위치가 행마다 어긋남)를 **좌(속성 라벨:값 세로 정렬) + 우(썸네일 박스)** 2단으로 재작성. Type/Device/Status/Result(편집) + 신호/AI/객체(읽기전용) 정돈.
  - **높이 고정 + 스크롤(사용자 지시)**: 좌측 속성 영역을 `ScrollViewer`(MaxHeight 150)로 감싸 창 Height를 키우지 않고 넘치면 세로 스크롤.
  - **썸네일 이미지화**: URL 텍스트 표시 → 실제 이미지. `DetectionSelectionViewModel.ThumbnailUri`(절대 URL 그대로 / 상대 `/api/thumbnails/…`는 API base host 결합, base는 `IApiSetupModel` IoC 조달) + `HasThumbnail`. 로드 성공=이미지, 부재/실패=default("미리보기 없음" 아이콘) — Image를 default 위에 겹쳐 로드 실패 시 뒤 default 노출(코드비하인드 불요). ⚠서버가 자체서명(mkcert, SAN 없음) https면 이미지 로드 실패로 default가 표시될 수 있음(인증서 정책 별개).
  - EventUiModule에 `IApiSetupModel` 인스턴스 등록(썸네일 base seam).
  - **검증**: Events.Ui 빌드 0오류 · 335 통과/7 실패(전부 기존 기준선=DeviceSymbolLookupModel·DetectionNatsSyncService, stash 대조로 회귀 0 확증) · 한글 BOM. ⚠앱 재빌드 후 런타임 육안.
- **조치보고 다이얼로그 "장애보고 속성(detail)" 레이아웃 정리 — 탐지와 일관화** (Track B · Events.Ui · 와이어프레임 승인 후 적용 · 사용자 지시 2026-07-31)
  - **레이아웃**: 10열 불규칙 그리드(Type/Device/Status 한 줄 + Reason + First/Second 구간 4칸 한 줄)를 탐지 속성과 동일하게 **좌(속성 라벨:값 세로 정렬 Type/Device/Status/Reason) + 우(고장 구간값 카드)** 2단으로 재작성.
  - **높이 고정 + 스크롤**: 좌측 속성 영역을 `ScrollViewer`(MaxHeight 150)로 감싸 창 Height 고정.
  - **구간값 카드**: 탐지의 썸네일 자리를 `FirstStart/FirstEnd/SecondStart/SecondEnd` 2×2 카드로 대체(장애엔 썸네일이 없어 detail 구간값을 시각화). Border=`MaterialDesignChipBackground`+`DividerBrush`, 탐지 썸네일 박스와 동일 톤.
  - 바인딩·VM(`MalfunctionSelectionViewModel`) 무변경(IsEditable 제외) — 뷰 재배치만. 와이어프레임 정본: `docs/design/action-report-dialogs-wireframe.html`.
  - **검증**: Events.Ui 빌드 0오류(경고는 전부 기존 nullable/분석기) · 한글 BOM. ⚠앱 재빌드 후 런타임 육안.
- **조치보고 다이얼로그 Device 필드 읽기전용화 — 인스턴스 매칭 ComboBox 제거** (Track B · Events.Ui · 사용자 지시 2026-07-31)
  - **이유**: 이벤트가 가리키는 장비를 다른 장비로 교체하는 건 데이터 무결성상 부적절 → Device는 편집 불가로.
  - **변경**: `DeviceProvider` 기반 ComboBox(인스턴스 매칭) → 장비 **필수 정보 읽기전용 텍스트**(구역/종류/장비/번호). 값은 테두리 없는 `IsReadOnly` TextBox(복사 허용·편집 차단, `IsTabStop=False`).
  - **VM**: `Detection/MalfunctionSelectionViewModel`에 표시용 파생 프로퍼티 추가 — `DeviceNameText`(DeviceName)·`DeviceTypeText`(DeviceType)·`DeviceNumberText`(DeviceNumber)·`DeviceZoneText`(DeviceGroups Id→`DeviceGroupProvider` 이름 변환, BaseDeviceViewModel 패턴). `RefreshAll`에서 NotifyOfPropertyChange, `ApplyButton`의 `item.Device = …` 적용 제거.
  - **대시보드 편집기 영향**: 동일 뷰를 쓰는 `EventDashboardView`에서도 Device는 읽기전용(IsReadOnly)이나 Type/Status/Result/Reason 등은 편집 유지.
  - 와이어프레임 갱신: `docs/design/action-report-dialogs-wireframe.html`(Device=읽기전용 필드).
  - **폰트 일치**: `ReadOnlyValue`(Device 값) 스타일에 표준 폰트 지정(NotoSansCJKkRMedium / FontSize 15 / Opacity 0.75) — 미지정 시 MaterialDesign 기본(Roboto→한글 시스템 fallback)으로 떨어져 라벨·ComboBox 값과 어긋나던 문제 수정.
  - **검증**: Events.Ui 빌드 0오류. ⚠앱 재빌드 후 런타임 육안.

### Fixed
- **서버 datetime 규약(aware ISO 8601) 정합 — GIS→서버 요청 시각 offset 통일** (Track C · Messages/Api/Events.Ui/Accounts.Ui/Devices.Api · 서버팀 통지 `GOP_Server_API_datetime_unification` · 사용자 지시 2026-07-31)
  - **배경**: 서버가 datetime Option B(저장 UTC / 출력 offset / **입력 aware 권장**)로 통일. 클라가 naive/거짓-Z를 보내면 서버가 ±9h 오해석. 전수 감사(6영역 병렬)로 gap 도출.
  - **P0(데이터 오염)**: `DtoToModelHelper`의 이벤트 생성 `created_at`이 KST 벽시계에 **리터럴 'Z'**를 붙여 거짓 UTC를 만들던 결함(서버 UTC 해석 시 +9h) → 공통 헬퍼 aware(+09:00)로 교정(탐지/장애/연결/조치 4곳). `BaseDto`의 올바른 aware 기본값을 덮어쓰던 문제도 해소.
  - **공통 헬퍼 신설** `KoreaTimeHelper.ToServerIso8601(DateTime / DateTimeOffset / DateTime?)` — Kind별(Utc→+00:00, Local→로컬, Unspecified→KST +09:00) aware ISO 8601 생성.
  - **쿼리 aware**: `EventProviderService`(조회·페이지·통계 11지점 start_date/end_date), `DetectionHistoryDialogViewModel`, `AuditLogPanelViewModel` — naive `ToString("yyyy-MM-ddTHH:mm:ss")` → 헬퍼 aware.
  - **공통 body 직렬화**: `ApiService`가 설정 없이 `SerializeObject`하던 것에 `DateTimeZoneHandling.Local` 부여 → 모든 body DateTime 필드(Grant valid_from/valid_until 등)가 offset 부착.
  - **응답 파서**: `DtoToModelHelper.ParseDateTime`가 `DateTimeOffset.TryParse`(RoundtripKind)로 offset 보존 후 KST 정규화(기존 offset 소실 수정).
  - **URL 인코딩**: `ServerApiService`/`DeviceApiService`의 `before_date` raw 결합 → `Uri.EscapeDataString`(aware '+' → %2B, 손상 방지).
  - **서버팀 확인 완료(2026-07-31 회신)**: `+09:00` aware 전 엔드포인트 수용✓ / **Report generate aware 자정 수용✓** — `[start,end]` 닫힌구간, end 자정을 서버가 `23:59:59.999999`로 확장해 끝일 포함(서버측 확장 로직 회복 커밋 `c349a1d`) → **Report도 aware 자정(+09:00)으로 통일 적용**(`ReportCreateViewModel`) / `collected_at` `+09:00` 201✓ / 응답 datetime **항상 offset 부착**✓ → `DateTimeOffset` 파싱 안전.
  - **잔여(별도·저영향)**: ① 서버 메트릭 조회/삭제 파라미터명 불일치(클라 `start_date/end_date`·`before_date` ↔ 서버 `start_time/end_time`·`older_than_days`) — 현재 **앱 호출자 0**(테스트만), 향후 UI 연동 시 정정. ② `GrantDto` 응답 `DateTime`→`DateTimeOffset`(한국 배포 무해, 타국 offset 대비 권장). ③ `BrokerMessageHelper` 헬퍼 일원화(현재도 aware).
  - **검증**: 솔루션 빌드 0오류. ⚠앱 재빌드 후 런타임 육안.
- **조치보고 다이얼로그 탐지/장애 속성 "스크롤 불가 + 높이 짤림" 버그** (Track B · Events.Ui · 사용자 스크린샷 제보 2026-07-31)
  - **원인 1(스크롤 불가)**: 다이얼로그가 `<ContentControl x:Name="SelectedItemEditor" IsEnabled="false">`로 읽기전용을 걸었는데, WPF의 IsEnabled는 자식으로 **강제 상속**되어 내부 `ScrollViewer`까지 비활성화 → 스크롤바 드래그·휠 모두 죽음.
  - **원인 2(높이 짤림)**: `ScrollViewer MaxHeight="150"`이 과소해 6행(Type/Device/Status/Result/신호/객체) 중 3행만 보이고 나머지가 숨음.
  - **해결**: 읽기전용을 `DetectionSelectionViewModel`/`MalfunctionSelectionViewModel`의 `IsEditable` 플래그로 이관 — 편집 컨트롤(좌측 필드 Grid·장애 구간값 카드·적용 버튼)만 `IsEnabled="{Binding IsEditable}"`로 묶고 **ScrollViewer는 항상 활성**(읽기 모드에서도 스크롤 가능). 다이얼로그 `ContentControl`의 `IsEnabled="false"` 제거 후 VM 생성 시 `{ IsEditable = false }` 주입. MaxHeight 상향(탐지 280 / 장애 220)으로 평소엔 스크롤 없이 전 행 표시, 넘칠 때만 스크롤.
  - **재사용 보존**: 동일 SelectionView를 쓰는 `EventDashboardView` 편집기는 `IsEditable` 기본 true라 편집 유지(회귀 없음).
  - 변경: `Detection/MalfunctionSelectionView.xaml`, `Detection/MalfunctionSelectionViewModel.cs`, `Detection/MalfunctionReportDialogView(Model).cs`.
  - **검증**: Events.Ui 빌드 0오류. ⚠앱 재빌드 후 런타임 육안(스크린샷은 수정 전 빌드).

## [2.9.0] - 2026-07-31

### Fixed
- **로그인 실패 시 raw 예외 문구("Cannot access child value on Newtonsoft.Json.Linq.JValue") 노출 버그** (Track B · 태그 `before-login-details-jvalue-fix` · Accounts.Api+Accounts.Ui · 사용자 제보 2026-07-31)
  - **원인**: `ApiAccountGateway`가 v6.3 잠금정책 안내를 위해 `error.details`(JToken)에서 `d?["failed_count"]` 등을 인덱싱하는데, `?.`는 C# null만 걸러 **`JValue`(details:null=`JValue(Null)` / 연결실패 로컬에러=`JValue(string)`)** 에는 `JValue["key"]`가 예외를 던짐. 원격 서버(123.141.236.253:8136) 연결 실패 시 이 경로가 크래시하며 `LoginPanelViewModel`의 catch가 `ex.Message`를 그대로 라벨에 노출 → 준비돼 있던 "서버에 연결할 수 없습니다" 문구를 가로챔.
  - **수정**: ① `DetailsToken as JObject` — 객체일 때만 잠금정책 파싱, 문자열/null details는 무시(generic 실패로 정상 전개) ② `LoginPanelViewModel`에 `LoginFailureException` 도입 — 의도된 실패(FailMessage 문구)와 예기치 못한 예외를 분리, 후자는 raw 문구 대신 일반 안내 + 스택 포함 ERROR 로그(SEC-5 준수).
  - **검증**: 빌드 0오류 · Accounts.Api 139/139 green(details 문자열JValue/JSON null/객체 회귀 3종 추가). ⚠앱 재빌드 후 육안(서버 연결 끊고 로그인→"서버에 연결할 수 없습니다").

### Changed
- **사이트 고정(앵커) 중 "정북 복귀" 비활성화** (Track B · GMaps.Ui 한정 · 사용자 결정 2026-07-30)
  - 앵커 활성(`IsAnchorActive`) 중에는 화면 각을 앵커가 소유하므로 수동 정북 개입을 차단: ①나침반 **더블클릭 정북** 무시 ②나침반 우클릭 메뉴 **"정북 복귀" 항목 비활성**(+"앵커 해제 후 사용" 안내 툴팁, 클릭 이중 방어) ③미바인딩 `ResetRotationCommand`의 CanExecute도 동일 게이트(향후 배선 대비).
  - SSOT 게이트의 "0(정북)은 항상 허용"은 **유지** — 앵커 자신의 정북 강제·kill-switch OFF 복구가 쓰는 안전 경로라 UI 트리거만 차단.
  - 검증: 빌드 0오류 · GMaps.Ui 370개 중 369 green(실패 1=EditRecorder 기존 기준선, 나침반 테스트 32종 포함 회귀 0). ⚠앱 재빌드 후 런타임 육안(앵커 ON→나침반 더블클릭 무반응·메뉴 항목 회색).

### Fixed
- **풍량모드(WINDY) 실패 시 라디오 미복원 + 프록시 서버 하드코딩 + 조준 실패 팝업 승격(GOP_Nats_Req_Failure_UX)** ([PRD](docs/prds/GOP_Nats_Req_Failure_UX-prd.md) · [Plan](docs/plans/GOP_Nats_Req_Failure_UX-prd-plan.md) · 태그 `before-windy-rollback-fix` · 라이브러리 2파일+메인솔루션 3파일)
  - **원인(전부 실측)**: ① proxy-settings 조회가 `serverId=1` 하드코딩(스펙 §8.8 예시 복사) — 실제 servers 테이블에 id=1 없음 → 404 ② 재조회 실패 시 롤백 폴백 없음 + 클릭 즉시 이전 모드 소실 ③ 서버 v6 에러 봉투(`error.message`)를 안 읽어 실패 사유가 빈 문자열.
  - **FR-1 프록시 서버 해석기**: 카테고리 목록에서 `type_server=PROXY`를 찾아 `ServerProvider`(로그인 후 전 서버 캐시)에서 해당 서버 id를 해석(사용자 지정 흐름). 캐시 + `SYNC_SERVER`/`SYNC_CATEGORY` 수신 시 무효화(`OnServerTopologyChanged` 훅 신설) + 404 자가치유. 미등록 시 "프록시 서버가 서버 관리에 등록되어 있지 않습니다" 사유 표면화.
  - **FR-2 로컬 롤백 폴백**: `SendWindyModeMessage`에 PrevMode 추가(하위호환) — REQ 실패+REST 재조회까지 실패한 이중 장애 시 클릭 전 모드로 라디오 복원(팝업의 "되돌립니다" 약속 보장).
  - **FR-3/FR-4**: 실패 사유에 `Error.Message` 사용(빈 사유 금지) · 부팅 무토큰 proxy-settings 조회를 로그인 후(`AllDevicesLoadedMessage`)로 게이팅 — 401+BearerAuthHandler 세션만료 오발화 제거.
  - **FR-5 조준 실패 팝업 승격(사용자 확정)**: 특정위치확인(PTZ_AIM_LOCATION) 실패 시 2.5초 배너 대신 WINDY와 동일한 표준 팝업으로 통지, 성공은 배너 유지.
  - **데이터 선행**: 테스트 서버에 PROXY 카테고리(id=11)+프록시 서버(id=17, PROXY-ab0101) 등록·lazy 생성 검증 완료. ⚠**현장 배포 시에도 type_server=PROXY 서버 등록 필요**(배포 체크리스트).
  - **검증**: 빌드 0오류(양 레포) · 메인 테스트 53개 중 45 green(8 skip 기존)+신규 해석기 테스트 5종 · GMaps.Ui 337/338(기준선 1 제외 green, 회귀 0). ⚠런타임 육안(풍량 실패→사유 팝업+라디오 복원 / 조준 실패→팝업)은 앱 재빌드 후.

### Added
- **제어기 무통신 그룹 검은색 전파(GMap_Controller_Blackout)** ([PRD](docs/prds/GMap_Controller_Blackout-prd.md) · [분석·시뮬](docs/analyses/Controller_Blackout_Propagation-analysis.md) · v2.6 `bff9b13`)
  - 제어기 장애(MALFUNCTION `reason=FAULT_CONTROLLER`) → 연결 센서가 먹통 → 그 센서 그룹의 PidsGroupSymbol을 **검은색(Blackout, 최상위 우선)**. 해소 시 남은 센서 장애 그룹은 **주황 복귀**.
  - `EnumCompositeEventStatus.Blackout` + 순수 `ControllerBlackoutModel`(우선순위 `Blackout>FaultedDetecting>Faulted>Detecting>Normal` + 제어기→센서 그룹 확장). `EventEntry.IsControllerBlackout`, EQM 상태계산 단일 우선순위 지점.
  - `MalfunctionNatsSyncService`: `DeviceProvider` 주입, `reason` 파싱, 연결센서 그룹 합집합 확장. SEM/모델/PidsGroup·Pids 마커 검은색 트리거.
  - **시뮬레이션(사용자 요구): 101 시나리오 ×2회**(시뮬 vs 독립 오라클 매 스텝 대조), 330 스텝·734 색상대조 실패 0·결정성. 적대 리뷰 wf(13에이전트) CONFIRMED 2건 수정(①DeviceProvider 수동팩토리 미전달=기능 죽음 ②제어기 자기 마커 주황→초록 회귀). 빌드 0오류·Events.Ui 59통과. ⚠런타임 육안=사용자.
- **지도 계기 인디케이터 2종(강풍·탐지장애) + 보기(View) 메뉴(GMap_Map_Instruments)** ([PRD](docs/prds/GMap_Map_Instruments-prd.md) · [Plan](docs/plans/GMap_Map_Instruments-prd-plan.md) · [강풍 WF](docs/design/GMap_Windy_Indicator-wireframe.html) · [탐지장애 WF](docs/design/GMap_Detection_Fault_Indicator-wireframe.html) · 태그 `before-map-instruments` · v2.6 `f1ee163`+메인솔루션 `ad17c73`)
  - **GMapWindyIndicatorControl**: WINDY 4모드(wind0 보통/wind1 약풍/wind2 강풍/wind3 태풍) 아이콘·색 전환(중립→Info→Warning→Critical, 태풍 회전+펄스)·아이콘+라벨/아이콘만·평상시숨김. 소스=`ChangeModeWindyMessageModel` 구독. 클릭→WINDY 패널(`OpenWindyPanelMessageModel`).
  - **GMapDetectionFaultControl**: 활성(미조치) 탐지/장애 집계 pill 2개(Critical/Warning)·탐지 펄스·세로/가로·0건숨김. **소스=`EventQueueManager`(활성 SSOT, 카드목록 아님 — 500캡 desync 회피)**. 클릭→이벤트 패널(`OpenEventPanelMessageModel`).
  - **EQM 확장**: `GetActiveCounts()`+`OnActiveCountChanged` 이벤트(Enqueue/Dequeue/DequeueAll 전 경로 발화, 동일상태 dequeue 포함, lock 밖 발화, Dispose 정리) — 전이 재집계.
  - **보기(View) 메뉴**: `_Maps` 좌표계 토글과 동일 `IsCheckable`+`IsChecked` 패턴으로 나침반·강풍·탐지장애·중앙십자선 토글, 체크상태 영속.
  - **나침반 FR-17**: 가시성 마스터를 보기 메뉴로 이관(회전 기능→가시성 커플링 제거, 회전 OFF여도 정북 계기 표시). 회전 기능은 회전 '입력'만 게이트.
  - **영속**: `MapWindyIndicator/MapDetectionFault/MapInstrumentVisibility` 모델, 복원 중 저장 억제. 표시클램프≠영속좌표 분리+마진≥16 도킹(HUD 회피).
  - **검증**: 적대 리뷰 워크플로(12에이전트) CONFIRMED 1건(나침반 doc 주석 stale) 수정, 나머지 반박기각(EQM 락경계·NATS→UI 정렬 클린 확정) · InstrumentMath 헤드리스+회전/나침반 93/93 · 양 레포 컴파일 0오류. ⚠런타임 육안=앱 재빌드 후.
- **방위각 심볼(나침반) CustomControl(GMap_Compass_Control)** ([PRD](docs/prds/GMap_Compass_Control-prd.md) · [Plan](docs/plans/GMap_Compass_Control-prd-plan.md) · [와이어프레임 v2.2](docs/design/GMap_Compass_Control-wireframe.html) · 태그 `before-compass-control` · v2.6 `731d8d4`+메인솔루션 `0cfe61e`)
  - **GMapCompassControl**(templated, Generic.xaml): 고정 베젤/러버라인(Accent)+회전 로즈(−Bearing), 변형 A로즈/B링, S64/M96/L128, 눈금·기수문자 코드생성(SetResourceReference=라이브 테마), 리드아웃 canonical(`+035.0°`), 전 색상 DynamicResource 토큰.
  - **싱크/게이트**: ViewportSnapshotPublisher 구독(Loaded/Unloaded 대칭) · RotationFeature OFF=Collapsed(비회전 회귀 0).
  - **조작**: 본체 드래그 이동(8px 데드존+캔버스 클램프)·외곽 링 드래그=지도 회전(SSOT 게이트 경유, 앵커 잠금 시 무시)·더블클릭 정북(Up 시점 승격)·우클릭 설정 메뉴(크기/스타일/각도표시/정북).
  - **영속**: `MapCompassModel {X,Y,Size,Variant,ShowReadout}` → AppSettings.MapCompass(복원 중 저장 억제 — 회전 영속 44bc6fc 교훈). 표시 클램프≠영속 좌표 분리(작은 창 부팅 시 저장 좌표 보존)+캔버스 리사이즈 재클램프.
  - **레이어**: PropertyPanelCanvas 자식 z6 계기층(리더선 위·팝업/패널/허브 아래).
  - **검증**: 적대 리뷰 워크플로(14에이전트) CONFIRMED 8건 전부 수정(캡처 유실 리셋·링 데드존·더블클릭 편승·좌표 소급훼손·투명 모서리 히트 등) · CompassMathTests 29케이스 · Compass/Rotation/Snapshot 72/72 · 양 레포 컴파일 0오류. ⚠런타임 육안=앱 재빌드 후(현재 앱 실행 중이라 출력 잠김).
- **GIS NATS v1.5.2 REQ 전환 + 경광등 제어 서비스 신설(GIS_Nats_v152_Req_Transition)** ([PRD](docs/prds/GIS_Nats_v152_Req_Transition-prd.md) · [Plan](docs/plans/GIS_Nats_v152_Req_Transition-prd-plan.md) · [분석 v2](docs/analyses/GIS_Nats_Spec_Gap-analysis.md) · 태그 `before-gis-nats-req-transition` · Enums+Messages+GMaps.Ui+메인솔루션 1파일)
  - **공통 REQ 실행기(FR-01)**: 신규 `BrokerRequestClient`/`BrokerRequestResult` — m_type=REQ 봉투 발행→RSP 대기(기본 **5초**)→3분기(무응답/서버거부/성공), §6.9 서버 message 패턴→한글 안내문 매핑. Broadcast/Aim/Lamp 3개 서비스 공유.
  - **REQ 전환(FR-03~05)**: `BROADCAST_PLAY`/`BROADCAST_STOP`(스피커 심볼 메뉴)·`PTZ_AIM_LOCATION`(지도 클릭 조준)을 PUB fire-and-forget→**REQ+RSP 확인**으로 전환(v1.5.2 §6.4 통일 규칙). 실패 시 표준 팝업/조준 상태문구로 가시 통지 — 음원 실행은 성공 시에만 패널 닫힘, TTS는 비스펙 cmd라 PUB 유지(OQ-1, 서버 RSP 확인 대기).
  - **경광등 제어 계층 신설(FR-06/07)**: `ILampControlService`/`LampControlService` — LAMP_CLEAR(생략=전체)/LAMP_OFF/LAMP_COLOR_SET/LAMP_BUZZER_SET 4종 REQ 발행(UI는 후속 — 심볼 메뉴 생기면 배선). `EnumLightMode`/`EnumBuzzerSound`/`EnumLampColor`에 EnumMember 와이어 값 고정("steady"/"blinking", "Fire A-WANG"/"PI-PI-PI"/"PI_continue"), Lamp DTO enum 타입 강제.
  - **cmd 정비+로그 위생(FR-02/08)**: `EnumGopCommand`에 BROADCAST 2종·LAMP 4종·TRACKING_STATUS 추가(기존 정수값 불변), PTZ_AIM_LOCATION "PUB" 스테일 주석 정정. 메인솔루션 `NatsBrokerService`에 TRACKING_STATUS+SYNC 무시 5종 **명시 no-op case** — 추적 활성 시 1Hz "Unknown" 경고 2건 로그 스팸 제거.
  - **검증**: 빌드 0오류(Enums/Messages/GMaps.Ui/메인솔루션) · 테스트 Messages 187/187 · GMaps.Ui 336/337(빨강 1=EditRecorder 기존 기준선, 회귀 0) · 신규/갱신 테스트 37개 · 한글 소스 UTF-8 BOM. ⚠**서버측 RSP 회신 여부(V-01~04) 미확인** — BroadcastingManager/NVRManager/PidsProxy가 REQ에 응답하지 않으면 5초 대기 후 "대상 서비스 응답 없음" 안내(WINDY 프록시 이력과 동일 계열), 서버팀 확인 필요. ⚠앱 재빌드 후 런타임 검증.
- **지도 회전 전면 싱크(GMap_Rotation_Full_Sync) — kill-switch 게이트 하 재활성 준비 완료** ([PRD v2.0](docs/prds/GMap_Rotation_Full_Sync-prd.md) · [Plan](docs/plans/GMap_Rotation_Full_Sync-prd-plan.md) · [버그 인벤토리 v2.0](docs/analyses/GMap_Rotation_Enable_BugInventory_v2-analysis.md) · [스파이크](docs/analyses/GMap_Rotation_P0_Spikes_V02_V08-analysis.md) · 롤백 5중: 태그 `before-rotation-fullsync`+백업브랜치+번들+worktree+kill-switch · GMaps.Ui+**GMap.NET 벤더**(V-01 승인))
  - **P0 치명·상태**: 회전 SSOT(canonical [-180,180) 단일 적용경로·DP 교정으로 소비처 자동 정규화 R-09)·`RotationFeature` kill-switch(기본 OFF, 정북 복귀는 항상 허용)·앵커 원자 게이팅(정북 강제→verify→활성 V-06)·이미지 클릭 크래시 가드(R-01)·SelectedArea 음수 Rect 가드·휠줌 이중 역행렬 제거(R-02)·회전 투영 절삭→Round(R-38). **V-02 실측**: 회전 ViewArea `(int)NaN`=int.MinValue(쓰레기 rect 확정).
  - **P1 동기 계약**: `MapViewportSnapshot`+`ViewportSnapshotPublisher`(per-consumer 예외격리·구독 즉시 replay·중복방지·revision·프레임당 1회 coalescing) + **12소비처 배선**(라벨·측정·조준·라인드로잉·그룹선택·FOV·Line/PidsGroup·트레일·재생·**카메라 팝업/leader(F-02 누락분)**·오버레이맵·맵전환 Resync) — R-12~R-21 stale 전면 해소.
  - **P2 심볼·복합**: 표시각↔모델각 분리(`DisplayAngle` 파생, write-back 0 R-35)+render/hit parity(F-05)+named persistent transforms(클릭펄스 충돌 R-40)+PIDS FOV 정확-1회(θ-free 지오메트리, F-07/R-22)+apex pivot+트래킹 화살표 WorldHeading(R-10).
  - **P3 기하·래스터**: **V-08 pixel-diff 스파이크**(7각도×4DPI)로 오버레이 접근법 확정 — 후보B(비절단 core좌표+회전 1회 Push)=전조건 0-diff, v1 설계안(절단좌표 역회전)은 seam 3~160px로 **실측 폐기**. 벤더 seam 2건(`FromLatLngToCoreLocal`·`RotationMatrixValue`)+ViewArea 4코너 원천교체(R-03)+이미지 "회전불변 중심 rect=ProjectedQuad 동치"(F-01·R-01/24/25/27, DB이미지 R-06/37 동시)+라벨 회전불변 익스텐트(R-23)+앵커그리기 정규화(R-28).
  - **P4+P5**: 중복 O(N) 제거(F-12)·Bearing 이탈 시 크기 재통지(R-43)·Offset 벡터변환(R-42)·스냅 데드존(R-33) + **입력 재개방**(Shift+휠·Ctrl+←/→)을 kill-switch 게이트로 복원 — **OFF(기본)면 af0f29d와 동작 완전 동일(머지 무영향)**.
  - **검증**: 전 단계 빌드 0오류 · 헤드리스 테스트 **77/77**(회전수학 31·발행기 6·측정 16·기타) · 단계별 태그 `rotation-p0-done`~`p5-done`. ⚠**런타임 E2E 및 flag ON 결정은 사용자 육안 검증 후**(PRD §9 매트릭스·NFR-02 정량 ≤1px/±0.1°).

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
  - **Phase 2 완료(테스트 11/11)**: 상태 VM `AccountViewModel`/`LoginViewModel`/`RegisterViewModel` 이관 + **C-1 수정**(RegisterViewModel `IoC.Get` 제거→전용 `AccountModel` 주입) + 모듈 등록 활성화 + C-1 회귀 테스트.
  - **Phase 3a 완료(테스트 14/14)**: leaf 다이얼로그 `DeleteAccountDialogViewModel`(2중 루프 버그→`AccountDeletionPolicy`)·`ResetPassDialogViewModel`(선적용 버그→gateway 롤백) gateway 주입 이관 + async void→Task + ct. 메시지 17종 전부 라이브러리 확인(hook 불필요).
  - **Phase 3b 완료(다이얼로그 4종 전부)**: `RegisterDialogViewModel`(H-4 중복확인 debounce, 파일위임 `IProfileImageService`, SetupModel(dead) 제거)·`EditorDialogViewModel`(하드코딩 `"12345678"`→`ISessionConfigService.AdminResetPassword`) gateway 주입 이관. `IUserDirectoryGateway.UpdateAccountAsync` 추가.
  - **Phase 3 완료(패널 4종 = VM 11종 전부 라이브러리)**: `LoginPanelViewModel`(`IAuthGateway`, **🔒 L115 토큰 평문 로깅 제거**, TokenGenerator 직접의존 제거)·`AccountManagerPanelViewModel`(`IUserDirectoryGateway`, NotImplemented→no-op, 취소토큰 버그 수정)·`MyPagePanelViewModel`(`IProfileGateway`, 사진 위임)·`AccountSetupPanelViewModel`(`ISessionConfigService`). 상태 VM 3 + 다이얼로그 4 + 패널 4 전부 gateway seam으로 라이브러리 완결.
  - **Phase 4 완료(View 7종, 자립 라이브러리)**: Login/AccountManager/MyPage 패널 + Register/Editor/Delete/ResetPass 다이얼로그 XAML 이관(네임스페이스 치환, 앱 전용 컨버터 참조 0). 라이브러리 = VM 11 + View 7 완결, XAML 컴파일 0. (소비앱 통합·v2.6 머지 = 다음 단계)
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

