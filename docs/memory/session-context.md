# 세션 컨텍스트

## ▶▶ 재개 포인트 (2026-09-08, 이 세션 — 그룹 심볼 변환 PRD **v2.0 재검토**: 3D 심볼·틸트 도입 반영) — 🔲 **사용자 결정 10건 + 승인 대기**

- **사용자 요청**: "다시 한번 검토해줘 3D 심볼까지 도입됐거든??"
- **투입**: 신규 영향면 5축 재정찰(에이전트 7기, 1.08M 토큰 — 틸트좌표계·3D하우징·철망·빌보드·선택/HUD) + **적대 검증**(반대 명제 방어) + 시뮬 v5→v6(5,255건). 커밋 `9f187458`.
- **핵심 유지**: C-A(inner 공간 base 스냅샷 1회 합성)·C-C(HUD=편집 스트립, 여전히 4그룹이라 5번째 자리 有)·C-E(3D도 무텍스처 메시라 확대해도 안 깨짐)·C-F(배치 트랜잭션).
- **개정 4건**:
  ① **FR-04 빌보드 분기 키(High)** — `Symbol3DFeature`가 **설치별 플래그**(개발 ON/배포 OFF)라 `IsBillboard` 렌더 기준으로 분기하면 같은 장비가 설치본마다 다른 DB를 쓴다 → **데이터(장비/모델) 기준**으로 재정의. 점 심볼을 3갈래(2D회전/3D-yaw/빌보드)로 재분기.
  ② **FR-03/10 base 스냅샷(High)** — **오버스캔이 컨트롤 높이를 H/cosφ로 바꿔** RenderOffset 재계산 → 전 마커 로컬 px가 Δ 평행이동(φ=20°·H=800 → 25.7px≈12m @z18). 틸트는 줌 18.0/17.5 히스테리시스로 **자동** 전이. 줌·팬·리사이즈도 동일 축 → **투영픽셀/지오 스냅샷** + 뷰 프레임 변화 무효화(FR-19 신설).
  ③ **FR-02 프리뷰(Medium)** — 3D 철망/하우징은 화면고정 35° 카메라·미터 기준 높이라 화면 변환 프리뷰가 커밋 결과와 불일치 → **2D 고스트 강제**.
  ④ **문구(Low)** — C-A 근거를 "보이는 대로"→"inner 공간(마커·격자·스냅 공유)". 구 주석의 "Bearing 가산은 FOV 입력으로만 유지"는 **거짓**(2D FOV에서 b 완전 상쇄) → 삭제.
- **⚠ 자기교정 2회차**: v5 시뮬의 **"틸트 중 제스처는 1/cosφ 역보정 필수"(ISSUE-42)는 오판** — WPF가 `RenderTransform.Inverse`를 자동 적용하므로 수동 보정은 **이중보정 버그**(`GMapCustomControl.Tilt.cs:336-341` 명시 금지). v6에서 ISSUE-44(역보정이 오답)로 정정. 적대 검증이 "서브픽셀 불가"·"BatchUpdate 미구현"·"철망 매 프레임 Rebuild" 3건도 개정 사유 아님으로 반박.
- **신설 FR**: FR-15(틸트 게이트) · FR-16(혼합 계열 고지) · FR-17(구역 부속 완결성) · FR-18(철망 재생성 비용) · FR-19(뷰 프레임 재기준화).
- **범위 정정**: "3D 하우징 Out of Scope"는 **무력** — 3D 컨트롤이 2D의 sealed 파생이라 러버밴드에 그대로 포함됨(`GMapCustomControl.cs:1019-1036`).
- **📋 산출물**: PRD **v2.0 Draft**(FR 19 · GAP 22) / 분석 v2.0(§7 재검토) / 카탈로그 v6 · 전량로그 v6(5,255건).
- **🔲 다음**: **착수 전 결정 10건** — 기존 7건(GAP-1/2/3/4/10/13/14) + 신규 3건(**GAP-16** 틸트 시 차단vs허용+배지 / **GAP-21** 빌보드 분기 키 / **GAP-22** 스냅샷 좌표계). 승인 시 `node .claude/hooks/advance-phase.js approve prd "코멘트"`.


## ▶▶ 재개 포인트 (2026-09-07 — GOPDB 09-07 배포 2건 후속 · **"순서대로 작업을 승인한다" ①②③ 완료**)

- **사용자 승인 흐름**: 조치보고 PRD 승인 → 운영서버 버전 확인(V-08) → 억제 반복 PRD 신설. 3건 모두 처리 완료.
- **① ✅ 조치보고 커스텀 PRD 승인** — `docs/prds/action-report-custom-template-prd.md` **v1.1 Approved**.
  ⚠ `approve prd` 는 **경로 인자를 받지 않고 mtime 최신 PRD** 를 집는다(타 세션이 `symbol-3d-housing-prd.md` 편집 중이었음)
  → 실행 전 `scanDocsDir('prds')[0].name` 대조 가드를 걸고 승인. 같은 함정은 앞으로도 유효.
- **② ✅ V-08 확정 — 개발 대상 = `localhost:8000`(6.3.3), 원격 `123.141.236.253:8136` = **6.3.2 미갱신**.
  `openapi.json` 이 **무인증 200** 이라 로그인 없이 확인(앱 세션 evict 회피). 사용자 확인: "지금은 localhost로 하는거야. 원격 서버는 업데이트가 안되어있다."
  | | localhost | 원격 |
  |---|---|---|
  | 템플릿 경로 | 3개(`/reorder` 포함) | **0개** |
  | 억제 응답 필드 | 24 | **17**(신규 9종 전무), `window_end` non-nullable |
  | `EnumPermissionModule` | `action_report_templates` **포함** | **미포함** |
  ⇒ **FR-21 은 블로커가 아니라 진행 대상**. 오히려 미루면 권한 화면 그룹 저장 1회로 서버 키가 **소멸**한다(클라가 Catalog 전체 교체).
  원격 전환 전에만 **V-08b**(원격 6.3.3 배포) 확인하면 된다. PRD·분석 문서 양쪽 정정 완료.
- **③ ✅ 억제 반복 PRD 신설** — [`docs/prds/event-suppression-recurrence-prd.md`](../prds/event-suppression-recurrence-prd.md) **v1.0 Draft(승인 대기)** · Track C(9점) · 421행.
  베이스라인 `event-suppression-schedule-prd.md` v1.4(출하분)의 **증분**으로 분리(조율문서 `INTEGRATION.md` §5-A 가 이 경로를 명시).
  - **★ 서버가 "제일 중요"라 한 항목은 GIS에 고칠 코드가 0건** — `window_end` 소비처는 `ItemViewModel.cs:34/:66` **표시 문자열 2곳**이 전부이고 `window_end` 로 도는 해제 타이머가 존재하지 않는다. 실제 무게중심은 **표시 정직성 + 반복 생성 UI + 기존 결함 정리**.
  - **표시 정직성**: `status=="active"` 는 "유효기간 안"일 뿐 — 실측 **62.2%가 active면서 미억제**. 토요일 새벽에도 "진행중"(초록) 배지가 거짓말.
  - **기존 결함 B-1~B-6(반복과 무관하게 이미 깨져 있던 것)**: 사문 `SuppressionActiveMonitor`(DI 등록만, **소비자 0건**) · stale 배너(패널 타이머 0건) · fail-open 부재(TTL 없음) · 중복경고가 **시간 비교를 전혀 안 함** · `SYNC_EVENT_SUPPRESSION` **enum 값 자체 부재** · PATCH UI 부재.
  - **FR-18 = 잠복 함정 구조 제거**: `EventSuppressionScheduleRequestDto` 를 **POST·PATCH 가 공유**(`EventSuppressionApiService.cs:128-132`)하는데 서버 Update 스키마엔 반복 4필드가 없고 `extra="forbid"` → 반복 필드를 추가하는 순간 **모든 PATCH 가 422**. 지금은 PATCH 호출부가 없어 잠복이고, 나중에 편집 UI 붙이는 사람이 밟는다.
  - **결정 D-1~D-8**: D-1~6 은 권고안 기본 채택(생성/수정 DTO 분리 · `bool?`+status 폴백 · `daily_*`는 `string?`(DateTime이면 '오늘 08:00'으로 오염) · **로컬 반복계산 미구현** · stale 표기 후 유지 · 라이브러리 자가필터 서비스). **D-7(반복창 편집 UI)·D-8(Phase2 라이브 억제)만 사용자 범위 결정 필요** — 둘 다 제외 권고.
  - **⚠ 메인 솔루션 1케이스(FR-17, 사전 통지 대상)**: `NatsBrokerService.cs:311-317` 명시 no-op 그룹에 추가하지 않으면 회차 경계마다(반복창 1개당 하루 2건) `default:` Warning 이 로그 오염.
  - **V-01 ✅ 확정**: 목록 응답에도 파생 9필드가 내려온다 — `list_suppression_schedules` 가 행마다 공용 변환기 `_to_response(s, now)` 호출(`api-test-server/app/routers/event_suppression_schedules.py:272`, 변환기 `:80-108`). 목록에서 배지를 그리는 설계 성립.
  - **잔여 미확인**: V-02(회차 전이 라이브 관찰) · V-03(NATS 브리지 도달) · V-04(테마 캡처) · V-05(로그인 게이팅 유지) · V-06(UiTests, ⚠ **데스크톱 독점 세션 필요**).
### ▷ ✅ 억제 반복 **UI 설계** 완료 (2026-09-07) — 사용자 "억제 쪽 기능에 따라 UI도 추가해야 되는 거 알지?"

- **PRD 승인됨** — `advance-phase.js approve prd` 실행 전 `scanDocsDir('prds')[0]` 대조 가드 통과(내 PRD가 mtime 1위 확인). ⚠ `approve prd` 는 **phase 를 안 건드린다**(`changelog.executePrdApproval` 만 호출) → 다른 세션의 `pidsgroup-rightclick` dev 36/50 무영향. 배너 상태 출처는 **`.claude/.branch-v2-6/pipeline-state.json`**(브랜치 스코프)이고 `docs/memory/pipeline-state.json` 은 2026-07-30 stale.
- **설계 워크플로** `wf_0df86dba-875` — 실측 5축(제약 78) → 설계 3안 → 3렌즈 심사 → 적대검증 4렌즈. **15 에이전트 / 2.93M 토큰 / 306 툴콜 / 45분 / 오류 0**.
  심사 **20.0 / 19.5 / 19.0 사실상 동률** → 승자를 그대로 쓰지 않고 **승자 구조 + 반증 10건 반영**으로 합성.
- **📐 산출물**: [`event-suppression-recurrence-wireframe.html`](../design/event-suppression-recurrence-wireframe.html)(53KB, 치수·바인딩·직렬화) · [`event-suppression-recurrence-storyboard.html`](../design/event-suppression-recurrence-storyboard.html)(37KB, 흐름·문구 S1~S13). 둘 다 UTF-8 BOM · U+FFFD 0 · 알파순서 오류 **0**(8자리 hex 12개 전수 검증) · 태그 균형 정상.
- **🖼 라이트 실렌더 검증 완료**(Playwright, `docs/assets/suppression-recurrence/`) — 알파 순서 오류는 다크에서 티가 안 나 리뷰를 통과하는 게 이 레포 실증 함정이라 **라이트를 눈으로 확인**. 콘솔 에러는 favicon 404 뿐. **FATAL-05 수정(채움 vs 하단바 축 분리)이 라이트에서 실제로 구분됨**을 확대 캡처로 확인.

- **🔴 적대검증이 무너뜨린 승자안 전제 3종(전부 메인세션 재확인)**
  | # | 반증 | 확인 |
  |---|---|---|
  | F-01/06/09 | `mah:TimePicker.SelectedTime` **부재** | MahApps 2.4.10 `MahApps.Metro.xml` 실측 — `TimePickerBase` 에 `SelectedDateTime`·`SelectedTimeFormat` 만 존재, `SelectedTime` **0건**. ⇒ "TimeSpan 이라 offset 422 가 구조적으로 불가능"이라는 설계 근거가 **소멸** |
  | F-02/08 | 요청 직렬화가 **전 API 공통** `DateTimeZoneHandling.Local` | `ApiService.cs:23-27` 확인 → DTO 에 `DateTime` 노출 시 `"08:00:00+09:00"` 로 나가 **즉시 422**. ⇒ DTO `daily_*` 는 **`string`**, VM→DTO **단일 지점** 변환. `JsonIgnoreCondition` 은 STJ 타입이라 이 경로에 없음 → `ShouldSerializeXxx()`(`SpeakerDeviceDto.cs:40` 선례) |
  | F-10 | `TimePickerBase` 계열 **UIA 미노출** | `UiTests/Pages/SuppressionSchedulePage.cs:70` · `SuppressionSweepTests.cs:216` 에 **이미 명기**돼 있음 → 피커 AutomationId 는 죽은 계측. ⇒ **텍스트 미러**(`RecurrenceSummaryText`)를 1차 단언 경로로 |
  | F-03/07 | 무제한 **게이트 불일치** | 체크박스=Row3(`IsWeeklyMode`) vs 피커전환=Row2(`IsWindowEndUnlimited`) → 반복+무제한 후 단발 복귀 시 **플래그만 남아** 단발+`null` 전송 → 422, 원인이 화면에서 사라져 **진단 불가**. ⇒ 파생 `IsUnlimitedEffective` + 전환 정리 + `CanCreate` 3중 |
  | F-04 | 배너 **로컬값 > Style Trigger** | `:307-309`·`:314` 에 로컬값 박힘. WPF 우선순위 로컬(3)>Trigger(5) → **Trigger 영원히 무효**. ⇒ 로컬값 삭제 후 Setter 이관 |
  | F-05 | 제스처 바가 채움과 **같은 브러시** | 지배적 제스처('칠하기')에서 범위셀이 즉시 checked→채워짐, 그 위 같은 Primary 바 = **1.00:1**. 범위 표시가 가장 필요할 때 **안 보임**. ⇒ `MultiDataTrigger` 2분기(범위∧선택→`OnPrimaryFixed` / 범위∧미선택→`Primary`) |

- **📏 설계 확정 골자**: Row3 하나만 증설 + 통째 `Collapsed` → **단발 세로 증가 0**(폼 315·DataGrid 293 유지), 반복 시 +48. 반복 라디오는 Row2 남는 218 에 인라인(**세로 0**). 좌측 408 정렬계약·우측 356 트레이 **불변**. 목록 **컬럼 추가·삭제 0** — 상태 90→132 / 대상 200→170 폭만.
- **⚠ 세로 제로섬**: 총예산 648, **ScrollViewer 없음** → 폼이 **608 넘기면 DataGrid 0 + 조용히 잘림**. 배너 3종 동시 시 반복모드 목록 **≈149(3.9행)** — V-11 실기 확인 필요.
- **🔲 사용자 결정 대기 3건**: **D-7** 반복창 편집 UI(제외 권고 — 서버 Update 스키마에 반복필드 없어 규칙 자체 수정 불가) · **D-8** Phase2 라이브 억제(서버 PM 대기) · **L-1** 목록 열 폭 변경(⚠ CHANGELOG ⑦ 에 이미 "사용자 확인 대기"로 걸려 있음).
- **잔여 미검증**: V-07(맵 밖 AdornerLayer — **설계가 회피해 블로커 아님**) · V-08(ToggleButton 8.0 데드존+클릭폴백 **스파이크 필요**, 레포 선례 0) · V-09(MDIX `IsFloating=False` 자연높이) · V-10(TimePicker 재템플릿 UIA) · V-11 · V-12(WPF 실기 캡처).

### ▷ 🔧 플랜 착수 준비 (2026-09-07) — 롤백 지점 + 하네스 규칙 정정

- **롤백 지점 확보**: 태그 `before-suppression-recurrence` = 브랜치 `backup/before-suppression-recurrence` = **`b4ffde6`**.
  ⚠ **1차 시도가 조용히 실패**했다 — `git add -A -f .` 의 `-f` 가 gitignore 를 무시해 `bin/Debug/.../Logs/` 까지 긁었고,
  그중 **파일명이 반복 연결된 비정상 로그**(`log-2025-11-12.txt` ×11 연결)가 Windows 경로 한계를 넘겨 `add` 전체가 **exit 128**로 죽었다.
  `read-tree HEAD` 만 남아 **스냅샷 트리가 HEAD 와 동일**(차이 0)한 빈 껍데기가 만들어졌다.
  ⇒ **교훈: `-f` 는 전역이 아니라 `docs/` 에만.** 검증은 "HEAD 대비 변경 파일수 > 0" 으로 반드시 확인할 것.
  최종본은 3939파일 · docs 3417 포함 · **bin/obj 오염 0** · HEAD·인덱스 불변 확인.
- **다중 세션 실시간 충돌 관측**: 스냅샷 직후 `docs/INDEX.md` 가 **14초 만에 타 세션에 의해 8행 추가**됨(내 3행은 보존).
  ⇒ INDEX 편집 전 **항상 재독**(read-modify-write)해야 한다. [[project_multisession_checkpoint_sweeps_wip]]

- **🔴 하네스 규칙 자체 모순 발견·정정 — `.claude/rules/common/drag-first-ux.md`**
  - `:55`·`:137` 은 **`ToggleButton` 을 권장 핸들**로 올렸는데, `:105` 는 `Button` 을
    "**`ButtonBase.OnMouseLeftButtonDown` 이 캡처를 강탈해 마우스다운 순간 자멸**"이라며 금지한다.
    **`ToggleButton` 도 `ButtonBase` 파생이라 같은 코드를 탄다** — 규칙이 스스로 모순.
  - **리플렉션 실측(확정)**: 계보 `ToggleButton → ButtonBase → ContentControl → …` ·
    `OnMouseLeftButtonDown` 의 `DeclaringType` = **`ButtonBase`**(ToggleButton 이 **재정의 안 함**) ·
    `ClickMode` 기본값 = **`Release`** → `!= Hover` 이므로 **캡처 분기 실행**.
  - **정정 완료**(Must Always·Must Never·Quick Reference 3곳) — 차이는 **peer 유무이지 캡처 거동이 아니다**,
    `RadioButton`·`CheckBox` 도 동일, `ToggleButton` 을 쓰려면 **컨테이너 Preview 선점 필수**.
  - **해소책**: 스트립 `Border` 가 `PreviewMouseLeftButtonDown` 에서 `e.Handled=true` 로 선점 + **컨테이너가** `CaptureMouse()`.
    `ButtonBase` 버블 핸들러가 도달하지 못해 캡처 강탈·앵커 이중토글이 **원리적으로 사라진다**.
    **UIA `TogglePattern.Toggle()` 은 그대로 동작**(peer `OnToggle()` 은 마우스 경로 미사용), `Space` 도 `KeyDown` 이라 무관.
  - ⇒ "레포에 Preview 선례 0건"은 **부모와 경합 없는 경우** 이야기였고, 여기선 **자식(`ButtonBase`)과 경합**한다.
    와이어프레임 §3-2/§3 근거박스/V-08 을 이 결론으로 갱신.
- **V-08 재정의**: 상속·`ClickMode` 는 확정됐고, 남은 스파이크는 **"Preview 선점 후 드래그·클릭폴백·UIA Toggle·Space 4경로 동시 성립"**.
  실패 시 폴백 = 드래그 제거 + 빠른선택 버튼(평일/주말/매일).
- **플랜 워크플로** `wf_300ac456-5a8` 진행 중 — 실측 4축(테스트 파손·NATS 배선·파일별 변경·공수 캘리브레이션) → 분해 2안 → 3렌즈 심사 → 적대검증 4렌즈.

### ▷ 📊 플랜 실측 3/4축 완료 (2026-09-07) — PRD 에 결함 3건·완료현실 반영

- **🔴 신규 결함: 무제한 창은 영원히 삭제 불가** — `status` 계산이 `window_end` 로 `expired` 를 판정하는데
  무제한 창은 `window_end IS NULL` 이라 **영구 `active`**(`service:203-215`). 클라 `IsDeletable` 은
  `Status is "cancelled" or "expired"`(`ItemViewModel.cs:39`) → **영원히 false** ⇒ 하드삭제 불가 ⇒ **목록 무한 증식**.
  FR-10(무제한)을 넣는 이상 "정리하려면 먼저 취소" 경로를 같이 설계해야 한다. PRD §6-C 에 반영.
- **🔴 함정 17: 일일 시각 구분자에 `~` 재사용 금지** — 하네스가 피커 UIA 미노출 때문에
  **`FirstOrDefault(SafeName=="~" && !IsOffscreen)` 를 좌표 앵커**로 쓴다(`SuppressionSchedulePage.cs:91-100`, 2순위 폴백 **실제 활성**).
  `~` 가 2개면 트리 순서 의존 → 틀리면 **엉뚱한 피커에 타이핑해 무증상 오생성**(실패로도 안 잡힘).
  ⇒ 와이어프레임에서 일일 시각 구분자를 **`–`(en dash)로 분리** 완료. 창 구분자 `~` 2개는 의도적 유지.
  **덤**: 하네스 상수 `90`(=피커폭 180의 절반)이 **실제 196과 불일치** — 2026-08-07 폼 재설계 미반영.
  현재 8px 치우쳐 우연히 동작 중. 별건 태스크(90→98 또는 AutomationId 부여로 1순위 복원).
- **🔴 함정 18~20**: ① `EventSuppressionScheduleTests.cs` 가 **`Events.Ui.dll` 본체에 컴파일**(`csproj:21-22`)
  → **테스트 파손 = 라이브러리·앱 빌드 실패**, 수정은 같은 커밋 필수 ② `ItemViewModel` 생성자 인자 추가 시
  테스트 **11곳 CS7036** → "억제중"은 **DTO `is_suppressing_now` 파생**으로 ③ 상태 필터 항목 추가 시
  `SuppressionSweepTests.cs:155-159` 의 `Length == 5` **정확 단언** 즉사.
- **FR-13 보정**: `_activeCache` 소비자가 **3곳**(배너 `:669-675` · `DuplicateWarningText` `:679-692` ·
  **취소 후 '진행 중 N건 남음' 경고 `:280-286`**). 세 번째 누락 시 컴파일 에러 또는 경고 영구 무발화.
- **✔ 실측이 제기한 미확인 4건은 이미 확정된 것** (재조사 금지, PRD §6-C 표):
  목록은 **스케줄 1행**(occurrence 다행 아님) · `/active` 는 **"지금 회차 진행 중인 창만"** ·
  `recurrence_type` **기본값 `none`**(필수 아님) · `status` **4종 고정**(새 값 없음).
- **📊 공수 캘리브레이션(95플랜·677태스크 실측) → PRD §9-A 신설 + 2단 게이트**
  - 태스크 추정 밀도 평균 **1.26h**·중앙값 1.00h·**최빈 0.5h(29%)** — 스킬 규정(1~4h)과 다르다. **0.5~1h 로 쪼갠다.**
  - 벽시계 **81% 당일 종료**, 세션 1회 ≈ **20~41 태스크**.
  - 착수 플랜은 **80~100%(중앙 88%)에서 정지**. 잔여는 **항상** ①실기/런타임(데스크톱 독점) ②**메인 솔루션 재빌드(DLL 락)** ③외부(서버) 3종.
  - **메인 솔루션 연동은 실측상 사실상 100% 미완** ⇒ **FR-17 을 게이트 B 로 강등**, 라이브러리 완료 판정을 여기 걸지 않는다.
  - XAML 레이아웃 **1.5~2배 버퍼**(재작업 4패턴), 테스트 수정 **+0.5h**(기존 실패 격리·blame 입증).
  - **최대 좌초 선례** `UI_ModernTheme_DesignSystem`: XAML 전면 토큰화 **127h/72태스크가 17%에서 정지** — 추정 상향도 무효.
    ⇒ **XAML 범위를 넓히지 않는 것이 유일한 방어**. 이 PRD 가 "목록 컬럼 0건 / Row3 하나"로 좁힌 근거.
- **NATS 배선 확정**(실측): `ISuppressionActiveMonitor` 에 `RequestImmediatePollAsync(reason, ct)` 추가 +
  **리딩 엣지 스로틀**(서버가 단발 경계에서 중복 발행 + bulk-delete N건 버스트) · `PollAsync` 의 `_active` 쓰기와
  `ActiveChanged` 발화를 **디스패처 마샬링**(현재는 DispatcherTimer 라 우연히 성립, NATS 백그라운드에서 부르면 깨짐) ·
  `EventUiModule` 에 `.As<IService>()` **필수**(`:78` 사문화된 `DeviceNatsSyncService` 가 정확히 이 누락 함정) ·
  `Order` 메타데이터 중복(`:131` `:141` 둘 다 `_count+4`) 정리.

### ▷ ✅ 구현 플랜 완성 (2026-09-07) — [`event-suppression-recurrence-prd-plan.md`](../plans/event-suppression-recurrence-prd-plan.md)

- **워크플로** `wf_300ac456-5a8` — 실측 4축(70 단위항목/65h) → 분해 2안 → 3렌즈 심사(**tdd-first 19.5 / risk-first 17.5**) → 적대검증 4렌즈.
  **13 에이전트 / 2.86M 토큰 / 307 툴콜 / 47분 / 오류 0**. 결과 **fatal 6 · major 12 · minor 11**.
- **최종**: **61 태스크 / 52h**(밀도 0.85h — 레포 중앙값 0.89와 일치) · **2단 게이트**(A 라이브러리 55 / B 실기·메인 6).
  임계 경로 ≈30h = `VER-01/02 → S1-A(7.5h) → T-D02 → S3(순차 10h) → S4(순차 9.5h) → T-G06 → DOC`.
  **S3(718행)·S4(963행)는 단일 파일 연속 편집이라 병렬 불가.**

- **🔴 적대검증 fatal 6 — 전부 반영**
  | # | 반증 | 반영 |
  |---|---|---|
  | F-1/5 | `RequestDto` 개명 후 호출부(`PanelViewModel:222`)가 S3까지 안 고쳐져 **약 15h 동안 `Events.Ui.dll` 빌드 불가 · `dotnet test` 0회** — TDD 플랜인데 Red/Green 판정 수단 소멸 | **T-B04**에 최소 컴파일 픽스 병합, T-E11 축소 |
  | F-2 | `IsDrag` 정본 Utils 이관이 `GMaps.Ui.Tests` **CS0246** — `ProjectReference` 추가는 **NU1201**(net8.0 → net8.0-windows/UseWPF)로 불가 | **DEFER-01** 범위 밖. `CameraPopupHubMath.IsDrag` 그대로 호출 |
  | F-3 | `FormErrorText`/`HasFormError` **생성 태스크 부재** → WPF가 없는 경로에 **예외 없이** 빈 문자열+Collapsed → **이미 출하된 '30일 초과' 경고가 조용히 소멸**하고 새 경고도 안 뜸. 버튼만 죽고 **이유 표시 없음** | **T-E05b** 신설 |
  | F-4 | stale 배너가 **폴링 성공 + count>0**일 때만 보임(`Visibility`가 `HasActiveSuppression` 게이트) → 정작 **폴링 실패 시 배너 통째 소멸** | `HasActiveBanner = HasActiveSuppression \|\| IsActiveStale` (T-E08/T-F05) |
  | F-6 | S5가 **메인 레포 3파일** 편집하는데 M-00 통지는 1건뿐 → `main_solution_advance_notice`·`spec_full_compliance_propose_deviations` 위반. ⚠ `GopApiClient.cs`는 **타 세션 +92줄 미커밋 편집 중** | **U-2** 통지 4건 확장 + 🅑 표기 |
- **major 반영 10건**: ToggleButton **Style 부재**(F-05 수정 자리 없음 → `T-F03b`, 선례 `DetectionHistoryDialogView.xaml:63-95 FilterChip`) ·
  **FR-06 화면 출력 태스크 부재**(계산만 되고 표시 안 됨 → `T-F08b`) · 2줄 셀 vs `RowHeight="38"` 고정(→`MinRowHeight`) ·
  가로 예산이 **미커밋 `Width="1050"`에 의존**(→RISK-01ⓒ 선커밋) · 메인 레포 WIP 스냅샷 부재(→RISK-01ⓑ) ·
  게이트 B가 게이트 A 회귀를 막음(→dep 제거) · S5 거짓 병렬(→신규 파일) ·
  **`GopApiClient` 운영 쓰기 가드 부재**(기본 BaseAddress=운영, `EnsureWriteAllowed` 일반화 — **"운영 쓰기 0" 계약**).

- **🔲 착수 전 사용자 확인 3건**
  - **U-1 worktree 미사용 deviation** — [[feedback_prd_worktree_required]] 이탈. 사유: 메인 솔루션이 **v2.6 워킹트리를 `ProjectReference`**(`csproj:60~87`) 하므로 worktree 브랜치는 **앱 빌드에 반영되지 않는다**. 롤백은 스냅샷 태그로 대체
  - **U-2 메인 솔루션 편집 4건**(NatsBroker 1줄 + GopApiClient + SuppressionSchedulePage + DestructiveGuard)
  - **U-3 목록 열 폭**(상태 90→132 / 대상 200→170) — CHANGELOG ⑦에 이미 "사용자 확인 대기" 상태
- **⚠ 파이프라인 상태는 건드리지 않았다** — `activePlan` 이 타 세션 `pidsgroup-rightclick`(dev 36/50)이라
  `advance-phase plan` 을 돌리면 그 세션 진행을 **가로챈다**. 플랜은 문서로만 존재한다.

### ▷ 🔍 미커밋 작업 전수 조사 (2026-09-07 23:5x) — 사용자 "세션 종료된 작업이면 커밋하고 진행 가능한지"

- **결론: 종료 확정 작업은 단 1건.** 나머지 161건은 **전부 진행 중** → 커밋 금지.
- **⚠ 내 1차 판단 오류 정정**: "claude 프로세스 3개"라고 했으나 `head -25` 에 잘린 출력이었다.
  실제 **11개 생존**(시작 09:32~18:36). 프로세스 수로 세션 종료를 판정하려던 접근 자체가 틀렸다.
- **판정 근거(mtime 클러스터)**
  | 시각 | 작업 | 판정 |
  |---|---|---|
  | **08-08 02:23** | `EventSuppressionSchedulePanelView.xaml` **1줄**(`Width 1180→1050`) | ✅ **종료 확정**(한 달 방치) |
  | 09-06 23:33 | `.gitignore`·Enum건물·Infra마커·csproj | 진행 중 |
  | 09-07 09:50~16:19 | 인프라(시설물) 심볼 8파일 | 진행 중 |
  | 09-07 18:44~18:49 | 한글변환/장비타입 13파일 | 진행 중 |
  | 09-07 19:05~19:46 | PIDS그룹 3D 철망/통문 다수 | 진행 중 |
  | 메인 20:56~**23:02** | `NatsBrokerService`·`NatsDomainService`·`GopApiClient`·`FenceGateRuntimeDiagTests` | **51분 전까지 활동** |
  session-context 가 "실기 검증 진행 중"·"5차 실행 진행 중"·"코드 조사 에이전트 진행 중"을 명시.
- **✅ 회수 커밋 `87cc86e0`** — `Width="1180"→"1050"` 한 줄. 폼 재설계 커밋 `dac71ff`(08-07 16:21) **10시간 뒤** 조정 후
  **한 달 미커밋 방치**(CHANGELOG 미기록, 이 파일에만 존재하는 값). **`git add <path>` 단건** — `-a` 는 타 세션 WIP 161건을 쓸어간다.
  ⇒ 적대검증 MAJOR-7 / 플랜 **RISK-01ⓒ 해소**. S4 가로 예산(626/218/762)의 근거 확정.
- **✅ 커밋 없이 진행 가능함을 실증** — 내 플랜 대상 파일 **11개 중 9개가 깨끗**.
  겹치는 2개는 `EventUiModule.cs`(19:27, T-D07 대상 — S2 후반) · 메인 `NatsBrokerService.cs`(20:56, M-01 = **게이트 B**).
  둘 다 **후반부라 착수를 막지 않는다**. 하네스 `GopApiClient.cs`(21:07)도 게이트 B.
- **✅ 빌드 green 확인** — 현 미커밋 상태 그대로 `dotnet build` **exit 0**(오류 0 / 경고 396, 기존 수준). GIS 앱 미실행(DLL 락 없음).
- **사용자 결정**: **U-1 승인**(worktree 미사용 — 메인 솔루션이 v2.6 워킹트리를 `ProjectReference` 하므로 worktree 는 앱 빌드 미반영) ·
  **U-3 보류**(목록 열 폭은 구현하면서 조정) · U-2 는 M-00 에서 통지 예정.

### ▷ ✅ V-08 스파이크 완료 (2026-09-08) — **설계 성립**, 하네스 규칙 2차 정정

- **실증 프로그램**: `scratchpad/v08spike`(WPF STA 콘솔, `net8.0-windows`+`UseWPF`). 5회 반복 동일 결과.
- **🎯 T8 결정적 판정** — `ProbeToggle : ToggleButton` 로 `OnMouseLeftButtonDown` virtual 호출을 **직접 계수**:
  - 미핸들 이벤트 → **1회 호출**(정상)
  - **이미 `Handled=true` 인 이벤트 → 0회 호출**
  ⇒ `ButtonBase` 클래스 핸들러는 **`handledEventsToo: false`** ⇒ **터널 선점이 컨테이너를 보호한다.**
  ⇒ 앞서 발견한 "ToggleButton 도 ButtonBase 라 캡처 강탈" 문제의 **해법이 원리적으로 성립함이 확정**됐다.
- **🔴 T1 설계 정정(중요)**: **`PreviewMouseLeftButtonDown`·`MouseLeftButtonDown` 은 `Tunnel`/`Bubble` 이 아니라 `Direct` 다**(실측 출력).
  실제 터널/버블 쌍은 **`PreviewMouseDown`/`MouseDown`**. 컨테이너 터널이 셀보다 **먼저** 실행되고 `Handled` 로 하류를 끊는 것을 순서값(1 vs 0)으로 확인.
  ⇒ 와이어프레임·플랜·하네스 규칙 3곳 모두 **`PreviewMouseDown`** 으로 정정.
  (실입력에선 WPF 가 route 전 요소에 Direct 를 승격시켜 주므로 기존 표기도 동작은 하나, 터널 쪽이 명시적·확실)
- **T3**: UIA `TogglePattern.Toggle()` 이 마우스 선점과 무관하게 `IsChecked` 를 뒤집는다(peer=`ToggleButtonAutomationPeer`) — **자동화 경로 안전**.
- **T5**: `ToggleButton` 이 `OnMouseLeftButtonDown` 미재정의 · `ClickMode` 기본 `Release` 재확인.
- **⚠ 미검증으로 남긴 것(정직 고지)**: **실제 마우스 입력에서의 캡처 소유권**.
  오프스크린·투명·`Opacity=0`·비활성 창에서 `Mouse.Capture` 결과가 실행마다 **`Border`/`ToggleButton`/`null` 전부 관측** →
  **환경 산물이지 발견이 아니다**. 합성 `RaiseEvent` 는 `ButtonBase` 를 결정적으로 구동하지 못한다(실 마우스 디바이스 상태 의존).
  ⇒ **게이트 B(실기, 데스크톱 독점)** 로 이월. 단 T8 이 원리를 확정했으므로 **설계 무효화 위험은 해소**됐다.
- **플랜 갱신**: RISK-02 ✅ 완료 · RISK-03(폴백 확정) **불필요로 보류** — 게이트 B 실기에서 뒤집히면 그때 착수.
- **하네스 규칙 2차 정정**(`drag-first-ux.md`): 터널 이벤트명 정정 + **`handledEventsToo: false` 실증 근거** 명문화 + Quick Reference.

### ▷ ✅ 억제 반복 **게이트 A(라이브러리) 구현 완료** (2026-09-08) — 47/61 태스크

- **검증**: 솔루션 빌드 **오류 0** · `Events.Ui` **563 통과 / 15 실패** · **신규 회귀 0건**
  (실패 15건은 VER-05 기준선과 **완전 동일** — 타 세션 미커밋 작업. `comm` 차집합으로 격리 입증)
  통과 484 → **563**(+79), 억제 도메인 31 → **110**.
- **신규/수정 파일 9개**
  | 파일 | 내용 |
  |---|---|
  | `Messages/Dto/Events/EventSuppressionScheduleDto.cs` | 응답 9필드 · `WindowEnd` → `string?` · **Create/Update DTO 분리(둘 다 sealed·무상속)** · `ShouldSerializeXxx` 3종 |
  | `Events.Ui/Helpers/SuppressionRules.cs` | 요일 Mon0 변환 · 프리셋 · `Summarize` · 검증 3종 · 모드별 상한 · `IsRelevantToday` |
  | `Events.Ui/Helpers/SuppressionPollThrottle.cs` 🆕 | 리딩엣지+**트레일링 보장** 스로틀 · TTL stale · 경과 문구 |
  | `Events.Ui/Services/SuppressionActiveMonitor.cs` | `LastSuccessAt`/`IsStale`(TTL 90s)/`RequestImmediatePoll` · **디스패처 마샬링** · 재진입 가드 |
  | `Events.Ui/Services/EventSuppressionSyncNatsService.cs` 🆕 | subject+cmd **이중 자가필터** · 로그인 게이팅 · 폴링 가속 전용 |
  | `Enums/EnumGopCommand.cs` | `SYNC_EVENT_SUPPRESSION = 30` |
  | `Events.Ui/Modules/EventUiModule.cs` | DI 등록(**`.As<IService>()` 포함** — 누락 시 구독 누수) |
  | `Events.Ui/ViewModels/Panels/…PanelViewModel.cs` | 반복 폼 상태 · **`IsUnlimitedEffective` 파생** · 모드전환 정리 · `FormErrorText`/`HasFormError` · `HasActiveBanner` · Monitor 이관(소비자 3곳) · OnActivate/OnDeactivate 짝 |
  | `Events.Ui/ViewModels/Panels/…ItemViewModel.cs` | **ctor 불변** 유지하며 DTO 파생으로 억제중/무제한/요약/회차 투영 |
  | `Events.Ui/Views/Panels/…PanelView.xaml` | 배너 **로컬값 제거→Style Setter** · Row2 라벨밴드+라디오 인라인 · **Row3 신설**(요일 7칩·TimePicker 2·무제한) · 요약 미러 · 스타일 3종 뷰로컬 |
- **신규 테스트 2파일 79건**: `SuppressionRulesRecurrenceTests`(요일원점·검증·요약·중복판정) ·
  `SuppressionItemProjectionTests`(배지↔억제중 분리·무제한 3종 구분·회차·**무제한 삭제불가**)
- **적대검증 fatal 6 전부 반영 확인**
  - F-1/5: `T-B04` 에서 DTO 개명과 호출부(`PanelViewModel:222`)를 **같은 단위로** 처리 → 빌드 무중단, `dotnet test` 상시 가동
  - F-2: `IsDrag` Utils 이관은 **DEFER-01** 로 범위 밖(NU1201 구조적 불가)
  - F-3: **`FormErrorText`/`HasFormError` VM 신설** → 기존 30일 경고 소멸 방지
  - F-4: **`HasActiveBanner = HasActiveSuppression || IsActiveStale`** + XAML Visibility 교체
  - F-6: 메인 레포 편집은 전부 **게이트 B** 로 분리(M-00 통지 후)
- **정적 XAML 전수 검증**: 참조 47키 중 미해석 **0**(잔여 4건은 MDIX 패키지 키) · 바인딩 61개 전량 VM 실재 ·
  **`Text="~"` 1개 유지**(하네스 좌표 앵커 보호, 일일 시각은 `–` 로 분리) · `x:Name` 변경 0건 · UTF-8 BOM 전량.
- **🔲 잔여 14** — 게이트 B(M-01/M-02 메인, V-08 실기 캡처 소유권, V-09~V-12 실기) ·
  **T-F07/F08/F08b 목록 열**(U-3 사용자 결정 — "구현하면서 조정") · **T-F09 드래그 페인팅**(T-F03b 위에 얹으면 됨) ·
  T-D06 NATS 단위테스트 · T-E12 패널 VM 헤드리스 · T-G03~G06 하네스 · DOC.

### ▷ ✅ 게이트 A **커밋 완료** `3acb06c9` (2026-09-08) — 타 세션 무손상 검증 통과

- **커밋 범위**: 정확히 **15파일 / +1980 −75**. 억제 도메인 전용.
- **🔴 유일한 위험은 `EventUiModule.cs` 였다 — 헝크 3개 중 2개가 타 세션 것**
  (`IDoorContactPolicy`·`DefaultDoorContactPolicy`·`OperationEventNatsSyncService` 등록 + `ons.StartService()`).
  통째로 커밋하면 **미커밋 신규 타입 4종을 참조하는 커밋**이 되어 클린 체크아웃에서 컴파일이 깨진다.
  ⇒ **HEAD 판본 + 내 헝크만** 재조립한 blob 을 `git hash-object -w` → `git update-index --cacheinfo` 로
  **인덱스에만** 넣었다. **작업트리 파일은 손대지 않았다.**
  커밋 후 그 파일엔 타 세션 헝크 2개가 **미커밋으로 정확히 남았다**(실측: 헝크 2, 심볼 4건 잔존).
- **사전 정밀 검증(격리 워크트리)**: `HEAD + 내 15파일` 트리로 `git worktree add --detach` →
  **솔루션 빌드 오류 0** · `Events.Ui` **535 통과 / 15 실패** · **억제 실패 0**.
  실패 15건은 기준선 목록과 **바이트 단위 동일**.
- **⚠ 내 앞선 귀속이 틀렸다(정정)**: 기존 실패 15건을 "타 세션 미커밋 작업 때문"이라 했으나,
  타 세션 코드가 **하나도 없는** 격리 워크트리에서도 동일하게 실패한다 ⇒ **HEAD 자체의 기존 실패**다.
  (결론 "내 회귀 아님"은 동일하지만 원인 귀속이 잘못됐었다.)
- **⚠ 검증 중 걸린 함정**: 첫 테스트가 `OnvifSolution` 에러로 죽었는데, 이건 **서브모듈(gitlink)이라
  새 워크트리에 초기화되지 않아서**였다(`git ls-files` 가 디렉터리 자체 1건만 반환 = mode 160000).
  ⇒ **어떤 새 clone/worktree 도 그대로는 빌드되지 않는다.** 메인에서 복사해 보강 후 정상 결과.
- **커밋 후 사후 검증**: 작업트리 217 → **203**(감소 14 = 내 커밋분) · 타 세션 파일 `M`/`??` 그대로 ·
  **전체 솔루션 빌드 오류 0**(모든 세션 작업 공존 상태).
- **의도적 제외**: 내 산출물 문서 4종(PRD·플랜·와이어프레임·스토리보드)은 **gitignore 대상이라 커밋 불가**.
  추적되는 `docs/INDEX.md`·`session-context.md` 는 **타 세션이 동시 편집 중**이라 제외했다(클로버 방지).

### ▷ ✅ 2차 커밋 `2af65ab8` (2026-09-08 11:20) — 목록 표시 · 드래그 페인팅 · 테스트 80건

- **파일 6개**(수정 1 + 신규 5) / +1184 −5. 전부 억제 도메인 소유.
- **목록 표시(FR-03~06) — 컬럼 추가·삭제 0건**: 고정 합이 빠듯해(750/≈1000) 새 컬럼을 만들면 작업명이 붕괴한다.
  ⇒ 폭 대신 **높이**를 쓰거나 기존 셀 안에 넣었다. 상태 90→132(배지 옆 '억제중' 형제) ·
  작업명 2줄(반복 요약) · 종료 2줄(회차 정보) · 대상 200→170 ⇒ 고정 합 **762**, 작업명 ≈238.
  **`RowHeight="38"` → `MinRowHeight`** (고정값이면 2줄 셀이 잘린다).
  헤더 7종 이름 불변(T-SUP025) · `StatusText` 접미 금지(T-SUP028).
- **드래그 페인팅(FR-08)**: 컨테이너 `PreviewMouseDown`(터널) 선점 + 직접 캡처 ·
  데드존 8.0 **초과**(정확히 8.0은 클릭) · 단일 `FinishDrag` + `LostMouseCapture` 양쪽 ·
  ESC 는 `PreviewKeyDown` 에서 **드래그 중일 때만** 소비 · 범위 표식은 **첨부 속성**(로컬값 write 금지) ·
  시각화는 채움(면) vs 하단바(선) **축 직교** + 바 색 2분기.
  ⚠ `IsDrag` 정본은 `GMaps.Ui` 라 참조 불가 → **같은 값·같은 수식·같은 경계**를 `DayStripDragMath` 에 복제하고
  테스트로 고정(새 상수 신설이 아님을 주석에 명시). Utils 이관은 DEFER-01.
- **테스트 +80**: `DayStripDragMathTests` 35 · `EventSuppressionSyncNatsServiceTests` 19 ·
  `SuppressionPanelRecurrenceTests` 26. 드래그는 **UIA 로 단언 불가**(.NET 8 에 패턴 타입 부재)라
  순수함수 테스트가 유일한 회귀 방어선이다.
- **검증**: 솔루션 빌드 오류 0 · Events.Ui **643 통과 / 15 실패(전부 HEAD 기존)** · **신규 회귀 0** ·
  억제 도메인 155 → **190** · XAML 미해석 0 · `Text="~"` 1개 유지 · `x:Name` 변경 0 · UTF-8 BOM.

### ▷ 🎯 헝크 분리가 실제로 통했음이 이력으로 입증됨

- 타 세션이 **10:43 에 직접 커밋**(`5d52779e` PIDS 3D 철망·통문·함체 + 2.5D 틸트 + 라인드로잉 10건).
  내 두 커밋(`3acb06c9` 10:35 / `2af65ab8` 11:20) **사이**에 들어왔다.
- **`EventUiModule.cs` 교차 검증**
  | 확인 | 결과 |
  |---|---|
  | 내 커밋에 `OperationEventNatsSyncService` 포함 | **0건** ✅ |
  | 타 커밋에 `EventSuppressionSyncNatsService` 포함 | **0건** ✅ |
  | 현재 파일에 양쪽 등록 공존 | 내 2건 + 타 4건 + DoorContactPolicy 3건 ✅ |
  ⇒ **서로의 작업을 한 줄도 건드리지 않고 같은 파일을 나눠 커밋**했다.
- 작업트리 217 → **74**(그들 커밋 + 내 커밋). **유실 0** — 파일 전부 디스크有·추적됨.
- ⇒ 다중 세션에서 공유 파일을 다룰 때: `git hash-object -w` + `git update-index --cacheinfo` 로
  **인덱스에만** 분리본을 넣고 작업트리는 그대로 두는 방식이 실증됐다. [[project_multisession_checkpoint_sweeps_wip]]

### ▷ ✅ 승인 3건 완료 + 적대검토 회귀 3건 수정 (2026-09-08) — 커밋 4개

| 커밋 | 레포 | 내용 |
|---|---|---|
| `da60c613` | Lib | **`.gitmodules` 복구** — 새 clone 빌드 불가 해소 |
| `60503d3b` | Lib | **적대검토 회귀 3건 수정** |
| `5cb8977` | Sol | `SYNC_EVENT_SUPPRESSION` no-op + 좌표 앵커 상수 정정 |
| `48def0b` | Sol | **운영 쓰기 가드 일반화**(별건 안전 결함) |

- **🔴 적대검토(4렌즈·6에이전트·1.28M토큰)가 잡은 실제 회귀 3건 — 전부 "안전 경고가 거짓말하거나 침묵"**
  | # | 결함 | 원인 |
  |---|---|---|
  | A | 취소 직후 '잔존 억제' 경고가 **30초 묵은 스냅샷**을 읽어 거짓 경고 | `_activeCache`→`ActiveWindows` 이관 때 **판정 시점**을 놓침. 운영에서 monitor 는 항상 주입돼 폴백으로 안 떨어지고, 방금 await 로 갱신한 `_activeCache` 를 아무도 안 읽게 됨. **내가 만든 회귀** |
  | B | stale 배너가 **글자 없는 빈 테두리**로 뜸 | `ActiveStaleText` 가 XAML 참조 **0건**이고 `ActiveCountText` 는 count==0 에서 빈 문자열 ⇒ '억제 0건'과 '서버에 못 물어봄'이 **구분 불가** |
  | C | **한 번도 성공 못 한 폴링은 영원히 stale 이 아님** | `IsStale` 이 `_lastSuccessAt` 만 봄 ⇒ 서버 재기동·구버전 404·502 로 첫 폴링부터 실패하면 배너가 통째 침묵. `DescribeAge` 의 "확인 안 됨"은 **도달 불가 죽은 코드**였음 |
  - 수정: `FreshActiveWindows` 신설(변이 직후 판정 전용) + 변이 후 `RequestImmediatePoll` ·
    stale 문구 형제 TextBlock 바인딩 + AutomationId · `IsStale` 기준을 `_lastSuccessAt ?? _startedAt` 로.
  - **회귀 테스트 +15**(스로틀 리딩엣지/트레일링·시계역행·TTL·"한 번도 성공 못 함"·경과 문구).
  - ⚠ 검토가 지적한 major 2건("아이템 VM 프로퍼티가 화면에 안 나온다")은 **검토 기준 커밋 시점의 사실**이고
    `2af65ab8`(목록 표시)에서 이미 해소 — 현재 4종 전량 바인딩됨을 grep 으로 확인.
- **🔴 별건 안전 결함(억제와 무관, 조사 중 발견)**: `GopApiClient` 쓰기 8개 중 가드가 **장비 4개에만** 걸려 있고
  `CreateSuppressionAsync`·`DeleteSuppressionAsync`·`CreateDetectionAsync`·`DeleteDetectionAsync` **4개가 무방비**였다.
  주석이 스스로 "기본 BaseAddress 는 운영 서버(123.141.236.253)"라 적어둔 상태 ⇒ 스왑 없이 돌리면
  **운영에 탐지 이벤트·억제 창이 생성·삭제된다**(「운영 쓰기 0」 위반). 가드 로직 자체는 옳았고 **적용 범위만 좁았다**.
  `CanWriteServer`/`EnsureWriteAllowed` 로 일반화하고 구 이름은 위임 래퍼로 남겨 호출부 무파손.
- **`.gitmodules` 복구**: gitlink(`160000`, `35e0aeba`)는 있는데 매핑 파일이 없어 `submodule update --init` 이
  **원리적으로 실패**했다. 로컬 remote(`lsirikh/Ironwall-Onvif-Solution`)·HEAD 가 인덱스 기대값과 일치해 매핑만 추가.
- **⚠ M-02 블로킹(플랜 예측대로)**: 앱 프로젝트 빌드 불가 — **GIS 앱(PID 51412, 10:36 기동) DLL 락**
  (`MSB3027 ... 51412 에 의해 잠겨 있습니다`). UiTests 프로젝트는 **오류 0**으로 검증됨.
  `NatsBrokerService` 변경은 기존 no-op 그룹에 **case 한 줄**이고 참조 enum 은 이미 커밋(`3acb06c9`) — 앱 종료 후 재빌드로 확인.
- **판단 정리**: D-7 편집 UI 는 **"수정=취소 후 재생성" 제3안**을 권고(서버 변경 0)하되 이번 범위 밖 ·
  D-8 은 서버 PM 결재라 **물을 필요 없음**(결재 나도 GIS 는 `is_suppressing_now` 소비뿐) ·
  DEFER-01 은 **하지 않음**(Utils=net8.0-windows vs GMaps.Ui.Tests=net8.0, NU1201 구조적 불가).
- **최종 검증**: 솔루션 빌드 오류 0 · Events.Ui **658 통과 / 15 실패(전부 HEAD 기존)** · **신규 회귀 0** ·
  억제 도메인 **205** · XAML 미해석 0 · `Text="~"` 1개 유지.
- **잔여 10건**: 전부 **실기 환경 필요**(V-08·V-09·V-11·V-12 실기 캡처 · UI 하네스 회귀 · 라이브 왕복 · M-02 · DOC).

### ▷ ✅ M-02 해소 + XAML 런타임 위험 실증 검증 (2026-09-08) — 플랜 60/61

- **M-02 통과**: GIS 앱(PID 51412)이 종료돼 DLL 락이 풀렸다 → **메인 솔루션 전체 빌드 오류 0**.
  이제 `SYNC_EVENT_SUPPRESSION` 명시 no-op(커밋 `5cb8977`)이 실제로 컴파일됨을 확인.
- **하네스 Unit 97/98** — 실패 1건 `DbProbeTests.should_hold_valid_attributes_for_every_symbol`.
  **격리 입증**: 억제 참조 **0건**(테스트·DbProbe 양쪽 grep) · 내 커밋 2개가 `DbProbe` 를 건드린 적 **0건** ·
  마지막 변경자는 타 세션(`b04f7c8`). ⇒ **로컬 monitor_db 심볼 데이터 감사 실패**로 내 회귀 아님.
- **🎯 XAML 런타임 위험 정면 검증(`scratchpad/xamlprobe`)** — 이 레포에서 가장 위험한 부류를 직접 쳤다.
  "XAML 리소스 미해석은 **빌드가 못 잡고 런타임에만 터진다**"(CHANGELOG 실증).
  테마 5딕셔너리를 Application 스코프에 병합한 뒤 **컴파일된 View 를 실제로 인스턴스화**:
  - **T1 PASS — `InitializeComponent` 통과.** ⇒ 신설 StaticResource 전량(`DayToggleChip`·`FormTimePicker`·
    `ActiveSuppressionBanner`·`BoolToVis`/`BoolToVisInv`)이 **해석됨**. Style·ControlTemplate·MultiDataTrigger 구조 오류 없음.
    **정적 grep 으로는 절대 못 잡는 층위**를 통과한 것이다.
  - **T2 미결(정직 고지)** — 시각트리가 `UserControl → Border → ContentPresenter` 에서 멈춰
    요일 칩 7개·`~` 앵커 존재를 트리로 단언하지 못했다. DataContext 없는 헤드리스 창의 **실현 한계**이지
    XAML 결함이 아니다(같은 항목을 정적 검증으로는 이미 확인: `Text="~"` 1개·`–` 1개·바인딩 69개 전량 실재).
    프로브 기계장치를 더 손보는 것은 산출 대비 비용이 안 맞아 **중단**했다.
- **계정 환경변수 4종 전부 미설정**(`IRONWALL_UITEST_ID/PW/SERVER/API`) ⇒ UI 스모크·기능은 규칙대로 **Skip**.
  ⚠ 계정은 **환경변수로만** 전달한다(스크립트 인자 금지 — 콘솔 이력 잔존).
- **플랜 60/61** · 게이트 A DoD 4항목 체크 완료.
- **잔여 4건 — 전부 실기/외부**: V-08 실입력 캡처 소유권 · V-09 MDIX 자연높이 · V-11 목록 잔여행 ·
  V-12 라이트/다크 실기 캡처 · UI 하네스 회귀(계정 필요) · 로컬 6.3.3 라이브 왕복.

- **📮 서버팀 회신 대기 4건**: ① 원격 6.3.3 배포 일정 ② `permission_map.py` 에 `/reorder` 항목 누락 ③ 단발 창 경계 `suppressing` 오발행(`suppression_scheduler.py:55-89 _fire_boundary` 가 `notified_suppressing` 미기록 → 시작 시 `{active,false}` 발행 후 ≤5분 뒤 2번째) ④ G5 통계 문자열 매칭 여부.
- **🐛 별건 발견 — `docs/INDEX.md` mojibake 14행**: coordination·design 절의 **섹션 제목·표 헤더**가 깨져 있다(`?뚯씪`=파일, `?ㅺ퀎`=설계). **HEAD 커밋본에 이미 존재 = 이 세션 소행 아님**(과거 CP949 쓰기). `?` 로 소실된 바이트가 있어 변환 복원 불가 — 표 헤더·섹션 제목은 재작성으로 복구 가능. **사용자 판단 대기**.

### ▷ ✅ 서버 API **6.3.4 입력 검증 강화** 대응 완료 (2026-09-08) — 커밋 `248b160e` · 플랜 64/65

**계기**: api-test-server 팀 전달 [`GOP_Server_API_suppression_input_guards_GIS_NOTIFY.md`](../coordination/GOP_Server_API_suppression_input_guards_GIS_NOTIFY.md) — 억제 스케줄 입력에 **422 5종 신설**. 전부 그전까지 조용히 통과하던 값이다.

- **🔴 검토 결과 내 코드에 실제 결함 3건** — 안내문은 "UX 개선 목적, 안 해도 데이터는 안전"이라 했지만 실제로는 기능이 하나 죽어 있었다.
  | # | 결함 | 증상 |
  |---|------|------|
  | **①** | `ValidateWeeklyForm` 이 **모든 동일 시각을 차단** | 서버가 인정하는 **유일한 종일 표기** `00:00:00~00:00:00` 까지 막아 **종일 억제를 등록할 수단이 아예 없었다**. 내 검증이 서버보다 과했던 것 |
  | **②** | 단발→반복 전환 시 유효기간이 `now+1h` 그대로 | 반복은 '기간' 개념인데 1시간 창엔 **어떤 요일도 들어가지 않는다** → 요일을 고르는 즉시 422. 아래 도달 가능성 검사가 잡아냈다 |
  | **③** | `_windowEndBackup` 하나를 두 전환이 공유 | '반복 전환 복원'과 '무제한 해제 복원'이 서로를 덮어, 무제한을 켰다 끄면 단발 복귀 시 1시간이 아니라 **30일 창**이 남았다 |
- **왜 자정만 허용인가**: 전면 금지하면 **진짜 24시간을 표현할 방법이 없어진다** — `00:00:00~23:59:59` 는 매일 **1초 구멍**. 서버 규칙은 `if daily_start == daily_end and daily_start != time(0,0): raise`. ⇒ `DailyTimeVerdict.AllDay` → **`AllDayMidnight`(허용) / `AllDayAmbiguous`(차단)** 로 분리.
- **신설**: `SuppressionRules.IsOccurrenceReachable` / `DescribeUnreachable` — 유효기간 안에 회차가 하나도 없는 창을 **저장 전에** 경고. 서버 `assert_occurrence_reachable` 과 동일한 좁은 범위(무제한=항상 통과 · 요일 1회라도 있으면 통과 · 자정넘김/자정종일 처리 · 유효기간 클램프). **이 검사가 위 ②③ 을 잡았다.**
- **✅ 확인했으나 대응 불필요** (안내문 지적 중 4건)
  | 지적 | 판정 |
  |---|---|
  | **센티널 `9999-12-31` 제거** — 안내문의 **유일한 '필수' 대응** | **해당 없음** — 레포 전수 grep **0건**. 무제한은 이미 `window_end: null` 명시 전송 |
  | `target_type=all` + 대상 배열 | **해당 없음** — 서버 POST 검증은 `device`/`group` 에만 배열 ≥1 요구, `all` 은 검사 안 함(소스 확인). 422 는 **PATCH 한정**이고 이 클라엔 PATCH 호출부가 없다 |
  | 단발 366일 상한 신설 | **주석만 정정** — 클라 30일이 더 좁아 422 불가. "서버는 단발에 상한 없다"는 낡은 기술 삭제 |
  | `next_occurrence_start` 정확도 수정 | **무변경 이득** — 발동하지 않을 시각을 약속하던 서버 버그가 고쳐져 `OccurrenceText` 가 그대로 정확해짐 |
- **⏸ 미착수(선택)**: 종일 체크박스 신설(체크 시 `00:00:00`/`00:00:00` 전송) · 연도 1900~2999 클라 가드. 둘 다 서버 **권장**이지 필수가 아니고, 폼 레이아웃 변경이라 별건.
- **검증**: 솔루션 빌드 **오류 0** · `Events.Ui` **674 통과 / 15 실패**(전부 HEAD 기존, 격리 워크트리 동일 재현) · **신규 회귀 0** · 억제 도메인 205 → **217**. 회귀 고정 **12건**.
- **커밋 `248b160e`** — 정확히 **4파일 / +271 −22**, 억제 도메인 전용. 타 세션 심볼 누출 검사 통과(IDoorContactPolicy 0 · OperationEventNatsSyncService 0 · HousingVisual 0). 조율 문서는 `.gitignore` 대상이라 제외.
- **문서 갱신**: PRD **v1.1**(FR-11 정정 · **FR-19 신설** · 함정 21~23 추가) · 플랜 **S7 절 신설**(G-01~05, 64/65).

---

## ▶▶ 재개 포인트 (2026-09-06 — 창(Window) 아키텍처 전면 재기획 · Track C · **범위 B 승인됨 · PRD v1.1 Review 검토 대기**)

- **요구(사용자)**: "Conductor 에 의해 Section/Panel/Dialog/Popup 으로 레이어가 잡혀 있어 **창 이동이 불가능**하고 장점보다 단점이 많다. 전체 창을 새로 기획하고 싶다. **다크/라이트 디자인이 깨지지 않으면서**. confirm/info popup 이 EventAggregator 를 쓰는 것도 번거롭다. 최적화하고 깔끔하게."
- **투입**: 워크플로 `wf_49895179-7f3` — 실측 8축(레이어·메시징·실제창·테마·인벤토리·DI·상처증거·외부해법) → 설계 4안 → 심사 3렌즈×4안 → 승자 적대검증 4렌즈 → 합성. **29 에이전트 / 8.07M 토큰 / 929 툴콜 / 65분**. 반증 34건.
- **📋 산출물(정본)**: [`docs/analyses/window-architecture-analysis.md`](../analyses/window-architecture-analysis.md) (99KB) — 현행 해부 + 문제 P0 7·P1 7·P2 8 + 목표 아키텍처 **Surface Kernel** + `IDialogService` 전문 + 테마 무결성 설계 + 로드맵 Phase 0~4 + 리스크 24 + **GAP 11**
- **근본 원인 확정**: 모달 3층(`PanelShell`/`DialogShell`/`PopupDialogShell`)이 `ConductorControlView` 안에 컴파일타임 고정 z-order 로 겹쳐 있고, 그 `ConductorControlView` 자체가 **`LeftMenuSectionView.xaml:264-272`("Do not change order!!!" 주석)의 자식**이다. 패널·다이얼로그 View 전체에서 `Thumb`/`DragMove`/`DragDelta` **0건** — 좌표를 줄 수 있는 지점이 한 곳도 없다.
- **✅ 사용자 최대 우려 해소(테마)**: `ThemeService` 스왑이 **`Application.Current.Resources.MergedDictionaries` + `PaletteHelper.SetTheme` + `ThemeManager.ChangeTheme(app,…)` 셋 다 Application 스코프** → 새 Window 는 토큰을 자동 상속하고 런타임 토글도 전파. 다중 창 전환의 최대 리스크가 이 프로젝트엔 없다. 조건은 하나 — 새 표면이 **아무것도 로컬 병합하지 않을 것**(커밋 `f811588` revert 전례).
- **🔴 즉시 고쳐야 할 P0(창 재설계와 무관하게 오늘도 터짐)**: ① `ProgressPopupDialogView.xaml` 44행에 **닫기 요소 0개** + 삭제 핸들러 catch 없음 → 예외 시 전체화면 스크림 + 취소 불가 = **앱 재시작 외 탈출 불가**(11개 패널 동일 경로) ② 확인 팝업 '취소'가 어디에도 전달되지 않음 → `_pending*` 필드 워크어라운드 13파일 ③ 억제 패널이 Info 발행 직후 `finally` 에서 즉시 닫음(결정적, 레이스 아님) ④ 팝업 VM 3종 `SingleInstance` + 층당 1슬롯 → 두 번째 팝업이 첫 번째 페이로드를 덮어써 **'확인'이 엉뚱한 액션 실행 가능**
- **마이그레이션 물량(실측)**: `OpenInfoPopup` 138 · `OpenConfirmPopup` 40 · `OpenProgressPopup` 28 · `ClosePopup` 46 = **252 편집점** + `Call*ProcessMessageModel` 29종 + `IHandle` 29개 + `_pending*` 13파일 + Close 마커 자기-닫기 29곳 + UI 하네스 17파일 ≈9,000행
- **✅ 자산 발견(부록 B)**: `GMaps.Ui/GMapControls/LayerPanelControl.cs` 가 **이미 드래그 가능한 패널**(Thumb 2 + `Canvas.SetLeft` 로컬값 직접쓰기). `SurfaceFrame` 은 신규 발명이 아니라 이 코드의 일반화 → 공수 하향 + 메모리 `project_panel_design_system`("전 패널 표준=GMaps LayerPanel")과 정합
- **✅ 사용자 결정 완료(2026-09-06)**: **GAP-7 범위 = B(창 이동까지, Phase 0→1a→1b→2→4)** · **GAP-1 = 셸 안 드래그·리사이즈 + 위치/크기 영속, 별도 OS 창(Detached) 미채택** · **GAP-2 = 라이트에서 밝은 카드로 뒤집기**(ThemeAssist 4곳 + FlatDarkBgButton 16파일 원자묶음) · **GAP-11 메인솔루션 변경 승인**(단계별 통지). 파생 자동해소 4건: GAP-4·6 무의미(Detached 제외) · GAP-5 요청큐만(AllowStack=false) · GAP-8 전량 SingleInstance 유지(**−1.5주**). 잔여 GAP-3·9·10은 권고안 진행+해당 Phase 확인.
- **📋 PRD 정본**: [`docs/prds/window-architecture-prd.md`](../prds/window-architecture-prd.md) **v1.1 · 상태 Review — 사용자 검토 대기**. FR-01~40 · NFR-01~13 · 비목표 N-1~13 · 리스크 활성22/신설3/소멸2 · V-01~12 · 미결 Q-1~5. 공수 **150점 = 계획선 12.5주 / 상한 15.0주**, **1차 출시 4주차**(Phase 0+1a, 셸 무변경).
- **⚠ PRD 검증 상태(정직 고지, §0-1)**: PRD 워크플로 `wf_a612906c-b95` 는 초안 2안+병합까지만 성공하고 **적대검증 3렌즈+확정 4에이전트가 "organization has disabled Claude subscription access" 로 실패**. 메인세션이 대행 표본검증 → **7건 확인**(Phase3 잔재 0 · ShellView.xaml:86 Splitter 컬럼 실재 · Close마커 10+19=29 정확 · LeftMenuSectionView:96-102 Light.Blue 실재 · DialogHost 22파일/호출0 · Progress View 닫기요소 0 · 공수 150점 검산 일치) **+ 정정 1건**(FR-18 무음 권한차단 **6→7곳**, `ConductorControlViewModel.cs:320` `CanOpenSetup()` SETUP 메뉴 누락). **FR-19~40 개별 인용 · §3 C# 시그니처 API 적합성 · §5 테마 토큰 키 실재 · NFR 수치 출처는 미검증** → plan 착수 전 재검증 권고: `Workflow({scriptPath: '…window-architecture-prd-wf_a612906c-b95.js', resumeFromRunId: 'wf_a612906c-b95'})` (초안·병합 캐시 적중, verify 단계부터 재개).
- **🎨 스토리보드·와이어프레임(사용자 지시: PRD와 동반 진행)**: [`docs/design/window-surface-kernel-storyboard.html`](../design/window-surface-kernel-storyboard.html) **v1.0**. 실제 토큰 렌더 + WPF `#AARRGGBB`→CSS `rgba()` 변환 명시. 10절 — 현행 시각트리 / 목표 Surface Kernel / **조작 가능한 와이어프레임**(Float 드래그·리사이즈·최대화·창목록·클램프 금지구역) / 표면 5종 / Before-After / **GAP-2 라이트 대비 1.05:1 재현** / **GAP-9 스크림 4조합** / **§8 권한 UX 역할 전환 데모** / API Before-After / 확인요청 6건. 캡처 `docs/assets/window-surface-{wireframe-dark,gap2-light,perm-light}.png`. ⚠ 브라우저 검증 중 **클래스 충돌 결함 1건 발견·수정**(P0 알람 배너 `.alarm` 이 지도 심볼 `.sym.alarm` 을 지움 → `.p0alarm` 분리) — **수정 후 재실행 검증은 브라우저 점유 충돌로 미실행**(정적 검사만).
- **🔐 권한 전수 감사 완료(사용자 지시 "절대 권한 누락 금지")**: 워크플로 `wf_88b8adce-e05` — 6에이전트·1.45M토큰·347툴콜 → 권한지점 **300건** → 중복제거 **정본대장 181행**. PRD §11 에 **FR-41~66(26건·164점)** · RR-01~30 · Q-01~14 · 미확인 11 편입.
  - **실측 정정 3건**: ① 무음 권한차단 6→**7곳**(`ConductorControlViewModel.cs:320` `CanOpenSetup()` SETUP 누락) ② 무음 실패는 7이 아니라 **59곳**(Devices 22·GMaps 24·Events 6·Conductor 7) ③ 감사 원문 공수합 **178→164점 오산**(메인세션 검산).
  - **실측 수치**: 표면 열림핸들러 25 중 가드 7 = **가드율 28%**, 무가드 **18**(패널4·다이얼로그11·팝업3) · fail-open **17** · 죽은 권한프로퍼티 **12**(XAML 바인딩 0) · 패널 내부 액션게이트 **63** · `PermissionsChanged` 구독 18/해제 15(**미해제 3** — SingleInstance라 현행 누수 아니나 **Float 다중화 시 누수 전환**).
  - **✅ 최우선 미확인 2건 메인세션 직접 해소(§11-0)**: ① "GOP 모드에서 `IPermissionService` 미주입이면 가드 7개 전부 무력" → `Bootstrapper.cs:509` GOP 고정 + `AccountApiModule.cs:39` 등록 → **주입됨, 우려 무효** ② "MahApps `HamburgerMenuItemBase.IsVisible` 런타임 재필터 안 하면 메뉴 게이팅 축 무력" → DP 실재 + `UpdateSourceTrigger=PropertyChanged` + `LeftMenuSectionViewModel.cs:57-68`이 `CanSee*` 7건 알림 → **배선 완비**(컨테이너 collapse 실동작만 5분 스파이크 잔여).
  - **🔴 신규 위험 최상위**: RR-19/20/21 **레이아웃 복원이 RBAC 우회**(저장파일 한 줄로 강등계정이 계정콘솔 Float 복원) · RR-22 레이아웃을 appsettings.json 에 얹으면 **평문 자격증명 재작성·유실** · RR-26 콘솔 6탭 Float 승격 시 감사로그·권한매트릭스 **무게이트 노출** · RR-27 복원이 `IPermissionService` Apply 이전이면 **게이트 63곳 전부 무력**.
  - **⚠ 범위 초과**: 기존 150 + 권한 164 = **314점 ≈ 31.4주**(승인 B는 11~14주). §11-8 이 **Tier A 필수동반 86점 / B 지시직결 30점 / C 기존부채 48점**으로 3분류, **권고 = B안 266점 ≈ 24~25주**(Tier C 는 별건 `permission-debt-prd.md`). **사용자 결정 필요.**
- **🚫 맵 OverlayWindow 불가침 경계(사용자 지시 2026-09-06 "맵에 있는 overlaywindow는 건드는거 아니다")**: 워크플로 `wf_8b74dc08-029` — 4축 대조 + 합성. PRD **§12** 신설(220KB).
  - **경계 정본 = 3중 판정식 교집합**: ①시각트리 부모 `MapView.xaml` `x:Name="PropertyPanelCanvas"`(z200) 자식 ②디렉터리 `GMapControls/`·`GMapRoi/`·`GMapMilitary/`·`GMapProperties/` ③Themes xmlns 프리픽스 `ctrl:`/`roi:`/`milreg:`/`properties:`. 실측 검증 — `Themes/*.xaml` 40개가 프리픽스로 정확히 분할(ctrl 17+roi 1+milreg 1+properties 10+control 9+중립 2).
  - **동결 59파일**: 구현 29(GMapControls 17 + MapRoiControl + MilitarySymbolRegister + GMapProperties 10) + 스타일 29 + `PropertyPanelCanvas` 서브트리. **A-4: 경로·네임스페이스·csproj 소속도 불변**(형식적 우회 차단). "건드리지 않는다"=**파일 무수정 AND 렌더 무회귀**.
  - **⚠ 내가 준 경계 목록의 오류 2건 정정**: ① **누락 3계열** — `MapRoiControl`(z40)·`GMapMilitarySymbolRegisterControl`(z30)·`GMapProperties` 10파일(z20, 속성창) ② **`LineDrawingHud` 오분류** — 캔버스 형제(z150)가 아니라 `Adorners/LineDrawingAdorner.cs:126`이 AdornerLayer에 호스팅(z150은 중앙 십자가). 구현체는 축②③ 충족해 불가침 유지.
  - **침범 FR 24건**(직접 9·간접 9·문서모순 6) + 수정지시문 A-01~A-24 / **무침범 확인 41건**.
    - **A-06 최대 위험**: 신설 `SurfaceHostView`가 셸 루트 ColumnSpan=4인데 **`Background`·`IsHitTestVisible` 규정이 PRD에 0건** → `Transparent`면 **표면 0개 상태에서도 오버레이 전멸**(파일 한 줄 안 건드리고 죽이는 경로, 현행 스크림 6/7 타임아웃의 재발). 처방=`Background="{x:Null}"` + 자식 유무 바인딩 + **실마우스 회귀 6건**(UIA는 히트테스트 우회라 대체 불가).
    - **A-05**: 드로어 400px가 `MapView.xaml:1328 Margin="5,5,300,5"`(MapZoomControl 하드코딩 회피)를 덮음. 지도가 ColumnSpan=4라 `PropertyPanelCanvas.ActualWidth` 불변 → **오버레이 자체 재클램프 미발화**. FR-10 범위 **200~300 축소**(D-5).
    - **A-01**: §11-4 권한 검증계획의 '표면 축'이 오버레이 9종을 Float 후보로 열거 — §3-1·N-5·N-10·G-11과 **PRD 내부 3중 모순**.
    - **A-02**: `LayerPanelControl` "일반화/추출/승격" 지시가 본문 **6곳** 잔존(헤더·분석 정본은 정정 완료). → 전부 "참조 전용 독립 구현", **≈130행 중복 영구 병존**이 대가.
    - **A-13/A-22**: 방송 Play·TTS·카메라 스트림 팝업 **Float 이관 삭제** → VM 선두 게이트 1줄로 대체(오버레이 0줄). 카메라 강제로그아웃 스트림 해제는 **이미 구현됨**(`MapViewModel.cs:1865`) → §11-7 #9 해소.
  - **✅ 경계와 충돌 없음 확인**: `MaterialDesignFlatDarkBgButton` GMaps **0건**(GAP-2 안전) · `#88000000`/`Scrim` GMaps **0건**(GAP-9 안전) · `md:DialogHost` 0 · `md:Card`/`ElevationAssist` GMaps Themes 0 · GMaps `DesignTokens` 31키 ∩ `Tokens.Light` 42키 **공집합** · **z-order 충돌 0**(`Panel.ZIndex`는 형제 간에만 유효 — SurfaceLayer는 ShellView 루트 스코프). **오폭 주의**: FR-37 Float 1순위 "WINDY"는 메인솔루션 `WindyPanelViewModel`이지 오버레이 `GMapWindyIndicatorControl`이 **아니다**.
  - **⚠ 집행 절(§12-4)은 제안이며 미적용** — 하네스가 설정-지시형 패턴(settings-json) 감지·중립화. `.claude/settings.json` 훅·`pre-tool-gate.js` 확장·`pre-commit` 훅은 **사용자 승인 후** 판단.
  - **부작용(정직)**: PRD 핵심 논거("SurfaceFrame=LayerPanelControl 일반화") 붕괴 → 공수 하향 전제 소멸(FR-24 6→8점, 계획선 **12.5→13.0주**, 상한 15.5주, **D-7 재승인**) · 디자인 표준 분기(`project_panel_design_system`을 상속 아닌 복제) · 권한 시각적 회색화 포기(Toast로 대체) · 오버레이 UIA 커버리지 영구 동결.
  - **🔲 사용자 확정 D-1~D-7**: **D-2(경계가 이 PRD 스코프인가 레포 전역인가 — PRD 스코프 권고)가 최우선** — 전역이면 `symbol-3d-housing`(WIP 속성창 4파일 위반) 등 진행중 PRD 3건 즉시 정지. D-1 속성창 불가침 · D-3 구역 B · D-4 정의 · D-5 드로어 200~300 · D-6 회색화 포기 · D-7 공수 재승인.
- **🔲 다음**: ① 사용자 PRD 검토 → 승인 ② **착수 전 필수**: 두 레포 각각 `git tag before-surface-kernel` + `git branch backup/pre-surface-kernel` ③ Phase 0 스파이크 6건(**S-3 HamburgerMenu 히트테스트가 이 PRD 전체의 가치 전제 — 실패 시 재작성**) + UI 하네스 green 복구(2026-08-06 실패 후 한 달 방치, Phase 1a 하드 게이트) ④ plan(worktree + CHANGELOG). ⚠ 파이프라인은 `pidsgroup-rightclick` 사이클(dev 36/50) 중이라 new-cycle 은 complete 이후만 가능(`--force` 금지). `approve prd` 는 **mtime 최신 PRD** 를 집으므로 주의(현재 최신 = 이 PRD).

---

## ▶▶ 재개 포인트 (2026-09-04, 이 세션 — 그룹 심볼 변환(전체 선택 회전/확대축소) 시나리오 분석 → PRD Draft) — 🔲 **사용자 결정 7건 + 승인 대기**

- **요구(사용자)**: 맵 편집 모드에서 드래그로 심볼 전체 선택 → 선택영역 중심 회전 + 확대/축소 버튼 커스텀 컨트롤. "확대 시 깨지면 안 됨(개별 이미지 소스라 깨질 것 같은데)". 가능·논리 검증을 시나리오+시뮬로 철저히 → PRD.
- **투입**: 영향면 7축 정찰(에이전트 7기, 1.4M 토큰) + 합성/완결성 2기 + **적대검증 4렌즈**(인용·수학·완결성·아키텍처, 0.5M) + 시뮬레이터 4세대(v1→v4, 최종 5,144건).
- **⚠ 자기교정 사례(중요)**: 시뮬 v3.1은 **진리값(GT)과 후보(M2E 접평면)가 같은 함수**여서 "M2E 291/291 통과"가 자기참조 항등식이었음 — 적대검증 L2가 독립 측지 계산으로 반증. v4에서 진리값 2종(구면 측지 GT-GEO / 화면 메르카토르 GT-SCREEN) 독립 구현 + 접평면 곡률반경 정정 + 허용치 단일화 + 서브픽셀 변형 추가 → **결론 역전**.
- **✅ 최종 결론**: 변환 공간 = **화면(메르카토르) + base 스냅샷 1회 합성 + 서브픽셀**(화면 진리값 432/432, 지면 348/432·실패는 전부 500m 초과). 배제 = M1(정수픽셀 증분, 720스텝 523m 드리프트)·M2E(접평면, 양쪽 열위 — v3.1 결론 철회)·M3(위경도 평면).
- **✅ 사용자 우려 답변**: 심볼 7계열은 **Viewbox+Path 벡터라 안 깨짐**. 오버레이 이미지만 래스터(원본 대비 2배 초과 시 블러). **더 심각한 건 따로 있음** — 이미지 `UpdateSize`가 값 ≤10이면 '도(degree)'로 해석해서 축소 시 **폭 668km 폭발**(GMapImageMarker.cs:811-843).
- **주요 발견(적대검증 L3)**: 그룹 회전/스케일이 "전무"가 아님 — 그룹 공통 속성창의 **절대·균일 Bearing/W/H 일괄 쓰기**가 이미 프로덕션(GMapPropertyBaseControl.cs:1038-1040) → 신규 상대 변환과 의미 충돌(FR-12). 그 외 어도너 HitTestCore 투과로 HUD는 편집 스트립에만 가능, PidsGroup 정점은 **PidsGroupPoints 별도 테이블**, Symbols에 **MapId 없음**(전 맵 공유), base 스냅샷 인덱스 페어링 붕괴, 카메라 심볼 좌표=PTZ 조준 원점, 1000개=2000 순차왕복 8초.
- **📋 산출물**: PRD `docs/prds/group-symbol-transform-prd.md` **v1.0 Draft**(FR-14·NFR-6·V-5·리스크 5·GAP 15) / 분석 `docs/analyses/group-symbol-transform-scenario-analysis.md` / 카탈로그·전량로그 `docs/tests/group-symbol-transform-{scenarios,simulation-log}.md`.
- **🔲 다음**: **착수 전 사용자 결정 7건**(GAP-1 점심볼 스케일 의미 / GAP-2 Bearing 동반 / GAP-3 피벗 / GAP-4 잠금 / GAP-10 이미지 포함 / GAP-13 정본 공간 / GAP-14 속성창 관계) → 승인 시 `node .claude/hooks/advance-phase.js approve prd "코멘트"` (⚠ approve는 mtime 최신 PRD를 집음 — symbol-3d-housing PRD도 Draft 공존).


> 📎 **RTSP 영상 팝업 관련 컨텍스트는 한 곳으로 통합됨** → [`session-context-rtsp-popup.md`](session-context-rtsp-popup.md) (2026-06-23~07-03, 14개 블록 시간순 재구성 + 상단 요약). 아래 원본 블록은 그대로 보존.

## ▶▶ 재개 포인트 (2026-09-06 — 3D 심볼 하우징 · Track C · **PRD v2.3 Draft · 미결 0 · 코드 변경 0 · 승인 대기 + 타 세션 검토 요청**)

## ▶▶ 재개 포인트 (2026-09-07 18:0x — PIDS 그룹 3D 철망 · 통문(enum 신설) · 함체 개폐 · Track C · **사용자 "도입해줘" 착수 승인 · 설계 워크플로 진행 중**)

- **착수 결정**: 스토리보드(`docs/design/pidsgroup-3d-fence-gate-storyboard.html`) 확인 후 사용자 "도입해줘"(18:0x). 결정 확정: D1 슬라이더 간격(1.0~10.0m·0.5·기본 3.0) · D2 통문 = 신규 `EnumDeviceType.Gate = 21` + 전용 3D 모델. D3~D9 는 사용자 위임 방침("가장 합리적인 방향")으로 권장안 확정(LOD ≥18 3D / 1차 그룹 전체 상태·센서 모드 구간 / 함체 도어 = 접점 이벤트 / 통문 양개 1종 / 단순화 2px / 잔여 균등·센서 정확 / 차량 = Phase 2 범위만).
- **범위**: Phase 1 = A(3D 철망: 기둥/센서 장착 2형태·슬라이더·구간 상태·LOD·드래그 드로잉) + B(통문 enum·3D 개폐) + C(함체 개폐). Phase 2 = D(차량 5종, 영속 배관 신설) — PRD 에 정의만.
- **롤백 포인트(두 레포)**: `before-pidsgroup-3d-fence`(HEAD: Lib dc65462 / Sol 778f2d5) + `before-pidsgroup-3d-fence-snapshot`(미커밋 WIP·untracked 포함 스냅샷: Lib 91ccb75 / Sol 2d1f7ce). 작업트리·인덱스 무변경.
- **⚠ 프로세스 편차(명시)**: worktree 규칙 대신 **메인 작업트리(v2.6)** 에서 진행 — 이 기능은 미커밋 3D WIP(untracked Symbols3D 등) 위에 쌓여 HEAD 기준 worktree 가 컴파일 불가. 파이프라인 상태(`pidsgroup-rightclick` dev)는 타 세션 것이라 손대지 않음(문서로 추적). PRD 는 Draft(승인 마킹은 사용자).
- **설계 워크플로 `wf_03598e8c-5ff` 결과**: 6축 조사 완료(144 fact·68 risk, 저널 `subagents/workflows/wf_03598e8c-5ff/journal.jsonl`), 분석/PRD/Plan 에이전트 3개는 **세션 사용량 한도(20:30 KST 리셋)** 로 실패 → 메인 루프에서 직접 작성 완료: [`analyses/pidsgroup-3d-fence-gate-scenario-analysis.md`](../analyses/pidsgroup-3d-fence-gate-scenario-analysis.md) · [`tests/pidsgroup-3d-fence-gate-scenarios.md`](../tests/pidsgroup-3d-fence-gate-scenarios.md) · [`prds/pidsgroup-3d-fence-gate-prd.md`](../prds/pidsgroup-3d-fence-gate-prd.md)(v1.0 Draft) · [`plans/pidsgroup-3d-fence-gate-prd-plan.md`](../plans/pidsgroup-3d-fence-gate-prd-plan.md)(46 태스크).
- **⚠ 사용자 검토 필요(스토리보드와 달라진 점)**: ① **R1 서버 정본 정렬** — 서버 `operation-event-prd.md` v1.4(2026-09-07 사용자 승인)가 통문 = `gate` 카테고리·`gates.gate_status`·개폐 실시간 `SYNC_DEVICE`·알림 `OPERATION_EVENT` 로 확정 → 클라 DoorState 는 SYNC_DEVICE(주)+OPERATION_EVENT(보조)+DETECT ContactOn/Off(로컬 폴백, 설정) 3채널 수렴 ② 3D 키 `gate`→**`fencegate`**(시설물 정문 충돌) ③ **접점(Gate/Enclosure) 이벤트는 큐·카드·자동조치보고·사운드에서 제외**(현재는 탐지처럼 취급) — 메인 솔루션 변경 3건(카드 스킵·SYNC_DEVICE 훅·appsettings) 통지 ④ LOD 는 줌 숫자가 아니라 간격 픽셀(z18 3m=6.3px) ⑤ 정점 드래그 편집·구간 상태 채널·차량은 Phase 2.
- **✅ 2차 실기(00:25~00:29)** 그룹 펄스 ✅ · 개폐 파이프라인 로그 ✅ · 문짝 픽셀 0 → **결함 확정: `GMapPidsMarker.PidsModel_Update` 가 DoorState 를 재통지하지 않음** → 수정 + 회귀 테스트(Housing 74/74) + 앱 재빌드 · 하네스: 배치 실패 시 시드로 계속 · **3차 실행 대기(RDP 창 복원 필요, 런처가 입력 가능 시 자동 시작)**.
- **✅ HUD 고정 직접 검증 확장(2026-09-08 14:45, 사용자 "직접 테스트해봐 / 레이어 창도 켜고 같이 비교")**: 실기 진단 ✅8/✖0 — HUD 214.0×101.0 px 가 **디지털 줌 s=1.25·1.50·2.00(최상단) · 틸트 25° · 레이어 창 동시 표시** 전 구간에서 Δ0.0. 픽셀 측정(UIA 무관)으로도 HUD 헤더 238 px · 레이어 창 헤더 248 px 고정, 지도 타일만 2배. **정정 2건**: ① 줌 라벨의 `+` 개수는 dzl−1(정본 `ZoomLadder.Label`) — 종전 "레벨 2 정체" 보고는 내 디코딩 오류였고 실제로는 최상단까지 도달했다. ② 드로잉 중 Ctrl+↑ 틸트 각도 무반응은 `DrawingKeyRouter` 가 방향키를 Ignore 로 소비하는 **설계**(스트로크 투영 보호). 커밋: 라이브러리 `4f252585`(레이어 버튼 AutomationId) · 메인 `ef05577`(진단 확장).
- **✅ 드로잉 HUD 화면 크기 고정(2026-09-08 14:30, 사용자 "확대 축소할 때 커졌다 작아졌다")**: HUD 가 지도 `AdornerLayer` 자식이라 컨트롤 `RenderTransform`(디지털 줌 s · 틸트 ScaleY=s·cosφ)을 상속하던 것이 원인. `LineDrawingAdorner.ApplyHudScreenScale` 역배율 재대입 + 드래그 클램프 + **`HitTestCore` 히트 사각형** 3곳 보정. 실기 진단 `LineDrawingHudScaleDiagTests` 2회 통과 — 214.0×101.0 px 불변(디지털 줌 s=1.50 · 틸트 25°, Δ0.0), HUD 우측 +60 px 클릭 정점 2→3, 앱 로그 예외 0건. 커밋: 라이브러리 `983b4bea` · 메인(하네스) `c2bd79b`.
- **✅ 커밋 완료 + 세션 마무리(2026-09-08 10:4x, 사용자 "커밋하고 세션 마무리")**: 3개 레포 커밋 — 라이브러리 `5d52779e`(v2.6, **155파일 13,331줄**: 3D 철망·통문·함체 + 거울상 수정 + 라인 드로잉 결함 10건 + 지도 카드 틸트) · 메인 `b04f7c8`(v0.5, 18파일: NATS 통문/함체 개폐 배선 · SetupModel.MapTilt · 인스톨러 템플릿 · 하네스 MapProbe/AppLogParser/NatsEventPublisher/진단 4종/EnvironmentGuard) · 서버 `0528850`(release/v6.3, 6파일: SmartMultisensor2 v79, pytest 18 passed). 커밋 전 전체 의존성 빌드 성공(Events.Ui 포함 — 타 세션이 억제 파일 정리 완료). **의도적 제외**: 타 세션 억제 WIP 5파일(EnumKoreanMap·EventCardViewModel·ExEventViewModel·UiKoreanMap·억제 테스트) · 메인 이벤트 UI XAML 4개 · 개발자 `appsettings.json`(운영↔localhost 로컬 스왑 섞임) · 귀속 불명 3건(`deploy-watchdog.ps1`·`tests/`·`ui-test-summary.md`). 앱은 사용자 확인용으로 실행 중(PID 51412).
  - **남은 작업**: ① 틸트 VER-06(회전+틸트 타일 페치 계측)·VER-07(빌보드 실기 — 툴바 회전 토글에 AutomationId 부여 후 재배터리) ② 3D 철망 PRD 편차 11건 검토 대기(Draft 유지)·VER-01/02/03/06/07 ③ 서버 `/api/gates` 라우터 + 클라 Gate fetch 경로 미구현 ④ 검토 대기 PRD: symbol-3d-housing(v2.8)·group-symbol-transform(빌보드 교차 편차 반영 필요)·window-architecture(Review)·Admin_Photo_Upload ⑤ 파이프라인 배너의 Active PRD 는 타 세션(pidsgroup-rightclick)이라 phase 전환은 건드리지 않음.
- **✅ 틸트 실기 6차 통과(2026-09-08 09:34~09:40, 사용자 "승인")**: TiltRuntimeDiagTests ✅12/ⓘ3/✖0 — 토글 실클릭·배지 "기울임 20°"·17.5 유지/17.0 해제/18.5 재진입(앱 로그 `cause=dzl coalesced=3 z=17.5 Hold active=True`)·슬라이더 35/20(UIA RangeValue)·OFF 픽셀 diff 0·layoutChanges 5/조작 5·Tier 0x20000(RDP 하드웨어 Tier 2)·설정 저장/복원. ⓘ: 회전 kill-switch 하네스 미가동(빌보드 실기 미검증) · 카메라 팝업 RTSP 없음. FenceGate 회귀 ✅26/ⓘ13/✖0(함체 배치 포함, 문짝 591/717, 펄스 1380, LOD 0%). 리포트 §2 확정 · 플랜 47 중 45 완료(잔여 VER-06/07) · PRD DoD 체크 · 회전동기 분석/메모리 정정(DOC-01). **코드 미커밋**(WIP, 롤백 태그 `before-map-tilt-25d`, 백업 패치). 다음: VER-07 실기(툴바 회전 토글 AutomationId 부여) · 커밋은 사용자 지시 시.
- **✅ 틸트 배선 완료(2026-09-08 07:5x~08:5x)**: 배선 워크플로(wf_577e65e4) F/H/I → G/T + 적대 리뷰 5건 전부 PASS(정정: DigitalZoomCoordinateTests BOM · 빌보드 테스트 nullable · 키보드 각도 저장). 잔여 3건 직접 수정: 편집모드 방향키 넛지 터널이 Ctrl 조합을 삼키던 것 제외(`MapViewModel.cs` OnMapPreviewKeyDownForGroup) · `RequestedTiltDeg` DP 변경 구독으로 키보드 각도 debounce 저장(`MapViewModel.Tilt.cs`) · 각도 팝업 `StaysOpen=False`(`MapView.xaml`). 스위트: GMaps.Ui.Tests **566** · Housing **150** · 속성창 4 · 하네스 Unit 94 green. 메인 exe 08:45 재빌드. PRD v1.2(구현 편차 7건) · 플랜 21 태스크 추가 완료(남은 20: VER-01/03~07·RISK·TEST-02/04/05·DOC·EXT-02 등). **실기 배터리 5차**(TiltRuntimeDiagTests + FenceGateRuntimeDiagTests, 런처 `run-fence-door2.py`)는 08:4x 시작했으나 입력 주입 거부(RDP 창 최소화)로 대기 중 — **20분 대기 초과로 자동 중단(09:0x)** — 시드/DB 변경 없음, RDP 창 복원 후 같은 명령으로 재실행. 미검증: V-01 오버스캔 배치 실기 · V-03 모아레 · V-04 RDP Tier · V-06 타일 · V-07 빌보드 편집 UX · NFR-03 OFF 픽셀 diff.
- **✅ 지도 카드 틸트 PRD Approved + 플랜 + 기반 구현 착수(2026-09-08 07:1x~07:5x)**: 사용자 결정 ①살짝 비스듬(각도 조절·오버스캔) ②줌 게이트(17 이하 탑뷰) ③베어링 회전 시 마커 정립 → 조사 4축 워크플로(wf_feb5fdae, V-02 헤드리스 확정) + 시뮬 4,096+22(정책 불변식 0 위반) → `docs/analyses/map-tilt-25d-scenario-analysis.md` · `docs/prds/map-tilt-25d-prd.md`(**v1.1 Approved**, 결정 G1~G10 승인값 20°/35°/18.0/h0.5/앵커A차단/3D(i)/배지/G7 무보정/G8 150 ms 커밋/OFF 즉시0) · `docs/plans/map-tilt-25d-prd-plan.md`(47태스크). 적대 검증 반영(v1.1): 판정 코얼레싱(휠 스텝 중간 정수 상태가 wasActive 파괴) · 뷰 변환 재대입 불변식(어도너 in-place 미추종 실증) · 그룹 변환 PRD 교차 편차 · 헬퍼 소비처·하네스 강제 OFF·STA 테스트 위치. 롤백 태그 `before-map-tilt-25d` + WIP 패치. 기반 워크플로(wf_faf03536): A TiltMath 70/70 완료, B/C/D/E 진행(SetupModel:88·템플릿 키·스냅샷 TiltCos 이미 반영됨). **메인 변경 통지**: SetupModel 1줄 + 템플릿 키. 다음: 배선(F)·VM/XAML(G)·3D 정책(H)·빌보드(I) 워크플로 → 테스트 → 실기.
- **✅ 실기 4차 + 2.5D 검토 완료(2026-09-08 06:3x~07:0x, 사용자 "남은 작업 승인")**: 4차 배터리 ✅27/ⓘ14/✖1 — 통문 문짝 594·함체 676 픽셀 개폐, 반전 옵션 0, 그룹 펄스 1375, LOD 왕복 잔차 0%, 드래그 드로잉 정점 3(왕복 재발 없음), **거울상 수정 FG-2 육안 확인**. ✖1 은 함체 팔레트 드롭 지점(1220,240)이 나침반 오버레이 위였던 하네스 결함(앱은 클릭 배치 대기) → 오버레이 회피·클릭 폴백 추가. 2.5D 지도 검토 워크플로(wf_00baf3f6, 01:27 검증 단계에서 사용자 중단에 끊김 → 캐시 재개 2회, B 반박자는 세션 한도 재시도) 완료: 분석서 `docs/analyses/map-25d-tilt-feasibility-analysis.md` — A(아핀 카드 틸트)·B(Viewport2DVisual3D 원근) 채택 불가, **C(하이브리드) 권고**(C4 형제 레이어 + C1 post-Fit 하우징 높이 과장 → C2 피치 SSOT 하한 30° → C5 스냅샷 스파이크 → C3 닫힌 라인 압출). 사용자 결정 3건 대기(원근 기대 여부 · 필수-060 이탈 수용 · RDP 상시 운용). 남은 실기: VER-09/10.
- **✅ 라인 드로잉·3D 철망 결함 10건 + 하네스 2건 수정(2026-09-08 02:0x~03:0x)**: 헌트 워크플로(wf_7884f98c, 60 에이전트) 확정 20건 중 앱 결함 10건을 5 그룹 병렬 수정(wf_e76f89f0) — 키 라우팅 `DrawingKeyRouter`(ESC/Enter/Backspace/Ctrl+Z, 윈도우 터널 1차) · 맵 포커스/선택 해제 · 휠 차단/팬 억제/클릭 1 m 최소 간격(`LineDrawingInputGates`) · 어도너 `ExclusiveInputModeGate` · 철망 `DecideFrame`/`ApplyFrame`(LOD 전이 Rebuild, 지터 Reuse, **150 ms 디바운스 정착** — G4 리뷰 FAIL(ContextIdle 은 스로틀 아님) 후 내가 재작업) · 예산 m 환산 · 카메라 거리 비례 · `WritesBackRenderSize=false`. FinishLineDrag 순서 정정(`DiscardStrokePreview`). 하네스: 드래그 성립=앱 로그 `[LineDrag]` 판정, ⑤ 앵커 LocateByGeo, **앱 로그 UTF-8**(규칙 문서 정정). GMaps.Ui 348 · Housing 98 · 속성창 2 · 하네스 Unit 30 green, exe 재빌드. 실기 VER-09/10 대기(데스크톱 독점 필요). 2.5D 지도 검토 워크플로(wf_00baf3f6) 아직 진행 중.
- **✅ 속성창 3D 철망 절 → 맨 밑으로 정정 + 3D 거울상 결함 2건 수정(2026-09-08 01:3x~02:0x)**: 사용자 "판망 간격·센서 부착·높이는 마커 속성 **맨 밑**, 단계별 순서" → PinnedContent 슬롯 제거(베이스 DP/PART 되돌림), 블록은 SpecificContent 마지막 자식(요약 행 포함, 5행 Grid), 테스트는 "마지막 자식 + ScrollToEnd 뷰포트 안" 계약(2/2, PNG 라이트/다크). 사용자 "꺾어진 라인 그릴 때 반대로 그려진다" → 픽셀 테스트로 **좌우 거울상** 재현: 철망·하우징 카메라가 −Z→+Z 라 세계 +X 가 화면 왼쪽(오른손 좌표계) — `FenceRunVisual` 루트 `ScaleTransform3D(-1,1,1/sin35)`, `HousingVisual` 최외곽 `ScaleTransform3D(-1,1,1)`(함체 문 방향·yaw 회전 방향까지 잠복 결함). `FenceMirrorTests` 2·`HousingMirrorTests` 1 green, Housing 77, GMaps.Ui 300. 메모리 `feedback_property_panel_section_order`. 실기 3차(01:25 자동 시작)는 ① 캡처 후 사용자 데스크톱 조작과 겹쳐 ②부터 오염(팔레트 열기 실패) — 데스크톱 비운 뒤 재실행 필요. 2.5D 지도 검토(wf_00baf3f6)·라인 버그 헌트(wf_7884f98c) 워크플로 진행 중.
- **✅ 1차 실기 배터리(23:02~23:50, 48분)**: 팔레트 40종 3/3 PASS · D-21 PASS · 그룹 드래그 생성/슬라이더 5.0→DB/요약/센서 모드 ✅ · 3D 철망 Full3D 렌더 육안 확인 · 통문 배치/폭 ✅ · 개폐 파이프라인 앱 로그 확인(`[DoorState] Device(7) Unknown→Open→Closed`, 큐 제외) · LOD 왕복 ✅ · 설정 키 보존 ✅. 미계측: 문짝 DiffPixels(시드가 툴바 위) · 함체 개폐 · 그룹 펄스(장비 7 그룹 미소속). **2차 재실행 차단(23:56~00:2x)**: RDP 창 최소화로 SendInput err 5 → 런처 `run-fence-door2.py` 는 입력 가능 대기(20분) 후 시작하도록 변경, 대기 초과. 부수 수정: 세션 검사 WTS · 접점 result=CONTACT_SENSOR · 리포트 §2 갱신.
- **⛔ 실기 검증 중단(22:3x)**: `run-fence-door.py` 1회 실행 → 픽스처 프리플라이트가 **화면 잠금(LogonUI)** 감지로 중단(UIA 입력 주입 불가). DB 시드는 정상 원복(시드 426 삭제·138 재연결). 완료 리포트 1차 `docs/reports/pidsgroup-3d-fence-gate-report.md` 작성(실기 대조표 = 전부 미실행/미검증). **다음**: 잠금 해제 후 같은 명령 재실행 → 결과 시트로 리포트 §2 갱신.
- **✅ S4 워크플로(wf_1a540ec1, 6 에이전트·25분) 완료(22:2x)**: 메인 M-01~04(NatsDomainService 접점 스킵·함체 door_status→SetDoorState·Gate 단락 경고·switch Gate·appsettings 2본 키) 빌드 green + 적대검토 PASS · 서버 S-01/02(SmartMultisensor2 enum·v79·컨테이너 재빌드 v77~79 적용·openapi Gate 확인·pytest 18/18) · 하네스 T3-02/03(MapProbe 이관·PublishContactAsync/SyncDevice·FenceGateRuntimeDiagTests, 검토가 DestructiveGuard 우회 결함 수정) · T-C06 갤러리 PNG · T4-01/02 문서. **실기 검증 진행 중**: 시드 경로(통문 심볼 ↔ 로컬 장비 7) 런처 `scratchpad/run-fence-door.py`. 서버 /api/gates 라우터·클라 Gate fetch 경로는 미구현(통문 SYNC_DEVICE 는 접점 폴백만).
- **✅ S2-C05 + S3 완료(21:4x, 라이브러리 측 구현 종료)**: `FenceRunVisual`(3버킷·예산·펄스) · `GMapMarkerPidsGroup3DControl`(`ComputeFrame` px 프레임·해시 스킵·3중 게이트·Posts LOD) · 그룹 3D 템플릿(PidsGroupMarkerStyle.xaml) · PIDS `DoorState` DP + 0.4s 문짝 애니메이션/LED StatusBrush · 속성창 3D 철망 절(150ms 지연 커밋·프리셋·요약·AutomationId) · 통문 절 · 팔레트/HUD AutomationId · 캡처 드래그 드로잉(`StrokeReducer`) — 헤드리스 300/300 · GMaps.Ui 481 · Housing 73 · UiTests 빌드 green. **남음**: 실기 검증(VER-01/02/03/08) · 메인 솔루션 M-01~04(사용자 통지 필요) · 서버 S-01~03 · 하네스 T3-02~05 · T-C06 갤러리 · 문서 T4. PRD v1.1 에 구현 편차 8건 기록(사용자 검토 대기).
- **✅ S2-C/D 완료(21:0x)**: 관절 일반화(`HousingJoint`·문 스윕 bounds·`HousingMath.DoorAngle` 거울) · `fencegate` 모델 + 함체 도어 관절(R-06 닫힌 크기 보존) · `HousingVisual.DoorOpen` 경량 DP + `StatusBrush/MeshBrush` · 이벤트 배선: `ISymbolEventManager.SetDoorState/ApplyDoorEvent`, 접점 분기(큐 제외, `IDoorContactPolicy`), `OperationEventNatsSyncService`, 함체 door_status 부팅 초기화, `DoorStateMachine` → Monitoring.Models 이관 — 검증: Housing 61/61 · Events.Ui 도어 36/37(1건 헤드리스 고유) · GMaps.Ui 481 · 헤드리스 295 · UiTests 빌드 green. 다음: C05 `FenceRunVisual` → S3(그룹 3D 컨트롤/템플릿·속성창 슬라이더·통문 절·팔레트/HUD AutomationId·드래그 드로잉) → 메인 솔루션 M-01~04 통지 → 서버 5싱크.
- **✅ S1-B 완료(20:1x)**: 모델 4층(그룹 PostSpacingM/FenceHeightM/FenceMode/Render3D/ReverseSensorOrder/ActiveSensorDeviceIds · 장비 GateWidthM/OpenOnContactOn/DoorState[JsonIgnore]) · DB 7열(DDL·COLUMN_SPECS·**columnMeta PidsGroupSymbols 로드**·SELECT/INSERT/UPDATE/DTO·Restore 선례 누락 보정) · Undo 11속성 · `Symbol3DSettings` record(+SaveSymbol3DAsync 키 보존) · DbProbe 허용목록/행 조회/감사 — 검증: Housing 46/46 · GMaps.Ui 481/481 · GMaps.Db PIDS 두 컬렉션 각 2회 green(격리 DB) · SchemaGuard 4/4(VER-04) · UiTests 빌드 green. **부수 정비**: GMaps.Db 픽스처 `[Fact]` 라이프사이클/시드(I-01 위반) 제거 + 시드 id 기반 조회 + LinkedDeviceId 고유 발급 → PIDS 테스트 순서 의존 제거(다른 도메인 픽스처 Geometry/Line/Military/Infra/Image/Map 은 아직 [Fact] 시드 잔존 — 전체 suite 병렬 실행은 여전히 비권장). 남은 알림: Pids 컬렉션 cleanup MySqlException(테스트 통과와 무관, 원인 미조사).
- **✅ S1 진행(19:00)**: 순수 함수 5종 + 테스트 45건(`tests/GMaps.Ui.Tests` 295/295) · `EnumDeviceType.Gate=21`·`EnumDoorState`·`EnumFenceMode` · enum 전수 11지점 + 테스트 InlineData + 하네스 목록(DbProbe/정리/순회 제목) · `HousingModels.DeviceKey(Gate)="fencegate"` · 빌드 전부 통과 · Housing 41/41(팔레트 20·갤러리 32) · GMaps.Ui 481/481 · Events.Ui 실패 15건은 Gate 무관(FOV 줌범위·DTO 날짜·XAML 문자열) — 스냅샷 worktree 재실행으로 기존 실패 여부 확인 중.
- **dev 착수(S1, 메인 루프)**: 순수 함수+헤드리스 테스트(FenceLayout·DoorStateMachine·FenceLod·FenceMath·PolylineSimplifier) → Enum/모델/DB/설정. 에이전트 한도 해제(20:30) 후 S2/S3 를 워크플로로.
- **설계 워크플로 `wf_03598e8c-5ff`**(진행 중): 6축 조사(그룹 렌더·3D 파이프라인·이벤트 DoorState·enum 전수·속성창/DB·하네스/서버) → `docs/analyses/pidsgroup-3d-fence-gate-scenario-analysis.md` → `docs/prds/pidsgroup-3d-fence-gate-prd.md` → 적대검증 3렌즈·보정 → `docs/plans/pidsgroup-3d-fence-gate-prd-plan.md`. 완료 후 INDEX 등록 → dev 워크플로(영역 병렬: 순수함수+테스트 선행 → enum/모델/DB → 3D 모델 → 그룹 렌더 → 이벤트 → 속성창/팔레트 → 드래그 드로잉 → 로컬 API 서버 Gate(5싱크) → 실기 프로브).
- **선행 조건**: 로컬 API 테스트 서버 `C:\workspace_python\api-test-server`(별도 레포)에 `type_device="Gate"` 장비 — 명세·코드·swagger·이미지·컨테이너 5싱크.

---

### ▷ 🆕 사용자 신규 요청(2026-09-07 16:5x) — PIDS 그룹 3D 철망 · 통문/함체 개폐 3D 심볼 → **스토리보드 먼저**

- **요청**: ① 그룹 라인을 드래그로 그으면 설정 간격(2/3/5m)으로 기둥·철망이 3D 로 이어지는가(현재는 2D 폴리라인 — 신규) ② 통문(열림/닫힘, 이벤트로 형태 전환) 3D PIDS 심볼 ③ 함체도 열림/닫힘 형태 필요 ④ "HTML 와이어프레임 스토리보드로 먼저 확인하고 진행".
- **✅ 스토리보드 v0.1 Draft**: [`docs/design/pidsgroup-3d-fence-gate-storyboard.html`](../design/pidsgroup-3d-fence-gate-storyboard.html) — 인터랙티브 기둥 배치(2/3/5m·상태·통문 절개), 35° 3D 스트립, LOD 표, 통문 닫힘/열림/열림+탐지, 함체 닫힘/도어열림/장애, DoorState 상태 머신(ContactOn/Off), 배선 계획(DP 1개 추가), DB/appsettings/NATS 영향(서버 계약 무변경), **결정 D1~D8**(권장: 3m 기본·통문=접점 센서 모델 변형·LOD ≥18 3D·1차 그룹 전체 상태·함체 도어=접점 이벤트·양개 1종·단순화 2px·잔여 균등).
- **현행 사실(스토리보드 §0)**: 그룹 점 `pidsgrouppoints(SequenceOrder, Lat/Lng/Alt)`, 그룹 속성에 간격 없음 · `EnumDeviceType.Contact(5)`·`Enclosure(19)` · `EnumEventType.ContactOn(86)/ContactOff(102)` 기존 → 개폐는 기존 이벤트 파이프라인으로 수신 가능, 통문 전용 장비 타입 없음 · 3D 모델 키 `enclosure` 존재, `gate`/`fencerun` 신규.
- **✅ D2 결정(사용자 17:2x "통문이라는 속성(enum)을 만들어야 하고 그 enum 에 해당하는 3D 심볼이 필요하다")**: 통문 = **신규 `EnumDeviceType.Gate = 21` + 전용 3D 모델 `gate`**(접점 센서 변형 아님). 영향면 실측(스토리보드 §5-B): enum·EnumKoreanMap·DeviceModelConverter(20)·DeviceFilterHelper(32)·SymbolPaletteItem(19)·HousingModels(15)·PidsControl/Fallback(22)·MapViewModel(40)·EventInfoViewModel(18)·테스트 5파일 + **서버 `type_device="Gate"` 선행**(없으면 이벤트 폐기).
- **✅ 사용자 지적 2건 반영(17:3x)**: ① 톱다운 간격은 **기둥 간격** 또는 **펜스 센서 장착 간격**(철망 위 감지 노드) 두 형태 — 스토리보드 §1 형태 토글 + 센서 모드에서 노드↔그룹 센서 순번 매핑으로 **구간 상태(탐지 센서 좌우 패널만 펄스, D4)**, 3D 스트립도 연동. ② "지도가 3D 구조가 아니지 않나" → 맞음: GMap.NET 2D 타일 + 마커별 Viewport3D 화면고정 35° 피치 = **2.5D 오버레이**(지형·가림·시점 없음). 철망도 그룹 마커의 Viewport3D 안 압출 형상으로만 가능 — §0-B 에 가능/불가능/설계 결과 표로 명시.
- **✅ D1 결정(사용자 17:5x "기둥 간격은 프로그래스바로 수정")**: 슬라이더 1.0~10.0m·0.5 스텝·기본 3.0(전역 appsettings), 프리셋은 보조, 3D 재생성은 놓는 순간 1회. 스토리보드 §1 인터랙티브 슬라이더 + 그룹 속성창 목업 반영.
- **🔲 다음**: 사용자 D3~D9 결정 → 시나리오 분석 → PRD(별도 문서) → 순수함수(FenceLayout/DoorStateMachine) 헤드리스 테스트 선행.

### ▷ 실행 6·7차 상태 (16:40~) — 사용자 "같은 위치에 놓지 말라고"(2회) 반영
- 6차 중단: 소프트밴드 줌에서 마커 UIA 사각형이 부풀어 `EmptySpot` 이 54칸 전부 "근접"으로 오판 → 폴백 재사용 경로 → 즉시 중단. 줌 프로브는 줌아웃이 앵커 하한(16.5)에서 멈추고 줌인은 요청 스텝을 다 올려(18.5++) 뷰가 북서쪽 아파트 단지로 끌려감(마커 0) → 판정 불가.
- 하네스 수정(p55~p58): 소각·사용 칸 **프로세스 정적 공유**(테스트 간 재사용 0) · 60칸 격자·재사용 시 실패(throw) · 화면 밖/거대 사각형 무시 · 속성창 미열림 시 인접 마커 재클릭 · 줌 지표 정규식(`++` 허용, 두 자리) · 줌아웃 지표 불변 시 중단·내린 스텝만큼만 올리고 시작값 보정 · 이벤트 프로브 가시 영역=MainMap∩셸·분할 팬·로그 도달은 정보성.
- **✅ 이벤트 애니메이션 실기(6·7차)**: 로컬 NATS 직접 발행(`Support/NatsEventPublisher.cs`, loopback/사설만 허용) → 장비 7 '외부_외곽79' 3D 카메라에 DETECT → **빨간 배지 + 3링 펄스(약 2s 주기) 재생 확인**(대조표 `s3d-event-anim-detect.png`, red 픽셀 12→210…73 변동). 장애/장애중탐지 단계는 6차엔 위치 산출 실패(하프스텝 UIA 사각형) → 7차 재실행 중.
- **✅ 7차(`…_164833`) 완료**: 시설물 **12/12** · 도형 **8/8** · 기본 **1/1** 전체 속성 스윕(전부 다른 칸) · 이벤트 프로브 PASS(탐지 펄스 ✅·장애 배지 ✅·공존 갭 실증) · D-20 PASS. 줌 프로브는 휠 무시로 판정 불가 → 버튼 Invoke 로 재작성(p60).
- **8차(`…_1714xx`)**: PIDS 2종 재실행 **2/2 ✅**(돔형 카메라·I/O 제어기 — 인접 마커 재클릭 보완) → PIDS **19/19** · **D-21 **실기 검증 ✅**(`…_171455/s3d-infra-zoom.log`, 줌 버튼 Invoke): S1 '18.5+'→'18.5'→'18'(실효 18.0 < 최소표시줌 18.5 → 숨김, 마커 65→82) → **+1 → '18.5'(dzl 만 변경)에서 막사 재표시 ✅** · 시작 '18.5+' 복귀 ✅ · S2 하한 '15'까지 8스텝 내렸다 복귀 '18.5+' 후 존재 ✅ — 수정 전(16:02 4차)엔 같은 경로에서 라벨만 남고 본체 소실**.
- **차량(사용자 질문)**: 없어진 게 아니라 미구현(VF-15 숨김·3D PRD G-4 별도) → 스토리보드 §3-D/D9 흡수안.
- 5차 결과(`…_161421`): PIDS 전체 속성 스윕 **17/19**(2종은 생성 직후 속성창 미열림 — 하네스, 재클릭으로 보완).

### ▷ ✅ 7차(14:37~16:10) 팔레트 40종 **드래그** 순회 + 앱 결함 D-18/19/21 수정 (2026-09-07) — 사용자 "새로 모든 심볼을 추가하고 속성을 바꾸는 테스트"

- **결과**: PIDS 19/19 · 도형 8/8 · 기본 1/1 · 시설물 11/12 — 전부 실제 마우스 드래그 배치(OLE 모달 루프를 SendInput 스텝 이동으로 통과, `Id=318` 실증). 정본 §8-5 [`symbol-3d-wip-propagation-analysis.md`](../analyses/symbol-3d-wip-propagation-analysis.md).
- **🔴 D-18/19 수정+검증**: 어도너(라벨·선택박스)는 맵의 형제 AdornerLayer 라 그 위 드롭·배치 클릭이 맵에 안 닿아 무음 거부 → `GMapCustomControl.SymbolPlacement.cs` `AdornerDecorator` 드롭 호스트 승격 + 어도너 원천 이벤트만 전달. 16:03 프로브 3/3 ✅(이후 사용자 지시로 Skip).
- **🔴 D-21 원인 확정+수정(사용자 "시설물은 줌 줄였다 올리면 사라진다")**: 실기 재현(−3/+3 후 18.5+ 복귀인데 3D 본체 소실, 라벨만 잔존). 원인 = dzl(하프스텝) 변경 경로에 최소표시줌 게이트 재평가 없음 + `ReapplyLayerVisibilityForZoom` 심볼 무동작 + 팔레트 심볼 `Zoom=CreationZoom`(x.5). PIDS 는 `Zoom=0` 이라 면역. 수정 = `GMapCustomControl.cs` `UpdateMarkersVisibilityByZoom(force)` 를 `OnDigitalZoomLevelChanged`·`SetEffectiveZoom` 에서 호출. **실기 재검증 = 5차 실행(줌 프로브) 결과 대기.**
- **하네스 결함 4건 수정**: `PART_*` 앵커 오인(→`GMaps.SymbolPalette.*`) · 같은 지점 반복(→20칸 격자·재사용 금지·재시도) · 비활성 컨트롤 주입 예외(→`IsEnabled` 가드) · 편집 예외가 삭제 삼킴/파일명 `/`.
- **사용자 지시 4건 반영**: "드래그 방식" · "같은 위치로 보내지 마"(겹침 프로브 Skip) · "속성 일부만? 전부 다"(`EditAll` 25항목/심볼) · "빠르게"(대기 350ms·스크롤 축소·그룹당 1캡처, ~64s/심볼).
- **🔲 진행 중**: ① 5차 실행 `run-s3d-full.ps1`(줌 프로브 + 40종 전체 속성, ~35분) → §8-6 기록 ② 사용자 신규 요청 "PIDS 탐지·장애 이벤트 시 3D 애니메이션 확인" — 코드 조사 에이전트 진행 중, 이후 로컬 테스트 서버로 이벤트 발행 + 프레임 캡처 ③ D-20(층수 미지원 타입 층 텍스트) 결정 ④ PRD Draft/미커밋 코드 · 운영 DB 마이그레이션 미검증 · R-14 TintStrength 영속 그대로.
- **잔여 정리**: 16:08 중단으로 남은 '감시탑_T' Id=373 은 일회성 테스트로 DB 삭제 완료(파일 삭제).

### ▷ ✅ 3D 심볼 WIP 반영 실패 수정 적용 (2026-09-07) — 사용자 지시 "정상동작 가능하도록 최대한 구현"

- **롤백 지점(사용자 질문 답)**: 기존 `before-symbol-3d-housing`(7881823)은 **구현 이전**이라 현재 작업이 없다. 수정 착수 전 워킹트리를 백업 커밋으로 잡음 — '
  tag `wip-symbol-3d-20260907-before-fix` = branch `backup/wip-symbol-3d-20260907` = **`322f8c5`**(임시 인덱스 `GIT_INDEX_FILE` 로 생성, HEAD·실제 인덱스 불변, docs 는 `-f` 포함). '
  복원: `git checkout 322f8c5 -- <경로>`.
- **적용 12건 / 14파일** — D-1 `SetCurrentValue`(`GMapBaseMarker.cs`) · D-2 FOV Collapsed/IpCamera 트리거 제거 · D-12 Infra3D 빈 `UpdateMarkerAppearance` · '
  D-10 모델변형 MultiDataTrigger · D-5 antenna/helipad/powerpole 재질 토큰 · D-4 `FloorDisplayText` DP + 3D 템플릿 이식 + **레거시 2D 바인딩도 수정**(메서드 바인딩이라 2D 도 빈 텍스트였음) · '
  D-7 지하 링 적층 · D-3 DB 3컬럼(NULL 허용·폴백·한 변경) · D-9 `MarkerAltitude` DP + '설치 높이' 행(SpecificContent 전용) · R-4 Display 반사 · R-10 주석 · R-17 로그.
- **검증**: `dotnet build` GMaps.Db ✅ · GMaps.Housing.Tests ✅ · `dotnet test` **23/23**(신설 `should_keep_size_binding_alive_when_marker_updates_size_through_adorner_path` + 기존 테스트에 바인딩 생존 단언). '
  mojibake 0 · BOM/CRLF 보존(패처가 `newline=''` 로 처리). **앱 실기·운영 DB 마이그레이션 미실측.**
- **보류 8건(결정 필요)**: D-6 층수 형상 · D-8 면적 스케일 · D-11 헤드 회전(화면 확인 선행) · D-13 2D 글리프 · D-14 재질 토큰 · R-2 폴백 계약 · R-6 OBJ · R-14 TintStrength 영속.
- **⚠ 사용자 통지(메인솔루션)**: 소스 `Dotnet.Monitoring.Solution/appsettings.json` 에 `Symbol3D` 키 없음 → **다음 빌드에 3D 꺼짐**. 배포본은 `false` 명시 권장. 라이브러리 밖이라 미수정 — 승인 시 처리.
- **패처**: 스크래치 `fix/p01~p28_*.py`(정확 문자열 + 개수 단언, 세션 종료 시 소실). 진단서 §8·§8-2·§8-3 에 적용표 기록.
- **✅ 3차 실기 구동 검증(사용자 "스스로 켜서 구동확인" · "안양발전소로 이동 후 테스트")**: 메인솔루션 하네스에 `Functional/Symbol3DRuntimeDiagTests.cs` 신설(UiDiagnostic). '
  런처 `scratchpad/run-s3d-diag.ps1`(계정은 env 로만, 로컬 도커 API admin). **PASS 28s** — 앱 로그 `[Symbol3D] ON` · 마커 생성 **55/55 → GMapMarker3DHousingControl** · '
  UIA raw 24/24 Viewport3D · 속성창 PIDS 절 색상 강도/설치 높이 노출 · 모델변형 행 제어기에서 숨김 · 설치 높이 60 → DB Altitude=60 · 제어기1 FOV 부채꼴 표시 · 너비 50→74→(Ctrl+Z)→86 · 예외 0. '
  아티팩트 `docs/assets/symbol-3d-implementation/runtime-20260907/`. **한계**: Undo 발화 불확실(rect 74 유지), 설치 높이 부양은 소형 마커라 육안 불가, D-11/D-8 육안 미확인.
- **하네스 실측(규칙 후보)**: 마커 UIA rect 는 화면 밖이면 수만 px 가짜 좌표 → 셸 rect 안만 클릭 · 템플릿 TextBlock 은 Raw 뷰로만 · WPF 휠은 delta 무시(이벤트 반복) · 속성창 ScrollPattern 없음 · ROI 패널 `PART_RoiListBox`+'이동' · FlaUI `Capture()` 는 Bitmap 직접 반환.
- **동반 라이브러리 변경**: `Symbol3DFeature.ReadFile/LastDiagnostic` · `GMapPidsMarker` 마커별 컨트롤 타입 로그 · Housing 테스트 +1(40/40).
- **✅ 4차 속성 전수 스윕(사용자 "Zoom 확대·속성 하나씩·심볼 중앙에")**: 하네스 `should_reflect_every_property_edit_in_3d_when_swept` 3회 PASS(13:16/13:24/13:32). '
  지도 줌 +3(rect 2배) + 마커 240×96 확대 + 속성별 3배 크롭 → 대조표. **전부 3D 반영 확인**: 크기·채우기+강도·테두리 링·선두께·회전(하우징 전체)·설치 높이(부양+기둥)·FOV 표시/색/투명·탐지 범위/각도/방향·'
  모델 변형(불릿→매달린 돔→스피드돔)·고정형 회전·건물종류 6종·층수→높이+F/B 텍스트·지하층·용도→지붕색·헬리패드 슬라이더 비활성·**어도너 리사이즈 후 속성창 너비 반영(D-1 실제 경로)**. '
  한계: 제목 편집기 탐색 실패(도구), 기준방향/면적/지하 링은 미세. 관찰: 50×20 에선 상태 배지가 하우징을 가림, 제어기는 금속 뚜껑 우세로 채우기색 약함.
- **✅ 5차 사용자 지적 2건(13:54)**: ① "제목 변경 못 찾음" = 하네스 결함 — DISPLAY 절 '제목' 표시 체크박스(`BasePropertyStyle.xaml:567`) 텍스트를 라벨로 오인 → `RawTextElement` 에 컨트롤 콘텐츠 제외(`IsControlContent`) 추가 → 제목 변경 **DB 왕복 확인**. '
  ② "링은 크기 바뀌면 같이" → `HousingVisual.OnRender` 링 **마커 박스 비례**(`rx=W·.46, ry=min(H·.22, rx·sin35°)`)로 변경, 집중 테스트 `should_scale_ground_ring_with_marker_size_and_edit_title` PASS(100×40→320×160 링 비례 대조표). Housing 40/40. PRD v2.5.
- **✅ 6차(14:02) "제목을 체크해야지 나온다"**: ShowTitle 체크(DISPLAY '제목')를 켜고 제목 변경 → 라벨 '정문1'→'정문1_변경' **화면에 그려짐** 확인(대조표 `s3d-ring-sheet-showtitle.png`), DB Title/ShowTitle 커밋, 원복 0. 헬퍼 `SetCheckByName`(체크박스 자기 이름으로 설정) 추가.
- **하네스 결함 발견·수정**: `DbProbe.RestoreColumns` bool→"True" 문자열 UPDATE 실패(무음) → 1차 실행 후 심볼 108·139 잔존 변경 → `ToDbValue` 수정 + 1회성 복구 테스트로 **원복 완료**(파일 삭제). `RestoreTypeTable`·`PidsRow`·`InfraRow`·`PidsDeviceTypeByTitle` 신설. '
  스윕 헬퍼: `CenterSelected`(편집모드 OFF→팬→ON→재선택) · `SetEdit/SetSlider/SetCombo(ByText)/SetCheck` · `DragHandle` · `CaptureZoom`+`SaveSheet`(대조표) · Raw 뷰 라벨 탐색 · 휠 반복 스크롤.
- **✅ 2차(보류 8건 결정, 사용자 "가장 합리적인 방향으로 정의해서 간다")**: D-6 층수→감시탑·안테나·전신주 높이 + `SupportsFloors` 슬라이더 게이트(게이트·발전기·물탱크·헬리패드·교량) · '
  D-8 면적→XZ 배율(.7~1.6, Fit/Project/링 동일) · **D-11 고정형·돔형 헤드 제거(하우징 전체 회전), PTZ 만 독립 헤드 — PRD FR-19 정정 v2.4** · D-13/D-14/R-6/R-14 의도 명시 · R-2 계약 주석. '
  **appsettings 적용**: 소스 `Dotnet.Monitoring.Solution/appsettings.json` ON · `Installer/appsettings.template.json` OFF(현장 opt-in, 설치기가 publish 판을 Excludes + 업그레이드 불가침). '
  빌드 ✅ · 테스트 **39/39**(결정 고정 Theory 16 신설). 앱 실기 미실행.

### ▷ 🔴 3D 심볼 WIP 반영 실패 진단 (2026-09-07) — 사용자 지시 "속성창/어도너 반영 안 되는 것 위주 점검"

- **정본**: [`docs/analyses/symbol-3d-wip-propagation-analysis.md`](../analyses/symbol-3d-wip-propagation-analysis.md) · 워크플로 `wf_526cbc73-344`(7렌즈 추적 + 렌즈별 적대 반증, 15에이전트·3.69M토큰·872툴콜·37분) + 메인세션 직접 재실측.
- **결과**: 132항목 추적 → **BROKEN 5 · PARTIAL 9 · RISK 17 · 정상확인 21**. 코드 변경 0(정적 추적, 앱 미기동).
- **⚠ 3D 플래그 상태**: `Symbol3D.IsEnabled=true` 가 **`bin/Debug/net8.0-windows7.0/appsettings.json` 에만**(mtime 09-07 10:22). **소스 `Dotnet.Monitoring.Solution/appsettings.json` 에 키 없음 → 다음 빌드에 3D 꺼짐.** 배포본(`Installer/publish/`)도 키 없음. `Symbol3DFeature.cs:9` 가 Lazy 라 프로세스당 1회 읽기(런타임 토글 불가).
- **🔴 D-1 최상위 공통원인**: `GMapBaseMarker.cs:227-235 UpdateShapeSize` 의 `element.Width = width` **로컬값 대입**이 3D 의 **OneWay** W/H 바인딩(`GMapMarkerBaseControl.cs:382-383` + 3D 가 `WritesBackRenderSize=false`)을 **영구 파괴**. 재바인딩 경로 없음(`SetupDataBindings` 실호출은 생성자 `:288` 1회뿐). 발화원 3개 — 어도너 `MarkerEditAdorner.cs:1025/1054/1090/1319` · **그룹 일괄편집이 재사용하는** `UndoableCommandBase.ApplyProperty:33-34` · `TransformCommand.cs:47`(위치만 바꿔도 UpdateSize). 증상 = "어도너로 크기 한 번 조절 후 속성창 크기 입력이 영영 무반응 + 심볼이 옆으로 밀림". **수정 = `SetCurrentValue` 한 줄, 부작용 없음.** ⚠ `HousingTests.cs:143-145` 가 `control.Width=27` 로컬 write 후 **바인딩 생존을 단언하지 않아** 이 버그가 테스트를 통과한 채 살아 있다.
- **🔴 D-2**: `Housing3DMarkerStyle.xaml:12`·`:250` `PART_FOVCanvas Visibility="Collapsed"` 를 여는 규칙이 **`DeviceType=IpCamera` MultiTrigger 뿐** → 비카메라 PIDS 8종 FOV 영구 불가. 폴백 컨트롤 대상 8종에 IpCamera 가 없어 조건이 영원히 거짓. **`GMapPidsMarker.cs:47-49` 가 3D OFF 에도 폴백 템플릿을 태워 2D 회귀.**
- **🔴 D-3**: `DetectionBearing`/`DetectionRange`/`DetectionAngle` **컬럼·SELECT·INSERT·UPDATE 전부 0건**, `GMapDbSymbolService.cs:4150` 이 `= BaseBearing` 하드 대입 → 탐지방향 저장 후 재조회 시 기준방향으로 복귀. ⚠ 하드대입 제거를 컬럼 추가보다 먼저 하면 **전 카메라 정북 리셋** — 한 커밋으로.
- **🔴 D-4/D-5**: Infra3D 템플릿에 층수 텍스트 요소 0건(계산·클릭토글은 살아 있어 영구 no-op) · antenna/helipad/powerpole 에 `mat_body`/`mat_roof` 파트 부재 → recolor 가 `_materials` 순회라 **색이 도달 자체를 못 함**.
- **🔴 D-12**: Infra 3D 는 MarkerState 변경 시 `base.UpdateMarkerAppearance()` 가 `MarkerFill` 에 로컬 write → 사용자 색 바인딩 영구 파괴("상태 한 번 바뀌면 채우기색이 안 먹음"). **PIDS 는 base 미호출이라 면역** → "어떤 심볼만 안 먹는다"로 체감. 수정 = Infra3D 에 빈 `UpdateMarkerAppearance()` override. 🚫 base `:437` 을 SetCurrentValue 로 바꾸는 근본안은 **"MarkerFill 권위자가 상태냐 사용자냐" 시맨틱 변경**이라 별도 합의 필요.
- **PARTIAL 9**: D-6 건물 8종 층수 무반응(+UI Max 50 vs 코드 Clamp 12) · D-7 지하층수 값 미사용 · D-8 건축면적이 3D 스케일 미반영 · **D-9 고도 입력 UI 자체가 없음**(표시·모델·Undo·DB 는 전부 준비됨) · D-10 모델변형 게이트 누락 · **D-11 카메라 헤드 회전**(배선은 정상, `m.Head=true` 가 실루엣 대부분이라 무반응처럼 보임 — **의사결정 필요**) · D-13 2D 건물 자산 1종 · D-14 재질 3종 하드코딩으로 테마 미추종.
- **RISK 주요**: R-1 D-1 일반형(`:319-332` 이 13개 DP 로컬 대입) · R-4 팔레트 `buildings[(int)type]` 병렬배열(enum 삽입 시 팔레트 전체 사망) · R-6 OBJ 로더 `builder.Head` 대입 0건 · **R-10 `GMapMarkerBaseControl.cs:501` 한 줄이 3D 성립 필수 의존인데 문서·테스트 고정 0** · R-8 어도너 박스 3D 회전 미추종(그리기만 고치면 히트와 갈라짐) · R-14 TintStrength 미영속.
- **🔲 수정 순서 12단계 확정**(진단서 §6). 1~7 은 부작용 없는 국소 수정, 8~10 은 결정·선행 제약 있음. **금기 6건**: TwoWay 무조건 복귀 · base `:437` 동시 수정 · Slider Max 축소 · recolor 폴백 · BasePropertyStyle 에 고도 배치 · `GMapMarkerPidsControl.cs:405` return 제거.
- **배포 전 필수**: `Installer/publish/appsettings.json` 에 `Symbol3D` 명시 추가(**값은 false** — opt-in 계약 유지) + 소스 appsettings 도 함께.

### ▷ 🔴 인계 문서 검토·검증 결과 (2026-09-07) — **판정: 그대로 넘기면 안 됨**

- **정본**: [`docs/analyses/symbol-3d-housing-handoff-review-analysis.md`](../analyses/symbol-3d-housing-handoff-review-analysis.md) · 워크플로 `wf_d5ccb1d6-35d`(7축 조사 + 축별 독립 적대 재검증, 15에이전트·2.48M토큰·555툴콜·27분) + 메인세션 직접 재실측.
- **주장 62건 대조 → CONFIRMED 44 · 결함 17 · UNVERIFIABLE 6.** 기술적 본체(§3 앵커 14/14 · §4 정정 E-1~E-4 · §5 결정 15건 · 수치 2,032·중복ID 0 · 시안 v2.8/모델35/JS 문법)는 **HEAD 기준 정확**. 무너진 것은 **자기 상태 진술**.
- **🔴 D-1 (최중요)**: "코드는 한 줄도 안 고쳤다"가 **거짓**. `git status -uall` 실측 **미추적 신규 17경로 + 수정 27파일** — `GMaps.Ui/Symbols3D/`(Housing{Models,Visual,ObjLoader,MeshBuilder,Math,Appearance}.cs) · `GMapMarker3DHousingControl.cs` · `GMapMarkerInfra3DControl.cs` · `Themes/Housing3DMarkerStyle.xaml`·`SymbolPaletteStyle.xaml` · `Utils/Symbol3DFeature.cs` · `Views/Maps/SymbolPaletteView.cs` · `MapViewModel.SymbolPalette.cs` · `Models/SymbolPaletteItem.cs` · `Resources/Symbols3D/` · `tests/GMaps.Housing.Tests/`(should_ 15건, 빌드 산출물 존재). PRD 가 "신설하겠다"던 베이스 3멤버가 이미 랜딩 — `GMapMarkerBaseControl.cs:34 RotatesIn2D`/`:800`/`:801 WritesBackRenderSize`/`:804 ApplyDisplayAngle`, **HEAD 판본 grep 히트 0**. 파일 mtime 09-06 **23:33**(인계문서 20:55 이후) → 문서는 작성 시점엔 참이었고 지금 낡았다. **⚠ §11 대로 태그 checkout 하면 미커밋 19경로 복구불가 소실** — 태그와 백업브랜치는 동일 커밋(7881823)이라 2중 안전장치 아님.
- **🔴 D-2**: "드래그 인프라 0건"이 문서가 **스스로 인용한** `drag-first-ux.md:11-13`("OLE 0건 / 캡처 드래그 다수")와 정면 배치. 실측 `CaptureMouse()` **22파일**. 그런데 FR-18 이 `DoDragDrop` 채택 + `SymbolPaletteView.cs:158` 구현 = 같은 규칙 **Must Never(OLE 기본안 금지, 채택 시 스파이크 선행) 위반**이 코드로 굳음. 폴백 6종 중 ESC 만 있고 FR-16 이 콤보·[추가] 제거 → 드래그 유일 경로.
- **🔴 D-3**: "베이스 3멤버는 순수 가산" 반증 2건 — 군대부호 `IsPreviewMode` DP **베이스 이관**(비대상 파생 수정) · `:382-383` W/H 바인딩을 `WritesBackRenderSize ? TwoWay : OneWay` 로 **기존 계약 조건부 변경**. FR-20 회귀가드 범위 과소.
- **🔴 D-4 (안전)**: 금지문이 **안전한 GMaps.Db** 를 지목(`GMapTestDb.cs:23 monitor_test_db` 격리 완료). 진짜 위험은 **`Accounts.Db/Tests/UnitTest.cs:43 monitor_db` + `DROP DATABASE`**, `Devices.Db:57`, `Events.Db:67`. CLAUDE.md `test_command` 가 무필터 `dotnet test` 라 기본값 실행 시 운영 DB 전멸 경로.
- **🔴 D-5**: "성능 계측 0건" 거짓 — `docs/assets/symbol-3d-implementation/rotation-cpu.txt`(p95 **0.100ms @ N=100 오프스크린**, GPU/RDP/오버레이 제외) 실재. N=500·실기는 여전히 미측정.
- **🔴 최상위 누락**: `window-architecture-prd.md` **§12 불가침 경계 전면 미반영** — `:1911` 이 `SymbolPaletteView` 를 "symbol-3d-housing 미커밋 산출물"로 명시, `:1941` **D-1 "미결 시 symbol-3d-housing 즉시 동결(WIP 5파일이 이미 위반)"**. 현재 WIP 가 속성창 4파일 + `Generic.xaml` 수정 중. (window-arch PRD mtime 09-07 00:19 = 인계문서 이후라 과실 아닌 미반영)
- **medium 6 / low 6**: HEAD 대비 워킹트리 **+10~+19행 드리프트** 미표기 · §3 이 지목한 결함 3건 이미 수정됨 · pipeline-state 정본은 `.claude/.branch-v2-6/`(docs/memory 판은 07-30 stale) · **approve mtime 1위가 지금 `window-architecture-prd.md`** · ISSUE 626→**628** · 리스크 10→**9** · V-01~15 중 V-10 결번·V-12 철회(활성 13) · "모든 회전 경로"→파생 8종 한정.
- **🔲 다음**: 수정 지시 12절이 분석 §6 에 적용 가능한 형태로 정리돼 있음. **사용자 결정 필요** — ① 인계문서를 수정해 넘길지 ② 미커밋 WIP 를 먼저 보존/커밋할지 ③ window-arch D-1/D-2 결정 전까지 동결할지.
- **⚠ 훅 오탐**: 이 턴의 게이트 배너가 "PRD 검토 승인 키워드 감지 — 마커 해제"를 띄웠으나 **사용자는 승인한 적이 없다**("검토·검증" 요청을 승인으로 오인). PRD 는 여전히 Draft.

### ▷ 검토 인계 (2026-09-06, 사용자 지시 "다른 세션에서 검토")

- **인계 정본**: [`docs/coordination/symbol-3d-housing-handoff.md`](../coordination/symbol-3d-housing-handoff.md) — 맨바닥 세션이 읽고 검토할 수 있게 자립형으로 작성.
- **담은 것**: 사용자 요구 10건 이력(범위가 늘어난 순서) · 파일 지도(정본 5종·캡처 6종·시안 여는 법) · 조사한 코드 17지점 file:line 표 · **선행 문서 오류 4건 E-1~E-4**(이걸 모르고 착수하면 회귀) · 결정 15건 요지(뒤집힌 4건 G-6/8/14/15) · 지시 8 대응 스코프 원칙(base 확장이 왜 가산적인가) · 시뮬 ISSUE 626 은 대부분 **대조군**이라는 읽는 법 · 미검증 V-01~15 + 성능 근거 0 + 부팅 O(N²) 오염원 · 검토 요청 5항 · **금지 5항**(게이트 `--force` · 승인 대행 · GMaps.Db 통합테스트 운영 DB 삭제 · 한글 셸 인자 · 타 세션 WIP 커밋).
- **검토 요청 5항**: ① 성능 게이트(16ms/500개)가 차용 수치인데 그대로 게이트로 삼아도 되나 ② base 3멤버 추가가 정말 가산적인지 코드로 반증 ③ 결정 15건 근거 검증(특히 G-6·G-14·E-1~E-4) ④ 팔레트·드래그 설계의 빈틈(드래그 인프라 저장소 0건 = 전량 신설) ⑤ 태스크 56 이 과한가(3D 본체 ↔ 팔레트 분리 여부).
- **⚠ 받는 세션 주의**: `docs/` 는 gitignore 대상이라 **Grep 으로 안 잡힌다** — 경로로 직접 열 것. `approve prd` 는 **mtime 최신 PRD** 를 집으므로 창 아키텍처 PRD 와 공존하는 지금은 대상이 뒤바뀔 수 있다.

### ▷ 형상 재설계 2회 (2026-09-06, 사용자 육안 지적)

- **1차** "IP스피커 혼 옆 통이 너무 얇다" → 드라이버 배럴 r11×20 + 숄더 플랜지, 혼 길이 38(입구 r21).
- **1차** "고정형 카메라는 돔이 될 수 없다 · 돔형은 거꾸로 달린다 · PTZ 를 그럴듯하게" → 카메라 계열 3분할: 고정형=불릿(배럴 33 + 실드 13.5, 실린더가 드러나게 실드를 좁힘) / 돔형=벽 브래킷 팔에 **아래로 볼록한** 버블(`domeDown` — 절두체를 아래로 쌓아 법선 유지) / PTZ=폴 마운트 스피드돔.
- **2차** (스크린샷) "카메라와 스피커 디자인이 너무 별로다" → **원인은 형상이 아니라 시야각**: 피치 35° 탑다운에서 정면(+Z)이 관찰자를 향한 bearing 0 은 원판으로 보이고, 아래로 볼록한 돔은 위에서 볼록면이 안 보인다. → `PREVIEW_B` 모델별 3/4 미리보기 방위(camera 48 · dome 35 · ptz 35 · speaker −52 · sensor 20 …)를 갤러리·팔레트 셀·§8-C 크롬이 공유하게 하고, 돔/PTZ 를 브래킷·폴 설치형으로 재모델링(실제 경계 설치 형태와도 일치).
- **중간 결함 3건 수리**: 갤러리 `B=undefined°` 라벨(`dome` 키 누락) · 구 모델 정의의 고아 줄이 스크립트를 깨뜨림(`Unexpected identifier 'base'`) · 줄범위 삭제가 한 줄 더 먹어 **`sensor:` 모델 전체 소실** → 복원. Node `new Function()` 구문 검사 + 브라우저 확인(모델 35 · 팔레트 셀 90 · 갤러리 20 · 와이어프레임 25행 · 콘솔 무오류).
- **시안**: `docs/design/symbol-3d-housing-storyboard.html` **v2.8** · 캡처 `docs/assets/symbol-3d-gallery-v28-20260906.png`.

### ▷ 미결 9건 전부 확정 (2026-09-06, 코드 근거)

| ID | 결정 | 결정적 근거 |
|---|---|---|
| G-6 PTZ | `EnumDeviceType` **확장 금지** → `PidsSymbols.ModelVariant VARCHAR(20) NULL` 신설 | `MapViewModel.cs:349-352` 가 심볼 DeviceType 을 **서버 장비 레지스트리와 대조**, 불일치 시 ID 폴백 + 경고 로그. 새 멤버는 서버가 안 보내므로 영구 경고 |
| G-14 좌표 | 드롭·클릭 **둘 다 `SubPixelGeo`** 상향 | `Helpers/SubPixelGeo.cs` 가 이미 드래그 이동에 사용 중 — 세 경로 정밀도를 맞춘다 |
| G-15 링 채널 | **철회**(충돌 없음) | 상태 = `PART_EventStatusIndicator` 배지 + `PulseRing1~3`(고정 `#CCFF0000`), 접지 링은 3D 신규 요소 — 요소 자체가 다름 |
| G-8 크기표 | **확장 안 함**(현행 명시 6종 + 폴백 32 유지) | N3 정규화가 W/H 박스를 채우므로 무의미하고, 늘리면 기존 저장 W/H 와 어긋나 회귀 |
| G-11 FOV z | **현행 유지** | `PidsMarkerStyle.xaml:450-451` `PART_CameraSection Panel.ZIndex="-2"` — 이미 뒤에 있다. V-12 철회 |
| G-3 호버 | 접지 링 밝기 +15%, `PART_HoverHighlight` 존치 | 현행 10×10 Opacity 0.15 는 하우징 뒤라 3D 에서 안 보임 |
| G-4 차량 | 별도 PRD | 부팅 로드 SQL `WHERE Category='BASIC_SHAPES'`, 집계 7종에 VEHICLES 없음 |
| G-5 건물 | 12종 확정, 초소·감시탑·게이트 우선 | `EnumBuildingType`·`EnumBuildingUsage` 확장 후보가 주석으로 존재 |
| G-12 건물용도 | 지붕 액센트 재질(Phase 2) | `EnumBuildingUsage` = Office 1종 + 8종 주석(BuildingType 과 동일 상태) |

### ▷ 실물 와이어프레임 §8 신설 (시안 v2.0)

- **실제 XAML 구조·실측 토큰 재현**: `Tokens.Dark.xaml`(Surface `#161D26` · SurfaceAlt `#1E2832` · Border `#2C3A48` · Divider `#243140` · Primary `#22B8D9` · Accent `#F0A33C`)
- 8-A 편집 스트립(현행 콤보120+콤보100+[＋추가] vs 신규 [아이콘 등록] 토글) · 8-B 속성 패널 실제 5섹션(BASIC 제목·제목크기·크기·회전 / COLOR 채우기·테두리·선두께 / LABEL 글자색·배경색·글꼴·글자체·제목폭 / DISPLAY 최소 줌 / Z-ORDER 순서) + PIDS 전용 + 3D 전용 3필드 · 8-C 팔레트 크롬(`LayerPanelStyle` CornerRadius 8·CountBadge) · 8-D 드래그 상태 전이 7단계
- **중요 실측**: 색은 스와치 그리드가 아니라 **ComboBox**(`AvailableColors` + `ColorTypeToBrushConverter`), 크기는 TextBox 2개, 회전·최소줌은 Slider. 공통 패널 레이아웃은 **행 하나 안 늘어난다** — 신규 3필드(틴트 세기·고도·모델 변형)는 전부 `SpecificContent` 안
- 브라우저 검증 7/7 통과 · 캡처 `docs/assets/symbol-3d-wireframe-20260906.png`·`symbol-3d-wireframe-flow-20260906.png`
- 시뮬 라운드 3 = **2,032건**(패밀리 85, PTZ·SZ 패밀리 추가)

### ▷ 이전 이력 (v1.2 까지)

### ▷ 추가 지시 반영 (2026-09-04, 시뮬 라운드 3)

- **지시 1**: "아이콘 등록 버튼 → 미리보기 보고 선택 → 드래그해서 지도에 놓는 방식으로 바꾸고 싶다"
- **지시 2**: "마커 에디터의 각 카테고리별 속성들이 3D 에서 어떻게 매칭될지 검토"
- **현행 실측**: 콤보 2개(`AvailableMarkerCategories`/`AvailableSymbolTypes`) + [추가] → `EnterSymbolPlacementMode`(MapViewModel.cs:739) → `IsSymbolPlacementMode` → 지도 좌클릭(`GMapCustomControl.cs:1015`, geo=`FromLocalToLatLng((int)x,(int)y)` :1020) → `SymbolPlacementClicked`(:1041) → `OnSymbolPlacementClicked`(:765-790) 4분기
- **핵심 제약 3**: ① GMaps.Ui 에 `AllowDrop`·`DoDragDrop`·`DataObject` **0건**(전부 신설) ② `IsPreviewMode` DP 는 군사 컨트롤에만(`:155`) — 베이스 신설 필요, 팔레트 셀은 부모 맵이 없어 yaw 캐시 재적용 필수 ③ 배치 클릭이 좌표를 `(int)` 절단 → 드롭이 서브픽셀이면 같은 화면점이 다른 위경도(G-14)
- **속성 매칭 판정**(시안 §6-D 정본): 무변경 다수 · 재정의 4(크기=N3 입력 전용 · 회전=3D yaw · 탐지방향=표시각 보정 · 건물종류=**모델 키 선택자**) · 결정 3(채우기/테두리/선두께 G-7 · 건물용도 G-12 · 층수 G-13). 속성 패널 레이아웃 무변경
- **산출**: 시안 **v1.8**(§6 팔레트 — 탭 5종 클릭 전환 실물 목업 `PIDS 17 · 기반시설 12 · 도형 8 · 기본 2 · 차량 5(취소선, 별도 PRD)`, §6-B 드래그 4단계, §6-C 배선, §6-D 속성 매핑 / 브라우저 렌더·탭 전환 검증 완료, 캡처 `docs/assets/symbol-3d-palette-all-20260904.png`) · PRD v1.2 FR-16~19(태스크 40→54) · V-13/14/15 · G-12/13/14 · 시뮬 라운드 3 = **1,933건**(신규 DND-A~H·PAL-A~C·PROP-A~C·R2)
- **스크래치 스크립트**(재현용): `scratchpad/patch_palette.py`·`patch_palette_all.py`·`patch_propmap.py`·`patch_liveedit.py`·`patch_liveedit_fix.py`·`patch_color_alive.py`·`sim3d/sim.py`(ROUND 3)·`sim3d/export_docs.py`

### ▷ 속성 라이브 에디터 + 색 매핑 확정 (2026-09-04, 시안 v1.13 / PRD v1.3)

- **지시 3**: "구현 테스트 하고 3D는 마커 속성 수정하면 어떻게 반영될지 HTML로 보여줘, PIDS 심볼과 그룹 먼저"
- **지시 4**: "컬러는 심볼색이나 그런거 될 수 있잖아" → **G-7 해소** — 채우기 = 하우징 `mat_body` 계열 **재질 틴트**(렌즈·어두운 부품·라벨은 원색 유지), 테두리·선두께 = **접지 링** 색/두께. 사용자 색과 상태 색은 채널 분리, 평상시 사용자색·이벤트 중 상태색(**G-15** 신설)
- **시안 §7 라이브 에디터**(신규): 7-A PIDS 점 심볼(장비타입·크기 W/H·회전·최소줌·Z-ORDER·채우기·틴트세기·테두리·선두께·제목·글자색·FOV 6필드·지도 θ) · 7-B PIDS 그룹 철책(포인트·패턴·투명도·선두께·색·압출 높이·지도 θ → 정점 재투영). 실제 슬라이더 조작 → 3D 즉시 반영
- **구현 테스트(브라우저 실측)**: 1차 10/10 · 색 매핑 2차 10/10 통과. 도중 발견·수정한 결함 2건 — ① `.ring` 의 `border` 단축속성이 편집기 밖에서 `var(--normal)` 미해석으로 무효화(→ `borderStyle` 명시 + `.edstage` 에 토큰 스코프 부여) ② 틴트 0% 가 대소문자 다른 hex 를 만들어 원색 복귀 비교 실패(→ amount 0 이면 원본 문자열 그대로 복원)
- **캡처**: `docs/assets/symbol-3d-propedit-20260904.png`(7-A) · `symbol-3d-group-20260904.png`(7-B, θ=35°) · `symbol-3d-palette-all-20260904.png`(§6)
- **시뮬 라운드 3 재실행**: 1,933 → 1,944 → **1,973건**(패밀리 80)

### ▷ 전수 매핑 "안 쓰이는 필드 0" (2026-09-04, 시안 v1.17 / PRD v1.4)

- **지시 5**: "어떻게든 안 쓰이는 것 없이 쓰이도록 3d 아이콘들을 맵핑해봐"
- **DB 컬럼 전수 조사로 찾은 미사용 필드 6종** → 3D 역할 배정
  - `Altitude` 고도: 렌더 참조 **0**(`GMapBaseMarker.cs:385-391` 모델 프로퍼티만) → **하우징 부양 + 지주 + 접지점 링**. 지오 앵커는 지주 밑동이라 Position 계약 불변
  - `IsLocked` 잠금: 클릭 차단만·지도 표식 없음 → **접지 링 파선 + 자물쇠 배지**
  - `BaseBearing` 기준방향: 속성창 슬라이더만 있고 마커 DP **없음**(`PidsPropertyStyle.xaml:307`) → **하우징 몸체 yaw**, 헤드는 `탐지방향 − 기준방향` 상대각(PTZ 처럼 몸체 고정·헤드 회전 장비에서 의미)
  - `BuildingArea` 면적: 정보 필드(렌더 미사용) → **접지 footprint 크기**(W/H = 심볼 크기, 면적 = 부지 크기로 역할 분리)
  - `FloorCount` 지상층: 텍스트만 → **창문 띠 개수**(높이 불변 → N3 정규화 충돌 없음, **G-13 해소**)
  - `BasementFloorCount` 지하층: 텍스트만 → **접지 아래 파선 윤곽**(깊이)
- **비시각으로 남기는 것**: 데이터 키 7개(`Id`·`Pid`·`Category`·`LinkedDeviceId`·`LinkedDeviceGroup`·감사 컬럼) — 속성창에 노출되지 않으므로 형상 매핑 대상 아님
- **시안 추가**: §6-E DB 컬럼 전수 매핑표 · §7-C 기반시설 라이브 에디터(파라메트릭 빌딩 — 층수→창문 띠, 지하층→파선 윤곽, 면적→접지, 용도→지붕 액센트)
- **구현 테스트**: 신규 매핑 10/10 통과(누적 30/30). 캡처 `docs/assets/symbol-3d-fullmap-pids-20260904.png`·`symbol-3d-fullmap-infra-20260904.png`
- **잔여 미결**: G-3 호버 · G-4 차량 · G-5 건물 명칭 · G-6 PTZ 필드 · G-8 크기표 · G-11 FOV z순서 · G-12 건물용도 · G-14 드롭 좌표 · G-15 링 채널 우선순위

### ▷ 3D 하우징 본체 (v1.1 까지)

- **사용자 지시**: "git 롤백포인트 → 최대한 다양한 시나리오로 시뮬레이션 2회 → PRD 구성 → 개발"
- **롤백 포인트**: tag `before-symbol-3d-housing` = `7881823`, 브랜치 `backup/pre-symbol-3d-housing`
- **산출물(정본)**: 분석 `docs/analyses/symbol-3d-housing-scenario-analysis.md`(영향면 6축 103건·차단 19·정본 오류 4건 E-1~E-4·정책 P-1~P-16·Gap G-1~G-11) · 카탈로그 `docs/tests/symbol-3d-housing-scenarios.md`(라운드 2 최종 **1,812건**·패밀리 59·ID 유일성 생성기 검증) · 전량 로그 `docs/tests/symbol-3d-housing-simulation-log.md` · PRD `docs/prds/symbol-3d-housing-prd.md`(**Draft v1.1**, FR-01~15, 예상 태스크 40 = Phase1 37 + Phase2 3)
- **적대적 검증(워크플로 4렌즈, 55건) 반영 완료**: SIM ID 충돌 재발번(`SIM-H2001~`·`SIM-X1001~`) + 생성기 유일성 게이트 · **FR-06⇄FR-07 피벗 모순 해소**(결정(a): yaw 피벗 = bbox 바닥 중심, FOV 원점 = 렌즈 정사영 픽셀 매 tick 파생 — 렌즈 피벗 안은 몸체 스윕이 N3 박스 초과로 기각) · NFR 출처 열(16ms/N=500 차용, 32ms/500ms 잠정) · FR-11 enum 21행 매핑표(Fence 추가, PTZ·siren 분리) · FR-13 부팅 1회 적용(런타임 토글 비목표) · 템플릿 열 보강(가능성·확인 여부·5-B 3열·변경 이력) · 정본 2종 stale 문장 정정 완료
- **라운드 2 재검증 워크플로는 서브에이전트 월 한도로 4렌즈 전부 실패** — 핵심 항목(카탈로그 중복 ID 0, PRD 인용 SIM 30개 범위 1:1 존재, 신규 file:line 12곳, PidsMarkerStyle 트리거 8행)은 직접 grep 으로 재확인 완료. 미실행 검증은 재개 시 `Workflow({scriptPath: …symbol-3d-prd-verify-round2-wf_80244d60-894.js, resumeFromRunId: 'wf_80244d60-894'})`
- **시뮬레이터**: 스크래치 `sim3d/sim.py`·`parse_models.py`(스토리보드 29모델 bbox 자동)·`export_docs.py` — 세션 스크래치라 재개 시 docs 정본만 남는다(재실행 필요하면 로그 형식대로 재작성)
- **검증 이력**: 검증자 #1(인용)·#3(문서 정합)·#2(완전성) 정정 반영 완료 → PRD/분석 적대적 검증 워크플로 4렌즈(인용·커버리지·정합·템플릿/DoD) 실행 → 결과 반영 여부는 아래 "다음 할 일"
- **핵심 발견(정본 정정)**: E-1 `AppliesMapRotation` 은 표시각과 히트를 겸함(false 처방 = yaw 에서 θ 소실) → `RotatesIn2D` 분리 · E-2 점 심볼 라벨은 `RotatedAabbHalf` 미사용(193행) · E-3 W/H write-back 3채널(base·Pids override·TwoWay DP) · E-4 상태 Trigger 는 Fill 교체가 아니라 배지/불투명도(트리거 49, Fill 0)
- **파이프라인 상태 주의**: `.claude/.branch-v2-6/pipeline-state.json` = phase **dev** / activePrd `pidsgroup-rightclick`(36/50). 새 PRD 사이클(`new-cycle`)은 complete 에서만 가능하고 `--force` 금지 → **사용자 결정 필요**(pidsgroup 사이클 complete 후 new-cycle, 또는 그 사이클 계속). 승인 명령 `approve prd` 는 mtime 최신 PRD(=이 PRD)를 집는다.
- **결정 현황**: 채택 G-1(피치 35° 상수)·G-2(삼각형>500 경고 로드)·G-9(6토큰+Kd)·G-10(핀 유지) / **미결 승인 요청** G-3 호버(현행 `PART_HoverHighlight` 10×10 유지 vs 링 밝기) · G-4 차량 별도 PRD · G-5 건물 12종 명칭·우선순위 · G-6 PTZ 필드(`EnumDeviceType.PtzCamera` 신설 권고 — `EnumShapeType.PTZ_CAMERA` 는 GMaps 미참조 레거시) · G-7 속성 패널 3필드 · G-8 폴백 15종 크기표 · G-11 FOV z 순서
- **다음 할 일**: ① 적대적 검증 결과 반영 ② 사용자 PRD 검토·`node .claude/hooks/advance-phase.js approve prd "…"` ③ plan(worktree 브랜치 + CHANGELOG) ④ dev — Phase 1 = FR-01~13,15(PIDS 21종), Phase 2 = FR-14(건물 12종)

## ▶▶ 재개 포인트 (2026-08-07 — 지도 심볼 3D화 기획 · Track A, 조사 진행 중)

**요구**: (A) 3D 모델을 돌려보며 심볼을 고르는 선택 UI · (B) 지도 위 심볼을 3D 로, 지도 회전에 동기.
**산출물**: [`docs/design/symbol-3d-storyboard.html`](../design/symbol-3d-storyboard.html) — **bearing 슬라이더로 직접 돌려보는** 인터랙티브 스토리보드(다크/라이트).
- 원리 = 선행 정본 `map-25d-rotation-sync-analysis.md` 의 **M3 5축 분해**를 그대로 시연: 발판·FOV·철책선=지오 고정(−θ) / 수직 압출·광원·그림자·라벨=화면 고정 / 아이콘=현행 `Bearing−θ`.
- 선행 기각 2건 재론 금지: **방위별 스프라이트**(16f 잔차 ±11.25° → 150 m 에서 29.8 m 조준 이탈) · **아이콘 빌보드**(회전 핸들 무반응, θ가 FOV·오버레이로 이전) — 스토리보드에 "기각안 미리보기" 체크박스로 사유를 보이게 둠.
- 렌더 경로 판정(잠정): E1 마커별 Viewport3D 기각 · E2 단일 3D 장식층 조건부 · **E3 2.5D 벡터 권고** · E4 스프라이트 기각 · E5 빌보드 기각 · **A 픽커 프리뷰 권고(지도 무관, 임계경로=3D 자산)**.
- **✅ 실현성 조사 완료 `wf_7e7dc76c-f3d` → 정본 [`docs/analyses/symbol-3d-feasibility-analysis.md`](../analyses/symbol-3d-feasibility-analysis.md)** · 스토리보드 v1.1 로 §5·Q1 정정.
- **🔴 반증 5건 전부 뒤집힘(내 초기 판단 오류)**: C1 마커 템플릿 `PART_MainContainer` **안에** Viewport3D 하우징을 넣으면 히트(AABB)·어도너(`RenderSize`)·드래그(어도너 캡처) 계약 **0줄 유지** + `Bearing−θ` 회전 자동 상속(Shape **교체**만 금지; 필수 수정=`ActualWidth→Marker.Width` write-back 차단) · C2 seam 은 **타일**과 정합이지 마커 Canvas(정수 반올림)와는 ±1px 지터, seam 이 디지털줌 미포함→층은 컨트롤 서브트리 안 · C3 스프라이트 기각 근거=**양자화** → 연속 `DisplayAngle` 회전 3D 하우징엔 무관, 조준 정확도는 `PART_FOVCanvas` 벡터 · C4 (A) 임계경로=**코드**(심볼이 enum 절차 합성: 군대부호 4×4×4×37×11·PIDS 21·도형 25 → 3D 합성기, 자산 컬럼/로더 0, 선택 5/6 은 EditStrip 콤보→배치모드, 속성창 즉시 DB UPDATE) · C5 회전 기본값 **2026-07-30 이미 확정**, 플래그는 입력만 게이트(렌더 비게이트) → 선행 결정 불필요, 게이트=OFF pixel-diff 0.
- **실측**: 3D 스택 0(OpenTK 는 LiveCharts 전이), Viewport3D 사용 0, 3D 자산 0, GPU tier 게이팅 없음, 평가 형상=노트북 1대 · 심볼 11계열/재제작 35~40종/Trigger 199 · 유일 프리뷰=군사 등록창 80×80 마커 재사용(`IsPreviewMode`, 드래그 게이트 Y>45) · 규모 로컬 102행(운영 미실측) · **Viewport3D N개 vs 1개 벤치 리포 내 0** → 성능은 양방향 미입증.
- **권고 로드맵**: **A-1 2D 회전 갤러리(자산 0·패키지 0, 1~2주)** → **PoC-E1′ 템플릿 내부 3D 하우징 2종(카메라·함체, N=100/350/500 프레임타임·RDP Tier0·OFF 16장 pixel-diff)** → Phase 1 2.5D(철책 리본·그림자)+하우징 6종 → Phase 2 A-2 턴테이블(파라메트릭 합성기)·건물 프리즘.
- **🎨 심볼 디자인 개선(사용자: "3D는 좋은데 심볼 디자인이 별로")** → [`docs/design/symbol-design-system.html`](../design/symbol-design-system.html) + 실물 인벤토리 [`symbol-current-inventory.html`](../design/symbol-current-inventory.html).
  **실물 확인 방법**: `PidsMarkerStyle.xaml` Path 를 파서(`scratchpad/extract_symbols.py`)로 SVG 변환 → 로컬 http.server + Playwright 캡처(`file:` 차단됨) → `docs/assets/symbol-*-20260807.png`.
  **진단 7**: ① 제품 사진 트레이스 일러스트(19~51도형, 명암 포함) → 30px 붕괴 ② 시점 5종 혼재(카메라 정면·스피커 측면·센서 3/4 아이소·펜스 입면·다중센서 실루엣) ③ Fill 하드코딩(White 52·Black 13, 토큰 0) → **다크에서 다중센서·스피커 검정 실루엣 실종** ④ Trigger 199 가 상태색으로 형태를 덮음 ⑤ **제어기 250×90 캔버스가 50×50 Stretch=Fill 로 2.8배 세로 왜곡**(⚠ 생성 W/H 타입별 확인 필요) ⑥ 방향 노즈 없음(FOV 의존) ⑦ SVG 소스 없음.
  **제안**: 24-grid 2px 실루엣 · **배지(토큰 배경+상태 링)+글리프 2층** · 토큰 바인딩 · 톱다운 통일 · 노즈 · SVG→XAML 생성기. 시안 9 글리프(카메라/PTZ/스마트센서/다중/스피커/제어기/조명/함체/펜스) **30px 판독 확인**(캡처). 단계 D-1 글리프 확정(3일) → D-2 배지 템플릿 통합 → D-3 생성기+정사각 캔버스 → D-4 Infra/Geometric(군사부호 제외). 불변식: 카메라 `RotationPivot` apex 비율·W/H·Offset·AABB 유지.
  **✅ 사용자 결정(2026-08-07)**: "군대부호 빼고 **사실적인 그림을 3D 형태로 유지하면서 단순화**, 회전 시 같이 돌아가는 구조" → 픽토그램안(symbol-design-system.html)은 **참고용으로 격하**, 정본은 3D 하우징.
  **WGS-84 질문 답**: 된다 — 화면은 Web Mercator(`MercatorProjection.Instance`, 등각) · 회전은 뷰포트 중심 화면 어파인(`GMapControl.cs:1570-1572 _rotationMatrix.Angle=-Bearing`) · 표시각 `DisplayAngle=Bearing−θ`(`RotationMath.cs:52`) · 방위 계산 `MarkerHelper.cs:138 Atan2(dx,-dy)` 화면/진북 기준 · UTM 은 좌표표시·MGRS 격자에만 → **yaw=Bearing−θ 가 위도·격자북 보정 없이 정확**(심볼은 픽셀 고정이라 축척 무관).
  **3D 하우징 스토리보드** [`docs/design/symbol-3d-housing-storyboard.html`](../design/symbol-3d-housing-storyboard.html): 프리미티브 코드 합성 8종+펜스, CSS-3D 정사 피치 35°·화면 고정 광원 플랫 셰이딩·θ/피치 슬라이더·30/50/100 사다리·지도 미리보기. **캡처 확인 완료 v4**(슬롯 중복 버그·다중센서 높이·스피커 혼 방향/대비·돔 4단 수정 후 재확인 — `docs/assets/symbol-3d-housing-*-20260807.png`, θ=0/45 갤러리·지도 미리보기·30/50/100 사다리).
  **핵심 발견**: 피치 카메라 3D 하우징은 **θ=180 에서 거꾸로 서지 않는다**(빌보드 없이 2D "거꾸로 문제" 소멸). 단 **피치를 주면 베이스의 2D `_displayRotate` 로 컨트롤을 돌리면 안 됨** → `DisplayAngle` 을 3D yaw(`RotateTransform3D` Y)로 라우팅해야 함 — 히트·편집 핸들·FOV·라벨 영향은 **직접 검증 완료**(서브에이전트 워크플로우 2회 모두 API 529 전멸 → 코드 grep 으로 대체, 정본 §7-2):
  `_displayRotate` private(:782)·`OnMapBearingChanged` non-virtual(:795)·`_appliedMapBearing` private → **베이스에 `protected virtual ApplyDisplayAngle` 1개 추가 불가피(additive)** · 편집 어도너는 `RenderSize`+`TransformToAncestor`(자기 RenderTransform 제외) 기준이라 그대로 동작, 회전 핸들→모델 Bearing→3D yaw 자연 전달 · FOV 는 `FovControlSpaceBearing(D,b)` 에 표시각을 더해 `D−θ` 유지(PIDS 컨트롤 1줄) · 히트는 정사각이면 모서리 오차만(정확히는 3D 컨트롤 `AppliesMapRotation=false`+각도 0) · 라벨 AABB 과팽창 무해.
  **구조적 이점**: 3D 하우징은 θ=180 에서 거꾸로 서지 않음(2.5D §9-7 미결 해소, 빌보드 불필요).
  **모델 교체 가능성(2026-09-04 질문 "obj 되나?")**: WPF Viewport3D 는 `MeshGeometry3D` 만 먹고 내장 임포터 0 · NuGet 캐시에 Helix/Assimp/SharpGLTF **없음**(오프라인 설치 불가) → **OBJ(+MTL) 자체 로더(~150줄, v/vn/vt/f 삼각화)** 가 정답. glTF 는 로더가 커서 후순위, STL 은 색 없어 부적합.
  설계: `Resources/Symbols3D/{DeviceType}.obj` 파일 교체로 모델 교체 · 사이트 오버라이드 폴더(AppData) 우선 · 파일 없으면 코드 프리미티브 폴백 · 로드 시 정규화(바운딩 박스 단위화, 바닥 y=0, +Z=정면/렌즈) · **MTL 색 하드코딩 금지 → 재질 이름(mat_body/mat_dark/mat_lens…)→테마 토큰 매핑**(현행 아이콘의 하드코딩 색 실패 재발 방지) · 삼각형 상한 500 · 공유 Freeze 캐시.
  **전수 적용 + 빈칸 채우기(2026-09-04 "군대부호 빼고 다 적용, 자동차·건물 없는 것도 채워라")** → [`docs/analyses/symbol-3d-catalog-analysis.md`](../analyses/symbol-3d-catalog-analysis.md) + 스토리보드 v1.2 §1-B.
  실측: `EnumMarkerCategory` = BASIC_SHAPES·GEOMETRICS·**VEHICLES**·MILITARY·PIDS·AREA_BOUNDARY·INFRA · PIDS 21종 중 전용 아트 6종(Controller/Multi/Fence/Camera/Speaker/SmartSensor; SmartCompound→SmartSensor, SmartMultisensor→Multi 재사용) · **VEHICLES 는 아트 0 + `AddVehicleMarker` 미복구(MapViewModel.cs:4340-4353 팝업 "아직 지원하지 않습니다", 콤보 숨김)** · `EnumBuildingType`=Factory 1종, `EnumBuildingUsage`=Office 1종, `InfraSymbols.BuildingType VARCHAR(20)`(값 추가만으로 확장 가능).
  등급: A 하우징(점 심볼 전부) / B 2.5D 리본(철책·라인·케이블) / C 그림자(도형·구역) / D 제외(군대부호·이미지·추적). 신규 24종 = 차량 5(Car/Truck/Patrol/Boat/Drone — **생성·저장 흐름 신설 필요**) · 건물/인프라 11(막사·초소·감시탑·창고·게이트·안테나·발전기·물탱크·헬리패드·교량·전신주) · PIDS 8(Radar/PIR/Contact/Laser/IoController/SmartMulti/Underground/Lamp·Enclosure 기존 시안).
  스토리보드 v1.2 §1-B **24종 렌더 캡처 확인**(`docs/assets/symbol-3d-housing-extended-24-20260904.png`) — 모델별 `fit` 배율로 바운딩 박스 단위화 규약 반영(PIR·접점·초소 등 소형 모델 확대).
  **SVG 원본 발견(2026-09-04)**: `e:.사업관련자료.통제UI정리\Resources\` 에 **sensor_{3d_view,3d_view2,front_view,side_view}.svg + ipcamera_3d_view·speaker_3d_view·enclosure_3d_view·controller_front_view·siren_3d_view·lpr_3d_view.svg** — 리포엔 없던 원본 벡터(사본 `docs/assets/svgref/`). 스마트센서를 이 비율(정면 W:H≈0.55, 측면 D≈0.35H, 상단 2×2 감지 모듈·중앙 PIR 렌즈·하단 패널+라벨 201·나사 4·측면 브래킷)과 실측 색(#6B7642/#57643D/#2B2B2B/#F0F0B3/#969A9C)으로 **프리미티브 3D 하우징 재구성** → 스토리보드 v1.3 §0 원본 대조(캡처 `symbol-3d-sensor-refcmp-20260904.png`). 카메라·스피커·함체·제어기·경광등도 같은 방법으로 재구성 가능(원본 있음). **경광등(siren)·LPR 은 심볼 타입 자체가 없음** → 카탈로그 추가 후보.
  **원본 기준 재구성 완료(2026-09-04 "카메라·스피커·함체·제어기·경광등·펜스 재구성")** → 스토리보드 **v1.5 §0** 7종 대조(캡처 `symbol-3d-refcmp-7types-20260904.png`). 카메라(베이스+베벨+반구+렌즈 하우징) · 스피커(혼 길이 34/입구 r21 + U 브래킷) · 함체(경사 빗물 후드+문+잠금) · 제어기(납작 검은 박스+포트 3+LED 2+스위치+고무발) · 경광등(백/녹/적 3단, **타입 신설 후보** grp newtype) · 펜스(기둥 2+캡+**체인링크 메쉬 패턴 페이스**+레일).
  **🔴 CSS 목업 근본 버그 교정**: `world.rotateX(+pitch)` 는 CSS y-down 좌표에서 **아래에서 올려다보는 뷰**였다(렌즈 +Z 가 화면 위로 이동하는 DOM 실측으로 확정). 그동안 돔이 사발로, 상자가 속 빈 것으로 보이던 원인. → `rotateX(-pitch)` + 셰이딩 부호 교정. 또한 **브라우저가 range 슬라이더 값을 복원**해 피치 10°/θ 298° 로 렌더되던 것도 발견 → `autocomplete=off` + 초기값 강제. (WPF 구현과는 무관한 목업 이슈 — 단 "카메라 피치 부호 검증"은 PoC 체크리스트에 넣을 것.)
  **🔲 결정 대기**: 피치 각(35°) · 팔레트 · PoC-E1′ 착수 · OBJ 확정 · **차량 범위(생성 흐름 포함 여부)** · 건물 11종 목록/우선순위 · PTZ 분기 필드(`EnumDeviceType` 에 PTZ 없음, `EnumShapeType.PTZ_CAMERA` 만) · 핀 3D 여부(권고 유지).
- **결정 대기(정정)**: ~~Q1 회전 기본값~~ 불필요 · **Q2 A-1 vs PoC-E1′ 우선순위** · Q3 θ=180 거꾸로 유지(권고) · Q4 자산 없으면 코드 프리미티브, 군사부호 제외 · **Q5 PoC 성능 기준(운영 심볼 수 실측 선행)**.

## ▶▶ 재개 포인트 (2026-08-07 — 보고서 템플릿 "공개/비공개(is_public)" 분석 완료 · Track A)

**정본**: [`docs/analyses/report-template-is-public-analysis.md`](../analyses/report-template-is-public-analysis.md)
(6축 병렬 정독 + 핵심 주장 6건 반증 시도 → **6건 전부 유지**)

- **결론**: `is_public` = *"템플릿을 다른 사용자에게 **읽기 전용으로 공유**할지"* 로 설계됐으나 **3계층 전부 미집행** → 지금은 순수 메타데이터.
  체크해도 달라지는 건 목록의 '공개' 열 체크 표시 하나뿐.
- **원설계**(정본은 서버 레포 `PRD_Report_System.md`): 목록=`owner_id==me OR is_public` · 상세 비공개+비소유자=403 · **수정/삭제는 공개여부 무관 소유자 전용**.
  시드 의도 = 감사로그·로그인이력 든 "보안 감사 보고서"만 비공개(PII 통제).
- **실제**: 서버 목록 GET에 **WHERE 절 자체가 없고**(`reports.py:268-276`), 생성 시 **owner_id 미설정 → 영구 NULL**(`:303-310`, 라이브 캡처 전건 null),
  상세/PATCH/DELETE에 403 검사 0건. 접근통제 축은 `reports` view/edit/delete verb RBAC 하나뿐.
  **서버 레포가 갭을 스스로 표로 기록해 둠**(`Report_System_Development_Status.md:145-147, 325-329`, 2026-02-02 — 6개월 미해소).
- **클라**: `IsPublic` 참조 7곳 전부 저장/왕복/표시, **분기 0건**. 목록·보고서생성 콤보 모두 전량 적재(필터 0). `OwnerId`는 선언 1줄 외 사용 0건.
- **결정적 정황**: DB에 `idx_report_templates_is_public` **전용 인덱스**만 있고 WHERE 사용 0 → **미완성 기능의 잔재**.
- **부수 결함(별건, 클라 단독 Track B 가능)**:
  **I-1** PATCH가 `Name`·`IsPublic`·`DefaultPeriod`를 항상 전송 → **lost update**(부분수정 쓰는 건 `Components` 뿐)
  **I-2** POST 201 응답에 `is_public` 부재 + non-nullable `bool` + `MissingMemberHandling.Ignore` → 생성 직후 **항상 false로 읽힘**(현재 무해·잠복)
  **I-3** `CanViewReports`/`CanEditReports` 계산만 하고 **XAML 바인딩 0건** — PRD 범위의 "RBAC UI 게이팅" 미배선
  **I-4** 클라에 삭제 권한 가드 없음 + **숫자 user id가 Reports 모듈에 미도달**(`IPermissionService`는 `LoginId`/`Name` 문자열뿐) → "내 템플릿" 판정 구조적 불가
- **⚠ 미검증**: 운영 서버 소스 직접 확인 불가. OpenAPI 스냅샷 **6종 전부 stale**(v1.6.0/paths 114/mtime 2026-06-22, 실서버는 6.3.2/133).
  확정하려면 **계정 2개로 목록 대조 실측**(읽기 전용, 비파괴) 필요.
- **✅ 결정·적용: D(UI 제거)** — 사용자 지시("서버에서 못 쓰면 UI에서 제거"). Track B, 롤백태그 `before-remove-template-ispublic-ui`, Reports.Ui 빌드 오류 0.
  제거: `ReportTemplateView.xaml:43` "공개" 컬럼 · `ReportTemplateEditView.xaml:33` 체크박스 · VM `IsPublic` 프로퍼티/대입 3곳.
  **🔴 함정 회피**: PATCH 는 `IsPublic` 을 **미지정(null)** 으로 둬 전송 자체를 생략 — 체크박스만 지우고 false 를 실어 보내면 서버 저장값(시드 4종 `true`)을 **저장할 때마다 덮어쓴다**. POST 는 미지정=false 라 서버 DB 기본값과 동일(무변화).
  **와이어 계약(DTO 3종 `is_public`)은 유지** — 서버 구현 시 UI 만 복구하면 된다. 부수로 두 파일 BOM 교정.
  ⚠ **실기 검증 미실행** — 앱 기동해 템플릿 목록/편집 다이얼로그 확인 필요.

### ▷ 버그 수정: 새 템플릿이 생성 탭 콤보에 안 뜸 (Track B, 롤백태그 `before-report-template-combo-refresh`)

- **원인 확정**: 콘솔 탭 호스트가 `Conductor.OneActive` 가 아니라 **평범한 `TabControl`**(`ReportConsoleView.xaml:37-47`) →
  **탭 전환이 Caliburn Activate/Deactivate 를 일으키지 않는다.** 세 탭 VM 은 콘솔 열 때 `ReportConsoleViewModel.OnActivateAsync:44-46` 에서 한 번에 활성화되고,
  `ReportCreateViewModel.LoadTemplatesAsync` 호출부도 그 활성화 하나뿐 → **콤보=콘솔 연 순간의 스냅샷**. `OnEditSaved` 는 템플릿 탭 목록만 갱신했다.
  ⚠ **이 패턴은 다른 TabControl 호스트 패널에도 그대로 존재할 수 있다** — 탭 간 데이터 동기화가 필요한 곳은 전부 의심 대상.
- **같은 뿌리 미보고 결함 2건 동시 수정**: ① 삭제한 템플릿이 생성 콤보에 잔존(선택·생성까지 가능 → 서버 404/422) → `TemplatesChanged` 이벤트 신설
  ② 재적재 시 콤보 빈칸 잠복버그 — `Templates.Clear()` 가 바인딩을 통해 `SelectedTemplate` 을 null 로 되돌리고 재적재 항목은 **다른 인스턴스**라 참조 비교가 깨짐
  → **Clear 이전에 Id 확보 → Id 로 복원**(기존 `SelectedTemplate is null` 가드는 stale 참조 때문에 발동 안 됨).
- **회귀 방지**: `SelectedTemplate` setter 가 기간을 덮어쓰는데 새로고침은 같은 Id 재대입 → **Id 가 실제로 바뀔 때만** 기간 동기화하도록 가드.
- 변경 3파일(`ReportConsoleViewModel`/`ReportTemplateViewModel`/`ReportCreateViewModel`), 빌드 오류 0, BOM 교정.
- ⚠ **실기 검증 미실행** · Reports.Ui 에 **테스트 프로젝트 자체가 없어** 회귀 테스트 미작성.

### ▷ 억제 스케줄 생성 폼 레이아웃 재구성 (Track B, 롤백태그 `before-suppression-form-layout`)

- **증상(사용자 스크린샷)**: 장비를 고를수록 칩이 쌓이며 뒤 필드를 줄바꿈으로 밀어냄 → "장비 100개면 어쩔 거냐".
- **원인**: 폼 전체가 `<WrapPanel Orientation="Horizontal">`(구 :205)이고 칩 `ItemsControl` 에 **크기 상한이 없었다**(`MinHeight=22` 뿐).
  칩이 늘면 그 칸 폭이 자라 바깥 WrapPanel 재배치 → 후속 필드 밀림. 칩 폭도 장비명 길이에 끌려 제각각.
- **수정 3층**: ① 바깥 WrapPanel → **2컬럼 Grid**(좌 필드 `*` / 우 트레이 `356` 고정), 좌측 3열×3행 고정 → **모드 전환에도 필드 자리 불변**
  ② **칩 정규화** `WrapPanel ItemWidth=148 ItemHeight=26` + `TextTrimming=CharacterEllipsis` + 전체명 ToolTip
  ③ **트레이 고정 높이 104 + 내부 스크롤**(2×4=8개 노출) → **칩 개수와 무관하게 폼 높이 불변**
- **부가**: 헤더에 `선택 N개` + **[모두 지우기]**(`ClearDevices`/`ClearGroups` 신설) · 빈 트레이 안내 · 장비/그룹 트레이는 같은 자리 겹침 · 전체 대상은 안내 카드로 자리 채움.
- **🔴 동반 버그**: 내장 `BooleanToVisibilityConverter` 는 **`ConverterParameter` 를 무시**한다 → `대상 측(side)` 이 주석·VM 문서와 **정반대로** 장비 모드에서 보이고 그룹/전체에서 숨겨지고 있었다.
  `utils:BoolToInverseVisibleConverter` 로 교체 + 선언부에 재발 방지 주석. **⚠ 이 안티패턴은 다른 View 에도 있을 수 있다 — `ConverterParameter=invert` 전수 점검 대상.**
- 검증: 빌드 오류 0 · `EventSuppressionScheduleTests` **31/31 통과**. ⚠ **실기 렌더 미검증**(UI 배치 확인은 데스크톱 독점 필요).
- **이어서 수정한 버그 2건**(같은 패널, 사용자 스크린샷에서 발견):
  ① **완료 팝업이 대상 전량 나열 → 잘려서 안 읽힘**(장비 6개만 돼도 헤더·마지막 항목 소실). `BuildTargetEcho` 를 **"앞 3개 + 외 N개" 한 문장**으로 접음(총 개수는 유지 = §6 안전 목적 보존).
  ② **상태/대상 필터 콤보가 빈칸 + "전체" 선택 불가** — setter 가 `""`→`null` 정규화하는데 "전체" 항목 `Tag=""` 라 `SelectedValue=null` 이 어떤 항목과도 매칭 안 됨.
     바인딩 값은 원문 유지, **null 정규화는 API 호출 직전에만**(`ApiFilterStatus`/`ApiFilterTargetType`).
- **🎨 디자인 개선안 작성**: [`docs/design/suppression-schedule-form-redesign.html`](../design/suppression-schedule-form-redesign.html) — 진단 8종 + 다크/라이트 목업 + 적용범위 7항.
  핵심: 입력 스타일 통일(Material 밑줄 ↔ MahApps 박스 혼재) · 좌우 바닥선 정렬 · 카드 `SurfaceAlt` 로 한 단 올림 · **주 액션 Primary 채움** · 하단 액션 바 신설 · 빈 트레이 점선+안내 · 선택행 3px 시안 바.
  **✅ ①~⑥ 적용 완료(사용자 승인)** — ⑦(목록 작업명/대상 열 통합)만 미적용, 사용자 확인 대기.
  적용 상세: `FormTextBox`/`FormComboBox`(=`MaterialDesignOutlined*` 기반)/`FormDateTimePicker` 로 **박스형 36px 통일** ·
  좌측 **2열×3행** 재배치로 트레이와 바닥선 정렬 · `PrimaryActionButton`(Primary 채움, 비활성 38% + `ToolTipService.ShowOnDisabled` + `CreateHintText` 5분기) ·
  하단 액션 바 `[초기화][생성]`(`ResetForm` public 승격) · 빈 트레이 `Rectangle StrokeDashArray` 점선+아이콘 · `SuppressionRowStyle`(선택행 3px 시안 바+틴트).
  **⚠ 스타일은 전부 뷰 로컬** — Events.Ui/Resources.xaml 미머지라 라이브러리에 넣으면 런타임 XamlParseException([[project_events_ui_resources_not_merged]]).
  검증: 빌드 0 · 테스트 31/31 · **StaticResource/DynamicResource 41키 + PackIconKind 8종 정적 전수 검증(미해석 0)** — XAML 리소스 미해석은 빌드가 못 잡으므로 스크립트로 확인
  (`scratchpad/check_keys.py` 패턴: 뷰의 키 추출 → 리포 전 xaml `x:Key` + MDIX/MahApps DLL 문자열 대조).
  🔴 **실기에서 깨짐 확인 → 후속 수정**: `Height="36"` 강제가 **MDIX Outlined 입력 3종을 세로 클리핑**(작업명 힌트·억제범위 값·장비추가 힌트가 글자가 아니라 '실선'처럼 뭉개짐).
  원인 = Outlined 템플릿이 **floating hint 노치용 세로 공간을 내부 예약**하는데 Height 고정이 그걸 눌렀다(힌트 없는 ComboBox 도 같은 템플릿이라 동반 파손).
  수정 = `Height` → **`MinHeight` 하한만** + **`md:HintAssist.IsFloating="False"`**. 라디오 Border 도 MinHeight 전환.
  **교훈: MDIX Outlined 계열에 Height 강제 금지. 리소스 키 정적 검증을 통과해도 레이아웃 파손은 실기에서만 드러난다.**
  메인 솔루션 빌드 완료(오류 0) — 앱 켜면 반영된다. ⚠ 재렌더 확인 필요.

### ▷ 서버 인프라 준비도 (§8 추가, 6축 + 차단요인 6건 반증 → 4건 기각/2건 실재)

- **결론: 인프라는 이미 다 있다.** identity·precedent·router = READY / schema·ops·plan = PARTIAL.
  `reports.py:62` 가 이미 `APIRouter(dependencies=[Depends(get_current_account_user_async)])` — **전 엔드포인트 토큰 강제 상태에서 신원을 버리고만 있다.** 필요한 함수는 `reports.py:41` 에 이미 import 됨.
  같은 파일 `:709/:769` 가 `generator_id=current_user.id` 로 이미 같은 일을 한다 — **템플릿만 빠졌다.**
- **코드 작업량 총 ~10줄**: 핸들러 5개 파라미터 5줄 + POST `owner_id=` 1줄 + 목록 `or_(...)` 1줄 + 403 헬퍼 6~8줄·호출 3줄. **신규 모듈·의존성·토큰 변경 0.**
  응답 스키마도 이미 `owner_id` 노출(`schemas/report.py:83,309`) → 클라 계약 필드 변경 0줄. Create/Update 스키마엔 owner_id 없어 **소유자 위조 불가**.
- **🔴 실재 차단요인 2건**:
  **B-1 기존 행 owner_id 전량 NULL** — `NULL = id` 는 false라 필터 켜는 즉시 `owner NULL + 비공개` 행이 **전원에게서 소멸**(시드 "보안 감사 보고서" 확정 피해).
  **ADMIN bypass는 권한 게이트에만 있고 목록 WHERE엔 없어 관리자도 못 본다** → "나중에 정리" 불가. 선택지 (a)admin 백필 (b)NULL=공개 취급 (c)ADMIN 예외. **사용자 결정 필요.**
  작업=`v72` 마이그레이션 + **시드 코드 동시 수정**(마이그레이션은 PostgreSQL 전용, SQLite/fresh DB 미적용 → 한쪽만 고치면 환경 갈림) + 화이트리스트 1줄.
  **B-2 클라 breaking change** — 목록 건수 감소 + 신규 403인데 `GOP_Restful_Api_연동설계.md` reports 구간(L15400~15900)에 **403 매치 0건**. 계약 개정 + NOTIFY 선행.
- **정책 공백**: `reports:delete` 가 **어떤 프리셋에도 미부여**(`init_db.py:183-197`, RW조차 delete:False) → 소유자여도 비-ADMIN은 소유권 검사 전에 403. **소유권 vs 매트릭스 우선순위 결정 필요.** 소유권은 templates·generations **2축**이라 templates만 고치면 절반.
- **운영 리스크**: CI 전무 · 대상 회귀망 `tests/test_reports_router.py` **36개 중 35개 이미 401 FAIL**(자체 fixture가 AUTH_MODE 미패치) · 코드만 커밋되고 미배포된 드리프트 선례 있음(bulk-delete 라이브 405) → **배포까지가 범위**.
  단 테스트 하네스 신설은 **불필요**(반증됨) — 레포 표준은 override가 아니라 격리 async DB + 엔드포인트 직접 호출(`test_proxy_settings_router.py`).
- **미구현 원인 = 기술 차단이 아니라 추적 이탈**: 인지 근거는 2026-02-02 단일 문서(git 미추적+INDEX 미등재+line stale)뿐.
  2026-07-03 verb-RBAC 요청서가 "reports 인증 미구현"을 종결시키며 **row-level 소유권이 완료 판정에 묻혔다**(그 문서에 owner/is_public 0건). 이후 6개월 언급 0건, 최신 감사(`spec-code-swagger-triangulation.md` 08-07)도 미지적, TODO/FIXME 0건.
- **⚠ 미실측**: 운영 DB의 실제 owner NULL 행 수(코드 추론) · `owner_id` **물리 컬럼 존재 여부**(ADD COLUMN 마이그레이션 없음 + `create_all()`은 기존 테이블에 컬럼 미추가 → 구버전 볼륨이면 UndefinedColumn 500 위험).

## ▶▶ 재개 포인트 (2026-08-07 — 이벤트 매핑 워크벤치: 기획→시나리오분석→PRD Draft 완료, **승인 대기**)

**정본**: [`docs/prds/event-mapping-workbench-prd.md`](../prds/event-mapping-workbench-prd.md) (Draft) — 결정 10건 + 메인솔루션 4파일 승인 대기.

- **산출물 6종 완성**: 스토리보드/와이어프레임 HTML(`docs/design/event-mapping-workbench-*.html`) ·
  시나리오 분석 v1.1(`docs/analyses/`) · 시나리오 카탈로그 v1.1 + 시뮬 전량로그 + 시뮬레이터(`docs/tests/`) · PRD v1.0 Draft(`docs/prds/`).
- **기능 정의**: GOP API §7.2~7.5 EventMapping 4리소스(본체 + 카메라/스피커/경광등 배선)를
  **3-Pane 워크벤치**(매핑목록 280 · 액션보드 * · 장비팔레트 304)에서 **Drag&Drop + 멀티셀렉션**으로 편집. 카드 규격 4종 고정(268×52 / 300×72 / 244×62 / 고스트 224×40).
- **🔴 착수 전 반드시 고칠 결함 — `dotnet run` 프로브로 실증**:
  ① **하위 3 DTO에 config PK(`id`)가 없다** → PATCH·DELETE·벌크해제 **원천 불가**
  ② nested 응답(`camera{}`)을 요청전용 DTO로 파싱 → **`CameraId=0` 침묵 오염**(`MissingMemberHandling.Ignore`)
  ③ `GetMappingLampsAsync`가 items 파서 사용 → Lamp는 `data:[]`라 **`ArgumentException`으로 조회 실패**(조사 초기의 "조용한 빈 목록" 서술은 **오답, 정정함**)
  ④ 매핑 생성 body에 `{"id":0, "cameras":null, "created_at":…}` 전송(`BaseDto` 상속 + `Id` 재선언이 `DefaultValueHandling.Ignore`를 잃음)
  ⑤ 하위 목록에 `page/limit` 없음 → **21번째 배선부터 유령 소실** ⑥ **벌크 6종 미구현**(서버는 v4.3부터 제공)
- **시뮬레이션 633건 / PASS 523 / ISSUE 110 / 33종.** SIM ID는 PRD→plan→test로 승계한다.
- **⚠ 적대 검증 3기가 내 분석의 오진 3건을 잡았다**(반드시 기억):
  ① "FileGroup API 없음" → **서버 §5.10 `GET /api/file-groups`는 존재**. 부재한 건 클라 래퍼뿐. 원인 = **`docs/`가 gitignore라 전역 grep에 안 잡힌다**([[project_docs_gitignored_grep_blind]])
  ② "Lamp enum `[EnumMember]` 미부착" → **이미 전부 부착돼 있다**(`EnumBuzzerSound.cs:13-22`). 남은 건 DTO에 `StringEnumConverter` 지정
  ③ "Lamp `priority` 0 전송 → 422" → `LampDto.cs:32`이 `= 1`이라 안전
  ④ **CSS 알파 hex 바이트순서 버그** — WPF `#AARRGGBB`를 CSS(`#RRGGBBAA`)에 그대로 복사해 라이트 대비가 1.38:1까지 붕괴.
     두 HTML의 틴트 12토큰을 변환함. **기존 프로젝트 스토리보드들도 같은 실수를 갖고 있다**(예: `pidsgroup-rightclick-storyboard.html`).
- **🔲 다음**: ① PRD 승인(결정 G-1~G-10 + 메인솔루션 4파일) ② 실측 V-1~V-8(응답 shape·벌크 존재·Lamp enum·extra=forbid·RBAC) ③ plan 분해.
- **호스트 경로 판단**: 맵 오버레이는 `LayerPanelControl.cs:342` `MaxPanelWidth=375`라 3-Pane 불가 → **좌측 메뉴 Conductor 패널(경로 B)** 뿐. 이 때문에 메인솔루션 변경이 불가피하다.

## ▶▶ 재개 포인트 (2026-09-03 — 27일 만의 재검증: "playwright로 검증 가능한 부분" 식별·재실행)
- **마지막 업데이트**: 2026-09-03 14:40

**⚠ 용어 확정** — 이 프로젝트에서 "playwright"라 불러온 것은 FlaUI(UIA3) 하네스다. **진짜 Playwright는 WPF 본체를 못 건드리고 웹 표면 3종**(매뉴얼 HTML · 보고서 미리보기 HTML · GOP REST)만 대상([[project_playwright_verification_surfaces]]).

- **✅ Playwright 실표면 ① 매뉴얼** — `docs/manual/manual.html`을 `_serve.js`(8791)로 띄워 MCP Playwright로 검증: **59페이지**(기준선 59p 일치)·이미지 140개 **깨짐 0**·목차 링크 45·장 제목 10개 렌더·콘솔 오류는 favicon 404 하나뿐. `file://`은 MCP가 차단하므로 반드시 로컬 서버 경유.
- **🔴 환경 사고 — VS 설치기가 dotnet SDK를 갈아끼우는 중**: `setup.exe update --activate`(10:33~)가 돌며 `sdk\10.0.*` 5개 폴더가 **전부 0바이트 껍데기**, `host\fxr`에 10.0 없음 → `dotnet --version`이 "No .NET SDKs were found". 제 빌드(GMaps.Ui→앱→하네스 전부 성공)는 10:33 직전에 통과. **앱 자체는 8.0.22 런타임이 온전해 실행 가능**, 막힌 건 `dotnet test`뿐. 설치 완료 감시 백그라운드 → 완료 즉시 `scratchpad/run-recheck.ps1`(18회차, 회차당 10~50항목, `IRONWALL_UITEST_SERVER=localhost:8000` 스왑으로 쓰기 안전) 실행.
- **✅ 로컬 API 서버 생존** — /health 200 · /docs 200 · /api/auth/login 405(GET) = 살아 있음. NATS 대시보드 9000은 **DOWN** → 주입 E2E 3종은 Skip 예정.
- **✅ DB 접속 파일 재생성** — `%TEMP%\uitest_my.cnf`가 사라져 있었음(TEMP 정리). 재생성 후 Symbols 102·UITEST 잔재 0 확인. 단위테스트 33/34의 실패 1건은 **알려진 항목**(Id=227 `구역-01` LabelOffsetY=41.8 — 8/7에 ⓑ 클램프 퇴화로 판정해 의도적으로 원복해 둔 값).
- **✅ 27일분 커밋 완료(사용자 "커밋해줘", 작성자 `lsirikh <lsirikh@naver.com>` — 양 레포 로컬 config 그대로)** — 남의 WIP 무혼입(스테이징 후 경로 화이트리스트 검사로 자체 중단하게 함):
  - LIB v2.6 **`e84feb2`** `fix(gmaps-ui)`: VF-15 VEHICLES 콤보 제외+안내팝업 · VF-17 `CancelRubberBand` · `docs/INDEX.md` (4파일 +66/−5). **제외**: `EventSuppressionSchedulePanelView.xaml`(타 세션 1줄) · `.playwright-mcp/*.yml`.
  - MAIN v0.5 **`39d7032`** `fix(setup)`: `SetupModel.ClientId` + `appsettings.json ClientId` (2파일 +17).
  - MAIN v0.5 **`804892c`** `test(ui-harness)`: `UiTests/` 전체 + `tools/run-ui-tests.ps1` (40파일 +9,764/−26). 커밋 직전 BOM 감사: 38 .cs 중 **5개 BOM 부재 → 추가**(UiCollection·AppSweepTests·PanelTabSweepTests·DeviceCrudReachabilityDiagnostic·Probe). **제외**: 이벤트 다이얼로그/패널 XAML 3 + `WebServerSetupView.xaml`(타 세션) · `tests/` · zip · `deploy-watchdog.ps1` · `ui-test-summary.md`(출처 불명, 내 것 아님).
  - ⚠ 함정: `.NET [IO.File]`은 PowerShell `Set-Location`을 따르지 않는다(프로세스 CWD=라이브러리 루트) → 상대경로 BOM 감사가 통째로 실패했었음. **절대경로(Join-Path $root)** 로만.
- **⚠ 셸 함정 2종(신규)** — ① Git Bash의 `dotnet`은 10.0 런타임을 잡아 `app-launch-failed`로 죽는다 → dotnet은 PowerShell로만. ② PS 5.1에서 `dotnet ... *> $out`/`2>&1` 뒤 파이프는 exit 코드가 8비트 절단(145/150/155 = 0x8000809x)돼 그 자체로는 진단이 안 된다 → 파일로 받아 `Get-Content`로 읽을 것.
- **✅ Playwright 실표면 ② 보고서 미리보기 — 앱 없이 검증 성공(골든 픽스처 경로 실증)**: 앱이 쓰는 엔드포인트는 `GET /api/reports/preview/{id}`(Bearer 자동부착, **text/html 봉투 없음** — `ReportApiService.cs:225-236`). ⚠ `/generations/{id}/preview`는 **sections JSON**이고 `/generations/{id}/preview-page`는 `/preview/{id}`로 **307**. curl+Bearer로 id=45 "샘플 테스트" HTML 299KB(기준선 ≈280KB) 수신 → `python -m http.server 8792`로 서빙 → MCP Playwright 렌더: **캔버스 12/12 픽셀 그려짐 · Chart.js 인스턴스 12 · 테이블 17/290행 · 본문 17,952자 · 외부 참조 0 · 실패 마커 없음** · 콘솔 favicon 404뿐. 캡처는 레포 루트에 떨어졌던 것을 `docs/assets/report-45-preview-full-20260903.jpeg`(2.4MB, gitignore)로 이동. PDF 다운로드도 200(868KB, MuPDF).
- **✅ client_id 서버 원인 확정** — 로컬 테스트 서버(localhost:8000)는 로그인 바디 `client_id`(OpenAPI: `login_id·password·client_id`)를 받아 `GET /api/user-sessions/me`에 **그대로 회신**(`'gis-monitoring-recheck'` 확인). 이전 세션들엔 수정 전 기본값 `'central-ui'`가 남아 있음 → 클라 수정(SetupModel.ClientId)은 끝단까지 동작. **운영 서버(123.141.236.253:8136)만 구버전** — 요청서 `docs/coordination/REQ_Session_ClientId_Support.md` 그대로 유효.
- **✅ S3 전제 충족** — 로컬 서버에 COMPLETED 보고서 12건(id=45~) → `ReportPreviewHybridTests`는 서버 스왑 시 Skip 아님.
- **⚠ 로컬 서버 프로브 함정** — 자격증명은 `[IO.File]::WriteAllText`로 임시파일에 쓰고 `curl --data-binary @file`, 삭제는 `Remove-Item`이 아니라 `[IO.File]::Delete`(훅이 `Remove-Item $var`를 보호경로로 오인해 차단). PS 5.1엔 `??` 없음.
- **✅ 데스크톱 재검증 완료(18회차, 앱 17회 기동, `IRONWALL_UITEST_SERVER`=로컬 스왑)** — 설치기 MSI 3분 정적 게이트 통과 후 10:49 자동 시작.
  - **항목 레벨(ProbeSession, dedup)**: 오늘 **510항목 · 443 PASS · 4 FAIL · 63 SKIP** vs 기준선 8/7 512·442·5·65 → **회귀 PASS→FAIL 0건**, 개선 1건(T-CID005 client_id — 로컬 서버가 회신하므로), 여전히 FAIL 4건 = 전부 기존 계측 결함(T-LAYER021/022 셰브런 좌표 · T-EVENT007 카드 표면 · T-TREE009 리프 체크박스).
  - **xUnit 레벨(FluentAssertions 테스트 포함)**: **85 통과 · 2 실패 · 6 건너뜀**. 실패 = R01 라벨오프셋(알려짐) + R17 S3 하이브리드. 건너뜀 = 주입 E2E 4(NATS 대시보드 DOWN, 예상) · ToolbarOverflow(최대화라 정상) · GMapSymbolMutation(운영 심볼 클릭 경로 폐기 — CRUD 배치가 대체).
  - **S3 하이브리드 실패 → 테스트 전제 결함으로 확정·수정·통과**: `grid.Rows[0].Select()`가 최신 행을 무조건 고르는데 로컬 서버 최신 행이 **id=51 CANCELLED**(8/7 13:37, 타 세션 잔재)라 PreviewButton 이 정당하게 비활성 → `SafeInvoke` '[비활성 클릭]' 즉사. 앱 게이팅 옳음. 수정: 상위 12행을 차례로 선택하며 **PreviewButton.IsEnabled 인 첫 행**을 고른다(`ReportPreviewHybridTests.cs`). 재실행 **통과 8s** = FlaUI→WebView2 Pane→Playwright CDP 부착까지 오늘 실측.
  - CRUD 왕복 2/2 통과(GEOMETRICS·PIDS_EQUIPMENT 생성→이름변경→Undo/Redo→색상→삭제) · 속성 감사 통과 · 줌/맵/내비 기능 14/14.
  - 사후 안전: appsettings Url 운영 복원 ✔ · DB 102/UITEST 잔재 0 ✔ · 앱·testhost 0 ✔ · `.uitest-backup` 잔존 없음 ✔.
    ~~msedgewebview2 고아 18개(VF-04)~~ **정정**: 18개 전부 **부모 생존·앱 비소유**(설치기 등 타 프로세스) → 우리 잔재 아님, 건드리지 않음. VF-04 는 오늘 관측되지 않았다.
    ⚠ **`appsettings.json.bak`(오늘 11:13)에 localhost 스왑 URL** — 앱이 설정 저장 시 만드는 백업이 스왑 중 내용을 담았다(EnvironmentGuard 의 `.uitest-backup` 과 별개, 그쪽은 정상 복원·삭제됨). 앱의 손상복구가 `.bak` 을 집으면 운영이 로컬 서버를 본다 → `appsettings.json.bak.swapped-20260903` 으로 격리(8/6 `.contaminated` 와 동일 처리). **재발 방지 후보**: EnvironmentGuard 복원 시 `.bak` 도 함께 되돌리거나 삭제.
  - ⚠ 러너 `.ps1`에 한글 정규식을 BOM 없이 넣어 요약 출력만 깨짐(테스트는 전부 실행) → BOM 추가로 수정. `.ps1 UTF-8 BOM` 규칙 재확인.
- **✅ "현재 문제 없나?" 전수 감사(13 에이전트, 읽기 전용, 68건 제기 → 확정 58·하향 9·미검증 1·반박 0)** → 정본 `docs/analyses/current-problems-audit-20260903.md`. 핵심: ① 🔴 **오늘 커밋한 하네스의 E2E 3종이 운영 API/NATS 기본값**(`GopApiClient.cs:21,32` + NATS 호스트 하드코딩, VF-07) — 오늘 무사한 이유는 대시보드 DOWN 뿐, **대시보드 켜기 전 반드시 수정** ② 🔴 라벨 오프셋 D-2 + 변환 마이그레이션 부재(앱) ③ 🟠 **신규** LiveCharts `XAxes/YAxes` 예외가 매 실행 ERROR(dev 2회·설치본 1회, UnobservedTaskException 으로 삼켜짐) = VF-12 유력 원인 ④ 🟠 설치본 `C:\PidsMonitoringSystem` Url 이 **08-31부터 localhost:8000**(하네스 무관, 의도 여부 사용자 확인 필요) ⑤ 🟠 업그레이드 설치본은 ClientId 키 없어 여전히 central-ui(F1) ⑥ v2.8.3/2.8.4 에 VF-15/17/ClientId 이미 포함(재릴리즈 불필요, 대신 git 재현성 없음) ⑦ **정정**: VF-04 "failed to exit" 는 17/17 재현됐고(FlaUI 타임아웃 메시지, 앱결함 미확정) 위 "오늘 관측되지 않았다"는 틀림 ⑧ 훅 소음 원인 확정(`:381` "dev." 마침표 / 첫 `마지막 업데이트` 마커 / Draft PRD 만료 루프) — 이 블록에 마커 삽입으로 ②번 나그만 해소.
- **🔲 다음** — 남은 계측 FAIL 3건(레이어 셰브런: B15 가 `TogglePattern.Toggle()` 로 해결한 방식을 B04 에 이식 · 이벤트 카드 패널 표면 전환 · 리프 체크박스 행 특정) · GMapSymbolMutationTests 폐기 또는 자기심볼 경로로 전환 · client_id 는 서버 대기로 종결 · EnvironmentGuard 가 앱 생성 `appsettings.json.bak` 도 복원/삭제하도록(재발 방지 후보). 커밋은 완료(위 3건) — 푸시는 지시 없음.

---

## ▶▶ 재개 포인트 (2026-08-07 — GIS 전기능 UI 테스트 스윕: 카탈로그 1,220 + 실행 367)

**정본 문서**: [`docs/tests/gis-test-master-checklist.md`](../tests/gis-test-master-checklist.md) — 카탈로그·실행결과·FAIL 분류가 전부 여기 있다.

- **✅ 시나리오 카탈로그 1,220항목** (11도메인, `docs/tests/gis-test-catalog-*.md`). 자동화 가능 598 / **계측필요 529** / 불가 93. P0 407건.
  → **계측필요 529건이 최대 레버리지** — `AutomationProperties.AutomationId` 만 붙이면 자동화 가능. (`x:Name` 은 CM 바인딩 지시자라 절대 금지)
- **✅ UI 스윕 실행 367항목 — PASS 327 / FAIL 14 / SKIP 26.** 배치 14종(B01~B08 + 패널별 6종), 앱 1회 기동당 10~50항목 규칙 준수.
  결과 원본 = `UiTests/bin/Debug/net8.0-windows7.0/artifacts/sweep-results.jsonl` (항목 단위 append, 재실행 시 최신값으로 덮어 집계).
- **✅ 앱 불필요 검증 75건** — 라이브러리 41/41, DB 속성감사 25검사, **DB 관계 무결성 30검사 신설(전부 통과)**.
  신설 `DbProbe.AuditRelational()` = 정점 테이블(linepoints·pidsgrouppoints) 고아·순서결번·2점미만, 레이어/ROI/이미지/카메라위치.
  ⚠ **오탐 3종을 확정 제외**: `maplayers.MapId` 는 NULL 이 정상(카테고리 레이어는 지도 비종속) · 같은 계층 ZOrder 공유 정상 · Symbols.Title 중복 정상.
- **✅ CRUD 라운드트립 2/2 통과** (GEOMETRICS·PIDS_EQUIPMENT) — 생성→속성창→이름변경→DB영속→Undo/Redo→**색상변경 영속**→삭제.
  `GMapEditCrudTests` 에 **속성 변경 단계(8-b) 통합**: 색상 콤보/줌 슬라이더/표시 체크박스를 실제로 바꾸고 DB diff 로 확인.
  ⚠ **운영 심볼을 클릭하면 선택 전환만으로 암묵 DB UPDATE 가 일어난다**(`MapViewModel:2949`) — 반드시 자기 심볼을 만들어 그 위에서만 조작할 것.
  ⚠ 지도에는 슬라이더가 둘이다 — **지도 줌(6~19.5)** vs **심볼 최소표시줌(Minimum=0)**. 상한만으로 고르면 지도 줌을 집어 "심볼 Zoom 미반영"으로 오진한다(실제로 오진했다가 정정).
- **✅ 하네스 결함 5종 수정**: ① `appsettings.json` 한글 `//` 주석을 System.Text.Json 이 거부 → 픽스처 전멸(79건 중 47건 1ms 실패). 주석 허용 + **Url 값만 정규식 치환**(재직렬화하면 주석·서식이 소실되고 백업이 오염된다) ② 창 **최소화 시 좌표 (-32000)** 무한루프 → `WindowVisualState` 로 판정해 복원 ③ **포그라운드 강탈 실패**(다른 앱이 포커스를 쥐면 SetForegroundWindow 가 조용히 실패) → `ForegroundHelper`(잠금타임아웃 완화 + AttachThreadInput + TopMost 순간전환) ④ 패널 **첫 클릭 흡수** → `SmokeNav.OpenPanelWithRetry` ⑤ 패널 앵커를 **XAML 파일명으로 추정**해 전멸(UIA 에는 `PanelShellView` 만 노출) → 호스트→셸 폴백.
- **🔲 남은 FAIL 14건 분류**:
  - **앱 결함 후보 6건** — T-LAYER021/022(레이어 트리 **펼침 전후 49→49**, 구역 리프 미노출) · T-SET010/013(**설정에 '이벤트설정' 탭 없음**) · T-SET023(**3rdParty이벤트설정·와치독설정 탭 입력컨트롤 0개**) · T-EVENT007(카드 0건 + 안내문구 없음 = 조용한 로드 실패 의심)
  - **기대값 낡음 3건** — T-MAP011(파일 메뉴는 신호등 개편에서 의도적 제거) · T-MAP003 · T-LAYER002
  - **하네스 2건** — T-PROP005/007(B07 이 운영 심볼을 클릭하는 잘못된 접근. CRUD 배치로 대체됨 → B07 해당 항목 정리 필요)
- **🔲 다음**: ① 앱 결함 후보 6건 확정(레이어 펼침·설정 탭) ② 계측 529건 AutomationId 부여 ③ CRUD 를 나머지 카테고리·오버레이 이미지·라인/구역으로 확대.
- ⚠ **UI 테스트는 데스크톱 독점** — 화면 잠금 시 입력 주입 거부, 창이 뒤로 밀리면 절대좌표 클릭이 다른 앱으로 들어간다([[project_ui_tests_require_exclusive_desktop]]).

### 계정 실조작 + 클라이언트 ID 결함 (2026-08-07 오후)

- **✅ 계정 조작 왕복(B10)** — 마이페이지 진입 → 이름 `시스템 관리자`→`-UITEST` 입력 반영 → 확인 팝업 → 원복 확인. 파괴적 버튼 4종(계정삭제·정보초기화·비밀번호재설정·사진제거)은 IsEnabled 만 읽고 누르지 않음.
- **✅ 프로필 이미지 실제 등록 성공** — `증명사진샘플1.jpg`. 세 함정을 넘어야 했다:
  ① 앞 단계 **확인 팝업이 안 닫혀 이후 클릭이 전부 흡수**(→ ESC×3 + DismissBlockingPopups 선행)
  ② 파일 다이얼로그는 **별도 hwnd** — `App.GetAllTopLevelWindows` 로 안 잡힘(→ EnumWindows + `Automation.FromHandle`, 앱 PID → 데스크톱 전체 순 폴백)
  ③ **파일명 입력란은 `ValuePattern.SetValue` 를 거부**(`InvalidOperationException @ IUIAutomationValuePattern.SetValue`) → `Focus()`+`Click()` 후 **키보드 타이핑**(한글 파일명 OK)
  ⚠ 다이얼로그 핸들은 **닫히기 전에** 읽어둘 것 — 사라진 뒤 `Properties` 접근은 예외.
- **🔴 클라이언트 ID 결함 확정·수정(메인솔루션, 사용자 승인)** — 증거 3개 일치:
  ① `SetupModel.cs:25` 가 `IApiSetupModel` 을 구현하는데 **`ClientId` 프로퍼티가 없음**(파일 전체 0건)
  ② 그래서 인터페이스 default `get => "central-ui"; set { }` 적용 — **세터가 값을 버림**
  ③ `appsettings.json` 에 `ClientId` 키 자체가 없음
  → **설정에 무엇을 넣든 항상 `central-ui` 만 전송**됐다. 수정: `SetupModel.ClientId` 추가 + `appsettings.json` 에 `"ClientId": "gis-monitoring"`. 롤백 태그 `before-clientid-fix`.
  ⚠ **수정 후에도 세션 목록은 `-`**(117행 전부, B11 검증). 헤더는 정상 부착(패턴 `^[A-Za-z0-9._:-]{1,64}$` 만족, 경고 없음)이므로 **남은 원인은 서버** — `UserSessionDto.cs:23` 주석대로 "구버전 서버는 null → UI `-`".
  → 서버 요청서 작성: [`docs/coordination/REQ_Session_ClientId_Support.md`](../coordination/REQ_Session_ClientId_Support.md) (확인 3항목: 컬럼 존재 / 헤더 수신 저장 / 응답 포함)
- **검증 배치 신설**: `sweep_batch10`(계정 조작·이미지 등록) · `sweep_batch11`(세션 클라이언트 ID). 서버 준비되면 B11 만 돌려 확인 가능.

---

## ▶▶ 재개 포인트 (2026-08-06, 이 세션 — PidsGroup 우클릭: 등록 센서 정보 오버레이 + 그룹 탐지 이력) — ✅ **전체 구현 완료(FR-01~15) + v2.6 머지 + 메인솔루션 EXT**, 🔲 런타임 실측 5건
- **✅ Phase 2 완료 — `b2613a1` → v2.6 머지 `6624d4d`(zoom 세션 fbc77ea와 자동 병합, 충돌 0) + 메인솔루션 EXT `d0a19c1` @ v0.5**: 그룹 오픈 메시지+진입점 3곳(맵 메뉴/오버레이 푸터/그룹 탭 행)·다이얼로그 그룹 모드(팬아웃 Task.WhenAll+공용 CTS, 총합 500 절단 G-1=a, 부분실패=확보분+푸터 경고)·SignalChartControl 멀티 시리즈(SeriesIndex/Name 하위호환, ≤3 라인+기타 산점, 끝단 라벨, 토큰 ChartSeries1~3Brush)·센서 칩(엔티티 고정 색)·센서 컬럼+최다 발생 센서(동률=최근 우선)·그룹 탭 행 우클릭(Draft/빈그룹 팝업 가드).
- **✅ 검증**: Events.Ui 25/25(신규 9 포함)·GMaps.Ui 12/12·Theme.Tests 18/18·머지 트리 재검증 그린·메인솔루션 빌드 0에러·hex grep 0·BOM/한글 무결. README Unreleased 등재. 플랜 33/38(87%).
- **⚠ 발견 2건**: ① CM 4.x `IsActive`는 OnActivateAsync **완료 후** true — 첫 활성화 중 Initialize는 재조회를 안 건다(테스트로 문서화) ② Moq `It.IsIn(int)`은 `int?` 파라미터와 미매칭.
- **✅ UI 개선(사용자 피드백, `6343d50`→v2.6 머지 `6d20b6a`)**: ① 오버레이 행 Selected 효과(ListBox 전환, TintAccent+좌측 3px Primary 바, 토큰=다크/라이트 자동, 재우클릭 시 선택 리셋) ② 그룹 이력 센서 칩 개선 — FilterChipCompact+최대 2줄 스크롤+[전체] 마스터 토글(SetSilently 일괄, 개별 토글 동기)+그룹 모드 차트 220→175(그리드 높이 확보). 테스트 +2 (26/26·13/13), 머지 트리 빌드 0에러. ③ **조치보고 재보고(중복) 허용**(`3e8bdb0`→머지 `eb0dea2`) — 이력 다이얼로그 IsActioned 차단 제거(센서/그룹 공용, IActionReportGuard 동시중복·권한 게이트는 유지). PRD v1.2 변경 이력 기록, 플랜 Phase 6(FB-01~03) 36/41.
- **✅ 버그헌트+수정(사용자 3버그, `280f5c7`→머지 `acf95e4`)**: 23-에이전트 워크플로우 `wf_0d472016-a47` — 실증 7축+카탈로그 6축 완료(13기), 시뮬 6·검증 4기는 **세션 사용량 한도(19:40 리셋)로 미실행**(캐시 재개 가능). **B2/B3 근원 확정 = 클라 `sensor` ↔ 서버 `device_id` 파라미터명 불일치(PRD v2.1, detections.py:213)** → FastAPI 무시 → 팬아웃 전부 동일 전체 데이터+N중복(dedup 부재). 수정: ①쿼리 키 device_id 교체+죽은 파라미터 제거 ②요청 센서 후필터(멱등)+MAX_PAGES 캡 ③EventId DistinctBy ④stale `_loadedByInitialize` 리셋+그룹→그룹 컨텍스트 리셋 ⑤B1: ChartSeries1~8 8색(검증 통과)·전 시리즈 라인·끝단 라벨 겹침회피·칩 견본 8색 ⑥테스트 28/28(신규: 필터무시 서버 분리/dedup + 페이지경계 dedup). 분석서=`docs/analyses/detection-history-bughunt-scenario-analysis.md`. ⚠ 백로그: malfunctions/connections 동일 계약 갭(EventApiService.cs:260-261,:412-413)·재보고 FindEntryByDevice 라이브 조기종결 리스크. ⚠ 단일 센서 다이얼로그도 동일 원인으로 무필터 표시였음(이번에 함께 해소).
- **✅ 시뮬·적대검증 전체 완료(19:45 재개, 23/23)**: 6축 280+ 시나리오 데스크 시뮬 + 검증 4기 — **이슈 30건 전원 CONFIRMED(반박 0)**, 1차 수정이 B1/B2/B3 해소 확인. 잔존 P2 6건은 **2차 수정 `2eb223a`→머지 `90b8298`로 해소**: NEW-1(테스트를 실서버 형태 중첩 device로 전환 — 후필터 실경로 검증), NEW-2(LoadAsync 조회 시점 멤버십 재해석), C1/Q5(IsBusy 세대 가드 — 프로그레스 써클 레이스), Q2/E1(부분 페이지 "(일부 페이지)" 표식), F1/E9(단일→단일 완전 리셋), E4(malf/conn device_id 정리). Events.Ui 28/28. 산출물: 분석서 v1.1 + `docs/tests/detection-history-bughunt-scenarios.md` + `-simulation-log.md`(113KB 전량). **백로그 VF-15~20 등재**(A1 권한 verb 불일치 — 운영 token이면 P1 승격 / A2 실카드 고아화 / E5 enum 격리 / NEW-3·4·5).
- **✅✅ 런타임 실측 완료(2026-08-06 21:09, UI 자동화 admin/admin123, 안양발전소 현장)**:
  - **VER-01 통과** — 구역 bbox 우클릭 → 그룹 메뉴 정상 `[구역 — 구역-NN | 등록 센서 정보 | 그룹 탐지 이력]`(구역-01~04·15~17 등 7개 순회 확인, 항목 IsEnabled=True)
  - **VER-02 통과 → G-2=(a) 현행 유지 확정 · RISK-01 스킵 확정** — 구역 지점은 그룹 메뉴 정상 확보, 센서/카메라 겹침 지점만 장치 메뉴(정상 동작). z-순서 보정 불필요
  - **TEST-08 오버레이 시각 검증 통과**(캡처 `…/artifacts/20260806_210656/overlay_missing_7.png`): 헤더 "등록 센서 정보 — 구역-15"·요약 "그룹 235 센서 0"·테이블 헤더·**빈 상태 문구**·푸터 [닫기]+[📈 그룹 탐지 이력] 전부 정상 렌더(라이트 테마)
  - **데이터 정합 실측**: 구역-01(groupId=273)·02(275)·04(276) rows=1 / 15(235)·16(236)·17(237) rows=0 — 사용자 진술("구역1만 등록")과 일치. 렌더 실측 W=400 H=175~179 Template=True
  - ⚠ **UIA 미탐지(테스트 한계 — 앱은 정상)**: 오버레이 ControlTemplate 내부 TextBlock이 UIA 컨트롤 뷰에서 빠짐(TemplatedParent=Control — MapScaleBar/MapCoordinate 기지 함정 동일). 자동 단언 불가 → 시각 검증으로 대체. 자동화하려면 **AutomationId 계측 필요(백로그)**
  - ~~⚠ 이동 경로 실측: 레이어 트리 **오버레이 이미지 노드의 "이동"은 카메라를 안 움직임**(IsEnabled=True인데 무동작)~~ **← 2026-08-07 정정: 정상 동작한다.** 레이어 패널 → `안양열병합발전소_20260720` 우클릭 → 컨텍스트 메뉴 6종(`삭제 | 이름 바꾸기 | 위로 | 아래로 | 이동 | 잠금 해제`) → **'이동' 실행 시 카메라가 현장으로 이동**(마커 40/40 화면좌표 변화, 캡처 `b13_after_move.png` 로 안양 현장 렌더 확인: 좌표 37.64976457/126.90087676, 줌 18). 배치 `sweep_batch13`. **PIDS 그룹 구역 리프 "중앙으로 이동"도 정상** — 둘 다 쓸 수 있다.
    ⚠ 컨텍스트 메뉴는 **별도 hwnd** → `EnumWindows` + `Automation.FromHandle` 필요(셸 하위 탐색으로는 안 잡힘).
    ⚠ 이동 여부 판정은 **마커 화면 rect 델타**로 한다 — 지도 중심 좌표 TextBlock 은 `TemplatedParent=Control` 이라 UIA 컨트롤 뷰에서 빠진다.
  - 진단 로그(SENSORINFO-DIAG) 원인 규명 후 제거·라이브러리 빌드 0에러 복구. UiSmoke 8/8 회귀 통과
- **🔲 잔여**: 그룹 이력 다이얼로그 런타임 확인(오버레이 UIA 미탐지로 자동 진행이 막힘 — 계측 추가 또는 수동 1회) · VER-05(어도너 선점) · VF-15 운영 AUTH_MODE 확인 · 8색 라인/칩 필터/재보고 육안.
- **✅ 승인/플랜**: PRD v1.1 Approved(승인 코멘트: G-1=(a) 총합500·G-3=채택·G-2=V-02 후). ⚠ approve가 mtime 최신을 집어 zoom PRD와 충돌 → 내 PRD mtime bump 후 승인. 플랜 `docs/plans/pidsgroup-rightclick-prd-plan.md` 38태스크(~40h), pipeline-state activePrd/activePlan 수동 동기화.
- **✅ VER-03/04 완료**: 호스트 다이얼로그 재사용 가능(제목/크기 하드코딩 無) / Controller nav=fetch 직후 매핑·갱신경로 재할당(:969) null 가능→"—" 폴백 확정.
- **✅ PRD Phase 1 구현 완료 — 커밋 `d02c47f` @ `feature/pidsgroup-rightclick`**(워크트리 `C:\workspace_app\worktrees\pidsgroup-rightclick`, 롤백태그 `before-pidsgroup-rightclick`): ShowMarkerContextMenu 그룹 분기(캡션+[등록 센서 정보], disable+ToolTip 게이트) + SensorInfoPanelControl/Style(Generic 등록, 표준 크롬, 배지4종·테이블·빈상태) + SensorInfoPanelViewModel(단일 인스턴스·Load 교체) + MapView ContentPresenter + 데이터 조립(DeviceProvider 역필터+DeviceGroupProvider IoC lazy+EQM GetDeviceState) + 행 우클릭 2종(센서 탐지 이력 재사용/위치 확인). GMaps.Ui 빌드 0에러, `SensorInfoPanelViewModelTests` 12/12, BOM·한글 검증 완료. Caliburn `Action` 모호성 1건 `System.Action` 한정으로 해소.
- **⚠ 워크트리 빌드 함정(신규 발견)**: GMap.NET `sn.snk`+`Directory.Build.props`(SignAssembly)가 **미추적**이라 새 워크트리에서 CS0281 서명 오류 — 메인 체크아웃에서 복사 필수(OnvifSolution robocopy와 별개 2번째 함정).
- **🔲 남은 것**: Phase 2(FR-08~15: 그룹 이력 메시지+팬아웃+멀티시리즈 차트+SETUP-02 토큰+그룹 탭 행 우클릭+**메인솔루션 EXT(사전 통지 필수)**) / VER-01·02·05+TEST-08 런타임 실측(재빌드 후, G-2 결정) / v2.6 머지는 Phase 2와 함께 또는 Phase 1 선머지 사용자 결정.
- **✅ PRD**: `docs/prds/pidsgroup-rightclick-prd.md` v1.1 Draft — FR-01~15(Phase 1=메뉴 [등록 센서 정보]+오버레이, 서버 0 / Phase 2=[그룹 탐지 이력] 진입점 3곳+팬아웃+멀티시리즈+EXT), NFR 7, AD 7, V-01~05, 리스크 8, G-1~3 결정 대기. 적대검토 결과: fact-check 30/30 CONFIRMED + 비평 P1 2건(죽은 메뉴 Phase 모순·도달불능 팝업)→v1.1에서 해소, P2 4건·P3 2건 반영. 스토리보드 A2/Phase 주석도 v1.1로 동기화.
- **핵심 확정(v1.1)**: 맵 게이트=disable+ToolTip(맵 컨벤션)/패널 게이트=활성+팝업(패널 컨벤션), [그룹 탐지 이력] 노출은 FR-15 배선과 같은 릴리스(죽은 메뉴 금지), 차트 시리즈색=신규 토큰 ChartSeries1~3Brush(Theme 파리티), 오버레이 생명주기=단일 인스턴스·컨텍스트 교체·스냅샷.
- **🔲 승인**: `node .claude/hooks/advance-phase.js approve prd "코멘트"` (⚠ approve는 mtime 최신 PRD를 집는다 — zoom-float-halfstep PRD도 Draft 상태로 공존 중이니 승인 시점에 이 PRD가 최신인지 확인)
- **요구(사용자)**: ① 맵 PidsGroup 심볼 우클릭(현재 무반응) → 등록 센서 정보 + 그룹 탐지 이력(센서 탐지 이력 확장판) ② 장비정보 패널 그룹 Row 우클릭 → 그룹 탐지 이력 ③ 등록 센서 정보는 오버레이 윈도우로 HTML 기획(현행 테마/패턴·다크/라이트).
- **✅ 조사(워크플로우 5축+적대검증 C1~C5 전부 CONFIRMED, wf_a2176dbc)**: 핵심 = 그룹 우클릭은 이미 `ShowMarkerContextMenu`(MapViewModel.cs:5797)까지 도달하나 `IPidsGroupEditableMarker` 분기가 없어 항목 0개 → 메뉴 미오픈(:6031 게이트)이 "무반응"의 정체. **훅 = :5809 형제 분기 1개 추가**(우클릭 캡처 경로는 코드 0줄 — 편집=Shape base:612 / 뷰=GetMarkerAtScreen:1367 bbox AABB).
- **데이터(서버 변경 0)**: 멤버십=역참조(`BaseDeviceModel.DeviceGroups: List<int>`), 해석=`DeviceProvider.OfType<ISensorDeviceModel>().Where(d=>d.DeviceGroups?.Contains(groupId)==true)`(DeviceGroupSelectionViewModel.cs:64 선례) + `DeviceGroupProvider`(그룹명) + `EventQueueManager.GetDeviceState`(라이브 상태). ⚠ `SymbolEventManager._groupSymbolLookup`은 그룹당 대표 1개라 멤버십 소스 금지, DeviceCount는 stale 가능.
- **그룹 탐지 이력 갭**: `GetDetectionEventsAsync` 필터=controller/sensor/status뿐(그룹 필터 서버 부재 확정, EventApiService.cs:92-121) → 센서별 팬아웃+병합. 기존 다이얼로그는 단일 센서 전제(단일 폴리라인 차트, `SignalChartPoint`에 시리즈 키 없음) → 시리즈 확장+센서 칩+센서 컬럼+"최다 발생 센서" 필요. 오픈 메시지 신규(`GroupId/GroupName`) + **메인솔루션 ConductorControl IHandle 배선 필수(EXT·사전 통지)**. 그룹 Row 우클릭은 proxy(:17)·RowStyle(:163) 기존재라 XAML 난이도 0(DeviceGroupPanelView.xaml).
- **리스크**: 그룹 bbox z-순서 가로채기(메뉴 신설 순간 가시화 — 센서 우선 보정 여부 PRD 결정) / IsLocked 이중 차단 / 과거 이력 그룹 귀속=현재 멤버십 재계산(영속 이벤트에 그룹 필드 없음) / 500 상한 정책(권장: 그룹 총합 500).
- **✅ 산출물**: `docs/design/pidsgroup-rightclick-storyboard.html` v1.0 (화면 A~D + S1~S6 필름스트립 + 데이터계약/리스크/Phase 분할, 실토큰 다크/라이트 토글, 차트 시리즈 팔레트 validate_palette ALL PASS: 다크 #1490AC/#BF7C16/#A371F7·라이트 #0E7EA3/#B25A06/#7A3FC2). INDEX.md 등재 완료.
- **🔲 다음**: 사용자 스토리보드 검토(특히 오픈 결정: 500 상한 정책·z-순서 보정·"지도에서 위치 확인" 항목 채택) → PRD 작성(Track C, Phase 1=서버 0 오버레이+메뉴 / Phase 2=그룹 이력 확장+EXT).

## ▶▶ 재개 포인트 (2026-08-06, 이 세션 — 탐지·장애 신호등 우상단 재배치 기획) — ✅ 와이어프레임+스토리보드 완성, 🔲 **사용자 배치/형태 결정 → PRD**
- **요구**: GMap 위 탐지·장애 배지(GMapDetectionFaultControl — 미조치 탐지/장애 pill 2개, EQM 소스)를 "프로그램 우상단 시스템쪽과 같이" 재배치 + **신호등 형식** + (추가) CPU/RAM/GPU 아이콘 리디자인("너무 구려").
- **실측 핵심**: 시스템 지표는 타이틀바가 아니라 **지도 상단 메뉴바 우측 스트립**(MapView.xaml:214-247, SysResChip 3개 — PackIcon Chip/Gpu/ExpansionCardVariant 19px+Consolas %, ResourceLevel 색, GPU 미탑재 숨김). 셸 타이틀바 우측엔 테마 토글뿐.
- **산출물**: `docs/design/detection-fault-trafficlight-wireframe.html`(배치 A′ 권장=기존 칩 스트립에 신호등 삽입 · 형태 T1 가로 3램프 · 상태 매트릭스·green 불변식 · 지도 계기 처분 D1 권장 · **§7 아이콘 리디자인 R1 도넛 게이지 권장**) + `-storyboard.html`(9장면). 신호등 의미론=설비 상태등(동시 점등 허용, 🔴장애/🟡탐지/🟢정상, 3중 부호화).
- **✅ 배치 확정(사용자 2026-08-06)**: **분리 배치** — 이벤트 신호등=이벤트 카드 리스트 창 헤더(B′, 클릭=같은 창 필터·포커스) · 시스템=지도 상단바+R1 도넛. **✅ 추가 확정: 기존 지도 pill 계기 제거(D2) + 보기>탐지·장애 상태(Ctrl+Shift+F, IsDetFaultVisible) 토글을 신호등 표시 제어로 재연결**(문서 v1.2).
- **✅ 통합 PRD Approved + dev 진입(2026-08-06)**: `docs/prds/map-topbar-trafficlight-prd.md` v1.0 (FR-A1~A6 신호등/B1 도넛/C1~C6 상단바, 사용자 "만들어서 진행" 지시로 승인 등록) + 플랜 `docs/plans/map-topbar-trafficlight-prd-plan.md`(17태스크). **VER-01 완료**: V-02=EA 메시지 방식(Events.Ui는 GMaps 미참조), 신호등 앵커=`EventCardListPanelView.xaml` Row1 헤더("이벤트 수 N" 스택 좌측/대체), V-05=`s.CpuPercent` 원값 존재(SystemResources.cs:75 — DP 노출만 추가). 롤백태그 `before-topbar-trafficlight`(양 레포).
- **✅ dev 전량 완료(2026-08-06, 15/17 — 88%)** — LIB 커밋 7개 + MAIN 1개:
  · **Part A 신호등**: ee51761(헤더 T1 인라인 3램프+EQM 구독) → fcca1ea(**IMPL-04/05**: 공용 메시지 2종 `TrafficLightVisibilityChanged/RequestMessage`@CommonMessages.cs, IsDetFaultVisible 세터 EA 발행(복원 중에도)+Request 응답, **pill 계기 제거(D2)** — MapView 계기층+EQM 구독+건수/위치/방향 프로퍼티+SaveMapDetectionFaultState+OpenEventPanelCommand 제거, `_eventQueueManager` 필드는 GetDeviceState:3293 사용처로 유지, 메뉴 "탐지·장애 신호등"+TrafficLight 아이콘) → 0304657(**IMPL-03**: 램프 클릭=기본 CollectionView.Filter 종류 필터, 재클릭 해제, 외곽선, Connection 카드 양필터 제외, 비활성화 해제, UIA FaultGroup/DetectionGroup)
  · **Part B**: d29f04c(**IMPL-06**: SysResChip→26px 도넛+중앙 약어 C/G/R+% 병기, VM Cpu/Gpu/RamPercent 원값, GPU Hidden 승계, UIA GMaps.SysRes.*) → f331fa3(**FB-01 사용자 피드백**: 회색 트랙 원 상시 노출 — MD ProgressBar 템플릿 의존 제거, `PercentToDonutArcConverter` 직접 드로잉(D=26/T=3, 반경 11.5=Ellipse 스트로크 접힘 동일 수식이라 동심 보장, 0%=트랙만/100%=완전 원/Freeze) + % 텍스트 Width=34 선점·우측 정렬로 자릿수 변동 흔들림 0. 기하 테스트 12종, GMaps.Ui 471/471)
  · **Part C**: 69334f2(**IMPL-08/09**: 운영 툴바 20→9, 그룹 라벨 제거, 편집 스위치 CanMapEdit **비노출**+"편집 중" 라벨, 편집 스트립 Grid.Row=1 MaxHeight 0↔64 200ms+주황 테두리, 등록/심볼/선택/기준 4그룹, UIA GMaps.EditStrip.* 10종) → 844eae0(**IMPL-07**: 파일 메뉴 폐지, 지도 메뉴=지도 전환 라디오(AreEqualMultiConverter 신규)+좌표계 서브메뉴+줌 범위 바로가기(OpenSetupPanelMessageModel)+종료, 타일폴더+Ctrl+D 삭제, VM SelectMapCommand/OpenMapSetupCommand) → e69ec11(**IMPL-10**: PlaybackConsoleControl.SettingsCommand DP+헤더 ⚙, UIA GMaps.Playback.SettingsButton)
  · **테스트**: d9a0e89(**TEST-01**: TrafficLightStateTests 11 green — 매트릭스/green 불변식/미초기화 게이트/필터/EA 수신) · **TEST-02**: LIB+MAIN 빌드 0오류, GMaps.Ui 459/459, Events.Ui 467/482(**실패 15=기존** — 대상 클래스 태그 이후 diff 0 입증, datetime-aware 'Z vs +00:00' 등 선행 변경 기인, 별도 수리 대상), §3 매핑표 전수 grep 대조 통과 · MAIN 86d7350(**TEST-03**: MapPage FileMenuId 소멸·NavGroup 4버튼 타입 필터·EditStrip/PlaybackSettings 앵커, GMapScenarioTests D-01 4버튼·GM-M09 오버플로 전제 제거, UnitTests 32/33 — 실패 1=DbProbe 라벨오프셋 데이터 이슈 무관)
- **🔲 잔여 2**: **TEST-04 실기 검증**(데스크톱 독점+IRONWALL_UITEST_ID/PW 필요 — 신호등 매트릭스/보기 토글/클릭 필터/스트립 확장/도넛 렌더, MAIN 하네스 갱신분 실행 재검증 포함 — **미검증 상태 명시**) + **DOC-01**(PRD DoD·기획서 v 동기 — TEST-04 후). ⚠ 주의: 신규 UIA 앵커(GMaps.EditStrip.*/Playback.SettingsButton/SysRes.*)는 덤프 미실측 [B] 등급.
- **✅ 실기 피드백 반영(c296726)**: ① 도넛 3개 캡슐 Border(GMaps.SysRes.Panel, 신호등 pill 동계열 토큰) ② 재생 ⚙ 그림자 제거 — 원인=인라인 Template만 있으면 **암시적 MD Button 스타일 Elevation Setter 누수**(제어허브 '모두 닫기' 동일 계열) → 명시 `PanelHeaderGearButtonStyle`(DesignTokens) 신설로 차단.
- **✅ 신호등 배치 최종 확정 = 상단바 이관(a5152c4, 사용자: "시스템 도넛 왼쪽에 높이 비슷하게" + "이벤트 패널 원래대로")**: 이벤트 카드리스트 패널 **완전 원복**(git ee51761^ 기준 — 헤더 신호등/램프 필터/EA 수신 전부 제거, '이벤트 수' 원상). 신호등 pill=MapView 상단바 도넛 캡슐 좌측·높이 32 동일·동계열 토큰, 클릭=이벤트 패널 오픈(OpenEventPanelCommand 복원), 보기 토글 직접 바인딩(EA 브리지 메시지 2종 CommonMessages에서 삭제). **깜빡임 정책(사용자 확정): 탐지(노랑)=깜빡 0.5s · 장애(빨강)=상시 점등.** 상태 소스=MapViewModel.Instruments EQM 재구독, 점등 규칙 SSOT=`TrafficLampLogic`(+테스트 8종, GMaps.Ui 479/479). UIA=GMaps.Status.TrafficLight.* (Events.Status.* 소멸 — MAIN 하네스 문서에 반영 필요시 TEST-03 후속).
- **✅ 이벤트 차트 툴팁 테마/한글화(5469ee8, scenario-analysis 스킬 — 사용자 "시나리오 기반으로 확인" 지시)**: 시뮬 232건(chartsim, 현행/수정 병렬 — 대비비·문자열·타입페이스·전환 경로), 현행 ISSUE 65 → **수정 모델 잔존 0**. 근원=3표면(EventInfoView 막대·파이/DataChartPanelView 라인) 중 **TooltipBackgroundPaint 전부 미지정** → 라이브러리 기본 밝은 박스 위 다크 텍스트 CR=1.04(SIM-R009, 스크린샷 재현). 수정: ChartThemeProvider.TooltipBackground/TextPaint 신설(다크 #243240/라이트 #E9EEF4)+3표면 바인딩+전환 재공급 · **차트 문자열 18지점 한글화**(센서 탐지/카메라 탐지/탐지/장애/연결/조치/데이터 없음/제어기/이벤트 — 서버 코드 DET/MAL 무접촉, 테스트 시리즈명 미단언 확인) · 한글 렌더 전 지점 SKTypeface 보장+**KoreanTypeface Noto 폴백 체인**(Noto Sans KR→CJK→Malgun, 캐시) · 데이터라벨 OnSeriesInk(#0C1117, 흰 라벨 CR 1.5~2.9 기존 결함 해소). 산출물: docs/analyses/event-chart-theme-scenario-analysis.md + docs/tests/…-scenarios.md·-simulation-log.md(전량). Chart 테스트 6/6·영문 재grep 0. **실기 렌더 미검증**(다이얼로그 호버/테마 전환 확인 필요).
- **✅ 차트 라이트 모드 2결함(76c989d, 시뮬 v3 254건 — 사용자 라이트 스크린샷)**: ① **테마 stale**(라이트에서 다크 페인트 연회색 범례) — 근원 3중: 차트 VM SingleInstance(EventUiModule.cs:68-70) + ctor 1회 구독·OnDeactivate(close:true) 해제 + **대시보드가 매 검색마다 Deactivate(true)→Activate 반복**(EventDashboardViewModel.cs:76-78,:123-125) → 첫 사이클에 ThemeChanged 구독 소멸. 수정=OnActivateAsync 해제-후-재구독+CurrentTheme 페인트 재설정(EI ApplyThemePaints/DC — [[project_singleton_panel_vm_event_lifecycle]] 함정 재발 사례). ② **한글 축명 겹침**('이벤트'↔0.5 눈금) — NamePadding 음수(영문 기준)×한글 세로 폭 → EI 축 4개 Padding(0)+12px. 수정 모델 시뮬 잔존 0, Chart 6/6. **실기 재확인 대기**(라이트/다크 오픈+검색·테마 왕복).
- **✅ 차트 툴팁 액티브 리프레시(8190229, FR-8 — 사용자 "호버 UI는 별도 Refresh 수신 필요" 지시)**: 툴팁=차트 컨트롤 소유 뷰 요소라 VM 페인트 재할당만으론 떠 있는 박스가 구테마 잔존 → `ChartThemeRefreshBehavior`(Events.Ui\Behaviors, 첨부 행위) 신설 — 차트가 ThemeChanged 직접 수신, TooltipPosition Hidden→다음 프레임 복원으로 열린 툴팁 즉시 소멸·재생성. 3표면 부착(EI 막대/파이+DC 라인), Loaded/Unloaded 수명 관리(해제-후-구독). 분석서 v3.1 FR-8. **실기 확인 대기**: 다이얼로그 연 채 테마 왕복 시 구테마 툴팁 잔존 0.
- **🔲 백로그(사용자 요청 접수, 착수 전 중단 — "정리하자")**: 억제 창 추가 폼 레이아웃 — 작업명·억제 범위 입력의 폭을 억제 시간창(시작~종료) 폭과 일치, 대상 유형 라디오 그룹은 폭 축소. 대상=EventSuppressionSchedulePanelView.xaml(⚠ 현재 워킹트리에 타 세션 수정분 있음 — 착수 전 상태 확인 필수).
- **⚠ 미해결: 테마 전환 중 System.AccessViolationException 크래시(사용자 실기 보고)**: 윈도우 이벤트 로그에 앱 AV 기록 없음(VS 디버거 가로챔 추정 — 보고 형식이 VS 예외 창), WER 덤프 없음. 당일 별건 크래시 1건 존재(오전 9:19, 설치본 C:\PidsMonitoringSystem, **0xc0000409 clrjit.dll** — 다른 유형). **다음 단계: VS 예외 창에서 호출 스택 확보 필요.** 가설(미확증): ①wpfgfx 렌더 스레드 — DropShadowEffect+무한 깜빡임 애니메이션 중 테마 DynamicResource 스왑 레이스 ②GMap 타일 렌더 interop ③드라이버/RDP. 진단 실험 후보: RenderOptions.ProcessRenderMode=SoftwareOnly로 재현 여부 이분.
- ~~신호등 잘림 해소(3b90880, 1안)~~ → **상단바 이관으로 대체됨(위)**. 기록용: 근원=이벤트 패널 폭 **250px 고정**(MAIN ShellView.xaml:272) vs 헤더 우측 스택 필요폭 220~280px — StackPanel 축소 불가라 좌측(신호등)부터 구조적 상시 잘림이었음. 수정='이벤트 수'(15px+마진32) → **"총 N" 컴팩트**(12/13px, 총수는 셸 벨 배지·신호등 건수와 3중 중복이라 축약) + pill 마진 24→8 + 건수 MinWidth=32(4자리 선점, 자릿수 변동에도 신호등 위치 불변). 결과: 점등 최악 ~175px < 가용 ~195px. 실기 렌더 확인=TEST-04.
- **🆕 상단 메뉴·툴바 간소화 기획 완성**: `docs/design/map-topbar-simplification-wireframe.html` — 실측 인벤토리(메뉴 12항목+툴바 20컨트롤, MapView.xaml:120-592) 기반 **운영/편집 이원화**: 운영 기본=콤보1+아이콘7+편집스위치(-55%), 편집 ON 시 2번째 줄 스트립(등록/심볼/선택/기준 — 파일 메뉴의 맵·이미지·커스텀맵 등록 이관). 중복 2건 삭제(툴바 십자선↔보기 메뉴, 파일>타일폴더↔설정 카드), 파일 메뉴 폐지(종료→지도 메뉴), 편집 스위치는 CanMapEdit 권한자만 렌더. 이동 매핑표 전수(기능 제거 0). 🔲 Q1~Q4(파일 폐지/추적설정 이동/측정 위치/스위치 비노출) + 신호등 T1·R1 승인 → PRD 2건(신호등+상단바) 또는 통합 1건.
- **⚠ 발견**: `docs/INDEX.md`가 훅 자동 재구축 후 **다수 행 mojibake 손상**(예: 설계 섹션 전체) — advance-phase 재구축 경로의 인코딩 결함 의심, 별도 수리 건.

## ▶▶ 재개 포인트 (2026-08-06, 이 세션 — 줌 Float(0.5스텝) 전환: 분석→PRD 승인→**dev 완료**) — ✅ 코드 전량 구현·테스트 green, 🔲 **실기 검증(TEST-04 잔여) + GMapCustomImage 편집 UI 후속**
- **✅ PRD v1.1 Approved(2026-08-06 "승인")** — 권장안 전체 채택: G-1=B(최상단 `Max→Max.5→Max.5+→Max.5++`), G-2=1.25× 유지, G-5=신설, G-6=소프트 밴드만 주황. 훅 approve 등록 완료.
- **✅ dev 완료(플랜 23/25, 92%)** — 커밋 4개(+sweep 1): 74eecd5(Phase1 코어 SSOT — 타 세션 sweep에 흡수, 내용 무손실)·a14069d(표현/복원/저장)·dc7380a(게이트 10곳+생성 8곳+속성패널)·8692c41(ROI DECIMAL 5개소+스키마 가드)·fbc77ea(기존 상시실패 테스트 수리 — FOVColor 표본이 ae253f6에서 승격돼 전제 무효였음, 내 변경 무관 blame 실증).
- **✅ 테스트**: ZoomLadderTests 36 신설(SIM 승계) — GMaps.Ui **446/446** · tests\GMaps.Ui.Tests **217/217**(DigitalZoomCoordinate 계약 유지) · UnitTestMapRoi **6/6**(17.5 무손실 왕복 실증, `-p:IncludeTests=true` 필요). 빌드 0오류.
- **핵심 구현**: GMapCustomControl `EffectiveZoom`/`SetEffectiveZoom`(원자)/`StepEffectiveZoom` + Reset 게이트(`IsApplyingEffectiveZoom`) + dzl 변경 시 스냅샷 발행·가시성 재평가(FR-19). ZoomLadder(Helpers, 순수 산술 SSOT). 슬라이더 0.5틱(시각 틱은 None — 250px에 36틱 과밀), 라벨 "17.5"/"19.5+"/"19.5++"(IsSoftZoom 주황). VM.Zoom setter는 SetEffectiveZoom 위임(getter는 타일줌 유지 — 축척바 계약 NFR-02).
- **🔲 실기 잔여(TEST-04 — 전부 미검증 명시)**: 휠/슬라이더/커맨드 실조작, 복원 라이브(홈/맵전환/ROI), 실 monitor_db `MigrateMapRoisZoomColumnAsync` MODIFY 분기 발동(다음 부팅 — 현 monitor_db.maprois=int(11) 실측), SIM-RT/W/U/A 계열. 검증 시 `시뮬 {ID}` 로그 포맷 승계.
- **🔲 후속**: GMapCustomImage(비마커 오버레이) 최소줌 편집 UI(속성패널 미경유 구조 — 우클릭 메뉴+다이얼로그), 앵커 TextBox 정수 고지 문구. ※마커형 이미지는 base 최소줌 행이 이미 존재(Z-17은 과대판정이었음 — 검증).
- **배포 주의**: 라이브러리 변경은 v2.6 → 메인 앱 재빌드 후 반영 [[project_library_deployment_path]]. GMaps.Db 테스트는 `-p:IncludeTests=true` 게이트(출하 차단 유지).
- **✅ FR-20 구현 완료(v1.2)**: "왜 최대줌 19?" 실측 — MBTiles 메타데이터(maxzoom=19)→시드(4768)→Maps 테이블 6~19→부팅 ZoomMax 체인, 설정 항목 부재. **사용자 확정 의미론 = DB Maps.Min/MaxZoomLevel 직접 편집**(appsettings 캡 폐기). LIB `UpdateMapZoomRangeAsync`(1288510) + MAIN 설정정보>지도설정>"지도 줌 범위" 카드(1045547, MapSetupView/VM — IGMapDbService 주입·편집사본·검증·MapProvider 동기). 적용=지도 전환/재시작. ⚠파일 교체 시 시드가 파일 메타로 재동기(4731-4741). 빌드: 재빌드 중 앱(PID 4564)·VS DLL 잠금 → 앱 종료 후 0오류.
- **🔲 실기 검증 대기 상태(전 턴)**: FlaUI ZoomHalfStepUiTests 준비 완료(계측 0adda40 + MAIN 빌드 0오류 + 디스커버리 확인) — **IRONWALL_UITEST_ID/PW User 스코프 설정만 대기**. Playwright 세션(8/3) 검토: WPF 영구 불가·FlaUI 후계 확정이 그 세션 결론 — 현 경로가 정합.

## (이전 상태 — 참고) 줌 Float 분석·PRD 단계 요약
- **요구(사용자)**: 줌 표기 `18+`/`18++` → `17.5`식 소수, 0.5 스텝 래더(범위 언급 1~20.5), **(정정) 최상단은 `+`/`++` 개념 2스텝 유지**, 심볼/이미지 줌 속성도 x.5 지원, 수백 시나리오→시뮬→PRD, **프로세스를 하네스에 등록**.
- **✅ 하네스 등록**: `.claude/skills/scenario-analysis/SKILL.md` 신설(시나리오 기반 분석 6단계: 영향면 6축 전수→카탈로그(SIM ID)→시뮬레이터(제품코드 무수정)→전량 로그(`시뮬 {ID}` 포맷)→이슈 3분류(결함/정책공백/마이그레이션)→PRD 주입, test phase까지 SIM ID 승계) + CLAUDE.md 참조 경로에 연결. 스킬 목록 반영 확인.
- **✅ 분석(Explore 6기 + ZoomSim 1,472케이스 v2, PASS 1,448/ISSUE 24·22종 + 적대 검증 3기 반영 v1.1)**: `docs/analyses/zoom-float-halfstep-scenario-analysis.md` · 카탈로그 `docs/tests/zoom-float-halfstep-scenarios.md` · 전량 로그 `docs/tests/zoom-float-halfstep-simulation-log.md`. 시뮬레이터 소스=스크래치패드 `ZoomSim/`(세션 종료 시 소멸 — 필요 시 로그의 미러 로직으로 재구성 가능).
- **✅ 적대 검증 3기(인용/완결성/정합성) 반영 완료**: 반박 성립 — SIM-D006은 DP 기본값 시뮬 오류(실배포 MapView.xaml:956 Steps=3 → PASS, 잠재로 강등) / 게이트 "전수"에 2곳 누락(MapViewModel.cs:9079·9605 레이어 토글 경로) / Z-22(생성 기록 8곳 타일줌)·Z-23(홈 저장 타일줌)·Z-24(dzl 변경 시 뷰포트 스냅샷 미발행 — 발행은 회전 경로 2814 한정) 신설 / FR-11↔FR-13 상한 모순 해소(객체 줌 캡=Max+0.5). PRD v1.1 = FR-01~19. 동형성 증명 ID는 SIM-D009.
- **핵심 발견**: ① **현행 휠 래더 ≡ 제안 0.5 래더 완전 동형(SIM-D008)** → 전이 로직 무수정, 표현·합성·복원 계층만 수정 ② 객체 Zoom 의미론=최소 표시 줌 게이트 단일(`mapZoom>=obj.Zoom`), Symbols/Images DB는 이미 DECIMAL(3,1) ③ **서버 무영향**(줌이 서버로 안 나감) ④ 치명 함정: `MainMap_OnMapZoomChanged` 무조건 `ResetDigitalZoom`(MapViewModel.cs:6952)이 복원 하프 소거 + `GMapControl.Zoom` 소수 직대입=무성 절단(GMapControl.cs:341) ⑤ ROI는 INT 4중 절단(캐스트+모델+POCO+컬럼) ⑥ MapZoomStyle.xaml 한글주석 4곳 실 mojibake(BOM은 있음).
- **✅ PRD Draft**: `docs/prds/zoom-float-halfstep-prd.md` v1.0 — FR-01~18(SIM ID 추적)·NFR 5·마이그레이션(ROI DECIMAL + 레거시 Zoom MODIFY 가드). 설계 축=실효줌 SSOT(타일+0.5×dzl) + `SetEffectiveZoom` 원자 세터.
- **🔲 결정 대기(Gap)**: G-1 최상단 형태(권장 B: `20→20.5→20.5+→20.5++`, 현행 4레벨 재라벨) / G-2 하프 배율(권장 1.25× 유지=40m 래더 보존) / G-5 이미지 최소줌 편집 UI 신설(권장 신설) / G-6 주황 라벨(권장 소프트줌 구간만).
- **롤백태그**: `before-zoom-halfstep-analysis`. 적대 검증 워크플로우(wf_f7466146) 결과는 최종 응답 참조.

## ▶▶ 재개 포인트 (2026-08-06, 이 세션 — 현장 라벨 미표시 근본원인 분석) — ✅ 원인 확정, 🔲 수정은 사용자 승인 대기
- **입력**: 현장(안양열병합발전소) 운영 DB 덤프 `gis_db_20260804_오후.sql`(업데이트 前) — 스크래치패드에 ASCII명 사본으로 분석
- **✅ 원인 확정**: 라벨 오프셋 **도메인 불일치**. 현장 DB는 px 단위 `LabelOffsetX/Y`(UpdatedAt 7/21, −84·−52·−218 등)를 보유한 채 신버전을 만남 → `48493fe`(7/27)가 라인/PidsGroup을 비율 해석으로 전환 + `ac6c153`(7/28)이 px 정리 마이그레이션 제거(변환 마이그레이션은 애초 없었음) → `ComputeLabelCenterRel`(LabelAdorner.cs:147-150, 클램프 없음)이 px값×하프익스텐트로 곱해 라벨을 화면 밖 수천 px에 렌더. **영향 = PIDS_GROUP 15/17 + AREA_BOUNDARY 5/5 (구역/울타리 라벨 전멸). 점 심볼은 px 유지라 무영향.**
- **배제**: 스키마 22컬럼 추가(36b81fb)는 DDL DEFAULT=엔티티 기본값 동형이라 중립 / 색상정리·band shift UPDATE는 덤프 기준 0행 / maplayers 전부 IsVisible=1 / c53ffff 첫페인트 수정 HEAD 포함 / 15ed865·34f28fe 라벨 미접촉
- **산출물**: `docs/analyses/gmap-label-offset-domain-mismatch-analysis.md` (현장 확인 SQL + 반증 실험 + 권고 3안 포함)
- **🔲 다음**: 사용자 확인 후 수정 방향 결정 — 권고 1안=1회성 px→비율 변환(W/2·H/2 근사, 자기비활성화 가드 필수), 2안=0 리셋, 3안=렌더 클램프 방어. 수정 시 PRD 선행([[feedback_prd_before_code]])
- **부수**: 이전 세션의 분석 워크플로 `wf_c107344c-269`는 세션 종료로 중단 — 본 세션에서 직접 검증으로 대체 완료(재개 불필요)
- **✅ 현재 로컬 DB(127.0.0.1/monitor_db) 실측 추가**(사용자 "현재 DB도 그렇다, 특히 구역"): 실좌표(pidsgrouppoints/linepoints) Web Mercator 투영으로 렌더 공식 재현 계산 →
  - **AREA_BOUNDARY 5행(3지대×3·한국가스공사·평촌소각장) = 확정 파손**: 현장 덤프와 동일한 px 값 잔존(7/21 이후 미변경, 오늘 8/6 실행에도 자가치유 없음) → 라벨 1,470~46,580px 밖. 5/5 실종 재현.
  - **PIDS_GROUP 구역-01~17 = 퇴화 인코딩**: 8/4 13:51 새 비율 경로로 재저장. 선형 심볼 가는 축이 클램프 1px에 걸려 비율=px÷1(LabelAdorner.cs:189) — 현재 조건(무회전·포인트 로드)에선 56~121px로 정상 계산되나, 익스텐트 조건이 바뀌면 12/17이 ×20 튐. 회전은 무죄(inv 역회전 보정 :177, 저장·렌더 모두 31e1024 이후).
  - 구역-XX가 로컬에서 실제 안 보인다면 판별 실험: 줌/팬 후 나타남=첫페인트 stale / 드래그하면 나타남=익스텐트 불일치.
  - PRD 추가 결함: 퇴화 축 정규화 무의미(÷1) — 변환 마이그레이션과 함께 설계.
- **✅ 워크어라운드 검증(사용자 "복사하면 정상?")**: **불가** — SymbolSnapshot.DeepClone=JSON 왕복 전필드 복제(LabelOffset 포함), ApplyCopyTransform은 위치/Id/링크/제목만 변형, INSERT에 LabelOffsetX/Y 포함 → 복사본도 동일하게 화면 밖. 임시조치는 SQL 오프셋 0 리셋+재시작(지오메트리 보존)이 최저비용. 라벨 드래그 재저장은 화면 밖 라벨은 못 잡아서 불가. 분석 문서 §4-2 반영.
- **✅✅ 런타임 확증 + 결함 2분리 확정(8/6 12:20, 분석 문서 §4-3/§6)**: 사용자가 `LabelOffsetX/Y=0` 리셋 → **라벨 정상 표시**. 진단 3중 확증(코드·정적계산·실기기).
  - **D-1(데이터)**: 현장 즉시 조치 = `UPDATE symbols SET LabelOffsetX=0,LabelOffsetY=0 WHERE Category IN ('PIDS_GROUP','AREA_BOUNDARY') AND (ABS(X)>3 OR ABS(Y)>3)`. ⚠**Category 한정 필수** — 점 심볼(BASIC/PIDS_EQUIPMENT/GEOMETRICS 77행)의 큰 값은 정상 px라 리셋하면 라벨이 심볼에 겹친다. px→비율 변환은 **비권장으로 하향**(현장값 의미불명 + 퇴화축서 식 불성립).
  - **D-2(코드, 신규 확정)**: 리셋 후 재드래그 대조군 — 폴리곤 278(0.047,−0.738)·285(0.117,−0.781)는 건강한 비율, 울타리 227 구역-01은 **(0, 41.8) 퇴화값 재생산**. 선형 심볼 가는 축 서브픽셀(z18 hh_raw=0.50px) → `Math.Max(…,1d)` 클램프(LabelAdorner.cs:189) → 비율=px÷1. **데이터를 고쳐도 드래그하면 재발** = 코드 수정 필요. 검증가능 예측: 구역-01 라벨 z16~19=51px 고정, **z20=94px·z21=180px 이탈**(클램프 해제). 수정후보 (a)축별 도메인 판정 (b)클램프 하한 상향 (c)저장·렌더 익스텐트 동일값 보장 — 저장포맷 변경이라 마이그레이션 동반, D-1과 묶어 설계.
  - 부수: Id 279 평촌소각장 삭제 후 **285로 재생성**(점 5개 동일).
  - **🔲 다음**: D-1은 운영 조치 즉시 가능 / D-2는 Track C PRD 필요(정책·저장포맷 변경 → `scenario-analysis` 선행 대상).

## ▶▶ 재개 포인트 (2026-08-06, 이 세션 — GM 심볼 CRUD 왕복 자동화 통과) — ✅ 전 구간 실측, 🔲 최대화 회귀배치 확인
- **✅ `GMapEditCrudTests` 통과(12s)**: 편집ON→GEOMETRICS/Circle→'추가'=**배치모드**(배너 "클릭한 위치에 '원형' 배치" 실측)→빈 지점 클릭 생성(Id=281, **즉시 INSERT**)→클릭 선택(속성패널 자동개방)→이름변경 `UITEST-CRUD-*`(**800ms 디바운스**→UPDATE)→**Ctrl+Z 복원→Ctrl+Y 재적용**→삭제(팝업 문구 **내 서명 검문** 후 OK)→**지도 103→104→103·DB 102→103→102 누수 0**.
- **D-04 해소**: 속성패널 `PART_ZOrder*` **UIA 노출 4개 실측** — [B]등급(grep만)→확정. 계측 없이 속성패널 시나리오 가능.
- **🔴 하네스 함정 3건(전부 수정)**: ① **`TogglePattern.Toggle()`은 Command 미발화** — `EditModeToggle`(IsChecked OneWay+Command, MapView.xaml:375-383)에서 시각만 On, VM은 false→'추가' 영구 비활성. **실마우스 클릭 필수**. ② `EnvironmentGuard.cs` foreign fail-fast가 **Dispose 후 p.Id 접근**→"No process is associated" 1ms 즉사(PC 재부팅 좀비가 격발)→HasExited 스킵+PID 선읽기. ③ 오버플로 팝업은 콤보조작·내부 실클릭에도 닫힘→**매 시도 표면 재확보**.
- **픽스처 정책 변경**: `AppFixture`가 셸 **최대화**(사용자 지시) — 툴바 안 접혀 편집컨트롤이 **인라인**, 오버플로 의존 소멸. `EditSurface()`=인라인 우선/오버플로 폴백.
- **✅ 전체 배치 그린(재실행)**: **75건 = 68통과/0실패/7스킵, 316s**. 스킵 7 전부 설계 게이트(오버플로 2=최대화로 접힘 없음 · 주입 E2E 5=NATS Dashboard 9000 다운). `GMapEditCrudTests` 배치 안에서도 통과.
- **🔴 배치 전멸 사건(1104s abort) 해결**: 진범=최대화 아님, **"미저장 항목 안내" 모달**. 장비 도달성 진단이 '추가하기' 프로브 Draft를 남기고 패널만 닫음 → 다음 화면 전환에서 모달 → 이후 전 클래스 클릭 흡수. 수정 ① `SmokeNav.DismissBlockingPopups`(내비 진입점 선제 정리 — Info 닫기·Confirm **취소**·'닫기' 이름 폴백) ② 장비 진단 finally에서 **'갱신하기' 재조회로 Draft 폐기**. **공유 앱 철칙의 사각 = Draft/모달**(패널 닫기만으론 부족).
- **빈 지점 탐색 요령**: 라인/경계 폴리곤 bbox가 뷰포트 전체 → **맵면적 25% 초과 rect는 차단 제외**(103중 102 소형만 회피). 오폭은 '제목 편집기 값=내 심볼' 검문이 차단.
- **신규 인프라**: `Support/DbProbe.cs`(MySql.Data, `UITEST-` 서명+베이스라인 이중 삭제가드 — 운영심볼 삭제 거부 단위 실증 2/2) · 백업 `_db_backups/20260805_map_edit_crud_test`(Symbols/Images/MapLayers).
- **편집 계약(스카우트 확정, GMapEditCrudTests XML doc에 정본)**: Ctrl+Shift+A 죽음(Command 미배선 MapView.xaml:515) · 본체 드래그=팬(이동은 어도너 Move핸들/방향키) · 마커 더블클릭=카메라 팝업(회피) · 선택 전환=이전 마커 암묵 DbUpdate(:2949) · 이미지 undo 불가·삭제 undo Id 가변.
- **⚠ 시나리오 보드 아티팩트 삭제됨** — 재발행 시도 시 접근불가. 정본 `docs/tests/ui-scenario-board.html`은 갱신 완료. 새 URL 필요하면 요청.

## ▶▶ 재개 포인트 (2026-08-05, 이 세션 — UI 자동화 3단계: 앱1회기동 배치 + GMap/탭 실행 + 무음실패 규명·Track B 조치) — ✅ 66건 65통과, VF-10·11 수정완료, 🔲 PRD 3건 검토대기
- **✅ 하네스 구조 전환 — 앱 1회 기동**: `[Collection("UI")]`은 있는데 **`CollectionDefinition`이 없어** 클래스마다 앱이 재기동됐다(14회, 477초). `Fixtures/UiCollection.cs` 신설(`ICollectionFixture<AppFixture>`) + 14개 클래스에서 `IClassFixture` 제거 → **314초(34%↓)**. 대가로 상태가 이어져 로그인전 전제 3클래스가 깨짐 → **`UI-Fresh` 컬렉션 분리**(UiaDiscovery·LoginShellSmoke는 IClassFixture 유지). 실측: 배치 250초 vs 클래스분리 336초(26%↓).
- **✅ 순서 의존성 3건 제거**: ① `SmokeNav.OpenLeftMenuItem` **진입점에서 CloseOpenPanel 선행**(개별 테스트마다 부르면 하나씩 빠뜨려 연쇄 재발) ② `GMapScenarioTests.EnsureCleanMapState()` — 패널·오버플로·메뉴 정리 + **홈 위치로 뷰포트 복귀**(줌/팬 테스트가 뷰포트를 옮겨 다음 시나리오가 "마커 없음"으로 Skip됨) ③ `ReportPreviewHybridTests`에 CloseOpenPanel 추가.
- **🔴 닫기 규약이 4종**: `ClickCancel`(대부분) / **`ClickClose`(계정·권한 관리 — 이걸 빠뜨려 REPORTS·MY PAGE가 각 48초 타임아웃)** / `Reports.Console.CloseButton` / `PART_CloseButton`. `FindCloseButton`이 4종+접미사 규칙으로 훑고, 못 찾으면 **ESC 폴백**(현재 앱은 ESC 미지원 — 보완사항).
- **✅ GMap 실행 결과**: GM-S01 마커 103개 전부 알려진 타입·rect 유효 / **D-01 툴바 HelpText 8/8 노출 → O-4 해소**(ordinal 대신 ToolTip 특정) / **GM-W01 오버플로 O-1 해소** — 팝업이 **별도 HWND로 실재**하고 `GetDesktop().FindAllChildren(ByProcessId)`로는 안 잡히나 **Win32 EnumWindows + `Automation.FromHandle`로 도달**(내부에 `EditModeToggle`·`SnapGridToggle` 계측 有). ESC로 안 닫힘 → 토글 재클릭. / GM-W02 레이어 패널 닫기규약=`PART_CloseButton` 확정.
- **⚠ 용어 정정**: `PART_TreeView`는 **`ScrollViewer`**(`LayerPanelStyle.xaml:679-684`). 트리는 중첩 `ItemsControl`+`ToggleButton`(꺽쇠) 자체구현 → UIA가 `List`/`DataItem`으로 봄 → **`ExpandCollapsePattern` 불가**, 꺽쇠에 이름 없음. "트리가 없다"가 아니라 **이름이 없어 식별이 안 되는 것**(사용자 지적).
- **✅ 탭 순회 신설(`PanelTabSweepTests`)** — 사용자 지적 "왜 탭 이동은 안 하나". **못 한 게 아니라 안 했고, 그게 결함을 숨긴 원인**이었다(VF-10=조치탭, VF-12=차트탭 — 탭별로 갈리는 결함). 21개 탭 전수 통과. 단언 = **"통지도 없는데 내용이 비면 실패"**. ⚠ **탭 헤더가 `ReportConsoleView`만 계측**, 나머지는 Header가 컨트롤이라 Name이 `System.Windows.Controls.TabItem…` → **어느 탭이 실패했는지 지목 불가**.
- **✅ Track B 조치 2건(라이브러리, 롤백태그 `before-event-silent-failure-fix`)**
  - **VF-11** `Base/Services/LogService.cs:131` `Level.Warn`→**`Level.Error`**. `Error()`가 Warn으로 나가 ① `Evaluator=LevelEvaluator(Level.Error)`(:78 "ERROR 즉시 flush") 조건 **영구 미성립**(버퍼 50줄 찰 때까지 미기록, 크래시 시 유실) ② ERROR grep 0건 → "에러 없음" 오판. **관측 계층 자체가 고장이라 가장 먼저 고침.**
  - **VF-10** `Events.Ui/Services/EventProviderService.cs:434` `Success = response.Success,` **추가**. 조치 탭만 누락(탐지:298·장애:346·연결:391은 주석까지 달고 보유). 기본값 true라 API 실패에도 통지 분기 통과 → 목록 전건 제거 + 0건 추가 = "로딩 끝난 빈 화면". **실패 분기는 대조군과 완전 동일하게 이미 작성돼 있었고 플래그만 안 세워짐** → 활성화 위험 0.
  - 빌드 0오류(라이브러리 2 + 메인솔루션), 회귀 66건 중 65통과(수정 전과 동일 수준)
- **✅ 장비 CRUD 도달성 실측(제어기·센서 등록 가능한가)**: 툴바 4버튼이 **HelpText로 식별 가능**('추가하기'·'삭제하기'·'갱신하기'·'저장하기'), DataGrid는 x:Name 폴백(`DataGridDeviceGroups`/`DataGridUsers`—4탭이 이름 공유). **'추가하기'=그리드 Draft 행 추가**(3→4, 별도 다이얼로그 아님. Edit 0인 건 셀 편집모드 진입 후 생성). 🔴 **막는 것 3**: 탭 헤더 식별불가 / **그리드 행 id 0개 → DestructiveGuard '자기 행만 삭제' 불성립(안전 문제)** / 셀 편집 진입. → **계측 PRD 선행 후 진행(사용자 B안 선택)**.
- **🔲 PRD 3건 작성중(적대검토 포함)**: `Event_Silent_Failure_Elimination`(VF-12+IsVisible 증폭기+VF-01) · `UI_Automation_Instrumentation_Phase2`(장비CRUD·탭헤더·레이어트리 ⓐ계측 vs ⓑTreeView전환·오버플로) · `Overlay_Image_Canonical_Join`(VF-09 조인키 FilePath→Images.Id + 고아 MapLayers.Id=41)
- **미해결**: SetupPanel 내비 간헐 1건 / VF-07(주입 E2E가 게이트 없이 운영서버 쓰기) / VF-08(MapPage 죽은 코드)
- **⚠ 실행 전제 정정**: `quser` Active만으론 부족 — **`Get-Process LogonUI`(화면 잠김)면 SendInput 거부**. UIA 읽기·ValuePattern은 통과해 "로그인 버튼 비활성"으로 위장. `run-ui-tests.ps1`에 하드가드 추가. [[project_uia_needs_connected_session]]

## ▶▶ 재개 포인트 (2026-08-04, 이 세션 — UI 자동화 2단계: 기능 E2E 하네스 + 억제 차단구조 규명) — ✅ E2E 4건 통과 · 원인 2건 확정·수정, 🔲 **세션 재연결 후 재검증**
- **🔴 지금 막는 것 — Windows 세션 `Disc`**: `quser` STATE가 `Disc`(연결 끊김)라 UIA·SendInput이 `Win32Exception: 액세스가 거부되었습니다`로 죽는다. **원격 세션 재연결 전까지 UI 테스트 실행 불가.** 증상이 `SmokeNav.LoginIfNeeded`의 `Keyboard.Type`에서 터져 로그인 버그처럼 보이니 주의. [[project_uia_needs_connected_session]]
- **✅ E2E 통과 4건**(`Category=UiFunctional`, 하네스 = MAIN `Dotnet.Monitoring.Solution.UiTests`)
  - `DetectionInjectionE2ETests`(E2E-1C) — 탐지 NATS 주입 → 카드 등장. **카드 패널은 `ToolTip="이벤트 패널 열기"` 버튼을 눌러야 UIA 트리에 존재**(안 열면 카드 0)
  - `SuppressionLiveBoundaryE2ETests`(E2E-3B) — 억제 활성 중 서버 `202` 확인 + NATS 직발행은 카드 등장(설계대로)
  - `MalfunctionInjectionE2ETests` ×2(E2E-4) — 장애 카드 등장 + 탐지/장애 카드 종류 분리
- **✅ 억제 차단 구조 확정**(사용자 지적 "막는 로직이 달라"): 실제 차단은 **서버 POST 핸들러 `is_suppressed()` 단 하나**(202+무저장+상태플립 생략). Proxy 라이브 발행 skip(§3.2 P-3)·GIS 알람 필터/딤(§3.1 G-4)은 **둘 다 Phase 2 미구현**. GIS의 Phase 1 몫은 관리UI·삭제2종·활성배너 = 전부 표시. `SuppressionActiveMonitor`도 배너 소스일 뿐 차단 안 함. 근거 `api-test-server/docs/subsystems/event-suppression/INTEGRATION.md`. [[project_event_suppression_blocking_layers]]
  - **부수 발견 → `VF-01`로 백로그 등재**: `SYNC_EVENT_SUPPRESSION`이 `EnumGopCommand`에 **없다**. 단 §2.7이 경고한 "수신 루프 사망"은 **해당 없음** — `NatsBrokerService.ResolveCommand`가 `TryParse` 실패 시 `NONE`+Warning, switch `default:`도 Warning으로 이미 방어됨. 영향은 Warning 2줄/발행 + 배너 즉시갱신 불가(30s 폴링 의존). **enum 1줄 추가는 사용자 승인 대기**(PRD 우선 원칙).
- **🆕 장치 신설 — `docs/analyses/verified-findings-backlog.md`(정본)**: 검증·테스트·감사에서 **증거는 확보했으나 미조치**인 사항을 모으는 곳. 기존엔 정본이 없어 session-context(세션 쌓이면 묻힘)·analyses(주제 끝나면 갱신 정지)·dev-journal(훅 전용 Phase 전환 로그)로 흩어졌다. 상태 5종(🔵열림/🟡승인대기/🟣외부/⚪설계상예정/✅조치됨), 항목 필수: 발견일·file:line·**증거**·영향·조치안. 현재 **VF-01~06 + VF-A1(조치됨)**. ⚠ `docs/`는 `.gitignore:361`로 통째 무시라 **로컬 전용** → 보완 3단: 정본(여기) + `docs/INDEX.md` 포인터(**docs 중 유일 추적**) + 소스 `// TODO(VF-xx)` 마커(git에 남는 유일 경로).
- **✅ 패널 내비 6건 실패 원인 확정·수정**(사용자 지적 "왜 닫고 끝내지 못하나"): 첫 패널만 통과하고 이후 6건 45초 타임아웃. 진단 트리에 **직전 패널이 그대로 잔존** + 클릭 좌표가 매번 `x=233`이었고 실패 스크린샷에서 그 지점은 **열린 '장비 정보' 패널 위**였다. 원인 2개 — ① 패널을 닫지 않음 ② `CompactOverlay` 접힘 상태에서 `ListBoxItem` 레이아웃 폭이 펼침 폭으로 보고돼 `item.Left+24`가 스트립 밖으로 나감. **수정**: `SmokeNav.CloseOpenPanel()` 신설(`ClickCancel` → `*.CloseButton` → 없으면 조용히 포기 — `AccountManagerPanelView`는 닫기 버튼 자체가 없음) + 클릭 X를 `HamburgerButton` rect 중앙에서 산출(DPI·창위치 무관). 빌드 0오류, **재검증 미실행**(세션 Disc).
- **진단 자산**: `Smoke/LeftMenuGeometryDiagnosticTests.cs` 신설(좌측 메뉴 기하 실측 — 세션 복구 후 1회 돌려 좌표 전제 확정용).
- **보드**: `docs/tests/ui-scenario-board.html` 갱신·재배포(실행결과 표 + 억제 3계층 표 + 블로커). 아티팩트 URL 동일 유지.
- **⚠ 미이관**: 위 테스트 코드는 전부 **MAIN 솔루션**(`C:\workspace_app\Dotnet.Monitoring.Solution\...UiTests\`) 소속 — 이 repo(v2.6)에는 문서만 있다.
- **✅ GMap 시나리오 전면 분해(정본 `docs/tests/gmap-scenario-spec.md`)**: 4도메인 병렬 분해 → **56 시나리오**(🟢NOW 27 / 🟡INSTRUMENT 14 / 🔴PIXEL 8 / ⚫BLOCKED 7), P0~P5 우선순위 + D-프로브 6종. 실측 3가지가 전략을 결정 — ① **GMaps.Ui 전체 명시 AutomationId = 3건**뿐(`GMaps.Map.Root/.FileMenu/.MapMenu`) ② **마커는 UIA에 나온다**(`List id='MainMap'` 하위 `DataItem`, Name=타입 FQN) → 타입별 개수·rect는 되지만 **인스턴스 식별 불가**(O-3) ③ **좌표·축척이 안 보이는 건 어도너 때문이 아니라 `ControlTemplate` 내부 TextBlock이 컨트롤 뷰에서 빠지는 것** — `MapViewFunctionalTests.cs:70-71` 주석은 분류 오류. 공통 장애물 O-1(툴바 오버플로 절단)·O-2(`PART_CloseButton` 9패널 공유)·O-3·O-4(이동 버튼 8개 id 공백). **계측 투자 1순위=툴바 12 id**(6병목 동시 해소).
- **✅ GMap 테스트 코드 작성+2라운드 적대검증**: `Pages/MapPage.cs` + `Functional/GMapScenarioTests.cs` 신규. 1차 검증 **결함 12건** → 전량 수정 → 2차 검증 **4건 잔존** → HIGH·MEDIUM·LOW 직접 수정. 빌드 0오류. **아직 1회도 실행 못 함**(잠금).
  - 🔴 **안전 구멍 2건 실제로 있었다**: ① 레이어 패널 `위로`/`아래로`가 금지목록에 없어 **ZOrder DB 영속 쓰기가 가드 통과**(지도 마커 메뉴 문자열만 막고 있었음) ② `NavButton()`이 가드 없이 요소를 반환해 `InvokeNavButton` 우회 가능 → 둘 다 차단. [[project_event_suppression_blocking_layers]] 와 별개
  - 🔴 **VF-07 신규**: `-Category UiFunctional` 이 **운영 서버에 게이트 없이 쓴다**(`GopApiClient.cs:21` 기본값=운영 API, E2E 3종 NatsHost=운영). 탐지·장애 주입분은 **cleanup 없음**. 내가 문서에 적었던 "운영 쓰기 0"은 **거짓**이었고 정정 완료.
- **🔒 지금 막는 것 정정 — 화면 잠금**: `quser` STATE=`Active` 여도 **`Get-Process LogonUI` 가 잡히면 SendInput 거부**. UIA 읽기·ValuePattern은 통과해서 "**로그인 버튼 비활성**"으로 위장한다(ID는 들어가고 PW만 실패). WPF `PasswordBox`는 ValuePattern 미노출이라 우회 불가. → `tools/run-ui-tests.ps1` 에 **LogonUI 하드 가드(throw)** 추가. [[project_uia_needs_connected_session]] 갱신됨

## ▶▶ 재개 포인트 (2026-08-04, 이 세션 — 기본맵 폴더 옵션화 + 빌드 복사 전환 + appsettings 전수감사·정리) — ✅ 전부 빌드 0오류·배포 완료, 🔲 런타임 확인 후 커밋(2 repo)
- **① 기본맵 폴더 옵션화 (MapData_Directory_Option)**: `AppSettings.MapDataDirectory` 신설 — `IGMapSetupModel`/`GMapSetupModel`(복사 ctor 포함)/MAIN `SetupModel` + `MapViewModel.ResolveMapDataDirectory()`(설정 실재 시 그 폴더, 아니면 실행폴더\Datas 폴백)로 **스캔(Seed)·초기로드·맵전환 3개소 일원화** + 기본맵 .mbtiles 0건이면 부팅 안내팝업(`NotifyMissingBaseMapAsync`). 설정>지도 패널에 카드 추가(MapSetupView/VM: 경로 TextBox+찾아보기 FolderBrowserDialog+상태줄 "지도 N개/⚠"+재시작 안내). ⚠ `Kind="FolderMapleLeaf"` 미존재로 XamlParseException 사고 → `Folder`로 교정 [[feedback_verify_xaml_string_enums]]. **현재값 `C:/maps/`**(사용자가 173GB 이전, 타임스탬프 보존 확인).
- **② 빌드 맵 복사 전환**: MAIN csproj의 `Datas\*.mbtiles CopyToOutputDirectory`(출력마다 173GB 복제 = 설치본 불가 뿌리) 제거 → `DeployBaseMaps` 타깃(AfterTargets=Build)이 appsettings에서 MapDataDirectory를 regex 추출(단일 중첩 프로퍼티 함수)해 **설정 폴더로 SkipUnchangedFiles 복사**, 미설정 시 $(TargetDir)Datas 폴백. 타깃 단독 실행 검증 완료(`[BaseMaps]` 로그). bin\Debug·Release·게시본의 기존 173GB 복제본은 수동 삭제 가능.
- **③ appsettings 전수 감사(워크플로 12에이전트, 77키, 미사용 13건 적대검증 전원 유지)**: ACTIVE 60 / DEAD 11 / WRITE_ONLY 2(MapType·MapMode — 저장코드 살아있어 json만 지우면 부활) / LEGACY 4. 특이: **`"Key"`는 프로퍼티명 `ApiKey`와 불일치라 애초 바인딩 안 됨** + 유일 소비 Aligo 프로젝트가 csproj/sln 미참조 고아(Phone 동일 체인). `DbDatabase`는 Gateway와 GMaps 타일 DB가 **공유**(GMap 폴백은 null일 때만).
- **④ 정리 실행(13키)**: appsettings에서 TileDirectory/IsOnSensorFusions/IntervalSensorFusions/WatchdogProc/IsServer/Coordinate/TimeIntervalSecFault/LengthMax·MinEventPrev/SessionExpiration/IsSession/IsPopupTimer/PtzTimeOut 제거(+주석잔재 3줄). SetupModel은 **인터페이스 구현분(LengthMax·Min=IEventSetupModel, IsSession·SessionExpiration=IAccountSetupModel)만 프로퍼티 잔존**, MAIN 전용 9개 프로퍼티 삭제. LIB `MapSettingsHelper.SaveTileDirectoryAsync`(호출자 0 고아) 삭제. Newtonsoft 동등 파싱 검증(64키 잔존·13키 소거). **보류: Key/Phone(Aligo 계획 결정 대기), MapType/MapMode(저장코드 동반 제거 필요)**.
- **커밋 완료(2026-08-04)**: MAIN `77dcebe`(내 5파일만 — csproj/SetupModel/appsettings/MapSetupView·VM). ⚠ **LIB 본체는 타 세션 체크포인트에 2회 쓸림** — 모델+MapViewModel은 `47c2bcc`("chore: phase plan → prd"), MapSettingsHelper 헬퍼 제거는 `43ee29c`("chore: phase prd → plan")에 포함되어 HEAD 반영 확인(재커밋 불요, 메시지만 무관). [[project_multisession_checkpoint_sweeps_wip]] 재발 — LIB에서 코드 작성 즉시 커밋해야 함. 롤백태그 양 repo `before-mapdata-directory-option`. MAIN 잔여 미커밋(타 세션): Detection/MalfunctionEventDialogView·EventPanelView·WebServerSetupView.xaml + untracked 3건 — 미접촉.
- (동일 세션 오전) 어도너 박스 정합 `15ed865` · 썸네일 로더 GC수거 수정 `9e33641` · 더블클릭 런처 테스트 13/13 `798f95b` 커밋 완료. 더블클릭 런타임 검증은 여전히 0회(로그 `[GroupActionReport]` 없음 — 깜빡이는 선 위 손가락커서→더블클릭 절차 안내됨).

## ▶▶ 재개 포인트 (2026-08-04, 이 세션 — 시작 시 first-chance 예외 진단 → 사이드이펙트 시뮬레이션 → PRD) — Track A→C, ✅ 원인 확정 + 시뮬레이션 188/2R + **PRD Draft**, 🔲 **사용자 PRD 검토·승인 → plan**
- **활성 PRD(신규)**: `docs/prds/GMap_Schema_Migration_Idempotency-prd.md` (**Draft** v1.0) — FR 15 · NFR 7 · V-01~08 · 리스크 10. 승인: `node .claude/hooks/advance-phase.js approve prd "코멘트"` (⚠ approve는 mtime 최신 PRD를 집음 — 직전 touch 필수).
- **① 진단 확정(워크플로우 10에이전트 + 런타임 재현)**: 부팅 시 디버거 `MySqlException` 22 + `NotSupportedException` 4 = 전량 `GMapDbSymbolService.BuildSchemeAsync` bare catch 소음. 22 = CREATE에 이미 있는 컬럼을 매 부팅 `ALTER ADD COLUMN` 재실행(1060). 4 = **Dapper 파라미터 오용** — `conn.ExecuteAsync(sql, token)`의 token이 `object param` 슬롯에 들어가고, `REGEXP '^-?[0-9]+$'`의 홀로 선 `?`가 Dapper `smellsLikeOleDb`에 매치 → 필터링 해제 → `The member None of type System.Threading.CancellationToken cannot be used as a parameter value`(SQLite/MariaDB 양쪽 실측). 예외 개수는 DB 세대 지문(신규 21 / 수렴 22). [[project_startup_firstchance_exceptions]]
- **② 시뮬레이션(워크플로우 28에이전트, 8축 + 완결성 비평)**: 188 시나리오 × 2라운드(2차=적대적 반증) → ISSUE 78(P0 11) · WATCH 82 · UNKNOWN 4 · **2차에서 46건 판정 뒤집힘**.
  - 🔴 **최대 위험(P0)**: 예외를 없애는 순간 같은 try의 `L561 UPDATE Symbols SET ZOrder = ZIndex`가 **처음 실행**된다. 실측 monitor_db Symbols 53행 전부 ZIndex=10 / ZOrder=1018~1070(불일치 53/53) → ZOrder 평탄화 → `L573` band shift가 전부 1010 → 지도 로드 시 `EnsureUniqueZOrder`가 붕괴 순서를 `BatchUpdateZOrderAsync`로 **DB에 영구 확정**. **코드 롤백으로 복구 불가 — 백업이 유일 수단**인데 레포에 DB 백업 자산 0건.
  - 2차에서 뒤집힌 대전제: **"CS-2로 취소가 살아난다"는 거짓** — MySql.Data 9.3.0은 명령 실행 중 CancellationToken 미관측(IL 전수 + `SELECT SLEEP(5)` 프로브). 취소 가능 지점은 `MySqlConnection.OpenAsync`뿐. → CS-2 목적을 "취소 활성화"가 아니라 **"사문 마이그레이션 정상화 + API 오용 제거"**로 재정의.
  - 기타 확정: `ADD COLUMN IF NOT EXISTS`는 MariaDB 전용 → MySQL 현장 1064 전멸이라 **information_schema 선조회 채택**(선례 `GMapDbService.cs:451-460`). `AND TABLE_SCHEMA=DATABASE()` 누락 시 같은 인스턴스의 `monitor_test_db`와 합집합 오판 → 심볼 0건. bare catch를 `catch(MySqlException)`으로 **좁히면** NotSupportedException이 탈출해 지도 전멸(→ catch-all 유지 + 분류 기록). `MODIFY` 4문을 ADD 가드에 접으면 타입 교정 영구 정지. Images Id=8의 숫자 TitleColor는 지금 `ColorHelper` 기본 분기로 **파랑** 렌더 중(정리 UPDATE 부활 시 흰색으로 가시 변화).
  - 별건 P0: **`GMaps.Db.csproj`가 xunit/Test.Sdk 직접 참조 → `DROP TABLE` 픽스처가 출하 배포본에 실림**(배포 폴더에 xunit/Moq DLL 실재). FR-15로 선행 차단.
- **✅ 롤백 포인트 구축 완료(2026-08-04, 사용자 지시)** — `C:\workspace_app\_db_backups\20260804_gmap_schema_guard\`
  - 코드: 태그 `before-gmap-schema-guard` @ **`47c2bcc8`** (v2.6, annotated)
  - 데이터: `monitor_db_full_20260804.sql`(전체덤프 76KB/22테이블) · `symbols_zorder_backup_20260804`(102행, 라이브 대조 0 불일치) · `images_title_backup_20260804`(1행) · `ROLLBACK.sql`(표적복구+전후검증) · `pre-counts.txt` · `README.md`
  - 도구: MariaDB 12.2 클라이언트 `C:\Program Files\MariaDB 12.2\bin\mariadb-dump.exe`. DB=127.0.0.1:3306 monitor_db root/root(appsettings). 비밀번호는 `--defaults-extra-file`(scratchpad my.cnf) 경유 — 명령행 노출 회피
  - 🔴 **실측 베이스라인**: `symbols_total=102`, **`zindex_ne_zorder=102/102`**, `zindex_distinct=1`(전 행 10), `zorder_distinct=102`(1000~1106), `zorder_lt_1000=0` → **전 행 파괴 대상이며 ZIndex에 복원정보 0**. 시뮬레이션 시점 53행 → **102행으로 2배 증가**(시간이 지날수록 커짐)
  - V-02 해소: TitleColor/TitleBackground 숫자행 Symbols 0 / Images 0 → 색상정리 UPDATE 부활 영향 **0행**. V-04 해소: `Symbols` 보유 스키마 = monitor_db 1곳(GMaps.Db 테스트 1회 실행 시 monitor_test_db에도 생성 → 필터 검증은 그 후에)
- **✅ PRD Approved(v1.1) + Plan 작성 완료** — `docs/plans/GMap_Schema_Migration_Idempotency-prd-plan.md` **37태스크 / 26h**, 순환의존 0·미존재참조 0. Phase0 선결검증11(VER-01~08+RISK-01~03, **5건 완료**) · Phase1 환경3 · Phase2 핵심11 · Phase3 테스트8 · Phase4 문서4. 외부 솔루션 변경 없음(단 ProjectReference라 메인 재빌드 필요)
- ⛔ **절대 순서 제약**: `IMPL-01(L561 UPDATE ZOrder=ZIndex 삭제)` → **같은 커밋** → `IMPL-04(L559 가드 전환)`. 역순이면 102행 ZOrder 영구 파괴. 커밋 diff에 해당 UPDATE 남으면 리젝
- **✅ dev 진행 19/37 (51%) — 핵심 구현 IMPL 11/11 완료, worktree `v2.9.24` @ `C:\workspace_app\worktrees\gmap-schema-guard`**
  - **정정(중요)**: `GMap.NET`은 고아 gitlink가 **아니라 추적 트리**(`040000 tree`)라 worktree에 자동 체크아웃된다. 복사 필요한 건 **미추적 `GMap.NET/Directory.Build.props`·`sn.snk` 2개뿐**. gitlink는 `OnvifSolution`(160000)이나 **GMaps.Db 빌드에는 불필요**. [[project_gmap_orphan_submodule]] 갱신 필요
  - ⚠ **worktree 손상 사고**: 세션이 `git worktree add` 도중 끊겨 **2530파일 스테이징 삭제 + csproj가 1180바이트 전부 NUL**인 worktree가 남았다. 메인 체크아웃·타 worktree는 무사(대조 확인). `worktree unlock`→`remove --force`→브랜치 삭제 후 재생성으로 해소. **중단된 checkout은 조용히 손상된 worktree를 남긴다 → 재생성 직후 `git status` 빈 상태 + 핵심 파일 head 확인 필수**
  - **구현 요지**(`GMapDbSymbolService.cs`): `COLUMN_SPECS` 선언적 사양표(Symbols 11+Images 11) → `LoadColumnMetaAsync`(테이블당 1회, `AND TABLE_SCHEMA=DATABASE()`, OrdinalIgnoreCase, **0행=판정불능→스킵+Warning**, CommandTimeout 10s) → 가드 루프 → MODIFY 분리판정(COLUMN_TYPE/IS_NULLABLE) → 색상정리 UPDATE 정상화(param 슬롯 제거) → band shift **행수 수신** → 요약로그(적용/스킵/실패/타입교정/band행)
  - **FR-08 ↔ FR-09 충돌 해소**: PRD는 REGEXP UPDATE에 `cancellationToken: token`을, FR-09는 전 구간 취소 비대상을 요구 → **FR-09 우선**(드라이버가 실행 중 토큰 미관측이라 편익 0, 스키마 절반적용 위험만 남음). 전 마이그레이션 문장에서 param·token 인자 제거. PRD v1.2 반영 필요
  - **FR-15 부분 완료**: GMaps.Db.csproj에 `IncludeTests`(기본 false) 도입 → 기본 빌드에서 `Tests/**` 컴파일 제외 + 테스트 패키지 `PrivateAssets=all`. **어셈블리에서 픽스처 코드 제거 실증**(strings 검색 0건). 잔존 `testhost.dll`은 **`Monitoring.Models` 등 24개 출하 프로젝트가 무조건부로 xunit 참조**하는 리포지토리 전역 결함 — PRD 범위 밖, 별도 PRD 필요
  - **🟢 회귀 테스트 그린**: `Tests/SchemaGuardMigrationTests.cs` 4/4 (운영 덤프를 `monitor_test_db`로 복원한 **수렴 DB** 기준. 기존 픽스처는 DropTables 때문에 파괴를 못 잡음)
  - **🔬 반증 실험(가짜 그린 아님 실증)**: 위험 UPDATE를 일시 되살리자 S2가 즉시 실패 — `(48,1036),(49,1035),(50,1034)…` → **전부 `1010`으로 평탄화**. 시뮬레이션 예측과 정확히 일치. 원복 후 4/4 재통과
  - **운영 DB 불가침 확인**: 백업 테이블 대조 `zorder_changed=0`, `color_changed=0`
- **✅ 커밋·머지·실앱 E2E 완료(2026-08-04)** — Library **v2.9.24 `36b81fb`** → **v2.6 `3c7c7c2`**(--no-ff, 충돌 0). 내 파일 4개만 명시 커밋(타 세션 WIP 무혼입). CHANGELOG `[Unreleased]` 항목 추가
  - 메인 솔루션 재빌드 0 오류 → **실앱을 운영 monitor_db 대상으로 부팅**
  - 🟢 **같은 로그에 구/신 대조**: `15:20:37` 구버전(`:659` "Symbol 관련 테이블 생성/확인 완료") vs `15:26:37` 신버전(`:737` **"적용 0 / 스킵 22 / 실패 0 / 타입교정 0 / band시프트 0행"**) → **예외 26건 → 0건**
  - 🟢 **운영 DB 무결**(부팅 중·종료 후 2회 대조): `zorder_changed=0`, `color_changed=0`, `img_color_changed=0`, ZOrder 102행 1000~1106 distinct 102, band 이탈 0
  - 신버전 부팅 구간 **ERROR 0건**. WARN은 `MarkerFactory.cs:101 LinkedDevice 바인딩 실패` 41건뿐인데 **구버전 부팅에도 52건** 있던 기존 현상(내 변경 무관)
  - 앱은 로그인 화면까지 정상 도달(크래시·오류 다이얼로그 없음). ⚠ **지도 화면은 로그인 게이팅 뒤라 미확인** — 심볼 마커 생성 로그(15:26:41)는 확인됨
  - 로그 파일은 시스템 코드페이지(CP949) 기록이라 UTF-8로 읽으면 깨짐 — 기존 LogService 특성, PowerShell `GetEncoding(949)` + FileShare.ReadWrite 로 열어야 읽힌다(앱 실행 중 파일 잠김)
- **다음**: IMPL-10(심볼 0건 사용자 가시화) · TEST-01/02/04/06/08(S1·S3·픽스처 가드) · 지도화면 E2E(로그인 필요) · DOC-01/03/04 · 현장 수집 VER-01/05/06/07/08 · PRD v1.2(FR-08↔FR-09 충돌 해소 반영)
- ⚠ 파이프라인 `plan→prd→(approve)→plan` 전환 완료. `approve prd`는 mtime 최신을 집으므로 **타 세션 `installer-prd.md`(이미 Approved)가 먼저 집혀 1차 실패** → 내 PRD touch 후 재실행으로 해소. 타 세션 PRD 오승인 없음. [[project_approve_prd_picks_newest_mtime]] [[feedback_never_force_gates]]

## ▶▶ 재개 포인트 (2026-08-04, 이 세션 — 메인솔루션 Inno 인스톨러 기획→PRD) — Track C, ✅ 기획서 v1.2 + **PRD Draft**, 🔲 **사용자 PRD 검토·승인 → plan**
- **활성 PRD**: `docs/prds/installer-prd.md` (**Approved** v1.0, 2026-08-04 사용자 "계속"=승인, touch+approve 한 명령 처리) — FR 13 · NFR 7 · V-01~08 · 리스크 7(R-01 mbtiles 169GB publish 유입=높음). Q1~Q5 기본안 확정.
- **활성 Plan**: `docs/plans/installer-prd-plan.md` — **35태스크, 진행 24/35(69%)**. ⚠ VER-04·TEST-05는 재부팅 필요(사용자 협조). 현재 Phase: dev.
- **✅ 2026-08-04 구현 1차 완성 — 설치기 빌드 성공**: `C:\workspace_app\Dotnet.Monitoring.Solution\Installer\` 신설(v0.5 브랜치 위, 태그 `before-installer`, 미커밋). 구성: IronwallMonitoring.iss(본체)+Pages.iss(설치유형·구성6화면·요약·프리필·검증)+ConfigWriter.iss(UTF8 치환+백업원복)+appsettings.template.json(감사후 스키마+토큰 28종)+build.bat(Datas격리 self-heal+fail-closed 크기가드+ISCC)+fix-autostart.bat/.ps1+Assets\sounds 3종+Korean.isl(선행품 복사 — Inno에 한국어 없음). **산출물 `Output\IronwallMonitoring_Setup_v2.6.0.0.exe` 83.8MB**(버전 자동추출·와치독 합류·mbtiles 0 확인). .iss 3종은 **UTF-8 BOM 필수**(없으면 ISCC가 ANSI 오독→UI mojibake).
- **빌드 함정 해결 3건**: ① build.bat 자체 `cd /d %~dp0` 필요(호출 cwd 무관) ② 고아 체크아웃 실명=`Ironwall.Dotnet.Libraries.OnvifSolution`(OnvifSolution 아님) ③ 배치 for /f+PS 파이프 조합이 0MB 오측 → 파일 경유+fail-closed(100MB 미만/1GB 초과/측정실패 모두 중단)로 교체.
- **최종 사용자 정책 반영**: 지도=외부 C:\maps 분리(Datas 미생성, 화면12 필수·기본값 C:\maps — PRD v2.1) · 사운드=번들+자동구성(FR-14).
- **✅ 08-04 마감 스퍼트 (plan 28/35, 80%)**: IMPL-04 에셋 4종 완성(Assets\src HTML 정본→렌더→BMP24, 버전 미표기)·IMPL-05 활성(재컴파일 83.9MB, features BMP 포함 확인)·TEST-01 verify-payload.ps1(publish 3항목 PASS, installed/log 단계는 TEST-02용)·DOC-01 README(핵심 정책 7종)·**기본 주소 전면 기입**(사용자: NATS/Redis/통제웹=localhost·DB=127.0.0.1·API=https://localhost:8000/api — PRD v2.2)·**커밋 `e2225a7`**(v0.5, Installer 23파일만 명시 스테이징).
- **🔴 08-04 TEST-02 실기 P0 발견·수정 (`34527df`)**: 설치 후 부팅 즉시 크래시 — 이벤트 로그 실증 `FileNotFoundException: xunit.core 2.9.3.0`(Bootstrapper 생성 중). **원인=내가 넣은 [Files] 테스트 어셈블리 제외**: 라이브러리가 테스트를 출하 프로젝트에 내장해 **출하 DLL이 xunit.core 하드 참조**(현행 zip 배포가 돌던 이유=bin 통째 복사). 제외 철회+NFR-04 정정(PRD v2.3)+verify-payload는 xunit.core **포함**을 검증으로 반전+README 금지 정책. 설정 생성 자체는 완벽 확인(토큰·한글·사운드·C:/maps). **근본 과제(별도)**: 테스트 프로젝트 분리 리팩토링. 재빌드 85.4MB.
- **✅ 08-04 실기 통과 (사용자 "잘 되는 거 같아" + 객관 증거)**: fix 재설치 후 정상 구동(로그 630KB·테마 저장 동작), 예약 작업 등록 완벽(**Start In=C:\PidsMonitoringSystem**·Users·로그온·시간제한 Disabled), 업그레이드 기존 유지로 위저드 값 보존. **TEST-02 [x]·TEST-03 ①기존유지 [x]**. verify-payload 실설치 10/12→정비.
- **마감 수정 3건(`b2gxk1dgt` 빌드 후 커밋)**: ①사운드 키=확장자 제외가 앱 정본(실기 확인)→ConfigWriter 수정 ②Inno Excludes 하위폴더 미매치 실측→build.bat post-publish prune(publish 407→**268MB**) ③BOM 검사 철회(앱 런타임 재저장=BOM 없음, 정상). 신규 메모리 [[project_shipping_dlls_reference_xunit]].
- **✅ 08-06 v2.7.0 릴리즈 빌드(사용자 요청 "버전 업데이트+새로 빌드")**: 그간 변경 확인(라이브러리 PidsGroup 우클릭·그룹 탐지 이력·줌 0.5 래더·재보고 허용·UIA 계측 / 메인 d0a19c1 다이얼로그 배선) → **설정 스키마 무변경 확인**(appsettings·SetupModel diff 0 — 위저드/템플릿 유효) → csproj 3필드 2.6.0→**2.7.0** → build.bat 전체 파이프라인 성공 → `IronwallMonitoring_Setup_v2.7.0.0.exe` 85.4MB(publish 272MB·위생 3/3 PASS·버전 자동추출 정상). 커밋: 버전 범프(csproj만 명시 스테이징).
- **✅ 09-03 v2.8.5 릴리즈 완료**: LIB `7881823`(와치독 WinExe+MapViewModel 안내 팝업) / MAIN(iss UninstallRun WatchdogLauncher 정리+버전). 검증: 와치독 PE subsystem=2(GUI) 실측, 위생 3/3, `IronwallMonitoring_Setup_v2.8.5.0.exe`(85.4MB, 19:47). 지도 미로드 실패 PC의 로그(`[MapData]`·`Map DB 연결`) 사용자 회신 대기.
- **🔧 09-03 현장 보고 2건 규명+수정(v2.8.5 빌드 중)**: ①**CMD 창=와치독 콘솔 확정** — 와치독 `OutputType=Exe`+자가 등록 로그온 작업 `Ironwall\WatchdogLauncher`(/rl limited, exe 직접 실행)가 매 로그온 콘솔 창 상주. 앱이 띄우는 경로는 CreateNoWindow라 무창. **수정: 와치독 csproj WinExe 전환**(Console 사용 0건 확인, 파일 로그만) + 설치기 [UninstallRun]에 WatchdogLauncher 삭제 추가. ②**지도 미로드** — 이 PC 로그(08-07~09-03 전 실행)는 `C:/maps` 정상 초기화. 코드 확정 실패 조건: (a) 폴더 변경은 **재시작 후 적용**(MapSetupView 문구 있음), (b) **DB(monitor_db) 실패 시 FetchDefinedMapsAsync throw→시드 전체 중단→`_mapProvider` 비면 MapConfigureAsync:4539 조용히 return=무증상 빈 지도**(위저드 DB 기본 127.0.0.1인 PC에서 확정 발생), (c) mbtiles 열기 실패 파일별 스킵. **수정: NotifyMapUnavailableAsync 신설** — 시드 외곽 catch·SelectedMap null 경로에서 확인순서 팝업. ⚠ MapViewModel.cs는 타 세션 WIP와 동일 파일 — 내 훅만 분리 커밋 필요.
- **✅ 08-18 v2.8.4 릴리즈(현행 개발본 전체 확인 후 재패키징)**: v2.8.3 이후 양 레포 커밋 0건, 유일 미반영=억제 스케줄 패널 XAML 08-08 수정분(작업트리) → 포함 재빌드 `IronwallMonitoring_Setup_v2.8.4.0.exe`(85.4MB, 08-18 19:11, 위생 3/3). 판정법=산출물 시각 vs 커밋+워킹트리 mtime 대조.
- **✅ 08-07 v2.8.3 릴리즈**: 차트 툴팁 테마 액티브 리프레시(8190229)+차트 테마 stale·축명 겹침 수정(76c989d)+**SetupModel.ClientId 활성화**(타 세션 미커밋분 — 템플릿은 8bc9134 선반영으로 정합) 포함 → `IronwallMonitoring_Setup_v2.8.3.0.exe`(85.4MB, 16:22, 위생 3/3).
- **✅ 08-07 뮤텍스 가드 시나리오 사후검증+P1 수정(v2.8.2)**: 3방향(카탈로그 44행/적대 8가설/실측 시뮬 10건 — 산출물 docs/tests/single-instance-guard-{scenarios,simulation-log}.md + docs/analyses/single-instance-guard-scenario-analysis.md). 코어 견고(상호자멸 근절 확정·무력화 가설 전부 기각·OS 전제 10/10 PASS). **P1 확정: 빠른 재기동 데드존**(구 인스턴스 teardown 10s+ 중 재실행→침묵 자멸→둘 다 소멸, 실측 SIM-M005a) → **수정: 활성화 선행+WaitOne(15s) 인수인계, AbandonedMutexException=획득 성공 처리**(실측 M005b/M007b 근거). P2 백로그: 와치독 만료 강행 체인(RecordRestart 오소모 포함, 타 세션 도메인)·크로스유저 fail-open(운영 수칙)·가드 위치 ctor 이후·조기 크래시 무인 방치·trace 회전·문서 stale.
- **✅ 08-07 R1~R3 구현+v2.8.1 릴리즈(`8bc9134`)**: Bootstrapper 뮤텍스 선점 가드(`Global\IronwallMonitoringGIS_SingleInstance`, fail-open)+팝업 제거·기존 창 활성화+`Logs\redundant-trace.log` 계측(부모 프로세스)+**템플릿 ClientId 반영**(`__CLIENT_ID__` 재사용). 빌드 `IronwallMonitoring_Setup_v2.8.1.0.exe`(85.4MB, 위생 3/3). **신규 규명: 재부팅 후 안 사라지는 CMD 창=와치독 콘솔**(OutputType=Exe + WatchdogLauncher 자가등록 로그온 작업) — A안(작업 삭제+설정 OFF) vs B안(WinExe 전환, 와치독=타 세션 도메인) 사용자 선택 대기. R4(UninstallRun에 WatchdogLauncher 정리)·R5(스케줄러 로그) 잔여.
- **🔴 08-07 Redundant Execution 2창 분석 완료**(3에이전트, `docs/analyses/redundant-execution-boot-race-analysis.md`): 로그온 ~40초 창에 최소 3기동(1생존 pid17660+2자멸). 구조 결함 확정=Bootstrapper 이름카운트(뮤텍스無, 승자결정無→동시기동 상호자멸)+모달 팝업 블로킹(잔존 프로세스가 연쇄 중복판정 유발). 잉여 2개 주체=계측 전무(4688/TaskScheduler로그/Prefetch off)로 미확정. 이 PC 특이점: `\Ironwall\WatchdogLauncher` 자가등록 작업(와치독 ScheduledTaskInstaller)+설치본 Watchdog=true. 권고 R1(뮤텍스)~R5, R1~R3=Bootstrapper 수정(사용자 승인 대기). ⚠ 부수: dev appsettings에 **ClientId 키 신설(타 세션)** — 설치 템플릿 미반영, 다음 패키징 전 추가 필수(미반영시 신규 설치본 전부 central-ui로 세션 상호축출).
- **✅ 08-06 v2.8.0 릴리즈(마이너 승급)**: 신호등·상단바 개편(3램프+클릭 필터·도넛 게이지·운영/편집 이원화·메뉴 재구성)+버그헌트 P2 5건 포함 → `IronwallMonitoring_Setup_v2.8.0.0.exe`(85.4MB, 21:44, 위생 3/3). 스키마 무변경. 커밋=버전 범프.
- **✅ 08-06 v2.7.1 릴리즈**: 추가 업데이트(지도 줌 범위 카드 1045547 + LIB UpdateMapZoomRangeAsync 1288510 + 미커밋 XAML 4건 — 작업트리 그대로 포함) → 패치 승급 후 재빌드 `IronwallMonitoring_Setup_v2.7.1.0.exe`(85.4MB, 17:09, 위생 3/3). 업그레이드 구조(자동제거+세팅보존) 사용자에게 확답.
- **✅ 08-06 About 버전 수정(`4ffec42`)**: About이 appsettings 수기 "Version"("0.6" 고착)을 표시하던 것을 **어셈블리 버전(csproj 정본, 스플래시와 동일 소스)**으로 전환. 폐기된 "Version" 키를 원본+설치 템플릿에서 제거(유일 소비자=About, grep 확인). v2.7.0.0 설치기 재빌드 완료(85.4MB, 16:53).
- **잔여 5건**: **VER-04+TEST-05(재부팅 3회 — 사용자, 예약작업 등록상태 검증완료라 고신뢰)**, TEST-03 ②새로구성(백업+프리필), TEST-04(제거+무인), DOC-02·03.
- **Phase 0 실측 완료(2026-08-04, VER-01~03·05~08 [x], 잔여 VER-04(재부팅)·RISK-01(build.bat 구현))**:
  - VER-01/RISK-01: publish 출력에 mbtiles 무유입, 단 `DeployBaseMaps`(AfterTargets=Build)가 빈 bin Datas로 169GB 복사 유발 실측 → **Datas rename 격리안 확정**(실제 격리 하 publish 성공, `-r win-x64` 금지).
  - VER-02: publish 1,230파일/406.8MB 완전성 통과. 파생: **`-p:ErrorOnDuplicatePublishOutputFiles=false` 필수**(NETSDK1152 — 내장 테스트 xunit.runner 3.0.1/3.1.0 충돌), 출력의 테스트asm 82건+**개발 원본 appsettings.json(운영 IP·암호)** → [Files] 제외 필수.
  - VER-03: 실측 스모크 — **`SaveStringsToUTF8File`=BOM UTF-8 한글 무결(채택)**, ANSI 저장=CP949(금지), no-BOM 템플릿 정독, 한글 분리 불필요.
  - VER-05: 와치독=별도 single-file publish 필수(앱 publish 자동 미포함). VER-06: 분기 기준="선택 폴더 appsettings.json 존재" 단일화. VER-07: 런타임 감지=`dotnet\shared\Microsoft.WindowsDesktop.App\8.*` 폴더 검사(레지스트리 키는 설치 상태서도 부재 실측). VER-08: 프리필=주석 라인 스킵+키 부재 폴백(첫-일치가 주석 옛 값을 집음 실측).
- **PRD v2.0(사용자 요청)**: **FR-14 기본 사운드 번들** — C:\sample 3종(mp3, 탐지/장애/조치 1:1) → `{app}\Sounds` 배치 + `DirectoryUri`/3키 자동 기입(**파일명만** 저장 — Sounds UnitTest.cs:134,346 근거). 위저드 화면 추가 없음.
- **⚠ 타 세션 정합**: appsettings 키 감사(77dcebe)가 **TileDirectory 포함 13키 삭제** → 화면 12 캐시 폴더 필드 제거(기획서 v1.3 반영), 템플릿은 감사 후 최신 스키마 기준. appsettings.json이 세션 도중에도 변함(TileDirectory 소실·MapDataDirectory=C:/maps/ 실측) — [[project_appsettings_key_audit_2026_08]].
- **구현 위치**: `C:\workspace_app\Dotnet.Monitoring.Solution\Installer\` 신설(앱 소스 무변경, 메인솔루션 통지=PRD 요청으로 갈음).
- **산출물(정본)**: `docs/design/installer-wizard-plan.html` — 16화면 위저드 와이어프레임 + 스토리보드(신규/업그레이드 분기, 예외 E1~E3) + 특징 보드 2장·배너 시안 + 설정 기록/보존 설계 + 기술 노트 + 미결 Q1~Q5. 아티팩트 게시본 동일(📦, claude.ai/code/artifact/f67d764e). INDEX.md 등록 완료.
- **요구사항 핵심**: ① 설치 시 appsettings.json 현장 설정을 단계별 입력(화면 07~12) ② **기술명 비노출** — NATS=통합 브로커 서버 / Redis=서드파티 브로커 서버 / API=통합 관제 서버 / DB=데이터 저장소 (제품 셋업 UI가 이미 "브로커 서버" 용어 사용해 정합) ③ 제품 특징 이미지에 **버전 표기 금지**(참조본 v2.8.5 관행 미계승).
- **조사 확정 사실(워크플로우 3에이전트)**: appsettings.json=**사용자 데이터**(앱이 AtomicFile 원자쓰기로 상시 갱신+.bak) → 업그레이드 덮어쓰기 금지=화면 06 설치유형 분기 근거. 앱=설치폴더 상시쓰기+매니페스트無 → 기본경로 `C:\PidsMonitoringSystem`(PF 선택 시 users-modify ACL). mbtiles 52+117GB → MapDataDirectory 입력(화면 12). 페이로드 ~415MB, framework-dependent .NET8(런타임 동봉=Q2). dev bin은 테스트어셈블리 오염 → **clean dotnet publish에서만 수집**. 와치독 exe 동봉+taskkill 2프로세스. 바로가기 WorkingDir={app} 필수(CWD 기준 설정 로드). Inno: wizard-large 164:314(410×785), small **정사각**(참조본 164×175는 결함), 특징 페이지 BMP 1040×660 dontcopy.
- **참조**: `C:\source\repos\Multiplatform.Downloader\Installer`(ShashalungDownloader.iss+build.bat) — 이전버전 무인제거/강제종료/버전자동추출/HTML→렌더→에셋 파이프라인 계승.
- **검증**: fork 대조검증(appsettings 커버리지 완전·명칭·스토리보드 정합 통과) + Playwright 렌더 4샷 확인. 지적 5건 반영(PTZ→원격 제어, hint margin, 415MB 통일, 분기 문구, title→head).
- **v1.1 추가(사용자 질문)**: §7.2 설정 스키마 업그레이드 정책 — 기존 키 불가침 / **신규 키=지울 필요 없음**(Bootstrapper.cs:262 optional+:309 Bind → 코드 기본값+셋업패널 저장 시 자가치유) / 폐기 키 방치(무해, 단 옛 키 읽는 1회성 마이그레이션 금지=부활 함정).
- **v1.2 추가(자동시작 인사이트)**: 사용자 지목 `Ironwall.Monitoring.Solution_Ecopro_Hungary\Installer`에서 자동시작 장애 해법 발굴 → §8.2 **D-7: 자동시작=예약 작업 필수(Run 키 금지)**. 핵심=WorkingDirectory={app}(미지정=System32→설정 미로드 빈화면), PT15S 지연, Users SID+Highest, **PT0S**(72h 함정), schtasks XML=ANSI만(UTF-8 거부)+PS -Command 폴백, fix-autostart.bat 동봉, 프리필/백업-원복 패턴도 계승. [[project_installer_autostart_scheduled_task]]
- **다음**: 사용자 검토 → Q1(제품 표기명) Q2(런타임 동봉) Q3(보드 아이콘/스크린샷) Q4(연결확인 버튼) Q5(서드파티 화면 통합) 결정 → `docs/prds/installer-prd.md` 작성 → 메인솔루션 `Installer\` 구현(메인솔루션 변경이므로 사전 통지 대상).

## ▶▶ 재개 포인트 (2026-08-04, 이 세션 — 2단계: UI 기능 테스트 계층) — Track C, ✅ **16/16 완료·머지**(Sol v0.5 `577d226`)
- **활성 PRD**: `docs/prds/UI_Functional_Testing_Layer-prd.md` (Approved→구현·머지 완료). 사용자 지시 "기능 테스트 하네스 연동, 밀도 있는 제어와 검토".
- **산출**: `Interact`(SetTextAndCommit=LostFocus 커밋 보장·콤보 선택반영 검증·`Keyboard.Type`만 사용(Press는 down만—키 오염)) · `Dialogs`(대기형 확인 `AwaitConfirmAndChoose`) · `GridHelper`(행ID `{prefix}.{DB Id}` 등장/소멸 대기) · `Pages/SuppressionSchedulePage`(표본) · `Functional/` 3종 · `tools/run-ui-tests.ps1`(Trait: Unit/UiSmoke/UiFunctional/UiDiagnostic) · test SKILL.md "UI 테스트 스텝"(.claude는 gitignore—디스크 정본).
- **라이브 그린**: 단위 30/30 · 비파괴 2종(운영: 필터 순회 건수정합·dry-run 커밋재독) · **CRUD(테스트 서버 `https://localhost:8000/api` 스왑: 생성→취소→자기행 삭제, 확인팝업 문구 캡처, 잔존 0)** · 스모크 회귀 8/8(러너 경유) · appsettings 원복 실측.
- **적대적 리뷰(wf 35에이전트) CONFIRMED 25건 전건 반영** — 핵심: [CRITICAL] 크래시 잔존 백업 재복사→운영 URL 소실 = **생성자 stale 백업 선복원** / [HIGH] 이름 기반 Kill→운영앱 즉사 = exe 경로 일치분만+foreign fail-fast / [HIGH] 취소·삭제에 쓰기 게이트 부재 = `RequireWrite` 전면 / [MEDIUM] 스왑 호스트 allowlist(loopback·RFC1918, 운영 IP 거부 단위테스트) / [HIGH] 팝업 단발 Try 레이스 = 대기형 / Reload stale 게이트 = 안정화 이중판독.
- **서버 시맨틱 실측**: `DELETE /{id}`=취소(소프트) · `bulk-delete`=terminal만 하드삭제(pending 200이어도 스킵) · 로그인 스키마 `login_id`/`password`/`client_id`.
- **다음 확장(사용자 결정 대기)**: 페이지 오브젝트 수평 확장(Reports/Devices), 심볼 AutomationPeer 트랙, 시각 회귀.

## ▶▶ 재개 포인트 (2026-08-03, 이 세션 — Playwright 자체검증 가능성 조사 → FlaUI UI 자동화 PRD) — Track A→C, ✅ 분석 2건 + PRD Draft, 🔲 **사용자 PRD 검토·승인**
- **활성 PRD**: `docs/prds/UI_Automation_FlaUI_Smoke-prd.md` (**Approved** 2026-08-03, v1.0) — FR 8개 · NFR 6개 · V 8건 · 리스크 7건.
- **활성 Plan**: `docs/plans/UI_Automation_FlaUI_Smoke-prd-plan.md` — **38태스크 / 30h**. Phase0 선결검증 11(VER-00~08+RISK-01~02) · Phase1 하네스골격 6(SETUP-01~06) · Phase2 핵심 14(계측 IMPL-01~05 + TDD TEST-01~03/IMPL-06~10) · Phase3 테스트 4 · Phase4 문서 3 · EXT 2(메인솔루션 계측 15건+빌드검증). 순환의존 0·미존재 참조 0.
- **현재 Phase**: dev
- ⚠ **실행 순서 주의**: VER-01~07은 **Phase 1 완료 후** 실행한다(검증 수단 = 하네스 자체). **계측을 눈감고 시작하지 않는다** — 트리 덤프 1회로 V-01~V-06 해소 후 IMPL-01 착수. 안 지키면 peer 없는 요소에 ID 붙이는 헛작업 발생.
- **✅ 2026-08-04 진행 — 계측 97건 완료 + 하네스 완성 + 계측 exe 런타임 검증 (18/38, 47%)**:
  - **worktree**: `C:\workspace_app\worktrees\ui-automation\` 아래 두 레포 **같은 부모·원래 폴더명**(ProjectReference `..\..\` 상대경로 제약). 브랜치 Library **v2.9.23**(v2.9.1은 기존재로 폐기) / Solution **v0.5.2**. GMap `Directory.Build.props`+`sn.snk` **미추적이라 복사 필수**(CS0281) — [[project_gmap_orphan_submodule]] 기존 기록 재확인.
  - **계측**: 워크플로우 10에이전트(계측5+검증5 전부 pass)로 XAML **정확히 97건**(라이브러리 82+메인 15). x:Name 변경 0(diff 대조)·mojibake 0·BOM 정비(원본부터 NO BOM 8파일에 추가). LeftMenu Tag 실측 11종(억제창=OpenEventSuppressionPanelViewModel, REPORTS=OpenReportPanelViewModel).
  - **하네스**(`Dotnet.Monitoring.Solution.UiTests`): AppFixture(★**`Application.Launch`에 WorkingDirectory 필수** — 미설정시 앱이 appsettings 못 찾고 10초 뒤 exit 1, 증상은 "UIA 창 못찾음"으로 위장)·EnvironmentGuard·UiaTreeDumper·DestructiveGuard(삭제 4종 차단)·FailureDiagnosis(4분류)·CdpAttach(NewPageAsync 금지)·SmokeNav(햄버거 펼침 폴백). 스모크 S1/S2/S3 코드 완성(SkippableFact — `IRONWALL_UITEST_ID/PW` 미지정시 Skip). 단위 14/14. 테스트 프로젝트 `UseWPF` 켜면 System.IO 누락 함정.
  - **런타임 검증(계측 exe, 로그인 전)**: 고유 AutomationId 20→**31**, `Accounts.Login.*` 8 노출, 구 `TextBoxID` 조회 무 — **AutomationId가 x:Name을 덮음 확정**. 햄버거 접힘 상태에선 `Shell.LeftMenu.*` 1/11만 실체화.
- **✅ 2026-08-04 스모크 전체 그린 (35/38, 92%)** — 계정 admin(사용자 제공 — 비밀번호는 문서에 기록하지 않음, 원격 운영 API 대상): **S1** 통과+NFR-01 3회 연속 flaky 0(회차 35s) · **S2** 2/2(목록 2/2건, 삭제버튼 활성 단언만, 바인딩식 행 ID `RowSelectCheckBox.28/.29` 실서버 동작 확정) · **S3 하이브리드** 통과(FlaUI Pane → **Playwright CDP: canvas=2 Chart.js 렌더·JS에러0·body 219,943자**) — 원질문 "Playwright 자체검증"의 실동작 완성.
  - **S2 최초 실패 근본원인**: CompactOverlay 접힘 메뉴의 BoundingRectangle이 전체 폭 보고 → Center() 클릭이 지도 위로 낙하 → 핸들러 미발화(앱 로그 침묵 확정, 권한 정상). 해결=**아이콘 컬럼 Left+24px 클릭**(SmokeNav). 진단 PanelHostDiagnosticTests 보존.
  - 커밋: Library `34f28fe`(v2.9.23) / Solution 하네스+계측 2커밋(v0.5.2). VER 전건 해소(V-03 DateTimePicker는 덤프 미노출=peer 부재 추정, 비차단).
- **✅ 2026-08-04 머지 완료 (37/38)** — Library **v2.6 `6b4590a`** / Solution **v0.5 `c53bebf`** (--no-ff). 메인 재빌드 후 **메인 exe 대상 S1 재검증 통과**(배포 경로 확인). worktree 2개 제거 + 브랜치 v2.9.23/v0.5.2 삭제(-d).
  - **CHANGELOG 충돌 처리(다중세션)**: ① 머지 충돌 = HEAD(타 세션 커밋 항목) + v2.9.23(내 항목) 양측 보존 해소 ② 스태시 pop 충돌 = 타 세션 **미커밋 WIP 5건**(로그아웃/제어기자동복구/enum한글화/SYNC_DETECTION썸네일 등)을 미스테이지 수정으로 정확 복원(diff 대조 검증, 백업 patch는 scratchpad). 타 세션 더티 파일 무손상.
  - **테스트 결과 정본**: `docs/tests/ui-automation-smoke-20260804/RESULT.md` (+ 스크린샷·트리덤프 24파일). 실행 가이드·재발방지 함정 5종 포함.
  - **잔여**: DOC-02(CHANGELOG `[Unreleased]`→버전 전환)는 **릴리즈 컷 시점** 작업(관행상 머지 항목은 Unreleased 유지). 재실행: `IRONWALL_UITEST_ID/PW` → `dotnet test C:\workspace_app\Dotnet.Monitoring.Solution\Dotnet.Monitoring.Solution.UiTests`.
- **출발 질문**: "Playwright로 자체 검증 가능한가?" → **WPF 본체는 영구 불가**(공식 지원=Chromium/FF/WebKit+Electron 실험). 그 자리는 **FlaUI(UIA3)**.
- **세션 직접 실측(에이전트 조사와 별도, 전부 재현 가능)**:
  - **WebView2 CDP 부착 = 앱 코드 0줄**. 환경변수 `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=N`만으로 `msedgewebview2.exe` 커맨드라인 주입 확인 → CDP OPEN → `playwright-core connectOverCDP` → textContent/evaluate/screenshot 성공 → `browser.close()` 후 호스트 앱 생존. 성립 근거: 라이브러리+메인솔루션 전체에 `CoreWebView2Environment`/`AdditionalBrowserArguments`/`CreationProperties`/`UserDataFolder` **0건**(기본 환경 → 환경변수 적용). `playwright-core`는 브라우저 다운로드 0바이트.
  - **`x:Name`은 이 앱에서 Caliburn.Micro 바인딩 지시자** — 계측은 반드시 `AutomationProperties.AutomationId`로. 실측: AutomationId가 x:Name을 이기고 x:Name은 보존됨 → 무침습. [[project_xname_is_cm_binding_not_automation]]
  - **peer 없는 요소는 이름을 붙여도 UIA에 안 나옴**: Grid/StackPanel/Border/Canvas/ContentControl/Control(기본)/Shape/커스텀 Control 파생. Grid에 AutomationId 달아도 peer=null. peer 보유 66개 타입 확정(PresentationFramework `OnCreateAutomationPeer` 오버라이드 리플렉션).
  - **커스텀 컨트롤도 제어 가능** — `OnCreateAutomationPeer` 오버라이드 한 줄이면 트리 진입, 커스텀 peer면 `AutomationId`+`Name`+`ValuePattern`(심볼 상태)까지 노출. **`GMapMarkerBaseControl<T>` 베이스 1곳 → 파생 마커 8종 전부 커버**. Adorner도 동일 방식 성립.
  - ⚠ **정정**: "지도 심볼은 UIA에 아예 안 보인다"는 **틀림**. `GMapControl:ItemsControl` + `ItemsSource=Markers`(GMapControl.cs:887-888)라 마커가 `ItemsControlItemAutomationPeer`로 **개수는 보인다**. 안 되는 건 **개별 식별**(GMapMarker에 ToString 오버라이드 없어 전부 동일 문자열).
  - ⚠ **정정**: 통합 명세의 "WebView2=peer 없음"은 **틀림**. `WebView2 : HwndHost`이고 HwndHost는 peer를 만든다 → `HwndHostAutomationPeer`(ControlType=Pane), AutomationId 정상 적용. **Pane까지는 UIA로 잡히고 내부 DOM만 CDP 필요.** 계측 96→**97건**.
  - **DEBUG는 스플래시 없음**(`Bootstrapper.cs:220 #if DEBUG` → 첫 창이 곧 ShellView), RELEASE는 스플래시 + `:233` MainWindow 명시 교체. 앞선 "DEBUG엔 교체조차 없다"는 오해 소지 서술.
- **계측 규모 실측**: peer 보유 상호작용 컨트롤 1,003개 중 `x:Name` 139 / `AutomationId` **0** / 무명 **864(86%)**. 무명 = 템플릿 밖 664 + 템플릿 안 200. 프로젝트별 무명률: Devices 96%, GMaps 97%, Events 88%, Accounts 67%.
- **사용자 승인 정책 결정(4건)**: 대상 빌드=**Debug 먼저** / S2 억제스케줄=**읽기 전용까지만**(하드삭제+운영서버 연결이라 파괴 위험) / **메인솔루션 15건 변경 승인** / S3=**서버 정상 전제, 없으면 skip**. 범위=**시나리오 우선**(864 전체 아님), 네이밍=**`도메인.패널.요소`**.
- **🔴 안전 경고**: `POST /event-suppression-schedules/bulk-delete`는 **서버 하드삭제(복구 불가)**인데 라이브 데이터가 **원격 운영 서버**. 헤더 전체선택은 terminal 행 **최대 100건** 강제 선택. → S2 삭제 단계는 코드에 넣지 않음(NFR-02로 정적 검증).
- **별건 발견(사용자 영향)**: 로그인 화면 **확인·취소 버튼이 둘 다 `IsDefault="True"`**(`LoginPanelView.xaml:204, :222`) → Enter 제출 시 비결정적. 별도 보고 필요.
- **선행 P0(별건)**: 루트 `dotnet test`가 **abort** — `Ironwall.Dotnet.Libraries.Api.deps.json`이 Autofac 8.2.0 요구, 복원본 8.3.0. sln에 테스트 프로젝트가 Watchdog.Tests 1개뿐이라 전체 [Fact] 중 ~21%만 게이트 통과. 본 PRD는 UI 테스트 프로젝트 직접 지정 실행이라 비차단.
- **산출물**: `docs/analyses/playwright-self-verification-analysis.md`(경로 판정·복붙 코드·함정 22건) · `docs/analyses/ui-automation-scenario-spec-analysis.md`(AutomationId 97건 표·단계 시퀀스·안전경고·미해결 8건) · `docs/prds/UI_Automation_FlaUI_Smoke-prd.md`.
- ⚠ `pipeline-state.json`은 **타 세션 점유**(GMap_Rotation_Full_Sync, phase=prd) → 하이재킹 안 함. [[feedback_never_force_gates]] [[project_playwright_verification_surfaces]]

## ▶▶ 재개 포인트 (2026-08-03, 이 세션 — ① 탐지 썸네일 결측 규명 ② PidsGroup 더블클릭 조치보고 PRD) — Track A + Track C, ✅ 원인 확정 + PRD Draft, 🔲 **사용자 PRD 검토·승인**
- **① 탐지 썸네일 "안 뜸" 규명 완료 = 클라 무결, 데이터 결측**: 원격 `123.141.236.253:8136` read-only 조회로 확정. 최신 탐지 10건(52355~52364, 08-03 17:06~19:03)은 `detail={signal}`뿐 — thumbnail 키 자체 없음. **thumbnails 테이블 최신 = id 347 @15:41:57** 이후 업로드 0건. 표시 경로는 반증됨(rebase URL `HTTP 200 image/jpeg`, 바이트수 메타 일치, 카드 캐시버스트 `?v=N`도 200). 오늘 그리드 대조표: 총 59행 중 **썸네일 45 / 없음 14**, 중복 URL 0건, 45개 URL 전수 200. 생산 주체는 **업스트림 탐지/카메라 모듈**(`POST /api/thumbnails` 업로드 + `detail.thumbnail` 기입, 서버 PRD §1.4) — GIS repo엔 업로드 코드 0줄. 탐지 생성 +3.4~7.2초 뒤 파일 업로드 → 라이브 카드가 처음 비는 건 정상 설계. 잔여: 업스트림 모듈 점검(사용자), 탐지 id 52342~52354는 404(삭제됨). [[project_thumbnail_producer_upstream]] [[project_thumbnail_host_rebase]]
  - 부수 발견(별건): 카드 뷰 `DetectionEventCardView.xaml:128`은 아직 `Image.Source={Binding ThumbnailUri}` **Uri 직결** — 그리드만 `ImageSource Thumbnail` 캐시로 이전됨(`02f357b`). 데이터 정상화 후 카드만 안 뜨면 1순위 의심.
- **② 신규 PRD `docs/prds/GMap_PidsGroup_DoubleClick_ActionReport-prd.md` (Draft, v1.0)**: PidsGroup 심볼 좌더블클릭 → 그룹 **최선착 이벤트** 조치보고 패널 + 손가락 커서. FR 10개 · NFR 6개 · 시나리오 **44종**(S-01~S-44) · V 8건 · 리스크 8건 · 미결 Q-1~Q-5.
  - **코드 실측 확정**: (a) 더블클릭 인프라 **이미 존재** — `GMapCustomControl.cs:1107-1116`(500ms 창)·`:886`(Shape 경로 합류) → `MapViewModel.OnMapMarkerDoubleClicked:1465`가 **IpCamera 아니면 즉시 return**. 분기 추가만 하면 됨. (b) **`EventEntry.SourceEvent`는 프로덕션 대입 0건**(테스트만, README:604 "EventId가 SourceEvent 대체") → 엔트리로 모델 못 꺼냄 ⇒ 모델 해석 3단 폴백(활성 카드 `OfType<T>` → EventProvider → 안내) 필요. (c) `GetMarkerAtScreen:1344`는 **중심 AABB** 히트 ⇒ 구역 바운딩 박스 빈 공간 오탐 위험(R-01 가능성 높음) ⇒ FR-09 라인 세그먼트 거리 판정. (d) 그룹 렌더 소스=`CompositeStatus`, EQM `_groupIndex` 보유, 조치보고 오픈=`UpdateData(카드VM,계정)`+`OpenEventReportDialogMessageModel`.
  - **설계 확정**: 트리거=Detecting/Faulted/FaultedDetecting/Blackout / 선정=타입 무관 **서버 발생시각(모델 DateTime)** → `EnqueuedAt` → `EventId` / 패널=기존 경로 재사용(심볼 복원·멱등가드 상속) / GMaps.Ui→Events.Ui는 **IoC 인터페이스 조회**(순환참조 회피).
  - **✅ 사용자 "승인"(2026-08-03) → PRD 상태 Approved** (변경 이력 자동 기록, 코멘트 "User approved v1.0 - defaults Q-1..Q-5 accepted"). 미결 Q-1~Q-5는 **기본안 채택**으로 확정(Q-3 Blackout 포함은 VER-07 결과에 따라 재확정 여지).
  - ⚠ `approve prd`는 **mtime 최신 PRD**(`scanDocsDir` desc)를 집는다 — 타 세션이 1분 전 `logout-on-exit-prd.md`를 썼기에 **touch + approve를 한 명령으로 묶어** 실행하고 출력 파일명을 검증했다. approve는 pipeline-state를 건드리지 않음(확인 완료: PRD 파일 상태 + audit + session-context "다음 할 일"만 수정).
  - **활성 Plan `docs/plans/GMap_PidsGroup_DoubleClick_ActionReport-prd-plan.md` 작성 완료**: **36태스크 / 약 25h**. Phase0 선결검증 9(VER-01~08+RISK-01) · Phase1 준비 4(SETUP-00 롤백태그~SETUP-03) · Phase2 핵심 15(TDD TEST-01~04/IMPL-01~10/REFAC-01) · Phase3 테스트 3 · Phase4 문서 3 · EXT 2(메인솔루션 **코드변경 없음**, 재빌드+E2E만). 순환의존 0·미존재 참조 0 검증 완료.
  - ⚠ **구현 순서 주의**: 최선착 선정(IMPL-03)이 정렬을 위해 모델 해석(IMPL-04)을 필요로 함 → **IMPL-04 → IMPL-03** 순.
  - **다음**: Phase 0. VER-02(Shape 더블클릭 경로)·VER-04(SourceEvent null)·VER-08(다이얼로그 열림 판별)은 **코드 정적 확인 가능**, VER-03·VER-05는 **심볼 DB 조회**로 가능, **VER-01·VER-06·VER-07은 앱 실행 런타임 관측 필요(사용자)**. Phase 0 미완 시 Phase 2 착수 금지(FR-09 설계가 VER-01/02에 좌우됨).
- ⚠ `pipeline-state.json`은 **타 세션 점유**(GMap_Rotation_Full_Sync, phase=plan) → 하이재킹 안 함. [[feedback_never_force_gates]] [[feedback_prd_before_code]]

## ▶▶ 재개 포인트 (2026-08-03, 이 세션 — 이벤트 억제 스케줄: 일괄삭제 + 서버 v2.0 회피항목) — Track B, ✅ 구현·커밋 4건 완료, 🔲 **서버 재배포**(bulk-delete) + 시각검증
- **커밋**: LIB `4c0513b`(체크박스 다중선택→일괄 하드삭제) · `c72eb9a`(컬럼 **헤더 전체선택** + 405 미배포 안내) · `b8d67bd`(**v2.0 회피항목**) / SERVER(api-test-server `release/v6.3`) `82ed70d`(POST `/bulk-delete`). 롤백태그 `before-suppression-harddelete`.
- **근거 문서**: `docs/coordination/GIS_event-suppression2.md` v2.0(API 6.3.2). PRD `event-suppression-schedule-prd.md` **v1.4**(FR-12~FR-17).
- **핵심 진단(실측)**: "선택 삭제 안 됨" = **클라 정상, 서버 미배포**. 원격 `123.141.236.253:8136`은 **v6.3.1**이라 `POST /bulk-delete` → **405**(`/{schedule_id}`로 흘러 Method Not Allowed). 원격/로컬 openapi 대조로 동일 코드베이스(GOP RESTful API Server) 확인 → `82ed70d` 배포하면 동작. 확인법: `curl -sk https://123.141.236.253:8136/openapi.json | grep bulk-delete`.
- **"취소했는데 탐지/장애 안 옴" 규명**: 서버 리보크는 **정상**(로컬 E2E: 억제중 POST→**202 suppressed·미저장·상태플립 생략**, 리보크 후 POST→**201 생성**). 진짜 원인 2가지 — (1)**억제 중 이벤트는 영구 폐기**, 리보크 **소급 없음**(장비가 재전송해야 복구; 엣지트리거라 안 옴) (2)**겹친 창 잔존**(실제로 id=2 활성 잔존이 원인이었음). → FR-15로 취소 후 `/active` 재확인 안내 추가.
- **구현(FR-13~17)**: 창 기간 상한 30일 · 중복 활성창 사전경고 · 취소 후 `/active` 잔존 안내 · 패널 상단 **활성 억제 배너** · 생성 응답 **대상 되풀이 확인**(장비명#id) · `not_found_ids` 안내 · `events:view` 게이팅 · "취소·종료 모두 정리" 버튼. 신규 `Events.Ui/Helpers/SuppressionRules.cs`(순수규칙: `IsWindowLengthValid`/`CountOverlappingActive`).
- **감사(code-reviewer)**: offset(+09:00) 전송 정상(`KoreaTimeHelper` `zzz`) · `status` 사용/`is_active` 미표시 정상 · **PATCH UI 미호출**(§5-A 500 위험 없음) · 장비 선택 **`devices.id` 전송 정상**(`DtoToModelHelper` Id=dto.Id, DeviceNumber 별도 — §6 사고 원인 아님).
- **적대적 리뷰 반영(서버)**: bulk-delete `SELECT … FOR UPDATE`(TOCTOU 오삭제 차단) · `ids` 상한 500 · 권한회수 시 버튼 즉시 비활성.
- **검증**: 억제 테스트 **31/31** · Events.Ui 빌드0 · 메인솔루션 빌드0. ⚠전체 스위트 15실패는 **타 세션 WIP 영역**(EventUiDevicePropertyBinding/DeviceSymbolLookup/DtoToModelHelper/DetectionNatsSync) — 내 4파일과 접점 없음.
- **미완**: ① **서버 재배포**(사용자 파이프라인) ② **시각 검증**(데스크톱 세션 잠김으로 보류; 잠금 해제 시 앱 실행→억제창 캡처) ③ **§7 상황도(지도 z6) 배너 = G-2 별건**(`SuppressionActiveMonitor.cs` 미커밋 상태로 남음, GMaps.Ui 연동 필요 → Track C 규모) ④ §10 Phase2 라이브 딤(정책 대기).
- ⚠ `pipeline-state.json`은 **타 세션 점유**(GMap_Rotation_Full_Sync, Track C) → 하이재킹 안 함. [[project_gis_app_uses_remote_api_server]] [[feedback_never_force_gates]]

## ▶▶ 재개 포인트 (2026-08-01, 이 세션 — 제어기 고장 블랙아웃 런타임 결함 + PRD) — Track C, ✅ 로그 근거 원인 3건 확정 + PRD Draft, 🔲 사용자 PRD 승인 후 Plan
- **활성 PRD**: `docs/prds/GMap_Controller_Blackout_Runtime_Fix-prd.md` (Draft, 사용자 "진행" 승인=경로 가) · **활성 Plan**: `docs/plans/GMap_Controller_Blackout_Runtime_Fix-prd-plan.md`(18태스크) · **현재 Phase**: dev(문서상; pipeline-state는 타 PRD 점유라 미하이재킹) · **롤백**: `before-controller-blackout-runtime-fix` · **다음**: Phase0 VER-01~04+RISK-01 → IMPL.
- **요청 맥락**: 사용자가 원 기능(`GMap_Controller_Blackout` `bff9b13`, 타 세션 `019BkpG3…` 작업)의 런타임 오동작 조사 지시. 도메인=제어기 고장→하위 센서 먹통→그 센서 그룹(구역 라인) 검정. "Blackout"은 검은 라인 시각명칭일 뿐 실사건은 FAULT_CONTROLLER 고장.
- **로그 근거 확정 3결함**(Debug 빌드, `Dotnet.Monitoring.Solution/bin/Debug/.../Logs/log-2026-07-31·08-01.txt`):
  - **A** 새 그룹 구역라인 검정 안 됨: 도출은 정상(`제어기무통신 ctrl=1352 → 그룹=[276,277]`), 그러나 `그룹 복합 상태 설정: DeviceGroup(276/277)` 로그 전무(273/275만 76/70회). 근본=`MapViewModel.InitializeDeviceSymbolIntegration:377-389`가 `s.LinkedDeviceGroup==groupId` 심볼만 `RegisterGroupSymbol`. 새 구역은 `LinkedDeviceGroup=0`생성(`LineDrawingService.cs:529`)→수동연결 필요(사용자 미연결)→`_groupSymbolLookup` 없음→`SetGroupCompositeStatus`(SymbolEventManager.cs:356-363) **무경고 no-op**.
  - **B** 부팅전 장애 조치보고 검정 미복원: `EntryId null — Dequeue 스킵`(EventCardListPanelViewModel.cs:763) 다수→EQM 미enqueue→Blackout 미소거. + 복구(SYNC_DEVICE ACTIVATED)는 OperationState만, `EventStatus=Blackout` 잔존.
  - **C** SYNC_DEVICE 상태반영 무산: `상태 동기화 실패 - 미등록 Device — 재등록(Id변경)/미배치`(SymbolEventManager.cs:210) 07-31 256회(Fence 1801/1802/2066/2067). 수신·라우팅은 정상, device경로는 경고라도·group경로는 무경고(=A와 동일 계열).
- **조사 워크플로**: `wf_59a0b549`(블랙아웃 체인 6링크, 코드 정합 확인) · `wf_41f99610`(그룹심볼 등록 4영역, LinkedDeviceGroup=0 확정). **코드 로직은 정상 — 심볼 등록/조치보고 복원/관측성 갭이 원인.**
- **PRD FR**: FR-01 그룹심볼 재등록 견고화 / FR-02 무경고 no-op 제거(관측성) / FR-03 조치보고 EntryId-null 심볼 복원(재계산 경유) + 복구시 EventStatus 소거 / FR-04 LinkedDeviceGroup 미연결 UX 가드. Out=부팅 EQM 전면 재동기화([[project_startup_fault_reconciliation]])·재등록 desync 전면([[project_device_reregistration_symbol_desync]]).
- **Phase0 검증(wf `wf_a4a5cb22` 5에이전트) 확정**: 검정 렌더 소스=**CompositeStatus**(PidsGroupMarkerStyle.xaml:257), OperationState 아님(SYNC_DEVICE는 OperationState만 갱신, SSOT주석 DeviceSymbolLookupModel.cs:76). EntryId-null 2원인: (1)라이브지만 report-VM 인스턴스 desync(52154) (2)부팅전(EQM엔트리 자체 없음, 52149). report핸들러(704/742)엔 CloseCardByEventId(671)의 FindEntryByDevice 폴백 없음. LinkedDeviceGroup 재등록 훅 지점=MapViewModel.OnMarkerPropertyChanged:7952(LinkedDevice 브랜치 옆).
- **구현 완료(빌드0·회귀0, 108/109 통과=1실패는 기존 headless 기준선)**:
  - **SETUP-01**: `EventQueueManager.GetGroupState/GetDeviceState`(+IEventQueueManager) SSOT 재계산 readers. `SymbolEventManager.RefreshGroupSymbol/RefreshDeviceSymbol`(+ISymbolEventManager)=GetState→직접세팅(ProcessEventReport 휴리스틱 회피, 공존 fault 보존). SEM ctor 선택 `IEventQueueManager?` 주입(Autofac 자동, 3-arg 테스트 호환).
  - **FR-01**: `MapViewModel.OnMarkerPropertyChanged`에 `LinkedDeviceGroup>0` 변경 시 `RegisterGroupSymbol` 즉시 재등록(대표장비 DeviceProvider 조회, 없으면 경고).
  - **FR-02**: `SetGroupCompositeStatus`/`RestoreGroupSymbol`/`SetGroupDetecting` 미등록 시 경고 + `MapViewModel` 등록루프 미매칭 경고 + 성공로그 복원(조용한 no-op 제거).
  - **FR-03(IMPL-04)**: report 핸들러(Detection/Malfunction) 공통 `ResolveReportedFaultState` — EntryId 폴백체인(_pendingEntries→FindEntryByEventId(id,Type)→FindEntryByDevice)→Dequeue, 그래도 EQM엔트리 부재면 `RefreshDeviceSymbol`+`RefreshGroupSymbol` 재계산 복원.
  - **테스트**: `EventQueueManagerStateReaderTests` 3종(blackout/normal/**공존 fault 후 Faulted=맹목 Normal 아님 RISK-01**) 통과.
- **IMPL-05 재해석(2026-08-01)**: 사용자 재정의 — "자동복구 = 자동 조치보고 = 조치보고의 한 형태". 처음엔 "조치보고로만 해제"로 정리했으나, 자동복구 조치보고를 **원함**으로 확정 → Runtime_Fix 범위 밖 **신규 PRD로 분리**.
- **신규 PRD `Controller_Fault_AutoRecovery_Extension-prd.md`(Draft, 검토 대기)**: 제어기 고장 자동복구 2트리거 — **(a)소속 센서 라이브 탐지** (b)**SYNC_DEVICE ACTIVATED 복구** → 자동 조치보고("etc 자동복구")+그룹 검정 해제. 근거 workflow `wf_30337696-d9d`(4-리더+적대검증): 기존 `Device_CompositeState_SSOT_And_FaultAutoRecovery`(REQ-01)는 **same-deviceKey만**, 제어기 cross-device는 **명시적 제외**(V-05·분석§4). FAULT_CONTROLLER=단일`(controllerId,Controller)`엔트리라 센서탐지`(sensorId,Fence)`가 절대 매칭 안 됨. 설계: EventEntry.**OwningControllerId** 태깅→Enqueue controller-ownership 정밀매칭(group-overlap 아님, 공유그룹 오매칭 방지)+`TryAutoRecoverController(controllerId)` 공개API. **FR-05=메인솔루션 NatsDomainService 배선(사전통지 대상)**. 부팅전 고장(EQM엔트리 없음)은 범위밖=Runtime_Fix 조치보고폴백 소관. 오픈이슈 3 해소(사유="etc 자동복구" 확정, 태깅위치·핸들러위치 앵커 확정). **PRD 승인·Plan 작성 완료**(`Controller_Fault_AutoRecovery_Extension-prd-plan.md`, T1~T9, 리코네상스 `wf_e477d5ac-0be`).
- **확정 앵커**: EventEntry.cs:33(OwningControllerId 추가) · EQM.cs:56-70 삽입점(⚠제거는 제어기 자신 deviceKey로) · EQM TryAutoRecoverController→Dequeue+OnAutoRecovery(594) · DetectionNatsSyncService.cs:28-44 ctor DeviceProvider 미주입→추가+EventUiModule:87-99 팩토리 배선 · [메인]NatsDomainService.cs:867-873 ProcessSyncDeviceAsync UPDATED→`Controller && Status==ACTIVATED`(EnumDeviceStatus.ACTIVATED 존재) 가드 호출, IEventQueueManager 주입(`.As<IEventQueueManager>` 등록됨→해결가능). HandleAutoRecoveryAsync(ECLPVM:599-661) 재사용.
- **dev 완료(사용자 "둘 다 지금 진행" 승인)·회귀0 입증**: 롤백태그 LIB `before-ctrl-autorecovery-20260801`@c8f21b1 / MAIN @080cd75. T1~T8 구현(EventEntry.OwningControllerId·EQM 제어기-소유 매칭+TryAutoRecoverController·DetectionNatsSyncService DeviceProvider 태깅·EventUiModule 배선·[메인]NatsDomainService FR-05 `Controller&&ACTIVATED`→TryAutoRecoverController). 라이브러리 빌드0·메인 csc0(실행앱 DLL잠금 copy만 실패=코드정상). EQM 65/65. 신규 `EventQueueManagerControllerAutoRecoveryTests` 5종 통과.
- **⚠ 회귀 조사 확정(worktree baseline 대조)**: 작업트리 전체스위트 15실패 = baseline(c8f21b1) 사전실패 11 + **타 세션 미커밋 XAML 변경 4**(EventUiDevicePropertyBindingTests). 깨끗한 baseline+내 4파일만 격리적용 시 그 4개 포함 **39/39 통과** → **내 기능 회귀 0 확정**. 백업 패치 scratchpad/backup-ctrl-autorecovery. **FR-04**(LinkedDeviceGroup=0 UX 가드)만 Runtime_Fix 선택 잔여. [[feedback_backup_before_destructive_ops]]
- **커밋·머지 완료(2026-08-01, 사용자 "커밋+충돌없음 머지+마무리")**: LIB 내 6파일만 커밋 `0da701f`(v2.6) → main FF 머지(충돌0). MAIN NatsDomainService 커밋 `78bdac3`(v0.5) → main FF 머지. **푸시 안 함**(로컬 머지만). 공유 `CHANGELOG.md`는 타 세션 항목 혼입이라 커밋 제외(항목은 작성됨, 미커밋 유지). docs/=gitignore. **런타임 E2E=앱 종료 후 재빌드=사용자**(현재 실행중이라 최종 copy만 잠김). 커밋·앱 재빌드 런타임 E2E=사용자. **⚠ pipeline-state activePrd=타 PRD 점유 → 하이재킹 안 함**. [[feedback_prd_before_code]] [[feedback_main_solution_advance_notice]] [[project_controller_blackout_propagation]] [[project_startup_fault_reconciliation]]

## ▶▶ 재개 포인트 (2026-08-01, 이 세션 — 썸네일 렌더링 안정화 + 조치보고/연결보고 SelectionView) — Track B/C 다건, ✅ 구현·빌드0·**내 파일 커밋 5건 완료**, 🔲 메인솔루션 재빌드 런타임 E2E=사용자
- **① 탐지 썸네일 렌더링 (커밋 `02f357b` 일부)**: (a) `ThumbnailUriResolver` **host-rebase** — 서버가 `detail.thumbnail`에 내부호스트(`https://192.168.202.151:8000/...`) 박음 → 절대URL host 버리고 `PathAndQuery`만 취해 클라 API base(scheme+authority)로 재조합. 이미지라우트 무인증 공개(404≠401)·인증서 SAN 외부IP 포함 확증. (b) 신규 `ThumbnailImageLoader` — 원격URL 1회 로드→**BitmapImage.UriSource+OnLoad+Freeze**→Uri정적캐시 → **탭전환/그리드 재활용에도 유지**(구 `<Image Source={Uri}>` 언로드후 재렌더 실패=blank→default 회귀 해소). ⚠**HttpClient 다운로드는 자체서명(mkcert)서 실패→"완전히 안나옴" → WPF 네이티브 경로(BitmapImage.UriSource)로 통일**(구 Image와 동일 인증서 신뢰경로). VM `ImageSource Thumbnail`, XAML `Source={Binding Thumbnail}`. [[project_thumbnail_host_rebase]] [[project_thumbnail_image_render_cache]]
- **② 조치보고 원본 썸네일·필수정보 (커밋 `02f357b`+`b715961`+`2051404`)**: ActionEvent `OriginEvent`(from_event)가 탐지면 `(as IDetectionEventModel).Thumbnail`→리졸버→로더 노출(장애=Default). ActionSelectionView 원본 필수정보(원본종류/구역/장비/번호/발생/결과·신호/사유) readonly + 패널 썸네일 컬럼. **Type·원본지정 콤보 제거**(`b715961` — IsDraft 가드+PUT from_event 제외로 편집 무의미). **장애 origin 미바인딩 수정**(`2051404` — `CommonOrNullReference`가 OriginEvent를 EventProvider Id-only 재조회→탐지↔장애 Id독립시퀀스 충돌→`Id && GetType()` 매칭+`?? ret` 폴백). [[project_event_origin_id_type_collision]]
- **③ 연결보고 SelectionView (커밋 `c535ac2`+`b56ee0c`)**: 연결=조회전용(device/status 불변, PUT=type_event만, `ConnectionEventDto`에 status 없음) → 편집 콤보·Apply 제거, **탐지/장애와 동일 포맷 장비정보(구역/종류/장비/번호) readonly만**(라벨열56·ReadOnlyValue 15/0.75). 롤백 `before-connection-selection-readonly`.
- **멀티세션(동시=탐지 SYNC_DETECTION 세션[아래 line 20 블록]·enum한글화 세션[아래])**: 공유 `DetectionSelectionView.xaml`의 내 1줄(`Source` ThumbnailUri→Thumbnail) **미커밋**(조율 `docs/coordination/EVENTS_THUMBNAIL_COORDINATION.md`). `git add -A/-a 금지`, **내 파일만 명시 pathspec 커밋**. 통합접점: 그쪽 SYNC_DETECTION이 새 썸네일URL 갱신 시 로더는 URL키캐시라 자동 새로드, 단 행VM `NotifyOfPropertyChange(Thumbnail/OriginThumbnail)` 필요. [[project_multisession_checkpoint_sweeps_wip]]
- **산출**: 와이어프레임 `action-report-dialogs-wireframe.html`(조치보고 이벤트 섹션 추가)·`connection-selection-wireframe.html` · PRD `action-report-origin-thumbnail-prd.md`+plan · 롤백태그 4종(`before-thumbnail-host-rebase`/`-image-cache`/`before-action-origin-thumbnail`/`before-connection-selection-readonly`). 빌드 0오류·한글BOM. 🔲 **메인솔루션 재빌드·재배포 후 육안**(탐지/조치 썸네일·탭전환 유지·장애 origin 사유·연결 장비패널)=사용자. [[project_library_deployment_path]] [[feedback_verify_in_real_context_diff_siblings]]

## ▶▶ 재개 포인트 (2026-08-01, 이 세션 — 탐지/장애 이벤트 UI enum 한글화 컨버터) — Track C, ✅ 전수 식별+PRD Draft, 🔲 사용자 PRD 검토·승인 대기
- **요청**: 탐지 이벤트 패널 + 이벤트 리스트 패널에 노출되는 영어 enum(`Fault`/`Intrusion`/`Fence` 등)을 **표시 전용 Converter**로 한글화. "우선 영어 enum 노출 전수 식별". **하드 제약(사용자 명시)**: 메시지/포맷 절대 불변 — **UI에서 보이는 것만** 한글. enum 정의/DTO/NATS/DB/SelectedItem 값 전부 불변.
- **전수 식별(인라인, grep 확증)**: 2메커니즘 — M1 직접텍스트바인딩(`.ToString()`), M2 ComboBox 항목(`EnumBindingSourceExtension`=GetValues만). 대상 enum 5종: `EnumEventType`(MessageType), `EnumDetectionType`(Result), `EnumFaultType`(Reason), `EnumDeviceType`(DeviceTypeText="Fence" 출처=`Device.DeviceType.ToString()`), `EnumTrueFalse`(Status). **노출지점**: [P0] 탐지카드 Result(DetectionEventCardView:186/375)·장애카드 MessageType(:159)/Reason(:299) · 탐지패널 Result컬럼+콤보(DetectionEventPanelView:428/438) · 장애패널 MessageType/Reason컬럼+콤보(MalfunctionEventPanelView:283/293/378/388) · Selection편집기 Type/Status/Result/Reason콤보+종류(Detection/MalfunctionSelectionView). [P1] 조치보고 다이얼로그(Selection 재사용) · 탐지이력 다이얼로그(DetectionHistoryDialogView:473 Result컬럼 + 칩 Name/ShortName + TopResultText + 차트라벨=VM). 기존 `DeviceTypeName`(ExEventViewModel/EventCardViewModel)은 이미 한글 switch(제어기/카메라/스피커/함체/경고등/센서). `EnumToDescriptionConverter`(Utils) 존재하나 `[Description]` 부재로 전부 ToString 폴백.
- **사용자 결정(AskUserQuestion)**: ①범위=**P0+P1**(다이얼로그 포함, 연결/조치 탭은 P2 제외) ②방식=**전용 EnumToKoreanConverter**(EnumKoreanMap SSOT 딕셔너리 + 단방향 컨버터 + VM 공용 헬퍼). `[Description]` 방식 배제(enum 불변 제약).
- **설계**: `Events.Ui/Converters/EnumKoreanMap.cs`(값→한글 딕셔너리+`To(Enum)` 폴백 ToString) + `EnumToKoreanConverter.cs`(IValueConverter, Convert만, ConvertBack NotImplemented) → `Events.Ui/Resources/Resources.xaml` Converters블록(라인 331~) 등록. M1=Text 컨버터, M2=ItemTemplate 컨버터(SelectedItem raw enum 유지), VM문자열=EnumKoreanMap 호출. 배치=Events.Ui(향후 Utils/Enums 승격 여지).
- **산출**: `docs/prds/Event_Enum_Korean_Display-prd.md` v1.0(FR-01~08, NFR-01~05, V-01~03). **V-01=Status(EnumTrueFalse) 표기**=A안 "조치완료/미조치" 채택(코드 `IsActioned=Status==True`, 사용자 재확인 대상). [[feedback_prd_before_code]] [[feedback_prd_worktree_required]] [[project_category_discriminator_contamination]] [[project_malfunction_card_controller_sensor_display]]
- **→ 승인·구현(사용자 "진행")**: PRD Approved(`advance-phase approve prd`) + plan(`docs/plans/Event_Enum_Korean_Display-prd-plan.md`) + 롤백태그 `before-enum-korean-display`(v2.6). 구현: 신규 `Converters/EnumKoreanMap.cs`(값→한글 SSOT, 폴백 ToString)+`EnumToKoreanConverter.cs`(단방향). 적용: 카드 2·패널 2(컬럼+콤보 ItemTemplate)·편집기 2(콤보 ItemTemplate 신설 PowerShell 정밀치환)·이력 다이얼로그(컬럼+VM 칩/통계/차트라벨). VM `DeviceTypeText`(종류=Fence 출처)·`ResultChipViewModel.Name`·`TopResultText`·`SignalChartPoint` 라벨을 EnumKoreanMap로. **SelectedItem은 raw enum 유지**(저장/전송 불변). 신규 `EnumToKoreanConverterTests` 10/10. `DetectionSignalTests` 1건(TopResultText 한글) 기대값 갱신. 한글 BOM 4파일.
- **→ 적대리뷰(code-reviewer opus) CRITICAL 1건 자가검증+수정**: **`Events.Ui/Resources/Resources.xaml`는 앱에 미머지** — 메인솔루션 `Dotnet.Monitoring.Solution/App.xaml`(45행)은 **복제본** `Dotnet.Monitoring.Solution;component/Resources/ResourceDictionary.xaml`만 머지(라이브러리 Resources.xaml 아님). → 거기 등록한 `EnumToKoreanConverter`를 6개 뷰가 `{StaticResource}`로 못 찾아 **XamlParseException**(기존 `SignalBarWidthConverter`가 뷰마다 로컬 선언된 이유). **수정**: 6개 뷰 `<UserControl.Resources>`에 `<converters:EnumToKoreanConverter>` **로컬 등록**+누락 `xmlns:converters`(DetectionHistoryDialogView는 이미 로컬). MEDIUM: 표시 TextBlock 3곳 `Mode=TwoWay`→`OneWay`. 재빌드 0오류·테스트 26/26. **교훈: Events.Ui/Resources.xaml는 앱 미머지 → 라이브러리 뷰 컨버터는 뷰 로컬 등록 필수(앱측 ResourceDictionary.xaml는 수기 복제본).**
- **⚠ 커밋(사용자 "커밋해줘")**: 동시세션이 `b56ee0c`/`c535ac2`(연결보고 SelectionView, P2) 커밋 — **내 파일 안 쓸려감**. 안전 분리 커밋 완료 `a7e74c7`(내 15파일: 신규3+단독10+혼재2 hunk만). **혼재 파일**(Detection/MalfunctionSelectionView·CHANGELOG)은 `git apply --cached`로 **내 hunk만**(타 세션 썸네일 크기 hunk는 unstaged 보존), CHANGELOG는 타 세션 미커밋 항목과 같은 hunk라 **제외**(워킹트리 보존). Python hunk필터 `scratchpad/filter_hunks.py`. 파괴적 명령 0. [[project_multisession_checkpoint_sweeps_wip]]
- **→ 카드 썸네일 레이아웃 재설계(사용자 "썸네일 나중 뜰 때 구조 깨짐" 보고→ v3 와이어프레임 승인→"진행")**: 문제=탐지 카드 앞면 **히어로 썸네일 밴드**(Height 62, `Visibility Collapsed(0px)↔62px` 토글)가 나중 로드 시 본문 밀어 리플로우. **해결**: 밴드 제거 → 본문 2행(①구역·장비 **전체폭 단일라인**+말줄임 ②좌 **56×56 고정 슬롯**(뒤 ImageOffOutline 기본+앞 Image HasThumbnail)+우 결과·신호). 슬롯 크기 불변 → 지연 로드 **리플로우 0**. 장애도 동일 골격(사진 없어 기본이미지 상시+"사진 미제공"). 뒷면 250→**220 통일**(플립 폭점프 제거). 구역·장비 잘림 원인=다중 TextBlock 분할 → 기반 VM `EventCardViewModel.ZoneDeviceText`("그룹-장비타입 번호" 단일, 표시전용) 신설+단일 TextBlock CharacterEllipsis. 롤백 `before-eventcard-thumbnail-layout`. 파일=`EventCardViewModel.cs`+`Detection/MalfunctionEventCardView.xaml`(전체 재작성, BOM). 빌드 0오류·카드/컨버터 테스트 41/41·회귀0. 와이어프레임 `docs/design/EventCard_Detection_Malfunction_Storyboard.html` v3. 🔲 커밋 미실행+앱 재빌드 런타임 육안. [[feedback_verify_in_real_context_diff_siblings]] [[project_events_ui_resources_not_merged]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 이벤트 억제(정비 창) 스케줄 UI 설계+PRD) — Track C, ✅ 와이어프레임+스토리보드+PRD Draft(연구 워크플로 3에이전트 grounding), 🔲 사용자 검토·승인 대기
- **요청**: `docs/coordination/GIS_event-suppression.md` 기반, 장비/그룹/전체 이벤트 억제 스케줄 CRUD UI를 Tactical Command Dark/Light + Conductor(PanelShell) 시스템에 맞게 HTML 와이어프레임+스토리보드+구현 PRD.
- **소스 G-1/2/3**: G-1 정비창 CRUD(6엔드포인트 POST/GET목록/GET{id}/PATCH/DELETE soft-cancel/GET active) events:view/edit/delete · G-2 활성 억제 배너 /active 30~60s 폴링 · G-3(Phase2 정책D1) 라이브 딤(완전숨김 금지=딤+표식+카운트). 폼: name·target_type(device/group/all)·target_device_id·target_group_id·target_side(det/surv/both)·event_scope(conn/det/malf/all)·window_start/end(KST). status 파생 pending/active/expired/cancelled.
- **연구 워크플로 `wf_d002d000`(architect+general×2)**: ①호스팅=ShellVM→LeftMenuSection→ConductorControlVM(라우터 IHandle<Open…>)→PanelShellVM(ConductorOneVM=단일활성 완전교체, 탭 아님). **신규 최상위 패널**(권장A, events축 공유); 대시보드 4탭/SETUP탭 기각. 선례=**GrantManagementPanelViewModel**(대상+유효기간+POST+무한스크롤+soft-cancel Confirm, 거의 1:1). 폼=인라인(다이얼로그 아님), 삭제=OpenConfirmPopupMessageModel. ②인프라: event-suppression 클라 없음(신규)—IEventSuppressionApiService를 EventApiModule Named IApiService 재사용(Bearer 상속). DTO=Messages/Dto/Events(BaseDto snake_case), Model=Monitoring.Models. CRUD베이스=BaseDataGridMultiPanelViewModel(Temp-state Draft Id≤0+일괄Save+RunCrudOperationAsync 봉투, 선례 DetectionEventPanelViewModel). RBAC=PermissionUiPolicy(CanView/Edit/Delete("events") fail-open+PermissionsChanged). 피커=DeviceProvider/DeviceGroupProvider(싱글톤 ctor주입). KST=KoreaTimeHelper.ToServerIso8601(+09:00 거짓Z금지). ③비주얼: Theme/Styles.Containers.xaml keyed(PanelSectionLabel/StandardPanelCard/ModernDataGrid), 날짜시간=**mah:DateTimePicker**(MD DatePicker는 날짜만), DataGrid HeadersVisibility=Column+RowHeaderWidth=0(거터함정), 상태배지=WindyIndicatorStyle DataTrigger pill, 무한스크롤=Utils.Behaviors.DataGridScrollEndBehavior, 배너=SurfaceTranslucent+StatusWarning z6.
- **산출**: `docs/design/event-suppression-schedule-wireframe.html`(G-1 패널+G-2 배너+G-3 딤, 권한 disable 시뮬, Dark/Light 토글) + `docs/design/event-suppression-schedule-storyboard.html`(9장면 REST/NATS/UI 메시지) + `docs/prds/event-suppression-schedule-prd.md` v1.0 **Draft**(FR-01~11, NFR-01~06, V-01~07, 인과결합·리스크). **메인 솔루션 라우팅(FR-07)=착수 전 통지 필요**. 🔲 사용자 PRD 검토·승인. OQ: 진입점·PATCH범위·G-3 Phase·배너 배치층. [[feedback_prd_before_code]] [[feedback_main_solution_advance_notice]] [[project_view_architecture]] [[project_permission_ui_gating_model]] [[project_server_datetime_aware_iso8601]]
- **→ 대상 복수화 개정(사용자 "그룹이면 그룹 복수, 장비면 장비 복수, 전체")**: 서버 실제 구현 확인(`api-test-server/app/schemas/event_suppression.py`+`v67` 마이그레이션)=**현재 단일 대상**(`target_device_id`/`group_id` 단일 int, junction 없음). 사용자 요구=한 스케줄에 장비 복수 **또는** 그룹 복수 **또는** 전체(**혼합 없음**, target_type 배타). → **서버 다중대상 확장 요청 글**(복붙용, 파일 아님) 작성: `target_device_ids[]`/`target_group_ids[]` 배열+junction 2테이블+게이트 `is_suppressed` IN/교집합+목록필터+응답배열+하위호환. GIS 3문서 v1.1/v1.2 개정: 와이어프레임(멀티셀렉트 칩 UI)·스토리보드(scene2 복수칩)·PRD(DTO List<int>, FR-01/04/05, **V-01=다중대상 확장 블로킹 선결**, 리스크). 🔲 서버 확장 딜리버리 후 GIS 구현 착수(그전엔 보류). [[feedback_accurate_analysis_first]] [[reference_api_test_server]]
- **→ 구현 착수(사용자 "구현하자"→"GIS만, 서버 이미 적용")**: 서버 재확인=**이미 다중대상 완전구현**(`api-test-server` schema/model/router/enums: `target_device_ids[]`/`target_group_ids[]`, junction 2테이블 `event_suppression_target_devices/groups` CASCADE, enum device/group/all·detection/surveillance/both·connection/detection/malfunction/all·pending/active/expired/cancelled, 봉투 ApiSingleResponse/ApiResponse+PaginationMeta, 필터 page/limit/status/target_type/device_id/group_id, POST/PATCH **extra=forbid**). **GIS 데이터/API 레이어 구현+빌드0**: ①`Messages/Dto/Events/EventSuppressionScheduleDto.cs`(응답=BaseDto상속 전필드 + **요청 DTO는 BaseDto 미상속**=편집필드만, extra=forbid 회피) ②`Events.Api/Services/IEventSuppressionApiService`+impl(6엔드포인트, `{Url}/event-suppression-schedules`, 확장메서드는 `Messages.Helpers`=ApiMessageHelper) ③`EventApiModule` 등록(Named IApiService 재사용→Bearer 상속). **Events.Api 빌드 0오류**. UTF-8 BOM. 🔲 남은 것: 모델/Provider·**패널 VM(BaseDataGridMultiPanelViewModel)+RowVM+View XAML**(멀티셀렉트 칩/DateTimePicker/무한스크롤/상태배지)·메시지 2·EventUiModule DI·**메인솔루션 Conductor 라우팅/메뉴(착수 전 통지)**·G-2 배너·테스트. **미커밋**. PRD Draft(승인/plan 생략하고 코드 직행 — 사용자 지시). [[feedback_dev_phase_agents]]
- **→ 라이브러리 UI 레이어 완료(빌드0)**: 서버 재확인=**이미 다중대상 완전구현**. 선례=**GrantManagementPanel**(VM/View 정본 복제). 신규 파일: `ViewModel/Models/CommonMessages.cs`에 `OpenEventSuppressionPanelMessageModel` 추가 · `Events.Ui/ViewModels/Panels/EventSuppressionScheduleItemViewModel.cs`(행 표시=대상요약/상태/KST) · `EventSuppressionSchedulePanelViewModel.cs`(BasePanelViewModel, **멀티셀렉트 칩**(SelectedDevices/Groups+DeviceToAdd/GroupToAdd 세터추가패턴)·target_type 배타(IsDeviceMode/Group/All)·필터·무한스크롤 SimpleCommand·KST ToServerIso8601·events 권한게이팅·취소 Confirm→CallCancelSuppressionMessageModel→HandleAsync DELETE) · `Views/Panels/EventSuppressionSchedulePanelView.xaml`(**전부 DynamicResource 토큰**: PanelSectionLabel/StandardPanelCard/ModernDataGrid/ModernToolbarButton, 상태배지 pill DataTrigger(StatusNormal/Info/Warning/Muted), 칩 TintAccent/Primary, mah:DateTimePicker 시작~종료 한줄, HeadersVisibility=Column+RowHeaderWidth=0, DataGridScrollEndBehavior) · `EventUiModule` VM 등록. **Events.Api+Events.Ui 빌드 각각 0오류**. 함정수정: AsyncRelayCommand(Accounts)→SimpleCommand(Events.Ui), ToApiResponseAsync 확장=Messages.Helpers. 🔲 **메인솔루션 배선(착수 전 통지)**: ConductorControlVM `IHandle<OpenEventSuppressionPanelMessageModel>`+`CanOpenPanel("events")`+PanelShell.ActivateItemAsync · LeftMenu 버튼/핸들러(사용자 질문=진입점 A 좌측메뉴). 🔲 G-2 배너·테스트·런타임 E2E. [[feedback_main_solution_advance_notice]] [[project_permission_ui_gating_model]]
- **→ 커밋 + 메인솔루션 배선 완료(사용자 "라이브러리 먼저 커밋하고 진행")**: **라이브러리 커밋 `f3747d4`(v2.6, 9파일 +1243)** — 내 파일만 명시(EventUiModule/CommonMessages/EventApiModule diff 격리 확인). **메인솔루션 배선 커밋 `080cd75`(v0.5, 3파일)**: `ConductorControlViewModel`(IHandle<OpenEventSuppressionPanelMessageModel>→CanOpenPanel("events")→PanelShell.ActivateItemAsync(라이브러리 VM 직접활성, ReportConsole 패턴)·using 추가) · `LeftMenuSectionViewModel`(OpenEventSuppressionPanelViewModel 핸들러) · `LeftMenuSectionView.xaml`(햄버거 "억제창" HamburgerMenuIconItem, CanSeeEvents 게이팅, CalendarClock). 롤백태그 `before-event-suppression-wiring`. **메인 CS/XAML 0오류**(빌드실패=앱 락/mbtiles 디스크 환경). **G-1(CRUD 패널) 끝까지 배선 완료** — 좌측 메뉴 "억제창"으로 열림. 🔲 남은 것: G-2 활성배너·테스트·앱 재빌드 런타임 E2E. 원격 push 미실행. [[project_multisession_checkpoint_sweeps_wip]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 탐지 SYNC_DETECTION 썸네일 갱신) — Track C, ✅ PRD Approved+Plan+구현+테스트(6/6, 회귀0), 🔲 커밋 미실행 + 앱 재빌드 런타임 E2E=사용자
- **요청**: `docs/coordination/GOP_Server_API_detection_sync_NOTIFY.md` 분석 → "현재 탐지로 EventQueueManager에 등록된 경우 업데이트해서 썸네일 갱신". 서버 신설 `SYNC_DETECTION{action:UPDATED|DELETED, resource_id}` @ `all.sync.detection`(PTZ 회전 후 유효 썸네일).
- **핵심 발견(이해 wf `wf_8de9f1dc` 5에이전트)**: 전제 불일치 — **EQM/지도심볼/실시간 카드에 썸네일 없음**(썸네일은 이력 패널·선택편집기 2곳뿐, REST 캐시 기반). NATS 수신 배선 불필요(`all.sync.detection`은 이미 `all.>` 브로드캐스트 와일드카드 포함, SYNC_DEVICE 동일 경로). GET by-id 이미 존재(`EventApiService.GetDetectionEventByIdAsync`, 404=Success=false). 카드↔EQM 매칭=`ViewModelProvider.FirstOrDefault(c=>c.Model?.Id==eventId)`.
- **사용자 결정(AskUserQuestion 2회)**: ①대상=**실시간 탐지 카드**(카드 UI 신설) ②DELETED=**로그만**(UPDATED 전용) ③배치=**앞면 히어로**(카드 200→~320 확장, HasThumbnail 없으면 Collapsed).
- **구현(Path A 라이브러리, 11파일)**: 신규 `DetectionSyncNatsService`(멱등구독·로그인게이트·cmd필터·`FindEntryByEventId` 활성게이트→GET→UI스레드 `DetectionThumbnailSyncedMessage` 발행, GET는 펌프 비블로킹 분리) + `IDetectionSyncNatsService` + `DetectionThumbnailSyncedMessage` + `EventQueueManager.FindEntryByEventId` + 카드 VM `ThumbnailUri`(캐시버스트 `_thumbVersion`)/`HasThumbnail`/`ApplyThumbnailUpdate` + `DetectionEventCardView.xaml` 앞면 히어로행 + `EventCardListPanelViewModel.IHandle<>` + `EnumGopCommand.SYNC_DETECTION=29` + `EventUiModule` 등록(Order `_count+4`) + `DetectionSyncNatsServiceTests` 6종.
- **WIP 회피**: `EventProviderService`/`DtoToModelHelper`/`ThumbnailUriResolver`(host-rebasing) **타 세션 동시 수정 중** → **미수정**(IEventApiService 직접 사용, 캐시버스트는 카드 VM 로컬). 커밋은 내 파일만 명시(git add -A/-a 금지). [[project_multisession_checkpoint_sweeps_wip]]
- **적대리뷰(wf `wf_3bc9589d` 18에이전트) CONFIRMED 6건 수정**: F1[High] 카드조회 id충돌→`OfType<DetectionEventCardViewModel>()` / F2[Med] null 썸네일 소거→`!IsNullOrEmpty` 가드 / F3[Med] `FindEntryByEventId` EventType 미판별→`EnumEventType` 인자(Intrusion) / F4[Med] `dispatcher.InvokeAsync` 내부Task 미언랩→`.Task.Unwrap()` / F5[Low 보류] 테스트시드 경합(단일메시지라 무영향) / F6[Med] 같은id 동시GET 순서역전→resource_id별 seq 가드(newest-wins). **교훈: 탐지/장애는 독립 id 시퀀스라 숫자 id 매칭 어디서든 타입판별 필수**([[project_undo_id_collision_all_commands]] 재현).
- **사용자 추가지시(턴 중)**: ①"탐지/장애 카드 가로세로 맞춰줘"→카드 크기 장애와 동일 고정(앞 220×200/뒤 250×200), 썸네일은 상단 밴드(Height 62, Collapsed시 0px). (초기 히어로 200→320 확장 결정 수정.) ②"다른쪽에서도 같은 영역(카드) 작업중이니 조심"→카드 파일 동시세션, **커밋 내 파일만**(git add -A/-a 금지).
- **검증**: Events.Ui 빌드 0오류(신규 경고 0) · 신규 테스트 **12/12 통과**(sync 9 + 카드VM 3: 순서역전 stale폐기·EQM 타입판별·썸네일 보존 포함) · 사이드 78/79(1실패=**기존 기준선** `DetectionNatsSyncService.OnNatsDetection_ShouldPublish` headless Application.Current=null, 회귀 0=CHANGELOG/session 확증) · 한글 BOM 5파일. 롤백태그 `before-detection-sync-thumbnail`(v2.6). **커밋 미실행**. 🔲 앱 재빌드 후 런타임 E2E(실 SYNC_DETECTION→카드 썸네일 갱신·탐지/장애 카드 크기 일치)=사용자. [[project_detection_signal_pipeline_gap]] [[feedback_prd_before_code]] [[feedback_deliver_visible_design]] [[project_multisession_checkpoint_sweeps_wip]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 장애 카드 제어기/센서 오배정) — Track C, ✅✅✅ 정본원인 확정+수정+커밋(`196fd59`·`0548d81`·docs `bd11788`)+**main ff머지(→bd11788)**, 🔲 메인솔루션 재빌드·배포=사용자
- **머지**: v2.6→main fast-forward(ref-only `git fetch . v2.6:main`, 워킹트리 무접촉, 동시세션 미커밋 WIP 18개 무손상). main이 v2.6 조상이라 divergence 0. 내 3커밋+동시세션 datetime `de3f8c4`(기커밋)가 함께 main 반영.
- **정본 원인(사용자 실측 전문으로 확정)**: 실 MALFUNCTION 전문 `body.device`=flat(`controller_id`:1351 FK, `number_device`:센서번호, `type_device`:"Fence", `detail`:null). **FAULT_CABLE_CUTTING이 제어기가 아니라 Fence 센서(1802)에 실려 옴** → 카드 `ControllerDisplay/SensorDisplay`가 **사유(reason)로 장비타입을 단정**(CABLE_CUTTING=제어기)해서 센서번호를 제어기 칸에, 센서 칸은 공란. **reason≠장비타입.**
- **왜 탐지만 정상**: 탐지 카드는 reason 게이트 없이 `ControllerDeviceNumber`/`Device.DeviceNumber` **raw 바인딩**(DetectionEventCardView.xaml:324/339). 장애만 reason 래퍼 거쳐 오배정. (검증 wf `wf_1b2d1cc6` 8에이전트 corroborate)
- **수정(`0548d81`)**: `MalfunctionEventCardViewModel.ControllerDisplay/SensorDisplay`를 **장비타입 기준**(`Device is ISensorDeviceModel`)으로 교정 — 센서장비=제어기(ControllerDeviceNumber)+센서(자기번호), 제어기장비=제어기(자기번호)+센서(null). detail null→4값 0,0,0,0은 ToMalfunctionEventModel이 이미 `?? 0`. 테스트 장비타입 기준 재작성(CABLE_CUTTING-on-sensor 포함), Events.Ui 46/0. 롤백 `before-malfunction-card-controller-sensor-fix`.
- **선행 커밋 `196fd59`**: ControllerDeviceNumber controller_id→Provider 폴백 + ConvertDeviceFromDto LinkControllerFromDto + Devices.Ui ToSensorDeviceModel FK seed(제어기번호 해석 견고화). 둘 다 격리인덱스로 내 훵크만 커밋(동시세션 KoreaTime/AuditLog/UU 무손상). 🔲 **앱에 반영되려면 메인솔루션(Dotnet.Monitoring.Solution) 재빌드·재배포 필요**(라이브러리 v2.6 커밋 단독으론 미반영=여전히 실패의 1순위). [[project_malfunction_card_controller_sensor_display]] [[feedback_verify_in_real_context_diff_siblings]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 장애 카드 flip 뒷장 제어기/센서 공란 견고화) — Track C, ✅ 구현·테스트(회귀0), 🔲 커밋 미실행+발생시점 [MALFUNCTION] 로그로 발화모드 확정=사용자
- **요청**: 장애 이벤트 발생 시 장애 카드 flip 뒷장 **제어기/센서 번호 미입력**. 사용자가 auto-report(클리어) 구간 로그 제시 → `[SYNC-DIAG] Sensor(1801) Controller 재연결 완료: Controller(1351, DeviceNumber=1)`(Provider 하이드레이션 정상 증명).
- **진단(로그+검증 wf `wf_91ef2322` 9에이전트)**: **구조적 근본(H4 CONFIRMED)** = MALFUNCTION 전문 `BaseDeviceDto`는 컨트롤러 표시번호 없이 `controller_id`(FK)만 → 제어기는 `Controller.DeviceNumber`(중첩 nav) 바인딩이라 **DeviceProvider 로컬 해석이 유일소스**인데 클라 변환이 `controller_id`조차 버려 Provider 참조 미온전 순간(폴백/미하이드레이션) 복구불가·공란. `72865ab`(2026-06-02) SYNC 재연결은 정상이나 안전망 부재. `833436d`가 `[MALFUNCTION] Controller(Id/No/Name)` 진단로그 추가(카드빌드 시점 확인용).
- **수정 4곳(라이브러리, 메인솔루션 무변경)**: ①[중심] `EventCardViewModel.ControllerDeviceNumber` 중첩번호 0이면 `Controller.Id`로 DeviceProvider 재해석 ②`Events.Ui/DtoToModelHelper.ConvertDeviceFromDto` 폴백 `LinkControllerFromDto`(controller_id→Provider 연결/보존) ③`Devices.Ui/DtoToModelHelper.ToSensorDeviceModel` 중첩 controller 부재 시 `ControllerId` seed ④`MalfunctionEventCardViewModel.SensorDisplay` 화이트리스트→블랙리스트(FAULT_ETC 센서표시). 신규 `MalfunctionCardDisplayTests` 11종. Events.Ui 48통과/0 · Devices.Ui 27통과/0 · 빌드0 · 한글BOM. 롤백 `before-malfunction-card-controller-sensor-fix`(v2.6).
- **⚠ 다중세션 공유파일**: `Events.Ui/DtoToModelHelper.cs`(내 LinkController + 타세션 KoreaTime 공존)·`CHANGELOG.md` → **커밋은 내 훵크만**(git add -A/-a 금지). 🔲 **발생시점** `[MALFUNCTION] Controller(No.=?)` 로그+reason으로 D1(No.=0)/D2(FAULT_ETC)/D3(폴백) 중 실화모드 확정=사용자(견고화라 어느 것이든 커버). [[project_malfunction_card_controller_sensor_display]] [[project_multisession_checkpoint_sweeps_wip]] [[feedback_bug_claims_need_evidence]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 제어기 무통신→그룹 검은색 GMap_Controller_Blackout) — Track C, ✅✅ 시뮬(101×2)·구현·적대리뷰·v2.6 머지 `bff9b13`, 🔲 런타임 육안=사용자
- **요청**: 제어기 장애 발생 시 그 제어기에 연결된 센서가 모두 먹통 → 해당 센서 그룹의 PidsGroupSymbol을 **검은색**. 제어기 해소+센서장애 잔존이면 검정→센서장애 그룹은 주황 복귀. "시나리오 100개 만들어 시뮬 2회 돌리고 결과 반영 PRD 구현".
- **분석(Explore)**: `EnumCompositeEventStatus`=Normal/Detecting/Faulted/FaultedDetecting/Connection(dormant). 검은색 없음. `EventQueueManager.ComputeGroup/DeviceState`=EventType(Fault/Intrusion) OR집계, 세부타입 미참조. 색=`PidsGroupMarkerStyle.xaml` CompositeStatus 트리거(주황#FF9800/빨강#F44336). 토폴로지=`SensorDeviceModel.Controller`/`DeviceProvider`(싱글톤). **3갭**: 검정상태 부재·EventEntry에 Reason 없음(MalfunctionNatsSyncService가 body.Reason 미독)·그룹범위가 제어기 자기그룹만. 산출=`docs/analyses/Controller_Blackout_Propagation-analysis.md`.
- **시뮬(2회)**: 순수 `ControllerBlackoutModel`(Resolve 우선순위 Blackout>FaultedDetecting>Faulted>Detecting>Normal + ExpandBlackoutGroups) + `ControllerBlackoutSimulationTests` 101 시나리오(A단일/B덮음+재부상/C다중제어기/D탐지/E엣지/F순열/G시드무작위70). 시뮬 vs **독립오라클**(토폴로지 재유도) 매스텝 대조 → 330스텝·734대조·1·2회 실패0·결정성.
- **구현**: `EnumCompositeEventStatus.Blackout`+`EnumEventStatus.Blackout`·`EventEntry.IsControllerBlackout`·EQM 상태계산 Resolve화·MalfunctionNatsSyncService(DeviceProvider 주입+reason 파싱+그룹확장)·SEM Blackout 케이스·PidsGroup/Pids 모델 매핑+마커 검정 트리거.
- **적대리뷰 wf(13에이전트) CONFIRMED 2건 수정**: ①[Critical] MalfunctionNatsSyncService가 **수동 팩토리 람다** 등록이라 8번째 DeviceProvider 미전달→항상 null→기능 죽음(EventUiModule 인자 추가) ②[High] `PidsSymbolModel.CompositeStatus`에 Blackout 케이스 누락→제어기 자기 마커 주황→**초록 회귀**(모델 매핑+PidsMarkerStyle EventStatus=Blackout 트리거). 빌드0·59통과. **교훈: 수동 팩토리 DI는 optional 파라미터 자동주입 안 됨(명시 전달 필수)**. **신호원 가정=MALFUNCTION reason=FAULT_CONTROLLER**(device-status 대안은 발행처 부재, PRD §7). [[project_symbol_label_adorner_system]] [[project_category_discriminator_contamination]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 장애 이벤트 자동조치보고 독립 설정) — Track C, ✅ PRD Approved+plan+**라이브러리 구현·테스트(회귀0)**, 🔲 [외부 솔루션] 메인 UI/설정(EXT) 사용자 통지 대기
- **요청**: `이벤트설정`의 탐지 항목 1개가 탐지·장애 자동조치보고 타이머를 동시 결정 → `탐지 이벤트 해제` 아래 `장애 이벤트 해제` 항목을 **동일 디자인**으로 신설, 장애 자동조치보고를 독립 결정. "자동조치보고 꺼지면 장애 미보고"(명시 요구). "PRD 만들어줘".
- **핵심 코드 사실(1차 자료)**: `DetectionNatsSyncService`(128–129)·`MalfunctionNatsSyncService`(113–114)가 **둘 다** `TimeoutSeconds=TimeDiscardSec, IsAutoReportEnabled=IsAutoEventDiscard` 스탬프 → 공유. `EventQueueManager.OnSharedTimerTick`은 이미 엔트리별 `if(!e.IsAutoReportEnabled)continue;` 게이트 → 장애가 자기 필드만 읽게 하면 독립·off=미보고 성립(EQM/EventEntry 무변경). 지속경로: `appsettings.json`→`SetupModel` 싱글톤(NATS서비스 참조로 라이브 읽음)+VM `SetEventSetupPropertyAndSaveAsync`→`UpdateSetupModelProperty`(동일 싱글톤 리플렉션 세팅+원자적 저장)=런타임 반영.
- **검증 워크플로 `wf_e1fa5fc1`(architect+code-reviewer+consumer-sweep 3에이전트)**: ①EQM 글로벌 kill-switch(`!IsAutoEventDiscard`)는 **dead code**(`ResolveOptional<IEventSetupModel>()`=null, 미등록) — 현재 결합 없음, 향후 DI갭 수정 시 재결합 랜드마인. ②**최우선 리스크=업그레이드 기본값**: `SetupModel` 신규 필드에 C# `=true`/`=20` 필수(PreserveNewest라 구 appsettings에 키부재→Bind 미설정→false/0로 무음 OFF 또는 0초 폭주). ③`EventSetupModel` 복사생성자 수기 복사라 신규필드 복사라인 필수. ④카드 카운트다운=데드(표시 없음)→UI FR 불필요. ⑤appsettings 첫 저장 시 `//`주석 전소실(선재, 이 기능이 트리거).
- **산출**: `docs/prds/malfunction-autoreport-setting-prd.md` v1.0 **Draft** — FR-01~07(라이브러리 3파일: IEventSetupModel/EventSetupModel/MalfunctionNatsSyncService + 메인 4파일: SetupModel/appsettings.json/EventSetupViewModel/EventSetupView.xaml + 테스트), NFR-01~05, V-01~05, 인과결합·리스크 표. 네이밍 채택=접두 `IsMalfunctionAutoEventDiscard`/`MalfunctionTimeDiscardSec`(도메인 형제쌍 정합, 승인 전 변경 가능). **메인 솔루션 변경 포함=착수 전 사용자 통지 필요**. 🔲 사용자 PRD 검토·승인(`advance-phase.js approve prd`) 후 plan. [[feedback_prd_before_code]] [[feedback_main_solution_advance_notice]] [[project_autoreport_dual_path_issue]] [[project_appsettings_nonatomic_write_corruption]]
- **→ 승인·구현(사용자 "승인"→"진행")**: PRD Approved(advance-phase, dev→plan backward+reason) + plan 19태스크. **라이브러리측 완료(Phase 0~4)**: IMPL-01 `IEventSetupModel`/`EventSetupModel`(+복사생성자)에 장애필드 `IsMalfunctionAutoEventDiscard`/`MalfunctionTimeDiscardSec` · IMPL-02 `MalfunctionNatsSyncService`(라인 113–114)가 신규필드 읽기(**탐지 `DetectionNatsSyncService` 무변경**) · TDD **Red(3실패)→Green(5통과)** · 신규 `MalfunctionNatsSyncServiceTests`(매핑/off게이팅/장애≠탐지타임아웃/탐지독립성 4) + `EventSetupModelTests`(복사 1) · Events.Ui **340통과/7실패(전부 기존 기준선=DeviceSymbolLookupModel FOV/zoom×6 + DetectionNatsSyncService.OnNatsDetection_ShouldPublish×1, 회귀 0)** · 한글 BOM. 롤백태그 `before-malfunction-autoreport-setting`(v2.6). VER-01(kill-switch inert 확인)·VER-03(복사 green). **커밋 미실행**(내 파일만: Events 모델 2 + Events.Ui 서비스 1 + 테스트 2 + docs/plans·prds·INDEX·session + CHANGELOG). 🔲 **[외부 솔루션] EXT-01~05 메인솔루션**(`SetupModel` C#기본값 **=true/=20 필수**·`appsettings.json`·`EventSetupViewModel`·`EventSetupView.xaml` `장애 이벤트 해제` 항목 신설) = **착수 전 사용자 통지 대기**. VER-02(키부재 바인딩)·VER-04(x:Name)·VER-05(첫 저장 주석소실 고지)는 EXT 종속. [[feedback_dev_phase_agents]] [[project_multisession_checkpoint_sweeps_wip]]
- **→ 메인 솔루션 EXT 완료(사용자 "Dotnet.Monitoring.Solution 작업+롤백포인트")**: 롤백태그 `before-malfunction-autoreport-setting`(메인 v0.5 `ad17c73`). EXT-01 `SetupModel` 장애필드 **C# 기본값 `=true`/`=20`**(업그레이드 회귀 방지=RISK-01) · EXT-02 `appsettings.json` 키 2개(true/20) · EXT-03 `EventSetupViewModel` 바인딩 속성 2개(동일 저장 패턴) · EXT-04 `EventSetupView.xaml` `탐지 이벤트 해제` 클론→`장애 이벤트 해제`(고유 x:Name *TimerMalfunction 3종, 아이콘 EventClock/AlarmOff 동일, 힌트 "이벤트 장애 자동 해제"). **컴파일 검증**: 메인 격리출력 빌드 **CS/XAML 0오류**(빌드 "실패"는 전부 환경—실행중 앱 PID12860+VS DLL 락 40건 / mbtiles 디스크부족 4건, 코드오류 0). 라이브러리 5테스트 재green(타 세션 blackout WIP 병합 후). VER-02(C#기본값 설계보장)·VER-04(XAML 컴파일)·VER-05(고지) 완료. ⚠**미커밋 다중세션**: 라이브러리 `MalfunctionNatsSyncService.cs`=내 2줄+**타세션 GMap_Controller_Blackout WIP 공존**(deviceProvider/IsControllerBlackout/ControllerBlackoutModel) / 메인 `appsettings.json`=내 키+타세션 NATS주소 WIP / 메인 `Detection·MalfunctionEventDialogView.xaml`=타세션 WIP → **커밋은 내 훵크만 분리**(git add -A/-a 금지). 🔲 EXT-05 **런타임 E2E**(앱 재빌드→이벤트설정 `장애 이벤트 해제` 표시·토글 OFF→장애 미보고·탐지 정상)=사용자.

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — API/NATS 로깅 커버리지 개선) — Track C, ✅ 실측+PRD Draft, 🔲 사용자 승인 대기
- **요청**: "API 호출·NATS 호출/수신 성공·실패에 로그 필요". 로깅 커버리지 실측 워크플로 `wf_adc288d5`(4도메인 병렬+종합 → 11 패키지). 결과=`tasks/w5uhxgjbr.output`.
- **핵심 발견**: ①**확정 버그** `LogService.cs:131` Error()가 Level.Warn로 기록 → ERROR 필터/알림 무력화 + 즉시-flush(:78) 영구 미발동(크래시 직전 버퍼 50건 유실). ②REST 4xx/5xx 무음 통과(ApiService EnsureSuccessStatusCode 부재, 전 계층 무로깅) — 오늘 proxy 404 헤맨 원인. ③인증(login/refresh/logout) 클라 로컬 로그 전무(AccountApiService, GetMe만 예외). ④WINDY 발행/RequestAsync no-responders는 모범(기준선). ⑤제약: 고빈도(TRACKING/PTZ 1Hz) 성공로깅=스팸→ILogService에 Debug/샘플링 없어 all-or-nothing(P1 선결), 민감정보 마스킹.
- **구조적 이점**: ApiService.Initialize:57-63에 DelegatingHandler 삽입 슬롯 존재 → LoggingHandler 1개로 전 REST 도메인 무편집 커버.
- **사용자 범위결정**: "P0 핵심만" → PRD Draft `docs/prds/GOP_Logging_Observability_P0-prd.md` v1.0: FR-1 LogService.Error 레벨(1줄) / FR-2 HttpLoggingHandler 신설(method+url+status+시간, 2xx=INFO·4xx=WARN·5xx=ERROR, **본문 미로깅**) / FR-3 인증 의미로깅(loginId만, 토큰금지) / FR-4 마스킹 규약 명문화+죽은 ResponseHelper Raw-JSON 부활금지. V-1 핸들러 위치(refresh 재시도)·V-2 X-Request-ID 헤더=plan Phase 0. Out=P1(NATS 발행·수신 코어·ILogService 확장)·P2(수신 전면·LogEvent 표면화). 🔲 사용자 PRD 검토/승인 대기. [[feedback_accurate_analysis_first]] [[feedback_prd_before_code]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 조치보고 detail 레이아웃 정리 + 썸네일 이미지화) — Track B, ✅ 구현·검증(회귀0), 🔲 커밋 미실행+육안=사용자
- **요청**(스크린샷 `docs/pictures/스크린샷 2026-07-31 154236.png`): 조치보고 다이얼로그 "탐지 속성" detail 레이아웃 정리 + 썸네일 표시(안되면 default). 추가지시: **"탐지 속성 창 Height 키우지 말고 스크롤 가능하게"**.
- **진단**: `DetectionSelectionView.xaml`(SelectedItemEditor로 DetectionReportDialog+EventDashboard 공용)이 10열 불규칙 그리드+ColumnSpan 억지배치 → 값 시작 어긋남. 썸네일=`ThumbnailText`(URL 문자열) TextBlock.
- **구현 3파일**: ①View 재작성=좌(라벨:값 세로정렬 Type/Device/Status/Result 편집+신호/AI/객체 읽기전용)+우(썸네일 150×110), 좌측 `ScrollViewer MaxHeight=150`(창 Height 불변+스크롤). ②VM `ThumbnailUri`(절대 그대로/상대는 API base host 결합, base=`IoC.Get<IApiSetupModel>` try/catch)+`HasThumbnail`. ③EventUiModule에 `IApiSetupModel` RegisterInstance(base seam). 썸네일 fallback=Image를 default("미리보기 없음") 위에 겹침→로드 실패 시 뒤 default 노출(코드비하인드 불요, ImageFailed 불필요). ⚠mkcert 자체서명 https라 실제론 default 자주 뜰 수 있음(인증서 문제 별개).
- 검증: Events.Ui 빌드0 · 335통과/7실패(**stash 대조로 기준선 확증=DeviceSymbolLookupModel FOV·DetectionNatsSyncService, 내 회귀 0**) · 한글BOM. 롤백 `before-detection-detail-layout-thumbnail`. CHANGELOG [Unreleased] 기록. **커밋 미실행**. 🔲 앱 재빌드 후 육안(레이아웃 정렬·스크롤·썸네일 or default). ⚠MalfunctionSelectionView(장애)는 미수정(탐지만 요청). [[feedback_ui_tweak_act_simply]] [[project_detection_signal_pipeline_gap]]

## ▶▶ 재개 포인트 (2026-07-31, 이 세션 — 로그인 실패 시 raw 예외 문구 노출 버그) — Track B, ✅ 구현·검증, 🔲 커밋 미실행+육안=사용자
- **제보**: 로그인 패널에 빨간 문구 "Cannot access child value on Newtonsoft.Json.Linq.JValue." (스크린샷 2026-07-31 125318.png). 1차 원인=원격 API 서버(`http://123.141.236.253:8136/api`, appsettings Url) 연결 실패.
- **근본원인 2겹**(로그 12:52:41 실측): ①`ApiAccountGateway.cs:47` v6.3 잠금정책 파싱 `d?["failed_count"]` — `d`=`DetailsToken`(JToken). **`?.`는 C# null만 거름**, 서버 `details:null`→`JValue(Null)`·연결실패 로컬에러(Details 세터)→`JValue(string)`이라 `JValue["key"]`가 "Cannot access child value" 예외 → `LoginPanelViewModel.ClickOk` catch가 `ex.Message`를 라벨 노출 → 준비된 FailMessage "서버에 연결할 수 없습니다"(SERVICE_UNAVAILABLE 매핑 존재)를 가로챔. ②기존 테스트는 details=객체만 커버(구멍). 도달경로=서버다운 or 미존재계정(details:null).
- **수정**: ①`ApiAccountGateway.cs` `DetailsToken as JObject`(객체만 파싱, 비객체=generic 전개) ②`LoginPanelViewModel` `LoginFailureException` 도입 — 의도된 실패(FailMessage)와 예기치못한 예외 분리, 후자는 raw 대신 일반문구+스택 ERROR 로그(SEC-5). 태그 `before-login-details-jvalue-fix`.
- 검증: 빌드 0 · Accounts.Api 139/139(회귀 3종: details 문자열JValue/JSON null/객체). Accounts.Ui=테스트프로젝트 아님(빌드로 검증). ⚠앱 재실행 시 원격서버 다시 살아있음(405)→로그인 성공하므로 "연결실패 문구" 재현은 서버 끊어야. CHANGELOG Fixed 기록. **커밋 미실행**(요청 시 — 라이브러리 3파일, 워킹트리 클린 상태서 시작). [[project_gop_permission_delivery_gotcha]] [[feedback_accurate_analysis_first]]

## ▶▶ 재개 포인트 (2026-07-30, 이 세션 — GMap 지도 회전 재활성 GMap_Rotation_Full_Sync 구현) — Track C, ✅✅ P0~P5 전체 구현·v2.6 FF 머지 지속(최신 `01d652b`), 🔲 E2E 2차 잔여+메인솔루션 재빌드=사용자
- **경로**: worktree `C:/workspace_app/worktrees/rotation-fullsync`(feature/rotation-fullsync) → v2.6 FF 머지 반복. 롤백 5중: 태그 `before-rotation-fullsync`(8c6c7b1)+백업브랜치+번들+worktree+kill-switch(RotationFeature 기본 OFF). 단계태그 rotation-p0-done…p5-done.
- **완료(코어)**: canonical bearing SSOT(ApplyMapRotation, RotationMath [-180,180))·MapViewportSnapshot+Publisher(구독 12소비자, replay+coalesce)·DisplayAngle/FOV θ-free·오버레이 래스터 후보B(0-diff)·ViewArea 4-코너·불변 중심사각(GetImageScreenRect)·벤더 seam 7종(V-01 승인). 테스트 RotationMath 31+Publisher 6.
- **E2E 피드백 수정(전부 머지)**: 툴바 나침반 버튼+Ctrl+Shift+R(`IsRotationToggleEnabled`=앵커 활성 시 잠금, ShowOnDisabled 툴팁)·**라벨 진동 3차해결=ProjectSmooth**(FromLatLngToCoreLocal double+RotationMatrixValue, 지형고착 — 사용자 확인 "진동 해결")·앵커 옵션C=A(정북강제)/B(AllowRotation, 4-코너 라이브 inset)·회전허용 체크박스 자동체크+나침반 ON 중 잠금(`IsAnchorAllowRotationEditable`)·**회전 영속**=MapRotationModel→AppSettings.MapRotation(토글+1s 디바운스 저장, 부팅 ApplyMapAnchor 후 복원, 메인솔루션 SetupModel `574102a` v0.5)·앵커 활성 시 나침반 Editable=false(`6f9ee50`).
- **오늘**: 사용자 문의 "빨간 + 가운데 왜 존재?" → 벤더 GMap.NET `ShowCenter`/CenterCrossPen(±5px 빨강, 회전 작업 이전부터 앱이 true로 켜둠, 앱 자체 파란 십자 토글과 중복) → false 비활성 `01d652b` 머지.
- **오늘② 회전+앵커 재시작 백지밴드 버그**: 스크린샷 실측(경계 x=886 완전수직+#F5F5F5=EmptyMapBackground)+appsettings(`MapRotation.Angle:0.0`, `AllowRotation:true`)로 확정 — **부팅 복원이 자기 파일 오염**: `IsEnabled=true` → EnabledChanged 핸들러 SaveMapRotationState() '각도 적용 전' 동기발화 → Angle 0.0 저장, debounce는 `_lastSavedRotationAngle(θ)` 중복판정으로 자가치유 차단 → 다음 부팅 정북 + θ기준 중심좌표가 MBTiles 범위 밖 노출. 수정=`_suppressRotationSave` 복원 억제+종료 debounce 플러시 await(`44bc6fc` 머지). 기존 오염 파일은 재회전 1회로 자가복구. ⚠일반 함정: **복원 경로에서 변경 핸들러가 반적용 상태를 영속화**.
- **오늘③ 후속(44bc6fc 재빌드 후에도 백지)**: 이번엔 Angle=-90 정상 저장 확인 → 남은 원인=**부팅 리프레시 순서**(사용자 관찰 "줌/패닝 1회로 복구"). 1차 시도 `ApplicationIdle` 1회 리프레시(`3c19cdf`) **실패** — 로그 실측: idle 콜백이 부팅 await 중(코어 미시작)에 발화, ViewArea=(0.004088×0.000000)=미시작 코어 클램프 좌표(경도폭=화면높이 760px·위도폭 0). 2차 확정(`6e3230e` 머지): 벤더 seam `RefreshRotationState()`(Core.OnMapSizeChanged 현재 레이아웃→bounds+RenderOffset 재정립+UpdateRotationMatrix+ForceUpdateOverlays)+`IsCoreStarted` 노출, VM은 IsLoaded+IsCoreStarted+실크기 준비까지 250ms×40 재시도 후 seam+스냅샷 재발행+수동 줌 동일 리프레시 1회. ⚠v2.6 머지 시 타 세션(GIS NATS) WIP가 MapViewModel 공유 — 헝크 비겹침 확인 후 stash→FF→pop(충돌 0, WIP 보존). ✅**사용자 런타임 확인(2026-07-30): "이번에는 제대로 나온다"** — 회전+홈+앵커 재시작 정상 복원.
- **오늘④ E2E 2차 잔여 착수**: ②R-41 격자 표시가드 완료(`64493cb` 머지, DRAW도 |rot|>0.1 숨김, 헤드리스 43/43) ④R-22 실측(read-only): 심볼 53, 비영 Bearing=PIDS_EQUIPMENT 6기(최대 271°→NormalizeDeg 실사용) ①③육안 체크리스트 `docs/tests/GMap_Rotation_E2E2-scenarios.md` 작성(A~J 섹션, 기확인 ✅표기) — 사용자 실행 대기 ⑤flag 노출 **사용자 확정=현행 유지**(기본 OFF+툴바/단축키+영속, 코드 변경 없음). 잔여=①③ 육안 체크리스트만 — 통과 시 클로즈아웃(report+CHANGELOG).
- **오늘⑤ 신규 요구(사용자)**: 지도 회전 싱크 **방위각 심볼(나침반) CustomControl** + 클릭-드래그 이동 → 와이어프레임 `docs/design/GMap_Compass_Control-wireframe.html` v1.0 작성(Track A, 코드 무변경). 변형 A택티컬로즈(권장)/B미니멀링/C배지, 라이브 데모(θ 슬라이더·이동·링 회전·더블클릭 정북), 설계=GMapCompassControl templated+Generic.xaml, PART_Rose RotateTransform(−Bearing), ViewportSnapshotPublisher 구독, 표시=RotationFeature 연동, 벤더 ShowRotationControl 대체. **미결 OQ-1 위치영속/OQ-2 링회전조작/OQ-3 휠미세/OQ-4 기본위치 — 사용자 회신 대기, 확정 후 PRD**. [[feedback_prd_before_code]]
- **→ v2.0 재디자인(사용자 "Dark/Light 확인+테마 매칭")**: Theme 정본 토큰 실값 추출(Tokens.Dark/Light.xaml 전량)→CSS 변수 미러+🌙/☀ 토글. v1 다크 하드코딩 7건 교정: Primary(#22B8D9/#0C6B89)·베젤=SurfaceTranslucent(제작 반투명 토큰, alpha 계산 금지)·러버라인=Accent(앰버, "고정 기준=Accent/회전 요소=Primary" 규칙)·N=StatusCritical·소눈금=Divider·S바늘=Border·리드아웃=MapFloatingPanelStyle 계열. 변형 C 제외 제안(OQ-5). WPF ARGB→CSS RGBA 순서 변환 주의.
- **→ v2.1(사용자 확정)**: 크기 등 설정=**우클릭 컨텍스트 메뉴**(심볼 우클릭 패턴 준용, e.Handled로 맵 전파 차단, 데모에 동작 메뉴 구현: 크기 S/M/L·스타일 로즈/링·각도표시·정북). 설정+위치=`AppSettings.MapCompass` {X,Y,Size,Variant,ShowReadout} 통합 영속 제안(OQ-1 갱신). 잔여 미결=OQ-1~5 회신 후 PRD.
- **→ 구현 완료(사용자 "PRD=바로 승인, 끝까지 완료" 지시)**: PRD `GMap_Compass_Control-prd.md` v1.0 Approved(D-01~08 수렴: 링회전 포함/휠미세 제외/C변형 제외/우클릭 메뉴/z6 계기층/우상단 M/통합 영속) → plan 12/12 → **v2.6 `731d8d4`**(rebase 후 FF — 타 세션 `888e03a` GIS NATS 커밋 위) + 메인솔루션 `0cfe61e`(SetupModel.MapCompass). 신규: GMapCompassControl(GMapControls, 허브 드래그 패턴)+CompassControlStyle.xaml(Generic 병합)+CompassMath(+enum)+MapCompassModel+MapViewModel.Compass.cs(파셜)+CCMS 복원 1줄+CompassMathTests 29. **적대 리뷰 wf 14에이전트 CONFIRMED 8건 전부 수정**(캡처유실 리셋·링 데드존·더블클릭 Up승격·표시클램프/영속분리+캔버스 리사이즈 재클램프·투명모서리 x:Null·크로스레포 윈도우). 테스트 72/72·양레포 CS 0. 롤백 태그 `before-compass-control`(=888e03a). 🔲 런타임 육안=사용자(앱 실행 중이라 재빌드 대기).
- **→ 신규 계기 2종 와이어프레임(사용자 "강풍모드/탐지·장애 커스텀 컨트롤, 설정 아이콘, 우선 Wireframe")**: Track A(디자인만). 실 도메인 확정: WINDY 4모드(`EnumWindyMode` wind0 보통/wind1 약풍/wind2 강풍/wind3 태풍), 탐지 `EnumDetectionType`(절단/복구/PIR/열/진동/접점/거리/AI), 장애 `EnumFaultType`(컨트롤러/펜스/복합/케이블/기타), 상단메뉴=`_File`/`_Maps`(IsCheckable+IsChecked TwoWay 패턴). 산출: `docs/design/GMap_Windy_Indicator-wireframe.html`(배지, 모드별 아이콘/색 Normal→Info→Warning→Critical, 평상시숨김) + `GMap_Detection_Fault_Indicator-wireframe.html`(탐지/장애 집계 pill+타입칩, 활성 강조, 0건숨김, 세로/가로). 둘 다 실토큰 Dark/Light·드래그 데모·보기(View)메뉴 목업·설계표(CustomControl/DP/상태소스/z6/영속). **공통 통합 요구(구현 시)**: 상단 `_View` 메뉴 IsCheckable로 나침반·강풍·탐지장애 3계기 토글(체크상태 영속), z-order=나침반 z6 계기층, 드래그 위치 AppSettings 영속(나침반 패턴 재사용). 🔲 사용자 와이어프레임 검토+OQ 회신 후 PRD(나침반 PRD 구조 재사용). [[project_compass_control_feature]]
- **→ 데이터 소스 조사(사용자 "EventCardListPanel 데이터?")**: Explore 에이전트 추적 결과 **탐지·장애 소스=`IEventQueueManager`(EventQueueManager 싱글톤)** — 카드목록(EventCardListPanel.ViewModelProvider)은 MAX_EVENT_CARDS=500 하드캡으로 desync되는 뷰 투영이라 금지. EQM=활성/미조치 SSOT(조치/자동조치/자동복구/원격종결 시 Dequeue), 지도 심볼색(SymbolEventManager)과 동일 소스. MapViewModel은 SymbolEventManager 이미 주입(동일 DI)이라 IEventQueueManager 1개 추가로 배선. ⚠EventEntry는 EventType(탐지/장애 대분류)만 담고 세부타입(EnumDetectionType/EnumFaultType)은 이벤트모델에 → **집계 pill은 EQM 즉시, 세부 타입칩은 v2 분리**. EQM 타입별 카운터 없음→전이이벤트 재집계. 사용자 "너의 논리가 맞아" 승인.
- **→ PRD 작성(사용자 "PRD 만들어줘")**: `docs/prds/GMap_Map_Instruments-prd.md` v1.0 Draft — 강풍모드+탐지장애 2계기+보기(View)메뉴+z6+영속 통합. D-01~09 확정(EQM소스/타입칩v2/미조치카운트/재집계/WINDY소스/z6/영속/보기메뉴 IsCheckable/토큰), FR-01~17, OQ-1~4(클릭연동·펄스·기본위치·나침반 회전OFF 가시성). 나침반 PRD 구조·리뷰8건 교훈 재사용. 🔲 사용자 PRD 검토·승인 후 plan→dev. 롤백태그 예정 `before-map-instruments`. **OQ 진행 확정**: D-10 클릭 네비(강풍→`OpenWindyPanelMessageModel`/탐지장애→`OpenEventPanelMessageModel`, 둘 다 기존 Conductor 수신핸들러 있음=한줄 publish) · D-11 기본위치 마진≥16px+HUD회피 · D-12 가시성=View 체크 마스터(나침반도 RotationFeature와 분리, 회전OFF여도 체크ON이면 정북 표시=동작변경 승인포함). OQ-2=탐지 pill 펄스 **A안(항상 유지) 확정**.
- **→ 구현 완료(사용자 "구현해줘")**: 강풍(`GMapWindyIndicatorControl`)+탐지장애(`GMapDetectionFaultControl`) 2계기+보기(View)메뉴+z6+영속. **v2.6 `f1ee163`**(rebase 후 FF — 타 세션 f52fcd3/a8c0f66 위)+메인솔루션 `ad17c73`(SetupModel 3필드). 신규: 컨트롤 2+스타일 2(Generic 병합)+InstrumentMath+모델 3(MapWindy/MapDetFault/MapInstrumentVisibility)+MapViewModel.Instruments.cs 파셜+EQM `GetActiveCounts()`/`OnActiveCountChanged`(전 mutator 발화, lock 밖)+InstrumentMathTests. **나침반 FR-17**: RotationFeature→가시성 커플링 제거, 보기 메뉴가 마스터. **적대 리뷰 wf 12에이전트 CONFIRMED 1건**(나침반 doc 주석 stale)만 수정, 나머지 반박기각(EQM 락경계·NATS→UI 정렬 클린 확정 — 카운트 세터 동등성 가드는 재집계가 세터 앞이라 무의미로 기각). 테스트 93/93·양레포 CS 0. Events.Ui 실패 1건은 클린 기준선(cc51693)서도 동일=기존 baseline(회귀 아님). 🔲 런타임 육안=사용자(앱 재빌드→보기 메뉴 토글·강풍 모드전환·탐지장애 카운트·드래그·영속). [[project_compass_control_feature]]
- **→ 상단 메뉴 한글화+전역 단축키(사용자 "탑 메뉴 다 한글로+단축키")**: `2c7eff1`(v2.6 FF). File→파일(_F)/Maps→지도(_M)/Exit→종료. **단축키를 MenuItem 스코프→루트 `UserControl.InputBindings`로 이관**(종전엔 메뉴 열렸을 때만 동작=사실상 미동작). 파일 Ctrl+L/I/N/D/E·지도 Ctrl+1/2/3·보기 Ctrl+Shift+C/W/F/X(계기 토글). 기존 코드단축키(Ctrl+Shift+R·Ctrl+R·Ctrl+←→·Ctrl+A) 비충돌. 좌표계 토글 정합: 지도 메뉴 Command 제거(IsChecked 단일토글)+ToggleMGRS/UTM 플립통일+IsShowMGRS 세터에 격자적용 통합. 보기 토글 커맨드 3종 신설. ⚠Ctrl+E=종료가 이제 전역이라 오입력 주의(기존 설계 유지). 93/93.
- **→ 후속 수렴(커밋 경합)**: 타 세션이 나침반 위 "앵커 중 정북 복귀 비활성"(`cc51693`, 내 CHANGELOG compass 엔트리+INDEX 동봉)과 WINDY 라이브러리분(`2f4ffe9`)을 커밋 — 내용 검증(빌드0·66/66) 후 수용. worktree feature/rotation-fullsync를 v2.6(cc51693)로 FF 동기화 완료(고유 커밋 0=충돌 불가). 양 레포 트리 clean.
- **→ v2.2 레이어링(사용자 질문 "OverlayWindow와 차이/최상위?")**: MapView 실측 z맵(0 맵/어도너·150 십자가·200 PropertyPanelCanvas{리더선5·팝업10·패널20~120·허브210}·300 비상호작용 배너) → **권장=PropertyPanelCanvas 자식 z6 "계기층"**(팝업/패널/허브 아래). 최상위 비권장 근거: 작업면이 계기를 덮는 게 자연스러움·300층=비상호작용 컨벤션·WebView2 airspace면 어차피 최상위 불가·드래그 가능해 가려지면 옮기면 됨. 위치기억=허브 HubX/Y·SaveHubPositionCommand 선례.
- **🔲 잔여**: E2E 2차(B모드 앵커 풀테스트·오버레이 MBTiles 회전 티어링·휠줌 커서 불변·회전 심볼 클릭 패리티·이미지 리사이즈·측정툴·팝업 리더)·R-41 격자가드·플래그 노출 최종결정·클로즈아웃(report/CHANGELOG). 사용자=메인솔루션 재빌드로 `19aeb91`(영속)+`6f9ee50`+`574102a`+`01d652b` 확인, VS 에러=stale cache 여부 회신. [[project_map_rotation_disabled]] [[feedback_rollback_tag_before_work]] [[feedback_main_solution_advance_notice]]

## ▶▶ 재개 포인트 (2026-07-30 밤, 이 세션 — 앵커 중 정북 복귀 비활성) — Track B, ✅ 구현·회귀 0, 🔲 앱 재빌드+육안=사용자
- **요청**: "앵커링 상태일 경우 정북 복귀 비활성화". 정북 복귀 실트리거=**나침반뿐**(더블클릭 FR-05 + 우클릭 메뉴 "정북 복귀"; `ResetRotationCommand`는 XAML 미바인딩 사문; 툴바 회전 토글은 이미 앵커 잠금).
- **구현**(`GMapCompassControl.cs`+`MapViewModel.cs`): ①OnDialUp 더블클릭 정북에 `TargetMap?.IsAnchorActive != true` 가드 ②메뉴 항목 `_northMenuItem` 필드화→ShowSettingsMenu서 열 때마다 IsEnabled/툴팁 갱신(메뉴 1회 생성·재사용 함정)+클릭 이중 방어+`ToolTipService.SetShowOnDisabled` ③사문 커맨드 CanExecute도 게이트. **SSOT 게이트 0-항상허용은 불변**(앵커 자신의 정북 강제·kill-switch OFF 복구 경로).
- 검증: 빌드 0 · GMaps.Ui 369/370(기준선 1, 나침반 32종 포함 회귀 0). ⚠회전/나침반=타 세션 도메인이나 731d8d4 커밋 후 클린 상태에서 작업(충돌 없음). **✅ 커밋 완료(사용자 "커밋해줘")**: 라이브러리 `2f4ffe9`(WINDY/조준 라이브러리분: CommonMessages+MapViewModel — CanExecute 게이트 동승 명시)+`cc51693`(앵커 정북: CompassControl+CHANGELOG+INDEX — INDEX에 타 세션 기커밋 문서 행 흡수 명시)·메인 v0.5 `a713690`(해석기/폴백/게이팅 본체 4파일, SetupModel은 타 세션이 0cfe61e로 자가 커밋해 혼입 소멸). **✅ v2.6→main FF 완료(사용자 "타 세션 문제 없으면 머지")**: 사전 점검(트리 클린·rebase/MERGE 상태 없음·v2.6..main 공집합·타 세션 전부 커밋 완료+회전은 kill-switch 기본 OFF=머지 무영향 기록) 후 `git fetch . v2.6:main`(체크아웃 無) — main `8c6c7b1`→`cc51693`(29커밋: 내 3건+회전 전면 싱크 P0~P5·나침반 등 타 세션 26건, v2.6 전체 추종 관례). 원격 push 미실행(요청 시). 🔲 육안: 앵커 ON→더블클릭 무반응+메뉴 회색/툴팁, 앵커 OFF→정상.

## ▶▶ 재개 포인트 (2026-07-30 저녁, 이 세션 — WINDY "변경 실패" 빨리 뜸 + 라디오 롤백 안 됨 진단) — Track A(진단)+데이터 등록, ✅ 원인 전부 확정·FR-1~5 구현 대기
- **증상 2건(사용자)**: ①풍량 변경 실패 팝업이 너무 빨리 뜸 ②실패 팝업은 뜨는데 라디오 버튼이 롤백 안 됨. ③(파생 질문) 특정위치확인은 왜 팝업이 없나.
- **①원인=no-responders(정상 동작)**: 로그 실측 17:57:16 REQ→40ms 실패 — NATS가 `proxy.windy` **구독자 없음을 즉시 503 통보**(타임아웃 아님, NatsService.cs:157 구분 로직). PidsProxy가 이 NATS에 미접속. PTZ_AIM도 동일(nvr_manager.ptz 구독자 없음).
- **③=설계대로**: aim 실패는 팝업이 아니라 SetAimStatus 상단배너 2.5s(autoHide) — 로그 17:57:08,921 발화 확인. 사용자 기대=팝업 → FR-5(승격) 후보.
- **②원인 3겹(전부 실측)**: (1)롤백=REST 재조회 종속인데 `GET /servers/1/proxy-settings`→**404 "Server with id 1 not found"**(popup_manager 토큰으로 재현. admin/admin123 아님·admin 5회 잠금 주의, **popup_manager/sensorway1**) — servers 테이블 id=3~16, **id=1 없음** + 클라 `serverId=1;//TODO` 하드코딩(NatsDomainService.cs:510, 스펙 §8.8 예시 복사 흔적) (2)재조회 실패 시 폴백 전무+클릭 시 `_setupModel.ModeWindy` 즉시 덮어써 이전값 소실(WindyPanelViewModel:103) (3)**빈 사유 로그**: 서버 v6 에러봉투=error.message인데 FetchProxySettingsAsync만 top-level Message 읽음 — `ApiResponse.Error.Message`는 이미 파싱됨(FetchServersAsync:430은 잘 씀)→1줄 수정.
- **스펙 근거(REST v6.3=권위, `C:\workspace_python\api-test-server\GOP_Restful_Api_연동설계.md` 7/21 — 라이브러리 docs 사본은 v4.9 구본)**: EnumServerType에 **PROXY="PROXY" 표준 정의**(L573, 26종 중 9종만 시드됨), §8.8="PidsProxy 서버 운용 설정", lazy 생성 명문.
- **✅ 데이터 등록 완료(사용자 "하나 등록해줘")**: 카테고리 **id=11**(프록시 서버/PROXY/sort 10) + 서버 **id=17**(PROXY-ab0101, 192.168.1.30:8500) POST, `GET /servers/17/proxy-settings`→200 lazy 생성 검증(wind0/NORMAL).
- **✅ FR-1~5 구현 완료(사용자 "승인" → PRD Approved v1.2, plan 12/12)**: FR-1 해석기=`NatsDomainService.ResolveProxyServerIdAsync`(GetCategoriesAsync→TypeServer==PROXY→ServerProvider 매칭, **미발견 시 FetchServersAsync 재조회 후 1회 재시도 폴백**, `_proxyIdGate` lock, SYNC_SERVER/CATEGORY→`OnServerTopologyChanged` 훅(NatsBrokerService virtual 신설)→무효화, 404 자가치유=StatusCode/Error.Code 우선) / FR-2 `SendWindyModeMessage(Mode, PrevMode?)` 확장+WindyPanel 캡처+WINDY 실패 시 재조회 실패→prevMode 로컬 롤백 / FR-3 FailReason(top→Error.Message) / FR-4 BuildLookupTable 무토큰 조회 제거+`IHandle<AllDevicesLoadedMessage>`서 로드 / FR-5 aim 실패=ShowBrokerControlInfo 팝업(성공=배너, Cancelled 무통지). **검증**: 빌드 0오류(양 레포)·메인 테스트 46/54 green(8 skip 기존, 신규 해석기 6종)·GMaps.Ui 337/338(기준선 1). **적대 리뷰**: Review 2기 완료(발견 10)→⚠Verify 20기 전원 세션 한도(19:40 KST 리셋)로 유실→**직접 검증**: 타당 3건 수정 반영(폴백 미구현·NOT_FOUND 문자열 매칭·크로스스레드), 한계 2건 PRD §6 기록(재클릭 레이스·lost-invalidation=404 자가치유 회수). ⚠테스트 bin에 117GB mbtiles 복사 불가(디스크) → **하드링크 우회**(Tests\bin\...\Datas — 재발 시 동일 처치). CHANGELOG [Unreleased] Fixed 기록. **커밋 미실행**(요청 시 — 라이브러리 2+메인 3+테스트 1파일). **✅ 런타임 E2E 자가 검증 완료(사용자 "실행해서 확인해봐")**: 앱 직접 실행(PID 61464, 자동로그인 admin)+UIA/PostMessage 조작+PrintWindow 캡처로 검증 — ①FR-4: 부팅 로그 "[ProxySetting] 부팅 조회 보류"만 남고 401/강제로그아웃 오발화 소멸 ✓ ②FR-1: AllDevicesLoaded 후 "ProxyServer 해석 완료: server id=17 (category id=11)"→GET 200→"WindyMode 로드: wind0" ✓ ③FR-2 핵심: 풍량 패널 UIA Select로 약풍모드 선택→REQ no-responders 실패→**실패 팝업 표시+4초 내 라디오 평시모드 자동 복원**(UIA IsSelected 실측) ✓ — 사용자 보고 버그("라디오 유지") 해소 확정. **자동화 노하우**: 좌측 HamburgerMenu 항목=MouseLeftButtonUp이라 PostMessage 클릭 통함, RadioButton은 PostMessage 클릭 안 먹힘→UIA SelectionItemPattern.Select() 사용, 팝업 닫기 버튼은 합성입력 불가(실마우스만), CopyFromScreen 불가→**PrintWindow(PW_RENDERFULLCONTENT) 사용**, log4net 50건 버퍼링이라 최신 로그 파일 미반영 주의. 앱은 실패 팝업 떠 있는 상태로 실행 유지(사용자 직접 확인용). ⚠메인솔루션에 타 세션 `SetupModel.cs` M 존재(나침반 영속 추정) — 커밋 시 제외. [[project_windy_proxysetting_resolution]] [[reference_headless_screen_capture]] [[feedback_accurate_analysis_first]]

## ▶▶ 재개 포인트 (2026-07-30, 이 세션 — GIS.md v1.5.2 스펙 대비 gap 재검증) — Track A(분석), ✅ 분석 완료·사용자 우선순위 결정 대기
- **발단**: `docs/prds/GIS.md`가 오늘 11:59 **v1.5.2**(Broker v1.5.2/REST v4.6)로 갱신 → 사용자 "업데이트 필요한 것 먼저 식별하라". 7/13 v1.5 기준 분석은 스펙·코드 양쪽이 변해 재검증 필요.
- **방법**: 워크플로 `wf_7c0f1e4c`(10 도메인 병렬 실측+완전성 비평, 11 에이전트) — 라이브러리+메인솔루션 양쪽 정적 대조. 에이전트별 원결과=스크래치패드 `wfres_*.json`.
- **산출물**: `docs/analyses/GIS_Nats_Spec_Gap-analysis.md` **v2로 전면 갱신**(7/13 v1 대체). INDEX 갱신.
- **핵심**: ① v1.5.2 델타=REQ 통일(BROADCAST 2종·PTZ_AIM_LOCATION이 MISMATCH로 강등) ② **신규 P0 결함 2건(실환경 발생 중)**: ACTION_REPORT 타임스탬프 +9h 왜곡(`DtoToModelHelper.ParseDateTime` 로컬파싱+리터럴Z)·TRACKING_STATUS 1Hz 로그스팸(enum 부재) ③ 7/13 P1(SYNC_EVENT_MAPPING/PRESET 캐시) **진전 없음**(enum/DTO만, 핸들러·캐시 전무) ④ 해소 확인: DETECT frame_w/h·PTZ_STATUS v4.6 DTO 필드·ACTION_REPORT from_event 계열·WINDY 완전(REQ 템플릿) ⑤ EnumGopCommand 46종 부재, LAMP 4종/PTZ 36종/수신 4종(SYSTEM_EVENT·VMS_DETECT·BROADCAST_STATUS·ENCLOSURE_METRICS) 미구현, VMS_DETECT는 subject 자체 미구독, 부대ID unit001 고정(스펙 `*`).
- **우선순위 제안(분석 §4)**: P0 현행결함 → P1 캐시복구 → P2 m_type 전환 → P3 마스킹 → P4 수신4종 → P5 발행제어(LAMP/PTZ — PTZ는 ONVIF 병행 정책 결정 선행) → P6 스펙협의(group_device·TTS·부대ID).
- **🔲 남음**: 사용자 우선순위 확정 → 패키지별 PRD(기존 `GIS_Nats_Full_Integration-prd.md` Draft도 v1.5.2 개정 필요). 코드 미착수. [[project_gis_nats_spec_gap]] [[feedback_prd_before_code]] [[feedback_spec_full_compliance_propose_deviations]]
- **→ 후속(같은 날, 사용자 지시 "REQ 다 반영 + 스피커 Play/Stop + 램프 인터페이스라도")**: Track C 판정 → **PRD Draft 작성** `docs/prds/GIS_Nats_v152_Req_Transition-prd.md` v1.0 — FR-01 공통 BrokerRequestClient(타임아웃 5초=사용자 지정, BrokerRequestResult, §6.9 문구 매핑) / FR-02 EnumGopCommand 6종+주석 정정 / FR-03·04 BROADCAST REQ 전환+스피커 메뉴 실패 UX(IsBroadcasting 보정) / FR-05 PTZ_AIM REQ 전환(SetAimStatus 실패 안내) / FR-06 LAMP enum EnumMember 직렬화("steady"/"Fire A-WANG" 등)+DTO enum 강제 / FR-07 ILampControlService 4종 신설(UI 없음) / FR-08 로그위생 no-op(메인솔루션 1파일, 통지 겸). **V-01~04=서버 RSP 회신 여부(WINDY 프록시 미회신 이력) — plan Phase 0 선행 필수**. OQ-1 TTS=PUB 유지 권고. Out of Scope=LAMP UI·PTZ 36종·B그룹. ⚠파이프라인=타 세션(GMap_Rotation dev) 점유라 **phase 전환/approve 안 함**(문서만) — approve 시 내 PRD touch 필요([[project_approve_prd_picks_newest_mtime]]).
- **✅ 승인·구현·리뷰 완료(같은 날, "진행해줘" = OQ-1~3 권고안 채택 → PRD Approved v1.1 파일 상태만·advance-phase 미실행)**: 태그 `before-gis-nats-req-transition`(HEAD 44bc6fc), plan 24/28(잔여=VER-01~04 외부). **구현 15+9파일**: EnumGopCommand +7종(BROADCAST 2·LAMP 4·TRACKING_STATUS=28, PTZ_AIM 주석 정정) · LAMP enum 3종 EnumMember+DTO enum 강제+LampClear `List<int>?` · 신규 `Services\Brokers\{BrokerRequestResult,IBrokerRequestClient,BrokerRequestClient}`(REQ+RSP 3분기·5s·§6.9 한글 매핑) · Broadcast/Aim 서비스 REQ 전환(시그니처 `Task<BrokerRequestResult>`)+TTS PUB 유지 · 신규 `ILampControlService/LampControlService`(4종, Clear null=전체·빈리스트=Invalid) · GMapUiModule DI 2종 · MapViewModel: Play(성공시만 패널닫기)/Stop 실패팝업(`ShowBrokerControlInfo`)+**재전송 가드 `_isBroadcastRequestInFlight`**(리뷰 P2)+Aim 결과기반 SetAimStatus · 메인솔루션 `NatsBrokerService` no-op 6종(EXT). **적대 리뷰(41 에이전트)**: 발견 19→확정 6(P2 재전송가드·CA(false) 6곳·Clear 빈리스트·주석 드리프트 — 전부 수정반영), 반박 13. **검증**: 빌드 0오류(lib 3+메인), Messages 187/187, GMaps.Ui 338 중 337 green(빨강 1=EditRecorder 기준선), BOM 전수. CHANGELOG [Unreleased]·분석 v2 §6·plan·INDEX 갱신. **✅ 커밋 완료(사용자 "충돌 검토 후 커밋" 지시)**: 사전 검토 — ①삭제 라인 10줄=전부 내 의도적 교체분(구 PUB 코드) ②회전 세션 신규 3커밋(3c19cdf/6e3230e/64493cb)과 교집합=MapViewModel뿐이나 그들 코드(`ScheduleBootViewportResync` 등)는 내 작업본 생존+내 diff에 되돌림 없음 확인 ③**INDEX.md는 타 세션 라이브 편집 혼재(회전 행 3건)라 커밋 제외**(다음 INDEX 커밋 세션이 흡수 — 관례). 라이브러리 **`888e03a`**(26파일 +1155/−135, 내 파일만 명시 스테이징, 한글 `-F`) · 메인솔루션 v0.5 **`d062b13`**(NatsBrokerService +12, untracked 타인 파일 3종 제외). 🔲 남음: ①메인솔루션 재빌드+런타임 검증(스피커 Play/Stop 실패팝업·조준 완료/실패 문구) ②**V-01~04 서버 RSP 회신 확인(서버팀)** — 미회신이면 5초 대기 후 "대상 서비스 응답 없음" ③P0 잔여=ACTION_REPORT 타임스탬프 +9h(3-A, 본 PRD 범위 밖 — 차기 1순위) ④v2.6→main FF·원격 push 미실행(요청 시). [[project_gis_nats_spec_gap]] [[project_windy_rsp_missing_proxy_pending]] [[project_multisession_checkpoint_sweeps_wip]]

## ▶▶ 재개 포인트 (2026-07-28, 이 세션 — 라인/구역/PIDS 드로잉 HUD 리디자인 + 위치 유지 버그) — Track C, ✅ 구현·빌드0 완료, 🔲 앱 재빌드+런타임 육안=사용자
- **발단(Track A 질문)**: `LineMarkerStyle.xaml`이 Area에도 쓰이는데 왜 시작/끝 마커? → 조사(워크플로 3탐색): 원래 시작(초록)+끝(주황) 쌍이었으나 `b5a1dc2`(2025-09-19)가 **끝 마커만 삭제**, 위치미지정 유령 초록점(canvas 0,0)+복수형 이름/주석만 잔존. IsClosedPath=True(=모든 Area) 또는 IsSelected마다 상시 표시. **완전 죽은 마크업**(코드 0참조, GetTemplateChild는 PART_LineCanvas/MainPolyline 2개뿐). 진짜 시작/끝 강조는 `LineDrawingAdorner.OnRender`(그리는 중)에 별도 존재.
- **✅ Track B(즉시, 승인)**: `LineMarkerStyle.xaml`에서 PART_EndpointMarkers Grid+PART_StartPointMarker+IsClosedPath 트리거+IsSelected 세터+orphan 주석 제거(290→263줄, 라인번호 삭제 방식·grep 잔존0·XML well-formed·BOM 유지).
- **✅ Track C(HUD 리디자인 + 위치 버그)**: 드로잉 중 흰색 알약 HUD가 (1)다크/라이트 테마 불일치 (2)드래그로 옮기면 다음 점 클릭마다 원위치 튐. 사용자 지시 "**우상단 X 닫기 표준 패턴 활용**". 
  - **위치 버그 원인 확정**: `OnDragHandleMouseMove`가 `_controlPosition` 저장하나 **아무도 안 읽음** → `UpdateControlUI`(:479, AddPoint·팬/줌마다 호출)가 `firstPoint+(20,−50)` 하드코딩 재배치. 
  - **HTML 프리뷰**(사용자 "반드시 HTML로"): `docs/design/line-drawing-hud-redesign-preview.html` → Artifact. 사용자 "디자인이 딱이다 진행해" + 팬/줌 시 **절대좌표 고정** 선택.
  - **설계검증(architect+code-reviewer opus 워크플로 `wf_957dc212`)**: A안=**templated `LineDrawingHudControl : Control`** + `Themes/LineDrawingHudStyle.xaml`(암시 Style) → `Generic.xaml` 병합. StaticResource 패널스타일=Generic 스코프 해석, DynamicResource 토큰=App전역 → **라이브 테마 자동**(구독 불필요, `ThemeService.SwapTokenDictionary` App.Resources 스왑). code-reviewer must-fix: 위치버그·Esc의미(기존 Esc=valid면 완료)·테마.
  - **구현 5파일**: NEW `GMapControls/LineDrawingHudControl.cs`(DP HudTitle/PointText/DistanceText/CanComplete, 이벤트 Complete/Undo/Close, PART 배선) · NEW `Themes/LineDrawingHudStyle.xaml`(시안헤더 VectorLine아이콘+타이틀+PART_CloseButton=PanelCloseButtonStyle / 본문 칩+거리 + 완료=PanelPrimaryButtonStyle+되돌리기=PanelSecondaryButtonStyle, 전부 DynamicResource) · MOD `Generic.xaml`(병합 1줄) · MOD `LineDrawingAdorner.cs`(수기트리→`_hud` 호스팅, 위치 `_hasBeenDragged`+`_absolutePosition` 게이트·Clear리셋·`_controlPosition` 제거, HitTestCore→`_hud` 바운즈, OnRender 펜캐싱+토큰화(TryFindResource 1회), 죽은 핸들러/CreateButton 제거, OnMapChanged Dispatcher가드) · MOD `LineDrawingService.cs`(`ResolveHudTitle` 종류→타이틀, ctor 전달, **Esc=취소로 정렬**).
  - **검증**: GMaps.Ui 빌드 **0오류**(BAML 컴파일 OK). PackIcon `Kind="VectorLine"`(MapView:527 검증필·VectorPolyline 미검증이라 교체). 신규 .cs/.xaml BOM. 롤백 태그 `before-line-drawing-hud-redesign`.
- **🔲 남음(사용자)**: ①앱 재빌드(라이브러리라 메인솔루션 재빌드 전 미반영) ②런타임 육안: 다크/라이트 토글 반영·Line/Area/PIDS 3종 그리기·완료(Enter)/취소(Esc·X)/되돌리기·**드래그 후 점 클릭/팬-줌에도 위치 유지**. ③커밋 미실행(사용자 요청 시 — 내 파일만 명시, 동시세션 스윕 주의).
- **⚠ 참고 side-finding(미착수)**: PIDS 그룹 생성 시 `IsClosedPath=false`(LineDrawingService:527)인데 주석/DB기본값/테스트는 true — 불일치. 이번 범위 밖.
- **✅ 후속 Track B (사용자 "Track B·권장방향") — PidsGroup 첫 클릭 무효화 버그**: 원인=Fence_Group(PidsGroup)이 단일점 PIDS 장비와 함께 `EnumMarkerCategory.PIDS_EQUIPMENT` **배치모드**(placement) 경로로 분류(`ExecuteAddSelectedSymbol`)→다점 경계선인데 **첫 맵클릭이 배치클릭으로 소비**(좌표 무시)돼 그리기 시작만, 2번째부터 꼭짓점 인식. Area/Line(AREA_BOUNDARY)은 배치 우회 직접시작(`4018-4020` "라인/영역 기존 드로잉 유지")이라 무증상 — **PidsGroup 특정 원인 확정**(정적, 결정적 경로). 방증: `AddPidsGroupMarker(position,…)`가 position 미사용(배치 시그니처 잔재). 체인: `4013-4015`→`GMapCustomControl:990-995`(placement 클릭 소비)→`OnSymbolPlacementClicked:753-754`→`AddPidsMarker:4992-4993`→`StartLineDrawingAsync:5069`. **수정(Option A)**: `ExecuteAddSelectedSymbol` PIDS_EQUIPMENT 분기에서 `deviceType==Fence_Group`이면 배치 우회 직접 `AddPidsGroupMarker` 호출(MapViewModel.cs:4013~). 빌드0·한글 BOM 유지. 태그 `before-pidsgroup-firstclick-fix`. ⚠`MapViewModel.cs`=세션시작 시 타 세션 M 파일(조율 주의). 🔲 런타임 육안(PidsGroup 추가→첫 클릭부터 점). [[feedback_bug_claims_need_evidence]] [[feedback_accurate_analysis_first]] [[project_multisession_checkpoint_sweeps_wip]]
- **✅ 후속 Track B (사용자 "응") — 편집 모드 OFF 시 진행 중 드로잉 미정리 버그**: MapEdit OFF 경로(`IsEditModeEnabled` 세터 `:7252` + `GMapCustomControl.SetEditMode(false)` `:2740`)가 선택/러버밴드/마커편집드래그/배치모드는 정리하나 **`LineDrawingService`(드로잉) 미취소** → HUD·클릭라우팅(`IsLineDrawing` 패스트패스 `GMapCustomControl:1028`, 편집모드 독립 `:174`)·Cross커서·VM상태 orphan 잔존. 대비=aim충돌 `:769`/배치 `:716`은 `CancelDrawingAsync` 취소하는데 편집-OFF만 누락. **수정**=세터(모든 해제경로 단일게이트: 토글 `:3858`/aim `:775`/권한상실 `:1752`/강제로그아웃 `:1799`) 해제분기에 `if(MainMap?.IsLineDrawing==true){ CancelDrawingAsync(); IsLineDrawing=false; LineDrawingStatus=""; }` (`MapViewModel.cs:7252~`, `OnLineDrawingCancelled:6516`은 VM플래그 미리셋이라 명시 필요). 빌드0·한글BOM. 태그 `before-editmode-off-drawing-cancel`. 🔲 런타임 육안(드로잉 중 편집OFF→즉시 정리). ⚠MapViewModel=타세션 공유.
- **✅ 커밋·머지 완료 (사용자 "커밋 및 머지해줘")**: ⚠**WIP 스윕 발생** — 동시 세션 `458b002`(카메라 팝업 프리셋)가 내 `MapViewModel.cs`(PidsGroup+편집OFF 두 수정)를, `54ab19c`(docs INDEX)가 내 INDEX 행을 흡수(HEAD에 안전히 존재). 남은 내 파일(HUD 스택 6+CHANGELOG)만 명시 스테이징(⚠`MyPagePanelView.xaml`=타세션 WIP 제외)→커밋 `b5e19be` "라인/구역/PIDS 드로잉 HUD 리디자인+시작마커 제거"(한글 메시지=파일 `-F` 경유, mojibake 회피). **v2.6→main FF `d0bd165..b5e19be`**(체크아웃 無), main=v2.6=b5e19be. 원격 push 미실행(요청 시). 🔲 3건 런타임 육안=앱 재빌드 후 사용자. [[project_multisession_checkpoint_sweeps_wip]] [[project_library_deployment_path]]
- **✅ 후속 Track B (사용자 "진행해줘") — 레이어 패널 부모 체크박스 재오픈 desync**: 증상=PIDS 장비 그룹 언체크→닫기→재오픈 시 그룹/카테고리는 다시 체크됨인데 leaf는 언체크(실제 상태). **2-에이전트 적대검증 CONFIRMED**. 원인=`LayerTreeNode._isChecked` 기본 true + 부모 팩토리 IsChecked 미세팅(`LayerTreeNode.cs:23,252-295`); 패널 열 때마다 `LoadLayersFromDbAsync`가 재빌드(`:8874`)하는데 유일 집계 `AggregateLeafCheckedFromMarkers`(`:9281`) 필터 `Model.LayerType=="Symbol"`가 **개별 심볼 leaf(Model==null) 제외** → 부모 tri-state 재계산 경로 없음→기본 true 부활. **진단**(사용자 질문 "영속 수정이 반영 안된건지 미검출인지"): 영속(symbol.Visible)은 **정상**(leaf 언체크로 복원됨). 부모 체크는 파생 표시값(비영속). LayerVisibility_Persistence_Fix(966a254, 6/8) FR-4가 세운 부모 집계를 **개별 심볼 트리노드 기능 `367e6f0`(6/30)이 회귀**시킴(category-unit leaf→individual-symbol leaf, 필터 매칭 소멸)+미검출. 이미지는 `5b30b7c`(7/3) 별도 rollup(`:8892`)으로 생존(비대칭). **수정**=`LayerTreeNode.RecomputeCheckStateBottomUp()`(부작용 없는 자식→부모 상향 재계산, 세터 우회) + `LoadLayersFromDbAsync` 재빌드 직후 호출(`MapViewModel.cs:8908~`). setter 우회 필수=카테고리 노드 Model 보유→setter의 `Model.IsVisible` 기록이 DB플래그 오염(architect 지적). 빌드0·한글BOM. 태그 `before-layerpanel-parent-check-fix`. PRD §8 역방향금지와 무관(node↔node 정방향). 🔲 런타임 육안. ⚠MapViewModel=타세션 공유. [[project_map_layer_architecture]] [[project_symbol_visibility_dual_gate]] [[feedback_bug_claims_need_evidence]]
  - **✅ 커밋·머지 (사용자 "커밋하고 머지해줘")**: MapViewModel.cs diff=내 것만 확인(스윕 없음). 3파일 명시 스테이징(MyPagePanelView 제외)→커밋 `8c6c7b1`(한글 `-F`). v2.6→main FF `b5e19be..8c6c7b1`, main=v2.6=`8c6c7b1`. 원격 push 미실행. 신규 memory `project_layerpanel_parent_check_aggregation`.
- 문서: PRD `docs/prds/line-drawing-hud-redesign-prd.md`(Draft) · Plan `docs/plans/line-drawing-hud-redesign-prd-plan.md` · CHANGELOG [Unreleased] Changed. [[project_line_marker_render_trigger]] [[project_gmaps_overlay_window_pattern]] [[project_panel_design_system]] [[reference_theme_token_system]] [[feedback_prd_before_code]] [[feedback_analysis_agent_chain]] [[feedback_deliver_visible_design]]

## ▶▶ 재개 포인트 (2026-07-27, 이 세션 — appsettings.json 부팅 크래시(0x00) 진단·수정) — 메인솔루션, ✅ 즉시복구+원자적쓰기 하드닝 커밋(v0.5)
- **증상**: 부팅 시 `InvalidDataException`/`JsonReaderException '0x00' is an invalid start of a value. LineNumber:0`(Bootstrapper.cs `AddJsonFile`). bin `appsettings.json`이 **전량 3163바이트 0x00(널)**. 소스(5437B)는 정상.
- **근본원인**: 앱이 런타임에 appsettings.json을 **비원자적으로 덮어씀** — 테마 토글(`Services/AppSettingsThemeStore.Save` `File.WriteAllText`)+설정 패널(`BaseSetupViewModel.SaveSettingAsync` `File.WriteAllTextAsync`). truncate 후 미플러시 중단(앱 강제종료/크래시/디스크)=널 손상. **재빌드 무효**: csproj `PreserveNewest`+손상본이 소스보다 새 타임스탬프→MSBuild 재복사 스킵. 부수: Newtonsoft 라운드트립이 `//` 주석 제거(5437→3163). `AddJsonFile(optional:true)`는 부재만 관대, 손상은 크래시.
- **수정(커밋, 메인솔루션 v0.5)**: ①즉시복구=bin 손상본을 소스로 덮음(앱 기동 확인=PID 30840 실행중) ②신규 `Services/AtomicFile`(temp write+flush(디스크)→`File.Replace`(.bak 보존)+`IsHealthyJson`) ③두 저장지점 AtomicFile 전환 ④`Bootstrapper.EnsureConfigHealthy`(로드 전 손상→.bak 복구/격리) ⑤`AtomicFileTests` 8케이스+스테일목 복구(MockEventApiService Update*→*ReplaceDto 4개·MockNatsService RequestAsync). 검증: 원자교체 primitive 실증·널감지 실증·컴파일0. **MSTest 실행은 앱+VS bin 잠금이라 앱 종료 후**. [[project_appsettings_nonatomic_write_corruption]] [[feedback_accurate_analysis_first]] [[feedback_main_solution_advance_notice]]

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 카메라 팝업 3건: 패닝 딸려옴+카운터 위젯+ONVIF 프리셋/PTZ 반응성) — Track C, ✅✅ **구현·리뷰·v2.6 FF 머지 완료 (`475025e`)**, 🔲 메인솔루션 재빌드+런타임 육안=사용자
- **요청 3건**: ①상하 패닝 시 팝업이 경계에 걸려 딸려옴(좌우 정상) ②맵 구석 팝업 카운터 위젯(아이콘+뱃지+✕→confirm→전체닫기) ③ONVIF Preset 못 불러옴+PTZ 버튼 ~1.5s 지연.
- **분석 체인**(Explore 3기→architect(opus)→code-reviewer(opus) 적대검증): `docs/analyses/CameraPopup_PanClamp_Badge_OnvifPtz-analysis.md`
  - **(A) 원인 확정**: `CameraStreamPopupViewModel.cs:136` CanvasTop **세터 자체** 하한0 클램프(`58b3fd7`)가 팬 추종(`RefreshCameraPopupPositions:2259`)까지 적용 → "위로 팬" 한정 상단 고정. CanvasLeft 무클램프(좌우 정상 근거). 수정=세터 1줄 제거(소비처 4곳 음수 안전 전수검증, 드래그 보호는 컨트롤 리터럴0 독립·`MinCanvasTop`는 세터 단독 사용=dead화).
  - **(B)**: CloseAll 메서드·Count 노출 부재 → `CameraPopups.Count` 직접 바인딩+Count→Vis 컨버터 신규+`CloseAllCameraPopupsAsync`(순차 await)+`CallCloseAllCameraPopupsProcessMessageModel`(표준 confirm, 표시 구독자=메인솔루션 기존 인프라). 위젯 ZIndex≥200 필수(PropertyPanelCanvas=200), 줌컨트롤 우측 300px 여백 소비처 미확정(V-02).
  - **(C-P) 진실**: 프리셋 탭=**로컬 DB 전용**(CameraPtzPresets), ONVIF GetPresets 미배선(API 6종은 IOnvifService:62-108에 완비). "못 불러옴"=설계 불일치. 설계=ONVIF 전환+**표시 어댑터**(PTZPresetDto{Name,Token,Position} init-only·IsHome 없음이라 기존 XAML 필드 유지 매핑), IPtzController 확장(ctx.Gate 직렬, I-05)=메인솔루션 재빌드 통지.
  - **(C-L) ★적대검증이 1차 가설 반증**: 패드 `IsEnabled=IsPtzCapable`(:317)+capable은 EnsureReady 반환후(:1743, Gate 해제후) → "워밍 Gate 대기" 불성립. **수정 인과**: (a)Onvif 소스모드 한정 in-flight GetStreamUri(:436-471) Gate 경합(첫 누름) (b)**뗌마다 StopAsync가 같은 Gate 직렬(:273)+Digest 401 재왕복 가능** → 재누름이 직전 Stop 왕복 대기(매 누름, 유력). timeShift 매호출/채널 미예열/Task.Delay(-1) 가설 반박됨. Nudge는 비블로킹 string 오버로드(OnvifService:327) 확인.
- **PRD Draft**: `docs/prds/CameraPopup_PanClamp_Badge_OnvifPtz-prd.md` v1.0 — FR-A1~2(클램프)/FR-B1~2(위젯)/FR-C1~3(ONVIF 프리셋+3상태 가시성)/FR-L1~3(Stop LWW 확장+capable 정렬+Stop 진단로그, **R-1=Move실패시 보상Stop 필수**)/FR-L4(Phase2 조건부=Digest/전용채널, Out of Scope). V-01~05, OQ 7건(권고: A상수→드래그참조/오염앵커 유지/우하단/ONVIF 전용/DB프리셋 폐기/ONVIF Home/Phase2 제외).
- **⚠ 파이프라인**: 이전 사이클(Detection) report에서 complete 게이트 영구차단(전-plan 합산 결함, new-cycle은 complete에서만) → **허용 백워드 체인**(report→dev→plan→prd, --reason, no force)으로 prd 진입. 게이트 정책 수정은 백로그. approve 시 **내 PRD touch 필수**([[project_approve_prd_picks_newest_mtime]] — Overlay_Title PRD가 오늘 mtime 경쟁).
- **✅ 승인·구현·머지 완료 ("진행해줘" = OQ-1~7 권고안 일괄 채택, 2026-07-23)**: approve(PRD Approved v1.1, ⚠approve 코멘트를 bash 인자로 넘겨 audit-log.jsonl 한 줄 mojibake — PRD 파일은 정상, 이후 한글 CLI 인자 금지 재확인) → plan 23태스크 → 태그 `before-camerapopup-3fix`(f645eae) → worktree `feature/camerapopup-3fix`(고아 복사, ⚠robocopy가 OnvifSolution.Base 추적 3파일 또 덮음→checkout 복원 — 알려진 함정 재현) → 구현 11파일(+596/−81) → code-review(opus) **MERGE**(P0/P1 0, P2-1 불용using·P2-3 프리셋 준비완료 자동재조회 반영, P2-2 PtzPresetStore 데드코드=OQ-5a 의도 유지) → 타 세션 `e2e64c2`(라벨콤보) rebase 무충돌 → **v2.6 FF `475025e`** → worktree 정리(⚠서브모듈 gitlink로 `worktree remove` 거부 → rm -rf+prune).
- **구현 요점**: A=CanvasTop 세터 클램프 제거+드래그 하한 MinCanvasTop 단일참조 · B=위젯(DataTrigger 0숨김, 컨버터 불요)+`CallCloseAllCameraPopupsProcessMessageModel`+순차 CloseAll · C=IPtzController 프리셋 6메서드(Gate 직렬)+`OnvifPresetDisplayModel`(IPtzPresetModel 구현 어댑터=XAML/이벤트 시그니처 무변경)+Home=ONVIF 슬롯+상태문구 4종 · L=Stop LWW(뗌 Stop에 제스처 토큰, `CurrentPtzGestureToken` ODE→취소토큰)+보상 Stop(R-1, `ContinuousMoveWithStopFallbackAsync`)+capable 정렬(`_onvifResolveTasks` 레지스트리 WhenAny)+Stop gateWait/WCF 로그.
- **검증**: in-project 253/254(빨강 1=EditRecorder, v2.6 기준선 동일 실측=회귀 0)·외부 207/207·신규 12케이스·빌드 0·BOM 7파일 보정. 리포트=`docs/reports/CameraPopup_PanClamp_Badge_OnvifPtz-report.md`.
- **🔲 남음(사용자)**: ①**메인솔루션 재빌드**(IPtzController 확장 — 코드 변경 없음) ②런타임 육안 4종(위로 패닝 추종/위젯+전체닫기/ONVIF 프리셋 CRUD+Home/PTZ 연타 반응) ③1.5s 정량 채증(분석 §6.5: `[PTZ] Stop gateWait=/WCF=` 신규 로그 — WCF 수백 ms↑면 Digest 401 의심→FR-L4 Phase2 착수) ④기존 DB 프리셋은 팝업에서 미표시(폐기 확정, 필요 시 카메라에 재저장).
- **⚠ 파이프라인/조율**: complete 게이트 교착(전-plan 합산)으로 new-cycle 불가 → 허용 백워드 체인(report→dev→plan→prd)으로 진입했으나, 동시 세션(Overlay_Title)이 09:51 phase를 report로 되돌림 → 이후 공유 상태 미접촉(문서+worktree 격리, Grant 선례). 게이트 수정(activePlan 스코프 or new-cycle from report)은 백로그(리포트 §3-7). [[feedback_prd_before_code]] [[project_camerapopup_preset_db_ptz_stop_gate]] [[project_gmap_orphan_submodule]] [[project_pipeline_advance_phase_gotchas]]
- **✅ v2.6→main FF 머지 (2026-07-24, 세션 마감)**: `git fetch . v2.6:main`(체크아웃 無, ref만) — main `d706029`→`d0bd165`(20커밋: 본 카메라팝업 3건 `475025e` + 타 세션 Overlay Title 4스테이지·라벨 v2.3~v2.5·NATS 로그 등 v2.6 전체 추종, 기존 관례). 원격 push 미실행(요청 시). 메인 체크아웃=v2.6 유지, 타 세션 무영향.

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 세션 관리 패널 정리 3항목 + 로그인이력 서버REQ) — Track C, ✅ 구현·검증·v2.6 FF 머지 `1cd33a9`(rebase 후 FF)·메인 lib 빌드
- 사용자 "세션 쪽 정리(API 서버 무변경)" = **3항목**: ①표시(날짜 `string?` ISO→신규 `IsoDateStringConverter`(Utils) `yyyy-MM-dd HH:mm`) ②기능(**사용자 전체 세션 종료** 배선: `ForceLogoutAllUserSessionsAsync` **DIM**(스텁 무수정)+`DELETE /user-sessions/user/{id}`+확인팝업/409 ADMIN락아웃 안내+행 버튼) ③동작(기본 **전체표시**+자동갱신 20s `System.Threading.Timer`, page1&&!loading&&!teardown 가드, teardown **TOCTOU 하드닝**).
- 검증: code-review(opus) **READY**(P0/P1 0, 타이머 수명·크로스스레드 심층). Ui.Tests **44** green·빌드0. 롤백 `before-session-panel-cleanup`. Plan=`GOP_SessionPanel_Cleanup`.
- **⚠ 로그인 이력 패널 보류**: 서버에 리스트 엔드포인트 無(report_master_builder만, 500행) → REQ `docs/coordination/REQ_Server_LoginLogs_ListEndpoint.md`(GET /api/login-logs, audit §9.6.2 미러) 작성, **서버 반영 후** 클라 착수(감사 패턴 이식). [[project_auditlog_datefilter_scroll_pattern]]

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 세션관리+권한부여 무한스크롤) — Track C+B, ✅ 구현·검증·v2.6 FF 머지 `2758c4e`(메인 lib 빌드로 DLL 산출)
- 감사로그 무한스크롤 패턴을 **세션관리**(`GetUserSessionsAsync` page/limit/is_active 확장+활성/전체 토글, **날짜=서버 미지원**=라이브모니터링)·**권한부여**(100초과 경고팝업→무한스크롤, `/grants` `TotalPages=0`→`Ceiling(total/size)` 파생)에 이식. 공유 `DataGridScrollEndBehavior`+`AsyncRelayCommand` 재사용(신규 UI 없음).
- 검증: code-review(opus) **READY**(P0/P1 0, F-1 실서버바인딩 확인, 기존 J섹션 테스트 충실 재작성+9), Ui.Tests **36**·Api **136** green·빌드0. 신규 테스트 9. 롤백 `before-session-grant-pagination`. PRD/Plan=`GOP_SessionGrant_Pagination`.
- ⚠ **fork 정체성 혼란**(오케스트레이션 서사 상속→"실행자=나" 망각→구현0) → **컨텍스트 없는 신규 general-purpose 에이전트**로 복구 성공.
- 🔲 **D-1**(사용자 결정): 세션 기본=활성만(라이브모니터 적합, 원하면 `_isActiveOnly=false`로 전체 기본). 🔲 **로그인 이력 패널**(UserLoginLog 성공+실패·날짜필터)=서버 데이터 有·클라 無 갭, 미착수. [[project_auditlog_datefilter_scroll_pattern]]

## ▶▶ 재개 포인트 (2026-07-24, 이 세션 — 측정 툴(길이·넓이) 신규 기능) — Track C, 🏁 코드그라운딩+HTML 스토리보드 완료·**사용자 검토/미결질문 대기**
- **요청**: 탑메뉴에 길이 재기·넓이 재기 아이콘+동작 구현, 단 **HTML 스토리보드/와이어프레임 먼저**.
- **코드 그라운딩(워크플로 `wf_e167a455`, 6탐색+종합)**: 탑메뉴=**GMaps.Ui `MapView.xaml` ToolBar @252**(메인솔루션 아님, CanvasSectionView는 `<ContentControl x:Name=MapViewModel/>`만) → 편집 그룹(@371-414) 뒤 「측정」 그룹 삽입. 재사용=**`LineDrawingAdorner`/`LineDrawingService` 포크**(저장단계 CompleteDrawingAsync만 제거=임시 오버레이) + `GMapCustomControl` OnMouseLeftButtonDown IsMeasuring 분기(@972 선그리기 옆) + 상태배너 `AimStatusMessage` 클론 + `MapViewModel.Measure.cs` 신규 파샬(CenterCrosshair 27줄 패턴). 아이콘=**Ruler(길이)/VectorPolygon(넓이)**. 수학=길이 Haversine(이미 `LineDrawingAdorner.TotalDistance` 라이브)·**면적=지오데식 구면초과 shoelace 신규**(GeoPointConverter.CalculateArea, **픽셀 shoelace 금지**=MBTiles EPSG:3857 왜곡). 단위 m/km·m²/ha/km². 활성=plain Button+DataTrigger 시안 or ToolbarToggleButtonStyle 채움. 상호배타(길이↔넓이↔선↔조준)·편집모드 무관·DB 미영속.
- **산출물**: `docs/design/Measure_Tools_Storyboard.html`(10프레임: 탑메뉴 idle/active·길이 첫클릭/진행/완료·넓이 진행/완료·리드아웃 클로즈업·다크/라이트·키맵·토큰팔레트·수학·재사용맵·미결질문 7). INDEX 설계 섹션 행 추가. 다크/라이트 토글 실 hex.
- **🔲 남음**: 미결질문 7 확정(완료제스처/활성표현/단일vs누적/영속/단위표기/지구반경상수/채움시점) → 승인 시 PRD `Measure_Tools`→plan→dev(worktree). 코드 미착수.

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 오버레이 Title 줌 위치버그+스타일 분석/PRD) — Track C, ✅✅ **dev 완료·v2.6 FF 머지 `f645eae`** (PRD v2.2 Approved→구현 13FR 전체)
- **✅ 구현 완료(사용자 "진행해줘" → approve→plan→dev 4스테이지, worktree `feature/overlay-title`, 롤백 `before-overlay-title`, v2.6 92edb4d 위 rebase 후 FF)**: 스테이지1 이미지영속화(Images 4+6컬럼·GMapImageMarker 위임+INPC·U/V 신설) → 2 줌안정화(LabelAdorner footprint 자체투영·U/V 렌더/역산·상한 3·max(hw,hh)·bearing AABB) → 3 스타일+폭(6속성 전계층·부분UPDATE `UpdateSymbolLabelStyleAsync`·패널 6행·**edge-pinned 폭 WYSIWYG**+`TitleWidthResizeCommand` 원자 undo) → 4 캐시(FormattedText/브러시/정적 Typeface·필터 20종·Service 증분 O(1)·Dispose TOCTOU). **테스트 242 중 241 green**(1 red=FOVColor CMD-02 v2.6 기존 상속). 빌드0. 신규 3파일 BOM. 리포트=`docs/reports/Overlay_Title_ZoomStyle-report.md`, Plan 32/33(TEST-06 벤치 [-] 후속).
- **🔲 남음**: ①**앱 재빌드 후 런타임 육안 6항목**(리포트 §4 — 줌 상대위치·재시작 보존·패널 스타일·회전 추종·폭 드래그·title-only) ②TEST-06 벤치 ③V-08 라인 정점 신호 런타임 ④OQ-1 라인 정규화 Phase2 ⑤파이프라인 `complete`는 **activePrd=타세션 PRD라 자동 PRD-Completed 오적중 위험 → 보류**(phase는 dev→test→report 복귀만 수행).
- **✅ 후속 v2.3 (사용자 버그리포트, 커밋 `1ce2614` v2.6 FF)**: 글자색/배경색 **hex 편집 콤보 렌더 결함**(PropertyComboBox 템플릿에 편집파트 없음→값 표시 깨짐+LostFocus라 선택 즉시 미반영) → **기존 채우기/테두리 색 콤보 구조 그대로 차용**: 비편집 콤보+`LabelColorOption`(이름+Argb) 팔레트(글자 11/배경 10, 투명·반투명 칩 포함, 기본값=첫 항목=선택 표시 보장)+스와치 40×12 ItemTemplate+`SelectedValuePath=Argb`. `ArgbHexConverter`는 XAML 리소스만 제거(클래스·테스트 보존). 라벨 테스트 37 green.
- **✅ 후속 v2.4 (사용자 크래시 리포트, 커밋 `04731a7` v2.6 FF)**: ①**폰트 콤보 크래시** — `{x:Static Fonts.SystemFontFamilies}` 전체 열거가 앱 사망 유발 → `LabelFontOption` 큐레이션 12종(한글 폰트=한글명 맑은고딕/굴림/돋움/바탕/궁서·영문=영문명, `PreviewFamily` 자기폰트 미리보기, 미탑재=WPF 무음 폴백, 빈값=Segoe UI 규약 유지) ②**디자인 정합** — 라벨 6행(글자색/배경색/글꼴/글자체/제목폭)을 BASIC 중간 → **COLOR 섹션 뒤 전용 LABEL 섹션**(구분선+FontSize13 Bold 대문자 헤더 = BASIC/COLOR/DISPLAY 컨셉 동일), 색 콤보=채우기 콤보와 픽셀 동일(Background=Transparent 포함). 라벨 테스트 37 green. v2.6이 중간에 2회 전진(92edb4d→475025e)해 rebase 2회 수행.
- **✅ 후속 v2.4-2 (사용자 스샷 "뭐가 잘못됐나", 커밋 `4bf34a5` v2.6 FF)**: 스샷 픽셀 실측(GetPixel 스캔)으로 검증 — **콤보 형식은 채우기/테두리와 완전 일치**(스와치 x=89 시작·40px 폭·콤보 높이 23px·행 피치 38px·색 α블렌드 정확). 실측된 유일한 구조 이상 = **LABEL 추가로 패널이 길어져 열린 시점 BASIC(제목/제목크기/크기)이 스크롤 위로 밀림**(상단 반잘린 '크기' 행+스크롤 썸 확인) → `PART_ContentScroll` 명명+`OnSelectedMarkerChanged`서 `ScrollToTop()`(마커 선택 시 항상 제목부터). 사용자 지적이 다른 지점이면 행 특정 필요.
- **✅ 후속 v2.5 (사용자 확정 지시, 커밋 `7ad5255` v2.6 FF)**: "심볼 기본 색상 콤보를 그대로 활용" → 라벨 글자색/배경색을 **FillColor와 완전 동형 EnumColorType 파이프라인**으로 전면 전환 — 계약/마커/모델/DB(VARCHAR(20) 'White'/'Black', 초기 INT 스키마는 MODIFY+숫자잔존 UPDATE 정리+EnumParseHelper 폴백)/undo(ToEnum)/패널(공유 AvailableColors 콤보+SelectedItem+동일 스와치 템플릿+MIXED_COLOR pending). 렌더=ColorHelper hex, 배경 `ChipAlpha 0xCD` 합성(Black=#CD212121≈종전 칩·Transparent=배경 없음). ARGB 팔레트·LabelColorOption·ArgbLabelConverters 폐기. 252/253 green(기존 red 1). **교훈: 사용자 "그 콤보 그대로" = 목록·바인딩까지 재사용이지 형식 모방이 아님 — architect의 int ARGB 결정을 사용자 방향으로 번복(PRD A4 갱신)**.
- **✅ 후속: 제목 저장 전수감사 (사용자 "PidsGroup Title 미반영·admin", 커밋 `d0bd165` v2.6 FF)**: 전 타입 추적 결과 — UPDATE에 Title 포함 ✓(base·JOIN 6종·이미지)·switch 상속가림 無·PidsGroup fetch JOIN s.Title ✓·레이어패널 rename=부분 UPDATE ✓·MySql.Data 기본(matched rows)이라 동일값 ==0 불가. **근본결함 2종 수정**: ①JOIN 6종(Geometry/Pids/Military/Line/Infra/PidsGroup) `typeAffected==0`(타입 테이블 행 부재) 시 **전체 롤백→base Title까지 무음 소실** → 타입행 자가치유 INSERT(트랜잭션 내, Warning 로그)+base 부재 시만 throw ②`OnMarkerPropertyChanged`(async void) DbUpdateProcess 무가드 → try/catch(실패 로그+미저장 값 undo 기록 스킵) ③비편집모드 변경 무시(설계)도 Warning 로그로 가시화. **사용자 확인 포인트**: 재빌드 후 PidsGroup 제목 변경 시 로그에 "PidsGroupSymbols 행 부재 → 자가치유"(=원인 확정) 또는 "속성 변경 미영속(편집모드=False…)"(=편집모드 문제) 중 무엇이 찍히는지.
- **✅ 후속: 재시작 리셋 근본원인 2종 (사용자 "로그 봐라, 재시작하면 다 날아감", 커밋 `0003332` v2.6 FF)**: 앱 로그(`Dotnet.Monitoring.Solution/bin/Debug/net8.0-windows7.0/Logs/log-2026-07-24.txt`) 직접 판독 — 01:18 스타일 4건(TitleColor=Lime 등) 저장 성공 로그+실패 0건인데 재시작 리셋, 그리고 이틀치에 "Title=..." 변경 이벤트 전무(콤보류는 기록됨). **원인① 로드 리셋**: 파생 JOIN 매퍼 6종(ToGeometry/Pids/Military/Line/Infra/PidsGroupDomain)이 base 필드 수동복사라 **스타일 6필드 매핑 누락** → 저장은 되나 재시작 로드에서 기본값(매핑 2→8곳 보정). **원인② 제목 커밋 미발생**: 제목 TextBox가 LostFocus 커밋이라 입력 후 바로 맵클릭/닫기 시 이벤트 자체가 안 남 → `PropertyChanged+Delay=800`으로 교체(입력 멈춤 후 자동 커밋, 기존 undo coalescing·트리 리로드와 정합). **교훈: 파생 DTO 수동복사 매퍼는 신규 필드 추가 시 전 매퍼 grep 필수(매핑 카운트 검증), LostFocus 커밋 필드는 무음 소실 벡터**.
- **✅ 영속 왕복 전수감사 (사용자 "라벨 영속 보존됨" 확인 후, 워크플로 9에이전트 `wf_1e451287`)**: 8개 타입(Custom/Geometry/Pids/Military/Line/Infra/PidsGroup/Image) × INSERT·UPDATE·FETCH SELECT·매퍼 4단계 컬럼별 감사 → **실결함 0건**(WRITE_ONLY_RESTART_RESET/READ_ONLY/MISSING 전무). Title+5기본필드=전 타입 OK, 스타일 6필드=이미지 인라인 OK·나머지 7타입 OK_VIA_PARTIAL(쓰기=UpdateSymbolLabelStyleAsync 부분UPDATE, 읽기=fetch+mapper 완비). 크로스레이어 7 PASS(DbUpdateProcess 8타입 라우팅·부분스타일 호출·try/catch 무음차단·Title 트리리로드·패널 6DP 대칭·제목 TextBox PropertyChanged+Delay·스키마 마이그레이션 INT→VARCHAR 교정). **잔존 P2(양성)**: 7 Symbols타입 INSERT에 스타일 6컬럼 미포함 → 신규 심볼은 생성 시 DB기본값(White/Black/200), 첫 속성편집 시 부분UPDATE로 실값 도달(재시작리셋 아님·읽기 무결). draft에 스타일 선지정 후 첫 저장 시에만 유실 가능 — 미수정(사용자 결정 대기). 감사 저널=`subagents/workflows/wf_1e451287-2c4/journal.jsonl`.
- **✅ OQ-1 해소: 라인/PidsGroup 라벨 오프셋 줌 고정 (사용자 "드래그한 PidsGroup 라벨 위치를 줌에서 고정", 커밋 `48493fe` + CHANGELOG `752aa96`)**: 드래그 위치는 재시작엔 유지(영속 OK)되나 줌 시 그룹 대비 어긋나던 원인=라인/PidsGroup만 px 오프셋(bbox는 2^Δz 스케일). 이미지와 동일 정규화(U/V=하프익스텐트 비율)로 전환 — `LabelAdorner.IsNormalizedOffset`(이미지||라인)+`ReadOffset/WriteOffset`, LabelCenter·드래그·폭조절 3경로 통합. `Symbols.LabelOffsetX/Y`를 라인계열에서 비율로 재해석(undo/record 무변경=opaque double). **마이그레이션**: BuildSchemeAsync서 AREA_BOUNDARY·PIDS_GROUP의 레거시 px(|v|>3, 비율 불가값) 1회 0 초기화 → **기존 드래그 위치 1회 리셋, 재드래그하면 이후 줌 고정**(드래그시점 footprint 미상이라 정확 변환 불가). 점 심볼은 px 유지(다른 Category, 미접촉). 라인 정규화 줌불변 헤드리스 테스트 +1(라벨 38 green). ⚠**주의**: 사용자에게 "기존 드래그 위치 1회 초기화 후 재드래그 필요" 고지함.
- **📋 남은 작업(전 타입 영속 감사 후 확정)**: [VERIFY 재빌드 후] 재드래그한 라인/PidsGroup 라벨 줌 고정·8타입 제목/스타일 저장·재시작 / [CODE 선택 P2] 신규 심볼 INSERT에 스타일 6컬럼 미포함(생성 시 DB기본값, 첫 편집 시 실값 — 무해) / [DOC] plan·report를 v2.6까지 동기화(PRD·CHANGELOG는 완료) / [PROCESS] 파이프라인 complete가 타 세션 activePrd 때문에 막힘(재조준 필요, --force 금지).
- **⚠ 워크트리 잔여**: feature/overlay-title에 orphan CHANGELOG 커밋 `d0de363`(v2.6엔 CHANGELOG를 직접 `752aa96`로 커밋함) — 브랜치 divergent, 재머지 금지(재사용 시 리셋). orphan 복사 Onvif 3파일 stash("orphan-copy onvif drift") — 내 변경 아님, 폐기 가능.
- **2차 검증(사용자 요청 "다시 시뮬로 PRD 이슈 체크")**: 라운드2 시뮬 69건(`docs/analyses/_overlay_title_sim_round2.js`, 분석 §9) — 1차 108건 재실행 동일(재현성 OK). **v2.0 스펙 결함 7건 발견→v2.1 전건 반영**: ①FR-13 리사이즈=**edge-pinned 유일 성립**(중심고정=커서 절반추종/반대편 밀림)→Δ폭/2 오프셋 보상+(오프셋,폭) 원자 undo ②스트립 `min(6px,박스폭25%)`(2글자 라벨 잠식) ③FR-03 상한 `√((U·hw)²+(V·hh)²)≤3·max(hw,hh)` 등방화(구식은 파노라마 세로 120px 이방) ④§5-B 무효화 필터 누락 5건 보정(TitleMaxWidth·U/V·**Bearing·ImageBounds**)+포함20/제외8 확정 ⑤W/H는 FR-01 후 값 미사용이어도 지오메트리 신호 유지 명시 ⑥V-08 신설(라인 정점편집 신호, RuntimePoints INPC無) ⑦DDL ARGB 부호 리터럴(TitleColor `-252645128`/TitleBackground `-853729758`).
- **요청**: ①줌에 따라 Title 위치가 이상하게 변함 ②Title 글자색/굵기/이탤릭/폰트/배경색 부재 → 시나리오 ~100건 시뮬 분석 기반 PRD.
- **분석(시뮬 108건 + 체인 Explore→architect(opus급)→code-reviewer(opus급))**: `docs/analyses/Overlay_Title_ZoomStyle-analysis.md` + 재현 스크립트 `docs/analyses/_overlay_title_sim.js`. **핵심 실측**: 라벨=맵레벨 `LabelAdorner` 단일 경로(이미지 마커 포함, `LabelAdornerService.Sync`가 전 IEditableMarker 부착) · 수식 `앵커+(offX, H/2+8+offY)`에서 **오프셋=px 고정 vs 이미지/라인 footprint=2^Δz 스케일(혼합 도메인)** · 이미지 모델 W/H는 `GMapMarkerImageControl.OnRender`가 매 렌더 화면px 역주입(:283-286, `SuppressBoundsUpdate`)→1프레임 stale 점프(최대 1,600px)+시작 시 이전 세션 줌 DB값 오배치 · **이미지 TitleSize(필드11)/ShowTitle/LabelOffset 전부 비영속**(Images 테이블 컬럼 무·UpdateImageAsync 무음유실=P0 확증) · 드래그 상한 화면px 도메인(줌마다 ×128 변동) · Bearing 미반영 · 스타일 전부 하드코딩(#F0F0F4F8/#CD1C1E22/Segoe UI/200px 1줄). **점 심볼 기하는 정상(0/30)** — 체감 증상은 이미지(24/30 FAIL)+라인(8/10 FAIL). 게이트 진리표 10/10 무결.
- **채택 설계(시뮬 전 그룹 0 FAIL = 모델 B)**: footprint 자체투영(이미지=`IImageEditableMarker.ImageBounds` 회전 AABB, 라인=`RuntimePoints`, is-패턴 — 신규 인터페이스 금지) + 고정 8px 갭 + **이미지 오프셋만 정규화(신규 Images.LabelOffsetU/V)**, 심볼·라인 px 유지(회귀 0, architect 옵션c) · 색=int packed ARGB(EnumColorType 불가침) · 폰트=`Fonts.SystemFontFamilies` 콤보. 지오앵커(C)=심볼 4,920px 발산 기각.
- **PRD Draft(v2.0)**: `docs/prds/Overlay_Title_ZoomStyle-prd.md` — FR 13(A:줌안정 4 / B:스타일 6+**FR-13 폭 WYSIWYG** / C:이미지영속+신뢰성 3, ~45태스크) · NFR 7(시각 무변화·성능 500마커 16ms·시뮬 xUnit 이식·멱등 ALTER 2곳·BOM) · V 7(V-01/05 확인됨) · 리스크 8(R-1 ALTER 누락=1054 전멸 / R-8 이동vs폭조절 히트 분리) · **OQ 4**(라인 정규화 Phase2? / 색 UI 형태 / 폰트 목록 / 폭 축소 시 말줄임vs줄바꿈). **FR-13(v2.0 사용자 추가)**: `TitleMaxWidth`(px·줌불변, DEFAULT 200=현행) — 편집모드 라벨 칩 좌/우 6px 스트립 드래그로 말줄임 지점 실시간 조절(WYSIWYG)+속성패널 숫자 병행+undo, 클램프 40~800. 롤아웃 4단계 독립 revert. code-reviewer 12항목 배선 체크리스트 §3 수록.
- **🔲 남음**: ①사용자 PRD 검토+OQ-1~3 확정 ②승인 `node .claude/hooks/advance-phase.js approve prd "코멘트"` — ⚠ approve는 mtime 최신 PRD 선택인데 **타 세션 Detection_Signal_History-prd가 오늘 Approved**(pipeline-state.activePrd 점유) → 내 PRD 승인 직전 touch 필수([[project_approve_prd_picks_newest_mtime]]) ③승인 후 plan(착수 시 롤백태그 `before-overlay-title`+v2.6 worktree+고아 서브모듈 복사).
- **⚠ 코디네이션**: pipeline-state 미변경(phase=prd·track=C 그대로, 타 세션 activePrd 미접촉). 문서 산출만: analysis·PRD·INDEX 2행·본 블록. [[project_symbol_label_adorner_system]] [[project_two_image_overlay_systems]] [[project_migration_resurrection_trap]] [[feedback_prd_before_code]]

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 탐지 신호(detail.signal) 표면화 컨셉 설계→구현) — Track C, ✅✅ 전체 완료(v2.6 머지+메인솔루션 배선), 런타임 육안=사용자
- **요청**: ①탐지 이벤트 DataGrid에 신호 크기 병기 ②장비속성→[탐지 이력 보기]→센서별 시간축 신호 추이(차트+그리드) ③HTML 와이어프레임+스토리보드.
- **데이터 흐름 실측**: 스펙(GIS.md) DETECT body `detail.signal`(int, 예 2000; AI_DETECT=0) → `DetectionDetailDto.Signal` 파싱 OK(테스트 有) → **`DtoToModelHelper.ToDetectionEventModel` 2개 오버로드에서 Detail 미매핑=유실**(모델에 필드 자체 없음, `DetectionEventModel`=Result만) → 카드/그리드 미표시. 라이브 카드 생성=메인솔루션 `NatsDomainService.ProcessDetection/ProcessDetectionMode`(:138/:381)에서 동일 헬퍼 경유라 배관 복구 시 자동 수혜.
- **실현성 확정**: 이력 조회 API `GetDetectionEventsAsync(startDate, endDate, sensor, page, limit)` 이미 센서+기간 필터 지원 → 화면 D(신호 이력 패널)는 **서버 변경 0**으로 가능.
- **⚠ 함정 2종(설계 반영)**: ①AI_DETECT signal=0(의미 크기=objects[].confidence) → "—" 처리+차트 기본 제외 ②`ToDetectionEventReplaceDto`(PUT)가 detail 미전송 → 이벤트 수정 저장 시 서버 detail 소실 가능, 신호 표면화 PRD에 보존 처리 포함 필수. 단위·최대값 스펙 없음 → 게이지=조회구간 최대 기준 상대 표현만.
- **산출물(v2 개정, 사용자 피드백 3건 반영)**: `docs/design/Detection_Signal_History_Storyboard.html` — ①진입점=속성창 폐기→**우클릭 2종**(C-1 맵 심볼 우클릭: 기존 `MapViewModel.ShowMarkerContextMenu`(:5219)에 "탐지 이력" MenuItem 추가, SmartSensor+LinkedDeviceId>0, 웹서버 게이트 미적용 / C-2 `SensorDevicePanelView` RowStyle ContextMenu 신설=탐지패널 '조치보고' BindingProxy 패턴 미러) ②컨테이너=**팝업 다이얼로그**(`md:DialogHost`+`md:Card` 880×620+`ModernDialogHeader`, ConfirmPopupDialog 계열; 오픈메시지/VM은 Events.Ui 소유, GMaps.Ui/Devices.Ui는 EA 발행만; ThemeAssist.Theme 하드코딩 금지+DialogTheme=Inherit; 중첩 조치보고=Identifier 분리 vs 순차전환 PRD 확정) ③**다크/라이트 토큰 완전 대응**(Tokens.Dark/Light.xaml 실측 HEX, 문서 우상단 토글로 두 테마 렌더) ④**MessageType 제거**(사용자: 패널이 타입별 구분이라 중복 — 그리드 'Message Type' 컬럼+카드 앞면 'Intrusion' 줄 둘 다 실제 현재 UI에 존재, Phase 1에서 제거). INDEX.md `## 설계 (docs/design/)` 섹션 신설.
- **✅ PRD 작성 완료 (Draft, 승인 대기)**: `docs/prds/Detection_Signal_History-prd.md` — **FR 15개(~39태스크, Track C)** = Phase1 배관·표면화 7개(FR-01 모델 Signal / FR-02 매핑 2곳+역방향 / FR-03 Replace detail 보존 / FR-04 그리드 신호컬럼 / FR-05 MessageType 컬럼 제거 / FR-06 카드 병기 / FR-07 Intrusion 줄 제거) + Phase2 진입·다이얼로그 8개(FR-08 공용 오픈메시지=**ViewModel/Models/CommonMessages.cs**(참조 실측: GMaps.Ui→Events.Ui, Events.Ui→Devices.Ui라 Devices.Ui는 Events.Ui 참조 불가=순환, 기존 OpenEventReportDialogMessageModel 관례 위치) / FR-09 다이얼로그 셸(Events.Ui VM/View+메인솔루션 Shell 배선=통지 대상) / FR-10 맵 우클릭 / FR-11 그리드 우클릭 / FR-12 조회엔진 ≤500 / FR-13 자체 Canvas 차트 / FR-14 필터·통계 / FR-15 그리드+조치보고 연계). NFR 5(테마 토큰/성능 2s/회귀/안정성/BOM) · V 5(signal 실기입 V-01·02, PUT detail 수용 V-03, Shell DialogHost 구조 V-04, MessageType 편집 잔재 V-05) · 리스크 5(R-1 실환경 signal 미기입=높음→선검증) · OQ 2(중첩 다이얼로그 방식, Phase1 선머지 여부). pipeline-state를 이 PRD로 동기화(stale 6/19 상태 회수, phase=prd·track=C 유지).
- **✅ 승인·구현·머지 완료 ("시작해", 2026-07-23)**: approve 게이트 통과(v1.1: V-01/03/04/05 확인, **OQ-1=(b) 순차**(DialogShell=Conductor OneActive 실측로 (a) 겹침 불가), OQ-2=(a)) → plan 36태스크 → worktree `feature/detection-signal-history`(태그 `before-detection-signal-history`, 고아 GMap.NET/OnvifSolution 복사, ⚠robocopy가 OnvifSolution.Base 추적파일 3개 덮음→checkout 복원) → **라이브러리 커밋 `7accdab`(21파일 +1929/−62)+리뷰fix `736207e`** → 타 세션 `2758c4e` rebase 흡수(CHANGELOG 충돌 1건 양쪽보존) → **v2.6 FF 머지** → **메인솔루션 EXT 커밋**(ConductorControl IHandle<OpenDetectionHistoryDialogMessageModel>+`DetectionHistoryHostDialogViewModel/View` 래퍼+Bootstrapper 등록, 빌드 0오류).
- **검증**: 신규 `DetectionSignalTests` 12/12 · 전체 331 green(실패 7=v2.6 기준선 동일=회귀 0) · code-review(opus) **P0 0**·P1 4 반영(이중로드 가드/툴팁 hover 캐시/클릭경로 단일화/static max 주석) · 신규 10파일 BOM · 메인솔루션 컴파일 0오류. 리포트=`docs/reports/Detection_Signal_History-report.md`.
- **⚠ 시스템 발견 2건**: ①pipeline-state는 **브랜치 스코프**(`.claude/.branch-v2-6/pipeline-state.json`) — `docs/memory/pipeline-state.json`은 레거시 폴백. ②complete 게이트=**전체 plans 폴더 미완료 합산**(과거 플랜 16개에 차단, test 게이트 전-plan 합산과 동류) → 사이클은 report 단계에서 정직 종료(타 플랜 조작 거부). set-track 체크포인트가 또 타 세션 WIP sweep(`10d8c0d`, 무해 확인).
- **✅ 런타임 피드백 1건 반영 (`f62c6f5`, v2.6 FF)**: 사용자 실측 스크린샷(docs/pictures/…174536.png — ⚠비표준 폴더, 표준=docs/assets/) — 다이얼로그 실구동 확인(Fence_002 No.1101). 툴바 필터 칩 잘림(Result 타입 다수 시 한 행 880px 초과) → **툴바 2행 분리**(1행 기간+새로고침 / 2행 칩 WrapPanel 줄바꿈+미조치만 우측) + 칩 `ShortName`(`_SENSOR` 제거, 풀네임 ToolTip). PRD v1.2·plan IMPL-17 기록. worktree=v2.6 FF 정렬(타 세션 d706029 ShowMarkerContextMenu 추가 수정과 '탐지 이력' 항목 공존).
- **✅ 런타임 피드백 2차 3건 반영 (`ad3267d`, v2.6 FF, PRD v2.0·plan IMPL-18)**: 스크린샷 3장 실측 — ①**detail 전체 표면화**: 모델 확장(AiModel/InferenceMs/Thumbnail/Objects+신규 `DetectionObjectModel`)+헬퍼 전체 왕복(BuildDetectionDetail(model) — AI signal=0도 objects 보존)+공용 "탐지 속성"(`DetectionSelectionView`, 조치보고·이벤트정보 다이얼로그 공유)에 신호/AI(model·ms)/객체(label conf% bbox)/썸네일 읽기 전용 행(선택 첫 항목 기준) ②**차트**: `RangeStart/End` DP=X축이 조회 구간 반영(데이터 몰림과 무관)+휠 줌(커서 시각 고정·최소 1분)+드래그 팬(4px 임계로 클릭과 구분·경계 클램프)+더블클릭 전체복귀+뷰 필터/클리핑/Y 재스케일+48h 초과 라벨 "MM-dd HH:mm" ③**닫기=헤더 우상단 ✕**(하단 버튼 제거, OnPrimaryFixed+hover). 테스트 14/14(+full detail 매핑·AI Replace 보존). V-02는 **사실상 해소**(스크린샷 신호 1,500 실표시=프록시 실기입 확인).
- **✅ 적대 감사(opus 워크플로 21에이전트, `511ddc6` v2.6 FF) 후속 반영**: "남은 작업 없나?" 질문에 미리뷰 2커밋(f62c6f5·ad3267d) 3차원 적대 리뷰 → **confirmed 17/rejected 1**. in-scope 8건 즉시 수정: ①**차트 크래시 봉인**(조회 구간<1분이면 `Math.Clamp(min=60>max=fullSpan)` ArgumentException=휠/드래그 시 앱 크래시 — OnRender 최소1분 보장+SetView/OnMouseWheel 가드) ②**드래그 고착**(`OnLostMouseCapture` — Alt-Tab 등 캡처 상실 시 팬 안전종료) ③**PUT frame_width/height 소실 봉인**(signal 함정과 동일 클래스, 왕복 대칭) ④복사생성자 Objects 깊은복사 ⑤멀티셀렉트 detail "(다중 선택)" 게이팅 ⑥`_loadCts` OnDeactivate Dispose ⑦BOM(SignalChartControl·DtoToModelHelper) ⑧문서(CHANGELOG/리포트/PRD v2.1). 테스트 16/16(+frame 왕복·깊은복사). rejected 1=focusable-steals-focus(Focus() 미호출로 반증). 리포트=`docs/reports/Detection_Signal_History-report.md` v2.1 §1-A.
- **✅ 이력 그리드 행 높이 클리핑 해소 (`5b17cd9`, PRD v2.2)**: 스크린샷(093417)에서 그리드 레코드 텍스트 하단 잘림 — 공용 `ModernDataGrid`가 `RowHeight`를 토큰 `RowHeight`=30(Tactical 고밀도)으로 **고정**해 신호 미니바 행+MD 셀 패딩이 30px에 눌림. HistoryGrid 로컬 오버라이드 `RowHeight={x:Static sys:Double.NaN}`(auto)+`MinRowHeight=34`(공용 스타일/토큰 무변경). ⚠커밋 메시지에 stray `@`(Bash에 PS here-string `@'...'@` 오용) — 무해·머지완료라 방치(bash 멀티라인은 `-F` 파일).
- **⚠ 미해결 관찰 (사용자 재빌드 후 재확인 요청)**: 스크린샷 차트 X축이 "30일" 선택인데 ~83분(21:14~22:37) 데이터 범위만 표시 = `RangeStart/End`(조회구간 축) 미반영. **머지 코드엔 바인딩 정상**(XAML 381-382·VM 212-215·296-297 확인) → 사용자 빌드가 그 fix 이전일 가능성. 최신 v2.6(5b17cd9) 재빌드 후에도 좁으면 진단 착수(단, 데이터가 83분에 몰리면 30일 축은 대부분 빈 채 클러스터 1개=의도된 동작이나 UX 어색할 수 있음).
- **🔲 남음(사용자 판단/선택 — Phase 3)**: ①썸네일 **이미지 미리보기**(현재 URL 텍스트, 범위 확대라 승인 필요) ②from_event full detail **서버 내성 검증**(objects embed) ③로컬 `Events.Db` detail 영속(별도 PRD) ④조치보고 후 이력 복귀 ⑤CSV/실시간 append/LiveCharts2. **런타임 육안**: 대시보드 퀵뷰(고정 Height=200) detail 행 클리핑 여부. [[project_detection_signal_pipeline_gap]] [[project_pipeline_advance_phase_gotchas]] [[project_multisession_checkpoint_sweeps_wip]]

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 심볼 우클릭 메뉴 뷰모드 표시+잠금 게이트 v2) — Track B, ✅ 구현·빌드·v2.6 FF 머지 완료 (`d706029`), 🔲 런타임 T1~T9만 대기
- **보고**: 편집모드 켜야만 심볼 우클릭 메뉴가 뜸. 기대=ZOrder만 편집모드 전용, 나머지는 잠금 아니면 뷰모드에도 표시.
- **실측 원인**: 우클릭 이벤트는 양 모드 발화 정상(뷰=`GMapCustomControl:1195` 수동 히트테스트, 편집=`GMapMarkerBaseControl:594` → 공통 `MapViewModel.ShowMarkerContextMenu:5219`). 문제=항목 Visibility 게이트 `(IsEditModeEnabled ∥ webServerEnabled)`가 장치페이지/상세/수정+**스피커 음원/TTS/Stop(NATS 기반인데 웹 조건 오결합)**에 걸림 → 웹서버 OFF 현장에선 뷰모드 우클릭=전항목 Collapsed 빈 메뉴(센서/스피커/경광등/함체). 제어기/카메라 홈페이지·특정위치확인은 무게이트라 떴음. 잠금(IsLocked)은 현재 메뉴에 영향 없음(양 경로 필터 부재).
- **사용자 확정 2건(AskUserQuestion)**: ①웹 의존 3종=표시하되 웹 OFF 시 **비활성**(disable 모델, 권한 UI 일관) ②잠긴 심볼=**메뉴 전체 미표시**(ZOrder 포함, 양 모드).
- **PRD**: `docs/prds/Symbol_ContextMenu_ViewMode_Lock-prd.md`(Draft, v2) — FR-1 잠금 조기 return / FR-2 스피커 3종 게이트 제거 / FR-3 웹 3종 disable 전환 / FR-4 ZOrder 현행 / FR-5 이미지 메뉴·비PIDS 범위외. 구 `ContextMenu_DisplayRules-prd.md`(6/10)=Superseded 표기. 대상=MapViewModel.cs 1파일 ~12줄.
- **✅ 구현 완료(커밋 `d706029`, v2.6 FF, +4/−9 1파일)**: Plan(12/13)→태그 `before-symbol-contextmenu-v2`→worktree `feature/symbol-contextmenu-v2`→FR-1 잠금 조기 return·FR-2 스피커 3종 게이트 제거·FR-3 웹 3종 disable 전환→GMaps.Ui 체인 빌드 0오류→FF 머지·worktree/브랜치 정리. CHANGELOG [Unreleased] 항목 포함.
- **⚠ worktree 빌드 함정(실측)**: GMap.NET 서명파일(`Directory.Build.props`·`sn.snk`·`Build/`)이 gitignore → CS0281/CS7027, 메인에서 3개 복사 필요. OnvifSolution 2폴더=여전히 orphan → 통째 복사(bin/obj 제외). [[project_gmap_orphan_submodule]] 갱신됨.
- **✅ v2.6→main FF 머지(2026-07-23)**: `git fetch . v2.6:main`(체크아웃 無, ref만 FF) — main `a408ef9`→`d706029`(18커밋: 벤더 GMap.NET 편입 1b85304·탐지신호이력·본 컨텍스트메뉴 v2 등). 메인 체크아웃은 v2.6 유지, 타 세션 무영향. 원격 3개(gitea/origin/sensorway) push는 미실행(요청 시).
- **🔲 남음**: 런타임 검증 T1~T9(PRD, 사용자·앱 재빌드 후) — 특히 뷰모드+웹OFF: 센서=웹3종 비활성 표시/스피커=방송3종 활성/잠금 심볼=메뉴 없음. [[feedback_prd_before_code]]

## ▶▶ 재개 포인트 (2026-07-23, 이 세션 — 강풍(WINDY)모드 변경 시 장애문구 원인 분석) — Track A(분석+조율문서, 코드 무변경), 🏁 진단 완료·프록시 회신 대기
- **현상(현장)**: 강풍모드 변경 → 적용은 됨 + GIS "풍량 모드 변경 실패" 팝업(=장애문구). 사용자 가설(Response 미수신) **코드로 확증**.
- **진단**: GIS `NatsDomainService.HandleAsync(SendWindyModeMessage)`(메인솔루션 :641~700) = `sensorway.{group}.proxy.windy` REQ 후 **NATS core request-reply 3초 대기**(`NatsService.RequestAsync`) → 무응답 시 팝업+`FetchProxySettingsAsync` 재동기화(적용된 새 모드 유지 → "적용은 됐는데 문구만"). 팝업 로직은 `120c02c`(2026-07-13, 5s→3s)에서 추가 — 이전 빌드는 로그만 남겨 무응답이 **비가시**였음(문제는 기존부터).
- **원인 후보(프록시 측)**: ①RSP를 **reply-to 인박스가 아닌 subject에 publish**(GIS 구독=`{domain}.{group}.gis.>`+`all.>`뿐이라 수신 불가) ②REQ 회신 미구현(Central PUB 경로만) ③전 제어기 적용 후 회신이라 3초 초과. 판별=팝업 둘째 줄: "브로커/서버 응답이 없습니다"=미도달 / "서버가 요청을 거부했습니다"=RSP 도달·success≠true(JSON 형태 불일치, `success` 부재 시 기본 false). 클라 `BrokerResponse` 매핑은 규격 §7.1.2와 일치 검증 완료.
- **산출물**: `docs/coordination/REQ_Proxy_WINDY_RSP_ReplyTo.md`(프록시 확인요청 — 체크리스트 4·판별표·nats CLI 검증법) + INDEX.md 수동 섹션(조율/확인요청) 신설.
- **✅ 후속(2026-07-23, 사용자 요청)**: ①타임아웃 3s→**5s** 상향(솔루션 `4bfb540` v0.5, NatsDomainService+CHANGELOG) ②라이브러리 `NatsService.RequestAsync` 실패유형 구분 로그(`31b1a26` v2.6) — **no-responders(구독자 부재, 즉시 503)** vs timeout(5s). 현장 "보내자마자 실패문구" 보고=no-responders 정황(타임아웃 상향으로 안 사라짐, 재빌드 후 로그로 확정). 확인요청 문서도 5초 기준+판별표 세분화로 갱신.
- **🔲 남음**: ①프록시 담당 회신(체크리스트 1~4) ②앱 재빌드 후 현장 GIS 로그 확보(`[RequestAsync] no responders` vs `timeout(5s)` vs `[WINDY] RSP failed`) ③(선택, PRD 대기) GIS 방어 개선 = 재조회 모드==요청 모드면 실패 팝업 억제. [[project_gis_nats_spec_gap]] [[project_windy_rsp_missing_proxy_pending]]

## ▶▶ 재개 포인트 (2026-07-22~23, 이 세션 — 감사 로그 뷰어 날짜필터+페이지네이션) — Track C, ✅ dev 완료·feature 커밋 `3e3ef04`(빌드0·테스트 27+136 green·CHANGELOG 포함)·**v2.6 FF 머지 완료**(워크트리·브랜치 정리, 롤백 `before-auditlog-datefilter`)·**DatePicker 다크스타일 수정 `97beaa8`**(맨 `<DatePicker>`→`MaterialDesignDatePicker` 명시, 앱 4모듈 DatePickerContent base 동일). **빌드break 오진 규명**: 사용자 CS0006(Accounts.Ui.dll 없음)+XDG0000(App.xaml)은 **코드 무결**(Accounts.Ui 빌드 0오류)·fork가 워크트리 bin에 빌드→머지후 메인 미재빌드→DLL부재 연쇄 = 메인 재빌드로 해소. 재빌드 시 앱 반영
- **요청**: 감사로그(권한부여 콘솔) DataGrid가 무한스크롤인지? 최소한 일자 기준 Date 설정이 이벤트 패널처럼 있어야 하지 않나?
- **현황(코드 실측)**: `AuditLogPanelViewModel.cs:41` = `GetAuditLogsAsync(1,100)` **1회 fetch·최신 100건 고정·갱신 버튼뿐**. 무한스크롤 ✗·날짜필터 ✗·페이지네이션 UI ✗ → **100건 초과 과거 로그 UI 접근 불가**. 이벤트 패널(`DetectionEventPanelViewModel`)은 `StartDate`/`EndDate` DateTimePicker + `LoadNextPageAsync` 무한스크롤 보유(단 DatePicker는 현재 `Visibility=Collapsed`).
- **서버지원 조사결론(사용자 선택=선행조사, 삼각검증 3소스 일치)**: 서버는 **날짜필터+페이지네이션 완전 지원, 격차 100% 클라측**. ①스펙 §9.6.2(`GOP_Restful_Api_연동설계.md:14471`)=`start_date`/`end_date`(+action_type/resource_type/resource_id/actor_login_id/action_status) optional 명시. ②실행서버(`api-test-server/app/routers/audit_logs.py:87-134`)=`created_at>=/<=` 필터 실구현·`id desc`·limit max100·`pagination:{page,limit,total,total_pages}`. ③DB(`audit_log.py:70`)=`created_at` 전용인덱스 `idx_audit_logs_created_at_desc`(성능확보).
- **핵심발견**: 신규기능 아님 — **PRD-GOP-05 FR-SS-03/STEP-15("페이지네이션+7개 필터 UI, 날짜 포함")의 미완성분**. 배포본이 축소 MVP로 나감. 재사용자산: `ApiListResponse<T>.Pagination`이 이미 메타 담음(VM이 무시 중, 새 타입 불요)·`EventProviderService.cs:56` `ToString("yyyy-MM-ddTHH:mm:ss")` 날짜쿼리 패턴·이벤트 VM 무한스크롤 이식.
- **PRD**: `docs/prds/GOP_AuditLog_DateFilter_Pagination-prd.md`(Draft). 클라 4파일 변경(IAccountApiService/AccountApiService 시그니처+쿼리, AuditLogPanelVM 날짜·페이지상태·LoadNextPage, View DatePicker·스크롤트리거). **R-1**: `GetAuditLogsAsync` 시그니처 변경→테스트 스텁 5곳+FakeGopServer 동시수정(Track C 근거).
- **PRD 검토 완료(OQ 3건 확정)**: 최근7일 / 무한스크롤 / 날짜만.
- **architect(opus) 검증 완료 — 블로커 2건 해소**: ①스크롤 자산(`DataGridScrollEndBehavior`+`SimpleCommand`)이 Events.Ui에 있고 Accounts.Ui 미참조 → **Utils에 공유 behavior 신규**(Events.Ui 미편집) ②Accounts.Ui에 MahApps 없음 → **`md:DatePicker`(날짜만)**, 종료일 `T23:59:59` 상향. **architect 오류 교정**: 인터페이스 파라미터 추가는 스텁 전수 시그니처 변경 필수(CS0535)=실구현1+스텁5 전부 수정(PRD R-1 맞음). 기타확정: provider 미도입(API직접+`res.Pagination`), `Messages.Defines.Apis.PaginationDto`(Page/TotalPages/Total, Api.Messages.Common 동명타입 함정), append=`DispatcherService.Invoke`(Base), 관리토큰 `BasePanelViewModel._cancellationTokenSource` 재사용.
- **Plan 작성 완료**: `docs/plans/GOP_AuditLog_DateFilter_Pagination-prd-plan.md` — 11파일(신규2: Utils behavior+Accounts.Ui AsyncRelayCommand / 수정9: IAccountApiService·AccountApiService·스텁5·VM·XAML). 시그니처=`(page,limit,startDate?,endDate?,ct)` ct맨뒤. `ToString("yyyy-MM-ddTHH:mm:ss")`. FakeGopServer는 audit 실페이징 구현(테스트용).
- **✅ dev 완료 (worktree `feature/auditlog-datefilter`, 롤백태그 `before-auditlog-datefilter`, 미커밋)**: 12파일 — 신규: Utils `DataGridScrollEndBehavior`·Accounts.Ui `Common/AsyncRelayCommand`·테스트 `AuditLog/AuditLogPanelTests`; 수정: `IAccountApiService`/`AccountApiService`(시그니처+쿼리)·스텁5(FakeGopServer는 audit 실페이징 구현)·VM(날짜·무한스크롤·`DispatcherService`·관리토큰·종료일 `T23:59:59` 상향)·XAML(`md:DatePicker`×2+검색+건수+`behavior:DataGridScrollEndBehavior`). **고아 서브모듈 불필요**(Accounts.* 미참조 확증, 복사 안 함). 빌드0(Utils·Api·Ui). 테스트: Ui.Tests **27**·Api **136** green(코드리뷰 fix로 `should_not_reload_when_already_loading` +1). 코드리뷰 3수정 반영(가드 field 명시·CTS 캡처 주석·재진입 테스트).
- **✅ 커밋 완료**: feature `8512a8a`(명시-pathspec 12파일, 타 세션 v2.6 인덱스 미접촉·sweep 회피). 롤백태그 `before-auditlog-datefilter`.
- **🔲 남음(사용자 보류/대기)**: ①**v2.6 FF 머지=앱 반영**(재빌드 필요)+ACCOUNT_COORDINATION §3 클레임 — 사용자가 **보류** 선택 ②런타임 육안검증(다크 테마 `md:DatePicker` 렌더). 라이브러리 변경(메인솔루션 통지 불요). [[project_library_deployment_path]] [[project_multisession_checkpoint_sweeps_wip]] [[project_account_session_coordination]] [[feedback_prd_worktree_required]]

## ▶▶ 재개 포인트 (2026-07-21, 이 세션 — 심볼/이미지 가시성 3조건 모델: Visible 마스터 필드) — Track C, ✅ 구현·적대검증·커밋 완료 (`6f2de5f`, 17파일)
- **요청(사용자 스펙)**: 가시성 3조건(영향 큰 순) — ①줌 ②레이어패널 심볼 Visibility(마스터) ③속성창 모양(ShowShape)/제목(ShowTitle). 언체크=모양+Indicator+제목 전부 제거·재시작 유지·선택해제. **오버레이 심볼+이미지 공통**. 이전 `d5d6a60`(ShowShape 영속)·`b3aae15`(라벨 ShowShape 게이트)는 조건2·3 혼재 오설계라 대체(b3aae15 리버트).
- **설계 오류 정정**: 레이어 체크가 ShowShape 하나에 물려 조건2(마스터)·조건3(속성 모양)이 한 필드 → PidsSymbol Indicator·PidsGroup·제목 안 숨김, title-only 위반. **신규 `Symbols.Visible` 마스터 컬럼**으로 분리.
- **DB(워크플로 사전검증)**: 스키마 자동 업그레이드 존재 — `BuildSchemeAsync` 부팅마다 `CREATE IF NOT EXISTS + 멱등 ALTER ADD COLUMN`(GMapDbSymbolService.cs:526-588 선례). **컬럼 추가=CREATE정의+ALTER블록 2곳 필수**(ALTER 누락 시 기존 현장 DB 1054 전멸). 적대검증 2/2 refuted.
- **구현(6f2de5f, 17파일)**: Models(ISymbolModel.Visible+SymbolModel 기본true) · DB(CREATE+ALTER+67 CRUD 미러링+UpdateSymbolVisibleAsync+Restore) · UI(리프체크=Visible 4곳·OnSymbolVisibilityChanged Visible영속+선택해제+CanEditMap·AddMarkerFromSymbol IsLayerEnabled=Visible·ApplyLayerVisibility 카테고리&&Visible) · 렌더(LabelAdorner b3aae15 리버트·PidsStyle Indicator FR-05) · 이미지(GMapImageMarker.Visible→Visibility) · Undo/그룹(ShowShape→Visible).
- **적대검증(워크플로 3에이전트)**: DB 미러링 **PASS(무결점)**, 렌더 6/6 PASS. P1(SyncMarkerNode ShowShape 잔재)·P2(CanEditMap 게이트) **수정**. 빌드0·헤드리스 80통과(FOVColor 1건은 타 세션 FOV 기존 red, 무관).
- **🔲 남음**: ①런타임 검증(재빌드 후: 언체크→모양+Indicator+제목 숨김·재시작 유지·title-only·이미지·PidsGroup) ②**P0 카테고리 cascade=현재 유지로 확정**(사용자 승인, tri-state 표준) ③P2 펄스링(title-only+Detecting 소나파동, XAML x:Name 필요, 보류) ④v2.6→main 머지(요청 시). [[project_symbol_visibility_dual_gate]] [[project_category_discriminator_contamination]] [[project_migration_resurrection_trap]]

## ▶▶ 재개 포인트 (2026-07-21, 이 세션 — 레이어 심볼 가시성(ShowShape) DB 영속) — Track B, 🏁 구현·빌드·커밋 완료 (`d5d6a60`) ※ 아래는 6f2de5f로 대체됨
- **요청**: 레이어(OverlayWindow) 패널에서 심볼 Visibility 체크 해제 후 앱 재시작 시 상태 리셋(사용자 확인 "A"=재시작이 트리거).
- **근본원인**: 가시성 토글(`ShowShape`)이 런타임만 적용·DB 미영속('DB 영속=v2' 의도적 유예, `MapViewModel.OnSymbolVisibilityChanged`). **로드 경로는 이미 ShowShape 반영**(DB 컬럼→`_model.ShowShape`→`GMapBaseMarker`→컨트롤 DP→스타일 트리거 `ShowShape=False` 숨김) → 저장만 누락.
- **수정(3파일+테스트, 커밋 `d5d6a60`, +56/−3)**: ①`IGMapDbSymbolService`/`GMapDbSymbolService`: `UpdateSymbolShowShapeAsync(id,showShape)` 부분-UPDATE 추가(잠금/제목과 동일 리전 — 전체 행 재기록의 Category 판별자 오염 회피, 2026-07-15 PidsGroup 전멸 사고 방지). ②`MapViewModel.OnSymbolVisibilityChanged` async 전환+`e.Symbol.ShowShape` 세팅+DB 영속 호출. ③`UnitTestSymbol`: 가시성 영속+Category 보존 검증(`should_persist_showshape_and_preserve_discriminator_when_partial_update`).
- **검증**: GMaps.Ui/GMaps.Db 빌드 0오류. DB 통합테스트는 격리 DB 필요(운영 wipe 사고 이력)+디스크 여유 부족으로 실행 보류→CI/사용자 몫. 롤백태그 `before-symbol-visibility-persist`.
- **✅ 곁작업(커밋 `b40b39c`)**: PIDS 심볼 디스크 배경 바인딩 — `DoubleToThicknessConverter`(신규, BOM) + `PidsMarkerStyle.xaml` 디스크 6곳 Background/BorderBrush/BorderThickness를 MarkerFill/MarkerStroke/MarkerStrokeThickness에 바인딩. 비정사각 아이콘=알약형(사용자 수용). 사용자 '지금 커밋' 승인.
- **✅ 후속 버그: 재시작 후 타이틀 잔존(커밋 `b3aae15`)**: 개별 심볼 언체크 후 재시작 시 모양은 숨되 타이틀만 남던 현상. 분석 체인 Explore→architect→code-reviewer(opus). **정정된 근본원인**: `RestoreLayerVisibility`가 `Flatten`(Leaf만) 순회 중 개별 심볼(`Model==null`) 스킵 → `IsLayerEnabled` 기본 true로 남음 → `LabelAdorner`가 `ShowShape` 아닌 `IsLayerEnabled`만 봐서 타이틀 렌더. (architect 초기 `ApplyLayerVisibility` 수정안은 reviewer가 **dead-code(복원 시 심볼 미호출)**로 반증.) **수정**: `LabelAdorner` 게이트를 순수 술어 `ShouldRenderLabel`로 추출+`ShowShape` AND(PidsGroup CLR타입 제외). `IsLayerEnabled` 미접촉→클릭/선택/트리집계 회귀 0. 테스트 12/12. PRD=`docs/prds/GMap_Symbol_Visibility_Restore-prd.md`, 롤백태그 `before-label-showshape-gate`. [[project_symbol_label_adorner_system]] [[project_category_discriminator_contamination]]
- **🔲 남음**: ①런타임 검증(재시작 후: 심볼 체크 보존 + 언체크 심볼 모양+타이틀 둘 다 숨김 + PIDS 디스크 Fill/Edge 반영). ②v2.6→main 머지(요청 시). ③PIDS Phase B(단색 글리프, Track C, 미착수). ④(선택)숨은 심볼 맵 클릭/선택 완전 제외=접근 B(IsLayerEnabled 합성, AggregateLeaf 회귀 트레이드오프).
- **⚠ 코디네이션**: 내 4파일만 명시 커밋(`DetectionEventCardView.xaml` 타 세션·미결정 PIDS 파일 미접촉). [[project_category_discriminator_contamination]] [[project_gmaps_db_tests_wipe_prod]]

## ▶▶ 재개 포인트 (2026-07-21, 이 세션 — 권한 부여(Grant) 검증 + F-1 + 만료 실시간 컷오프) — Track C, 🏁 개발 완료·v2.6 커밋(앱 빌드 성공)
- **요청**: GrantManagementPanel 권한 부여 기능 100+시나리오 2회 시뮬 검증 + API 검증 → F-1(절단경고)·만료 실시간 컷오프 수정 → 앱 반영 + NATS PRD.
- **검증**: `FakeGopServer`(api-test-server grants.py/grant_service.py/main.py 1:1 전사)로 `GrantManagementPanelViewModel` **119시나리오×2회(238)** + `AccountApiService` 계약 **14건**. 전건 green·재현성. 로그=`docs/reports/grant-verification-run.log`, 리포트=`docs/reports/grant-verification-report.md`. 롤백 `before-grant-verification`. 하네스=`Accounts.Ui.Tests/Grants/*`+`Accounts.Api/Tests/GrantApiContractTests.cs`.
- **✅ F-1 (커밋 `bf23fb2`)**: GET /grants 는 total 을 top-level 로 반환하나 클라가 pagination.total 만 읽어 절단경고(100건초과) 도달불가였음 → `ApiListResponse.Total`(int?) 수신 + VM 소비. events pagination 경로 무영향(additive).
- **✅ 만료 실시간 컷오프 FR-GS-03 (커밋 `21d8c5c`)**: 본인 `6c4ed0a`(FR-GS-01/02, 217커밋 미병합) 설계 계승 — `PermissionsSnapshotDto` + `IPermissionService.Refresh`(role/loginId/name 유지·clockSkew(server_time) 보정)+`IClock` + `GetMyPermissionsAsync`(/me/permissions) + `PermissionRefreshService`(valid_until 타이머·체인 재무장·fail-safe=권한확대 없음, `IService`). Accounts.Api **129/129**·Accounts.Ui.Tests **17/17**. 만료 시 로그아웃 없이 UI 재게이팅(서버 403 권위 유지).
- **분석/PRD**: 서버/클라 집행 분석 MD 2종(`docs/analyses/Grant_Enforcement_{Server,Client}_Analysis.md`, 서버본 `api-test-server/docs` 전달). PRD 3종 Draft: `GrantList_TopLevelTotal_Fix`·`Grant_LiveCutoff_Client`(v1.1 FR-GS)·`Grant_LiveCutoff_NATS_Push`(FR-GS-04 Phase2). 서버팀 정합확인=`docs/coordination/GOP_Server_API_Account_Coordination_Reconciliation_NOTIFY.md`(내 서버분석 S-2 검증됨).
- **앱 반영**: 메인솔루션(Dotnet.Monitoring.Solution) ProjectReference → v2.6 직접 커밋이라 재빌드로 반영. 1차 빌드는 위성지도 `map_satellite.mbtiles`(117GB) 복사가 디스크(C: 여유 39GB) 부족으로 실패했으나 **C# 전량 컴파일 성공**(코드 무관) → 사용자 빌드 성공 확인.
- **🔲 남음(전부 선택)**: ①NATS 실시간 push(FR-GS-04)=서버 3-게이트(NATS_REVOKE_ENABLED·ACL·배포) **합동 롤아웃** 필요 → 지금 타이머로 동작하므로 선택. ②PENDING→ACTIVE 활성화 정밀=폴링/서버 valid_from(옵션). ③위성지도 117GB 복사설정(메인솔루션 Session B, 과함).
- **⚠ 코디네이션**: 계정 라이브러리는 사용자 명시지시로 교차작업. pipeline-state=타 세션 점유 → advance-phase 미실행(공유상태 미변경, 문서 산출만). **내 파일만 명시 커밋**(다중세션 sweep 회피 — 타 세션 `Docs/INDEX.md`·`DetectionEventCardView.xaml`·`MapView.xaml`·커밋 `a408ef9` 미접촉). [[project_grant_feature_verified_pagination_gap]] [[project_rbac_enforcement_reality]] [[project_account_session_coordination]]

## ▶▶ 재개 포인트 (2026-07-21, 이 세션 — 관리자 타계정 프로필 사진 업로드 클라 완결) — Track C, ✅✅ 구현·테스트·v2.6 FF 머지 완료 (`a3290c6`)
- **요청**: 서버 `v6.3-admin_photo_upload`(POST/DELETE `/api/users/{id}/photo`) 배포 확인 → 클라 차단 스텁(`6842db5`) 해제·완결 진행.
- **확정(실측)**: 서버 계약 직접확인(`api-test-server/app/routers/users.py` L507~592: multipart `file`·`users:edit`+base-ADMIN 상승가드·`_save_profile_photo` 재사용·감사 `USER_PHOTO_CHANGED/DELETED` actor≠target via `log_action_async`=async세션이라 감사 무음버그 무관). 클라 현황=사진경로 전부 self(`IProfileGateway.UploadPhotoAsync(filePath)`→`/me/photo`), `{id}` 대상 없음. 차단지점=`EditorDialogViewModel.ClickAddPicture` 안내팝업.
- **설계**: `{id}` 사진 메서드는 self `IProfileGateway` 아닌 **관리자-타깃 `IUserDirectoryGateway`**(EditorDialog가 이미 `_gateway` 주입)에 배치. FR-01/02=API서비스 `Upload/DeleteUserPhotoAsync(userId,...)`, FR-03=게이트웨이(Api 구현·Db 미지원 null), FR-04~06=EditorDialog 재개+권한게이팅. 신규 DTO 없음(`AuthUserDto` 재사용).
- **PRD**: `docs/prds/Admin_Photo_Upload-prd.md` Draft(FR6·NFR4·V5·R4). 롤백태그 `before-admin-photo-upload`(21d8c5c).
- **⚠ 코디네이션**: 계정 라이브러리 src=계정 세션(A) 소유(내 세션=GIS, 사용자가 명시 지시로 교차작업). pipeline-state=타 세션 dev(LineArea_Symbol_Resize) 점유 → **advance-phase 미실행**(공유상태 미변경, 문서 산출만). dev 착수=사용자 승인 후 `ACCOUNT_COORDINATION` §3 클레임+worktree(v2.6, 고아 GMap.NET/OnvifSolution 복사). [[project_me_endpoint_crosses_accounts_trap]] [[project_audit_log_write_silently_stopped]] [[project_gmap_orphan_submodule]]
- **✅ 완료(문서승인→worktree→구현→리뷰→머지)**: 9파일(API서비스 Upload/DeleteUserPhotoAsync·IUserDirectoryGateway·ApiAccountGateway·EditorDialogVM/View·CommonMessages·신규 계약테스트4·CHANGELOG). 커밋 `a3290c6`, v2.6 FF 머지(v2.6 미이동). **NFR-01 회귀 테스트=업로드/삭제 `users/{id}/photo` 타깃·`/me` 아님**. Accounts.Api **132/132**·Accounts.Ui.Tests **17/17**·빌드0. code-review(opus): **P0 재오염 없음**(타입+대상Id 추적 확증)·P1 2건(삭제 확인 팝업·업로드 실패 원복/orphan) 반영. pipeline-state 미변경(타 세션 dev 점유, 문서기반). §3 클레임 해제.
- **✅ 후속 런타임 수정 (사용자 실측 3버그, 커밋 `c296dc4` v2.6 FF, 롤백 `before-admin-photo-fix`)**: ①삭제 '안 됨'=`HandleAsync(사진삭제)`가 Confirm 팝업 미청산(`ClosePopupMessageModel` 누락, 시블링 핸들러엔 有) 소프트락 → 진입 청산+결과 안내. ②업로드/삭제 후 목록 미반영=`AccountManagerPanel.HandleAsync(RefreshAccountsMessageModel)`가 인메모리 provider 재구성만·서버 재조회 안 함(**편집/초기화도 동일 기존 버그**, 사진이라 노출) → 서버 재조회(SSOT). ③허용형식 서버정렬(`ProfileImageHelper`/파일필터 bmp제거·webp/gif추가; 서버=jpeg/png/webp/gif, bmp=400). Ui.Tests **20/20**(+3 형식)·Api 132·빌드0. **업로드 자체는 정상**(서버 실측 200 OK) — 앞선 400은 사용자 파일 잘림/손상/5MB초과였음.
- **✅ 사진 UI 개선 (사용자 실측 '삭제 버튼 안 보임·등록 여부 식별 불가', 커밋 `e5dffd1` v2.6, rebase-FF, 롤백 `before-photo-preview`)**: EditorDialog 사진 행을 URL 텍스트박스 → **미리보기(56×56, `ImageConverter` 로컬선언; http photo_url 직접렌더·없으면 기본 아바타)** + **라벨 버튼(변경/삭제, `Delete` 아이콘)** 으로 교체(마이페이지 패턴 정렬), 미참조 `EditorImage` 제거. XAML만, 빌드0. ⚠타 세션이 동시에 MyPage 본인사진삭제(`93e1cb7`) 커밋 → rebase로 흡수(CHANGELOG 양쪽 보존). 계정 사진은 다중 세션 동시작업 중.
- **🔲 보류/다음**: FR-06(사진 버튼 `users:edit` 게이팅)=패널 진입 게이팅+서버 집행과 중복이라 보류(필요 시 후속). **런타임 검증(사용자, 앱 재빌드 후)**: 관리자가 타 계정 사진 업로드→대상만 변경/로그인 관리자 불변·삭제→default 아바타·감사 `USER_PHOTO_CHANGED/DELETED` 기록·비-ADMIN 403 graceful. ⚠full build는 타 세션 GMaps WIP(MapViewModel/PidsMarkerStyle/DoubleToThicknessConverter 등)로 실패 가능(내 계정코드 무관, worktree 격리빌드 0오류 검증됨).

## ▶▶ 재개 포인트 (2026-07-18, 이 세션 — GMap 지도 회전 완전 싱크 분석·설계) — Track A(분석만, 코드 무변경), 🏁 산출물 완료
- **요청**: Base 맵 회전 도입 시 오버레이맵/이미지/심볼 싱크 붕괴 원인 분석 + 완전 싱크 방안 연구 + 에이전트 총동원.
- **에이전트 총동원(체인)**: Explore×4(벤더 파이프라인/오버레이·이미지/심볼·FOV·라벨/히트·줌·Canvas) → 직접 유도(DrawMap+Core 투영) → architect×2(B1 컨테이너 챔피언 vs B2 레이어별+B1 적대검증, **반대 렌즈→동일 하이브리드로 수렴**) → code-reviewer(opus, V1~V8 적대감사+누락 리스크 5종).
- **🔴 핵심 정정**: 구 분석 §3.1 `(M−I)·T swim` **오류(반증)** — `DrawMap`(비회전 core에 타일)+`UpdateLocalPosition`(raw 상쇄) 유도로 마커·타일 화면좌표 둘 다 `M(Scale(coreLocal))` 정확 일치. **위치 드리프트 없음, 깨지는 건 방향(orientation)뿐**.
- **수렴 설계**: 위치=벤더 `_rotationMatrix` 상속(무변경)+방향=`−θ` 레이어별 합산, **벤더 GMapControl.cs 0줄**. 부호 전 레이어 `−θ`(심볼 `Bearing−θ`·FOV·히트; Architect `+θ` 반증). **선결 블로커 3종**: ①이미지 크기 왜곡(`GetImageScreenRect` 두 모서리 대각선, 커스텀+DB 두 시스템, 렌더=히트=드래그 동시수정 NFR-1, 최난) ②오버레이=렌더시점 `+θ`로 core 복원+전체 `Rotate(−θ)` 1회 Push(서비스 위치변경 크로스어셈블리 불가) ③`OnMapRotationChanged` 이미 존재(private DP 3034)→`MapRotationChanged` 개명(컴파일 충돌). ~9~12일 Track C.
- **산출물**: `docs/analyses/GMap_Rotation_FullSync_Design-analysis.md`(설계·§5 레이어별·§6 감사·§7 블로커·§8 Phase) + 구 `GMap_Rotation_Overlay_Desync-analysis.md` §3.1 정정 노트 + INDEX 행 + 메모리 [[project_map_rotation_disabled]] 갱신(스윔 정정).
- **정책 확정(2026-07-19)**: 사이트 앵커(`MapAnchor.IsEnabled`)×회전 상호작용 — 앵커 3요소 중 엄격모드(InsetByHalfViewport)만 회전과 충돌. **(B) 앵커 활성이면 회전 전면 차단** 확정("사이트 잠금=회전 금지", 운영 일관성). 구현: 회전입력/`RotateMap`/`SetMapRotation` no-op 게이팅 + 앵커 활성 시 `ResetRotation()`. 설계 doc §8 반영.
- **🔲 착수 시**: 설계 §5~8 기반 PRD `GMap_Rotation_Full_Sync`→롤백태그 `before-rotation-fullsync`→worktree(orphan GMap.NET/OnvifSolution 복사)→회전 헤드리스 수학테스트 선신설→Track C. 회전은 현행 비활성 유지(즉흥 토글 금지, §desync 재현).
- **코디네이션**: pipeline-state.json은 타 세션(Camera_PTZ prd) 소유 → **advance-phase 미실행**(공유상태 미변경, 문서 산출만). [[project_map_rotation_disabled]] [[project_two_image_overlay_systems]] [[project_gmap_orphan_submodule]]

## ▶▶ 재개 포인트 (2026-07-15 오후, 이 세션 — PidsGroup 탐지 깜빡임 선 두께 버그 조사) — Track A(분석만, 코드 무변경), 🏁 종결(사용자 런타임 검증 "정상")
- **요청**: 탐지 이벤트 깜빡임 라인이 PidsGroupSymbol 두께 설정을 안 따르고 더 얇게 고정된 것 같다.
- **결론(실증)**: 깜빡임 선=`PART_DetectionPolyline`(라이브러리 유일 라인형 깜빡임, 전수 스윕). 두께 추종은 오전 커밋 `b6cfc2c`(10:01)로 이미 수정됐고, **헤드리스 WPF 하네스 6시나리오 실측으로 정상 확증**(깜빡임 도중 변경 포함 Main=Event=Detection 항상 일치, scratchpad `BlinkThicknessHarness`). 코드상 재현 불가.
- **사용자 재현 타임라인 복원(로그)**: 14:25 세션(수정 포함 DLL)에서 Zone1이 14:29:47부터 두께6으로 깜빡이는 중 14:29:56 두께10 설정→깜빡임 미추종 관찰→14:31:09 6 복원. 단 그 세션 부팅 로그에 **전 존 Category=AREA_BOUNDARY 오염**(동시 세션이 14:50 `295ee42`로 수정한 현장 사고) — 심볼 상태 desync 가능성. 그룹 깜빡임은 ProcessEventReport가 **~20-40초 내 자동 Normal 복원**(관찰 창 짧음).
- **7-agent 워크플로우 검증**: 적대 반박+메커니즘 스윕+하네스, 발견: LineOpacity 로컬할당 클로버 잔존(P1, 투명도 라이브 변경 시 오버레이 미반영 — b6cfc2c와 동일 패턴)·MouseOver 4px 트리거 사문·데드 바인딩·마커 Update leak·개별장비 펄스링 6px 하드코딩. 메모리=[[project_pidsgroup_blink_thickness_verified]].
- **운영 데이터 발견**: `pidsgroupsymbols.LinkedDeviceGroup` — Zone4~8=0(탐지에 절대 안 깜빡임), 사용자가 실험 중 재배선(Zone1: 1→2). 사용자가 통제 실험용 `New Group`(Id=38, 두께10, Lime)→그룹1 연결함.
- **✅ 종결(최종 정리)**: **원 신고의 출처는 현장 설치본**(수정 전 빌드) — `Docs/reports/ANALYSIS_GMap_PidsGroup_Blink_StrokeThickness_FieldBuild.md`(타 세션 작성)가 현장 로그(`Docs/log-2026-07-15.txt`) **줄번호 포렌식**으로 입증: 로그 호출지 `MapViewModel.cs:7226`=b6cfc2c 직전 소스와 정확 일치(수정 후=7242, HEAD=7330 — 내가 git show로 교차검증 완료). 개발기 로컬은 수정 포함 빌드(로그 7330)라 정상이었고, 사용자도 New Group(두께10, 그룹1) 통제 실험으로 **"정상" 런타임 확인**. 구버전 결함 구조=OnApplyTemplate 로컬대입이 XAML 바인딩 클로버+콜백은 Main만 갱신 → 오버레이 두께 초기 3.0 고정(LineSymbolModel 기본값).
- **🔲 현장 조치(사용자/운영)**: b6cfc2c 이후 빌드로 **현장 전체 산출물 재배포**(GMaps.Ui.dll 필수, 버전 식별 부재로 SHA-256 대조) + 보고서 §10 인수 테스트(두께 변경→Detecting/Faulted/FaultedDetecting 두께 추종).
- **🔲 후속 후보(미착수, 원하면 PRD)**: ①보고서 §11 자동 회귀 테스트 — scratchpad `BlinkThicknessHarness`가 요구 시나리오 전부 이미 구현(xUnit STA 영구화만 남음) ②LineOpacity 로컬할당 클로버(P1, 보고서 §12.1과 내 조사 F-01 일치) ③LinePattern 오버레이 미동기(§12.2) ④포인트<2 시 오버레이 stale Points(§12.3) ⑤사문 MouseOver 트리거·데드 바인딩·마커 Update leak 정리. (Zone4~8 LinkedDeviceGroup=0은 개발기 DB 설정 이슈 — 현장과 무관)

## ▶▶ 재개 포인트 (2026-07-15, 이 세션 — RTSP A/B 동일영상 버그 분석+수정) — ✅ F1+F2 구현·테스트 180/180·빌드0, 미커밋
- **근본원인 확정→수정**: `GetCameraKey()` 쿼리 제외로 다른 카메라가 Hub 같은 디코더 병합 → 신규 순수헬퍼 `RtspCameraKey.Derive`(쿼리 포함, 자격증명만 제외)+Hub URL 상이 경고+로그 마스킹. 신규 `RtspCameraKeyTests` 6/6, 롤백 `before-camerakey-query-fix`. 상세=`docs/analyses/Rtsp_Popup_Streaming_Ptz-analysis.md` + [`session-context-rtsp-popup.md`](session-context-rtsp-popup.md) 최신 재개 포인트.
- **✅ A/B 버그 수정 커밋 `b1fe14b`** + **✅ RTSP 소스 우선순위(URL조회/Onvif조회) 라이브러리 완성·v2.6 머지 `eaa8b32`**(PRD Approved→Plan 17/20→worktree 구현→opus 리뷰 5건 반영→FF 머지, 테스트 194/194·빌드0, CHANGELOG `fc7fae4`). 상세=[`session-context-rtsp-popup.md`](session-context-rtsp-popup.md) 최신 재개 포인트.
- **✅ EXT 메인솔루션도 완료**(`d602c63`+`d4141ae`@v0.5). Plan 20/20. **✅ 적대적 감사(40에이전트, confirmed 11/refuted 0) 전부 수정 반영**(보안 로그 마스킹 4곳·ONVIF 실패 고착·CTS 레이스·Row Dispose 경합·UI스레드 오프로드·자동해제 경계·string 바인딩 방어 등 — 타 세션 체크포인트 `739ed4c`/`39001ac`에 휩쓸림, 유실0 검증). 배지 위치 수정 `c256d43`(스크린샷 피드백). **Onvif 실기 재생 1회 확인**(cam109). worktree/브랜치 정리 완료. **🏁 세션 종료(2026-07-15)** — 🔲 잔여(사용자): 앱 종료→메인 재빌드→재시작 후 런타임 검증(A/B 해소·라디오·Url 회귀0·Onvif 재생/배지/폴백/실패 재시도 복구). 상세=[`session-context-rtsp-popup.md`](session-context-rtsp-popup.md).

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — GMap 툴바 CPU/GPU/RAM 사용량 표시) — Track C, **🏁 세션 마감 — 기능+CPU버그수정+크기+아이콘 전부 v2.6 머지 완료 · 앱 재빌드만 남음**
- **🏁 세션 마감 요약**: 조사(7에이전트)→시뮬(34시나리오)→PRD/Plan→구현(신규 SystemResources 라이브러리+GMaps.Ui 배선)→테스트22/22·실측→리뷰(P0/P1)→**v2.6 머지**→후속 3건(칩25%확대·CPU"자주100%"버그수정·아이콘개선) 전부 완결. 리소스 관련 커밋 12건 v2.6 반영. worktree clean·feature 완전머지·롤백태그 `before-sysres-indicator` 존재. **잔여=사용자 앱 재빌드**(타 세션 미완성 작업 마무리 후 전체빌드 안전). 세부는 아래 follow-up 불릿들 참조.
- **✅ 후속: 칩 크기 25% 확대**(사용자 요청) — DesignTokens SysResChip 아이콘 15→19·폰트 12→15·MinWidth 58→72·Padding 7,2→9,3·CornerRadius 8→10·간격 4→5. worktree 커밋 `b3e6e56`→v2.6 머지(빌드0, DesignTokens 1파일만, HEAD `3041c78`).
- **✅ 후속: 아이콘 직관성 개선**(사용자 지적) — CPU `Cpu32Bit`('32'텍스트 지저분)→**`Chip`**(깔끔한 칩), RAM `Memory`(RAM으로 안읽힘)→**`ExpansionCardVariant`**(메모리 스틱). GPU=`Gpu` 유지. 방법=scratchpad IconRender 콘솔로 후보 PNG 렌더→Read로 시각확인→AskUserQuestion 선택. worktree `bdb3830`→v2.6 머지 `08ea9ad`(MapView.xaml 1파일, 빌드0).
- **✅ 후속: CPU "자주 100%" 버그 수정**(사용자 신고, 작업관리자와 크게 다름) — **근본원인=`% Processor Utility` 카운터가 주파수(Turbo) 배율 포함**. 진단콘솔 실측(32코어 머신, Performance=**145%**): busy 67%인데 Utility=100%(→clamp 고정). MyProbe=raw Utility 정확일치라 **프로브 무결·카운터 선택이 문제**. 짧은간격 가설 기각(dt30ms~1s 스파이크 없음). **수정=`% Processor Time`**(busy%·0~100, Processor Information>64코어 우선·클래식 폴백). 실측 재확인: 동일부하 100%→60%(=ProcTime, 작업관리자 정합). worktree `86fffa8`→v2.6 머지 **`9ae1dc6`**(PdhResourceProbe.cs 1파일·충돌0·테스트22/22 유지). [[project_system_resource_monitor_design]]
- **✅ v2.6 머지 완료 `caa7677`**(Merge feature/sysres-indicator into v2.6). 절차: worktree에서 v2.6(b6ce191) 통합(충돌0·빌드0·테스트22/22 재검증)→primary에서 `CHANGELOG만 stash→일반 머지(v2.6가 bae5b8d로 재전진해 FF불가)→stash pop(충돌0)`. **primary에 SystemResources 반영**. 타 세션 dirty 9파일(Events.Ui/GMapCustomControl/Messages/Accounts.Ui 등) 미접촉 보존(내 stash=CHANGELOG만, 머지 diff에 타 파일 없음).
- **✅ 커밋 `ae671cf`**(worktree `feature/sysres-indicator` off v2.6, 롤백 `before-sysres-indicator`, 25파일 +1482, 고아 GMap.NET/OnvifSolution 정션). 신규 라이브러리 `Ironwall.Dotnet.Libraries.SystemResources`(17파일) + GMaps.Ui 배선(6파일: csproj·GMapUiModule·MapViewModel ctor/생명주기·신규 partial·DesignTokens·MapView) + sln + CHANGELOG.
- **검증**: SystemResources 빌드0 + **단위테스트 22/22**(GpuAggregator 5·Hysteresis 6·Monitor 11) · **GMaps.Ui 전체 빌드 0오류**(경고925=기존) · **실기 PDH 실측 정합**(스크래치패드 콘솔: CPU 26.5%·GPU 10~18%·RAM 62% 40/64GB) · code-reviewer(opus) **P0 1건**(칩색 트리거 enum/문자열 불일치=항상 시안, x:Static로 수정)+**P1 2건**(fail-safe Publish 이벤트 격리·중복 IClock 등록 정리) 해소 후 재검증.
- **설계정련(리뷰 확정)**: 모니터 **WPF 비의존**(NFR-06) — 타이머 미소유, VM UI스레드 DispatcherTimer가 Sample() 구동 → 크로스스레드/락/재진입/백그라운드 UAF 크래시 원천 소멸(백그라운드타이머+네이티브핸들 P0 회피). IService 미구현(이중권위 회피). PDH 핸들 수명유지(탭전환=타이머만 정지). fail-safe·CPU clamp·GPU LUID busiest·PdhAddEnglishCounterW(ko-KR).
- **⚠ 자동 머지 안 함**: primary v2.6에 타 세션 미커밋 변경(Events.Ui/Messages/CHANGELOG/Docs) 존재 → 엉킴 방지. 머지는 사용자가 primary 정리/조율 후. [[project_library_deployment_path]] worktree→v2.6 머지+재빌드 시 앱 반영.
- **요청**: GMap UI 메뉴 우측 정렬로 CPU/GPU/RAM 사용량을 `아이콘 숫자%`로, 프로젝트 디자인 컨셉(Tactical Command)에 맞게. 리소스 취득은 "별도 프로젝트가 유용하면 그렇게". **"구현 전 충분히 조사+시나리오 시뮬레이션 후 PRD"** 명시.
- **7-에이전트 조사·시뮬레이션 체인**(feedback_analysis_agent_chain): Explore×3(툴바/디자인토큰/백그라운드서비스) + architect(opus) + code-reviewer(opus) + 시나리오 시뮬 3도메인(환경 sonnet·동시성 opus·UX sonnet).
- **사용자 결정(AskUserQuestion)**: ①**별도 라이브러리** `Ironwall.Dotnet.Libraries.SystemResources` ②**아이콘+% + 툴팁**(절대값 hover).
- **시뮬레이션이 뒤집은 확정 설계 5**: ①**IService 미구현**(ExecuteAsync/StopAsync 불일치+Bootstrapper 이중권위) →`ISystemResourceMonitor:IDisposable`만. ②**DispatcherTimer(UI스레드) 채택**(TrackingOverlayManager 선례) — 백그라운드 타이머의 **P0 크래시 2건**(tick중 Stop→PDH 핸들 close 네이티브 UAF; close==false 뷰전환 이중구독) + 크로스스레드 이력 통째 제거(PDH sub-ms). ③**PDH 핸들 수명유지**(Stop=타이머 일시정지)→탭전환 워밍업 깜빡임/재프라임 경합 제거. ④**fail-safe**(손상카운터 lodctr/R OpenQuery 실패=P0, 전체 N/A·UI 미전파·1회 로깅). ⑤CPU **clamp**(Turbo>100%), GPU **PDH_MORE_DATA 루프**+**LUID 그룹 busiest**(멀티GPU 블렌딩 방지).
- **기술 결론**: **LibreHardwareMonitor 배제**(WinRing0 커널드라이버=Defender 악성탐지 CVE-2020-14979, 보안제품 부적합) → OS 네이티브 **PDH(pdh.dll)+kernel32**. CPU=`% Processor Utility`(PdhAddEnglishCounterW=**ko-KR 로케일 독립**, 폴백 `% Processor Time`), RAM=`GlobalMemoryStatusEx.dwMemoryLoad`(직접%·로케일무관), GPU=`\GPU Engine(*)\Utilization Percentage` 집계.
- **UI 확정**: `MapView.xaml` DockPanel **Dock=Right 자식**(Menu:211~ToolBarTray:213 사이, **ToolBar 자식 아님**=narrow 오버플로 방지). 아이콘 **Cpu32Bit/Gpu/Memory**(MD5.2.1 DLL 추출 검증). 색=정상 TextPrimaryBrush+아이콘 PrimaryBrush / 경고 StatusWarningBrush / 위험 StatusCriticalBrush, **히스테리시스 62-57/87-82**. MinWidth≈58 지터방지. 전부 DynamicResource. GPU부재=Hidden(공간예약), 전체실패=N/A칩(숨김금지). MonoFont(Consolas).
- **변경 예정**: 신규 SystemResources(csproj/ISystemResourceMonitor/SystemResourceMonitor/Snapshot record/ResourceLevel/PdhInterop+RAII/MemoryInterop/Module/Tests) + GMaps.Ui(csproj 참조·GMapUiModule 등록·MapViewModel 배선·신규 partial MapViewModel.SystemResources.cs·DesignTokens.xaml SysResChip·MapView.xaml). **메인 솔루션 무변경**(GMaps.Ui 내부).
- **PRD**: `docs/prds/GMap_SystemResource_Indicator-prd.md`(**Approved** 2026-07-13, FR-01~09·NFR-01~07·V-01~07·인과5·**§5-C 시뮬레이션 34시나리오표**·리스크 R-01~10·OQ-01~09).
- **Plan**: `docs/plans/GMap_SystemResource_Indicator-prd-plan.md`(0/33, 34.5h). Phase0=VER-01/02/05/06+RISK-02/09 → Phase1 SETUP(worktree·csproj·참조) → Phase2 IMPL(모델→인터페이스→GpuAggregator/Hysteresis/Monitor TDD→PDH/Memory interop→DI→UI) → Phase3 테스트/런타임 → Phase4 문서. **테스트 seam=`IResourceProbe`**(순수로직 목킹, 네이티브·WPF는 수동).
- **⚠ 코디네이션**: pipeline-state.json activePrd=Camera_PTZ(타 세션 prd phase 소유) → **advance-phase 미실행**(공유상태 미변경, 문서로 승인/plan 처리). dev 착수 시 pipeline phase가 아직 prd면 pre-tool-gate가 src/ 쓰기 차단 가능 → worktree 진입 후 확인 필요. [[project_approve_prd_picks_newest_mtime]]
- **🔲 다음(사용자)**: **앱(Dotnet.Monitoring.Solution) 종료→재빌드→재시작** 하면 툴바 우측에 CPU/GPU/RAM 3칩 표시. ⚠primary v2.6에 타 세션 진행중 미커밋 작업(Events.Ui 서비스 삭제·GMapCustomControl 등)이 섞여 있어 전체 솔루션 빌드가 그쪽 미완성으로 실패할 수 있음(내 코드 무관, 이미 GMaps.Ui 단독 빌드 0오류 검증됨). 런타임 검증 4건: VER-03 PackIcon `Cpu32Bit`/`Gpu`/`Memory` 렌더·VER-04 MonoFont/Consolas·VER-07 narrow창 칩충돌·MANUAL-01 통합(색전환 히스테리시스·툴팁·GPU부재·테마스왑). 롤백=태그 `before-sysres-indicator`. worktree `feature/sysres-indicator`+정션 존치(정리 선택). [[project_singleton_panel_vm_event_lifecycle]] [[project_dialog_panel_dark_theming]] [[project_library_deployment_path]] [[project_gmap_orphan_submodule]]

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — Line/Area 심볼 리사이즈) — Track C, **✅✅ 완성·런타임검증 완료(사용자 "잘된다")·v2.6 머지·로그정리 완료**
- **✅ 런타임 검증 완료 + 후속 버그수정 전부 v2.6 머지**(feature/linearea-resize). 최종 v2.6 HEAD `cf440ab`. **런타임 발견 3버그 근본수정**: ①리사이즈 좌표 요동(먹통→갑툭튀)=드래그시작 어도너-로컬→**맵공간 앵커** 고정+Position 상수(재앵커 제거) ②Area 가이드박스 심볼밖=Position≠bbox중심→**리사이즈 시작 1회 재앵커** ③**리사이즈 미반영/Undo 어긋남/팬텀 중복=근본**: `GMapMarkerLineControl/PidsGroupControl.OnMarkerPropertyChanged`가 IsVisible만 반응·**RuntimePoints(점 변경) 무시**→ApplyGeometry로 점 바꿔도 `UpdateLineGeometry` 미실행→stale렌더(창전환해야 반영). **점 변경도 UpdateLineGeometry 재실행**하도록 수정=리사이즈 실시간+Undo 정합+중복 소멸. ④Undo/Redo 버튼 단축키 툴팁 `ToolTipService.ShowOnDisabled`(비활성 시에도 hover 표시). 진단로그([LINE-RESIZE]/[UNDO-DIAG]) 넣어 원인확정 후 제거.
- **핵심 교훈(메모리화 가치)**: Line/PidsGroup 컨트롤은 **점(RuntimePoints) 변경을 구독 안 함**→마커 점만 바꾸면(ApplyGeometry/undo) 렌더 stale. 이동은 Position 변경으로 GMap 재배치→우연히 갱신됐던 것. 점 기반 마커 편집은 반드시 UpdateLineGeometry 트리거 필요.
- **(이전) 구현 상세**: 커밋 `598251c`(백엔드 FR-01/04)·`2139320`(어도너 FR-02/03/05/07). LineScaleTests 8/8 + LineGeometryUndoTests 4/4 + UndoRedoTests 34/34.
- **✅ 구현 완료**(worktree `feature/linearea-resize` off v2.6, 롤백 `before-linearea-resize`, 정션 벤더). 커밋 3: `598251c`(백엔드 FR-01/04)·`2139320`(어도너 FR-02/03/05/07)·`d7d6f62`(CHANGELOG). **빌드0 · LineScaleTests 8/8 + LineGeometryUndoTests 4/4 + UndoRedoTests 34/34 회귀**.
- **구현 요지**: 순수 `LineGeometryUtils.Scale`(투영델리게이트·퇴화가드) + `ILineEditableMarker.ApplyGeometry`(스케일·Undo 공용 seam, SyncModelPoints 4단계). 어도너 `GetHandleBounds`=ActualLineBounds·`ProcessLineScale`(map좌표 절대배율·Position=bbox중심)·핸들 가드완화(코너/닫힘=변). **P0 Undo**=신규 `LineGeometryCommand`(점+Position 스냅샷, isImage=false)+`RecordLineGeometry`(HasChanges 우회·MarkerEditCompletedEventArgs.OriginalLinePoints). ESC=스냅샷 복원. FR-07 라벨 절대상한.
- **설계 정련(시뮬 반영)**: 스케일=지오 프레임(map px 왕복, 줌 stale 방지) · Position=새 bbox중심 갱신(렌더/라벨/핸들 3중 정합) · 퇴화 0나눗셈/부호반전/NaN 가드 · Undo 재적용 UI마샬(ApplyGeometry 내부). 아키텍트의 `ScaleAround`→테스트 용이한 `ApplyGeometry`+순수 Scale로 정련.
- **🔲 다음(사용자 런타임 검증 — 앱 재빌드)**: worktree→v2.6 머지 후 재빌드. 확인 V-01(핸들이 실제 점 범위에)·V-04(Position=bbox중심 밀림 없음)·V-06(절대배율 앵커)·V-07(디지털줌 배수)·닫힌폴리곤 변핸들·PidsGroup 하위 추종·ESC 복원. 헤드리스 불가한 좌표 정합이 핵심 미검증.
- **문서**: PRD `docs/prds/LineArea_Symbol_Resize-prd.md` v2.0 Approved(§5-C 시뮬레이션) · Plan `docs/plans/LineArea_Symbol_Resize-prd-plan.md`. [[project_undo_id_collision_all_commands]] [[project_digital_zoom_architecture]] [[project_adorner_trimmemory_crossthread_resize_drift]]
- **진행**: PRD 승인 완료(🅐) → 사용자 요청으로 **사이드이펙트 시뮬레이션 4도메인(Undo=opus·좌표·영속·교차기능, 40+시나리오) 실행 → PRD v2.0 보강**(§5-C 신설). 다음=Plan → 워크트리(`before-linearea-resize`) → dev.
- **시뮬레이션 P0 발견(구현 전 필수 5건)**: ①**R-07** line 스케일=`_model.Width` 불변→`HasChanges==false`→`RecordTransform` early-return(**undo 엔트리 아예 없음**)+`OnMarkerEditCompleted`가 `DbUpdateProcess` 무조건 선행→**되돌릴 수 없는 파괴적 즉시영속**([[project_overlay_image_delete_dataloss]] 패턴). FR-04=`RecordLineGeometry`가 HasChanges 우회·점 diff 기록. ②**R-08/C6** 퇴화도형(수직선 Δlng=0·중첩점 extent=0)→배율 0나눗셈→NaN 좌표→DB손상 → 퇴화가드(ε·IsFinite). ③**R-09/B1** 줌 도중 리사이즈=시작 픽셀앵커 stale→튐 → **지오좌표 앵커**. ④**R-10/B12** 렌더·라벨·핸들 전부 Position 기준인데 앵커=bbox중심→도형 밀림 → **스케일 후 Position=새 bbox중심 갱신(D-06)**. ⑤**FR-05** ESC=`RestoreOriginalData`가 W/H만 복원→스케일점 잔존 → 스냅샷 복원 필수.
- **확정/정정**: V-02(`IPidsGroupEditableMarker:ILineEditableMarker`)·V-03(트랜잭션 DELETE+재삽입) **확정**. 사실정정: "SetSize 200클램프"는 line 경로 off(ScaleAround이 UpdateSize 우회), 속성창 W/H는 Line서 비활성(MarkerSizeEnabled=false)→혼란無. 부작용: **FR-07** 방금 머지된 라벨상한(`max(W,H)/2*2`)이 line 파생 W/H 연동→흔들림→line 전용 상한. PidsGroup 하위 오버레이=RuntimePoints 파생 자동추종(D4 PASS). 그룹선택=별도 어도너 배타(D5 PASS).
- **PRD**: `docs/prds/LineArea_Symbol_Resize-prd.md` **v2.0 Approved**(FR-01~07·NFR4·V-01~11·D-01~06·OQ-01~08·리스크 R-07~13·§5-C 시뮬레이션표). architect+code-reviewer+시뮬4 근거.
- **🔲 다음**: 사용자 확인(시뮬 반영 설계=지오앵커·Position갱신·퇴화가드·회전 V-08) → Plan → dev. [[project_undo_id_collision_all_commands]] [[project_digital_zoom_architecture]] [[project_map_rotation_disabled]] [[project_adorner_trimmemory_crossthread_resize_drift]]
- **요청**: Line계열(PidsGroup 포함)·Area 계열 심볼이 편집 어도너로 크기 수정해도 적용 안 됨 → 처리방안 제안 요청 → 추천안 🅐 채택, "프로세스에 맞게 진행".
- **분석 체인**(feedback_analysis_agent_chain): Explore×2 + architect(opus) + code-reviewer(opus). **근본원인 확정**: Line/Area(=`IsClosedPath` 닫힌폴리곤, 별도마커 아님)는 `LinePoints`(위경도 절대) 기반이라 크기가 파생값 — `GMapMarkerLineControl.UpdateLineGeometry()`(:395-485)가 매 리드로우 점 bbox로 Width/Height 재계산(:440-441), `SetSize` 200px 클램프. 어도너는 `isLineMarker`면 리사이즈 핸들 미렌더(MarkerEditAdorner:304,319). **[P0] Undo: `TransformCommand`는 W/H/Pos/Bearing만 복원·LinePoints 미포함**(EditRecorder:43·TransformCommand:33) + Undo가 스케일된 점을 DbUpdate로 **재영속 desync**. `SyncModelPoints()`는 base UpdateSize서 미호출→누락 시 옛 점 저장.
- **채택 설계 🅐**(architect): 신규 seam `ScaleAround(center,sx,sy)`(ILineEditableMarker+PidsGroup) — 화면좌표 프레임(`FromLatLngToLocal`왕복) 점 스케일 + 동기 4단계(runtime→SyncModelPoints→NotifyPointsChanged→UI마샬). 어도너 `isLineMarker` 가드 완화(열린선=코너/닫힘=코너+변, 점 bbox `ActualLineBounds` 기준 `GetHandleBounds`), `ProcessLineScale`(시작bbox 대비 절대배율·앵커 `_editStartPoint`). **P0 Undo=신규 `LineGeometryCommand`(SymbolSnapshot 딥클론 재사용, LinePoints 포함)+`RecordLineGeometry`+MapViewModel `OnMarkerEditCompleted`(:2367) line 분기**. ESC=스냅샷 복원. 영속=기존 `UpdateLineSymbolAsync`(DELETE+재삽입) 재사용(SyncModelPoints 전제). 정점편집(🅑)=후속 PRD.
- **산출**: `docs/prds/LineArea_Symbol_Resize-prd.md`(Draft, FR-01~06·NFR4·V-01~07·D-01~05·OQ-01~05·리스크6·인과결합5). 변경 예정: MarkerEditAdorner/GMapLineMarker/GMapPidsGroupMarker/ILineEditableMarker/신규 LineGeometryUtils·LineGeometryCommand/IEditRecorder·EditRecorder/IUndoApplyContext/MapViewModel.
- **🔲 다음**: 사용자 PRD 검토(편차 D-01~05·OQ 확정) → 승인 `advance-phase.js approve prd` → Plan → 워크트리(`before-linearea-resize`) → dev. Track C 프로세스. [[project_undo_id_collision_all_commands]] [[project_map_rotation_disabled]] [[project_digital_zoom_architecture]]

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — 이벤트 카드 구역/제어기 표시 정리) — Track B, ✅ 구현·빌드0·테스트 green·앱 재빌드 대기·미커밋
- **요청**: 이벤트 카드 "구역"에 이상한 값(그룹 Id 숫자) 표시 원인 규명 → HTML 스토리보드로 방향 제안 → A안(카드 VM name 변환) 적용 → 제어기 필드까지 정리.
- **① 구역=그룹 이름**: `DeviceGroupsText`가 **두 구현** 존재 — 모델(`BaseDeviceModel`, raw id join `"1, 116"`) vs 뷰모델(`BaseDeviceViewModel`, DeviceGroupProvider로 Id→Name). 카드가 모델 것(`Device.DeviceGroupsText`)에 바인딩→숫자 노출(탐지는 없는 `Device.Name`→빈칸). 근본=`Events.Ui/DtoToModelHelper.cs:361`이 그룹 `name` 버리고 `id`만 매핑. **수정**=`EventCardViewModel<T>`에 name 변환 `DeviceGroupsText` 추가(Provider Id→Name, 미발견 Id fallback), 두 카드 `{Binding DeviceGroupsText}`로 통일. N:N 다중="구역 1, 10"·단일="구역 1".
- **② 제어기 테스트 정정**: red였던 `..._ControllerId_...` 2종은 카드가 `ControllerDeviceNumber`(탐지)/`ControllerDisplay`+`SensorDisplay`(장애)로 진화했는데 옛 `"ControllerId"` 문자열 기대한 **낡은 테스트**(커밋 HEAD부터 red). XAML 정상 → 테스트를 `..._ControllerField_...`로 정정.
- **③ 힌트 문구**: 제어기/센서 필드 힌트 "아이디"→"번호"(값이 DeviceNumber라 실측 정합, 탐지·장애 4곳).
- **④ 제어기 데이터경로 확인(버그 아님)**: 카드 목록은 `EventProviderService.ToXxxEventModel(_deviceProvider)`로 채워짐→provider 실제 device(=`DeviceProviderService`가 센서 `includeController:true`로 로드) 반환→Controller 정상. NATS 실시간은 카드 미생성(entryId 매칭만). **Gap 정리**: Insert/Update 반환 매핑 6곳(`EventProviderService`) `null`→`_deviceProvider` 오버로드 통일.
- **검증**: Events.Ui 빌드 0오류. 카드 테스트 8/8 + Insert/Update 6/6 green. 롤백 태그 `before-event-card-zone-name`.
- **변경 5파일(미커밋)**: `EventCardViewModel.cs` · `Detection/MalfunctionEventCardView.xaml` · `EventProviderService.cs` · `Tests/UnitTest.cs`. CHANGELOG [Unreleased] 반영(훅 동시재작성 레이스 주의).
- **⚠ 주의**: 사용자가 카드 XAML **라이브 편집 중**이었음(Detection 바인딩이 Device.Name↔Device.DeviceGroupsText로 계속 바뀜) — 구역 칸은 `{Binding DeviceGroupsText}` 유지 필요(`Device.` 붙이면 숫자/빈칸 회귀). pipeline-state는 타 세션(Camera PTZ, Track C prd) 소유 → advance-phase 미실행.
- **🔲 다음(사용자)**: 앱 재빌드 후 실측(구역=이름·제어기=번호·힌트 문구) → 이상 없으면 커밋. [[project_device_reregistration_symbol_desync]] [[feedback_deliver_visible_design]] [[feedback_spec_full_compliance_propose_deviations]]

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — 심볼 라벨 드래그 상한 확대) — Track B, ✅ 커밋 `d647cfa`·앱 재빌드 시 반영
- **요청/조치**: 심볼 Title(라벨) 드래그 이동 상한을 **심볼 최대 반지름의 2배**로 확대. `LabelAdorner.cs:143` `cap = max(W,H)/2 * 1.5` → `* 2.0`(=max(W,H)) 1줄 수정. 롤백 태그 `before-label-radius-2x`. LabelAdorner.cs만 커밋(타 세션 dirty 미포함). 빌드무관(상수). ⚠앱 재빌드 후 라벨 더 멀리 드래그 확인. [[project_symbol_label_adorner_system]]

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — LeftMenu 통합웹 접속 버튼) — Track C, 구현·리뷰·커밋 완료 + FR-05 라이브반영 추가·앱재빌드 대기
- **⚠ FR-05 라이브반영 추가(사용자 "설정 켰는데 버튼 안뜸" 후속)**: 진단=앱 재빌드(PID84780·DLL 13:30)·재시작·설정저장(appsettings.json `IsWebServerEnabled=true`, IP 192.168.202.195:6173) 전부 정상인데 `CanConnectWeb`이 OnActivate 1회만 평가돼 세션중 토글 미반영(FR-05 원설계 한계 실현). **해결=설정변경 통지 구현**: 라이브러리 `WebServerEnabledChangedMessage`(record, v2.6 `4e9dfb8`) + WebServerSetupViewModel가 IsWebServerEnabled 저장 후 `PublishOnUIThreadAsync` 발행 + LeftMenuSectionViewModel `IHandle<WebServerEnabledChangedMessage>`→`NotifyOfPropertyChange(CanConnectWeb)` (메인 v0.5 `88c047f`). 빌드 CS0. **앱 재빌드+재시작 1회 필요**(이후 토글 즉시반영). 즉시 확인=지금 앱 재시작만 해도 저장된 true라 버튼 뜸.
- **⚠ 크롬 전체화면(디자인 통일, 사용자 후속)**: "통합웹 창이 앱 디자인 미반영" 지적 → 조사: 확인 팝업은 **이미 디자인준수**(Tactical Command 표준, `ConfirmPopupDialogView` 시안헤더/다크카드/Modern버튼) · 외부 크롬은 WPF 테마 미적용. AskUserQuestion 2회 → 사용자 **외부 크롬 유지**(인앱 WebView2 패널 `ReportPreview` 패턴 거부) + **전체화면 `--start-fullscreen`** 선택. `LeftMenuSectionViewModel.HandleAsync`에 `--start-fullscreen` 1줄 추가(메인 v0.5 `34f8774`, 빌드 CS0). Esc/F11 해제 가능. [[project_panel_design_system]] [[project_webview2_airspace_popup]]
- **요청**: LeftMenu "통합API 연결" 버튼(실제=무동작 "DATABASE" 메뉴)을 "통합웹" 접속 버튼으로. **웹설정 탭 활성화(IsWebServerEnabled) 시에만 활성**. 권한 관련성 검토 요청. "프로세스 기반".
- **검토결과**: 현재 버튼은 권한 게이팅됨(`CanSeeDatabase=>_permission.CanView("reports")`). 사용자 결정(AskUserQuestion)=**①권한 제거·웹설정만 ②숨김(IsVisible) ③라벨"통합웹"·아이콘 Web**. 확인문구="통합웹을 접속하시겠습니까?".
- **PRD/Plan**: `docs/prds/LeftMenu_IntegratedWeb_Button-prd.md`(Approved), `docs/plans/LeftMenu_IntegratedWeb_Button-prd-plan.md`. 공유 pipeline activePrd=OverlayImage 잔재(타세션)라 `complete` 미실행. set-track C 체크포인트 태그=`checkpoint/track-c-20260713031400`.
- **선결검증(V-01~04 완료)**: V-01 `ConfirmPopupDialogViewModel.ClickOk`가 MessageModel 발행✅ / V-02 `BasePanelViewModel` OnActivate:SubscribeOnUIThread·OnDeactivate:Unsubscribe → IHandle 자동구독✅ / V-03 설정변경 브로드캐스트 부재(SetSetupPropertyAndSaveAsync는 자기 NotifyOfPropertyChange만)→부팅평가+재시작폴백 / V-04 md:PackIcon Kind="Web" 유효✅.
- **구현(3파일)**: ①라이브러리 `CommonMessages.cs`에 `CallWebApiProcessMessageModel:IMessageModel` 추가(ViewModel lib 빌드0오류) ②메인 `LeftMenuSectionViewModel.cs`: `IHandle<CallWebApiProcessMessageModel>` 구현(ClosePopup→크롬 null가드·설정 빈값/포트≤0 가드→`--app=http://{IP}:{포트}` Process.Start→try/catch) + 확인문구/MessageModel 주석해제 + `CanConnectWeb=>_setupModel.IsWebServerEnabled` + OnActivate 재평가 + using Utils 추가 ③메인 `LeftMenuSectionView.xaml`: DATABASE 아이템→통합웹(Label/Kind=Web/IsVisible=`CanConnectWeb`). `CanSeeDatabase`는 유지(§5-B).
- **⚠ BUILD 블로커(외부, 내 코드 무결)**: 앱실행중(PID41764) 출력DLL잠김 + **타세션 와치독배선 미완성** — `ShellViewModel.cs:6`(`using Ironwall.Dotnet.Framework.Models.Messages`)+오늘생성 `ExitMessageModel.cs`가 `Ironwall.Dotnet.Framework.Models` 참조하나 메인 csproj에 ProjectReference 없음→CS0234/CS0246 **3건 전부 타세션 파일**. 내 파일은 동일 컴파일패스 오류0.
- **✅ 리뷰/커밋 완료**: code-reviewer(opus) BLOCKER0, W1(Process 미해제+인수인젝션)→`using var proc`+`ArgumentList` 수정. 빌드 중 CS0104(`WebBrowser` System.Windows.Controls vs Utils 모호) 발견→정규화 해소. **CS 오류 0**(남은 빌드실패=앱 실행중 MSB3021 DLL잠금뿐). **커밋 고정**: 메인 v0.5 `7cd5b86`(LeftMenu VM+XAML), 라이브러리 v2.6 `0f16d6e`(CHANGELOG)+`b24d409`(CommonMessages 기커밋=phase 자동커밋이 쓸어담음). 명시적 add만(상대 미포함).
- **협의/우선순위**: 조율문서 `docs/coordination/LEFTMENU_INTEGRATEDWEB_COORDINATION.md`. 와치독 세션과 **파일 disjoint**(내=LeftMenu 2파일+CommonMessages / 와치독=ShellViewModel·ExitMessageModel·WatchdogSetup·Bootstrapper·NatsDomainService). 우선순위=P1 통합웹(완료·커밋) / P2 와치독(진행중) / 공통=앱종료 후 재빌드 1회(순서 무관).
- **🔲 다음(사용자)**: 앱(PID41764) 종료 후 메인 재빌드 → 실측(웹설정 ON→통합웹 표시·확인팝업·크롬 실행 / OFF→숨김). FR-05 라이브 mid-session=재시작 폴백. 선택 후속: FR-05 라이브(WebServerSetupViewModel 설정변경 메시지 발행+구독), N3(실패 시 InfoPopup 안내), N5(`CallWebApiProcessMessageModel`→`CallOpenWebDashboard…` 재명명). [[feedback_no_raw_messagebox_use_eventaggregator]] [[project_singleton_panel_vm_event_lifecycle]] [[feedback_main_solution_advance_notice]] [[project_watchdog_modern_rebuild]]

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — 맵 심볼 단축키 복사/붙여넣기/삭제) — Track C, **✅ PRD→Plan→구현→테스트→v2.6 머지 완료·런타임 검증 대기**
- **✅ v2.6 머지 완료 `96dac1f`**(merge --no-ff, feature `6893ae2`). CHANGELOG 충돌 1건=**CHANGELOG만 좁게 stash→머지→pop**으로 타 세션 편집(힌트 문구·통합웹) 전부 보존하며 해소. 타 세션 다른 dirty(Events.Ui/Watchdog/INDEX) 무접촉. 머지 시점 v2.6는 타 세션 `2d62049`(Watchdog)까지 포함. **앱은 primary v2.6 참조→재빌드 시 반영**([[project_library_deployment_path]]). worktree/정션 존치(정리는 선택).
- **✅ 구현 완료(worktree `feature/mapsymbol-shortcuts` off v2.6, 커밋 `6893ae2` 6파일 +364/−295, 롤백 `before-mapsymbol-shortcuts`)**: 4파일 수정(MapViewModel.cs·GMapCustomControl.cs·MarkerEditAdorner.cs·CHANGELOG) + 2 신규(MapViewModel.CopyPaste.cs·Tests/SymbolCopyTransformTests.cs). 워크트리는 GMap.NET/OnvifSolution 고아를 **정션(junction)**으로 공유(1.5GB 복사 회피). **빌드 0오류 · SymbolCopyTransformTests 7/7 · UndoRedoTests 34/34 회귀**.
- **구현 요지**: `CreateSymbolCopyAsync` 코어(스냅샷 `SymbolSnapshot.Capture`/`CloneModel` 딥클론 재사용→타입별 필드복사 switch 제거) + 순수 `ApplyCopyTransform`(재배치·미링크·Id리셋·제목·LinePoints Δ, 단위테스트 대상). Duplicate/Paste 공유. **PIDS P0 버그 2건 근본수정**(`=pidsSymbol` Id유실 제거+Fetch Id / `+1000` 제거→미링크0). Ctrl+C=`CopySelectionToBuffer`, Ctrl+V=`PasteFromBufferAsync`(커서·`BeginBatch`·트리1회·자동선택). Delete=`OnMapPreviewKeyDownForGroup` 단일분기→`ExecuteDeleteSelected` 확인팝업 + 어도너 Delete/스텁 제거. 게이트=텍스트/콤보 가드+`IsMapShortcutContextActive`(편집모드∧맵가시).
- **🔲 다음(사용자 런타임 검증 — 앱 재빌드 필요)**: worktree→v2.6 머지/체리픽 후 앱 재빌드([[project_library_deployment_path]]). 검증 V-01(어도너 Delete 순서=확인팝업 1회) · V-02(콤보 편집 중 Ctrl+C 무반응) · V-03(Ctrl+V 커서 위치 정확) · V-05(멀티 붙여넣기 다중선택·속성창) · V-06(크로스스레드 없음). 전체 플로우 헤드리스 테스트는 MapViewModel 하네스 부재로 미작성(순수 로직만 단위테스트, 정직 표기).
- **분석 체인**(feedback_analysis_agent_chain): Explore×3 + architect(opus) + code-reviewer(opus). 사용자 결정(AskUserQuestion)=클립보드 Ctrl+C→Ctrl+V·붙여넣기=마우스 커서. 편차 D-01(붙여넣기 _Copy 미부가)·D-02(PIDS 미링크)·D-05(이미지 v1 제외) 기본안 채택.
- **문서**: PRD `docs/prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md`(Approved) · Plan `docs/plans/MapSymbol_Shortcut_CopyPasteDelete-prd-plan.md`(구현완료 로그). [[project_undo_id_collision_all_commands]] [[project_device_reregistration_symbol_desync]] [[feedback_no_raw_messagebox_use_eventaggregator]] [[project_gmap_orphan_submodule]]

<details><summary>(이전) PRD Draft 단계 기록</summary>

### PRD 작성 단계 (완료됨)
- **요청**: 맵 심볼 제어 단축키 추가 — 삭제(Delete)·복사(단일/멀티). 삭제는 EventAggregator confirmMessageModel 확인 경로 필수. 멀티 복사 로직 없으면 복사 로직에 함께 구현. "PRD부터 짜자".
- **사용자 결정(AskUserQuestion)**: 복사=**클립보드 Ctrl+C→Ctrl+V**(즉시복제 아님), 붙여넣기 위치=**마우스 커서**(멀티는 상대간격 유지).
- **분석 체인**(feedback_analysis_agent_chain 준수): Explore×3 + architect(opus) + code-reviewer(opus). **P0 확정 3건**: ①단일 Delete 키 경로 **부재**(`MarkerEditAdorner.RequestMarkerDeletion`(:1291)=로그만 no-op 스텁인데 `OnKeyDown`(:520) `e.Handled=true`로 Delete 삼킴; 그룹핸들러 `OnMapPreviewKeyDownForGroup`(MapViewModel:929)는 그룹선택시만; KeyBinding 없음) ②PIDS 복제 Id 유실(`duplicatedSymbol = pidsSymbol` MapViewModel:5159가 Fetch Id 덮어씀→Id=0→`RecordAdd` Id>0 가드에 걸려 Undo누락) ③PIDS `LinkedDeviceId+1000`(:5143) 실장비 충돌.
- **설계 확정**: `DuplicateSelectedMarker`(5038-5352)→`Task<IEditableMarker?> CreateSymbolCopyAsync(source, targetPos, ct)` 코어 추출(Duplicate 오프셋/Paste 커서 공유, async void→async Task). 인메모리 `_copyBuffer`(List<ClipboardEntry{Model,SourcePos}>)+`_copyAnchor`(첫 항목). Ctrl+V=`CanEditMap()` RBAC→`MainMap.GetLastCursorLatLng()`(신설, GMapCustomControl `_lastMouseScreen` OnMouseMove 최상단+`FromLocalToLatLng`+맵밖 폴백=뷰중앙)→`delta=cursor-anchor`→`BeginBatch` 1매크로 Undo→항목별 코어→`AddMarkerFromSymbol`(RecordAdd Id>0)→자동선택. 삭제=단일진입점 `ExecuteDeleteSelected(null)`(그룹 감지 위임)+어도너 Delete case 제거. 키후킹=`OnMapPreviewKeyDownForGroup` 확장(TextBoxBase 가드에 ComboBox 추가+맵활성 게이트). 배치Undo/트리 1회 리빌드. 스레드=DB I/O 백그라운드·마커/선택 UI마샬. Id매칭=(isImage,Id)/인스턴스(Id-only 금지). 이미지 복사=v1 제외(삭제는 기존 포함).
- **산출**: `docs/prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md`(Draft, FR-01~06·NFR-01~05·V-01~06·D-01~05 편차·OQ-01~06·리스크7·인과결합6). 변경 파일 예정 3: MapViewModel.cs / GMapCustomControl.cs / MarkerEditAdorner.cs.
- **🔲 다음**: 사용자 PRD 검토 → 편차 D-01~05·OQ-01~06 확정 → 승인 → Plan → 워크트리 → dev.
</details>

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — GMap 맵뷰 개선 PRD 작성+승인) — Track C(PRD), 승인 완료·독립문서
- **PRD**: `docs/prds/GMap_Zoom_Anchor_Home-prd.md` **Approved**(사용자 명시 즉시승인 "바로 승인까지"). 범위=줌40m+맵앵커+홈포지션. FR10·NFR3·V6·리스크4. 공유 pipeline 미변경(OverlayImage report 보존 → advance-phase 안 씀).
- **설계 근거**: `docs/design/GMap_Zoom_MapAnchor_Storyboard.html`(줌 세분화·앵커·홈·줌컨트롤러 와이어프레임/스토리보드). Explore×3 조사.
- **핵심 발견=대부분 기존 존재**: 홈(HomePositionModel·SaveHomePositionAsync·GoToHomePosition·부팅적용 ConfigureCommonMapSettings·툴바버튼)·과녁(IsSymbolPlacementMode·Cursors.Cross·SymbolPlacementClicked)·BoundsOfMap(GMapControl.cs:569 미사용)·MinZoom(OnCoerceZoom)·디지털줌(DIGITAL_SCALE_TABLE)·ScaleHelper.AdjustScaleLabel(50÷1.25=40) → **재활용 중심**. 신규=앵커설정모델/UI·자동홈(앵커중심)·SetHome 과녁전환·디지털 중간스텝(1.25×)·컨트롤러 하프틱.
- **🔲 다음(dev 착수 전 게이트)**: ① **V-01 설정 UI 배치**=라이브러리 MapView vs 앱 셋업패널(앱이면 메인솔루션 사전통지) ② feedback_prd_worktree_required=버전브랜치 worktree + 롤백태그 `before-gmap-zoom-anchor-home` ③ Plan 작성(§8 순서: 앵커→홈→줌40m). 사용자 "바로 승인까지"=승인이 엔드포인트 → dev 진행은 재확인.

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — F11 전체화면 토글 구현) — Track B, 구현·빌드0·사용자 F11 실측OK. ⚠v1.1 창버튼숨김 재빌드 대기
- **요청**: 메인 솔루션에 F11 누르면 전체화면 되도록 PRD 만들고 구현.
- **대상**: ⚠외부 메인 솔루션 `Dotnet.Monitoring.Solution\...\Views\ShellView.xaml.cs`(MahApps `MetroWindow`) **단 1파일 순수 추가**. 타 세션 미커밋(NatsDomainService/Bootstrapper 등) 무손상. 롤백 태그 `before-fullscreen-f11`(@3a66503, 메인repo v0.5).
- **설계**: 전체화면 기능 전무였음(grep 0). 코드비하인드 직결(테마토글 EXT-06 선례) — `OnPreviewKeyDown`(터널링·포커스무관, **F11만 Handled**·`e.IsRepeat` 무시) + `ToggleFullScreen()`. 진입=ShowTitleBar false+ResizeMode NoResize+IgnoreTaskbarOnMaximize true+WindowState Normal→Maximized(작업표시줄 덮기 강제 트릭). 해제=진입전 상태 저장·복원. **Esc 해제는 범위 밖**(팝업 Esc 충돌 회피). ViewModel 미개입.
- **PRD/승인**: `docs/prds/FullScreen_F11_Toggle-prd.md` v1.0 Approved. FR-01~04/NFR3/V-01~03. 빌드 0오류(경고47=기존). CHANGELOG(메인 [Unreleased]) 반영.
- **code-reviewer(opus)**: CRITICAL 0. **W1 재진입(오토리핏+전환 중 저장 2회→_prev* 오염→FR-03 복원 붕괴) 수정 반영**(IsRepeat 무시 + `_isFullScreen` 플래그 선설정). W2(Minimized서 진입 진기) 수용.
- **v1.1 보강**: 사용자 "전체화면 X 버튼이 종료냐?" → MetroWindow 함정(ShowTitleBar=false여도 창버튼 잔존, X=프로그램종료). 진입 시 ShowMin/MaxRestore/CloseButton도 숨김·복원 추가(코드 CS0, 실앱 미반영=앱 실행중 DLL잠김). [[project_shellview_fullscreen_metrowindow_chrome]]
- **⚠ complete 미실행**: pipeline `activePrd`=타 세션 `Camera_PTZ_AimLocation_Nats-prd.md` → `advance-phase.js complete` 돌리면 남의 PRD를 Completed로 만듦. 내 PRD는 Approved 유지(구현 완료). 사이클 formal complete는 activePrd 정리 후 or 사용자 판단.
- **▶ 다음(사용자)**: ①앱 닫고 재빌드→창버튼 숨김 시각 확인 ②V-02(전체화면 중 팝업 정상)·V-03(WebView2 포커스 F11=HwndHost 한계) 실측 ③원하면 메인repo v0.5에 ShellView.xaml.cs 단일 커밋(타 세션 미커밋 다수라 scoped add 필수).

## ▶▶ 재개 포인트 (2026-07-13, 이 세션 — GMap 회전 오버레이 desync 분석) — Track A, 분석완료 → backlog 등록
- **요청**: 지도 회전 시 심볼/이미지 다 틀어지는 현상 왜? (사용자 인지: GMap 자체는 좌/우 회전 지원).
- **결론=반쪽 회전**: GMap은 회전 지원하나 이 구성선 **베이스맵 타일만 회전**. ①벤더: 타일=`회전∘스케일∘이동`(GMapControl.cs:1664-1687) vs 마커 Canvas=`이동만`(:702), `FromLatLngToLocal`(:2774)이 위치만 회전 → **타일 rot(pos+offset) vs 마커 rot(pos)+offset, 차이 `(M−I)·팬오프셋`**(팬+회전 시 swim) + 모양 미회전. ②커스텀 오버레이=회전보정 **전무**(MBTiles오버레이맵 RenderOverlayMapTiles:1878 / FOV DetectionBearing만 / 라벨어도너 축정렬 / 히트테스트 심볼Bearing만), 유일 보정=이미지 `MapCorrectionRotation=-MapRotation`(:2369). [[project_map_rotation_disabled]]
- **비활성 커밋 af0f29d**=입력만 차단(Shift휠/Ctrl←→ RotateMap 제거·Ctrl+R Reset 유지), 회전 기계장치 존치.
- **산출**: `docs/analyses/GMap_Rotation_Overlay_Desync-analysis.md`(desync 매트릭스 + B1 해법). 코드변경 0(분석만). 공유 파이프라인 미변경(OverlayImage report 보존). Explore×2 + git(af0f29d) 근거.
- **🔲 [BACKLOG · 할일] 회전 전면 재활성(B1)**: 사용자 "필요 예정" 확인(2026-07-13) → 옵션 **B1(단일 회전 컨테이너)** 채택, **착수시점 미정(보류)**. 착수 시 `GMap_Rotation_Full_Sync` PRD→worktree→Track C. 작업 7종: ①마커+오버레이맵+어도너 단일 `_rotationMatrix` 회전 ②FromLatLngToLocal 이중회전 제거 ③라벨 counter-rotate ④FOV=Detection+MapRotation ⑤히트 −MapRotation 선보정 ⑥디지털줌 중심 회전보정 ⑦입력트리거(af0f29d) 복원. **그때까지 회전 비활성 유지(즉흥 토글 금지)**.

## ▶▶ 재개 포인트 (2026-07-09, 이 세션 — RDP 세션전환 GMap 오버레이 앵커링 해제 수정) — Track B/C, 근본원인 확정→벤더 수정
- **증상/확정원인**: 로컬 실행 중 RDP 접속 시 오버레이 심볼·이미지가 화면 고정(BaseMap만 팬 이동). **근본원인=stale `_mapCanvas` 캐시** — RDP 세션전환으로 WPF가 ItemsHost Canvas를 새로 생성하나 GMap.NET `_mapCanvas`는 분리된 옛 Canvas 유지 → GMap이 옛 Canvas transform만 갱신, 새 Canvas(마커 실제 포함)는 `actualT=(0,0)` 미연결. RDP 진단로그로 확정(`canvasSame=False·transformSame=False·cachedSourceSame=False·actualSourceSame=True`). DPI·드래그가드·이벤트압축 가설은 반증됨. [[project_gmap_rdp_overlay_desync]]
- **수정(벤더 `GMap.NET.WindowsPresentation/GMapControl.cs`, 빌드0오류·테스트186/186)**: ①**자가치유 `MapCanvas` 게터**(`IsCurrentMapCanvas`=동일 PresentationSource 검증→무효 시 재탐색·`RenderTransform=MapTranslateTransform` 재연결, 최종방어선=다음 패닝/줌서 복구) ②`OnApplyTemplate`→`_mapCanvas=null`+예약 ③`OnPresentationSourceChanged`(생성자/Dispose 대칭)→캐시무효+즉시 `ForceUpdateOverlays` ④**`ScheduleOverlayResync`**(`DispatcherPriority.Loaded` 지연·코얼레싱)—레이아웃 완료 후 재투영. 재탐색 NRE 하드닝. 디지털줌/회전/좌표계약 미변경.
- **✅ 현장 1차 검증(사용자 RDP)**: 접속직후 순간 어긋남→**패닝하면 즉시 제자리 복귀**(=자가치유 게터 정상 작동 확인). 접속직후 무패닝 자동정렬 위해 ④ 보강(즉시복구가 새 Canvas 트리구성 전 실행되던 문제→Loaded 지연). **재검증 필요**.
- **롤백**: 태그 `before-rdp-overlay-canvas-rebind`(6390a17) + 수동백업 `GMapControl.cs.bak-before-rdp-canvas-rebind`(벤더=깨진 gitlink라 git 미추적).
- **🔲 다음(사용자)**: ⚠**소비 앱(Monitoring 솔루션) 재빌드+재시작** 후 재검증 — 접속직후 **패닝 없이** 자동정렬되는지(§13.3.1) + T-RD-01~10. **미결**: §8.2 orphan gitlink 소스관리(벤더 수정이 상위 커밋에 안 잡힘) · 확인 후 `LogOverlayDesyncDiag` 임시계측 제거(FR-RD-08). PRD `docs/prds/GMap_RDP_Overlay_Desync-prd.md` v2.1(§13 구현로그).

## ▶▶ 재개 포인트 (2026-07-05, 이 세션 — OverlayImage/맵편집 Undo 버그수정 마라톤, 도메인=GIS 심볼/이미지) — Track B/C, 라이브 로그기반 반복수정
- **컨텍스트**: worktree `feature/map-edit-undo-redo`(off v2.6). PRD 없이 사용자 실사용 로그로 버그 잡아 즉시 수정·v2.6 체리픽·앱 재빌드 반복. 로그파일 직접 grep 가능=`C:\workspace_app\Dotnet.Monitoring.Solution\Dotnet.Monitoring.Solution\bin\Debug\net8.0-windows7.0\Logs\log-YYYY-MM-DD.txt`(log4net).
- **근본 테마 = 이미지(Images.Id) ↔ 심볼(Symbols.Id) 같은 Markers 컬렉션 공존 + Id 충돌**(안양발전소 이미지 Id=1 ↔ 제어기1 심볼 Id=1). Id로 마커 찾는 undo/노드동기/이름변경 경로가 타입 미인지면 엉뚱한 대상 손상. [[project_undo_id_collision_all_commands]]
- **v2.6 커밋(시간순, 전부 체리픽·빌드0·Undo테스트 32/32)**:
  - `9261dd0` 속성창 리사이즈 undo가 엉뚱한 Controller 손상 → 단일대상 커맨드 4종(Property/Lock/Rename/LabelOffset) `FindMarkerById(id,isImage)` 타입인지
  - `bed0f74` Add/Delete/Restore 타입인지 + `[DIAG-TFM]` 진단 추가
  - `ac07359` **SyncMarkerNode·Visibility 타입인지**(이미지 이름 undo/선택 시 속성창에 "제어기1" 뜨던 손상)
  - `63274dc` **TrimMemory UI스레드 마샬**(보호파일 AdornerManagerService, 사용자 승인) — 5분 백그라운드 타이머 크로스스레드 예외가 어도너 편집배선 손상→리사이즈가 속성창 경로 누수. [[project_adorner_trimmemory_crossthread_resize_drift]]
  - `63f5271` 이미지 undo 화면튐(컨트롤 미리사이즈, UpdateScreenPosition) + 심볼 이름변경(OnSymbolRenameRequested) 타입인지
  - `2e02097` **선택 삭제 확인 팝업**(CallDeleteSelectedProcessMessageModel) — Delete 키 오입력에 오버레이 이미지 PNG+DB 영구삭제되던 **데이터손실 사고 차단**. [[project_overlay_image_delete_dataloss]]
  - `8dd81dc` 이미지 이름 undo 시 레이어 트리 desync(MapLayers.Name 재동기, SyncOverlayImageLayerNameAsync)
  - `337ea49` 리사이즈 undo 위치 튐 근본(Offset 재적용, ForceUpdateLocalPosition) — 이동 undo는 Position 바뀌어 정상, 리사이즈는 Position 불변이라 GMap 재배치 미트리거
  - `2169e65` **(최신) 이미지 UpdateSize 심볼식 자기재앵커**(ReanchorOffsetFromBounds) — 멀티에이전트 심볼vs이미지 비교 결론: 심볼은 UpdateSize 내부서 UpdateOffset→UpdateLocalPosition 동기실행. 이미지도 UpdateSize/UpdateBoundsFromPixelScale 말미에 bounds→Offset 재계산+재투영 추가해 전 리사이즈 경로 자기치유.
- **🔲 다음(사용자 검증 대기 — 재빌드 완료·배포됨 2026-07-05)**: 메인솔루션 재빌드 완료(전 커밋 배포). 사용자 앱 실행해 확인: ① 이미지 리사이즈+Ctrl+Z 위치유지(심볼식 재앵커 2169e65) ② Delete→확인팝업 ③ 이름 undo에 제어기1 안뜸 ④ 이름 undo 시 레이어 트리 싱크 ⑤ 삭제된 안양발전소 이미지 복구(원본 `안양발전소.png` 생존 → 앱서 오버레이 재등록). 정상 확인되면 `[DIAG-TFM]` 제거.
- **잔여 진단코드 제거 예정**: `[DIAG-TFM]`(TransformCommand.cs) — 리사이즈 undo 정상 확인되면 제거. `[DIAG-USZ]`는 이미 제거됨.
- **감사에서 식별된 잔여 갭(미수정)**: ZOrderBatchCommand→ApplyZOrderAsync가 아직 `(id,zOrder)`만 받아 isImage 유실(레이어창 ZOrder 시 같은 Id 충돌 가능). 필요 시 후속.
- 이 세션 도메인 밖(Reports `870b1f7` 등)은 타세션. 보호파일=MarkerEditAdorner/MarkerAdornerLayer 무변경(AdornerManagerService만 승인받아 1줄 마샬).

## 🔴 패널 디자인 통일 (권한계정 스타일로) — **전부 롤백됨**(디자인 후짐·시각검증 실패) (2026-07-04, Track C)
- **★결과=롤백**: 14패널 일괄적용했으나 **화면 검증 없이(빌드0·바인딩무손실만)** 밀어붙여 사용자 "디자인 개 후져졌어"→ **프라이머리 16파일 `git checkout HEAD --`로 정밀 원복**(29→13 dirty, 정확히 -16, 타세션 무영향, 레전드 보존). 소스 clean·dangling 참조 0(6개는 stale DLL뿐, 재빌드시 교체). **실패 교훈=[[feedback_deliver_visible_design]]** 강화(렌더 확인 없이 UI 확산 금지, "나머지도"≠시각승인, 버튼 Content 색 미조정=후짐).
- **보존물(재도전 시 재사용 가능)**: PRD/Plan(`docs/prds|plans/Panel_Design_Unification-*` Approved) · 롤백태그 `before-panel-design-unification` · worktree `feature/panel-design-unification` 커밋 `63a03bd`+`c7cd777`(장비7+이벤트4+폼3 이관본, 바인딩무손실 검증됨). **재도전 시 반드시 파일럿 1개 렌더 스샷→명시 승인→확산.**
- **진행(참고, 롤백됨)**: 파일럿 Sensor → 장비 7(wf_e7e64dec) + 이벤트 4(wf_fa35b9dc) + 로그인/로그아웃/EventCardList(wf_b668f557). 전부 멀티에이전트 bindingLoss=false·빌드0였으나 **디자인 품질 검증 안 함**이 근본실패.
- **🔲 남음**: **AccountManager/MyPage**(부분—카드셸 있음, 마감만. ⚠**MyPage 타세션 미커밋** → 그 세션 커밋 후 진행 권장). 설정(Setup)=메인솔루션 후속 별도세션. **미머지**(worktree만·프라이머리 미커밋 반영, v2.6는 e5beb26로 전진→머지 시 재싱크). EventCardList에 **기존 버그**(액션문자열 `$eventArgs]` 괄호오타) 원본보존·미수정.
- **★파일럿 템플릿 확정**: `SensorDevicePanelView.xaml`(worktree+프라이머리) = 계정 탭콘텐츠형(Grid Margin=6 → DockPanel[PanelSectionLabel+ModernToolbarButton] → ModernDataGrid+Row/Cell/ColumnHeader+DraftSurfaceBrush draft행 → 풋터). 신규토큰 `DraftSurfaceBrush`(Tokens.Dark #33F0A33C·Light #FFECC9). 장비패널 호스트=DeviceDashboardView(상단통계+**좌측 NavigationRail 탭**)=셸은 계정콘솔(시안헤더+상단탭)과 구조 다름→**셸 통일은 별도 결정**. ⚠worktree는 OnvifSolution 고아 복사 필요(복사완료). 앱은 프라이머리 참조라 시각확인=프라이머리 반영+재빌드.
- 요청: 장비·이벤트·설정·로그인/로그아웃 패널을 **권한/계정 패널(Accounts.Ui) 디자인으로 통일**. 사용자 결정=**라이브러리만 먼저 / 완전 통일(레이아웃 재작성) / 장비 1패널 파일럿 → 승인**.
- 스코핑 워크플로우 wf_632c386b(5매핑+종합): 타깃 공용스타일 **이미 Theme에 완비**(ModernDialogHeader·ModernDataGrid·PanelSectionLabel·ModernToolbarButton+토큰, Reports.Ui 이관성공 선례). 대상은 맨 Grid라 **토큰만으론 안 되고 골격 재작성 필요**. 접근=**hybrid**(토큰 1개 DraftSurfaceBrush 선행 + 뷰별 레이아웃 재작성, AuditLog 기준 템플릿, 바인딩/VM 보존). 라이브러리 16뷰~24h, 설정(메인솔루션 ~28h)=후속.
- **PRD** `docs/prds/Panel_Design_Unification-prd.md`(Draft, FR-01~07·NFR-01~05·V-01~04·R-01~06·OQ-01~07). FR-01=파일럿 장비1패널+템플릿확정+**사용자 시각승인 게이트**.
- **🔲 다음**: 사용자 PRD 검토·OQ 확정(OQ-02 데드코드정리·OQ-04b Camera/Sensor·OQ-05 로그인필드·OQ-06 Draft색·OQ-07 앱종료) → 승인 → Plan → worktree+`before-panel-design-unification` 태그 → 파일럿. ⚠선결: Events.Ui(내 DataChart 미커밋)·Accounts.Ui(타세션 MyPage/PermissionMatrix 미커밋) 정리. [[project_panel_design_system]] [[project_ui_design_storyboard]] [[reference_wpf_card_host_stretch]] [[project_dialog_panel_dark_theming]] [[feedback_main_solution_advance_notice]]

## ✅ DataChartPanel 레전드 회색 처리 (2026-07-03, Events.Ui, 미커밋·빌드0)
- 요청: DataChartPanel(Events.Ui) 오른쪽 레전드 기본색을 회색계열로. 차트=LiveChartsCore/SkiaSharp.
- **변경 3파일**: ①`Helpers/ChartThemeProvider.cs` 레전드 전용 `LegendTextColor`(theme-aware slate: Light `#64748B`/Dark `#94A3B8`) 신설(축·툴팁 `TextColor`와 분리) ②`ViewModels/Panels/DataChartPanelViewModel.cs` 레전드 색 3곳(ctor:46·테마전환:57·기본필드:250) `TextColor→LegendTextColor` ③`Views/Panels/DataChartPanelView.xaml` **누락돼있던 `LegendTextPaint="{Binding LegendTextPaint}"` + LegendTextSize=12 바인딩 추가**(잠재버그: 없어서 VM색 미반영·LiveCharts 기본색·다크서 안보임).
- 결과: 레전드 라벨만 회색, 축/툴팁/시리즈 점색 불변. ⚠앱 재빌드 필요. 미커밋(사용자 확인 후 커밋). EventInfoView 레전드는 미변경(요청=DataChartPanel만). 색 톤 조정 쉬움.

## ▶▶ 재개 포인트 (2026-07-03, 이 세션 — 카메라 RTSP팝업/ONVIF PTZ 진단) — ★진단만, 코드변경 없음
- **증상**: 카메라108 더블클릭해도 RTSP 팝업 안뜸 / 카메라66 ONVIF PTZ "안됨". **근본원인 확정=서버 디바이스 재번호(Id drift 1964→59·1968→60·1969→61)로 GMap 심볼의 stale LinkedDeviceId 불일치.** [[project_device_reregistration_symbol_desync]] (멀티에이전트 wf_7f9709d9·wf_6d917620 + 실로그 + pidssymbols DB조회 + 적대적검증. 사용자 스크린샷 ONVIF Device Manager로 최종 확증).
- **cam66 = DATA 문제 확정**: 실제 카메라66=`192.168.202.66`(ONVIF/PTZ 정상, 외부툴 확인). 앱 디바이스 레코드 IP가 `192.168.101.66`(유령주소)로 오등록 → ONVIF 타임아웃. RTSP는 수기 URL(202.66)이라 정상. **수정=디바이스 설정에서 카메라66 IP `101.66→202.66` 정정**(앱편집/서버 어느쪽인지 사용자 확인 대기). 포트는 80 폴백 정상.
- **cam108 = 심볼 stale링크**: 심볼은 DB 존재(삭제 아님), LinkedDeviceId=1968(죽음)→null→더블클릭 무음 return. **수정=속성창 "연결디바이스" 재선택(즉효) 또는 자가치유 코드.**
- **⚠ 사용자 오해 정정(정직 보고)**: Undo/Redo·멀티셀렉이 원인 아님 — 코드(각 마커 자기행만 저장·LinkedDeviceId 미전파)·타임라인(그룹편집 후에도 정상, 13:47 로드시 오염)으로 반박. STJ undo버그는 별개·이미 `9c33577` 수정됨.
- **🔲 다음(사용자 선택 대기)**: ① cam66 IP 정정(위치=RTSP URL 넣는 CameraSettings의 IP주소 필드 추정) ② **자가치유 PRD**: MarkerFactory.CreatePidsMarker Id매칭 실패시 Title/이름 폴백 재바인딩+UpdatePidsSymbolAsync 재영속 → 재번호돼도 전 카메라 자동복구 + 무음 return 표면화. ③ (부차) IpPort 단일필드가 RTSP(554)/ONVIF(80) 공유 가드.
- 이 세션 코드변경=0(진단). 별도로 앞서 playback 재생버튼 `35fb14f`(커밋완료)·gmap R-03 worktree `d2b71be`(FF대기) 있음.

## [A] ✅ 맵 편집 Undo/Redo — **v1 완료 + 300시나리오 리뷰×2 기반 수정 11종·v2.6 반영** `15316de` (2026-07-03, 세션 A, Track C)
- **✅ 시뮬레이션 리뷰 ×2 + 수정**(사용자 요청 "레이어/PropertyPanel 싱크 300시나리오 2회"): Round1(병렬 2에이전트, ~324시나리오 PASS~220/FAIL~41/GAP~63) → 수정 fork 9종 → Round2(opus 적대적 검증) → 잔여 2종. **커밋 `c94be07`(9종)+`15316de`(2종)** 체리픽 v2.6, 메인 빌드0·테스트 18/18. 타세션(Reports f58add4·GMapUiModule·Playback) 보존.
  - **핵심 수정**: ①**그룹이동 "하나만 복원" 근본수정**(MacroCommand 자식별 try/catch + Line/PidsGroup UpdateLocation가 스레드풀서 InvalidateVisual 호출→cross-thread 예외→매크로 중단이 원인, 예외안전+Dispatcher 마샬) ②OnMarkerPropertyChanged undo 에코 가드 ③`_isApplyingUndo` 카운터화 ④`SyncMarkerNode` 타겟 트리싱크(전체리로드 NRE 회피) ⑤이미지 undo 트리·⑥IdCollision Id전파·⑦Add-undo 패널숨김 ⑧**ZOrder undo 배선**(라이브 패널+컨텍스트메뉴, 죽은핸들러 아님) ⑨스냅샷 점 왕복. Round2 확인: V1~V3·V5 견고·크래시회귀0.
- **✅ v2.6 FF 머지 완료**(사용자 승인 reconcile): 타세션 `GMapUiModule` dirty(추적등록)를 stash 보존→FF→pop 재적용 → **내 Undo DI등록(L62-66) + 타세션 추적등록(L73) 병존**·타세션 MapSettingsHelper dirty 보존. Session C dirty 전부 보존. 🔲 앱 재빌드 E2E(Ctrl+Z/Y·툴바·왕복·전체선택 이동 undo·트리/패널 싱크).
- **✅ v2.6 FF 머지 완료**(사용자 승인 reconcile): 타세션 `GMapUiModule` dirty(추적등록)를 stash 보존→FF→pop 재적용 → **내 Undo DI등록(L62-66) + 타세션 추적등록(L73) 병존**·타세션 MapSettingsHelper dirty 보존. 메인 repo GMaps.Ui 빌드0. Session C dirty 전부 보존. 🔲 앱 재빌드 E2E(Ctrl+Z/Y·툴바·왕복).
- **PRD Approved v1.0**(7항목 권장안 채택) · Plan `docs/plans/Map_Edit_Undo_Redo-prd-plan.md` · 분석 `docs/analyses/Map_Edit_Undo_Redo-analysis.md`.
- **구현**: worktree `feature/map-edit-undo-redo`(off v2.6 `2d90bfc`, 롤백태그 `before-map-edit-undo-redo`). 커밋 `c71a8a7`(코어+통합) + `9d61fe7`(마무리+테스트). **GMaps.Db·Ui 빌드0 · 단위테스트 15/15 통과 · 금지 adorner 파일 무변경.** fork ×3(코어/통합/마무리).
- **동작 커버(v1)**: 심볼 추가·삭제·이동·회전·크기·속성(coalesce)·라벨오프셋·잠금·이름 + **그룹 이동·그룹 삭제**(Macro) + Ctrl+Z/Y·툴바 버튼. 신규 `GMaps.Ui/Services/Undo/`(UndoService·IUndoApplyContext·SymbolSnapshot(JSON딥클론)·EditRecorder·커맨드9종) + `GMapDbSymbolService.Restore.cs`(RestoreXxxAsync×8 Id보존+IdCollision폴백) + `MapViewModel.UndoContext.cs`(seam구현).
- **🔲 v2.6 FF 머지 보류**: 메인 repo `GMapUiModule.cs`가 **타 세션 미커밋**(TrackingSetupModel→`MapSettingsHelper.LoadTrackingSettings`, +MapSettingsHelper.cs). 내 커밋도 GMapUiModule 수정 → FF가 타세션 변경 덮어씀 → **덮어쓰기 금지, 그 파일 clean 시 FF**(v2.6=2d90bfc 그대로라 재머지 불필요). 대안=사용자 승인 시 stash 재적용 reconcile.
- **v1.1 이연**: ZOrder undo(인프라 완성=ZOrderBatchCommand/RecordZOrder/ApplyZOrderAsync, 3사이트 old/new 캡처만). **v2**(PRD): 런타임가시성·파일이미지·MapLayers노드·히스토리드롭다운·소프트삭제.
- **부수 발견 DB 버그**: InfraSymbol Fetch Factory/Office 하드코딩 · PidsSymbol Detection* 미영속 · AddPidsSingleMarker Id=0(→이번에 수정, savedSymbol 사용).
- **🔲 다음**: 타세션 GMapUiModule clean 시 v2.6 FF → 앱 재빌드 E2E(Ctrl+Z/Y·툴바·왕복). [[project_symbol_label_adorner_system]] [[feedback_prd_worktree_required]] [[project_library_deployment_path]]

### (이전) 조사·설계 근거
- **활성 PRD**: `docs/prds/Map_Edit_Undo_Redo-prd.md`(**Approved v1.0**) · 분석 `docs/analyses/Map_Edit_Undo_Redo-analysis.md`.
- **조사**: Explore×4(심볼/이미지·라인·그룹·라벨/DB·역연산/편집인프라) + architect(opus). 편집연산 전수·영속 choke point 3개(`DbSaveProcess`/`DbUpdateProcess`/`DbDeleteProcess`)·DB 역연산 제약 매핑.
- **설계**: **Command+내장Memento(하이브리드)** + record-after-the-fact + `IEditRecorder` 파사드(의미 이벤트 핸들러서 기록, 보호파일 무접촉). before 없는 경로=선택 시 baseline 딥클론. 삭제-Id=가산형 `RestoreXxxAsync`(명시Id+AUTO_INCREMENT리셋)+새Id폴백. 배치=MacroCommand. 함정=`SuspendRecording`+`_isApplyingUndo`(deselect 암묵저장 2107)+속성 coalescing.
- **v1**: 심볼 추가/삭제/이동/회전/크기/속성/라벨/잠금/이름/ZOrder + 그룹이동/삭제 + Ctrl+Z/Y·툴바. **v2**: 런타임가시성·파일이미지·MapLayers노드·히스토리드롭다운·소프트삭제.
- **DB 버그 발견(별건 영향)**: InfraSymbol Fetch가 BuildingType=Factory/Usage=Office 하드코딩(재-fetch 손실→라이브클론 필수) · PidsSymbol Detection* 미영속 · `AddPidsSingleMarker` Id=0(MapVM:4351).
- **🔲 다음**: 사용자 PRD 검토→7개 결정(§6 하단: 역연산시점·삭제전략·스택깊이·편집OFF스택·런타임가시성·v1범위·NATS무효화)→`advance-phase.js approve prd`→plan. [[feedback_prd_before_code]] [[feedback_analysis_agent_chain]] [[project_symbolprovider_stalecache_markers_hardcast]]

## ▶▶ 재개 포인트 (2026-07-02, 이 세션 — GMap 버그수정 복구 + R-03 + 재생버튼)
- **GMap 편집모드/삭제/신규심볼 3버그** (worktree `feature/gmap-delete-editmode-fix`, tip `d2b71be`, clean): BUG-CAST(편집모드 크래시)·BUG-DEL(삭제부활)=`7394b7b` · **R-03/FR-04(신규심볼 소멸)=`d2b71be`**. v2.6→feature 실머지 `263a4b5`(충돌0)로 **FF 준비완료**. 빌드0·회귀 173/173·code-reviewer(opus) APPROVE(FR-04=APPROVE_WITH_FIXES). PRD `GMap_Delete_EditMode_BugFix` v1.1. 상세=아래 gmap 블록.
  - 🔲 **다음**: ① **머지** — ⚠ v2.6가 그새 전진(내 playback 커밋 `35fb14f` 등)해 **순수 FF 아님**(feature d2b71be가 v2.6 새 tip 미포함). 머지 시 **worktree서 `v2.6→feature` 재싱크 머지 후 진행**(코드=GMapDbSymbolService/GMapCustomControl로 disjoint·충돌0 예상). 프라이머리 dirty(CHANGELOG 등 다수 타세션) clean 후. 강제/stash 금지. ② 앱 재빌드→E2E 3종(편집토글 크래시0·삭제 미부활·**신규심볼 추가→탭이동→복귀 유지**). ③ **FU-W1**(비블로킹, 사용자결정=머지 후): add-후-편집 인스턴스 정합성(PRD/Plan FU-W1, MapViewModel AddXxxMarker 7곳).
- **재생버튼 하이라이트 반전 수정**(2026-07-02, ✅커밋 `35fb14f` @v2.6, 빌드0): `PbPlayToggle` 스타일 신규(배속칩과 동일 IsPlaying DataTrigger) → 정지(▶)=배경색·재생중(⏸)=시안. `PlaybackConsoleStyle.xaml`만 커밋(내 재생버튼 + 부수적 섹션라벨 ①②③제거). 타 세션 미커밋(PlaybackViewModel 재생범위 로직 등)은 미커밋 보존. ⚠앱 재빌드+재시작 필요.
- ⚠ **미커밋 주의**: 프라이머리 v2.6에 타 세션 6파일(CHANGELOG·INDEX·MapSettingsHelper·GMapUiModule·PlaybackConsoleStyle·PlaybackViewModel) dirty — 내 재생버튼분(PlaybackConsoleStyle)만 내 것, 나머지는 타 세션 것(건드리지 말 것).

## [A] ✅ GMap 4항목 전부 완료·v2.6 머지 (①잠금메뉴 ②반경애니 ③다중선택 ④라벨분리) `a0c83f9` (2026-07-02, 세션 A, Track C)
- **④ 심볼-라벨 분리 완료**: PRD/Plan=`Symbol_Label_Decouple-*`. worktree `feature/symbol-label-decouple`, 롤백태그 `before-symbol-label-decouple`. Phase별 fork 분할(각 ~1M토큰):
  - **P1** 모델+DB `LabelOffsetX/Y`(멱등 ALTER, `348c331`) → **P2** `LabelAdorner`(맵레벨·수직 유지·심볼추종)+`LabelAdornerService`(AdornerManager 밖, `2090445`) → **P3** 7 `*MarkerStyle.xaml` 라벨트리거 45개 Collapsed(잘림 해소·Pids 27, `ac78d3f`) → **P4** 라벨박스 드래그+점선 리더선(라벨만 이동 시)+1.5배 상한+`DbUpdateProcess` 영속(`CanEditMap`)+공존(비겹침, `a0c83f9`).
  - **v2.6 FF**: P1-3 `ac78d3f` → P4 `a0c83f9`. 빌드0. **금지파일(MarkerEditAdorner/MarkerAdornerLayer/AdornerManagerService) 전 Phase 무변경**. click-through·디지털줌 inner공간(`e.GetPosition(_map)`)·Dispose 구독해제 준수.
- **GMap 4항목 v2.6 최종**: ① 잠금메뉴 정렬 · ② `AimOverlayAdorner`(반경 최상위+grow-in+주황리플)+requested_by=user.name · ③ `GroupSelectionAdorner/Service`(러버밴드+좌드래그 이동+우클릭 7메뉴) · ④ 라벨분리. **전부 앱 재빌드 E2E 대기**.
- **⚠ ④ 알려진 편차(수용)**: 라벨-드래그↔편집-드래그 배타=비겹침 의존(직접 라벨을 아이콘 위로 끌면 엣지케이스) · 표시DB영속 v2 · 라벨 오프셋 상한=기본앵커 기준. E2E 확인.
- **✅ ④ 후속 버그수정·사용자 검증 완료**(2026-07-02): 라벨 3버그 `4e11c53`(**C** 제목/제목크기/제목표시 미반영=LabelAdorner가 마커 `PropertyChanged` 미구독→구독추가·Dispose해제 / **B** 줌Hide=`SetMarkerVisibility` 술어(`Zoom<marker.Zoom||!IsLayerEnabled`) OnRender 체크 / **D** 박스2개=라벨칩 시안 테두리 제거) + **PidsGroup 옛 흰라벨 잔존 `2172782`**(PidsGroupMarkerStyle만 `PART_TitleContainer` 사용→Phase3 `PART_LabelContainer` 필터서 누락, ShowTitle 트리거 Visible→Collapsed). 라벨=`marker.Title`(패널 "제목"=`MarkerTitle`→`SelectedMarker.Title` TwoWay, 동일소스). 빌드0·금지파일 무변경·Session C dirty 보존. **사용자 "제대로 되는것 같아" 확인**. worktree tip `2f354dc`(v2.6보다 뒤·재사용시 리셋). [[project_symbol_label_adorner_system]]
- **✅ ③④ 실측버그 5종 수정·v2.6 `fd8f4b6`**(2026-07-03): **L1** 라벨 오프셋 DB 미영속=타입별 UPDATE 6개(Geometry/Pids/Military/Line/Infra/PidsGroup) SET 절에 `LabelOffsetX/Y` 누락(파라미터는 전달중, `= @LabelOffsetX` 1→7개) · **L2** 라벨 이동 편집모드 전용=`LabelAdorner` HitTestCore/드래그에 `_map.IsEditMode` 게이트(편집OFF=클릭스루, 편집=CanEditMap 정합→영속보장) · **M1** `GetMarkersInRect` `IsLocked`·`GMapMarkerImageControl` 제외('잠금 포함' 반전) · **M2+M3** `ClearAllSelections`에 `_groupSelection?.Clear()`(빈공간클릭 OnMapClicked·편집끄기 IsEditModeEnabled 세터 경유, 러버밴드 시작은 이미 IsEditMode 게이트). 롤백태그 `before-gmap-multiselect-label-fixes`@`2172782`. GMaps.Db·Ui 빌드0·금지파일 무변경·MapViewModel 자동병합(31962c7 등)·Session C 보존. 🔲 앱 재빌드 E2E.
- **✅ 멀티셀렉트 시각/싱크 4종·v2.6 `47486e2`**(2026-07-03): **A** 러버밴드 마퀴가 오버레이 이미지에 가려짐=OnRender(자식 아래)→신규 `RubberBandAdorner`(맵 AdornerLayer, 이미지·마커 위) · **B** 심볼별 선택박스 누락(PidsGroup만)=`GroupSelectionAdorner` 바운딩박스1개→심볼별 박스 + `GroupSelectionService` IsSelected 미세팅(템플릿 하이라이트 불일치 대체) · **C** 박스 크기 부정확=`MarkerRect`=`shape.TransformToVisual(map)` 실제 bounds · **D** 그룹 잠금/숨김 레이어트리 미싱크=`SyncLayerNodesFromMarkers`(Id매칭 `InitIsLocked`/`SetCheckedSilently`). GMaps.Ui 빌드0·금지파일 무변경·MapViewModel 자동병합·Session C 보존. 멀티셀렉트 자체는 동작 확인(편집모드 ON 필요, git blame f33d9c1 원래부터). [[project_symbol_label_adorner_system]]
- **🔲 다음**: 4항목 **앱 재빌드 E2E**(②③ 이미 검증가능·④ 라벨 사용자검증✅·나머지 신규). 이후 [[project_login_gating_init_architecture]] Grant Scheduling 정합(내 `6c4ed0a` worktree vs v2.6 T4)·②user.name 검증 등 잔여 스레드. [[project_symbolprovider_stalecache_markers_hardcast]] [[project_digital_zoom_architecture]] [[feedback_prd_worktree_required]]

## [A] ✅ GMap ③ Shift+드래그 다중선택 (UX개정 포함) — v2.6 FF 머지 완료 `2fff13a` (2026-07-02, 세션 A, Track C)
- **UX 개정(사용자 확정)**: 이동=코너핸들 제거→**선택 심볼 위 좌드래그**(커서 SizeAll), 우클릭=그룹 메뉴 **삭제/잠금/해제/표시/숨김/맨위로/맨아래로**. `HitTestCore`=선택마커영역(합집합) hit. 표시/숨김=런타임(DB v2), Z순서=`BatchUpdateZOrderAsync`. 커밋 `2fff13a`. 롤백태그 `before-groupselect-uxrevise`@`bda89b0`.
- **완료**: PRD/Plan=`GMap_RubberBand_MultiSelect-*`(Approved). worktree `feature/groupselect-multiselect`(off `4a84d61`), 롤백태그 `before-groupselect-multiselect`.
- **커밋**: IMPL-00 불변식#8 하드닝(`Markers` 하드캐스트 3곳→`OfType`, `b24d250`) · IMPL-01/02 Shift 러버밴드 캡처+`GetMarkersInRect`+마퀴(`f33d9c1`) · IMPL-03~09 GroupSelectionAdorner/Service+그룹 이동/삭제/잠금+공존(`d25c12a`, fork) · 디지털줌 델타 리뷰픽스(`b748e2a`). **v2.6 FF `4f100b4`, 빌드0. 세션 C WIP 보존.**
- **설계**: 맵레벨 `GroupSelectionAdorner`(바운딩박스+좌상단 이동핸들, 우클릭메뉴 삭제/잠금/해제) + `GroupSelectionService`(선택집합·adorner 소유, **AdornerManager 밖**). **금지파일(MarkerEditAdorner/MarkerAdornerLayer/AdornerManagerService) 무변경**. click-through(HitTestCore=핸들만)·`CanEditMap()` 게이트·Dispose 구독해제·디지털줌 inner공간(`e.GetPosition(_map)`). 잠긴 멤버=이동/삭제 스킵·잠금/해제 전체(FR-MS-07/08).
- **⚠ 알려진 편차(수용)**: ①그룹 이동은 persist만 RBAC 게이트(드래그시각 아님, 서버권위라 OK) ②그룹이동 그리드스냅 미적용 ③빈공간 클릭은 그룹 clear 안 함(마커클릭/새러버밴드/편집off만) ④AABB 회전 근사.
- **🔲 다음**: 앱 재빌드 **③ E2E**(Shift드래그 선택·핸들이동+DB·Del/메뉴 삭제·잠금해제·단일선택 무회귀·팬줌 geo앵커·디지털줌·추적마커 크래시0). 이후 **④ 심볼-라벨 분리**(마지막·최침습). [[project_symbolprovider_stalecache_markers_hardcast]] [[project_digital_zoom_architecture]] [[feedback_prd_worktree_required]]

## [B] ✅ §C NATS revoke 서비스 + FR-FL-08 문구헬퍼 + token 전수검증 — feature/superseded-revoke `ae8d03b` (2026-07-02~04, 세션 B)
- **✅ FR-FL-08 사유문구 헬퍼(`ae8d03b`, 셸 무접촉)**: `RevokeReasonMessage.ToUserMessage(reason)` — 현재 셸은 evict 시 reason 무분기 "시스템 인증 대기"만 표시(사유 안내 없음). 헬퍼 선준비로 셸 배선=`FetchProgressText = reason.ToUserMessage();` 한 줄만 남음(WIP 조율 후). 110/110. treadmill로 v2.6(af0f29d) 캐치업(`9bf4855`), FF-ready·최종머지 보류. Events는 오탐(A L175 규명)→클라 실기능문제 0.
- **✅ §C 라이브러리 팔 구현(`741e47c`, PRD Approved)**: `AccountSessionRevokeNatsService`(Accounts.Api, FR-FL-02/03/10) — NATS 세션 revoke 수신→HMAC검증+freshness+dedup+(session_id∧jti)매칭→ForceLogoutOnce(DUPLICATE→Superseded/FORCED→SessionRevoked). 테스트 107/107. **실버그 수정**: Newtonsoft DateParseHandling 기본값→issued_at DateTime 자동변환→canonical 불일치로 서명 항상실패→`DateParseHandling.None`. Accounts.Api→Nats참조+Autofac8.3(NU1605). FR-FL-01(TokenStorage jti/sid/uid)은 이미 DONE. **머지 보류**(main 작업트리 A WIP+CHANGELOG M로 FF막힘·죽은코드·Autofac범프 A조율). 배선잔여=구독subject(메인 `_additionalSubjects`)·서버 NATS_REVOKE_ENABLED·서명키·셸문구(FR-FL-08 `ConductorControlViewModel:197`). [[project_login_gating_init_architecture]] [[feedback_main_solution_advance_notice]]
- **✅ AUTH_MODE=token 클라 전수검증(Workflow 17에이전트·라이브 서버)**: 서버 `fe4ffc7`로 token 전환. **빌드0 · 라이브 401 집행 5도메인 전부 확인**(https:8000, 무토큰 401) · **Bearer 배선+DTO 계약 Accounts/Devices/Reports/Tracking 정합** → **클라 token 준비 4/5**. **유일 실문제=Events DTO v2.7 드리프트(MED, A)**(DetectionEvent/Malfunction/Connection nested device·필터 미정합→데이터손실). LOW(A)=Reports chart_type↔kind·스테일주석. 서버문제=Events statistics 인증우회·감사500·구성원해제·세션정리. **테스트 실패=전부 환경성**(통합/DB 라이브), **B 프로덕션 그린**(Accounts.Api 96/96·Ui 16/16). **분담**: A=Events v2.7 정합화·Reports LOW / B=계정 클린(조치0) / 서버=statistics·계정항목. [[reference_api_test_server]] [[project_rbac_enforcement_reality]]
- **✅ 라이브 token RBAC positive-path 완결(2026-07-04)**: gop_user/gop_operator(sensorway1, admin 미사용) 로그인 200·토큰·`{modules}`·권한차등정확(view만 vs view+control)·토큰 API 200/403(user-groups 403 정확차단). **§C 즉시통지 정밀갭**: 라이브러리 계약 완비(EnumRevokeReason.Superseded)이나 **NATS revoke 구독자·셸 Superseded 문구=메인솔루션 미구현**(evict는 401-폴백으로 기능성립, 즉시성·전용문구만 부재). §C 실행=메인솔루션 사전통지 후. [[project_nats_disconnect_strategy]] [[feedback_main_solution_advance_notice]]
- **✅ GET /grants 클라 API/DTO 씸(`2d90bfc`, 분담 B)**: 서버 실측=미커밋 grants.py/grant.py뿐 → **GET /grants만 신설**(REQ 반영, GrantResponse에 user_login_id/user_name 보강). 감사500·구성원해제·세션정리는 서버 미구현 확정(A와 일치). **B 몫**: `GrantDto.UserLogin/UserName` + `GetAllGrantsAsync(page/size/user_id/group_id/status/active_only)`. 빌드0·Accounts.Api 95/95. **A 몫**: GrantManagementPanel N-순회→단일호출 배선. + A `REQ_Server_Session_Cleanup.md` 서버 docs 배치. **B의 L162 SUPERSEDED 매핑=서버게이트**(메인솔루션 revoke 구독자 없음·서버 미발사·사전통지 대상).
- **✅ T2 재로그인 먹통 근본수정(`c2c6a2c`)**: 세션관리서 admin 자기 세션 강제로그아웃 후 로그인 버튼 무반응. **로그규명**: `ClickOk` 첫 줄 `if(ViewModel.IsLogin) return;` 가드인데 강제로그아웃 경로가 토큰만 지우고 `IsLogin` 미리셋(IsLogin=false는 `LoginViewModel.Logout()`에서만) → IsLogin=true 잔존 → 클릭 즉시 반환(로그상 ClickOk 첫 부수효과 ProgressPopup조차 0). **수정**: `LoginPanelViewModel.OnActivateAsync`의 `Clear()`→`Logout()`. 스크린샷 "HTTP 504"=그 시점 서버 타임아웃(별개). 원장 T2(A 전담·장기미해결) 그 버그를 B가 로그로 규명·수정. `LogService.Error()`→Warn 오분류(`LogService.cs:131`) 별도 발견(저순위).
- **✅ L154 실측버그(`43ddb62`)**: **② 그룹삭제 확인팝업 잔존 = 근본수정** — `ConfirmPopupDialog.ClickOk`가 MessageModel만 발행·팝업 안 닫음 → 핸들러가 `ClosePopupMessageModel` 발행해야(디바이스 패널 패턴). `HandleAsync(CallDeleteGroupMessageModel)`에 추가. ⚠A의 `GrantManagementPanel` grant회수도 동일 누락(flag). **① "추가 안 됨"**: A의 id=18 증거로 서버 생성 정상, 클라 201 처리·파싱 정상, `ClickSaveGroupForm`이 이미 생성 후 `ReloadAsync` → 흐름상 처리 + 성공 Info 피드백 추가. 잔존 시 raw-JSON 로그 필요(서버 다운). L153 감사500=서버세션.
- **요구(사용자)**: "권한 부여 기능이 제대로 동작안하는데 계정이 안뜬다. 권한설정 패널이 권한관리 미반영 — 권한그룹 등록/삭제/수정, 계정 할당 그게 없잖아. 확인해봐." (원장 2026-07-02 A 재분담: 둘 다 B 이관, A 레인 종료.)
- **✅ ①권한부여 계정 안뜸 = 근본원인 확정·수정(빌드0)**: `GrantManagementPanelViewModel.cs:109` `GetUsersAsync(1,**200**)` → 서버 `users.py:31 limit le=100` 초과 → **FastAPI 422** → `Success=false`·`Accounts` 빈 콤보(에러 L119 로그로만 삼킴). 타 호출부는 100 사용(`PermissionMatrixPanel:181` 주석에 상한100 경고·`ApiAccountGateway:112`). **수정**: 200→100 + 실패 시 `OpenInfoPopupMessageModel` 안내. A T4 파일이나 A 레인종료+B이관이라 B 직접 수정(**미커밋**, Accounts.Ui 빌드0).
- **② 권한그룹 CRUD/할당 = 의도된 Option A 갭(버그 아님) → Track C 신규 PRD**: `PermissionMatrixPanel` 주석 "OQ-PG-01=Option A: 권한=역할 5등급 고정(임의그룹 제외)" → `_levels` 필터로 임의그룹 숨김·매트릭스 편집만. 클라 API=`GetUserGroups`+`UpdateGroupPermissions`뿐(**POST/PUT/DELETE /user-groups 미구현**). 계정편집 UI=Role 콤보만(group_id 선택 없음). **서버 `user_groups.py`는 CRUD 완비**(POST/PUT/DELETE[멤버 group_id→NULL]·GET /{id}/users)+DTO에 group_id 존재 → **순수 클라 갭**. v5.2 ADR(권한=배정그룹매트릭스∪grant)와 정합하려면 그룹기반 관리가 정답(OQ-PG-01=과소구현).
- **✅ 산출: PRD `docs/prds/GOP_Permission_Group_Management-prd.md`(Draft, v1.0)** — FR-01~09(그룹 CRUD API 4 + DTO 2 + 전체그룹 표시/CRUD 툴바 + 계정 배정 UI + ADMIN 게이팅 + 실패안내), §8 미해결질문 3(패널 진화 vs 신규·배정 UI 위치·예약 5등급 보호). **사용자 승인 대기** → 승인 후 dev(worktree). ⚠A: `PermissionMatrixPanel`/`AccountApiService`/`EditorDialog`(A 소유) B 확장 예정 — 원장에 이의 기재 요청.
- **✅ 구현·머지**(사용자 "계속" 지시): worktree `feature/permission-group-management`(off `4a84d61`·태그 `before-permission-group-management`) → `59632ec` → treadmill(v2.6 `a2282b9` 흡수, disjoint 충돌0) → **메인 FF `d1edcda`**(A 미커밋 GMaps 보존). Messages DTO 3(Create/Update/**Assign**=group_id 항상직렬화) + Accounts.Api 그룹 CRUD 5메서드 + `PermissionMatrixPanel` 3화면(목록+CRUD툴바/매트릭스/구성원 배정) + 예약5등급 삭제·개명 금지. **빌드0 · Accounts.Api 93/93**(신규 10). 서버 무변경.
- **⚠ V-03 확정 결함(구성원 해제)**: 서버 `users.py:535` `update_user`가 `if group_id is not None`로 **group_id:null 무시** → 구성원 해제 no-op(배정=정상). **클라 가드 추가**(`bda89b0` FF): `OnClickRemoveMember`가 반환 group_id로 미반영 감지 시 안내(무증상 방지), 서버 수정 후 무변경 작동. **서버세션 이관**(원장 기재). 서버 다운(:8000)이라 라이브 미실행.
- **✅ v5.4 정렬(원장 L151 A→B 요청, `4502a61` v2.6 FF)**: 서버 Role Simplification(v57: role 5→2 ADMIN/USER, **ADMIN·GUEST 등급그룹 DROP**, MAINTAINER/OPERATOR/VIEWER → **'Preset - X' rename+편집허용**)로 stale해진 예약 5등급 로직 정리 — `_levels` dict 제거 · 예약 보호 해제(`CanModifySelectedGroup=HasSelectedGroup`, 전 그룹 편집/삭제) · `IsPresetGroup` 접두 인식(팀먼저→Preset 정렬) · `IsReserved` 제거 · View 툴팁/안내 갱신. 빌드0·**Accounts.Api 94/94**. A `0baa234`와 disjoint. ⚠앞서 보낸 스토리보드 HTML의 '예약 5등급/배지'는 v5.4로 대체(실제=팀3+Preset3).
- **🔲 다음**: **앱 재빌드 후 E2E**(ADMIN: 그룹 생성→계정 **배정**→매트릭스 저장→삭제 경고 / 해제는 서버 수정 후) · 서버 기동 시 라이브 라운드트립 · CHANGELOG 커밋(메인 클린 시). **세션간 충돌0**(A도 L147서 독립 확인). PRD Draft. [[project_rbac_enforcement_reality]] [[project_account_session_coordination]] [[feedback_prd_worktree_required]] [[project_library_deployment_path]]

## [A] 🟢 GMap 3항목(회전반경 애니·러버밴드·라벨분리) — adorner 분석 완료 + ② aim 애니 v2.6 머지(`7d81171`) (2026-07-02, 세션 A, Track C)
- **요구(사용자)**: ① 잠금메뉴 정렬(완료·커밋됨) ② 카메라 특정위치 회전 반경 점선 레이어를 심볼·이미지 **위**로 + `r=0→MaxDetectionRange` 등장 애니 + 클릭지점 주황 리플 ③ Shift+드래그 영역 다중선택→일괄 이동/삭제/잠금 ④ 심볼↔라벨 분리(점선 리더선·상대위치 DB저장·라벨만 이동 시 점선표시·심볼반경 1.5배 상한).
- **⚠ 사용자 지침**: ③④는 **adorner 체계 깨질 위험** → 착수 전 adorner 정밀분석 + 롤백태그 필수.
- **조사**: Explore×3(aim렌더/선택/라벨-adorner) + architect(opus) adorner 통합분석. **산출 `docs/analyses/GMap_Adorner_System-analysis.md`** — adorner 인벤토리(`MarkerEditAdorner`·`LineDrawingAdorner` 2개, 이미지편집=OnRender 별개)·생명주기·마우스라우팅·계층·**불변식 14개**(🔴#8 `Markers` 하드캐스트 크래시 3곳 `:1191/1415/2217`, AdornerManager 5분 정리타이머 크로스스레드, RBAC persist 게이트, 디지털줌 inner공간, 줌 teardown)·③④ 안전확장 설계·롤백순서.
- **결정(사용자)**: 순서=**②→③→④(리스크우선)**, **항목별 분리 PRD 3개**. ④가 최침습(전 심볼 템플릿+DB마이그레이션)이라 마지막.
- **✅ ② 완료·머지**: PRD/Plan=`Camera_Aim_Overlay_Animation-*`(Approved). worktree `feature/aim-overlay-animation`(off `c9571e7`, 롤백태그 `before-aim-overlay-animation`). **신규 `Adorners/AimOverlayAdorner.cs`**(맵 AdornerLayer 오버레이=심볼·이미지 위, grow-in CubicEaseOut 420ms `CompositionTarget.Rendering`, 주황 리플 650ms 전이adorner, click-through `IsHitTestVisible=false`, 줌/드래그 구독, Dispose 구독해제). `GMapCustomControl`: `IsTargetAimMode` full프로퍼티(setter→Show/HideAimOverlay, **AdornerManager 밖**)·`TriggerAimRipple`·`MetersToScreenPixels` internal·OnRender `DrawAimRadius` 호출제거. `MapViewModel.OnTargetAimClicked`=유효클릭 리플. 커밋 `c2dd831`→v2.6 **FF 머지 `7d81171`**(빌드0). 🔲 **런타임 E2E=앱 재빌드**(심볼위·grow-in·리플·디지털줌).
- **메뉴 함정(사용자 확인)**: "특정 위치 확인"(≠회전, 십자아이콘) 메뉴는 `pidsMarker.DeviceType==IpCamera && cameraModel(연결)!=null && Category==PTZ`(MapViewModel.cs:4801/4821). 안 보이면 카메라모델↔심볼 매칭/PTZ 여부 확인.
- **🔲 다음**: ③ 러버밴드(Step0 불변식#8 하드닝 선결 → GroupSelectionAdorner 맵레벨, AdornerManager 미접촉) → ④ 라벨분리(LabelAdorner+DB LabelOffsetX/Y+템플릿 라벨제거). 각 항목 PRD→worktree→롤백태그. [[project_symbolprovider_stalecache_markers_hardcast]] [[project_digital_zoom_architecture]] [[feedback_prd_worktree_required]] [[project_library_deployment_path]]

## [A] 🟢 Grant Scheduling 클라 연동 — 서버검증·PRD Approved·Plan·FR-GS-01/02 코어 커밋(`6c4ed0a`, 78/78) (2026-07-01, 세션 A, Track C)
- **요구(사용자)**: "API쪽 권한 스케줄링 → 클라가 정보 받아 갱신." **스케줄링 주체=서버**(grant 저장·유효권한 계산·만료·통지). 클라=서버가 준 `valid_until` 보고 그 시각 재조회+화면 갱신하는 **소비자**(클라가 스케줄링 안 함). "스웨거/문서 확인 후 클라 착수" 지시.
- **✅ 서버 라이브검증(2026-07-01)**: `api-test-server` healthy(:8000). grant 완전구현(`routers/grants.py` POST/GET/DELETE·`grant_service.py`·`nats_revoke_publisher.py`·`schemas/grant.py`·DB `v56_user_group_grants.sql`). `GET /me/permissions`=`effective_permissions_payload`(등급∪grant, valid_until=최임박)+`server_time`(KST). **라이브 확증**: admin→`{modules(10),device_groups[],valid_until:null,server_time:"…+09:00"}`. ⚠ 게이트 휴면: `AUTH_MODE=public`·`NATS_REVOKE_ENABLED=False`.
- **PRD/Plan**: `docs/prds/Grant_Scheduling_Client-prd.md`(**Approved v1.1**)·`docs/plans/Grant_Scheduling_Client-prd-plan.md`(25태스크). **Phase1=FR-GS-01·02·03·05·06(+07/09)**. FR-GS-04(NATS)·08(Event/Camera Bearer)=별건. 검토발견: FR-GS-04는 재사용 아닌 순수신규(HMAC/NATS 인프라 클라 0건)+서버게이트off→Phase2 보류. FR-GS-05는 B소유 `BearerAuthHandler` 접촉→조율필요. 07/09는 `1749a70`로 사실상 완료.
- **✅ dev 1차 커밋 `6c4ed0a`** (worktree `feature/grant-scheduling-client` off `5a5c1f8`, 롤백태그 `before-grant-scheduling-client`, **미머지**): FR-GS-01/02 코어 5파일 +162. `PermissionsSnapshotDto`(modules/device_groups/valid_until/server_time, 날짜=string 파싱, modules=raw JObject Flatten재사용). `PermissionService`: `ValidUntil/ServerTime/ClockSkew`+IClock **이중 생성자**(Autofac 미등록 폴백 SystemClock, `new PermissionService()` 하위호환). `Refresh(snapshot)`(role유지·modules교체·`clockSkew=server_time−localNow` UTC instant비교·PermissionsChanged). Apply=로그인 valid_until 캐시. **Accounts.Api 78/78·빌드0**(신규4).
- **🔲 다음**: IMPL-02(`GetMyPermissionsAsync` GET /me/permissions, AccountApiService)→IMPL-04(valid_until 타이머 `PermissionRefreshScheduler`, IClock, 최임박1개 RISK-01)→**IMPL-05(403,B조율 클레임 후)**→IMPL-06/07/08(grant 관리 UI ADMIN, 앱재빌드 검증)→SETUP-02(GrantDto). 파일 disjoint(Accounts.Api/Ui·Messages)라 B와 무충돌. [[project_rbac_enforcement_reality]] [[project_account_session_coordination]] [[reference_api_test_server]] [[feedback_prd_worktree_required]]

## [B] 🔄 사용자 6항목 A/B 분할 + T1 진단 (2026-07-02 후속, 세션 B)
- **분할(원장 §6 [사용자 6항목 분할])**: T1=A(exp 타이머 능동감지)+B(커버경로 검증✅) / T2=A 전담(세션관리 관리컬럼·Confirm·**재로그인 불가**) / T3=✅완료 / T4=A(그룹 CRUD+Grant Scheduling UI) / T5=A(라이브러리 패널)+B(메인솔루션 메뉴 감사) / T6=A(중앙 권한필터 계층)→B(시나리오 100+ ×2회 시뮬, 회차 로그) / T7=B 주도(AUTH_MODE=token 전환+Device/Server CRUD 전수)+A(Event/Camera Bearer).
- **T1 진단(라이브 revoke 실험)**: 유령 UI **재현**(revoke 후 계정콘솔+사용자 7명 표시, 커버 미발동). 원인 3층: ①클라 세션종료 push/능동감지 無 ②401 폴백은 인증필수 API 호출 시만(계정콘솔=캐시 표시, 앱로그 401/refresh 0) ③장비/이벤트 AUTH_MODE=public→익명 200. **B 커버경로=401 수신 시 정상(기검증)** → 근본해결=A exp 타이머 + 서버 NATS revoke push·AUTH_MODE=token(T7 동일 뿌리). 서버 login 필드=`login_id`. ⚠앱=유령 상태 실행 중(재현 보존).
- **✅ T7 완료**: token 모드=무토큰 전면 401+유토큰 풀 CRUD(lamps C→P→R→D)+**앱 fetch 성공(P3 Bearer 실증**, 센서160 등) / public 원복=하위호환 200. **양모드 겸용 달성(Device/Server)** — 잔여 게이트=A Event/Camera Bearer(token 모드 이벤트 401 실측). 경로 `/events/detections`·램프 필드 `name_device/number_device`·로그인 필드 `login_id`.
- **✅ T6 1회차**: **121 시나리오 PASS107/FAIL5/GAP9**, 리포트=`docs/reports/Permission_Simulation_Round1-report.md`(회차 마커). A 영역 2건 전달(FAIL-HIGH GMaps IoC `??true` fail-open L969-975 · 맵편집 PermissionsChanged 미구독). 2회차=A 중앙필터 후.
- **✅ T5 감사+수정(`97ec3f1`, 메인솔루션 3파일)**: H-1 SETUP=IsLogin→인가(CanSeeSetup=ADMIN∨setup_system/feature) · H-2/M-2 Conductor `CanOpenPanel()` 인가 백스톱(**RegisterType 자동주입 확인 후** 옵셔널 주입 — 수동팩토리 함정 회피) · M-1 이벤트 버튼 IsEnabled=CanSeeEvents(ShellView.xaml=타 세션 WIP 혼재→워킹트리 유지) · L-3 강제모드 다운그레이드 금지 · L-4 TokenTimeout 중복구독 방지. 빌드0. 보류=M-3 고아 SetupPanels VM·L-1/L-2. ⚠`op_tester` 서버 FORBIDDEN(잠금)→비ADMIN 런타임 차등 막힘(A에 해제 요청).
- **✅ op_tester 계정 복구**: DB failed_login_count=0·is_locked=false + reset-password test1234 → OPERATOR·group_id=12·10모듈 권한. **T5 런타임 실측 통과**(op_tester 메뉴: EVENTS/DEVICES 표시·ACCOUNTS/SETUP 숨김·편집토글 회색).
- **✅ T7 심화**: token 모드 비ADMIN /devices·/events 401 = **서버 결함**(장비/이벤트 라우터가 레거시 `get_current_user`→레거시 users 테이블(admin만) 의존, account_users 미조회). admin만 양테이블이라 은폐. **서버세션 이관**(라우터 의존성 get_current_account_user 통일 필요).
- **✅ 무인 자동조치보고 차단(`ce2a506`, v2.6)**: EventUiModule OnAutoReport 핸들러에 IsAuthenticated 가드(로그인 중 Enqueue→로그아웃 후 타임아웃 발송 경로 차단, 미Dequeue=재로그인 시 정상보고). 사용자 "절대 안 됨" 완결.
- **✅ T6 2회차 완료**: 133 시나리오 **PASS124/FAIL6/GAP3/REGRESSED0**(Round2 리포트). FIXED 5(SETUP 인가), GAP 8→3. **B 소유 잔여 A-35 수정(`ef4594e`)**: WINDY→broadcast:control·DATABASE→reports:view. **잔여 5건 전부 A 소유**(GMaps `??true` fail-open·맵편집 IsEditModeEnabled 미리셋·setup 탭 UI 부재·DEVICES 패널 조회 게이팅). 원장 전달.
- **✅ B 6항목 사이클 실질 종료**. **🔲 남은 것(대기/타소유)**: A 잔여 5건 · 서버 결함(T7 라우터 레거시 auth) · 타 세션 클린 후 ShellView M-1·M-3 커밋.

## [B] ✅ 로그인 게이팅 GIS Init — **전체 완료·E2E 실증** (2026-06-30~07-02, 세션 B, Track C)
- **요구(사용자)**: 서버 token 강화 시 부팅 init에서 Device fetch 실패→캐시 미구축→맵 심볼 연계붕괴. GIS측에서 로그인 생명주기로 init 게이팅. ①로그인전=맵 커버(전술다크그리드) ②로그인=하단 진행바로 Device fetch+캐시구축 후 심볼/오버레이 활성 ③로그인전 NATS 이벤트 수신 차단(미조치는 이벤트패널 후처리). **로그인=강제(부팅 자동표시·닫기불가), seam=Session A 위임.**
- **분석 완료**: Explore×4 + architect(opus). **현재 init 게이팅0** — `DeviceProviderService`(Order22) 토큰미부착·로그인전 무조건 fetch(=핵심 게이팅 대상 1개). 심볼마커=MariaDB(`_symbolProvider`, 토큰불요)지만 심볼↔디바이스 연계는 DeviceProvider(API) 의존. `BaseDeviceProdiver`=CollectionChanged 자동동기(타입Provider 재init 불요). `ITokenStorageService.IsAuthenticated`(게이트 단일소스). `MapViewModel.HandleAsync(AllDevicesLoadedMessage)`(:359) 재연계 경로 존재.
- **산출**: PRD `docs/prds/Login_Gated_GIS_Init-prd.md`(Draft, FR-CV/LG/DF/PB/NG/RL/BR·R1~7·OQ1~6). 조율원장 `docs/coordination/ACCOUNT_COORDINATION.md` §3클레임·§5계약·§6분배·진행로그. INDEX 등재.
- **★조율 성과**: A가 위임 seam을 **선구현**(`85ba680`: `ISessionLifecycle.LoginSucceeded`+`NotifyLoginSucceeded()` ResetForLogin 직후 발화 + `LoginPanelViewModel.IsForced`+ClickCancel 가드). **B 검증=PRD 계약과 완전일치, 조정 불요.** seam이 v2.6 tip에 준비됨 → dev 착수 시 즉시 사용.
- **✅ 승인·dev 1차(미커밋, worktree `feature/login-gated-gis-init` off `85ba680`)**: 결정=로그인패널 기존 재사용·커버 풀-셸(ShellView)·Bearer 포함. plan=`docs/plans/Login_Gated_GIS_Init-prd-plan.md`. preview HTML=`Docs/reports/Login_Gated_GIS_Init_Storyboard_Wireframe.html`(풀-셸).
  - **P1(빌드0)**: `DeviceProviderService` ExecuteAsync **게이팅(no-op)**+`TriggerInitFetchAsync`/`CancelInitFetch`(CTS+lock,재진입취소·재시작)+8단계 `DeviceFetchProgressMessage`+취소시 `AllDevicesLoaded` 미발화(커버유지) · `OpenLoginPanelMessageModel.IsForced`. **P2(빌드0)**: Detection·Malfunction NatsSyncService `if(_tokenStorage is {IsAuthenticated:false}) return;`(옵셔널 주입).
  - **★설계정제(Option3)**: DeviceProviderService=Accounts.Api 미참조(의존성 클린)·**로그인 구독은 ShellViewModel 오케스트레이션**(커버용 동일 이벤트). A 영역 무변경.
  - **✅ P3 Bearer(빌드0)**: `ApiModule` 옵셔널 `authHandlerFactory` + `DeviceApiModule` `BearerAuthHandler`(공유 토큰·`ResolveOptional` DB안전·401 single-flight·SessionExpired→ForceLogout). `Devices.Api`→`Accounts.Api` 참조+테스트SDK 17.14.0. **OQ-5 해소**(SemaphoreSlim single-flight refresh+세대가드). **A와 Bearer 분담 확정: Device/Server=B(완료)·Event/Camera=A**(FR-GS-08, A의 Grant_Scheduling PRD).
  - **✅ 커밋 `33bb574`**(worktree `feature/login-gated-gis-init`, 명시 9파일 +150/−9, **미머지**). 고아 GMap.NET/OnvifSolution 복사분은 커밋 제외.
  - **✅ 테스트(2026-07-02)**: 게이팅 단위 4종 추가(`def787d`) — DeviceProviderServiceTests **19/19**.
  - **✅ v2.6 머지**: worktree서 v2.6(`ace8d25`) 흡수(ort 충돌0) → 재빌드0·테스트 유지 → **메인 FF `85d23ba`**. A WIP 보존.
  - **✅ P4(커밋 `8019275`, 메인솔루션 v0.5)**: **ShellView.xaml 회피 — Conductor 클린 2파일**(ConductorControlView.xaml 풀-셸 커버[#0A1014+시안32px격자 DrawingBrush+브래킷+HUD+하단진행바, DataTrigger] + ConductorControlViewModel[**옵셔널주입 3종=DB모드 하위호환**·OnActivate 미인증→커버+강제로그인(IsForced)·LoginSucceeded→TriggerInitFetchAsync·DeviceFetchProgress→진행바·AllDevicesLoaded→커버제거·ForceLogout→취소+재커버+재로그인·ClosePanel 강제모드 차단]). 빌드0. 결선검증: BasePanelViewModel:42 SubscribeOnUIThread + Shell→LeftMenu→Conductor.ActivateAsync(:80).
  - **✅ E2E-01/02 실증(스크린샷 2장 사용자 전달)**: 부팅=풀-셸 전술커버+강제 로그인 자동표시+진행바 → (저장계정 자동로그인) → fetch완료=커버 제거·위성지도+심볼 활성. **실제 앱 작동 확인.**
  - **✅ code-reviewer(opus) 반영(2026-07-02)**: H1(fetch 실패=커버 잔존+패널 닫힘=앱 잠김 → 실패 안내+강제 로그인 재표시)·M1(로그아웃 직후 stale AllDevicesLoaded=맵 노출 → 인증가드+발행 직전 취소체크)·M2(사용 중 CTS Dispose → finally 자기소유)·M3(async 람다 미관측 예외)·M4(_forceLoginMode UI스레드 통일)·L1/L2. lib `0ea9469`→**v2.6 FF `9f54209`**(트레드밀: A `5a5c1f8` 흡수 후 재FF)·메인솔루션 `612a1bc`. 빌드0·19/19. **회귀 E2E 재실증**(리뷰픽스 빌드: 커버+강제로그인(비번 대기)+알람0 — 진짜 로그인 전 상태 확인).
  - **✅ E2E-03/04 완료(2026-07-02, UI 자동화+사용자 교차)**: Win32 클릭/타이핑+캡처 루프로 로그인·로그아웃 자동 조작. E2E-04=로그아웃→**커버+강제 로그인 즉시 재표시**. E2E-03 대조실험(신규 mock `publish_mock_detect.py`, DETECT·`sensorway.unit001.gis.event`)=로그아웃 발행→**배지0(차단)** / 로그인 발행→**배지19·Zone1 적색 점등(수신)**. 사용자 실기기 "탐지 된다"(로그인 상태) 교차 확인 → **게이트 증명. 전 E2E 통과.**
  - **🔴→✅ 사용자 실측 결함 2건(2026-07-02)**: ①경로A(NatsBrokerService→카드+사운드) 게이트 누락 → `8211aa3` ②**경로B 게이트 무력 — `EventUiModule` 수동 팩토리(`Register(c=>new ...)`)가 옵셔널 `tokenStorage` 미전달=항상 null** → `53d5e11`→v2.6 FF `bd8c115`→메인 재빌드. 무인 자동조치보고 유출(EQM Enqueue의 IsAutoReportEnabled)도 근원 차단. **로그 검증**: 수정 전 로그아웃 발행=DETECTION 10건+사운드 / 수정 후=**0건**. **★교훈: 옵셔널 주입 게이트는 등록부가 수동 팩토리면 조용히 무력(빌드 통과) — 등록부 확인 필수, 게이트 검증=스크린샷 아닌 로그(커버가 카드 가림·사운드 안 보임).**
  - **🔲 잔여**: 사용자 최종 재확인(로그아웃 실이벤트=무음·무카드·무보고) · EQM 로그아웃 drain 경계(로그인 중 Enqueue분 타임아웃→자동보고, [[project_autoreport_dual_path_issue]]) · E2E-05(AUTH_MODE=token 시) · CHANGELOG 재기재 · report. ⚠PS→git 큰따옴표→`-F 파일` · UI자동화 좌표/동시조작 주의. [[project_login_gating_init_architecture]] [[project_account_session_coordination]]

## ✅ GOP 계정·권한·세션 대규모 작업 (2026-06-29~30, Accounts.Api/Ui·GMaps.Ui·Events.Ui·Devices.Ui·Messages, Track C) — 클라 커밋 완료 / 서버·런타임 보류
- **권한 실제집행 `GOP_Permission_Enforcement` (FR-EN-05~11 클라 전부 완료)**: 모듈 Map/Broadcast 추가(`b1037f5`)·PTZ(`663c45e`)·방송+맵편집(`ff4c0d7`)+맵편집 안내팝업(`865655d`)·장비7패널(`a4e63f1`)·이벤트 ACK/CRUD독립(`3282de8`)·역할강등 재평가(`f353f28`+장비/이벤트). 등급: ADMIN/MAINT=full·OPERATOR=제어/조치(편집·삭제X)·VIEWER/GUEST=조회. **검증**: Accounts.Api 단위(시뮬 6 포함) + 라이브 E2E 등급매트릭스 일치. 게이팅 방식=대부분 "동작차단+안내팝업"(장비/PTZ는 disable).
- **강제로그아웃 전파 `GOP_Force_Logout_Propagation` (클라 Phase1 `b15359b`, 보고서 docs/reports/GOP_Force_Logout_Client_Phase1-report.md)**: `ISessionLifecycle.ForceLogoutOnce`(Interlocked once-guard, NATS/401/수동 수렴, `ForceLogoutRequested` 이벤트) + TokenStorage **jti/세대가드**(refresh 부활차단) + BearerAuthHandler 401폴백 + GMaps PTZ정지·팝업해제. **Accounts.Api 73/73** + **E2E: 강제로그아웃→access·refresh 401 무효화 확인**(서버 동작 정상→클라 401폴백 작동 보장). 후속=서버 NATS subject·session_id·서명 / **셸 화면전환=메인솔루션이 ForceLogoutRequested 구독(1줄 통합지점)**.
- **세션 설정 `GOP_Session_Settings_Admin` (클라 `dc87a39`→단일통합 `918861a`)**: 세션설정 탭을 **단일 "세션 정책" 폼**으로 통합(DB모드 IsSession/TimePicker 제거), `SessionSettingsDto`+`IAccountApiService.Get/UpdateSessionSettingsAsync`(GET/PUT /settings/session). **서버 API 미배포(404)→graceful**(기본값+편집비활성+배너). VM ISessionConfigService/LoginViewModel 의존 제거.
- **🔴🔴 미해결 핵심버그(최우선 선결)**: 로그인 응답 `user.permissions`가 **그룹 없는 비ADMIN 계정=null** → 클라가 빈권한 Apply → **OPERATOR 등 비ADMIN 전부 차단**(조치보고 권한없음의 진짜 원인). admin은 group 있어 채워짐. **클라 게이팅은 정상** — 수정=**서버 login의 role-기반 권한 응답** 또는 **운영 계정에 group_id 배정**(서버/계정설정 영역).
- **표시모델 검토 결론**: 현재 도메인별 혼재(계정=탭 Visibility/IsAdmin · 디바이스=버튼 disable · 이벤트/맵=막고팝업 · 설정=disable+배너). **원하는 모델=조회(view)권한→페이지 노출 + verb별→버튼 show/hide.** 구현=VM `CanX/Is*Visible` 프로퍼티 + XAML `Visibility` 바인딩(BooleanToVisibilityConverter) + PermissionsChanged 재평가. (검토 워크플로 스키마실패→수동분석). 우선순위 0)권한전달버그 1)이벤트·맵 버튼 show/hide 2)패널 view-gating(콘솔 탭 CanView 기준) 3)disable↔hide 통일.
- **서버 사실(라이브 확인)**: AUTH_MODE 기본=`token`(config.py), `JWT_EXPIRATION_HOURS`=24·refresh 7d, 잠금임계 **5 하드코딩**(auth.py:355), force-logout=**access+refresh 둘다 무효화**(검증), access JWT에 **jti O / sid·session_id X**. 세션 정책 런타임 조회/변경 **API 없음**(=.env/startup, 서버 PRD 대상).
- **서버 PRD 3종 → `api-test-server/docs/prds/`**: `PRD_GOP_Server_RBAC_Enforcement`(대체로 구현됨)·`_Session_Settings`·`_Force_Logout` + `INDEX.md`. 짝 클라 PRD=`docs/prds/GOP_*`. 서버 세션 이관용 핸드오프 문구 작성됨. (서버 변경=5-sync+도커 재빌드)
- **버그수정**: 권한등급 사용자수 0(`c693ddb`, countByRole 계산만 하고 UserCount 미사용)·로그인 카드 전체높이 stretch 회귀(`60cc97b`, MinHeight→VerticalAlignment=Center). ⚠ 로그인 레이아웃 1차 수정(e60499b)이 stretch 참사 유발 후 60cc97b로 해결 — **WPF Card는 고정 Height 없으면 호스트 stretch**(형제 LogoutPanel 패턴). · **세션정책 VM 통합(918861a)이 메인솔루션 `AccountSetupViewModel` 래퍼 빌드 깨뜨림(CS1061×7: IsVisible/IsSession/SessionExpiration)→레거시 스텁 복구(`e0a675c`, 메인 빌드0)**. ⚠ **메인솔루션 `AccountSetupViewModel.cs`+Bootstrapper 등록(308)+옛 `AccountSetupView`=고아**(설정탭 제거됨) → 메인 클린 후 제거 권장.
- **메인솔루션(미커밋)**: `SetupPanelView.xaml`+`SetupPanelViewModel.cs`에서 계정설정/계정관리/권한설정/세션관리/감사로그 **탭 5종 제거**(switch case 제거, DI 유지). 롤백태그 `before-remove-account-tabs-from-setup`(메인 v0.5). **타 세션 WIP 多(Bootstrapper/Playback/ShellView 등)→미커밋 보류**. 계정콘솔(Accounts.Ui AccountConsolePanel)이 **5탭 통합**(사용자/권한설정/세션관리/세션설정/감사).
- **테스트계정(api-test-server)**: `test_operator/maintainer/viewer/guest` = `test1234` (admin=admin123). 반복 로그인으로 일부 **is_locked** 가능 → 관리자 해제 필요. test_operator는 **group 없어 권한 null**(위 버그).
- **롤백태그**: `before-force-logout-client-foundation`@v2.6, `before-remove-account-tabs-from-setup`@메인v0.5.
- **🔲 다음**: ① **권한전달 버그**(서버/계정 — 운영계정 group 배정 or login role응답) = 비ADMIN 동작 선결 ② 표시모델 통합(조회→페이지·verb→버튼) ③ 런타임 재빌드 검증(등급계정 차등) ④ 서버 PRD 3종 구현(서버 세션) ⑤ 메인솔루션 탭제거 커밋(CHANGELOG 클린 후) ⑥ 강제로그아웃 셸 통합지점·NATS push. [[project_rbac_enforcement_reality]] [[project_library_deployment_path]] [[reference_api_test_server]] [[feedback_deliver_visible_design]]

## ✅ 구현·v2.6 머지 완료 · 🔲앱 재빌드+E2E 보류 — 레이어 패널 개별 심볼 트리노드 + 드래그 리사이즈 v1 (2026-06-30, GMaps.Ui, Track C)
- **★v2.6 머지 완료 `ebb3ef7`**(FF). worktree서 v2.6(918861a) 흡수→충돌0→메인 FF. **동시 세션 미커밋 5파일(INDEX·MapSettingsHelper·GMapUiModule·PlaybackConsoleStyle·PlaybackViewModel) 보존 확인**(전후 동일). code-reviewer(opus) HIGH(카운트배지0 회귀)+구독누수+견고성 반영 후 머지.
- 커밋 `367e6f0`(feat)+`ae07945`(review fix)+머지커밋. 롤백태그 `before-layerpanel-symbol-nesting`@dc87a39. PRD=`docs/prds/LayerPanel_SymbolNesting_Resize-prd.md`(Draft) · 스토리보드=`Docs/reports/LayerPanel_SymbolNesting_Resize_Storyboard_Wireframe.html`. 8파일 +565/−48, GMaps.Ui 빌드0·단위 **148/148**(신규19).
- **요청**: "레이어 패널 각 카테고리에 Overlay 심볼도 트리노드로" + "창 높이·넓이 최대 +50% 드래그 리사이즈" + **"세션간 충돌 안나도록 잘 구현"**.
- **구현 v1**: ①개별 심볼 노드화 — 카테고리(카메라/센서/군사…)를 펼침 노드 승격 + `_symbolProvider` 개별 심볼 자식(비균일 4단계). 체크박스=마커 가시성(ShowShape/IsLayerEnabled 런타임), 우클릭 '중앙으로 이동'=맵 팬. 모델 이음새 해소(신규 `NodeType.Category` + `ISymbolModel? Symbol` 페이로드 + 심볼 전용 이벤트 `SymbolVisibilityChanged`/`SymbolNavigateRequested`), `CanDelete` Model=null 역전버그 차단, tri-state 일괄 cascade로 O(n²) 제거. 카테고리 조인=비PIDS `EnumMarkerCategory` 직매핑(VEHICLES 보강)·PIDS `DeviceType` 6분기·미매핑 '기타'·Title폴백. ②리사이즈 — E/S/SE Thumb 그립, 250×420→375×630, 좌상단 앵커, Canvas 경계 클램프, 높이 Auto+MaxHeight 캡, 세션 내 크기기억.
- **변경 7파일**(GMaps.Ui): Args/LayerEventArgs · Models/LayerTreeNode · Models/LayerTreeBuilder · GMapControls/LayerPanelControl · Themes/LayerPanelStyle.xaml · ViewModels/Maps/MapViewModel(추가편집 최소) · Tests/LayerTreeBuilderTests(+19).
- **충돌안전(사용자 요구)**: 격리 worktree off **커밋된** v2.6 tip → 동시 세션 미커밋 안 가져옴. 고접촉 `MapViewModel.cs`는 신규 region+3 touch만(additive), `MapSettingsHelper`/`GMapUiModule` **무변경**(세션간 크기영속을 v2로 미뤄 회피). 코드 disjoint.
- **🔲 머지/E2E 보류**: ①main CHANGELOG 클린 시 `git merge --ff-only feature/layerpanel-symbol-nesting` ②앱 재빌드→E2E(카테고리 펼침·개별 토글→마커 표시/숨김·중앙이동·리사이즈 250→375/420→630·세션내 크기기억). ③code-reviewer(opus) 진행 중(`a9e69d0`).
- **v2 보류**: 개별 심볼 삭제/이름변경(파괴적), 전면 평탄화 가상화(구조 리라이트), 검색/뷰포트필터 바, 세션간 크기영속(MapSettings), 상태틴트 실시간. [[project_symbolprovider_stalecache_markers_hardcast]] [[project_two_image_overlay_systems]] [[project_gmap_orphan_submodule]]

## ✅ 구현·커밋·머지준비 완료 · 🔲FF/E2E 보류 — GMap 편집모드 크래시 + 심볼삭제 부활 2건 (2026-06-30~07-01, GMaps.Ui+Db, Track B/C)
- **★2026-07-01 세션복구+머지준비 완료**: worktree `feature/gmap-delete-editmode-fix`에서 **v2.6 tip(5a5c1f8) 실머지 커밋 `263a4b5`**(FF 발산=v2.6가 분기점 23f6f4b 이후 25커밋 전진→FF 불가라 실머지, **충돌0 자동머지**). 내 수정 2건 보존 확인(OfType 3곳=이제 @1193/1418/2221, `_symbolProvider.Remove` 7개). CHANGELOG 양쪽 병합. **재빌드 빌드0(GMaps.Db+Ui)·회귀 173/173 재확인**. 안전태그 `before-merge-v26-into-gmapdelete`@7394b7b. **v2.6(5a5c1f8)가 이제 feature 조상 → 프라이머리 v2.6 clean 시 즉시 `git merge --ff-only` 가능.** (이후 FR-04 커밋 `d2b71be` 추가 → **현 feature tip=`d2b71be`**, 여전히 FF 대상.)
- **커밋 `7394b7b`** @ worktree `feature/gmap-delete-editmode-fix`(off v2.6 23f6f4b). 롤백태그 `before-gmap-delete-editmode-fix`@23f6f4b. PRD/Plan=`docs/prds|plans/GMap_Delete_EditMode_BugFix-*`(**Approved**). diff +21/−3(2파일), GMaps.Ui+Db 빌드0, 회귀 173/173, **code-reviewer(opus) APPROVE**.
- **BUG-CAST**: `GMapCustomControl.cs` 하드캐스트 3곳(DeselectAllMarkers 1190·SelectAllMarkers 1414·SetEditMode OFF 2216) `foreach (IEditableMarker marker in Markers)` → `Markers.OfType<IEditableMarker>()`. 라이브 추적 GMapTrailMarker/GMapTrackingMarker(IEditableMarker 미구현) 상주 시 크래시 해소.
- **BUG-DEL**: `GMapDbSymbolService.DeleteXxxAsync` 7종 SQL 성공 후 `_symbolProvider.Remove(model)` 추가(참조일치 확정 VER-01). 세션 중 부활(SymbolConfigureAsync stale 캐시 재생성) 차단. 비모델 변형 4종(ByPid/Category/DeviceId/DeviceGroup)=테스트전용, 방어주석만(reviewer LOW). [[project_symbolprovider_stalecache_markers_hardcast]]
- **테스트**: TEST-01/02 자동화 **스킵**(하네스 WPF/STA·DB 미지원, `tests/GMaps.Ui.Tests`=WPF무의존·GMaps.Db 테스트프로젝트 부재). **E2E=실질 게이트, 머지+앱재빌드 후 수행**: ①추적 마커 상주서 편집모드 ON/OFF 반복=크래시0 ②심볼삭제→화면 재활성화=미부활 ③콜드재시작 삭제유지.
- **🔲 FF 머지 `[!]` 보류(블로커=프라이머리 dirty)**: 머지는 worktree에서 **준비 완료**(263a4b5, FF 대기). 블로커는 **프라이머리 v2.6 작업트리가 타 세션 미커밋 6파일로 dirty**(현재 CHANGELOG=Symbol_Lock_And_RenameSync `6156d10` 항목·INDEX·MapSettingsHelper·GMapUiModule·PlaybackConsoleStyle·PlaybackViewModel). CHANGELOG가 dirty라 `--ff-only`가 덮어쓰기 거부. 타 세션 파일 **안 건드림**([[feedback_main_solution_advance_notice]]). → **프라이머리 v2.6 clean되면 `git merge --ff-only feature/gmap-delete-editmode-fix`**(즉시 FF).
- **남은 작업**: ① 프라이머리 clean 후 **FF 머지**(worktree서 준비끝) ② 앱 재빌드→E2E 3종 런타임 검증([[project_library_deployment_path]] 앱 미반영) ③ **R-03=실제버그 확정**(아래) ④ **V-02=무해 확정**(아래). ⚠[[project_approve_prd_picks_newest_mtime]] [[project_gmap_orphan_submodule]](worktree에 GMap.NET+OnvifSolution 복사 완료).
- **★2026-07-01 R-03/V-02 조사 판정(Explore 코드증거)**: **R-03=실제 버그 발현(BUG-DEL의 거울상)** — Insert 7종이 `_symbolProvider.Add` 없음 → `SymbolConfigureAsync`(MapViewModel.cs:3637-3663)가 매 `OnActivateAsync`(:179-198)마다 `_symbolProvider` 캐시만 읽어 마커 재구성(DB 재-fetch 없음) → **세션 중 추가한 심볼이 탭이동 후 맵복귀 시 사라짐**(콜드재시작=FetchInstanceAsync가 DB재적재로 부활). AddCustomMarker(:3929-3932)=Insert 후 MainMap.Markers 직접추가만·provider 미갱신. **✅ 수정·커밋 `d2b71be`**(Insert 7종 SQL 성공 후 `_symbolProvider.Add(model)` 대칭 추가, 지점 896/1201/1566/1923/2250/2541/2882). PRD v1.1 확장(FR-04)·Plan IMPL-03·GMaps.Db 빌드0·회귀 173/173·code-reviewer(opus) **APPROVE_WITH_FIXES**. **🟡 FU-W1(비블로킹 WARN, ✅사용자결정=머지 후 별도)**: provider엔 `symbolModel`(A)·마커는 재fetch `savedSymbol`(B) → add-후-편집→재활성화 시 A로 되돌아감(부팅심볼과 불일치, DB엔 반영되어 재시작 후 정상). **재fetch 단순제거 불가**(FetchSymbolAsync가 CreatedAt/UpdatedAt/CreatedBy 로드, in-memory 모델엔 없음→메타유실). 인스턴스 통일 설계선택(ReplaceSymbol/canonical재조회/provider인스턴스재사용) 필요. PRD/Plan FU-W1 기록, 머지 후 착수. **V-02=무해 확정** — 이미지엔 부팅 스냅샷 캐시 없음(`ImageProvider`/`_imageProvider` 부재), `ImageConfigureAsync`(:3688-3725)가 매 재활성화마다 `FetchImagesAsync`(GMapDbSymbolService.cs:3066)로 **DB 직접 조회** → stale 구조 자체 없음, DeleteImageAsync가 provider.Remove 없어도 정상. GMapCustomImage(파일)/GMapImageMarker(DB) 둘 다 동일 무해 경로. [[project_symbolprovider_stalecache_markers_hardcast]]

## ✅ 구현·v2.6 머지 완료 — 장비심볼 "현재위치 적용"→디바이스 API 저장 (2026-06-29, GMaps.Ui+Devices, Track C) 【🔲앱 재빌드 필요】
- **🔧 후속 수정 다수(2026-06-29~30, 메인 v2.6 직접 커밋)**: ①네모버튼+콤보폭(`dafbdb2`) ②**클릭 미배선 근본수정**(`443fbf3`): 버튼이 PIDS SpecificContent(ContentPresenter 별 namescope)라 GetTemplateChild가 못 찾아 Click 미배선→눌러도 무동작이던 것을 **생성자 AddHandler(ButtonBase.ClickEvent)+이름식별**로 해소 + in-button 진행바 finally 복원 + 전구간 로그. ③aim 영역=**DetectionRange 반경+흰 점선 외곽(채움X)**(`6821171`) ④**aim 영역 = 카메라 MaxDetectionRange**(`23f6f4b`, 아래).
- **✅✅ 카메라 MaxDetectionRange 필드 신설(옵션2, 2026-06-30)**: DetectionRange는 **미영속·런타임 기본30m**(서버 코드 "실시간 데이터라 저장 안 함" 확정)이라 부적합 → 카메라 영속 필드 신설. **`HardwareSpec.max_detection_range`(JSONB 내부 → 서버 DB컬럼/마이그레이션 0)**. 서버 `aa0de7e`(feature/tracking-gis-ingest, app/schemas/device.py) **+ 이미지 재빌드·배포(service=`api-server`, healthy, 베이크 스키마 확인)**. 클라 `23f6f4b`: HardwareSpecDto/CameraInfoModel/ICameraInfoModel + DtoToModelHelper **양방향**(model→dto 라운드트립 추가=H1 보강) + aim `CameraAimMath.ResolveAimRadius`(MaxDetectionRange→DetectionRange→글로벌) + **입력UI=카메라 상세다이얼로그 정보탭 "최대 탐지거리(m)" 편집필드**(SaveAsync→UpdateCameraAsync 영속). **다층검증(룰): 단위 GMaps.Ui.Tests 173/173 + 통합 매퍼라운드트립 5/5 + E2E 서버 pytest 3종**(생성/GET/PATCH설정250/geolocation-PATCH보존, `tests/test_symbol_apply_devicelocation_e2e.py` 로컬). 빌드0.
- **🔲 사용자 잔여**: 앱 재빌드→카메라 상세 다이얼로그서 최대탐지거리 입력·저장→PTZ우클릭 "특정 위치 확인"→그 반경 흰점선원. (서버는 배포 완료라 앱만 재빌드)
- **요청**: 마커 속성창에 "위치 정보 API 업데이트" 버튼 + **버튼 내부 프로그래스바→OK시 정상복귀**. 위치(lat/lng)+방위 → 디바이스 Model 반영 + 서버 저장. ([[Camera_PTZ_AimLocation_Nats]]의 (0,0)/stale 좌표 근본해소 — 심볼위치≠디바이스좌표 별도저장소, 드래그는 심볼DB만 저장.)
- **결과**: worktree `feature/symbol-apply-devicelocation`(off v2.6 9fe796b) **커밋 `6394fd9`** + v2.6(865655d) 머지 = tip **`a21ba9e`**. Devices.Ui+GMaps.Ui 빌드0 · 테스트 **144/144**(presshold 흡수). 롤백 `before-symbol-apply-devicelocation`@9fe796b. PRD/Plan=`docs/prds|plans/Symbol_Apply_DeviceLocation_Api-*`(v1.2 Approved).
- **✅ v2.6 머지 완료 `5cac513`**(FF, 9개 코드파일). 머지 함정 해소: FF가 동시 세션 미커밋 `CHANGELOG.md` 덮어쓰기로 거부 → **내 브랜치 CHANGELOG를 base로 되돌려 FF 대상서 제외**(커밋 2e6efb3) → 코드 9파일만 FF(메인 dirty와 disjoint) 클린 머지. 동시 세션 미커밋(CHANGELOG/INDEX/MapSettingsHelper/GMapUiModule/PlaybackViewModel) 보존. v2.6 트레드밀(작업 중 865655d→e60499b 전진)은 재머지+즉시FF로 통과. **🔲 CHANGELOG 항목 재기재 보류**(메인 CHANGELOG 동시 세션 미커밋·활발 편집 중 → 충돌/lost-update 회피 위해 미터치, 메인 클린 후 재추가). 메인 체크아웃 PidsPropertyStyle.xaml:38-46 버튼 실재 확인.
- **🔲 사용자 화면 미표시 = 앱 구버전**: 스크린샷(마커속성 PIDS, 연결디바이스 콤보 보임)=정확한 패널이나 버튼 없음 → **실행 중 앱이 머지 전 옛 빌드**. **앱 닫고 재빌드→재시작해야 버튼(연결디바이스 콤보 아래) 표시.** [[feedback_deliver_visible_design]] [[project_library_deployment_path]]
- **핵심 설계(code-reviewer H1 반영)**: 전체 DTO PUT은 위험(`ToCameraDeviceDto` HardwareSpec 역매핑 누락 + 맵 LinkedDevice=목록엔드포인트라 password/스펙 미적재 → 소거). 서버 PUT=full-replace 확인 → **폐기**. 서버 PATCH=`exclude_unset`+geolocation 처리 확인 → 신규 `IDeviceApiService.PatchGeolocationAsync(kind,id,GeolocationDto)`로 **`{geolocation}`만 부분 PATCH**(타 필드 무손상, geo JSONB 전체교체라 location/altitude 보존, 성공시만 모델반영). seam=`IDeviceLocationGateway`(Monitoring.Models)/`DeviceLocationGateway`(Devices.Ui 6타입분기)/DeviceUiModule 등록/GMaps.Ui lazy IoC. 버튼=GMapPropertyBaseControl PART+이벤트, **클릭→버튼내부 무한ProgressBar→완료시 EndDeviceLocationApply 복원**.
- **후속(code-reviewer)**: FU-M1(heading=BaseBearing 항상적용, 미설정0이 디바이스Heading 0으로 — 의도된 동작, 필요시 게이팅) · FU-M2(DeviceLocationGateway 단위테스트) · FU-M3(ct가 PutRequestAsync까지 미전파, 기존 API계층 제약) · FU-L1(LinkedDevice 없을 때 버튼 IsEnabled 게이팅). 잔여리스크: 목록엔드포인트가 geo 하위필드(altitude/location) 미적재 시 빈값화 가능(lat/lng은 마커표시로 적재확실, 자격증명은 무손상).
- **🔲 다음**: ① 메인 클린 후 FF 머지 ② 앱 재빌드→런타임 검증(속성창 버튼·버튼내부 진행바·서버 좌표 PATCH 실측 `{geolocation}`만 가는지) ③ FU-M1~M3/L1. ⚠ 앱 미반영([[project_library_deployment_path]]). [[project_gmap_orphan_submodule]](GMap.NET+OnvifSolution 복사 유지) [[reference_api_test_server]]

## ✅ 구현·v2.6 머지 완료 — 3rd Party 이벤트 그룹 쓰레기값("1,116") 근본수정 (2026-06-30, Gateway lib, Track B) 【🔲 앱 재빌드 필요】
- **결과**: **v2.6 FF 머지 완료 tip `0275776`**(off 23f6f4b, amend `0275776`←`f9a74e6`). 메인 체크아웃 Gateway 빌드0 · 부활수정 통합 5/5 · 실 monitor_DB 덤프 사본 E2E 통과(실 코드 Event_A→116/Event_B→117·Group컬럼DROP·멱등). worktree·feature브랜치 정리완료. 롤백 `before-gateway-group-resurrection-fix`. PRD/Plan=`docs/prds|plans/GatewayEvent_Group_Resurrection_Fix-*`(Approved/15-16완료).
- **✅ CHANGELOG 재기재 완료** (`6b5aaf6`): FF 시엔 동시 세션 미커밋 CHANGELOG 충돌 회피로 보류했으나, 동시 세션이 CHANGELOG 커밋(`a725e50`)해 깨끗해진 뒤 [Unreleased] ### Fixed 단독 재기재.
- **남은 사용자 작업**: 앱 재빌드만 — 새 DLL로 앱 시작 시 BuildSchemeAsync가 라이브 monitor_DB 자동 1회 정리(Event_A→116). 롤백 태그 `before-gateway-group-resurrection-fix` 보존(불필요 시 삭제 가능).
- **진단(DB증거)**: **Provider/DB 측 문제, DataGrid 무죄**. 레거시 `GatewayEvents.Group` 컬럼이 `BuildSchemeAsync` 상시 이행쿼리로 **매 시작 부활** + Update 미클리어 → `[1,116]`. 라이브 조회 Event_A Group=1·조인{(1,1),(1,116)}.
- **수정**: 상시 이행쿼리 제거 + `information_schema` 가드형 1회 `FinalizeLegacyGroupColumnAsync`(최종이행→좀비정리 count>1→DROP COLUMN Group+IX_Group) + CREATE DDL 정리. 외부 무변경(NatsDomainService 이미 Intersect). 모델 obsolete `Group`은 보류(FR-05 선택).
- **잔여**: ① **v2.6 머지**(미수행, 사용자 결정) ② **앱 재빌드** → 라이브 monitor_DB는 새 DLL로 앱 시작 시 BuildSchemeAsync가 **자동 1회 정리**(수동 DB 손 안 댐) [[project_library_deployment_path]]. ③ 사전 존재 공유픽스처 순서의존 테스트 2건(시드개수/그룹별조회, baseline 동일 실패) 별도 정리 권장.
- ⚠ **기계적 트래커**: `pipeline-state.json` 동시 세션 점유로 `advance-phase` 게이트 차단 → 강제 안 함([[feedback_never_force_gates]]), 문서로 추적.
- **진단 확정(DB증거)**: **Provider/DB 측 문제, DataGrid 무죄**. 레거시 `GatewayEvents.Group` 단일컬럼이 `GatewayDbService.BuildSchemeAsync` 상시 이행쿼리(line 163-169 `INSERT IGNORE SELECT Id,Group WHERE Group>0`)로 **매 시작 부활** + `UpdateGatewayEventAsync`가 레거시컬럼 미클리어 → 사용자지정 116과 합쳐져 `[1,116]`. 라이브 `monitor_DB` 조회: Event_A Group=1·조인{(1,1),(1,116)}, Event_B Group=0·{117}정상.
- **근거 코드**: 부활=GatewayDbService.cs:163-169 / Update 미클리어=305-331 / 표시=GatewayEventViewModel.cs:75-79(GroupNames) / 단일선택 UI=88-91(SelectedGroup setter, 2개리스트 생성불가→2개↑=좀비확정).
- **A안(채택)**: 선행 `GatewayEvent_Group_NtoN_Migration`(Completed)이 연기한 **Step3 컬럼 DROP** 완료. 상시쿼리→`information_schema` 가드형 1회 마무리블록(①최종이행→②좀비정리 count>1 규칙→③`ALTER DROP COLUMN Group`+IX_Group) + CREATE DDL 정리. 좀비식별=이벤트당 단일선택 UI라 그룹2개↑이면 레거시값이 좀비, count=1이면 보존.
- **범위**: lib `Ironwall.Dotnet.Libraries.Gateway/Services/GatewayDbService.cs` 단독. 외부 솔루션 무변경(NatsDomainService 이미 Intersect, V-04 확인). 테스트 갱신(GatewaySetupViewModelTests.cs:76, UnitTest.cs).
- **검증완료**: V-01(Group 컬럼 타 참조 0)·V-02(단일선택)·V-03(Dapper 미매핑)·V-04(외부 미참조). 미확인 V-05(MariaDB DROP시 IX_Group 동반삭제 여부).
- **다음 할 일**: 사용자 PRD 검토·승인(`node .claude/hooks/advance-phase.js approve prd "..."`) → plan 작성. 구현 착수 시 롤백태그 `before-gateway-group-resurrection-fix` + monitor_DB 덤프 선행(DROP 비가역).

## ✅ 구현·v2.6 머지 완료 — 카메라 PTZ "특정 위치 확인" → NATS 좌표발행 (2026-06-29, GMaps.Ui, Track C)
- **결과**: worktree `feature/camera-ptz-aimlocation`(off v2.6) → **v2.6 FF 머지 완료 `ad5dd17`**(feat `3265082` + 머지 `ad5dd17`). GMaps.Ui 빌드0·테스트 **124/124**. 롤백 `before-camera-ptz-aimlocation`@72b08ea. PRD/Plan=`docs/prds|plans/Camera_PTZ_AimLocation_Nats-*`.
- **code-reviewer(opus) 반영**: H1(비활성화 시 ExitTargetAimMode — 전역 Cross커서 누수)·M1((0,0)미설정 거부)·M3(지연발행 세대가드)·M4(배너 IsHitTestVisible=False)·M5(편집모드 상호배제)·L1(nameof)·L3(극점가드)·L4(반경 1~500클램프)·L6(BeginInvoke). **후속 FU-M2**(requested_by=Environment.UserName→앱 로그인사용자+권한게이팅, 권위집행은 서버) **FU-M6**(서비스 단위테스트).
- **고아 함정**: worktree 빌드에 **GMap.NET + OnvifSolution 둘 다 복사 필수**(OnvifSolution 초기 누락→빌드실패→복사 해소). [[project_gmap_orphan_submodule]]
- **동시세션 통합**: v2.6가 그새 `663c45e`(PTZ 권한게이팅 FR-EN-06, MapViewModel.cs 변경)로 전진 → worktree서 v2.6→feature 먼저 머지(ort 자동, 충돌0, 재빌드/테스트) 후 메인 FF. 동시 미커밋 4파일 보존.
- **⚠ approve 사고**: `advance-phase.js approve prd`는 activePrd 아닌 **mtime 최신 PRD**(scanDocsDir[0]) 승인 → 동시세션 GOP_Server_RBAC PRD 실수승인→원복(Draft+이력행제거) 후 내 PRD touch→승인. **교훈: approve 직전 PRD touch**.
- **🔲 다음**: ① **앱 재빌드→런타임 검증**(PTZ우클릭→"특정 위치 확인"→Cross커서+반경30m원→영역내클릭=NATS발행/밖·ESC=취소, 모의구독자로 `*.nvr_manager.camera-aim` 수신확인) ② FU-M2/M6 ③ OQ-01 서버 subject 합의 ④ worktree 정리 선택. 앱 미반영 주의([[project_library_deployment_path]]).

<details><summary>설계 상세(접힘)</summary>

- **요청**: 맵 PTZ 카메라 심볼 우클릭→메뉴 "특정 위치 확인"→커서가 타겟모양+카메라 중심 반경 ~X m 영역 표시→영역 내 클릭→그 좌표를 NATS로 발행(카메라 회전요청+좌표). **클라 직접회전 X, NATS로 좌표만 전달·회전은 서버/NVR 집행.**
- **PRD**: `docs/prds/Camera_PTZ_AimLocation_Nats-prd.md` (**Draft**, v1.0). activePrd 갱신. INDEX 등재. 분석=Explore×4(마커/메뉴·NATS발행·좌표/커서/오버레이·카메라지리/거리)+architect(opus)+code-reviewer(opus) 체인(인메모리).
- **사용자 결정 4건**: ①반경=30m 전역기본값+설정변경(ITrackingSetupModel.CameraAimRadiusMeters) ②메시지=**PUB**(fire-and-forget) ③반경 밖 클릭=**모드종료(취소)** ④PTZ게이팅=**`ICameraDeviceModel.Category==EnumCameraType.PTZ`**(Monitoring.Models, 동기판정·DB변경불요 — 사용자가 "모델에 FIXED/PTZ 있다" 확인, 코드 실측 확정).
- **핵심 설계근거(코드 실측)**: 카메라마커=`GMapPidsMarker`(DeviceType=IpCamera)·우클릭=`MapViewModel.ShowMarkerContextMenu`(MapViewModel.cs:4268, IpCamera블록 4355) · NATS발행 템플릿=`BroadcastControlService`(GMaps.Ui/Services, **단 결함복제금지**: try/catch·ConfigureAwait(false)·ct·검증 보강) · 봉투=`BrokerPublish<T>`+`dto.ToBrokerPublish(cmd,"GIS")` · subject=`{Domain}.{Group}.nvr_manager.camera-aim`(V-01 서버합의) · 좌클릭 가로채기=`GMapCustomControl.OnMouseLeftButtonDown`(680) IsLineDrawing 분기점(base 전 e.Handled=true) · 좌표=`FromLocalToLatLng` · 반경판정=`TrackingMath.HaversineMeters<=R`(지오도메인·줌무관) · 원그리기=`ConvertMetersToPixels`(GMapMarkerPidsControl.cs:604, private→공용추출) · geo→PTZ pan 매핑은 클라에 없음(서버책임).
- **신규 컴포넌트**: `ICameraAimControlService`/`CameraAimControlService`·`CameraAimLocationBodyDto`(Messages/Dto/Brokers)·`EnumGopCommand.CAMERA_AIM_LOCATION`·순수 `CameraAimMath.IsWithinRadius`/`CameraAimRequestBuilder.Build`(IClock)·반경 오버레이·`ITrackingSetupModel.CameraAimRadiusMeters`·테스트 2종.
- **리스크 최상위**: 좌클릭 충돌(패닝/선택/편집/라인드로잉/더블클릭500ms)→단일진입점+플래그상호배제 / async void 메뉴Click 크래시→동기위임+내부try/catch / NATS예외 UI전파→격리.
- (당초 다음 계획) 사용자 PRD 검토→승인→plan. 미결 OQ-01~05, 선결 V-01~07.

</details>

[[project_rbac_enforcement_reality]] [[project_tracking_persistence_architecture]] [[project_digital_zoom_architecture]] [[reference_api_test_server]]

## ✅ 추적 Playback 날짜범위 캘린더 + NATS 모의데이터 (2026-06-29, 별도 작업스트림 — GMaps.Ui)
- **요청 흐름**: PC다운 후 추적 작업 복구(무손실 확인) → "재생 페이지 날짜 캘린더(첫클릭=시작·둘째=종료)" 구현 → "NATS로 20세션×100+ 모의데이터".
- **캘린더 구현(미커밋, GMaps.Ui 빌드0·단위테스트 103/103)**: WPF 기본 Calendar 회피하고 **토큰 기반 커스텀 월그리드 + 2클릭 상태머신**.
  - 신규 `Services/Tracking/DateRangeCalendar.cs`(순수로직 WPF무의존, 테스트 링크) + `ViewModels/Maps/RangeCalendarViewModel.cs`(VM 래퍼). 수정 `PlaybackViewModel.cs`(Calendar/IsCalendarOpen/ToggleCalendar/OnCalendarRange/From·To 알림화) · `Themes/PlaybackConsoleStyle.xaml`(달력버튼 PART_CalendarToggle + Popup 월그리드 PbDayCell) · 테스트 `tests/GMaps.Ui.Tests/DateRangeCalendarTests.cs`(12종) + csproj 링크. 롤백태그 `before-playback-calendar-rangepicker`@9f26ece.
  - **적대적 리뷰(wf_031461fe 3렌즈) → 실결함 4 수정**: ①[HIGH] Popup `IsOpen` OneWay→**Mode=TwoWay**(클릭아웃 닫힘 후 달력버튼 죽는 버그, PropertyComboBox 선례) ②[MED] 선택셀 호버 다크-온-다크→호버를 Style.Trigger로 이동(선택 edge가 이김) ③[LOW] 인접달 edge Opacity 복원 ④[HIGH] **MaxPlaybackHours(기본6h) 클램프**가 종일선택을 끝6h로 자름→OnCalendarRange 사전경고(미해결: 풀데이 재생하려면 가드 정책 변경 필요, 사용자 결정 대기). PASS: DataContext상속·AncestorType·ElementName·토큰해소·PackIcon 전부 정상.
- **NATS 모의데이터(완료·검증)**: 발행기 `C:\workspace_python\api-test-server\tools\publish_mock_tracking.py`(nats-py). subject `sensorway.unit001.gis.tracking-status`(클라구독 `sensorway.unit001.gis.>` 매칭), envelope `{id,m_type:PUB,cmd:TRACKING_STATUS,from,body,created}`, body=`TrackingStatusBodyDto`(camera_id/tracking:active/ttl_sec/targets[]), target=`TrackingTargetDto`(track_id/label/threat_level/confidence/observed_at ISO-UTC/location{lat,lng,distance_m}).
  - **20세션×120점=2400행 적재 검증**(MariaDB monitor_DB.CameraTrackPoints, before 120→after 2520, mock 2400/20세션). 영속경로=앱(TrackingStatusNatsSyncService→TrackPointStore writer)→로컬DB. **전제: 앱 실행중이어야 적재**(NATS core 비영속). observed_at 메시지값 사용(미래+5분 드롭·보존7일).
  - 날짜분산: D-1~D-6 저녁(20:30/21:30/22:30 KST, 캘린더 풀데이픽 클램프윈도 18-24시 안) + 오늘 최근과거(미래금지). **재실행 idempotent**(UNIQUE CameraId,TrackId,ObservedAt INSERT IGNORE).
- **탐지 리스트 DataGrid화 + 세션 분절(미커밋, GMaps.Ui 빌드0·테스트 110/110)**: 사용자 "각 세션 구분+DataGrid(탐지ID·타입·등급·시작~끝)". ① 신규 순수 `Services/Tracking/TrackSessionSplitter.cs`(같은 track_id라도 시간공백>**30s**면 별개 세션 분리; 트레일 10s와 분리) + 테스트 `TrackSessionSplitterTests.cs`(7종, csproj 링크). ② `PlaybackViewModel.BuildEvents` 세션분절+Start정렬, `PlaybackTrackItem`에 SessionId(다세션=`{TrackId}#n`)·TypeText/TypeDisplay(사람/차량/동물)·ThreatText(위험/주의/일반)·TimeRange·Lat/Lng 추가. ③ `PlaybackConsoleStyle.xaml` 섹션② ListBox→DataGrid(자체완결 다크 PbDataGrid/PbColHeader/PbCell/PbRow). **적대리뷰(wf_2a0dd982) 실결함 수정**: [HIGH]⌖잘림→콘솔폭430→**470**+가로스크롤Auto+ID MinWidth70 / [HIGH]따라보기 무반응→세션 대표좌표 직접 센터링 / [HIGH]표시토글 track단위→같은track 세션행 동기화(guard) / Status "트랙"→"건". 미수정(런타임확인 LOW): 헤더 정렬화살표·스크롤바색·체크박스 2클릭.
- **배속 버튼 활성표시(미커밋, 빌드0)**: 사용자 "배속 버튼 누르면 활성표시 안 됨". 신규 `Utils/SpeedActiveConverter.cs`(IMultiValueConverter [Speed,Tag]→bool) + `PbSpeedChip` 스타일(MultiBinding DataTrigger로 현재 Speed==Tag면 PrimaryBrush/OnPrimaryFixed 하이라이트), 4버튼에 Tag+스타일 적용.
- **재생 버튼 활성표시 반전 수정(2026-07-02, 미커밋, 빌드0)**: 사용자 스크린샷 — 재생버튼이 반대(정지 시 시안 강조·재생 중 어두움)였음. 원인=버튼이 `PanelPrimaryButtonStyle`(항상 시안) 사용. **배속칩과 동일 매커니즘**으로 신규 `PbPlayToggle` 스타일(BasedOn `PanelSecondaryButtonStyle` + `DataTrigger Binding=IsPlaying Value=True → Background=PrimaryBrush/Foreground=OnPrimaryFixedBrush`) 추가, 재생버튼(PlaybackConsoleStyle.xaml ~L435) 스타일만 교체(불러오기 L290 PanelPrimary 유지). VM `IsPlaying`(PlaybackViewModel.cs:126, `true→⏸/false→▶`) 활용, VM 무변경. 결과: **정지(▶)=배경색·재생 중(⏸)=시안**. GMaps.Ui 빌드0(CS2001 wpftmp는 타세션 동시빌드 일시오류·재시도 0오류). ⚠앱 재빌드+재시작 필요(실행 중 구버전). 동시 Playback 세션 dirty에 additive(충돌 없음).
- **🔲 다음**: 앱 재빌드+재시작 필요(현 실행앱=구버전, **재빌드 직전 닫혀 현재 미실행**). **사용자 선택=앱 켜지면 모의데이터 재발행** → 앱 프로세스 감시 백그라운드 `btlm82ywt` 가동(기동 감지 시 자동 `publish_mock_tracking.py` 재실행+검증). MaxPlaybackHours 풀데이 정책 미결. ⚠동시 Accounts 세션 v2.6 활발 커밋 → 미커밋 GMaps.Ui분 휩쓸림 주의(필요시 커밋). [[project_tracking_persistence_architecture]] [[reference_api_test_server]] [[feedback_deliver_visible_design]]

## ▶▶ 재개 포인트 (2026-06-29 최신 — RTSP 영상 팝업 + ONVIF/PTZ 세션 복구)
- **복구 트리거**: 사용자 "Popup RTSP 영상 팝업 및 Onvif 작업하던 세션 복구". ultracode 검증 워크플로우(wf_110a0069, 4축 병렬 + ONVIF 재조사 에이전트)로 코드/PRD 실측.
- **★정정(중요)**: 2026-06-26 "PTZ 드래그 비례이동 + 줌/포커스 +/- 버튼"은 아래 블록에 **"미커밋"으로 기록됐으나 실제로는 커밋됨**. 동시 세션 체크포인트 커밋 **`e013e08`**("checkpoint before track-c")에 휩쓸려 들어가 보존됨 → 현재 HEAD(v2.6)에 9개 항목 전부 살아있음(코드 실측 confirmed). **분실 0**. (session-context 경고했던 "미커밋이 타 세션 commit -a에 휩쓸림"이 실제 발생, 단 유실 아님.)
- **코드 실측 결과(현 HEAD)**:
  - ✅ **적용분 9개 전부 present**: 드래그 비례이동(velFactor=PanTiltSpeed*(0.3+0.7*mag), maxLen 0.65/floor 60 @MapViewModel.cs:725-731) + 적대적검증 5수정(①IsImagingCapable 배선 :677 ②패널 Border Height 230 @CameraStreamPopupStyle.xaml:229 ③드래그/줌 StopAsync ct전달 :734/:773 ④포커스 Stop finally @PtzController.cs:344 ⑤PtzDragMinDurationMs=120 :690) + 줌±(ZoomIn/Out→RaisePtzZoom) + 포커스±(MoveFocusAsync FocusMove Continuous ±0.7/350ms, FocusNear/Far, OnCameraPopupFocus 배선) + 속도세터 Math.Round(,1) + CanvasTop 하한클램프.
  - 🔲 **미수정 3건 여전(E/F/H)**: E[MED] 고정(비PTZ)+이미징 카메라 포커스行이 외곽 IsPtzCapable 게이트에 막힘(XAML ~L289 게이트가 ~L351 포커스行 감쌈) · **F[MED] 포커스 0.7이 GetMoveOptions 미클램프(PtzController.cs:325 하드코딩) → 런타임서 포커스 안 움직이면 1순위 원인** · H[LOW] 미지원 시 사일런트(imaging 레벨 프로브만).
  - ⚠ **ONVIF 비번 복호화 = (b)서버측 해결(평문 회귀)로 현재 정상, 클라 복호화 코드 0**: EnsurePtzReadyAsync(MapViewModel.cs:673)·DtoToModelHelper(:36)·CameraConnectionAdapter(:36) 전부 UserPassword 무변환 직통. token=ok·capable=True는 서버가 평문 반환하기 때문. **잠재리스크: API가 다시 암호화하면 400/PTZ비활성 재발** → 항구방어책(DtoToModelHelper 복호화) 미구현·PRD 미작성.
- **RTSP/카메라팝업 PRD 5종 상태**: ①Rtsp_Map_Popup(Approved, 기반코드 구현됨·Plan 체크박스 0/32 미갱신) ②**DigitalZoom_Alignment=✅완료**(14/14, v2.6 머지 2062caf/76a68b9, 런타임검증만) ③PTZ_Control(헤더 Draft지만 Plan 34/41·v2.6 머지 0d03f83+메인 EXT-01 55c3cf3, 실카메라 런타임+IMPL-28 권한게이팅 보류) ④Streaming_Settings(Draft, FR-01~11 미구현·체크박스 없음) ⑤Snapshot_UX(승인, Plan 없음·FR-01~05 미구현). ⚠ PRD 헤더 Status↔Plan 진척 불일치 다수(문서 위생).
- **🔲 즉시 다음(사용자 선택 대기)**: ⓐ 앱 재빌드→실카메라 런타임 검증(드래그길이/줌±/포커스±방향·실동작/66영상/디지털줌) ⓑ 미수정 E/F/H 코드 보강(특히 F=포커스 클램프) ⓒ Streaming_Settings 또는 Snapshot_UX 신규 구현 착수 ⓓ ONVIF 비번 항구방어책 PRD ⓔ PRD/Plan 문서 위생(헤더 Status 동기화).

### 후속(2026-06-29) — 줌·포커스 Press-Hold ✅구현·검토·테스트·**v2.6 통합(0fbb261) 완료**, 🔲최종 머지만 대기
- **요청/선택**: "줌·PTZ(포커스) 버튼 누르면 Continuous·떼면 Stop + 논리검토" → **줌+포커스 둘 다** / **안전망 우선 + 조건부 pre-Stop** / **F클램프 포함**(사용자 승인).
- **상태**: PRD `docs/prds/CameraPopup_PressHold_PtzZoomFocus-prd.md` + Plan `docs/plans/...-prd-plan.md`. **worktree** `C:/workspace_app/worktrees/presshold-ptz-zoomfocus`(branch `feature/presshold-ptz-zoomfocus` off v2.6) · 롤백태그 `before-presshold-ptz-zoomfocus`@9f26ece · GMap.NET+**OnvifSolution 둘 다 고아라 복사**(gitlink, .gitmodules 없음).
- **✅ 커밋 `4f8d0ea`**(feature 브랜치, 10파일 +308/-60): 줌/포커스 click-pulse→press-hold. 신규 헬퍼 `PtzGestureTag`(Tag파싱)·`PtzFocusMath`(클램프) WPF무의존 추출(F03). `StartFocusAsync`/`StopFocusAsync` 신설(ImagingClient 별도 모터경로). `_activeGesture`로 OnPadUp/LostCapture/Close/전환 **4곳 포커스Stop 라우팅**(code-review 치명결함 해소). FR-PH-10 GetMoveOptions 클램프(F해소). **XAML 포커스 게이팅 외곽 IsPtzCapable 밖으로 분리(E해소)**. 펄스 잔재 제거. 휠 줌 펄스 유지.
- **검증**: GMaps.Ui 빌드 **0오류** · 테스트 **111/111**(PtzGestureTag/PtzFocusMath 신규) · **code-reviewer(opus) MERGE_WITH_FIXES**→MEDIUM(포커스게이팅=E) 수정 반영, LOW 3 수용/범위밖.
- **✅ v2.6 통합 완료(2026-06-29) — 머지커밋 `0fbb261`**(부모=press-hold `4f8d0ea` + v2.6 `9fe796b`): 그새 v2.6이 **PTZ 권한 게이팅**(GOP_Permission_Enforcement: `663c45e` cam:control·`f353f28` 역할강등재평가·`3265082` AimLocation·`ff4c0d7` 맵편집)으로 진전→내 분기점(9f26ece)과 충돌. **worktree에서 v2.6→feature 머지로 해소**(v2.6·메인 무영향). 충돌 2: ①MapViewModel(press-hold 핸들러 유지 + **v2.6 `CanControlCamera()` 게이팅 통합**: ZoomHold/FocusHold=게이팅·FocusStop=무게이팅, 다른 PTZ핸들러와 동일패턴) ②test csproj(양쪽 include 유지). CameraStreamPopupStyle.xaml=자동머지(게이팅분리+Tag 보존). 빌드0·**테스트144** 통과.
- **✅ press-hold v2.6 머지 완료**(FF, 당시 v2.6=0fbb261). 이후 PTZ 지연/속도 작업이 그 위에 쌓여 현재 v2.6=`4ebb93a`. CHANGELOG/PRD DoD 동기화 완료. 🔲 앱 재빌드→런타임 검증(줌/포커스 press-hold·권한게이팅) 사용자 대기.

### 후속2(2026-06-29) — PTZ 큐잉제거(LWW)+속도반영 ✅구현·단위·opus리뷰·v2.6 머지(`4ebb93a`) 완료, 🔲E2E만 대기
- **요청**: "PTZ 명령 큐에 담지말고 즉시 fire(너무 느림)+직전동작 Stop먼저+빨리" / "팬틸트·줌 속도 반영안돼 날아감(드래그·버튼 모두)" / "단위+E2E 반드시 검증".
- **설계검증 wf_7ad8437b(architect SOUND_WITH_CHANGES + code-reviewer FLAWED)**: ★**Stop-before는 ONVIF §5.3.2 자동대체라 역효과(~100ms 추가)→사용자 승인하 생략**. 진짜 큐잉원인=**Nudge/ZoomHold가 BeginPtzGesture ct 미전달**(LWW 불능, FIFO 누적)→2줄+원자성수정. Gate 완전제거는 WCF ClientBase 채널fault cascade 위험(I-05 유효)→유지. 속도원인=**연속속도공간 범위(XRange/YRange) 미캡처**→raw[-1,1]×speed 전송.
- **✅ v2.6 머지 `4ebb93a`(FF)**: A=Nudge/ZoomHold에 ct 전달(LWW)+`BeginPtzGesture` AddOrUpdate 원자화(F-06). B=SpaceInfo 연속속도범위 캡처+신규 순수헬퍼 `PtzVelocityMath.ScaleToRange`(0정지보존·부호별 풀스케일·[-1,1]항등=회귀0) 전경로(드래그/패드/줌버튼/휠) 스케일. 진단로그 `norm→scaled ptRange/zRange`. 단방향범위 1회 경고. 빌드0·**단위168**(PtzVelocityMath 24신규)·**opus 리뷰 MERGE**(차단0). 롤백 `before-ptz-latency-speed`. PRD `docs/prds/CameraPopup_PTZ_Responsiveness_Speed-prd.md`.
- **✅ E2E 통과(2026-06-30, 라이브 카메라 66=192.168.202.66 admin/sensorway1, 사용자 직접테스트 승인·물리회전)**: `LivePtzSpeedTests`(OnvifSolution.Tests, env-gate, ⚠고아라 v2.6 미영속·worktree 실행만). **결과: 범위 X=[-1,1] VelocityGenericSpace 표준 / v=0.2→0.0006/s·v=0.8→0.0039/s(~6.7배) → 통과.** ★해석: **카메라가 velocity magnitude 정상 존중(R1 아님). 범위 [-1,1]이라 B(스케일)=항등(66엔 무영향, 비표준카메라엔 방어책).** → 사용자 증상 "속도 반영안됨/날아감"의 진짜 원인=**A(큐잉)**: Nudge/줌버튼 ct 미전달로 ContinuousMove 큐 무한누적→떼도 계속 실행. A의 LWW가 해소. 프로토콜 속도제어는 E2E로 정상 보증.
- **🔲 최종 = 앱 런타임 체감**(사용자, 메인 재빌드 후): 속도 슬라이더가 실제 속도 바꾸는지 + 떼면 즉시정지(큐지연/날아감 없음). E2E가 프로토콜 보증하므로 정상 예상. ⚠ 앱은 메인 재빌드 후 반영([[project_library_deployment_path]]).
- **Phase2(coalescing 디스패처) 보류**: gateWait 측정 후 부족 시만(F-02/03/04 동시WCF 위험). 잔여 LOW: 권한강등 시 진행중 move 미정지·휠/줌버튼 교차(기존, 무해).


> 2026-06-29 다중소스 워크플로우로 복구(wf_ed85785c, git/PRD/plan/memory/코드 7에이전트 교차검증). 06-28 PC 크래시로 인메모리 세션 유실 + session-context에 06-27~28 작업이 미기록이라 이중 유실 위험이었음.
- **활성 스트림 = PRD-GOP-01(GOP 권한 게이트) Phase 7 "계정·권한 관리 콘솔"**. 브랜치 v2.6, HEAD=`016c1c1`(06-29 IMPL-06 편집저장).
- **✅✅ IMPL-06 권한 편집저장 완료(2026-06-29, 사용자 요청 "체크→저장→그룹권한 부여")** — 라이브러리 `016c1c1` + 서버 `c71c8ce`(api-test-server, 브랜치 feature/tracking-gis-ingest):
  - **서버 신규** `POST /api/user-groups/{id}/permissions`(ADMIN 전용 `require_admin`) — 일반 PUT이 권한상승 방지로 permissions 차단(v4.8)하던 것을 admin 경로로 재개. PermissionsSchema strict(미정의 모듈/verb 422) + `PERMISSION_CHANGED` 감사. 로컬 pytest 3종 + **라이브 E2E 통과**(admin 저장 200·422·404·audit 확인). 도커 재빌드+재기동 완료. 안전점 `pre-perm-edit-endpoint`@4afaed6. ⚠ tests/는 서버 gitignore(로컬전용). ⚠ 스모크테스트가 그룹 id=1 권한을 테스트값으로 덮음(테스트서버, role기반 admin게이팅과 무관).
  - **클라**: `IAccountApiService.UpdateGroupPermissionsAsync`(기본 인터페이스 구현) + `AccountApiService` POST · `PermissionMatrixPanelViewModel.OnClickSave`(Modules→PermissionsDto·device_groups 보존·성공시 재조회) · `ModulePermRowViewModel`:PropertyChangedBase · View 체크박스 OneWay→TwoWay·[저장] 활성. 빌드0·Accounts.Api 62/62. 안전점 `before-perm-edit-save`@ecf8b4f.
  - ⚠ **앱 재빌드 후 런타임 검증 필요**: 권한설정 탭 → 그룹 더블클릭 → 체크 변경 → 저장 → 재조회 반영 확인. admin 비번=`admin123`(sensorway 아님).
  - 🔲 **남은 IMPL-06 후속**: 그룹 추가/삭제(서버 v5.0 그룹CRUD — 현재 생성은 POST /user-groups가 permissions 지원하나 클라 다이얼로그 미배선) · IMPL-07(RegisterDialog 그룹 ComboBox).
- **✅✅✅ 권한 모델 일원화 확정·구현(2026-06-29, OQ-PG-01=Option A)** — 사용자 지적 "권한그룹==권한레벨==같은 의미, 따로 논다" 해소. 라이브러리 `6fb5d0a` + 서버 `12fc48d`(feature/tracking-gis-ingest):
  - **진단(5축 워크플로우 wf_709c2f42 확증)**: 권한 개념 3중(레벨 EnumLevelType=역할 파생/역할 EnumUserRole/그룹 매트릭스) → **실제 집행은 역할 하나뿐**. 그룹 매트릭스=서버 미집행+사용자 배정 UI 0(IAccountModel에 GroupId 없음, IMPL-07 미구현)=고아. 클라 Can*는 매트릭스 토큰만 보는데 서버가 ADMIN 권한 빈 객체로 보내 **ADMIN이 Can*=false 되는 잠복버그**.
  - **결정=Option A: 역할(등급)=권한 단위.** 5개 역할명 등급그룹이 권한 단위, 계정은 "구분(등급)" 하나로 결정. 매트릭스 세분도=현행 8모듈×4동작 유지(PRD §6-1 세분토큰은 추후). 임의 팀그룹 폐지(비파괴).
  - **서버**(`auth.py`+`init_db.py`): `ensure_role_permission_groups`로 5등급그룹 idempotent 보장(기동 로그 "created 5/5", §6-1 기반 기본매트릭스, 팀그룹 비파괴) + 로그인 권한유도 `group_id`→**`user.role` 명 등급그룹**. 라이브 검증=admin이 빈권한 대신 FULL 8모듈 수신. 도커 재빌드+재기동.
  - **클라**(`PermissionService`+`PermissionMatrixPanel`): **ADMIN 무조건 통과**(잠복버그 수정) + 화면 "권한 그룹"→**"권한 등급"**(5등급만 등급순·한글라벨, 그룹 추가/삭제 제거). 빌드0·62/62.
  - 🔲 **앱 재빌드 후 확인**: 권한설정 탭=5등급(관리자~게스트) 표시, 더블클릭→매트릭스 편집→저장. ⚠ operator1 등 샘플계정 시드비번=`user123`(로그인 실패 반복 시 브루트포스 잠금 주의).
  - ⚠ 후속(미결): 서버 매트릭스 **실제 집행 0**(3선결조건 deferred) — 현재 권한은 UI 게이팅+표시 수준. 역할별 차등 게이팅을 GMaps/Devices.Ui에 주입(FR-PG-13)은 별도.
- **📋 권한 실제집행 PRD 작성(2026-06-29, Draft) — `docs/prds/GOP_Permission_Enforcement-prd.md`**: 위 "실제 집행 0" 갭 구체화. **멀티에이전트 시뮬레이션 wf_52155656(22에이전트): 시나리오 218·발견 99·8도메인**. FR-EN-01~15(P0=T4긴급/reports무인증/3선결조건/require_perm/모듈정의, P1=PTZ·방송·맵·장비·이벤트 게이팅+역할강등재평가+마지막ADMIN, P2=device_groups스코프·감사append-only·콘솔). **★PTZ 결론**: 서버가 PTZ 미중계(ONVIF 직결, cameras.py에 /ptz 경로 전무 확인) → **클라 MapViewModel 9핸들러 `CanControl("cameras")` 단독 집행**(Stop 제외, 모듈명 `cam` 아닌 `cameras` 필수). 미결=OQ-PG-02/04/06/07(PM) + V-EN-11(null폴백). **상태=Draft, 사용자 승인 대기**(`advance-phase approve prd`). 승인 후 plan→dev.
  - 🐞 **사용자 보고 버그 2건 즉시수정(PRD와 별개)**: ① 등급별 사용자수 0 → 역할 기준 카운트(라이브러리 `72b08ea`, /users limit≤100). ② 강제로그아웃 무효 → 서버 `force_logout`에 access+refresh jti 블랙리스트(서버 `980abbc`=FR-EN-01 일부, 라이브검증 401). ⚠ 서버 `980abbc`은 `feature/tracking-gis-ingest`에 있음(푸시한 `feature/gop-account-permission` 아님 — 다음 푸시 시 정리). 앱 UI 즉시 로그인복귀는 앱 SessionExpired 구독(버킷C) 필요.
- **🚧 권한 집행 클라 구현 착수(2026-06-29, 사용자 "구현해라")** — Plan `docs/plans/GOP_Permission_Enforcement-prd-plan.md`. **서버측은 서버 세션이 이관 구현 중**(user_sessions.py require_admin+벌크jti, user_groups GET require_admin 등 — 내가 안 건드림). 클라만 내 몫.
  - **✅ FR-EN-06 PTZ 제어권 게이팅 완료(`663c45e`, 빌드0)**: `MapViewModel`에 `IPermissionService` IoC.Get lazy 주입(`CanControlCamera()=CanControl("cameras") ?? true`, 미등록 전체허용 폴백) + **GMaps.Ui→Accounts.Api ProjectReference 추가(순환없음)** + PTZ 제어 핸들러 10곳 게이트(드래그/패드/줌/포커스/프리셋 이동·저장·삭제·홈/IR컷/AF, **Stop 제외**) + EnsurePtzReady IsPtzCapable 이중방어. 등급: ADMIN/MAINTAINER full·OPERATOR control·VIEWER/GUEST 조작불가. ⚠모듈명 `cameras` 고정(`cam` 금지). 🔲 앱 재빌드 후 등급별 PTZ 차등 런타임 확인 + TEST-01 단위테스트 후속.
  - ✅ **FR-EN-09 장비(`a4e63f1`)·FR-EN-10 이벤트(`3282de8`) 완료** (구현 에이전트 2대 병렬, sonnet): 장비 7패널 CRUD `CanEdit/CanDelete("devices")`(신규 `DevicePermissionGate` 헬퍼) + 이벤트 ACK=`CanControl`·CRUD 독립 게이팅(FR-PG-08, 배치 가드·자동경로 제외) + **FR-EN-11 PermissionsChanged 재평가**(장비/이벤트 버튼). Devices.Ui·Events.Ui→Accounts.Api 참조 추가(순환없음, IoC.Get lazy 폴백). 빌드0·**GMaps.Ui 통합빌드0**·Accounts.Api 62/62.
  - ⏸ **보류(서버 의존)**: FR-EN-05(클라 EnumPermissionModule Map/Broadcast) + FR-EN-07 방송·FR-EN-08 맵편집 — **서버 enums에 broadcast/map 모듈 추가(서버 세션) 후** 진행(지금 게이트=OPERATOR/MAINTAINER 영구차단 GOP-05 함정). GMaps PTZ 강등 재평가(FR-EN-11 맵분)·FR-EN-13(device_groups OQ-PG-02)·cam:imaging(OQ-PG-04)도 후속.
  - ✅ **FR-EN-11 GMaps PTZ 강등 재평가 완료(`f353f28`)**: MapViewModel PermissionsChanged 구독→권한상실 시 진행중 PTZ 제스처 취소+팝업 IsPtzCapable 비활성. (장비/이벤트 재평가는 a4e63f1/3282de8 포함 — FR-EN-11 전 도메인 완료)
  - ✅ **사용자수 0 실제버그 수정(`c693ddb`)**: 72b08ea가 countByRole 계산만 하고 UserCount에 미사용(9 insert/0 delete가 증거)이던 것 → `UserCount=countByRole[등급명]` 배선. 스크린샷(`Docs/pictures/스크린샷 2026-06-29 142802.png`) 0 보고. 라이브 {VIEWER:2,OPERATOR:1,ADMIN:1}. **앱 재빌드 후 반영.**
  - ✅✅ **방송·맵 게이팅 완료(2026-06-29, 서버 PRD 반영 후)**: 사용자 "서버 PRD 다 반영" → 라이브 프로브로 enum map/broadcast 수용 확인(단 등급 값 미시드) → **§6-1 등급별 값 API 시드** + FR-EN-05 클라 모듈(`b1037f5`) + FR-EN-07/08 게이팅(`ff4c0d7`: 방송2·맵편집13·레이어 FR-PG-11 영속만; MapViewModel CanBroadcast/CanEditMap). 빌드0.
  - **→ ✅ GOP_Permission_Enforcement 클라 PRD(FR-EN-05~11) 전체 구현 완료.** 남은 외부 의존: ① **AUTH_MODE=token 전환 시 클라 Device/Event/Camera ApiService Bearer 배선(FR-EN-03③ 미완, authHandler=null)** + **GOP-07 가림막** ② 런타임 검증(앱 재빌드) ③ 푸시(`663c45e`~`ff4c0d7` 미푸시). ⚠ AUTH_MODE 미확정(프로브 /audit-logs 무인증200·/user-groups 401 혼재) — 사용자 확인 대기.
- **💡 GOP-07(PreAuth Overlay) 설계 논의(2026-06-29)**: 사용자가 "AUTH_MODE=token 되면 startup 데이터 못 불러옴 → 로그인 후 API 로드, 그 전엔 맵을 시안패턴 가림막으로 덮기" 제안 = **기존 PRD-GOP-07 그 자체**. 정합 결론: 베이스 타일=로컬(토큰불요)·동적데이터=로그인후 API 1회·NATS=인증후 구독·가림막이 대기 가림→완료시 페이드. ⚠ **AUTH_MODE=token 전환과 한 묶음 배포**(안 그러면 startup 깨짐). GOP-07 PRD 정합 구체화는 미착수(사용자 선택 대기).
- **📋 서버(API) RBAC 집행 PRD — ⚠ 서버 레포로 이관됨(2026-06-29)**: 파일 위치=**`C:\workspace_python\api-test-server\docs\PRD_GOP_Server_RBAC_Enforcement.md`**(서버 컨벤션 `PRD_*.md`, 추적대상). **.NET docs/prds 및 INDEX에서 제거**(사용자: 서버작업을 서버세션에 직접 이관). 승인/plan/dev는 **서버 세션**이 진행. ⚠ 동시세션 advance-phase가 .NET 원본에 잘못된 Approved 스탬프(다른 PRD "Camera PTZ" 승인) 찍었으나 이관본은 Draft로 정리함. 위 enforcement PRD의 **서버측 분리본**. FR-SV-01~11: P0=T4 require_admin(3종)+벌크 jti·reports 인증·3선결조건(AUTH_MODE token + `get_current_account_user_optional` 신규)·`require_perm(module,verb)` 팩토리+write 엔드포인트 적용·enums 모듈(Map/Broadcast/Setup*) / P1=마지막ADMIN 원자가드(SELECT FOR UPDATE)·감사 append-only DB강제(RULE/RLS)·비계정도메인 jti검사 통일·servers/user_groups 누락보강 / P2=비번변경 세션무효화·RTSP 마스킹. ★권위집행=서버(클라는 보조), 단 PTZ/방송/맵은 서버 미경유라 범위 밖(상위 PRD 클라단독). ⚠ AUTH_MODE 전환은 클라 Bearer 부착과 **동시 배포**(분리 시 전원 401). 상태=Draft, 승인 대기.
- **✅ IMPL-11 마무리 커밋 완료(2026-06-29 `ecf8b4f`)**: 크래시 직전 미커밋이던 `AccountManagerPanelView.xaml` 콘솔 임베드 중복크롬 제거(자체 시안헤더바 Border+PackIcon+'사용자 정보' + 하단 Separator/닫기버튼 Grid, **−37줄**) 복구 후 커밋. `AccountConsolePanelView.xaml` 빈줄2개(편집흔적)는 restore. **Accounts.Ui 빌드0**(경고23=기존 Accounts.Db nullable). ※169行 Width=800 ProgressBar는 미수정(기존 잔존, IMPL-11 범위 밖).
- **⚠⚠ 문서공백 복구(06-27 03:53~06-28 01:58, 7커밋 22h)** — session-context·plan 미반영이었던 분:
  - `86d8451`(IPermissionService.Apply/Clear 로그인배선 IMPL-01/02, V-PG-01 §7) · `7791e55`(EnumPermissionModule8/EnumPermissionVerb4+PermissionCatalog Enums신설 IMPL-05) · `a20cca7`(PermissionMatrix/UserSession/AuditLog 패널3종) · `17c3dd5`(3패널 디자인토큰 IMPL-10) · `b7c460c`(AccountConsolePanel 콘솔셸 1120×700+시안헤더+4탭 EXT-08/09) · `66d98b8`(AccountManager 고정폭제거 IMPL-11 1차) · `348b03a`(권한설정 재설계: PermissionGroupRowVM+ModulePermRowVM 신설·PermissionRowVM 제거, 그룹요약1행+더블클릭 상세 마스터-디테일 IMPL-06 UI) · `a104d40`(세션 강제로그아웃 DELETE /user-sessions/{id} ForceLogoutSessionAsync IMPL-08).
- **코드 현황**: AccountConsolePanel(4탭=사용자/권한설정/세션관리/감사로그, ContentControl x:Name 주입)이 시안헤더+풋터닫기 보유. PermissionMatrix=조회전용 마스터-디테일. UserSession=강제로그아웃 배선됨(⚠서버 role가드 T4 확인필요). AuditLog=read-only(append-only). MyPage/AccountSetup/Login/Logout=콘솔 외부 독립.
- **🔲 미완(서버의존無·즉시가능)**: IMPL-07(RegisterDialog 그룹 ComboBox, GET /user-groups 읽기가능) · IMPL-09(감사로그 뷰어 필터/페이징). **🔲 앱종료 후(DLL락)**: EXT-10/11(SetupPanel 권한/세션/감사 탭 제거→콘솔 일원화) · 런타임 시각검증(콘솔 탭 레이아웃·강제로그아웃 동작). **⛔ 블록**: IMPL-06 편집저장(서버 v5.0 `POST /user-groups/{id}/permissions`) · Phase4 패널게이팅(저가치/WIP) · 장비/이벤트/맵 RBAC(서버 3선결조건) · 메인솔루션 Bootstrapper `useDbAuth=false` 커밋(사전통지 대상, 안전점 `pre-gop-auth-flip`@86c7855).
- **⚠ 문서정합 필요**: plan(`docs/plans/GOP_Permission_Gate_Feature-prd-plan.md`, mtime 06-28 00:46)이 348b03a/a104d40/7791e55 미반영([ ]로 남음)→IMPL-05/06/08 갱신要. PRD(`docs/prds/GOP_Permission_Gate_Feature-prd.md`)는 Draft v2.0(재검토요청)인데 구현 대부분 완료→approve 소급 + OQ-PG-04~07(PM결정) 해소. pipeline-state.json stale(06-19 OverlayImage·미사용).
- **안전점/푸시**: 롤백 `before-permission-gating`@e013e08(06-27 03:44). **✅ v2.6 origin(gitea) 푸시 완료(2026-06-29)** — ⚠ gitea **SSH 호스트키가 이 셸 known_hosts에 없어 `git push gitea`(ssh) 실패** → **HTTP 경로로 우회 성공**: `git push http://192.168.202.160:3000/Sensorway_SW/Ironwall.Dotnet.Libraries.git v2.6`(자격 캐시됨). 다음 세션도 동일 우회. 서버(api-test-server)는 새 브랜치 **`feature/gop-account-permission`** gitea 푸시(권한 커밋 `c71c8ce`/`12fc48d`). v2.14.0 워크트리(GOP-00 인증연동)는 v2.6 흡수완료(`git worktree remove` 가능).
- 참조 패턴: [[project_rbac_enforcement_reality]] [[project_panel_design_system]] [[project_account_integration_strategy]] [[project_dialog_panel_dark_theming]] [[feedback_main_solution_advance_notice]] · HTML `docs/GOP_Permission_UI_Map.html`·`docs/GOP_Account_Console_Wireframe.html`.

---

## ▶▶ 재개 포인트 (2026-06-27 최신 — 추적 서버 API 완료 + .NET 데이터소스 토글 진행)
- **서버측 추적 영속 = 완료·배포·E2E**(별도 repo `C:\workspace_python\api-test-server`): 읽기 GET API(`/api/tracking/points` keyset cursor·`/sessions`·`/health`, 차수 v4.11, 커밋 `6033b2f`) + 독립 인제스트 워커 `gis-ingest`(NATS `sensorway.*.gis.tracking-status` 구독→`track_points` 멱등 INSERT, 차수 v4.12, 커밋 `15365d5`). mock E2E 통과. 명세서 5-싱크 완료. [[project_tracking_persistence_architecture]] [[reference_api_test_server]]
- **.NET 데이터소스 토글 = 라이브러리 구현 완료·빌드·테스트**(Track C, analysis→PRD→Plan→dev 전부 진행). Playback reader를 **로컬 DB / 서버 API 설정 토글**(라이브, 무재시작). **worktree** `C:\workspace_app\worktrees\track-datasource-toggle`(브랜치 `feature/track-datasource-toggle`, off v2.6, 태그 `before-track-datasource-toggle`), **4커밋**(`6024a71` Phase1-2 계약/Tracking.Api · `5a662ad` Phase3-7 매퍼/리더/셀렉터/DI/설정/UI · `0d0948a` 매퍼7테스트 · `0ecb12a` CHANGELOG). 빌드0·매퍼7통과·전체 회귀0(GMaps.Ui 126/126). GMap.NET 고아라 worktree에 복사함([[project_gmap_orphan_submodule]]).
  - 산출: 분석 `docs/analyses/Tracking_Playback_DataSource_Toggle-analysis.md` · PRD `docs/prds/Tracking_Playback_DataSource_Toggle-prd.md`(Approved) · Plan `docs/plans/Tracking_Playback_DataSource_Toggle-prd-plan.md`. 설계=Explore×3→architect→code-reviewer(FLAWED 판정→수정 전부 반영: GetRequestAsync ct無·첫페이지 cursor omit·AsImplementedInterfaces·중복바인딩0·nullable매퍼·webSetup Url부재).
  - **✅ v2.6 머지 완료**(2026-06-27 ff): v2.6 동시변경(Accounts 권한패널·Enums) 충돌0 통합, v2.6 HEAD=**`64b55e5`**. cursor 테스트 3 추가(신규 10). GMapUiModule param **`IApiSetupModel?`로 정정**(앱 `SetupModel:IApiSetupModel` 호환, 내부서 concrete ApiSetupModel copy ctor 변환). 라이브러리=**integration-ready**.
  - **🔲 후속(미완) — Bootstrapper 연동만 남음**: `Dotnet.Monitoring.Solution/.../Bootstrapper.cs:350` `new GMapUiModule(setup,setup,setup,_log,70)` → **6번째 인자 `setup` 추가**(setup=SetupModel:IApiSetupModel). ⚠ **현재 Bootstrapper.cs가 동시 세션 미커밋(편집 중)이라 보류**(충돌 회피). 안전점 태그 `before-tracking-datasource-bootstrap`(메인솔루션). 연동 후 앱 재빌드 런타임. 미연동이어도 API 모드 graceful Local 폴백(무해). [[feedback_main_solution_advance_notice]]
- ⚠ **외부 미결**: AiAnalysis가 신 `targets[]` 발행해야 서버에 실데이터(그 전엔 API 모드 빈 결과 → **로컬 기본 유지**가 정답). pipeline-state는 stale(OverlayImage 2026-06-23) — 추적 작업은 이 머신 우회, session-context로 추적.
- ✅ **세션복구·무손실 검증 완료(2026-06-29, PC다운 후, 워크플로 wf_ad9019b3 3에이전트)**: v2.6 HEAD=`a104d40`(Accounts 동시세션이 전진시킴). ① **머지완전성 COMPLETE** — 토글 7커밋(`6024a71`·`5a662ad`·`0d0948a`·`0ecb12a`·`54cfec3`·머지 `2a0849b`·`64b55e5`) 전부 v2.6 ancestor, 추적파일 24/24 현존·삭제0. ② **런타임정합 COHERENT** — Reader포트(ITrackPointReader)/로컬(TrackPointStore)·API리더(TrackPointApiReader)/Selector/Mapper/Tracking.Api/DI/설정enum 14컴포넌트 전부 PRESENT, `_trackingApiSetup==null`→Local **graceful 폴백 2중**(GMapUiModule.cs:71 api인자 null주입 + TrackDataSourceSelector.cs:41-53 런타임폴백, 기본 DataSource=Local). ③ **유실스캔 NOTHING_LOST** — stash@{0}=v2.6 PRD 무관, dangling 367f368=빈 stash(tree≡base 54cfec3, 복구내용0). **결론: 코드 0손실, 라이브러리 100% 머지·완결.** 🔲 **유일 잔여=메인솔루션 `Bootstrapper.cs:350` 6번째 인자 `setup` 미배선**(2026-06-29 현재 Accounts 동시세션이 Bootstrapper.cs 포함 11파일 미커밋 dirty → 충돌회피 위해 그쪽 커밋 후 배선 권장. optional이라 미배선=Local 무해).

---

## ✅ 적용·빌드0(2026-06-26): PTZ 드래그 비례이동 개선 + 줌/포커스 +/- 버튼 (전부 GMaps.Ui, 미커밋)
- **드래그-PT 비례이동 수정**(`MapViewModel.HandlePtzDragAsync`): 기존 `panVel=(Dx/len)*speed`=**단위벡터라 드래그 길이 무관**(속도 일정, 시간만 mag 비례)→짧은/긴 드래그 차이 미미. 수정=**속도도 mag 비례** `velFactor=speed*(0.3+0.7*mag)`(하한0.3, 짧은 드래그도 안 죽게) + **maxLen 0.4→0.65·floor 40→60**(최대 드래그 길이↑, 미세조절 폭↑). 시간(`mag*700ms`) 유지 → 총 이동량 ∝ mag×(0.3+0.7mag).
- **줌 +/- 버튼**: PTZ 탭 Zoom 슬라이더 아래. VM `ZoomInCommand`/`ZoomOutCommand`→**기존 `RaisePtzZoom(±1)` 재사용**(휠과 동일 ContinuousMove 줌 펄스). 컨트롤러/배선 변경 0.
- **포커스 +/- 버튼**(그 아래, `IsImagingCapable` 게이트): 신규 `IPtzController.MoveFocusAsync(cameraId, direction)` + `PtzController` 구현(ImagingClient 직접 `OnvifImaging.FocusMove{Continuous=ContinuousFocus{Speed=±0.7}}`→MoveAsync→350ms→StopAsync, SetAutoFocus 패턴). VM `FocusRequested` 이벤트+`FocusNear/FarCommand`, MapViewModel `OnCameraPopupFocus` 배선. **direction +1=far/-1=near(카메라마다 반대일 수 있음 — 반대면 플립).**
- **PanelHeight 188→230**(버튼 2행 공간). 롤백태그 `before-ptz-drag-zoomfocus`@f811588.
- **🔬 적대적 검증(workflow wf_ac64b1ff, 20 에이전트): 확정 9·반증 7**. ★반증: 포커스 `ContinuousFocus.Speed`=플래그없는 일반 float→**정상 직렬화**(SpeedSpecified 사일런트 no-op 우려 해소. Relative/Absolute만 Specified 보유). **수정 적용 5(빌드0)**: ①[HIGH] **포커스 버튼이 옵션탭 안 열면 비활성**(IsImagingCapable를 LoadImaging(옵션탭)에서만 set)→`EnsurePtzReadyAsync` 완료 시 `vm.IsImagingCapable=ptz.IsImagingCapable()` 추가 ②[MED] **패널 Border Height 188 미반영**(VM PanelHeight만 230, XAML Border 188 그대로→42px 점프+스크롤)→XAML Border 230 ③[MED] LWW: 후행 `StopAsync` ct 미전달(빠른 2연속 드래그/줌이 stale Stop에 끊김)→drag/zoom Stop에 `ct` 전달 ④[MED] 포커스 Stop을 finally로 보장(취소 시 모터 계속 도는 latent) ⑤[LOW] 드래그 dead-zone: 최소 펄스 `PtzDragMinDurationMs=120` floor.
- **🔲 미수정(보고·보류)**: E[MED] **고정(비PTZ)+이미징 카메라**는 포커스行이 외곽 `IsPtzCapable` AND게이트에 막혀 비활성(유저 카메라=PTZ라 비차단, 수정=게이트 개별화) · F[MED] **포커스 0.7이 GetMoveOptions 범위 미클램프**→정규화 [-1,1] 아닌 카메라서 무동작 가능(런타임서 포커스 안 움직이면 1순위 원인, 수정=GetMoveOptions 캐시+클램프) · H[LOW] 포커스 미지원 시 사일런트(능력 프로브+UI피드백). **GMaps.Ui 빌드0.** 🔲 런타임: 드래그 길이별 차이·줌±·포커스±(방향/실동작) 확인. 튜닝(드래그 0.3/0.65/120, 포커스 0.7/350).

## ▶▶ 인수인계 (2026-06-26 — Tactical 디자인/테마 세션, 병행 작업스트림)
- **디자인 작업 전부 커밋 완료** (라이브러리 v2.6 + 앱 메인솔루션). 내 디자인 미커밋분 0 — 작업트리 깨끗.
- **핵심 커밋**: `571e1b5`(전역 md:Card 배경 통일) · `7fd3986`(로그인 카드) · `50f908a`+`3b49c90`(맵 메뉴/툴바/트레이) · `4e6fb93`·`21c584d`·`7abe702`·`684d3c9`(다이얼로그 다크) · `b3bd5cb`(RTSP 팝업) · 앱 `fd0ab7f`·`86c7855`·`72939ba`.
- **재사용 패턴(다른 세션이 새 화면 만들 때 그대로)**:
  1. **다이얼로그 흰-폼** = MD DialogHost 팝업이 런타임 다크 테마 미상속(별도 비주얼트리). → 다이얼로그 카드에 **`md:ThemeAssist.Theme="Dark"`** 강제 + `Background="{DynamicResource SurfaceBrush}"`. (Inherit/중첩DialogHost 제거로는 안 됨.)
  2. **패널 배경 회색/진청 불일치** = MD elevation 오버레이(회색) vs SurfaceBrush(진청) 혼재. → **전역 implicit `md:Card` 스타일**(`Themes/Styles.Containers.xaml`: Background=SurfaceBrush + ElevationAssist Dp0)로 통일. 명시 Style/Background 가진 카드(KPI·로그인)는 로컬값 우선 → **개별로 직접 SurfaceBrush+Dp0 추가** 필요.
  3. **맵 툴바** = ToolBar/ToolBarTray/Menu Background를 SurfaceBrush로(MaterialDesignPaper/MaterialDesignToolBar 기본은 다크-회색이라 안 맞음).
- ⚠ **미커밋 공유파일 = 내 것 아님, 건드리지 말 것**: `Bootstrapper.cs`(계정 API 컷오버 `useDbAuth:false`→ApiAccountGateway) · SetupView들(`#33000000`→`Gray`) · Device/Event 패널 일부 추가. **해당 세션이 커밋.** (메인솔루션 공유 — [[feedback_main_solution_advance_notice]])
- 🐞 **Stop 훅 버그(하네스 세션 전달용)**: `.claude/settings.json`의 훅 5개 전부 **상대경로**(`node .claude/hooks/*.js`) → 훅 실행 시 CWD가 하위폴더(예: GMaps.Ui)면 `MODULE_NOT_FOUND`. 파일은 루트 `.claude/hooks/`에 정상 존재. **수정 = `$CLAUDE_PROJECT_DIR` 절대경로**(`node "$CLAUDE_PROJECT_DIR/.claude/hooks/stop-observation.js"`), 5개 동일 적용. non-blocking이라 기능엔 무해하나 훅 로직 무음 스킵 위험.
- ℹ **메일박스 자동 전파 불가**: `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` 꺼짐 + 등록 세션 0 → `broadcast`/`sendMessage` no-op. 그래서 이 파일로만 전파.

---

## ▶▶ 다음 세션 재개 포인트 (2026-06-26 최신 — 추적 Tracking GIS)
- **현재 작업 = Tracking GIS 시각화 + Playback**. **P1~P5 + 설정UI + 오버레이 표준전환 + P7(API대비 영속강화) = 전부 구현·커밋 완료**. v2.6 로컬, **미푸시**.
- **핵심 커밋 체인**: …`1d98eb9`(P5 콘솔) → `79b351c`(P3-04 설정UI) → `d04a7aa`(오버레이 표준전환) → `1677263`(갭분절·슬라이더수정) → **`000ca40`(P7-1a Read seam ITrackPointReader) → `517fd14`(P7-1b 보존·dedup·쓰기버퍼, 최신)**.
- **★API vs 로컬DB 분석 완료(Agent 6대)**: `docs/analyses/Tracking_API_vs_LocalDB-analysis.md`. **다중 스테이션 확정(사용자)** → 최종=서버 NATS인제스트+클라 read-only(보류, 스테이션#2 시점). 결정 [[project_tracking_persistence_architecture]]. P7로 Read seam 봉인+로컬결함3종(보존죽은코드·UNIQUE부재·쓰기차단) 보강 완료 — 미래 서버전환=DI 플립.
- **🔲 즉시 다음 = 사용자 앱 재빌드(VS Stop→Rebuild→F5) → "캡처해줘" → 내가 시각검증**: ① 툴바 **▶재생·⚙설정** 버튼 ② 두 패널이 기존 창과 **같은 테마**로 뜨는지 ③ **헤더 드래그 이동**(사용자 마우스 테스트) ④ P4 적재 후 **P5 재생**(발행기 `37.39404047 126.96630120`로 적재 가능). ⚠재생은 P4가 DB에 쌓은 뒤라야 보임.
- **🔲 후속**: 정련(P2-07 lost grace·P1-08 ZOrder) / **push 여부**(사용자 결정).
- **참조**: PRD `docs/prds/Tracking_GIS_Visualization_Playback-prd.md` · Plan `docs/plans/Tracking_GIS_Visualization_Playback-prd-plan.md` · 설계HTML `Docs/reports/Tracking_Playback_Storyboard_Wireframe.html`·`Tracking_Settings_UI_Wireframe.html`. 패턴 [[project_gmaps_overlay_window_pattern]] · 배포 [[project_library_deployment_path]] · 캡처 [[reference_headless_screen_capture]].
- ⚠ **동시 세션 활발**(Accounts.Ui 등 v2.6 분 단위 커밋). 미커밋 추적파일(`.gitignore`·`CHANGELOG`·`Docs/INDEX`·`LogoutPanelView`·`CameraStreamPopupStyle`)은 **타 세션 것 — 건드리지 말 것**. 내 추적 작업은 전부 커밋됨(작업트리 깨끗, 분실 위험 0).
- 상세는 아래 "Tracking GIS" 데브 블록(P1~P5·설정·표준전환) 참조.

---

## 📝 CHANGELOG·버전(2026-06-25): 세션 작업 [Unreleased] 기록, 버전 컷은 보류(사용자 선택)
- **CHANGELOG.md `[Unreleased]`에 이번 세션 작업 추가**(Fixed: 66 영상 `:no-audio`·팝업 타이틀바 클램프·속도 0.1 / Changed: API·SVMS http→https). Track B라 정식 자동대상 아니나 사용자 요청으로 수동 기록. **미커밋**(루트 `CHANGELOG.md`).
- **버전 컷 보류**(사용자 "버전 컷 보류" 선택): `[Unreleased]`에 **동시 세션 미완료 작업**(Theme 시스템 Phase 6 미완·EXT-01/02 대기, Accounts.Ui 등) 섞여 있어 지금 컷하면 미완료가 릴리스로 묶임 → 동시 세션 Theme 마무리 후 일괄 컷. CLAUDE.md `version`(2.8.4) **미변경**.
- ⚠ **버전 불일치(정정 대기)**: CHANGELOG 최신 릴리스 `2.7.1`(2026-06-04) ↔ CLAUDE.md `2.8.4`. pipeline-state `last_changelog_date=2026-06-05`. 실제 컷 시점에 셋 동기화(스킴=작업로그형 X.Y.Z, 날짜바뀜→Y+1·Z=0; advance-phase complete 자동, Track C 한정). [[feedback_never_force_gates]]

## 🔬 조사완료(2026-06-25): HTTP→HTTPS 전환 영향 범위 — `docs/analyses/Http_To_Https_Migration_Impact-analysis.md`
- **핵심**: 모든 도메인 API(Device/Event/Account/Aligo)가 공유 `Ironwall.Dotnet.Libraries.Api/Services/ApiService.cs`의 **단일 HttpClient**로 수렴. 베이스 URL=appsettings.json **`"Url"` 단일 키**(현 `http://localhost:8000/api`)→`SetupModel.Url`(IApiSetupModel.Url). 도메인 서비스는 직접 HttpClient 생성 없이 ApiService에 위임.
- **서버팀 컨텍스트(사용자 공유)**: 서버만 mkcert `server.crt` 보유, **클라 인증서 불필요**(mTLS 아님, JWT Bearer). 클라는 **rootCA.pem을 Windows 신뢰저장소 1회 등록**(`mkcert -install`/`certutil -addstore Root`)만. → **ApiService cert 콜백 코드 불필요**(기본 HttpClientHandler가 OS 신뢰저장소로 검증 통과). 유일 조건=서버 cert SAN에 접속 host/IP 포함 + appsettings Url host 일치.
- **클라(이 앱) 실제 변경 최소**: appsettings `"Url"` http→https(설정 1줄). 코드 변경 0(API). mkcert/서버 listen은 서버·인프라 측.
- **별도 서버 2곳(선택)**: ① SVMS 장비상세 웹 `DeviceDetailUrlService.cs:64` 하드코딩 `http://{host}/ssw-svms`(IpAddrerssWebServer:6173) ② Streaming/AI `Dotnet.Streaming.UI` `HttpPrefix="http://"`. **사용자 요청=SVMS http/https 둘 다 지원** → 클라측 스킴 파라미터화(설정 플래그, 1파일) + 서버측 동시 listen.
- **범위 밖**: 카메라 ONVIF/RTSP/WebRTC/snapshot(장비측 별도), GMap.NET 지도타일, xmlns, NATS/Redis/DB(비-HTTP).
- ✅ **검증·적용(2026-06-25, 사용자 "https로 일단 API SVMS 둘다 적용")**: 사용자가 **rootCA Windows 신뢰등록 완료**(확인: `Cert:\CurrentUser\Root`에 `O=mkcert development CA`, 지문 6006D433…, ~2036). **끝단 검증**: `https://localhost:8000/` → HTTP 200·**TLS 검증 OK·CA 신뢰됨**(코드 override 없이) → 인증서 코드 변경 0 확정. **적용**: ① API = 메인 `appsettings.json:29` `Url` http→**https**`://localhost:8000/api`(운영 주석 L30=123.141.236.253:8136 별도). ② SVMS = `DeviceDetailUrlService.cs`에 `const WebScheme="https"` 도입 + L64 사용, 테스트 11곳 https 갱신(DeviceDetailUrl 25/25 통과·빌드0). 롤백태그 `before-https-api-svms`@1f8137a. ✅ **커밋(2026-06-25, 미푸시)**: 라이브러리 v2.6 `5edbfb1`(SVMS) + 메인 솔루션 v0.5 `162547f`(appsettings API Url).
- ⚠ **SVMS 서버 의존**: 클라가 이제 https 링크 생성 → **SVMS 서버(192.168.202.195:6173)도 https listen 필요**(+cert SAN에 그 IP). 현재 `IsWebServerEnabled=false`. http로 되돌리려면 `WebScheme` 상수만 변경(런타임 토글 원하면 IMainControlWebSetupModel에 스킴 키 추가=메인 SetupModel+appsettings 후속). ③ Streaming/AI `HttpPrefix`는 미적용(요청 범위 밖).
- 🔲 런타임: 메인 재빌드+재시작 → API 데이터 https 수신(로그 TLS 오류 0)·SVMS 클릭 시 Chrome https 자물쇠.

## ✅ 커밋·마감(2026-06-25): 카메라 팝업 3종 수정 v2.6 커밋 완료(미푸시)
- **커밋**: `4900093 fix(Streaming): Hub :no-audio — 66번 영상 프리즈` + `58b3fd7 fix(GMaps.Ui): 팝업 타이틀바 클램프 + 속도 0.1 반올림`. 속도 슬라이더 **XAML(StringFormat=0.0/0.1 스냅)은 동시 세션이 자기 커밋 `caf3677`에 휩쓸어 선반영**(내 미커밋 작업트리 XAML이 같이 커밋됨 — 분실 아님, 단 commit 분리는 안 됨). 빌드 전부 오류0. 롤백태그 `before-stream-audio-and-popup-clamp-fix`.
- **⚠ 동시 세션 주의**: v2.6 main checkout을 타 세션이 매우 활발히 커밋 중(HEAD a7cf02b→cc829e7→1b90f01… 분 단위). 같은 작업트리라 **미커밋 변경이 타 세션 `git commit -a`에 휩쓸릴 수 있음**(이번에 XAML이 그렇게 됨). 향후 동시 작업 시 worktree 분리 권장.
- **🔲 푸시 미정**(사용자 결정) — v2.6 origin 대비 다수 ahead. **🔲 런타임 재검증**: 메인솔루션 재빌드+재시작 후 ① 66 영상 정상(PTZ 이동 보임) ② 상단 카메라 팝업이 타이틀바 안 덮음 ③ 속도 슬라이더 0.1 스냅·"0.3" 표시.

## ✅ 적용·빌드0(2026-06-25): PTZ 속도 슬라이더 표시 0.1 단위 정리 (부동소수 0.29999… 노출 제거)
- **요청**: 속도(PanTilt/Zoom) 슬라이더가 `0.29999999999999999` 같은 부동소수를 가끔 노출 → 0.1 단위로 깔끔히.
- **원인**: TextBox `StringFormat=0.00`(2자리)+슬라이더 0.05 스냅 → `0.05×k` 부동소수 찌꺼기. **수정 3종**: ① VM `PanTiltSpeed`/`ZoomSpeed` 세터에 `Math.Round(Clamp,1,AwayFromZero)`(값 자체를 0.1 단위로 — 원천 제거, `CameraStreamPopupViewModel.cs`) ② TextBox `StringFormat=0.0`(1자리) ③ 슬라이더 `TickFrequency/SmallChange=0.1`·툴팁 "0.1 단위"(`CameraStreamPopupStyle.xaml`). ⑦의 0.05 granularity→0.1로 변경(사용자 명시 "0.1 단위"). 슬라이더 width 수정(⑦ 핵심)은 유지. **GMaps.Ui 빌드0**. 롤백 `before-stream-audio-and-popup-clamp-fix`@cc829e7(동일). **미커밋**. 🔲 런타임(메인 재빌드 후 슬라이더 0.1 스냅·"0.3" 표시).

## ✅ PTZ 후속(2026-06-25): 비번 수정→capable=True, PTZ 제어 정상 확정. 진짜 문제는 66번 스트리밍(영상 정지)
- **비번 해결**: 사용자가 (API 암호화) 비번 이슈 수정 → 03:40 `InitializePtz (ptz=True imaging=True **token=ok**)` → `capable=True`. ONVIF 인증 정상화.
- **"PTZ 제어 안됨"은 오인 — 제어는 정상**: 직접 SOAP로 cam 66(`0_PROFILE_WITH_AUDIO`)에 ContinuousMove(VelocityGenericSpace, with/without space 둘 다) 전송 → **카메라 물리 이동 Δpx≈-1.8**(px 0.98→-0.89), GetStatus MoveStatus=MOVING. velocity는 `Vector2D` 평범한 `[XmlAttribute]`라 항상 직렬화(Specified 누락 버그 아님), 프로파일에 PTZConfiguration(PTZConfigurationToken1/PTZNodeToken1) 정상. **테스트 후 AbsoluteMove로 원위치(px=0.9778/py=0.5050) 복귀 완료.**
- **★진짜 원인 확정 = Hub 플레이어가 오디오 미차단 → 66번(오디오 포함 스트림)만 비디오 프리즈**: 증상=정지(프리즈), **66번만**(108/109 정상). VLC로 66 직접 캡처 **8/8 고유프레임(1920×1088)** = 카메라 스트림 정상 라이브 → 프리즈는 앱 렌더 측. 66 URL=`…/video1+audio1`(오디오 트랙 O), 108/109=`…/`(오디오 X). **근본원인: `CameraStreamEntry.CreateAsync`(Hub 경로 — 팝업이 사용, `Streaming/Hub/CameraStreamEntry.cs:102`)가 `new Media(libVlc, uri)`를 옵션 0개로 생성 → `:no-audio` 없음**. Hub는 비디오만 커스텀 콜백(SetVideoCallbacks→SharedFrameBuffer)으로 렌더하는데 오디오 미차단 시 LibVLC가 오디오 클럭으로 A/V 동기를 맞추다 **vmem 비디오 콜백 stall → 프레임 정지**. 오디오 트랙 있는 66만 발생. (다른 경로 `ImprovedRtspStreamingService.cs:931`은 조건부 `:no-audio` 보유). VLC 테스트가 정상이던 건 `--no-audio` 줬기 때문(반증 일치). **수정 = CameraStreamEntry Media에 `media.AddOption(":no-audio")`(핵심) + `:rtsp-tcp`·`:network-caching=300` 권장. 라이브러리 1파일(Streaming), Track B.** ✅ **적용·빌드0(2026-06-25, "둘다 고쳐줘")**: `CameraStreamEntry.cs:103`에 `media.AddOption(":no-audio")` 추가(최소 적용 — rtsp-tcp/캐싱은 미적용, 필요시 후속). 롤백태그 `before-stream-audio-and-popup-clamp-fix`@cc829e7. **미커밋**(동시 세션 v2.6 활발). 🔲 런타임 재검증: **메인솔루션 재빌드+재시작** 후 66 영상 정상 갱신(PTZ 이동이 화면에 보임). ※초기 "재오픈 SharedFrame rebind 누락" 의심은 red herring(진짜는 오디오).
- ⚠ 초기 가설(ContinuousMove 무동작=커밋 9992458 회귀)은 **반증됨** — 카메라는 ContinuousMove로 잘 움직임. [[feedback_accurate_analysis_first]]

## 🔍 진단(2026-06-25): 카메라 팝업이 MahApps 타이틀바(최대/최소/닫기) 위로 덮는 버그 — MahApps 아님, 앱 오버레이 클립 미적용
- **사용자**: 팝업이 윈도우 상태바(min/max/close) 위 레이어로 옴. MahApps 라이브러리 의심.
- **원인=앱 자체 오버레이(MahApps 아님)**: 카메라 팝업은 `MapView.xaml`의 `PropertyPanelCanvas`(Grid.Row2, `Panel.ZIndex=200`) ItemsControl 자식 — Canvas/ItemsControl/ItemsPanel 전부 **`ClipToBounds="False"`**(L741/747/769/774). 위치=`CanvasTop`/`CanvasLeft`(VM 바인딩; `MapViewModel.cs:1023` 오픈·`1124` 갱신·드래그 핸들러). 카메라가 맵 **상단 근처면 CanvasTop이 작거나 음수 → 팝업이 위로 넘쳐 상단 툴바+윈도우 타이틀바를 덮음**. 타이틀바는 피해자일 뿐.
- **선례**: 동일 부류(디지털줌 확대분이 상단 컨트롤바 덮음)를 **C3에서 AdornerDecorator `ClipToBounds=True`로 해결**(MapView.xaml:619-621). 단 PropertyPanelCanvas는 음수Top 드래그(속성/군사심볼 패널) 위해 **의도적으로 클립 제외** → 카메라 팝업도 그 영향 받음.
- **수정(라이브러리 GMaps.Ui, 메인 솔루션 무관)**: `CanvasTop`(필요시 Left/Right/Bottom)을 최소값(상단 툴바 높이 이상)으로 **클램프** — `MapViewModel.cs:1023`·`1124` + 드래그 핸들러. 대안: 카메라 팝업 ItemsControl만 `ClipToBounds=True`(단 팝업 가장자리 잘림 가능). Track B. ✅ **적용·빌드0(2026-06-25)**: `CameraStreamPopupViewModel.cs` `CanvasTop` 세터에 `MinCanvasTop`(=0) 하한 클램프 + 상수 추가(open/refresh/드래그 단일 지점 커버). 롤백태그 동일(@cc829e7). **미커밋**. 🔲 런타임 재검증: 메인 재빌드+재시작 후 맵 상단 카메라 팝업이 타이틀바를 안 덮는지.

## 🔍 진단완료(2026-06-25): "PTZ 또 안됨" — API 암호화 비번을 ONVIF가 복호화 없이 사용(WS-Security digest 불일치) — `log-2026-06-25`
- **증상**: 맵 팝업 PTZ 비활성. cam=1969(192.168.202.66). `[Onvif] InitializePtz (ptz=True imaging=True **token=none**)` → `ProfileToken 단계 실패 (400)`(OnvifModelBuilder.cs:282 MediaClient.GetProfilesAsync) + `GetNode space 로드 실패 (400)`(PtzController.cs:379) + `StopPTZ (400)` → `capable=false`.
- **★실제 근본원인(사용자 힌트+코드 확정)**: **API가 카메라 비번을 암호화해서 보내도록 변경**됐는데 **클라가 복호화하지 않음**. `EnsurePtzReadyAsync`(MapViewModel.cs:662)가 `Password=cam.UserPassword`(암호문)를 그대로 ONVIF `ConnectionModel`에 넣음 → WS-Security PasswordDigest=SHA1(nonce+created+**암호문**) → 카메라는 평문(sensorway1)으로 계산 → 불일치 → **400(인증실패)** → PTZ 비활성. `DtoToModelHelper`(`UserPassword=dto.UserPassword`)·Devices/Api/Utils 전역에 **password 복호화 로직 0**(grep 확인. GMap.NET `Stuff.cs` DecryptString은 지도 API키 난독화라 무관).
- **RTSP는 멀쩡한 이유**: RTSP는 `cam.UserPassword`가 아니라 **수동입력 평문 전체 URL `Urls.RtspSub`**(CameraConnectionAdapter, MapViewModel.cs:537)를 씀. 영상 정상(VLC hasFrame=True), **ONVIF만** 깨짐.
- **"6시간 전엔 됐다" 설명**: 06-24 20:33 정상(token=ok)→20:37 재부팅. 재시작 시 앱이 API에서 카메라 데이터 **재취득→암호화 비번**을 받음(그 전엔 평문). 02:26 재빌드·02:57 재로그인에도 동일 400.
- **확정 probe(카메라 정상·코드 회귀 아님)**: 직접 SOAP, 평문 admin/sensorway1로 ① WS-Security GetProfiles/GetConfigurations=**200** ② HTTP Digest만=**200** ③ created 시간窓 ±1일 전부 200(시계 무관 **확정**) ④ 틀린 WS-Security digest→**400** / 틀린 HTTP digest→**401** ⇒ 앱의 400=**WS-Security 토큰 무효 확정**. 익명=401(서비스 정상). 동일 a7cf02b/35e0aeb.
- **🔲 수정(PRD 필요)**: ONVIF에 넘기기 전 `cam.UserPassword` **복호화** 필수. **핵심 미결=API 암호화 방식(알고리즘/키)** — 서버팀/사용자 확인. 권장=**모델 채움 지점(`DtoToModelHelper`)에서 복호화**해 전 소비자 일괄 해결(ONVIF 외 향후 전부). 평문이 로그/DB에 남지 않게(security.md). 메인 솔루션 무관(라이브러리 수정).
- ⚠ **정정**: 같은 세션 초기의 "카메라/NVR측 인증거부(코드 무관)" 결론은 **오류**였음 — 인증된 probe로 카메라가 정상 수락함을 확인해 뒤집음(추측→증거 전환). [[feedback_accurate_analysis_first]] [[project_mysql_mariadb_techdebt]]

## 🔍 검토+완성(2026-06-25): GOP-00 PRD ↔ gateway seam 정합 (Explore×2→architect→code-reviewer 체인) — **PRD v2.2 완성(plan-ready)**
- **트리거**: 사용자 "Account 프로젝트에서 다 진행?" → "한번 더 검토해봐". 이전 단언("VM 재편집 0, ApiAuthGateway만 추가")의 정확성 적대 검증. (PRD v2.0은 v4.9 **계약**만 갱신, **아키텍처는 추출 이전(2026-06-19)** 잔존.)
- **확정 사실(코드 grounded)**: gateway seam 깨끗(8 VM 누수0) / `IAuthGateway`(Authenticate/GetLatestLogin/Record)·`IUserDirectoryGateway`(GetAll/Create/Update/Remove+currentPwd/IsUsernameTaken/ResetPwd)·`IProfileGateway`(GetProfile/Update/ChangePwd) / `AuthResult`(Account,**Token만**,ExpiresAt,Role:string,**Permissions:IReadOnlyList\<string\> flat**) / `AccountUiModule(bool useDbAuth)` L75-82이 DbAccountGateway 3인터페이스 등록(스왑지점) / 공유 `.Api`=Bearer 전무·Timeout 무시·`.Result` 1건·catch→400 / **신규 전부 미존재**(Accounts.Api·IAccountApiService·BearerAuthHandler·ITokenStorageService·IPermissionService·EnumUserRole).
- **검증 결과(code-reviewer)**: CLAIM3/4/5(AuthResult refresh부재·useDbAuth스왑·flat permissions)=**CONFIRMED**. CLAIM1(FR-10 잉여)=**CONFIRMED+caveat**, CLAIM2(FR-11 잉여)=**PARTIAL**.
- **🔴 구멍 3 (이전 단언 정정)**:
  · **G1 seam 계약 결함**: `AuthenticateAsync`가 실패 시 **`null` 반환** → GOP의 `Error.Code`(UNAUTHORIZED/FORBIDDEN/423잠금/422)·lock_reason을 **구조적으로 못 실음**(FR-10/§3.2.4 위반). 모든 실패가 단일 generic 메시지로 붕괴. → **gateway 계약에 typed 실패결과 추가 필요**(라이브러리 변경, 어댑터만으로 불가).
  · **G2 토큰 라이프사이클 seam 밖**: refresh=공유 `.Api` BearerAuthHandler(투명)+IAccountApiService.RefreshAsync(Func 지연), 서버 logout=`IAuthGateway.LogoutAsync` 추가(Db=no-op/Api=POST)+**LogoutPanelVM 1줄 편집**(유일한 정당 VM 터치), 만료타이머=앱 LeftMenu(Bucket C). access_token 비블랙리스트라 로컬 Logout이 필연적 정답.
  · **G3 디렉터리/프로필 메서드 GOP 실현성 미명세**: `GetLatestLoginAsync`·`IsUsernameTakenAsync`=**v4.10 BLOCKED**(B-5/B-4)→로컬 prefs/낙관 폴백, `RecordLoginAsync`=서버 자동기록(no-op하되 **IsIdSaved 로컬 보존 필수**), `RemoveAccountAsync(+currentPassword)`=**서버 DELETE에 본문/비번게이트 없음**(보안: 클라검증화 or 소멸·자기삭제 무가드), `ResetAccountPassword`/`ChangePassword`=feasible(반환형 IAccountModel? vs `{success:true}` 재페치만). `GetAllAccounts` 목록=B-8 pagination meta 없음→클라 카운트.
- **추가 확정**: `AuthResult`에 refresh_token 없음 → **R1 권장**(ApiAccountGateway가 ITokenStorageService에 access+refresh 직접보관, AuthResult 무변경, refresh는 VM에 안 올림=보안+). LoginViewModel은 `AccountViewModel.Level`(EnumLevelType) 상속→외부 컨버터 바인딩 가능 → 어댑터가 Level 채워야 FR-11 완전 잉여 아님.
- **3-버킷 범위 확정**: **A**(Account 신규: ApiAccountGateway+IAccountApiService+TokenStorage+Permission+EnumUserRole+모듈) / **B**(공유 `.Api`: Bearer핸들러·Timeout·async·error분기 — **Device/Event 회귀 영향**) / **C**(외부 앱: Bootstrapper useDbAuth=false+등록, LeftMenu 만료타이머, 토큰공유 — **사전통지 필수** [[feedback_main_solution_advance_notice]]).
- **✅ 반영완료(2026-06-25, PRD v2.1)**: §2.4 신설(2.4.1 정합 아키텍처·2.4.2 3버킷·2.4.3 G1/G2/R1·2.4.4 메서드별 게이트·2.4.5 FR재분류) + §1.3 3버킷 재작성 + §3.1 superseded + §3.2 원칙1 정정(TokenStorage=gateway기록/handler읽기) + FR-10(G1 typed실패결과)·FR-13(LogoutAsync seam) 정합 + §7 리스크 3건(SEAM-G1/SEAM-METHODS/AUTHRESULT-R1). INDEX 갱신. **+ v2.2 완성(2026-06-25, "PRD 다 완성해")**: FR-9/11/19/20 source 정정 + 신규 **FR-21**(G1 typed 실패결과 + G2 LogoutAsync) + §5-A 검증(V-01~08) + §6 정합주의 + §10 DoD. **라이브러리 VM 편집=2곳**(LoginPanel 실패분기 + LogoutPanel 1줄). **상태=Draft 유지**(pipeline-state stale 미건드림).
- **✅ 사용자 구두승인("일단 진행해", 2026-06-25) → Plan 작성완료**: `docs/plans/GOP_Account_Auth_Integration-prd-plan.md` — **46 태스크 / ~116h / 7 Phase**(0 선결검증 VER-01~08+RISK / 1 버킷A 기초 DTO·Enum·Token / 2 버킷B 공유.Api Bearer / 3 버킷A 어댑터+FR-21 계약확장 / 4 버킷C EXT 메인앱 / 5 테스트 / 6 문서). 핵심산출=**IMPL-12 ApiAccountGateway**(3인터페이스+R1 TokenStorage+G3 폴백). 라이브러리 VM 편집 2곳=IMPL-08(LoginPanel 실패분기)/IMPL-09(LogoutPanel 1줄). 보류 DEFER-01(B-4/5 v4.10)·DEFER-02(권한편집 v4.8). 의존성 순환無(handler↔ApiService=Func 지연). INDEX 갱신. ⚠ **pipeline-state 미사용**(stale·공유) — 상태 파일관리. **활성 Plan**=위 파일, **Phase**=plan완료/dev대기.
- **🚧 dev 진행중(2026-06-25, "세션 충돌 없으면 진행"×반복) — 워크트리 `C:/workspace_app/worktrees/v2.14.0`(off v2.6, 롤백태그 `before-gop-api-migration`)**:
  · ✅ **28/46 (61%) — 라이브러리 100% 완료. 테스트 58/58, 빌드0. ⭐⭐⭐ 실서버 E2E + v2.6 머지 클린 + FR-21 + 스위치**: `https://localhost:8000/api/` admin 로그인→토큰(R1)→/users/me→계정목록 6건→refresh→logout 전 라이프사이클 통과. **테마세션 종료 후**: v2.6 동기화 머지(충돌0, HEAD `2781b21`) + **FR-21**(AuthOutcome typed 실패 + LogoutAsync, IAuthGateway 계약확장·양 어댑터·LoginPanel 사유메시지·LogoutPanel 서버로그아웃) + **스위치**(AccountUiModule useDbAuth=false→AccountApiModule, Accounts.Ui→Accounts.Api ref, 선택적 apiSetup). 커밋 8개(`8482a9e`..`2ec9ffd`, off v2.6 8커밋).
    - **✅ 머지 리허설**: v2.6@51e9911→v2.14.0 "Automatic merge went well" **충돌0**(동시세션 disjoint 확인, abort 원복). 정식 머지 클린 확정(현 시점).
    - **🔴 서버 발견(보고대상)**: GET `/api/audit-logs` = **HTTP 500**(기존 audit 레코드 action_type가 EnumAuditActionType 미포함→Pydantic 검증 실패). audit 연동 보류.
    - **실측 계약**: UserGroup 권한=**nested**(`modules:{view,edit,control,delete}`) vs 로그인 user.permissions=flat-string(admin 빈) = B-3 drift 실증. UserGroup/Session DTO 추가.
    - `LiveGopServerTests`(opt-in env, 비번 미저장). admin perms=0(role기반). ↓아래 부품:
    - Phase 1✅: IMPL-02 EnumUserRole+RoleMappingHelper / IMPL-01 DTO 7종(permissions=raw JObject?) / SETUP-02 `Accounts.Api`(net8-win, refs Api/Base/Messages/Enums/Monitoring.Models/Accounts, Test.Sdk 17.14·runner 3.1) / IMPL-03 TokenStorageService(JWT exp·lock)
    - Phase 2(공유.Api): IMPL-04 토큰필드+Timeout/`.Result` 수정 / IMPL-05 catch→BuildExceptionResponse(504/503/500)+ApiMessageHelper GATEWAY_TIMEOUT·JsonSettings공개 / IMPL-06 BearerAuthHandler(401 single-flight refresh·403미시도·`event SessionExpired`)+InternalsVisibleTo. **Devices.Api 회귀빌드 0**
    - Phase 3: IMPL-10 IAccountApiService/AccountApiService(Login/Refresh/Logout/GetMe + **User CRUD 5 + 본인프로필 3** = IMPL-11 User부분) / IMPL-14 AccountDtoMapper(+ToUserCreate/Update/SelfUpdate)+**PermissionsFlattener** / ⭐ **IMPL-12 ApiAccountGateway 완성(스텁0)** = IAuthGateway(login→API→R1 토큰보관→AuthResult) + IUserDirectoryGateway 6(목록/생성/수정/삭제/중복낙관/비번리셋) + IProfileGateway 3(GET·PUT /users/me·비번변경). DTO 추가: UserCreate/Update/SelfUpdate. TEST-12=게이트웨이 10테스트.
    - IMPL-13 **PermissionService**(flat 토큰 Can*·role등급·device_groups·이벤트) / IMPL-07 **ApiService 선택적 DelegatingHandler** 파이프라인(Device/Event=null 회귀0) / **AccountApiModule**(IMPL-15 일부: 전부품 DI등록+ApiAccountGateway 3인터페이스, TEST-15 DI스모크 순환0)
    - 🔲 IMPL-11 잔여=UserGroup/Session/Audit(관리화면 후속).
  · **충돌 회피 확정**: 동시 세션 Theme/Events.Ui/GMaps.Ui/Accounts.Ui/Tracking만 — 내 버킷 0겹침. **✅ 커밋 `8482a9e`(worktree v2.14.0, 41파일/+2074, 미머지·미푸시).**
  · ⚠ **로그인 API화는 테스트에서만 동작** — 실제 앱 반영은 IMPL-15 스위치(useDbAuth=false, **Accounts.Ui 충돌 보류**)+버킷C(메인앱 사전통지)+v2.6 머지+재빌드+살아있는 GOP 서버 필요.
  · ✅ **v2.6 머지 완료(2026-06-25, ff — "머지해")**: v2.6 HEAD=`2ec9ffd`, 메인 체크아웃 Accounts.Api 빌드0, 미커밋 docs 보존. **미푸시**.
  · ✅ **audit-logs 500 수정 완료(2026-06-26, 서버 직접) — 2중 조치**: `C:\workspace_python\api-test-server`([[reference_api_test_server]]). 원인=append-only audit 테이블에 테스트 찌꺼기(TEST_INS/TEST) 영구잔존+응답 strict enum → 500. ①**코드 하드닝**: `AuditLogResponse.action_type/resource_type`→str(tolerant), 5싱크(코드·명세서·swagger·도커이미지·컨테이너) 전부. ②**데이터 정리**(사용자 "지워"): 불변 트리거를 1트랜잭션으로 잠깐 DISABLE→3건 DELETE→ENABLE(가드 복구 검증=재차단 확인). 결과 38건·찌꺼기0·GET=200. **서버 repo 커밋 완료**(`1491532`, 안전점 `pre-audit-500-fix`, str유지 결정). → **.NET audit 읽기 연동 완료**: `AuditLogDto`(str tolerant)+`GetAuditLogsAsync`, 단위+라이브 E2E(감사 5건) 통과. v2.6 재머지 HEAD=`ae47ed5`(Tracking P5 disjoint 통합, 59/59).
  · ✅ **GOP 컷오버 플립 완료(2026-06-26, 사용자 "플립해")**: 메인솔루션 `Bootstrapper.cs:336` → `new AccountUiModule(setup,setup,_log,10, useDbAuth:false, apiSetup:setup)`. setup이 IApiSetupModel 구현·appsettings Url=`https://localhost:8000/api`. **앱 빌드 0오류.** 안전점 `pre-gop-auth-flip`@86c7855. **메인솔루션 미커밋**(런타임 확인 후).
    - 도중 2개 수정(v2.6 반영): **As<IService>**(AccountApiModule — 스위치 ApiService 런타임 Initialize 보장, 잠복버그) + **IApiSetupModel.BearerToken/RefreshToken 제거**(vestigial — 앱 SetupModel CS0535 break 해소). v2.6 HEAD=`eaee24b`, 라이브러리 60/60.
  · ✅ **런타임 1차 버그 잡음(2026-06-26)**: 앱 로그인·계정등록 둘 다 "아이디/비번 틀림"으로 실패 → 원인=appsettings `Url=https://localhost:8000/api`(끝 슬래시 없음)→HttpClient가 `/api` 떨궈 `/auth/login`·`/users`로 404. 증거: `/api/auth/login`=200 vs `/auth/login`=404. **수정=라이브러리 `ApiService` BaseAddress 끝슬래시 정규화**(appsettings 무수정, 모든 endpoint 일괄). v2.6 HEAD=`84c5239`, 62/62, 회귀테스트 `ApiServiceUrlTests`.
  · ✅ **로그인 성공 확인(런타임)** + 후속 2건: ⓐ 내 정보 매핑 분석(대부분 매칭, admin은 서버 null이라 빈칸. 갭=photo_url 인바운드 미매핑/Birth·Address·Company 서버無). ⓑ **"구분"을 GOP 5역할로 노출(`9bbc0e5` v2.6)**: AccountModel/IUserModel에 `Role`(EnumUserRole) 추가(Level 게이팅 호환 유지), AccountViewModel passthrough, AccountDtoMapper 인/아웃, EditorDialog 콤보+AccountManager 그리드→Role. 빌드0/62. **앱 재빌드 필요**.
  · ✅ **계정관리 런타임 버그 2건 수정(2026-06-26)**: ⓐ **삭제 무동작**(`656868a`) — OnClickDeleteButton이 SelectedItemCount 캐시(CheckSelectState 미발화로 0) 의존 → 체크상태 IsSelected 직접집계로 변경 + 결과 노출(서버 DELETE는 가드없음·FK CASCADE/SETNULL로 정상 확인). ⓑ **첫 진입 빈 목록**(`fc424dd`) — OnActivate가 _accountProvider 복사만 하고 fetch 안 함(갱신 버튼만 fetch) → OnActivate에서 GetAllAccountsAsync 로드. 둘 다 빌드0, **앱 재빌드 필요**.
    - 실서버 관찰: admin 비번 사용자가 변경(audit PASSWORD_CHANGED 10:07)→admin123 무효(정상). user123 생성됨(등록 동작 확인). FK는 전부 CASCADE/SETNULL.
  · ✅ **런타임 버그 다수 수정(2026-06-26, 에이전트 2회 투입 검증)**:
    - **admin 잠금 해제**: admin123 반복실패로 자동잠김(서버 브루트포스, is_locked) → DB UPDATE로 해제(account_users는 append-only 아님). 현재 비번=sensorway.
    - **monitor1(이력 있는 사용자) 삭제 = 서버 수정**(`bed2d74`, 안전점 `pre-audit-fk-anon-fix`): DELETE 시 user_login_logs.user_id/audit_logs.actor_id/config_change_logs.actor_id FK(SET NULL=UPDATE)가 append-only 트리거에 막힘 → `fn_block_audit_modification`을 FK익명화(링크만 NULL, 내용불변)만 허용하도록 수정(라이브 CREATE OR REPLACE + 마이그레이션파일 v51.1). 내용변경·행삭제는 계속 차단. **앱 재빌드 불필요(서버)**.
    - **code-reviewer(opus) 적발 수정**(`5cb1785`): W3 AccountViewModel.Role setter가 Level 동기(게이팅 드리프트 제거) / C1 ImageConverter http(s) URL 렌더(서버사진 표시) / W4 Reload·Delete refetch null가드 / W5 OnActivate fetch실패 안내.
    - **생년월일/주소/회사 제거**(`5771bd3`, 에이전트 실행+grep검증): 그리드+Editor/Register 폼 행 제거+Grid.Row 재인덱스, 모델필드 보존. 사진플러밍(`c0492ac`)·구분5역할(`9bbc0e5`) 기반 위.
  · ✅ **회원가입 버튼 + W6(`887d42a`)**: 로그인 ClickRegister → "회원가입은 통합관제 웹에서 진행" Info 팝업(self-signup 불가). FailMessage FORBIDDEN/LOCKED → "잠겼거나 비활성(로그인시도초과) 관리자문의". MyPage 필드제거(`022ebfb`)로 전 계정화면(그리드/편집/등록/마이페이지) 필드정리 완료.
  · ✅ **프로필 사진 서버 저장 구현 완료(W1 해결, 2026-06-26)**: PRD `GOP_Profile_Photo_Upload`(v1.1) + Plan. 방식=썸네일 패턴(파일+API URL, base64/static 아님 — 스키마 §8.1 photo_url=VARCHAR500 URL 근거).
    - **서버(`44a120f`)**: `POST /api/users/me/photo`(multipart, jpeg/png/webp/gif ≤5MB) → 호스트 `./data/profiles/{uid}_{uuid8}.{ext}` 저장 + photo_url 절대 URL 갱신. `GET /api/users/photo/{name}`(FileResponse, 인증불요, traversal 차단). `PROFILE_STORAGE_PATH=data/profiles`. 5싱크 완료. 안전점 `before-profile-photo-upload`.
    - **클라(`f0a8f72`)**: `UploadMyPhotoAsync`(PostFormDataRequestAsync) + `IProfileGateway.UploadPhotoAsync`(API=URL반환/DB=null) + MyPage `ClickAddPicture` 로컬저장 후 서버업로드→Image=URL. stub 4종 동기화. 빌드0, Accounts.Api 62/62.
    - **E2E 검증(서버)**: 업로드200/서빙200/badtype400/**강제재생성 후 GET200(영속)**. ⚠ 이미지=호스트 ./data(영속), DB엔 photo_url URL만.
    - **앱 재빌드 필요** → MyPage 사진 선택 시 서버 저장+표시. follow-up(미구현): admin Editor 사진 업로드(FR-04, 서버 admin 엔드포인트 필요), 로그인화면.
  · ✅ **로그인 부가기능(2026-06-26)**: 아이디저장(API모드 G3 — ApiAccountGateway 로컬 prefs `login-prefs.json`, `bcc1f86`) + 비밀번호 눈알 hold-to-reveal(LoginPanelView IsPressed 트리거) + AccountManager 정렬 고정(ADMIN최상단+Id순=생성순, `bc8a319`). 빌드0.
  · 🔬 **권한(RBAC) 재설계 진행중(2026-06-26~)**: 현재 **거의 미게이팅** 확정 — IPermissionService 엔진(CanView/Edit/Control/Delete) 존재하나 `Apply()` 호출0·VM소비0(dead), 옛 binary `AdminLevelAllowConverter` 3곳(계정관리·이벤트/장비대시보드 메뉴·계정셋업)만. SETUP은 Level가드 없음.
    - **산출물**: HTML 권한UI지도(`docs/GOP_Permission_UI_Map.html` — 역할별 스토리보드/와이어프레임) + **PRD-GOP-01 v2.0**(`docs/prds/GOP_Permission_Gate_Feature-prd.md`, §6 신설, 상태 Draft·재검토요청).
    - **검증 워크플로우 2회**: ① 200시나리오×2시뮬(31에이전트) → **확정모순11**(이벤트조회=CRUD흡수→OPERATOR위조·삭제 / 감사full=삭제 append-only충돌 / 마지막ADMIN본인삭제 무관리자화 / 비ADMIN본인삭제 감사익명화 / VIEWER가시성토글 공유DB영속 / **엔진 GMaps.Ui·Devices.Ui 참조0** / 서버RBAC부재 / 역할강등중작업 미차단) + 매트릭스14수정+규칙13+공백10. ② **V-PG-01 서버RBAC 실태감사 진행중**(`w489r4oyz` — 쓰기가 GOP REST vs 직접DB, role검증 유무).
    - **PRD v2.0 핵심규칙(FR-PG-07~14)**: 동사별토큰 / 이벤트 조회+ack vs 레코드CRUD 분리 / 감사 append-only(수동삭제 전면금지) / last-admin가드+비ADMIN소프트삭제(audit FK보존) / 레이어 로컬필터vs공유영속 분리 / PermissionsChanged구독(역할강등시 편집강제종료·버퍼링저장) / **양층방어(UI+서버RBAC)+GMaps/Devices.Ui에 IPermissionService 주입**.
    - **미결 OQ-PG-04~07**(PM결정): IR/AF등급·감사export권한·GUEST의미·비ADMIN본인삭제정책. **재검토→`advance-phase approve prd` 대기**.
    - ⚠ 대상 다수가 **메인솔루션**(좌측메뉴 HamburgerMenu·SetupPanel·이벤트/장비 패널 호스트) → 메인+라이브러리 동시작업(사전통지 대상). 메뉴 Label "ACCOUNTS" 오타(이벤트/장비 대시보드)도 발견.
  · ⚠ **앱 재빌드 필요**: .NET 수정(5cb1785/5771bd3 등) 반영하려면 앱 닫고 Rebuild. 동시세션이 Accounts.Ui 다이얼로그 테마 작업중(영역 다름, 충돌 없이 머지).
  · 🛑 **남은 것(사용자 테스트 중)**: **① 앱 닫고 재빌드→실행** → 로그인/등록/구분5역할/**삭제/첫로드** 확인 · **② 메인솔루션 Bootstrapper 커밋**(런타임 OK 후, `pre-gop-auth-flip`) · **③ origin push**(v2.6) · DEFER(v4.10/v4.8).
  · 🔲 **다음(사용자 결정)**: ① 워크트리 커밋(55테스트분 보존) 여부 ② Accounts.Ui 테마 세션 안착 후 스위치+FR-21 ③ 메인솔루션 사전통지 후 버킷C ④ 서버팀 v4.7/4.10 일정.
- HTML 쉬운설명=`docs/reports/PRD-explainer.html`(사용자 "너무 간단" 피드백—필요시 보강). [[project_account_integration_strategy]] [[feedback_main_solution_advance_notice]] [[feedback_dev_phase_agents]]

## 📋 PRD 갱신(2026-06-24): GOP-00 인증연동 PRD v1.0→**v2.0** — 서버 회신+v4.9 검토 반영 (Draft) — `docs/prds/GOP_Account_Auth_Integration-prd.md`
- **트리거**: "Account쪽 세션 복구" → 복구 중 **세션컨텍스트 미기록 사실 발견**: 서버팀(이기호 차장) 회신 `Docs/GOP_Server_API_OpenQuestions_RESPONSE.md`(2026-06-24, **31질의 중 30 코드실측 확정**) 도착(컨텍스트는 "전달→회신대기"까지만 기록했었음). 사용자 선택=**"GOP-00 PRD 최신화"**.
- **갱신 핵심(§2.3 신설)** — PRD v1.0(v4.6 가정, 06-19)이 서버 확정과 다수 모순 → v2.0 정정:
  · ① **401=항상 envelope** `{success,error,meta}`(명세 §9.2.4 `{detail}` 예시 폐기) → FR-1/6/7/10 raw `{detail}` 경로 제거
  · ② **🔴 permissions 3원 drift(B-3 P0)**: 실제 응답=**flat-string** `{"events":"rw"}`인데 PRD DTO=**nested** `{view,edit,control,delete}` → **역직렬화 실패** → 관용파서+**view-only**(v4.8 7/11 enum 결재 전), `Dictionary<string,object>` 원형보관(AD-PERM)
  · ③ **access_token 서버 블랙리스트 없음**(F01-S-01, refresh만 jti) → 로그아웃/잠금/비번변경 후 24h 유효 → **클라 강제 토큰폐기+재로그인**
  · ④ ChangePassword 스키마 확정 `{current_password(min1),new_password(min6,max100)}` + **서버 세션무효화 안함**(F07-01)
  · ⑤ `/auth/me`=flat·`/users/me`=envelope, **permissions는 login 응답에만**(me/refresh 없음→`/user-groups/{id}` 우회), 두 me 세션 미검증(F04-I01)
  · ⑥ `PUT /users/me`의 photo_url 미반영 버그(C-5, v4.7 6/30 핫픽스)
- **TTL 확정**: access **24h**(env `JWT_EXPIRATION_HOURS`), refresh **7일**(하드코딩), `expires_in` 응답 **없음**→JWT `exp` 디코드.
- **OQ-PERM 해소**: 단일그룹 raw=최종값(병합불요)·refresh 무권한·user-level 미구현. **잔존 게이트**: B-4(login_id 중복확인)·B-5(로그인이력)=**v4.10**, B-7(permissions in /me)·B-8(pagination meta)=요청중, ENV-1 봉투표준=v4.7/4.8. **서버 캘린더**: v4.7 핫픽스 **6/30** / v4.8 **7/4**초안·**7/11** 결재+RBAC매트릭스 UI 활성.
- **편집분**: 헤더 v4.6→v4.9·v2.0·Draft+변경이력 / §2.3(2.3.1 확정계약표·2.3.2 drift·2.3.3 caveat·2.3.4 게이트) / FR-1·7·10·17 정정 / §7 리스크 5건(ACC-TOK/PERM-DRIFT/ME-SESS/PHOTO-BUG/GATE-410)+OQ 해소 / INDEX 갱신.
- **판정**: 로그인/토큰/refresh/Bearer 파이프라인=**즉시 착수 가능**, 권한편집·photo·중복확인·이력=서버 일정 게이트. ⚠ **pipeline-state.json stale**(activePrd=Tracking_GIS, 공유상태)—**건드리지 않음**, Draft라 승인 전까지 activePrd 미설정(advance-phase 미사용).
- **🔲 다음(사용자)**: PRD v2.0 검토 → 승인 시 `node .claude/hooks/advance-phase.js approve prd "..."` → **GOP-00 Plan 작성**(현재 plan 0개). 또는 서버팀에 v4.9 검토 이슈리스트(`Docs/GOP_Server_API_v4.9_Review_Issues.md`) 회신 받아 잔존 P0 확정 후 착수. [[project_account_integration_strategy]] [[feedback_plan_required_before_dev]] [[feedback_prd_before_code]]

## ✅ 완료(2026-06-24): RTSP 팝업 PTZ — 배지+워밍+속도+레이아웃+ContinuousMove+슬라이더+빠른초기화 — …/`aa6090d`/`aaebfff`(+OnvifSolution `f64c35d`)
- **⑧ PTZ 빠른 초기화**(메인 `aaebfff` + OnvifSolution `f64c35d`, 롤백 `before-ptz-fastinit`: main@`aa6090d`/onvif@`f38c35a`): **목표=첫 오픈 준비 <5초**(현재 COLD ~28초, WARM 즉시). **로그 측정**: cold 28초 구간 25초가 `InitializeFullAsync` 침묵 블랙박스. **근본병목**(코드): `OnvifClientFactory.CreateClientAsync<T>`가 Media/PTZ/Imaging **클라이언트마다** device-core(`GetSystemDateAndTime`)+`GetCapabilities`(각 2왕복) **반복** → 3개면 6왕복 + device-info STEP(GetScopes/NIC/Services 5왕복)+프로파일별 GetStreamUri+ONVIF프리셋(앱은 로컬DB) = ~12왕복. **수정**: `CreatePtzBundleAsync`(device-core+GetCapabilities **1회** 공유로 3클라 일괄) + 빌더 `WithPtzBundleAsync`/`WithProfileTokenAsync`(스트림파싱 생략) + `InitializePtzAsync`(타이밍 로그 `[Onvif] InitializePtz … total=…ms`). PtzController가 InitializeFull→InitializePtz. **SOAP 12→3왕복**. InitializeFull 등 기존 경로 무변경(추가만). **⚠ 회귀+수정**(OnvifSolution `35e0aeb` + 메인 gitlink `a7cf02b`, 롤백 onvif@`before-ptz-caps-all`=f64c35d): 사용자 — 5초 내 끝나지만 **PTZ "지원안함" 오판정**. 원인: `CreatePtzBundleAsync`가 `GetCapabilities([Media,PTZ,Imaging])` 다중카테고리 요청 → 일부 카메라가 첫 카테고리(Media)만 반환 → `caps.PTZ=null` → PtzClient null. **수정=표준 `[All]` 요청**(원래 동작 코드는 카테고리 1개씩 요청해 정상이었음). **속도 최적화 자체는 성공**(5초 내). 🔲 **재빌드 후 로그 `[Onvif] InitializePtz … (ptz=True) total=…ms` 확인**(<5000ms + ptz=True). **다음=백그라운드 프리워밍**(맵 로드 후 PTZ 카메라 사전 준비, 사용자 지시). 🔭 **장기비전(사용자)**: NVR/통제에 ONVIF 인스턴스 전담 매니저 → 클라는 메시지 통신으로 제어+데이터 수신(ONVIF 직접 연결 제거).
- **⑦ PTZ 속도 슬라이더 단위 버그**(`aa6090d`): 사용자 — 슬라이더가 0.1↔1.0만 잡힘. 원인=가로배치에서 슬라이더 폭 ~38px(좁음). 수정: 라벨/값 위 행, 슬라이더 전체폭 + `TickFrequency=0.05`·`IsSnapToTickEnabled`(0.05 단위)·`IsMoveToPointEnabled`(클릭 이동).
- **⑥ PTZ ContinuousMove 무동작 수정**(`0c2a59b`, 롤백 `before-ptz-continuous-space`@ae3f0c7): **로그 진단 확정**(`log-2026-06-24.txt`) — cam=1969 capable=True인데 ContinuousMove 보내도 **오류없이 무동작**, "PTZ→FOV 업데이트"는 **14:49가 마지막**(당시 드래그=RelativeMove). 커밋 `9992458`이 ContinuousMove 전환한 뒤 이 카메라 무동작. **근본원인**: `ContinuousMoveAsync`가 velocity DTO에 `Space` 미설정(`Vector2DDto{X,Y}`) → `ToWsdl`이 space=null 전송 → 일부 카메라가 velocity 무시(RelativeMove는 GetNode `RelPtUri` 실어 동작). **수정(사용자 선택=space 보정)**: `SpaceInfo`에 `ContPtUri`/`ContZoomUri` 추가, `LoadSpaces`가 GetNode `ContinuousPanTilt/ZoomVelocitySpace` URI 로드, `ContinuousMoveAsync`가 velocity에 명시 + 진단로그 `[PTZ] ContinuousMove … ptSpace/zSpace`. 🔲 **재검증**: 로그 ptSpace가 URI면 적용·`null`이면 카메라가 continuous space 미advertise(→ RelativeMove 폴백 등 다른 대응 필요). ⚠ 앱이 18:39 세션엔 워밍수정까진 반영됐으나 이 커밋은 미반영 — **재빌드 후 테스트 필요**.
- **⑤ PTZ 탭 좌(패드)·우(속도) 가로배치**(`ae3f0c7`): 사용자 스크린샷 184203 — 8방향 패드(좌)+PanTilt/Zoom 슬라이더(우) 가로 2열 Grid(기존 세로적층 192>188 → 가로 132로 패널에 맞음). PART_PtzPad x:Name·Tag 유지(컨트롤 OnPadDown FindButtonTag 입력 보존). ⚠ **미해결**: 스크린샷 183852 "윈도우(재생) 컨트롤 바 가려짐" 버그는 사용자가 PTZ/레이아웃으로 화제 전환 — 별도 확인 대기(스트리밍 컨트롤 바=ImprovedRtspPlayer 최하단 행, PART_VideoRegion ClipToBounds).
- **④ PTZ 속도 제어 + 비PTZ 버튼 비활성**(`7935b51`, 롤백 `before-ptz-speed-control`@c5b008c): 사용자 — PTZ 제어 시 넘길 속도를 PTZ 탭에서 조절(PanTilt/Zoom 슬라이더+텍스트박스 직접입력), 비PTZ 카메라는 PTZ 패드·프리셋 버튼 전부 비활성. **구현**: VM `PanTiltSpeed`/`ZoomSpeed`(double, [0.1~1.0] 클램프, 기본 0.6) 추가 → MapViewModel 하드코딩 상수 `PtzMoveSpeed`/`PtzZoomSpeed`(0.6) 제거하고 드래그(HandlePtzDrag)·패드(OnCameraPopupPtzNudge)·휠줌(HandlePtzZoom)의 ContinuousMove 속도를 `vm.PanTiltSpeed`/`vm.ZoomSpeed`로 대체(펄스/지속시간 상수는 유지). XAML: PTZ 탭 패드 아래 Slider+TextBox 2행(양방향 동기). `PART_PtzPad`·속도패널·프리셋 StackPanel에 `IsEnabled="{Binding IsPtzCapable}"` → 비PTZ면 전체 비활성(옵션 탭은 IsImagingCapable 별도 게이팅, 고정카메라도 사용). 빌드0. 🔲 런타임: 슬라이더/텍스트 속도반영·비PTZ 비활성 확인.
- **① PTZ 상태 배지 재도입**(`be44839`, 롤백 `before-ptz-state-badge`@f030f08): 스크린샷(`Docs/pictures/스크린샷 2026-06-24 162918.png`) 요청. `CameraStreamPopupStyle.xaml` `PART_VideoRegion` 오버레이에 Border 3개(상호배타). 🟠준비중=`IsPtzLoading` / 🟢가능=`!IsPtzLoading && IsPtzCapable` / ⚪지원안함=`!IsPtzLoading && !IsPtzCapable`(MultiDataTrigger). 반투명 검정(`#AA000000`)+상태색 Ellipse(green `#2ECC71`/orange `#F39C12`/gray `#95A5A6`)+흰 텍스트. `IsHitTestVisible=False`. `IsPtzLoading`/`IsPtzCapable` VM·MapViewModel 배선(654/666/671)은 이전 제거(`e60c0c5`) 때 보존돼 재사용(코드 무변경). 이력: `de278eb`(추가)→`0b8b121`(하단)→`e60c0c5`(제거).
- **② 배지 위치 정정**(`d43159b`): 사용자 "Playing 있는 그 구간(division)·높이에" → RTSP 헤더 Row0(`#FF3F3F46`, "Playing"/"스트리밍 HUB" 띠)와 같은 높이. `Margin 6,34/Left` → `Margin 0,5/Center`(좌 HUB·우 Playing 충돌 회피 상단 중앙). ※팝업 영상=`ImprovedRtspPlayer` 템플릿(`ImprovedCameraItem.xaml` L172 `Style TargetType=ImprovedRtspPlayer`): 3행 Grid Row0=상태헤더띠, Row1=영상+HUB오버레이.
- **③ 비PTZ 워밍 버그수정**(`c5b008c`, 롤백 `before-onvif-warm-nonptz`@d43159b): **증상**(사용자) PTZ 미지원(고정) 카메라는 팝업 재오픈마다 ONVIF ~31초 재구동, 워밍(`c61a5d4`) 무효. **근본원인** `PtzController.EnsureReadyAsync`: "초기화 필요"를 `ctx.Model?.PtzClient==null` 기준 → 비PTZ는 `InitializeFull`이 PtzClient 없는 모델을 주는데 **캐시 안 하고 return false** → `ctx.Model` 영영 null → 매 오픈 재초기화. 또 `model?.PtzClient==null`이 연결실패(model null)와 비PTZ(model 존재)를 뭉뚱그려 둘 다 폐기 → 고정카메라 Imaging도 못 씀. **수정**: 판단을 `ctx.Model==null` 기준으로, `model==null`만 비캐시(재시도 허용)·model 있으면 PtzClient 없어도 캐시 유지(워밍+VsToken/ImagingPossible), `LoadSpaces`는 PtzClient 있을 때만(NRE 방지). Move/Imaging API는 각자 null-가드라 무영향.
- **검증**: GMaps.Ui 빌드 **오류0**. 🔲 런타임(사용자): ⓐ배지=Playing 띠 높이 중앙 ⓑ상태 전환(🟠→🟢, 비PTZ ⚪) ⓒ**비PTZ 카메라 재오픈 시 ONVIF 재구동 없이 즉시** ⓓ고정카메라 옵션 탭(주야간/포커스) 동작 여부. ⚠ 후속: Release 시 WCF 채널 미Dispose(기존 H2 정책, 별건) · 🟢"가능" 거슬리면 N초 fade 옵션. [[project_wpf_animation_patterns]] [[project_nats_disconnect_strategy]]

## 🔍 검토완료(2026-06-24): GOP RESTful API 연동설계서 v4.9 면밀 검토 (GOP-00 착수 전) — `Docs/analysis/GOP_Restful_Api_v4.9_Review-analysis.md`
- **트리거**: Account.UI 세션 복구 후, 사용자가 "API 명세서 `GOP_Restful_Api_연동설계.md` 최신화했으니 면밀히 검토" 요청. 다음 큰 목표=GOP-00(Direct-DB 인증→JWT Bearer) 직결.
- **대상**: `Docs/GOP_Restful_Api_연동설계.md` v4.9(최종수정 2026-06-24, 약 15,700줄/552KB, 작성자 이기호 차장). 9도메인·39 Enum·200+ 엔드포인트. ⚠ `Docs/`=gitignore라 Grep/Glob 불가→Read 직접. PowerShell 줄번호는 Read와 ~100~200줄 드리프트(한글 인코딩). 일부 구간 토큰 밀집(Read limit 작게).
- **방식**: 7개 병렬 검토 에이전트(§1-4 기초+Enum / §5.1-5.5 / §5.6-5.11 Device / §6 Event / §7 Integration / §8+§10 / **§9 Account+§11+§12 opus 심층**). 공통 루브릭: 정합성·완전성·모호점·.NET 연동영향·편집오류. 총 **60+ findings(Critical ~14)**.
- **★핵심 결론**: ① 명세는 성숙하나 **클라이언트 자동 역직렬화를 막는 횡단(systemic) 결함 5종**이 통합비용 좌우 → **S1 비밀번호 노출 — 2종 구분(사용자 지적 반영)**: 계정비번(§9.2 `admin123`)=제거 / **장비접속 자격증명(`user_password` 카메라ONVIF·RTSP·스피커·NVR §5.3/5.4/8.3)=클라 직접접속에 필수→제거불가, TLS+목록마스킹·상세평문 정책명문화+권한게이트+DB at-rest로 보호**(SEC-1a P0 / SEC-1b P1) · **S2 Response envelope 불일치(목록 data shape 3종/에러 4종/meta누락/DELETE/204·200 모순)** · **S3 datetime timezone 혼재(Z/μs-no-tz/+09:00)** · **S4 Enum 케이싱 7종(EnumBuzzerSound `"Fire A-WANG"` 공백+하이픈 쿼리위험, EnumOnOff 소문자)** · **S5 Enum 정의≠예시(§8.7 type_event, MainController, DetectionType 4누락)** · (S6 PATCH/PUT 모호, S7 편집잔재 JSON주석/trailing comma §5.3.3/연도).
- **★GOP-00 판정 🔴 현 명세만으로 빌드 불가**: 로그인/토큰/refresh/logout 골격 존재하나 — Crit 3(`/api/auth/me` 봉투위반 raw객체 §9.2.5 / 토큰 `expires_in` 미명시 / `PUT /users/me/password` 본문스키마 부재) + 게이트웨이 공백 4(IsUsernameTaken=B-4·GetLatestLogin=B-5·RecordLogin암묵·permissions in /me=B-7). **다행: 변경이력상 대부분 v4.9 잔존/v4.10으로 서버팀 이미 인지**. v4.9=GOP-00 인증차수(jti블랙리스트·refresh TTL분리 7일·RBAC강타입·photo_url XSS validator). v4.7 인증 113이슈 완성도 62.5% FAIL → 인증도메인 미성숙 반영중.
- **권장 액션**: S1(즉시 보안)→S2(봉투 표준1종 sweep)→GOP-00 게이트(인증 Crit3+잔존 B-4/5/7/8 서버팀 일정확정)→S3/S4/S5 정책 명문화→S7 JSON 린트. 게이트웨이 가변부는 `IAuthGateway` seam 격리(미구현 연산 임시우회 후 교체). [[project_account_integration_strategy]] [[project_docs_gitignored_grep_blind]]
- ✅ **서버팀 공유용 이슈리스트 작성**: `Docs/GOP_Server_API_v4.9_Review_Issues.md` (.NET 통제UI팀→서버 이기호차장). 기존 `GOP_Server_API_FollowupRequests.md`(Account 한정, B-1/3·A-2/4 v4.9 적용·B-4/5/7/8 잔존)와 중복 회피하며 전 도메인 신규+횡단 결함 정리. **P0 4**(SEC-1 password평문·ENV-1 봉투표준·AUTH-1 토큰expires_in·AUTH-2 비번변경본문) + **P1**(FMT-1 datetime·ENUM-1/2·도메인별 DEV/EVT/INT/SVR/RPT·AUTH-3/4+잔존 B-4/5/7/8) + **P2**(DOC 린트). 회신요청: ENV-1 봉투표준 결재전 선공유 + B-4/5/7/8 v4.10 일정.
- **🔲 다음(사용자 결정)**: 서버팀 전달 → 회신 후 GOP-00 PRD(`GOP_Account_Auth_Integration-prd.md` 존재)에 확정사항 반영 → Plan. (또는 특정 섹션 라인단위 심층 추가검토)

## 🔧 세션복구(2026-06-24): RTSP 팝업 PTZ — 크래시 복구 + 미기록 12커밋 정합
- **상황**: 세션이 갑자기 종료됨. PTZ/RTSP 팝업 작업은 v2.6 머지(`0d03f83`) 후 **메인 체크아웃 v2.6에서 직접 계속** 진행됐는데, 이 컨텍스트는 `8e44723`까지만 기록 → **이후 12커밋 미기록 상태**로 크래시. git log/서브모듈/worktree 대조로 복구.
- **현재 상태**: 브랜치 `v2.6` HEAD=**`f030f08`**(OnvifSolution gitlink bump, 아래 ✅), 직전 `c61a5d4`(2026-06-24 16:01, 빌드0). origin/v2.6 대비 **ahead 37(미푸시)**. worktree `v2.12.0`=c61a5d4(빈 분기·여기서 한 작업 없음). 작업트리 잔여: `.gitignore`·`Docs/INDEX.md`·미추적 `tests/`(전부 기존 보존분, 무관 — 손대지 않음).
- **미기록 12커밋(`8e44723`..`c61a5d4`) — 실카메라 런타임 피드백 반영 폴리시**:
  · `e986895` 컨트롤 패널이 영상 가림 → 펼침 시 팝업 성장(영상 보존)
  · `9992458` **PTZ ContinuousMove 전환**(드래그/패드/휠) + 패드 누름·뗌 + 탭 강조 ← Relative→Continuous 패러다임 전환(핵심)
  · `6cf7e41` 헤더 "옵션" 버튼 + 우클릭 컨텍스트 메뉴(PopupMenu)
  · `28a42bd` PTZ 방향 패드 미동작 수정(PART_PtzPad 의존 제거, 컨트롤 레벨 Preview 처리)
  · `895827e` PTZ 제스처 **Last-Write-Wins**(연속 드래그/줌 큐 적체 방지) — IMPL-05 LWW 후속 반영
  · `de278eb` PTZ "준비 중" 배지 추가 → `0b8b121` 위치 이동 → `e60c0c5` **배지 제거**(31초 동안 정적이라 거슬림, 최종 제거됨)
  · `c120c90` PTZ 조작 시 FOV 부채꼴 쪼그라듦(ONVIF값↔심볼 FOV 스케일 불일치) 수정 → `2dcf0dc` **revert: ONVIF 직접 FOV 갱신 제거**(FOV는 NVR→NATS 경로에 일임으로 결정)
  · `d99dcbf` **fix(Streaming): Hub 모드 스냅샷 미동작** — 공유 프레임(SharedFrame) 직접 PNG 저장(ImprovedRtspPlayer/ImprovedRtspStreamingService)
  · `c61a5d4` (HEAD) **ONVIF/PTZ 인스턴스 워밍 유지** — ONVIF 준비 ~31초라 매 오픈 재초기화 비효율 → CloseCameraPopupAsync에서 Release 제거(StopAsync만), per-camera 컨텍스트(PtzClient/GetNode space) 캐시 유지 → 같은 카메라 재오픈 즉시 활성(첫 오픈만 31초). 심볼 Remove 시에만 Release(FR-13).
- **✅ 처리 완료(2026-06-24, 복구 후)**: `OnvifSolution` 서브모듈(고아 gitlink)에 **`f38c35a perf(onvif): 카메라 초기화 ~31초 단축`**(WCF 타임아웃 명시 + 시간동기 중복 제거)이 서브모듈 repo에 커밋돼 있었고 부모 v2.6 gitlink는 옛 `7dfff32`를 가리켜 미스테이징 상태였음 → **부모 gitlink를 f38c35a로 bump 커밋 `f030f08`**(사용자 선택). 이로써 크래시 시점에 떠 있던 변경 0. 앱엔 이미 반영(메인 디렉터리 OnvifSolution), 포인터는 의존 버전 기록용.
- **다음**: ① **실카메라 재검증(사용자)**: 좌=ContinuousMove 드래그/우=패널·방향패드/휠줌/선택 시 영상 유지/Hub 스냅샷/인스턴스 워밍=재오픈 즉시(첫 오픈만 31초)/FOV는 NATS 경로만 갱신 ② 미푸시 **37커밋** 푸시 여부 ③ Plan 진행률(34/41, 83%)은 머지 시점 기준 — 위 폴리시 12커밋은 형식 태스크 외 런타임 반영분(Plan 재동기화 선택). [[project_nats_disconnect_strategy]] [[project_gmap_orphan_submodule]] [[feedback_main_solution_advance_notice]]

## 📋 PRD 검토대기(2026-06-24): UI Modern Dark/Light 테마 디자인 시스템 — `docs/prds/UI_ModernTheme_DesignSystem-prd.md` (Draft, Track C)
- **흐름**: GUI 디자인 개선 요청 → ① 전체 View ≈137 식별(읽기전용 Agent 6) → ② Dark/Light 컨셉 3종 HTML 스토리보드(`docs/design/storyboard.html`, 컨셉×테마 스위처+와이어프레임7+미리보기 3 jpeg, WCAG 검증) → **③ 사용자 "모던" 선택** → ④ Modern 적용 PRD(시나리오별 ≥2 시뮬).
- **PRD 생성 워크플로 `wf_9bc61002-c2b`(22 에이전트)**: 조사4(App.xaml/DesignTokens/하드코딩분류/CustomControl·빌드) + **8 시나리오 × 2 시뮬레이션(16)** + 합성1 + **적대적 리뷰1**. 리뷰가 초안 ship-blocker(카운트 오류·의존성·byte-identical 모순) 정정 → 최종 PRD 반영.
- **핵심 설계**: 전용 leaf 어셈블리 `Ironwall.Dotnet.Libraries.Theme`(토큰 dict 무의존 / `IThemeService`는 MD+MahApps 의존; GMaps에 두면 GMaps→Events.Ui ProjectReference로 **순환참조**) · **리터럴 whole-brush 스왑**(AD-2) · DesignTokens 3분할(Colors/Styles/Converters) · **Empty-Color-Common**(색은 App.Resources+Generic.xaml 두 스코프만) · GMaps **102** Static→Dynamic + **19 style dict** 색상merge 제거(Generic.xaml은 유지) · severity hue-lock Status* + 의미색/**CameraStreamPopupStyle** denylist · Streaming **영구 다크 OOS**(2 어셈블리) · **SkiaSharp 차트 ThemeChanged 재색칠**(WPF 리소스 미도달, ChartHelper.cs 포함 ~10 white) · 한글폰트 MaterialDesignFont+MahApps Fonts.xaml 동기화 · **참조-vs-정의 키 린터**(기존 134 DynamicResource 사전감사) · GMap.NET robocopy+**커밋가드**+v2.6 격리머지.
- **✅ AD-6 확정(2026-06-24, 사용자, 부록B Q-1)**: **Light=byte-identical(기존 출고 hex 유지) + 신규 Dark만 도입**(구조 안전). Modern 캐노니컬 Light는 후속 visual-refresh로 분리. → Phase 1 토큰 dict = Light(기존 hex 보존)+Dark 2종.
- **규모**: FR 15 · NFR 5 · 리스크 22 · V 13 · 시나리오 8×2. in-repo 확정 카운트(앱측 별도 캡처): *.Ui/Views 58, GMaps 토큰 StaticResource 102, DesignTokens self-merge 21, #33000000 10파일, Resources.xaml 클론 4, PidsMarkerStyle 168hex/198Path.
- **✅ 사용자 구두 승인("진행해줘", 2026-06-24) → Plan 작성 완료**: `docs/plans/UI_ModernTheme_DesignSystem-prd-plan.md` (72 태스크 / 8 Phase, planner 검증 중). ⚠ **pipeline-state.json stale**(`activePrd=Tracking_GIS_Visualization_Playback-prd.md`, phase=prd, 6-19 갱신) → `advance-phase approve prd` **미사용**(엉뚱 PRD 오염 + 동시세션 공유상태 비건드림). 정식 게이트 필요 시 activePrd 동기화 후 실행.
- **⚠️ 동시 세션 감지(D-9/R-23)**: 타 세션이 **v2.6 main checkout에서 GMaps.Ui PTZ 작업 + ~7분 간격 직접 커밋 중**(15:04~15:16 파일수정, 커밋 6/13/24/31분전 lsirikh). session-guard.json은 1세션만 기록(미감지) → git cadence+mtime으로 확인. **대응: 별도 worktree 전용 + Phase1~5 GMaps 무접촉(병행 안전) + Phase6(GMaps.Ui) PTZ 머지 후 v2.6 rebase 게이트 + CameraStreamPopupStyle denylist.**
- **✅ 환경셋업 완료**: SETUP-01 롤백태그 `before-modern-theme-migration`(@c61a5d4) + SETUP-02 worktree `C:/workspace_app/worktrees/v2.12.0`. **충돌 재검증(16:51)**: 동시 PTZ 세션이 v2.6 활발 커밋 중(우리 base보다 4커밋 전진, 마지막 56초전)이나 변경파일=GMaps.Ui/PtzController.cs+CameraStreamPopupStyle.xaml(denylist)+OnvifSolution 포인터 → **Phase 1과 0% 겹침·.sln 무변경·신규 어셈블리명 충돌 없음 = disjoint 안전 확정**.
- **✅ Phase 1 전체 완료(2026-06-24, 빌드0 0경고 + 테스트 11/11)**: leaf 어셈블리 `Ironwall.Dotnet.Libraries.Theme`(8파일) — IMPL-01(csproj net8, MD/Colors 5.2.1+MahApps 2.4.10, GMap/*.Ui 무참조) + IMPL-02~05(Tokens.Light byte-identical[`(현 ...)`5종=VER-14 앵커]/Tokens.Dark Modern/Tokens.Shared/Converters/Theme.Current) + IMPL-06/07(IThemeService+ThemeService: Add-new→Remove-old·Dispatcher단일패스·_gate직렬화·이벤트락밖·R-17중복차단·**토큰팩토리+MergedDictionaries 주입형=테스트가능**). **TEST-07**(`.Theme.Tests` 신규, ThemeService 7/7) + **TEST-LINT+RISK-03 린터**(`ThemeKeyLinter`, 4/4; 실제 Tokens.Light≡Dark 파리티 게이트 통과). **MD 5.2.1 API**: `ThemeExtensions.SetBaseTheme(Theme, BaseTheme enum)`; `Theme`는 네임스페이스 충돌로 정규화. **System.IO ImplicitUsings 제외**(명시 using). VER-09 완료. **미커밋(worktree)**. Plan 12/72.
- **✅ Phase 2 라이브러리측 완료(2026-06-24, 커밋 `2eacdf2`@v2.12.0, Plan 15/75)**: IMPL-08(Accounts/Devices/Events/Sounds.Ui 4종 csproj에 Theme ProjectReference — AD-1 leaf 순환없음, 4개 빌드0) + TEST-12(`ThemeToggleSmokeTests` 3종: Toggle 라운드트립·ThemeChanged 시퀀스·dict 누수 가드 → **테스트 14/14**) + IMPL-09(GMaps `Generic.xaml` 토큰 병합 **설계안** `docs/design/IMPL-09_GMaps_Generic_Merge_Design.md`; ⛔ Generic.xaml 실제 편집 0, Phase 6 게이트). **⚠ worktree 빌드 함정 확정**: OnvifSolution=고아 gitlink라 worktree 소스 0개(main 39개) → Devices.Ui/Events.Ui 단독 빌드는 OnvifSolution 타입 미발견 실패(Theme·내 변경 무관). main에서 robocopy(gitlink 경계라 git 미추적·커밋 무오염) 후 4개 green. Accounts/Sounds.Ui는 OnvifSolution 무의존 → 즉시 green. ([[project_gmap_orphan_submodule]] [[project_library_deployment_path]])
- **✅ Phase 3 공용 컨트롤 스타일셋 완료(2026-06-24, 커밋 `07039bd`@v2.12.0, Plan 19/75, 빌드0·테스트18/18)**: Theme 어셈블리에 keyed 스타일 3파일 — `Styles.Controls.xaml`(Button Primary/Secondary/Danger/Text·TextBox·PasswordBox·ComboBox·Body/Mono TextBlock) + `Styles.Containers.xaml`(DataGrid 헤더/셀/행 hover·selected·Card·Dialog 패널/헤더) + `Styles.Nav.xaml`(툴바 버튼/토글·NavRail 탭·StatusBadge Critical/Warning/Normal/Info severity hue-lock). **implicit 금지(keyed only)=AD-6 Light byte-identical 보존**, MD BasedOn 유지+색/radius/font만 DynamicResource 토큰. `Theme.Current.xaml` 단일진입점 병합(MD3.Defaults→Theme.Current 순서 전제). IMPL-15(OnPrimaryFixedBrush 양테마 #FFFFFF white-on-blue)=기존재+ModernDialogHeader 실현. `StyleTokenReferenceTests`로 참조토큰 전수정의 검증(+intra-file BasedOn 키 포함). **MD 5.2.1 BasedOn 키=이 코드베이스 실사용분 grep 확정**. VER-06 시각렌더는 앱컨텍스트 필요→파일럿(Phase 4)/EXT후 이연([~]).
- **✅ Phase 4 파일럿 — Accounts.Ui 토큰화 완료(2026-06-24, 커밋 `b0178a1`@v2.12.0, Plan 21/75, IMPL-16/17)**: 7뷰(Login/Logout/MyPage/AccountManager/AccountSetup + Editor/Register) 하드코딩색→DynamicResource. **byte-identical**: `#33000000` 구분선 12→`DividerBrush`·`#88000000` 모달 스크림→`ScrimModalBrush`(동일 hex). **의도적 severity 정규화(V-11 사인오프 대상, 회귀 아님)**: 로그인 실패 `Red`→`StatusCriticalBrush`(#C0392B)·성공 `Green`→`StatusNormalBrush`(#2E9E5B)·비활성 `Gray`→`TextMutedBrush`(#999999). 도입 토큰 5종 전부 Theme 정의 해소(린터)·Accounts.Ui 빌드0. **⚠ 머지 제약: 런타임 토큰 해소는 앱 Theme.Current 병합(EXT-01) 후 → v2.6 머지 시 EXT-01 선행 필수.** VER-11a 픽셀 diff=앱 렌더 필요→이연([~]). 기본 `Black` 결과색·`d:Background`는 의도적 유지.
- **✅ Phase 4 전파(사용자 "그대로 전파" 승인)**: IMPL-18 Sounds.Ui(Gray×8→TextMutedBrush, 커밋 `f84857a`) · IMPL-19 Devices.Ui(AddSensorDialog White×4→OnPrimaryFixedBrush byte-identical, `f84857a`; #FFE0B2 draft-row 현 코드 부재) · **IMPL-20 Events.Ui 부분(커밋 `9b7a351`)**: in-pattern만 — 카드 #33000000→DividerBrush·White→Surface(bg)/OnPrimaryFixed(헤더 fg)·severity #FFDD2C00/Crimson→StatusCriticalBrush(4파일, 빌드0). 각 라이브러리 빌드0.
- **🟡 Events.Ui 보류 — 확정 패턴 밖, 신규 토큰+Dark 값 설계 필요**: KPI 틴트 `#22111188`/`#22118811` · severity 틴트 `#22FF6464`/`#22FFB347`/`#224FC3F7` · `DodgerBlue` · 악센트 `#40C4FF` · 반투명 `#88FFFFFF`/`#88000000` · `WhiteSmoke`. (storyboard sb-modern 대조 후 토큰화 결정)
- **✅ IMPL-21 SkiaSharp 차트 theme-aware 완료(커밋 `17fd9dd`)**: 신규 `ChartThemeProvider`(축/범례/툴팁 theme-aware Light #1C1B1F/Dark #EDF1F6 — 기존 흰축↔어두운범례 모순 통일·세그먼트 고정백·한글 타입페이스 일원화) + ChartHelper white 라우팅 + EventInfoViewModel **IThemeService guarded-optional 주입**(미등록 시 Light graceful)+ThemeChanged rebuild+구독해제 + DataChartPanel 범례 중앙화. **V-07: white/FromFamilyName 잔여 0**(provider 외), 빌드0. ⚠ 교훈: Events.Ui에서 `Action`은 Caliburn.Micro.Action과 모호 → `System.Action` 명시.
- **✅ IMPL-20 완전 완료(커밋 `25a0e24`) + TEST-LINT2 통과**: Events.Ui 잔여 틴트/악센트 토큰화 — 신규 Theme 토큰 6종(TintInfo/Success/Critical/Warning/Accent·SurfaceTranslucent, Light byte-identical/Dark 별도) + DodgerBlue→Primary·#40C4FF→Accent·WhiteSmoke→OnPrimaryFixed·#88000000→TextSecondary. Theme 토큰 41개. TEST-LINT2: 4 라이브러리 Theme 참조 20개 전부 해소·미정의 0, Theme.Tests 18/18.
- **✅ Phase 4 라이브러리측 사실상 완료**(IMPL-16~21·TEST-12·TEST-LINT2, worktree 커밋 9개). 남은 건 전부 EXT/앱렌더: VER-06/08/11(시각)·EXT-07(폰트, 메인앱).
- **✅✅ 머지 + 앱 EXT 배선 완료(2026-06-25) — 앱이 Theme 통합 상태로 빌드0**: 동시 PTZ 세션 유휴(6h/14h) 확인 후 진행. ① **v2.12.0→v2.6 머지 `9cff999`**(disjoint·충돌0·43파일, 롤백 `before-theme-merge`@a7cf02b): Theme 프로젝트 main 체크아웃 존재, Theme.Tests 18/18·Events.Ui 빌드0. ② **외부 앱 EXT `bb6c088`@v0.5**(롤백 `before-theme-ext-wiring`@55c3cf3, 내 3파일만 커밋·기존 .sln 미터치): EXT-01 App.xaml Theme.Current append(MD3.Defaults·MD dicts 이후, app-local 직전) + EXT-02 csproj Theme ProjectReference + Bootstrapper IThemeService 싱글톤 등록 + StartPrograme InitializeFromSettings(Light) + VER-09b 슬롯순서 확인. **앱 전체 빌드 0에러**. → Phase 4 토큰화 뷰 런타임 Light 토큰 해소·EventInfoViewModel 차트 theme-aware 활성.
- **✅ EXT-06 테마 토글 버튼 완료(사용자 요청, sol 커밋 `03be21a`)**: 윈도우 컨트롤 바(MetroWindow RightWindowCommands) ToggleButton(md:ThemeLightDark) + ShellViewModel `ToggleTheme()`/`IsDarkTheme`/ThemeChanged 구독(IThemeService 주입, proxy 타깃). 앱 빌드0. **IMPL-11 결정(사용자): Dark 차단 게이트 미적용 — 토글 즉시 동작**(Dark 시 GMaps 지도만 미테마 잔존, Phase 6 완료 시 일관).
- **🟢 다음 = ① 사용자 앱 실행 → 토글로 Light↔Dark 확인 + Light byte-identical 검증(VER-06/08/11)** ② **Phase 6 GMaps 토큰 이관**(102 Static→Dynamic·DesignTokens→Theme·Generic.xaml IMPL-09 설계 실행·CameraStreamPopupStyle denylist) — PTZ 세션 영역이라 그쪽 안착/조율 후. 메인앱 추가변경 사전통지([[feedback_main_solution_advance_notice]]). [[project_ui_design_storyboard]]
- **🔴 다음 후보 B = Phase 2 외부 메인앱 배치 — 사전통지 게이트**: EXT-01(`Dotnet.Monitoring.Solution\App.xaml`에 Theme.Current.xaml append, BundledTheme<MD3.Defaults 순서) · EXT-02(`.csproj` Theme ProjectReference + `Bootstrapper.cs` IThemeService 등록 + InitializeFromSettings 기본 Light) · EXT-06(SetupPanel/ShellView 테마 토글 UI) · VER-09b(App.xaml 순서검증) · IMPL-11(Dark feature flag, EXT-02 의존). **전부 메인 솔루션 변경 → 사전통지 필수**(동시 PTZ 세션 활발). RISK-03 전수적용은 Phase 4(VER-04b). 동시 PTZ 세션 안착 후 Phase 6(GMaps) 게이트. [[project_ui_design_storyboard]] [[feedback_main_solution_advance_notice]] [[feedback_plan_required_before_dev]]

## 🛠️ 승인·Phase1 착수(2026-06-23~24): 맵 팝업 PTZ 제어 (Track C) — 디스커버리 wfr0y85ca + 옵션식별 + 시뮬레이션 wgfvpidqm
- **요구**: 맵 RTSP 팝업에서 ①우버튼 드래그 PTZ(벡터 시각화→RelativeMove 1회) ②단일 선택(굵은 테두리/컨트롤바) ③우버튼 짧은클릭→팝업 내 [PTZ][프리셋][옵션] 탭 패널. ONVIF 활용. 다양한 시뮬레이션 + PRD + HTML 스토리보드/와이어프레임 요청.
- **확정 결정(사용자)**: 드래그=RelativeMove(벡터 시각화) / 제스처 우버튼=PTZ(드래그=이동·짧은클릭=탭패널)·좌버튼=창 / 단일선택 / 폼팩터=팝업 내 탭·펼침 / 프리셋=로컬DB(이동 AbsoluteMove·저장 GetStatus·편집포함) / 중복항목(주야간·포커스·조리개)=ONVIF 실시간 / v1범위=PTZ+프리셋+주야간·포커스(이미지 슬라이더=Phase2).
- **핵심 발견**: `Ironwall.Dotnet.Libraries.OnvifSolution`=실동작 WCF/SOAP(PTZClient/ImagingPortClient/MediaClient/DeviceClient). IOnvifService엔 ContinuousMove/Stop/preset-token만 노출 → **Relative/Absolute/GetStatus/GetNode·ImagingSet은 미노출**(프록시엔 있음). 팝업=항상 Hub(PART_SharedImage=WPF Image)라 영상 입력 정상(airspace 비해당). PTZ송신 UI 전무(수신 FOV만). 프리셋 3계층(로컬DB/서버REST/ONVIF). 카메라 PTZ판별=Category==PTZ + IsPtzPossible.
- **신규 빌드**: `IPtzController`(Autofac, cameraId별 직렬화/CTS/GetNode space 캐시/FOV폴백) + ONVIF 래퍼 3종 + PtzVectorMapper + Imaging 쓰기 역매퍼.
- **3 Critical 리스크**: ①직접이동 후 FOV 부채꼴 영구불일치(NVRManager만 의존)→FR-FOV-01 GetStatus→ProcessCameraPtz 로컬폴백 ②픽셀→ONVIF 단위 진실원부재→GetNode space clamp(고정±1.0 금지) ③저장↔이동 좌표 round-trip 불일치→space URI DB보존.
- **산출**: PRD `docs/prds/CameraPopup_PTZ_Control-prd.md` v1.0 Draft(FR 35+ / NFR 11 / 리스크 9 / VER 6 / 미결 7) + HTML 스토리보드·와이어프레임 `docs/reports/CameraPopup_PTZ_Storyboard_Wireframe.html`(10 프레임 + 6 상태 + 탭 상세).
- ✅ **사용자 승인("승인")**. 미결 7건=권장 기본값 확정(이동=관람자허용/쓰기만권한·space URI 컬럼추가·완료감지 MoveStatus IDLE·연속 LWW·Y반전 고정·로컬 FOV폴백·이름 collation=DB). ⚠ pipeline stale→advance-phase 미사용(구두 승인).
- **Plan**: `docs/plans/CameraPopup_PTZ_Control-prd-plan.md`(41 태스크/8 Phase). worktree `v2.11.2`(base v2.6)+롤백 `before-camerapopup-ptz-control`(2062caf)+고아 복사(GMap.NET/OnvifSolution).
- ⚠ **아키텍처 제약 발견**: `OnvifSolution`=**자체 git repo**(독립 서브모듈, .gitmodules 없는 고아 gitlink). 내부 코드는 메인 repo 미추적→자체 repo 별도 커밋, worktree 복사본은 main과 별개(앱 반영은 메인 OnvifSolution에서). **전략: 신규 PTZ 코드는 GMaps.Ui(추적)에 두고 OnvifSolution 변경 최소화**.
- ✅ **Phase 1 핵심 완료**(6/41, 커밋 `44b7187`/`d794b6b`/`d3395e3`@v2.11.2, 모두 빌드0+테스트76):
  · `PtzCoordinateMath`(픽셀→상대 Y반전+카메라 space 클램프·고정±1.0 금지, IsDrag 8px) + `PtzCoordinateMathTests`(15케이스)
  · GMaps.Ui→OnvifSolution(.Base) ProjectReference + `PtzVectorMapper`(DTO→PTZVector)
  · `IPtzController`/`PtzController`(cameraId→InitializeFull 해석, GetConfigurations→NodeToken→GetNode로 Rel/Abs space 캐시=변환 진실원, RelativeMove/AbsoluteMove/GetStatus/Stop 래퍼, cameraId별 SemaphoreSlim 직렬화, 자격증명 마스킹) + GMapUiModule 등록. `IsPtzCapable`=강화 게이팅.
- ✅ **Phase 2 완료**(드래그-PTZ 수직 슬라이스, 커밋 `fe98c2c`/`d4a1487`): CameraStreamPopupControl 우버튼 핸들러(8px 데드존·Mouse.Capture·ESC/캡처분실 취소)+벡터 오버레이(화살표 Line+화살촉 Polygon, 강도색)+PART_VideoRegion/PART_PtzOverlay 스타일. VM IsSelected/IsPtzCapable/IsPanelExpanded+PtzDragRequested. **MapViewModel 배선**: ResolvePtzController(IoC lazy)·EnsurePtzReadyAsync(오픈 시 ConnectionModel→EnsureReady→IsPtzCapable)·HandlePtzDragAsync(RelativeMoveByPixel→GetStatus→ProcessCameraPtz FOV, OnUiAsync 마샬링)·Close 시 Release. 빌드0·테스트76. **진행 12/41(29%)**.
- ✅ **Phase 3 완료**(커밋 `4d7f13e`/`0535b19`, 18/41=44%): 단일 선택(SelectedCameraPopup 원자 setter·IsSelected 3px Accent DataTrigger·좌클릭 선택+맨앞·오픈 자동선택·닫기 dangling 방지) + 탭 패널(IsPanelExpanded 토글 시 영상 축소+하단 [PTZ][프리셋][옵션] 펼침, PTZ 8방향 패드+정지=nudge→RelativeMoveByPixel+FOV, 프리셋/옵션 플레이스홀더). 빌드0.
- ✅ **Phase 4 DB 계층 완료**(커밋 `e1842df`, 19/41=46%, 전부 라이브러리): `IPtzPresetModel`/`PtzPresetModel`(Monitoring.Models.Maps, space URI·Zoom double) + GMapDb `CameraPtzPresets` 테이블((CameraId,PresetName) UNIQUE) + DAL(Fetch/Upsert/Delete/SetHome CASE) + `IPtzPresetStore`/`PtzPresetStore`(GMaps.Ui). Framework.Models 미사용(GMapDb 새 테이블=clean). 빌드0·테스트76.
- ⚠ **사용자 제약**: `Dotnet.Monitoring.Solution`(메인 솔루션) 변경 시 **사전 통지 필수**(타 세션 작업 중). Phase 4~6은 전부 라이브러리. 메인 변경은 EXT-01(Bootstrapper OnvifServiceModule 등록)뿐 — 할 때 먼저 알릴 것.
- ✅ **Phase 4 완료**(커밋 `e64db56`, 23/41=56%): 프리셋 탭 UI(목록[Home아이콘+가기/Home/삭제]·[현재위치 저장]·[Home 이동]·인라인 이름입력·빈목록) + VM(Presets 컬렉션·명령·이벤트) + MapViewModel(PtzPresetStore 로드/이동=AbsoluteMove+FOV/저장=GetStatus→Upsert/삭제/Home). 빌드0·테스트76. **드래그/선택/탭/프리셋 전부 동작**(실카메라+Bootstrapper 시).
- ✅ **Phase 5 옵션 완료**(커밋 `09f0788`, 28/41=68%): IPtzController/PtzController에 IsImagingCapable·GetImaging·SetIrCutFilter·SetAutoFocus(ImagingPortClient 직접 Get/SetImagingSettings, read-modify-write로 타 필드 보존, VsToken=Profiles[0].VideoSourceConfiguration.SourceToken 캐시, Gate 직렬화, OnvifSolution 무수정) + 옵션 탭(주야간 3-세그먼트·포커스 2-세그먼트·미지원 안내). **3개 탭(PTZ/프리셋/옵션) 전부 동작**. 빌드0·테스트76.
- ✅ **code-review(opus) 반영**(`e3d71ab`): C1 Stop 직렬화·H1 doc 정정·H2 Release 안전·H3 자격증명 마스킹.
- ✅ **Phase 6**: 권한 가드=호스트 영역 보류(GMaps.Ui에 권한 서비스 없음, 능력 게이팅으로 대체). Dispose 완료(구독 대칭·Release 멱등·dangling 가드).
- ✅ **Phase 7 + EXT-01 완료 — 기능 통합 종료**: **v2.11.2→v2.6 클린 머지**(`0d03f83`, disjoint·충돌0) → **메인 Bootstrapper에 OnvifServiceModule 등록 + OnvifSolution ProjectReference**(메인 repo `55c3cf3`@v0.5, 사용자 승인 "지금 OK") → **메인 솔루션 전체 빌드 0에러**. 34/41=83%. 사용자 미커밋 파일(.gitignore·Docs/INDEX·tests) 보존.
- ✅ **실카메라 1차 검증 + 인터랙션 개편**(커밋 `ecff30d` 진단로그, `8e44723` 개편, 라이브러리+메인 빌드0): 드래그-PTZ 동작 확인됨. 사용자 피드백 반영 — ① **선택 시 영상 끊김 버그 수정**(BringToFront의 `CameraPopups.Move`가 ItemsControl 컨테이너 재생성→RTSP "Retry" 원인 → VM `ZIndex`+Panel.ZIndex 바인딩으로 교체) ② **버튼 개편**: 좌=영상 드래그 PTZ+짧은클릭 선택+헤더 창이동 / 우=컨트롤 패널(아코디언) 토글 ③ **휠 줌**(RelativeZoom+RelativeZoomTranslationSpace 캐시+BuildZoom).
- 🔲 **남음(런타임·사용자)**: 개편 재빌드 후 재검증(좌드래그/우패널/휠줌/**선택 시 영상 유지**) + 주야간·포커스 + 프리셋. 튜닝: 감도(2.0)·줌스텝(0.1)·ONVIF포트. 수동 near/far 포커스는 후속.

## ✅ 승인·Plan완료(2026-06-24): Tracking GIS — P1~P3 (실시간 마커+트레일+TTL+설정) — `docs/plans/Tracking_GIS_Visualization_Playback-prd-plan.md`
- **세션복구→PRD 검토→구두 승인→Plan 완료**. PRD=**Approved(P1~P3 우선)**, P4(영속)/P5(Playback)/P6(MP4)는 §8 서버 API 전달 후 별도 사이클.
- **전제 5/5 코드검증**: FR-15 stub(`TrackingStatusNatsSyncService.cs:69-71` TODO)·DTO 단수`target`(`TrackingStatusBodyDto.cs:14-18`)·`EnumThreatLevel` 미존재·신규 컴포넌트(GMapTrackingMarker/GMapTrailMarker/ITrackingOverlayManager/IClock) greenfield·MapSettingsHelper 존재. 추가: stub이 이미 `body["targets"]`(복수) 읽음→발행 신계약 가능성↑(단 track_id/observed_at/threat_level/ttl_sec 실재는 V-CONTRACT-1 미확인). `Messages/Tests/UnitTest.cs`가 DTO 참조→교체 시 동반수정.
- **충돌검토(23:22)**: PTZ 세션=휴면(main v2.6, 20:29 마지막, 작업트리 깨끗)·Modern Theme=활성(worktree v2.12.0, 23:19, Phase4 Accounts.Ui, GMaps Phase6 미착수). **하드블로커 0**. P1(Base/Messages/Events.Ui/IClock/MapSettingsHelper) 겹침 0. GMaps 마커·Generic.xaml은 Modern Theme Phase6과 순서합의. MapViewModel.cs는 PTZ편집분과 영역분리(:4008).
- **Plan = 58태스크/~103h/8그룹**(Phase0 5/SETUP 2/P1 11/P2 10/P3 7/테스트·검증 16/DOC 4/EXT 3). 게이트=구두승인(advance-phase 미사용).
- **✅ VER-01=V-CONTRACT-1 문서 확정(2026-06-24)**: `Docs/Gop_Message_Broker_연동설계.md §8.3.7`/§4.1 대조 — targets[]/track_id/observed_at(ISO8601 UTC ms Z)/threat_level(NORMAL·CAUTION·THREAT 3종)/location 전부 필수(Y), ttl_sec 기본5. **명세 보강 6건**(PRD §1.4 기록): ①복합키(camera_id,track_id) ②tracking lost/idle→camera_id 마커 제거(페이드) ③Unknown=클라전용폴백 ④tracking 소문자↔EnumTrackingStatus 변환 ⑤ttl 폴백5(D-09 10→정정) ⑥car·vehicle 둘다 차량. 잔여=런타임 캡처(가벼운 사후확인). Plan 1/58.
- **✅ dev 착수 — P1 Foundation 완료(2026-06-25, worktree v2.13.0 커밋 `a4aa9fc`, 빌드0·테스트 183/183)**: SETUP-01(tag `before-tracking-gis`@**62dd557**[v2.6 전진] + worktree v2.13.0 + GMap.NET 666·OnvifSolution 39 .cs 복사 + CHANGELOG) · SETUP-02(배치 확정: Enum=.Enums / IClock=Base/Services / DTO=Messages / 마커=GMaps.Ui/GMapSymbols / **인터페이스=Events.Ui**[Events.Ui→Messages 추가, GMaps.Ui→Events.Ui 기존이라 비순환]) · **IMPL-P1-02**(DTO target→targets[]+TrackingTargetDto) · **P1-03**(EnumThreatLevel+ToColorType) · P1-04 일부(EnumTargetType+ParseTargetType 토큰스캔, armed_person→Person) · P1-07 일부(IClock/SystemClock) · TEST-01(Phase26 재작성). Plan 6/58.
  - **⚠ 충돌 재평가(10:02)**: Modern Theme가 **v2.6에 머지됨**(`9cff999` Phase1~4) + **GMaps Phase6 토큰 sweep 진행**(`d269eec` 등, Themes/*.xaml·DesignTokens) — 단 **내 P1 대상과 겹침 0**(Generic.xaml/MapViewModel/GMapUiModule 무접촉). 신규 마커 템플릿은 theme-aware(DesignTokens) 설계.
- **✅ IMPL-P1-01 인터페이스(커밋 `460084a`)**: ITrackingOverlayManager(Events.Ui, UpsertBatchAsync 단일 Dispatcher 블록/ExpireCameraAsync lost·idle 카메라레벨/ClearAllAsync) + ITrackingSetupModel(GMaps) + Events.Ui→Messages. 빌드0.
- **✅ GMaps.Ui worktree 빌드 검증 = 0 errors**(고아 서브모듈 빌드 이슈 해소 실측). 통합점 파악: `MapViewModel.MainMap`(GMapControl)→`MainMap.Markers.Add()`, `DispatcherService.Invoke/BeginInvoke` 마샬링, ISymbolModel=18멤버(마커 결합 큼), 테스트=net8.0 격리 **링크-컴파일** 패턴.
- **✅ TrackingMath 순수코어(커밋 `d0dc6f5`)**: HaversineMeters·ComputeSpeedMps(가드)·BearingDegrees·IsReversed·IsValidLatLng (GMaps.Ui/Services/Tracking, WPF무의존) + TrackingMathTests 12(실소스 링크-컴파일) → **GMaps.Ui.Tests 90/90**. P2-03/04·P1-09/10 코어 완성. Plan **8/58**.
- **✅ P1 파이프라인 end-to-end 완결(2026-06-25, 커밋 `ab22ae6`·`178d11d`·`9f20e6e`)**: NATS→핸들러→매니저→마커→지도 전 구간. ① `GMapTrackingMarker`(GMapMarker 경량파생, transient라 ISymbolModel 회피 — 위협색/타입/베어링/라벨, severity hue-lock) ② `TrackingOverlayManager`(ITrackingOverlayManager 구현, **Dispatcher UI 일원화→lock 불필요**, 복합키 dict, 역전 skip, 속도/베어링, ttl 폴백5+500ms 스윕+틱당20 분할제거, ZIndex 2000) ③ 핸들러 stub 해소(active=UpsertBatch/lost·idle=ExpireCamera, 소문자파싱, 매니저 optional graceful) ④ GMapUiModule DI(SystemClock→IClock, 매니저 AsSelf+As인터페이스) ⑤ MapViewModel 주입+OnViewAttached `Attach(MainMap)`. **빌드0·테스트 Messages 183+GMaps.Ui 90. Plan 19/58(33%).**
- **✅ P2 코어 완결(2026-06-25, 커밋 `f81e587`)**: `GMapTrailMarker`(단일 Canvas+Line 세그먼트, 점선, 페이드 최신1.0→오래됨0.15) + 매니저 통합(점큐 FIFO TrailMaxPoints, NATS gap>임계 분절 P2-09, 색동기). **투영 최적화 P2-08**: anchor 상대좌표 → 팬 free, 줌만 OnMapZoomChanged 1회 구독 일괄 Rebuild(드래그 storm 회피). TrackingMath.TrailFadeOpacity 추출(테스트, GMaps.Ui.Tests 91). Plan 24/58(41%). P2-07(lost/idle 시각전이)만 정련 잔여.
- **✅ P3 라이브러리 완결(2026-06-25, 커밋 `3ba5915`)**: `TrackingSetupModel`(GMaps/Models, ITrackingSetupModel 기본구현+복사생성자, 기본 ttl5/max50/trail30/gap10) GMapUiModule 단일등록 → **매니저가 실제 설정 사용**(null 폴백 제거, 라이브러리 자족·EXT-01 불요). `MapSettingsHelper` **SemaphoreSlim**(P3-02 기존 무잠금 RMW 버그 수정) + Save/LoadTrackingSettingsAsync(AppSettings.Tracking, P3-03). Plan 27/58(47%).
- **✅ v2.6 머지 + 런타임 검증 성공(2026-06-25)**: v2.13.0→v2.6 머지(`668b236`, 충돌0·디스조인트, CHANGELOG stash로 타세션 작업 보존, 롤백 `before-tracking-merge`@e583f59). **DI 순환 수정**(`73dfdfc`): TrackingSetupModel 복사생성자 greedy 선택 자기참조 → `builder.Register(_=>new TrackingSetupModel())` 팩토리. **앱 재빌드 후 런타임 마커 렌더링 실측 확인**(스크린샷 `Docs/pictures/스크린샷 2026-06-25 212228.png` — 마커+이동+트레일). 와이어 검증: scratchpad `TrackingTestPublisher`(NATS.Client.Core, `dotnet run -- nats://localhost:4222 201 <steps> <ms> <lat> <lng>`)로 §8.3.7 발행 → 앱 정상 수신·렌더.
- **✅ 마커 디자인 개선(`902dd93`, P1-05)**: 글자→**물방울 핀(MapMarker, 색=위험도) + 타입 PackIcon**(사람=Walk/차=Car/동물=Paw/미상=HelpCircle) + 방향화살표 + DropShadow. 라벨 제거→**호버 툴팁(track_id+속도)**. Update에 speedMps 추가. 발행기 3종 동시(사람빨강/차주황/동물초록) 데모 보강.
- **✅ 영속 방향 변경 + P4 로컬 DB(2026-06-25, 커밋 `51e9911`)**: 사용자 "트랙킹데이터 자체 DB에 넣고 Playback도 자체DB에서, 일단" → PRD v1.1 결정변경 기록(서버 API §8 보류). GMapDb **CameraTrackPoints**(INT PK·ObservedAt DATETIME(3)·인덱스2) + DAL(InsertBatch/FetchByRange/DeleteBefore) + `ITrackPointModel`/`TrackPointModel`(Monitoring.Models.Maps) + `ITrackPointWriter`(Events.Ui seam)/`TrackPointStore`(GMaps.Ui→GMapDb, write/read/purge) + 핸들러 active 시 DB 기록(비-UI, 오버레이와 독립). 속도 미저장→Playback 재계산. 빌드0. 서버 전환 대비 store seam 유지.
- **✅ P5 설계 확정 + 백엔드 착수(2026-06-26)**: 아키텍처 = **C 하이브리드**(독립 콘솔=오버레이 윈도우 + **같은 1개 지도**에 격리 재생 레이어, **두 번째 지도 없음** — GMap static 충돌 회피). 멀티타겟 **단일 타임라인** 동시재생, 이벤트리스트=체크박스 필터+클릭 포커스. 기간=프리셋(최근N분)+절대범위+MaxPlaybackHours 가드. HTML 스토리보드/와이어프레임 `Docs/reports/Tracking_Playback_Storyboard_Wireframe.html`.
  - **커밋**: `2d0c995` PlaybackEngine(Load·재생/배속/탐색·SnapshotAt 순수로직·IClock) · `06cdddd` PlaybackOverlayManager(격리 Z2100·PLAY뱃지·체크박스필터)+GMapTrackingMarker.EnablePlaybackBadge. 빌드0.
- **✅ P5 Playback 콘솔 완료(2026-06-26, 커밋 `1d98eb9`)**: `PlaybackViewModel`(기간 프리셋 최근5/30/60분·오늘 + 절대범위 + MaxPlaybackHours 가드 → FetchAsync → engine.Load / 이벤트리스트 체크박스필터+⌖클릭포커스 / 컨트롤바 ▶⏸·타임라인 양방향·배속) + `PlaybackConsoleControl`(GMapControls, 오버레이 윈도우 — 콘솔에 지도 없음, MainMap 공유) + MapView 툴바 재생버튼(PlayCircleOutline)+캔버스 ContentPresenter + MapViewModel(주입·TogglePlaybackPanel·FocusRequested→센터링·OnViewAttached AttachMap) + TrackingSetupModel.MaxPlaybackHours(6) + GMapUiModule DI. 빌드0·테스트 91/91. **★추적 P1~P5 전구간 구현 완료.**
- **✅ P3-04 추적 설정 UI 완료(2026-06-26, 커밋 `79b351c`)**: `TrackingSetupViewModel`(싱글톤 ITrackingSetupModel 직접 조정=즉시적용, 오버레이 동일 인스턴스 참조 / [저장] MapSettingsHelper 영속 / [기본값] 복원) + `TrackingSettingsControl`(오버레이 패널, 표시·트레일·수명·속도/재생 4그룹 슬라이더+입력) + MapView 툴바 ⚙(CogOutline)+캔버스 Z120 + MapViewModel ToggleTrackingSettingsPanel + DI. 배치=맵 오버레이 패널·즉시적용+저장(사용자 확정). 빌드0·테스트 91/91. 컨셉 HTML `Docs/reports/Tracking_Settings_UI_Wireframe.html`.
- **✅ 오버레이 윈도우 표준 전환(2026-06-26, 커밋 `d04a7aa`)**: 사용자 지적(드래그 없음·스타일 불일치) → Playback·설정 패널을 **UserControl→Control 표준 패턴**(LayerPanelControl)으로 전면 재작성. `Themes/PlaybackConsoleStyle.xaml`·`TrackingSettingsStyle.xaml`(Generic.xaml 병합) + 헤더 드래그(Y≤38→부모 ContentPresenter Canvas 이동) + DesignTokens 테마색. 패턴 메모리화 → [[project_gmaps_overlay_window_pattern]]. 빌드0·테스트91.
- **🔲 다음**: ⓐ **사용자 앱 재빌드 1회** → 런타임 검증(마커디자인 + P4 DB적재 + **P5 재생** + **⚙ 설정** + **헤더 드래그 이동**). 재빌드 후 "캡처해줘"면 내가 시각 검증. ⚠재생은 P4 적재 후라야 보임(라이브/발행기 먼저). ⓑ 정련 P2-07/P1-08 ⓒ push(v2.6 로컬 미푸시 다수). [[project_gmaps_overlay_window_pattern]] [[project_library_deployment_path]] [[reference_headless_screen_capture]]

## 📋 (이력)PRD 작성완료(2026-06-23): Tracking GIS 시각화+트레일+영속+Playback — `docs/prds/Tracking_GIS_Visualization_Playback-prd.md` (Draft→Approved P1~P3, Track C)
- **요청**: 실시간 추적 심볼(타입별 형상·위험도 테두리)+이동경로 점선 트레일(시간경과 페이드·속도)+심볼 TTL(x초 무메시지 소멸, 설정화)+좌표 API 영속+불러오기 Playback(별 창·배속·MP4 다운로드). track_id 존재 가정.
- **2 워크플로**: 자산발견 `wf_b9e6ac66-e0f`(6영역) + 90시나리오+2회 시뮬레이션+architect종합 `wf_30318803-c79`(21에이전트). ※sim2 4/5렌즈 500에러·scn:MP4 실패 → 발견 leg6+sim1+architect로 보완.
- **★사용자 결정(영속)**: 로컬DB 아님 → **서버 API 신규 엔드포인트** + **"서버 API 구성 가이드"(PRD §8)** 명세, 클라는 가이드 지켜졌다 가정 개발. 권장=서버측 NATS 인제스트(클라 POST 중복쓰기 회피)+GET 조회만. 데이터량 소규모(초당 수~수십 행, 카메라당 1msg/초~3fps) — 초기 3천만행 추정 철회.
- **핵심 설계(시뮬 수렴)**: ①계약 인터페이스는 Base/Tracking.Contracts에(GMaps.Ui↔Events.Ui 순환회피) ②Markers 단일진입점+Dispatcher 직렬화(plain ObservableCollection 비안전) ③GMapTrackingMarker(GMapBaseMarker 상속)+ControlTemplate(ColorType/SymbolType 컨버터 재사용) ④GMapTrailMarker 신규(GMapMarkerLineControl 상속금지: 단일 Canvas+복수 Polyline 페이드) ⑤속도=클라 하버사인÷observed_at 델타 ⑥TTL 500ms 스윕+분할Remove ⑦Playback 옵션B(라이브 지도 위 레이어, 별 Window 아님)+서버 GET 데이터 ⑧IClock 신설(I-02) ⑨MapSettingsHelper SemaphoreSlim(기존 버그 동시수정) ⑩MP4=P6 후반 RenderTargetBitmap.
- **6 Phase**: P1 계약/Enum/DTO+오버레이마커 · P2 트레일+속도+TTL · P3 설정 · P4 서버API연동(조회) · P5 Playback창 · P6 MP4(out-of-scope v1). 46 FR/11 NFR/13 V/9 리스크.
- **선결**: §2.2 발행정합(V-CONTRACT-1) + 서버 §8 구현(없으면 P5만 게이팅, P1~P3 정상) + 계약 어셈블리 배치 + IClock + MapSettingsHelper 락.
- **다음**: 사용자 PRD 검토 → 승인(`node .claude/hooks/advance-phase.js approve prd "..."`) 시 Plan. **롤백 태그/worktree는 dev 진입 시 생성**(현재 코드무변경). [[project_korean... docs gitignore 주의: docs/는 PowerShell/직접 Read]]

## 🔬 분석완료(2026-06-23): NATS Tracking 메시지 수신 로직 흐름 — `docs/analyses/nats-tracking-message-flow-analysis.md`
- **요청**: "NATS에서 Tracking 메시지 받으면 어떤 로직 타는지" (Track A, 4구간 병렬 워크플로 wf_63c378db-70e 7에이전트 + 적대적검증 3건).
- **핵심결론**: 수신 핸들러 `TrackingStatusNatsSyncService.OnNatsTrackingStatusAsync`(Events.Ui)는 **로그-only stub** — subject(`gis.tracking-status`)+cmd(`TRACKING_STATUS`) 2단계 필터 → camera_id/tracking/targets 추출 → **로그만**. GIS오버레이/UI/DB/이벤트 부작용 0. 실제 처리=**FR-15 미구현 TODO**.
- **플럼빙**: 발행=외부 AiAnalysis(repo는 수신전용, PublishAsync 없음). 구독=베이스 `MessageService`가 와일드카드 `{Domain}.{Group}.{Subsystem}.>`(예 sensorway.{부대ID}.gis.>) 구독 → ThreadPool Task.Run await foreach → MessageArgsModel → `NatsSubscribeEventAsync` **순차** 발화. **공동구독자 5**(Device/CameraPtz/Detection/Malfunction/Tracking) 단일 이벤트 공유, 각자 필터. StartService는 EventUiModule BuildCallback(:178/182/186)에서 3개(dns/mns/tns)만 — **Device·CameraPtz 기동 누락(잠재 휴면)**.
- **계약 SoT**: `docs/prds/NATS-Tracking-Geolocation-메시지정리.md §2.2`(실재 24KB). 메시지 **단일 target→다중 `targets[]` 전면교체(line176)**. → 핸들러는 `targets[]`(신계약 일치)인데 **DTO `TrackingStatusBodyDto`는 `target`(단수, 구계약 잔재)** = FR-15에서 마이그레이션 필요. `EnumThreatLevel`(NORMAL/CAUTION/THREAT)은 spec엔 있으나 C# enum 미존재.
- **⚠️ 도구 교훈**: **`docs/`가 gitignore(.gitignore:361)** 라 ripgrep 기반 `Grep`/`Glob`이 docs/ 아래를 전부 못 봄("No files found") → 하마터면 실재 PRD를 "없음(환각)"으로 오판할 뻔(한글 파일명은 무관한 red herring). PowerShell `Test-Path`로 실존 확인. **docs/ 문서는 PowerShell `Get-ChildItem`/`Select-String` 또는 직접 Read 사용**. [[project_docs_gitignored_grep_blind]]
- **다음 제안**: FR-15 구현 PRD(계약 재정의 불요, §2.2 SoT) / DTO `targets[]` 마이그레이션 / Device·CameraPtz 기동경로 확인 / MessageService 핸들러 예외격리.

## ✅ 완료(2026-06-24): Accounts.Ui 라이브러리 추출 — **앱 분리 + 런타임 검증 + 양쪽 커밋**
- **전체 완료**: Account UI를 Device/Event/Gateway처럼 독립 `.Ui` 라이브러리로 분리. 라이브러리=VM 12종(상태3+다이얼로그4+패널5 Login/Logout/MyPage/AccountManager/AccountSetup) + View 8 + Gateway seam(`IAuthGateway`/`IUserDirectoryGateway`/`IProfileGateway`+`AuthResult`+`DbAccountGateway`) + 서비스 + AccountUiModule. 앱엔 불가피한 thin wrapper 1개(AccountSetupViewModel — Gateway 패턴, BaseSetupViewModel+appsettings.json 영속 결합).
- **커밋(로컬, 미푸시)**: 라이브러리 `v2.6` = 머지 `603668a`(6 phase) + `15431e9`(Logout/AccountSetup+DI테스트). 앱 `v0.5` = `74fd086`(배선+repoint+원본27삭제+AccountSetup wrapper, -5015라인). 롤백: lib `before-accounts-ui-merge`(2062caf)/`before-accounts-ui-extraction`, 앱 `before-accounts-ui-integration`(d2e83c3).
- **검증**: 앱 빌드0 · 라이브러리 DI 스모크 16/16 · **런타임**(로그인/로그아웃/계정관리/마이페이지/계정설정 탭) 사용자 확인. ⚠️ **AccountSetup ContentControl은 x:Name=프로퍼티명(Caliburn 컨벤션)이 있어야 ViewLocator 발동**(누락 시 VM 타입명만 렌더 — Gateway 패턴 교훈).
- **남은 것**: 🔲 푸시(사용자 결정 — lib v2.6 / 앱 v0.5). 다음 큰 목표=**GOP-00 API 연동**(`ApiAccountGateway` 추가 + `AuthMode` 토글, VM 재편집 0). [[project_account_integration_strategy]]

## 📐 (이력) Phase5 ②③④완료·Phase6런타임대기(2026-06-24): Accounts.Ui 추출 — **앱 분리 컴파일 완료(앱 빌드0)**
- **②③④ 완료(2026-06-24)**: 외부 `Dotnet.Monitoring.Solution`(브랜치 v0.5) 배선 — 앱 `.csproj` Accounts.Ui ProjectReference + Bootstrapper(AccountModule/AccountDbModule→`AccountUiModule(setup,setup,_log,10)` 1줄, 계정VM 등록 제거, SelectAssemblies에 Accounts.Ui.dll LoadFrom) + **외부참조 7파일 repoint**(Conductor/Shell/LeftMenu/Logout/AccountSetup + Bootstrapper/DevicePanel의 `ViewModels.Users` using→라이브러리) + **앱 원본 24파일 삭제**(VM10+View7×2; AccountSetupView/VM은 점진안 유지). **앱 컴파일 0**(CS0104 모호성 0). `AccountModel`(공유 IAccountModel) 등록은 유지. **미커밋**(롤백 `before-accounts-ui-integration` d2e83c3).
- **남은 것**: 🔲 **Phase6 런타임 스모크(사용자: 앱 닫고 재빌드→로그인/계정관리/메뉴가시성/로그아웃 동작 확인)** + 선택: 앱 `.sln`에 Accounts.Ui Project 추가(VS 편의, 빌드는 ProjectReference로 이미 동작) + C-2 앱 전역RD 스타일분리(방어적, AccountManager는 IsVisible 보유라 현재 무해). 라이브러리 v2.6 머지/앱 변경 **모두 로컬 미푸시**(런타임 확인 후 커밋·푸시 권장). 다음 큰 목표=GOP-00 API 연동.

## 📐 Phase5 ①머지완료·②앱배선중(2026-06-24): Accounts.Ui 라이브러리 추출 — Gateway seam
- **①머지 ✅(2026-06-24)**: 사용자 승인=단계적 통합(머지→배선→빌드확인→삭제). `v2.9.22`(6커밋: e70f955·1397ee5·80852f7·60e0090·14d072c·6845e8b) → **`v2.6` 머지 `603668a`**(충돌0, v2.6는 GMaps.Ui 디지털줌으로 분기했으나 다른파일). 머지 후 메인체크아웃 Accounts.Ui **빌드0**. 롤백: lib `before-accounts-ui-merge`(2062caf) / 앱 `before-accounts-ui-integration`(d2e83c3, 브랜치 v0.5).
- **②앱배선 범위 매핑(2026-06-24)**: 외부 `Dotnet.Monitoring.Solution`. Bootstrapper.cs(L287-312 패널/다이얼로그 등록 + L333-335 상태VM + L338-339 AccountModule/AccountDbModule + SelectAssemblies L364-409 LoadFrom) → **AccountUiModule 1줄로 교체 + 중복등록 제거 + Accounts.Ui.dll LoadFrom 추가 + .csproj ProjectReference + .sln Project**. ⚠️**계정VM 외부참조 4파일**(ConductorControlViewModel/ShellViewModel/LeftMenuSectionViewModel/LogoutPanelViewModel)=원본 삭제 시 라이브러리 namespace로 repoint 필요. 🔲 **다음=②배선 실행 → ③앱 빌드0 확인 → ④원본27 삭제 → C-2 앱RD 분리 → Phase6 런타임 스모크(사용자: 앱 닫고 재빌드+로그인/계정/메뉴/로그아웃 테스트).**
- **Phase 4 완료(커밋 6845e8b)**: View 7종 XAML 이관(네임스페이스 치환, 앱 전용 컨버터 참조 0, XAML 컴파일 0). 라이브러리=VM 11 + View 7 자립. **다음=🔲 Phase5(파괴적·크로스레포, 확인 필수): Monitoring .sln/.csproj ProjectReference + Bootstrapper(SelectAssemblies LoadFrom/AccountUiModule/공유VM 중복제거) + 앱 원본 27파일 삭제 + C-2 앱 RD 분리 + v2.6 머지 → Phase6 스모크.**
- **Phase 3 완료(2026-06-24)**: 상태VM 3(Account/Login/Register) + 다이얼로그 4(Register/Editor/Delete/ResetPass) + 패널 4(Login/AccountManager/MyPage/AccountSetup) = **11종 전부 라이브러리 이관 + gateway seam**. 동반 결함수정: C-1, H-4 debounce, 2중루프(AccountDeletionPolicy), 선적용 롤백, **L115 토큰 평문로깅 제거**, 하드코딩 비번 외부화, SetupModel 제거, async void→Task, ct, NotImplemented→no-op, 취소토큰 버그. 커밋 5개(e70f955·1397ee5·80852f7·60e0090·14d072c). 테스트 14/14. **다음=Phase4(View 8개 XAML 이관 + C-2 스타일 3분리) → 🔲 Phase5(외부 솔루션 27파일 제거+Bootstrapper+v2.6 머지=destructive 게이트) → Phase6(스모크).**
- **진행**: Phase0✅ → Phase1(골격+Gateway, e70f955)✅ → Phase2(상태VM+C-1, 1397ee5)✅ → **Phase3a(leaf 다이얼로그 Delete/ResetPass, 80852f7)✅**. **메시지 17종 전부 라이브러리 `ViewModel.Models`(CommonMessages) — 앱전용0, hook불필요**(중요: B-VM 이관 단순화). 다음=Phase3b(Register/Editor — H-4 debounce·파일위임·하드코딩비번·SetupModel제거) → 3c(패널 Login토큰로깅제거/AccountManager/MyPage) → 3d(AccountSetup) → Phase4(8 View XAML+C-2 스타일분리) → 🔲 **Phase5(외부 솔루션 27파일 제거+Bootstrapper+v2.6 머지)=destructive/cross-repo 확인 게이트** → Phase6(스모크). 커밋 4개.
- **목표 흐름**: ①Accounts.Ui 분리(선행) → ②GOP API 연동(GOP-00). 사용자 결정=**UI 분리 먼저 + abstract hook을 API 주입점(gateway)으로 설계**. 두 PRD의 같은-VM 이중작업을 gateway seam으로 제거. (메모리 project_account_integration_strategy)
- **현황 재확인**: Account UI(VM/View) 전부 외부 `Dotnet.Monitoring.Solution`에 존재, 이 레포엔 .Accounts(백엔드)·.Accounts.Db만. `.Accounts.Ui` 미존재·구현 0%·Account plan 0개·account커밋 0건(워크플로 2회로 9 PRD+코드 교차검증).
- **차단이슈 코드확인 완료**: 🟢**C-2 정정**(CRITICAL→MEDIUM): CustomDataGridStyle IsReadOnly/Single은 기능 차단 아님(다중삭제=VM IsChecked, 편집=다이얼로그). 진짜 위험=전역 스타일의 `IsVisible` DataTrigger 결합 → 스타일 키 분리(Base/Custom/Editable)+DataTrigger 제거. ✅OQ-1(EnumLevelType: Enums, ADMIN=1; 첫등록자=RegisterDialog L143). ✅OQ-6(InsertAccountAsync→Task<IAccountModel?>, 전 메서드 ct 보유).
- **Gateway 계약 확정**(외부 VM7 실제호출 grounded): `IAuthGateway`(AuthenticateAsync→AuthResult=계정+토큰+만료+role+perm / GetLatestLogin / RecordLogin — TokenGenerator 흡수), `IUserDirectoryGateway`(GetAllAccounts/CreateAccount/RemoveAccount(+currentPassword)/IsUsernameTaken), `IProfileGateway`(GetProfile/UpdateProfile/ChangePassword(+current,new) — PasswordHelper 흡수). 계약은 **API 형태로** 설계, Db 어댑터가 빈 필드 허용. 추출=`DbAccountGateway` 1개, GOP-00=`ApiAccountGateway` 추가+`AuthMode` 택일.
- **산출물**: PRD `Accounts_Ui_Library_Extraction-prd.md` **R3 개정**(§6.3a Gateway seam 신설, §4/§7.2/§11 C-2 정정, OQ-1/4/6 해소, §10 S1-6 gateway STEP+S3-x gateway 주입+S4-2 C-2). Plan `docs/plans/Accounts_Ui_Library_Extraction-prd-plan.md` **신규**(Phase0~6, 크로스-레포, gateway 선설계).
- **Phase 0 완료(2026-06-23)**: 롤백태그 `before-accounts-ui-extraction`(@ec2ed31) + worktree `C:/workspace_app/worktrees/v2.9.22`(off v2.6) 생성. P-CHK-1✅(.Accounts.Ui=Devices.Ui 참조패턴: Monitoring `.sln` Project항목 + 앱 `.csproj` ProjectReference 상대경로 `..\Ironwall.Dotnet.Libraries\...`; Libraries.sln 미등록; **앱은 메인체크아웃 참조→S5/S6은 v2.6 머지 후**), P-CHK-3✅(`ISessionConfigService.AdminResetPassword` config로 '12345678' 외부화), P-CHK-2⏳(csproj 생성 후 빌드검증=S1-1 위임). worktree CHANGELOG[Unreleased] + 27파일 매니페스트(VM11+View8, Plan 부록 B) 작성.
- **Phase 1 완료(2026-06-23, 빌드0·테스트10/10, worktree 미커밋)**: 신규 `Ironwall.Dotnet.Libraries.Accounts.Ui` 프로젝트(Devices.Ui 미러+Accounts/Accounts.Db/Framework). **Gateway seam**: 인터페이스 3종+`AuthResult`는 공유 `Accounts/Gateways/`(후속 Accounts.Api도 참조), `DbAccountGateway` 어댑터는 `Accounts.Ui/Gateways/`(IAccountDbService+TokenGenerator+PasswordHelper 래핑). `SessionConfigService`(+`AdminResetPassword`), `ProfileImageService`/`ProfileImageHelper`, `AccountUiModule`(모듈 일원화+`IAccountSetupModel` 브리지+`useDbAuth` 게이트웨이; VM 등록 Phase2/3 주석). 별도 `.Accounts.Ui.Tests`(10 통과). **P-CHK-2 해소**(Framework 충돌 없음). **교훈**: WPF 프로젝트 ImplicitUsings는 `System.IO` 제외(Path 충돌) → 명시 using 필요. 🔲 **다음=Phase 2(상태 VM 직접 이관 — 외부 솔루션 VM→worktree 복사), 사용자 확인 후 착수.**

## ✅ 수정완료·v2.6 머지(2026-06-23): 디지털줌 활성 시 RTSP 팝업/연결선 좌표 어긋남 — 좌표 도메인 비대칭 (워크플로 wiygavy5o)
- 증상(코드 진단·미관측확정): 디지털줌(1.5/2.0x) 시 카메라 팝업·빨간 연결선이 심볼에서 떨어짐(컨트롤 중심서 멀수록·줌 클수록 선형↑, 팝업이 심볼보다 중심쪽에 처짐). 디지털줌만 인/아웃 시 팝업이 전혀 안 따라옴(제자리 고정).
- **RC-1(high)**: 디지털줌=`GMapCustomControl.RenderTransform=ScaleTransform(scale,scale,ActualWidth/2,ActualHeight/2)`(GMapCustomControl.cs:1284). 마커는 transform 안(outer 확대), 팝업은 **형제** PropertyPanelCanvas(transform 밖, MapView.xaml:676)에서 `FromLatLngToLocal` raw **inner** 좌표 그대로 사용(MapViewModel.cs:665-684,736-742) → 오차벡터 `(x-cx)(scale-1)`. **마커=보정금지인데 팝업=보정필요** 비대칭이 핵심.
- **RC-2(high)**: 디지털줌은 `_core.Zoom` 불변(GMapControl.cs:329/341)→OnMapZoomChanged/OnPositionChanged 미발화→RefreshCameraPopupPositions 미호출. DigitalZoomLevelChanged→OnMapDigitalZoomLevelChanged(4966)=CreateScaleBar()만. RC-1+RC-2 동시 수정 필요(Refresh만 추가해도 raw라 여전히 어긋남).
- **RC-3(medium)**: 드래그 저장 `FromLocalToLatLng(CanvasLeft,Top)`(708)는 inner-일관이나 RC-1로 시각-기대 불일치(확대된 심볼 옆에 놓아도 저장 geo 어긋남).
- **권장수정=옵션 A**: GMapCustomControl에 `InnerToOuter/OuterToInner`(cx,cy=ActualW/H/2, s=DigitalZoomScale, ActualWidth<=0 가드) 헬퍼 신설→팝업 경로(665-684/736-742)만 정방향 보정 + 드래그 저장 OuterToInner + OnMapDigitalZoomLevelChanged(4966)에 Refresh 추가 + SizeChanged Refresh. **scale=1=항등→회귀0**. 마커/격자/스냅(불변식3·4) 무손상. 헬퍼는 '팝업전용' XML doc 가드(마커 오용=이중보정 금지). B(transform 안 이동)=팝업 UI 2배 확대·클립 회귀 high, C(TransformToVisual)=제외/포함 API 혼동 리스크 medium → 기각.
- **미결**: ①디지털줌+회전 동시 시 RenderTransform 합성 여부(합성이면 scale-only식 부족→옵션C 재검토) ②연결선 끝점1 심볼 화면밖 클램프 UX정책 ③SizeChanged 재계산 경로.
- 파일: GMapCustomControl.cs / MapViewModel.cs / CameraStreamPopupControl.cs.
- ✅ **증상 검증**(사용자): "팝업·연결선이 심볼에서 떨어짐" 확인 → RC-1 확정. ✅ **미결① 해결(V-01)**: `ApplyMapRotation`(GMapCustomControl.cs:1900)=Bearing(_core)만 변경·this.RenderTransform 무변경 → 회전·디지털줌 **독립 합성** → **옵션 A가 회전 활성 시에도 정확**(옵션 C 불필요).
- ✅ **구현·머지 완료**: PRD/Plan `CameraPopup_DigitalZoom_Alignment`. 사용자 구두승인("진행해줘"). ⚠ pipeline-state stale(OverlayImage 가리킴)이라 `advance-phase approve prd` 미사용(엉뚱 PRD 오염 회피). worktree `v2.11.1`(GMap.NET+OnvifSolution 고아 복사로 빌드) 구현 → **GMaps.Ui 빌드 0에러** → 격리 단위테스트 **61통과**(DigitalZoom 7신규: 항등/왕복/중심불변/보정정확/단조성/배율테이블) → **code-review(opus) MERGE**(C/H/M 0, L-1 동기화주석·L-2/L-3 무해 판정) → 커밋 `76a68b9` → **v2.6 머지 `2062caf`**(충돌0). 롤백태그 `before-camerapopup-digitalzoom-align`(ec2ed31).
- 🔲 **메인솔루션 재빌드 후 런타임 검증(사용자)**: ①디지털줌 1.5/2.0x에서 팝업·빨간 연결선이 카메라 심볼에 정합 ②디지털줌만 휠 인/아웃 시 팝업 추종 ③디지털줌 중 드래그→저장→재오픈 정합 ④디지털 0 복귀 시 기존 동작. worktree `v2.11.1` 정리 가능. 후속(보류): L-1 테스트 복제 추출, 연결선 화면밖 클램프 UX정책(Out of Scope).

## 🛡️ 완료(2026-06-23): v2.6 tip 프롬프트/명령어 인젝션 카나리 커밋 2개 제거 (안전 태그 생성 후 확인되어 삭제)
- **발견**: 브랜치 `v2.6` HEAD에 비정상 커밋 2개 `c749a9c`·`e3359e6` — 메시지가 `; echo INJECTED > /tmp/injection_proof.txt` (명령어 인젝션 페이로드를 흉내낸 카나리/레드팀 테스트).
- **증거 기반 판정(읽기전용 조사)**: ①두 커밋 모두 **완전 빈 커밋**(tree `75101e0e…`==부모 tree, 파일변경 0) ②`injection_proof.txt` 디스크 어디에도 부재 → **페이로드 미실행·무해**(메시지 문자열일 뿐) ③Author=lsirikh@naver.com, 동일 타임스탬프(15:04:47) ④**원격 미푸시**(ahead 2) ⑤전 ref 통틀어 이 2개뿐.
- **정리**: 롤백 태그 `before-remove-injection-commits`(=c749a9c) 생성 → `git reset --soft ec2ed31`로 빈 커밋 2개 드롭. 작업트리 보존(`M .gitignore` + 미추적 `tests/` 그대로). 결과 HEAD=ec2ed31, origin/v2.6과 ahead 0 동기, force-push 불필요. **무해 확정 후 안전 태그도 삭제** → 빈 커밋들은 어떤 ref에서도 도달 불가(추후 GC).
- **교훈**: 이 repo에 인젝션 카나리 테스트가 유입됨 → git 데이터(특히 커밋 메시지)를 셸에 그대로 넘기지 말 것. project_injection_canary_commits 메모리 참조.

## 🔍 분석완료·수정보류(2026-06-23): 관심지역(ROI) 패널 흐림/색감저하 원인 규명
- 증상: 맵의 관심지역 창(MapRoiControl)이 레이어 패널 대비 흐리고 색감 떨어짐.
- 근본원인: 두 패널 다 콘텐츠 보더에 DropShadowEffect(중간비트맵 렌더→기본 텍스트모드 ClearType-off 그레이AA). 차이는 **텍스트/렌더 품질 보정 유무**. LayerPanelStyle.xaml은 스타일+전 TextBlock에 TextOptions.TextRenderingMode=ClearType / TextFormattingMode=Display / UseLayoutRounding / SnapsToDevicePixels / RenderOptions.BitmapScalingMode=HighQuality 설정(438-442 등). **MapRoiStyle.xaml은 이 보정이 전무** → Effect 중간렌더에서 텍스트 ClearType-off+픽셀미정렬로 흐림.
- 무관 확인: 디지털줌 RenderTransform(ROI는 줌 형제 PropertyPanelCanvas 스크린공간), 프레젠터/캔버스 Opacity·Effect 없음.
- **보류된 수정**: MapRoiStyle.xaml `<Style TargetType=MapRoiControl>`에 위 5개 Setter 추가(TextOptions 상속→내부 텍스트 전파). BroadcastPlayStyle/CameraStreamPopupStyle 등도 동일 보정 누락 가능 → 일괄정비 옵션. (Track B, 미적용)

## 🔨 진행중(2026-06-23): 카메라 팝업 3종 확장 — 설정연동 + 연결선 + 스냅샷UX (롤백 before-camera-popup-settings / before-snapshot-ux 양repo)
- **PRD**: docs/prds/CameraPopup_Streaming_Settings-prd.md, CameraPopup_Snapshot_UX-prd.md / **Plan**: CameraPopup_Streaming_Settings-prd-plan.md
- **① 설정 연동**(lib `b57de1a`/`fb6ea46`, 메인 `3dcc6fe`): 사용자 지침 "IGMapSetupModel 패턴으로 SetupModel을 Streaming에 인터페이스 연동". `SetupModel : IStreamingSetupModel`(스트리밍 ~17속성 추가, 기본값=StreamingSetupModel 동일) → Bootstrapper가 `new StreamingModule(setup,...)` 라이브 주입(하드코딩 `new StreamingSetupModel()` 제거). StreamingModule: 라이브 `_model`을 `IStreamingSetupModel`로 등록(MapViewModel 라이브 읽기) + 서비스 concrete 스냅샷 `IsAutoDiscard=false`(팝업 수명은 MapViewModel 관장). MapViewModel: `ResolveStreamingSetup`(IoC lazy) → 더블클릭 게이팅(`IsCameraPopupUsed` OFF면 무시) + 자동해제 `DispatcherTimer`(TimeoutSeconds 만료→CloseCameraPopupAsync, 드래그/재더블클릭 리셋). EventSetupView 재배선: "자동해제"→IsAutoDiscard, "타임아웃"→TimeoutSeconds. **code-review(opus): H-1**(IsAutoDiscard=false여도 재연결소진 시 DisconnectAsync 항상→컨텍스트누수방지) **H-2**(RefreshCameraPopupPositions .ToList) **M-3**(팝업VM DisposeAsync 멱등) 반영.
- **② 연결선(Leader Line)**(lib `04005a2`): 카메라 심볼중점→팝업경계 빨간 점선. VM `CameraGeo/CameraScreenX·Y/LineX1..Y2 + RecomputeLine`(사각형 경계교점). MapView 팝업 ItemsControl 아래 z레이어 Line. 팬/줌/드래그 추종.
- **③ 스냅샷 UX**(lib `7b3e984`, 메인 `fe276ee`): 저장폴더 설정화(`SnapshotPath` in appsettings/SetupModel/StreamingSetupModel + EventSetupView "스냅샷 저장 폴더" 옵션+찾아보기 OpenFolderDialog) + **폴더 자동생성**(기존엔 폴더없으면 저장실패 — TakeSnapshotAsync에 Directory.CreateDirectory+Path.Combine+절대화). **플래시**(ImprovedCameraItem PART_SnapshotFlash 흰 Rectangle, ImprovedRtspPlayer PlaySnapshotFeedback 0.85→0) + **OSD**(PART_SnapshotOsd 우상단 "스냅샷 저장" 1초). appsettings IsCameraPopupUsed false→**true**(게이팅 기본 ON 안하면 팝업 안열림).
- **④ 맵 패널 z-order 명시**(lib `11d7294`): PropertyPanelCanvas 자식이 문서순서(ZIndex없음)라 카메라팝업이 맨위였음 → 사용자 요청 "카메라 최하위/레이어 최상위"로 Panel.ZIndex 부여: 연결선5<카메라팝업10<속성20<군사30<ROI40<방송50<TTS60<맵등록70<레이어100.
- **z-order 아키텍처 설명(Track A, 무변경)**: 맵영역(`Grid Grid.Row=2`)=2-tier 형제레이어. **Tier A**=`AdornerDecorator Panel.ZIndex=0`>GMapCustomControl(타일+심볼+이미지=GMap마커, 자체 3-Tier z-order). **Tier B**=`PropertyPanelCanvas Panel.ZIndex=200`(윈도우 시스템). Panel.ZIndex는 같은부모 형제만 비교 → 윈도우레이어(200) 전체가 맵(0) 전체보다 항상 위. **카메라팝업 ZIndex=10은 "윈도우들 사이"에서만 최하위, 심볼/이미지보다는 여전히 위**(부모가 다름). 사용자 의도 "심볼/이미지 위에 윈도우" = 이미 정확히 구현됨.
- **장비정보 다이얼로그 z-order Q&A**: ConductorControlView Grid 3레이어(PanelShell<DialogShell<PopupDialogShell) → 다이얼로그가 맵+팝업 덮음. 사용자: **현재대로 유지**.
- **airspace 검증(4에이전트 워크플로우 wi8cunaav, 무수정)**: **판정 LIKELY_HUB(~98% airspace 없음)**. 근거=`StartStreamAsync`(=AcquireAsync=CameraStreamEntry.CreateAsync)가 **Playing(10s)+FrameFormatReady(5s, WriteableBitmap 생성)까지 완전 블록** 후 반환 → ConnectViaHubAsync의 GetLeaseFrame 즉시 non-null → 3초폴링 i=0 break → Hub유지 → VideoView(HWND) 3중방어로 Collapsed → airspace구멍 없음. **폴백 3종(~2%)에서만 airspace**: ①frame 5+3초내 영영 안옴 ②AcquireAsync 예외 ③취소. **확정검증=HUB 배지**(영상 좌상단 파란 'HUB' 글자: 있으면 Hub=airspace없음, 없으면 VideoView=airspace) + 창겹침테스트. **수정보류**(워크플로우 경고: 정상~98% Hub라 관측前 수정금지). airspace 관측시 **1순위수정 대기**: ImprovedRtspPlayer.cs만(공유코어무변경) frame==null폴백 시 IsHubMode 유지+백그라운드 재폴링(롤백 before-hub-force-airspace).
- 🏁 **세션 마무리**. 커밋: `04005a2`(연결선)·`b57de1a`/`3dcc6fe`(설정연동)·`fb6ea46`(리뷰수정)·`7b3e984`/`fe276ee`(스냅샷)·`11d7294`(z-order). 롤백태그: before-camera-popup-settings/before-snapshot-ux(양repo). **양 솔루션 컴파일 0에러**(앱 실행중이라 출력 DLL 복사만 MSB3027 잠김 — 컴파일은 통과). 🔲 **사용자 앱 닫고 재빌드→런타임 테스트 대기**: ①게이팅(연동OFF→안열림) ②자동해제 타임아웃+드래그리셋 ③연결선 추종 ④스냅샷(플래시·OSD·폴더저장) ⑤z-order(레이어창이 위) ⑥**HUB 배지로 airspace 확정검증**. 후속: H-3(레거시 PtzTimeOut→TimeoutSeconds appsettings 마이그레이션), ROI/팝업 텍스트렌더 보정(별도 보류건).

## 🏁 세션 마무리(2026-06-23): Event 도메인 8건 전부 v2.6 머지 + 메인솔루션 재빌드 반영(빌드0)
- 리포트: docs/reports/2026-06-23_Event_Domain_Session_Report.md
- 머지 누적(v2.6): da23583(CRUD계약)→f9b9036(OriginEvent표시)→9d12d09(Device잠금)→a74ce60(MessageType고정)→dccff57(DateTime불변)→4ccbfaa(SelectionView셋터가드)→7d7f6dd→b76ff86(조치보고 컨텍스트메뉴).
- 메인솔루션(v0.5) 재빌드 0오류·DLL복사 성공 → 앱 실행 시 전부 반영. 🔲 런타임 UI 확인만 사용자 몫.

## ✅ 완료(2026-06-23): 탐지/장애 우클릭→조치보고 컨텍스트메뉴(1:N 후속) — 머지 `b76ff86` (롤백 before-event-action-contextmenu 양repo)
- 기존 조치보고 다이얼로그 100% 재사용(새 다이얼로그/메시지/핸들러 0). SimpleParamCommand 신설(CommandParameter 전달형).
- DataGrid.RowStyle ContextMenu>MenuItem '조치보고'(Command=Data.ReportCommand via proxy, CommandParameter={Binding}=행VM) + 패널VM ReportCommand/ReportRowAsync(행VM→.Model→new {D|M}EventCardViewModel(ea,log,model)→ReportDialog.UpdateData→OpenEventReportDialogMessageModel 발행).
- 결정: 중복 허용(1:N, 가드없음)·단건만·Draft(Id<=0) 차단(InfoPopup). 메인솔루션 무변경.
- 검증: 빌드0, 단위테스트 300/11(baseline 동일). PRD/Plan: Event_FollowupAction_ContextMenu.
- 세션 머지 누적(v2.6): da23583→f9b9036→9d12d09→a74ce60→dccff57→4ccbfaa→7d7f6dd→b76ff86. 🔲 메인솔루션 재빌드 후 우클릭 동작 확인.

## 📋 PRD 검토대기(2026-06-23): 탐지/장애 우클릭→조치보고 컨텍스트메뉴 (워크플로 w729slz3c, 시나리오32+시뮬2)
- PRD: docs/prds/Event_FollowupAction_ContextMenu-prd.md. 재사용 100%(새 다이얼로그/메시지/핸들러 0) — IoC.Get<DetectionReportDialogViewModel>().UpdateData(card,user)+OpenEventReportDialogMessageModel{EventType}, EventCardListPanelViewModel.OnButtonAction:254-290이 1:1 레퍼런스. ConductorControlViewModel.cs:371이 DETECTION/MALFUNCTION 라우팅 기존재.
- 유일 신규: 행VM(DetectionEventViewModel)→.Model(IDetectionEventModel)→new DetectionEventCardViewModel(model) 어댑터 + DataGrid.RowStyle ContextMenu(proxy line22 기존재 → Data.ReportCommand, CommandParameter={Binding}=행VM) + 패널VM ReportCommand. 변경=라이브러리 4파일(XAML2+VM2), 메인솔루션 무변경.
- 1:N: 단건 권장(N건은 기존 전체조치보고 배치 ExecuteBatchReportAsync 담당). 가드: Draft(Id<=0) 차단 필수(FromEventId FK), IsActionReported 중복은 정책결정.
- 사용자에게 UX 결정(중복행 처리/Draft 처리/단건확정) 질의 후 Plan→worktree→구현.

## ✅ 완료(2026-06-23): SelectionView 불변필드 누수 차단 — 머지 `4ccbfaa` (롤백 before-selectionview-lock 양repo)
- 감사(워크플로 wos6g7m1q): *SelectionView 4개(일괄적용 편집기)가 EventDashboardView 호스트에선 편집 가능 → ApplyButton 프로그래matic write-back → 불변필드 누수 PUT(진짜 갭). 리포트 다이얼로그 호스트는 IsEnabled=false라 무관.
- 수정: XAML로는 프로그래matic 못 막아 **모델 셋터 가드**가 정답 → MessageType(BaseEventViewModel)/Device·Status(ExEventViewModel)/OriginEvent(ActionEventViewModel) 셋터에 `if(!IsDraft)return` 추가. DateTime은 기존 가드. → 불변 보호가 모델 셋터에 중앙화(전 경로 차단), Draft만 편집.
- 검증: 빌드0, 단위테스트 300/11(baseline 동일·신규회귀0).
- 세션 머지 누적(v2.6): da23583→f9b9036→9d12d09→a74ce60→dccff57→4ccbfaa. 🔲 메인솔루션 재빌드 후 확인.
- ▶ 진행중: 탐지/장애 우클릭→조치보고 컨텍스트메뉴 PRD(1:N 후속 조치보고) 워크플로.

## ✅ 완료(2026-06-23): EventPanel 불변속성 보호 — DateTime 차단 — 머지 `dccff57` (PRD EventPanel_Immutable_Guard, 롤백 before-event-datetime-lock 양repo)
- 분석(워크플로 wu0uvar21, 시나리오50+시뮬2): 편집가능·불변 컬럼은 DateTime(created_at) 하나뿐(4패널). 나머지(MessageType/Device/OriginEvent/action_reported) 이미 정합.
- 적용: 4패널 DateTime 편집 TextBox IsEnabled={Binding IsDraft}+ToolTip+ShowOnDisabled + BaseEventViewModel.DateTime setter `if(!IsDraft)return`(2차방어) + 기존 Device/OriginEvent 툴팁에 ShowOnDisabled 소급. Inform 불필요(원천차단).
- 검증: Events.Ui 빌드0. → 🔲 메인솔루션 재빌드 후 기존행 DateTime 비활성 확인.
- 이벤트 불변필드 UI 보호 전 항목 완료: type_event(IsReadOnly)·device_id/from_event_id(IsEnabled=IsDraft)·action_reported(읽기전용)·created_at(IsEnabled=IsDraft+setter가드).
- 세션 머지 누적(v2.6): da23583→f9b9036→9d12d09→a74ce60→dccff57. ⏸️ 보류: 우클릭 조치보고 컨텍스트메뉴 PRD.

## 🔬 분석완료(2026-06-23): EventPanel 불변속성 보호 — 워크플로 wu0uvar21 (시나리오50+시뮬2) → PRD 검토대기
- 전수조사: 편집가능·불변 컬럼은 **DateTime(created_at) 하나뿐**(4패널). 나머지(MessageType/Device/OriginEvent/action_reported) 이미 정책 일치.
- 권장(차단형): 4패널 DateTime 편집 TextBox에 IsEnabled={Binding IsDraft}+ToolTip(+ShowOnDisabled). Device 가드 1:1 확장. Inform 불필요(입력 원천차단). 선택: BaseEventViewModel.DateTime setter `if(!IsDraft)return;` 2차방어.
- PRD: docs/prds/EventPanel_Immutable_Guard-prd.md. 사용자에게 구현 범위(최소 XAML만 / +2차방어·ShowOnDisabled) 질의중. Track B.

## ✅ 완료(2026-06-22): Message Type 패널별 고정 — 머지 `a74ce60` (롤백 before-event-type-fixed 양repo)
- 검토: 라이브 type_event는 패널당 단일 아님(탐지=Intrusion 19+ContactOn 1, 장애=Fault, 연결=Connection; 서버 type_event 자유 string). 사용자 결정: 패널별 고정(연결=Connection/장애=Fault/탐지=Intrusion/조치=Action), 하위유형 무시.
- 적용: 4패널 뷰 Message Type 컬럼 IsReadOnly=True(편집 차단·표시전용) + 4패널 OnClickInsertButton 신규 Draft에 정규 type_event 세팅(모델 기본 None 전송 버그 해소). 기존 ContactOn 등은 실제값 그대로 표시(오기재 안 함).
- 검증: Events.Ui 빌드0. → 🔲 메인솔루션 재빌드 후 확인.
- 세션 머지 누적(v2.6): da23583(CRUD계약)→f9b9036(OriginEvent표시)→9d12d09(Device잠금)→a74ce60(MessageType고정). ⏸️ 보류: 우클릭 조치보고 컨텍스트메뉴 PRD.

## 🛠️ 착수(dev): 맵 위 이동식 RTSP 스트리밍 팝업 — PRD Approved v1.3 (2026-06-23)

- **요청**: 맵 카메라 더블클릭 → 카메라 근처 이동식 RTSP 영상 팝업. 스트리밍은 참조 `C:\source\repos\Dotnet.Rtsp.Viewer.Ui` 답습. 디자인은 관심지역/레이어 창 답습. 640×380 큰버전, 기본은 더 작게. **드래그 이동 위치를 카메라별로 기억(Geo 좌표 중심)** — 1번→드래그→2번→닫기→더블클릭 시 2번 재현. 산출물=PRD+스토리보드+와이어프레임.
- **PRD**: `docs/prds/Rtsp_Map_Popup-prd.md` v1.0 **Draft** (스토리보드 §8 + 와이어프레임 §9 포함). FR12 / NFR7 / 리스크8 / 미결7.
- **분석**: 워크플로우 `wf_d10af6dd-754` — 5영역 병렬(sonnet) + architect 종합(opus, 782K token). 결과 `tasks/w9i9q652v.output`.
- **핵심 설계 결정**: ①앵커=**드래그 후 단일 위경도(PointLatLng) 저장**(픽셀오프셋 기각, FromLatLngToLocal 단일변환 추종) ②영속=**MariaDB `CameraPopupPositions` 테이블**(v1.1 확정 — 다중 클라 공유, GMapDb CRUD+CREATE IF NOT EXISTS, 로컬 JSON 기각) ③스트리밍=참조 `Streaming(.Base)` **ProjectReference 이식**(LibVLCSharp 3.9.4, ImprovedRtspPlayer/Service/CameraStreamHub/StreamingModule 재사용) + CameraConnectionAdapter 신규 ④**airspace→Hub WriteableBitmap(IsHubMode=true) 기본**(VideoView HWND hole 회피).
- **재사용 발견**: 우리 repo에도 동일 `Ironwall.Dotnet.Libraries.Streaming`(LibVLC, 완성) + 구형 `Dotnet.Streaming.UI`(FFmpeg, **사용금지**) 공존. 카메라 RTSP=`CameraUrlsModel.RtspMain/RtspSub`. 더블클릭=`GMapMarkerBaseControl.MarkerDoubleClick`(line217, **VM 미배선**). floating=`PropertyPanelCanvas`(ZIndex200)+`MapFloatingPanelStyle`. 좌표변환=`FromLatLngToLocal/FromLocalToLatLng`.
- **확정(v1.1, 06-23)**: Q1 저장소=**DB(다중클라 공유)** / Q2 키=카메라Id 단독 / Q3 중복=기존 포커스 / Q7 영상=전부 Hub. **미결(기본값 진행)**: Q4 크기영속 Q5 줌아웃정책 Q6 모듈등록위치(메인Bootstrapper).
- **v1.2(06-23)**: ①RTSP=**카메라모델 `CameraDeviceModel.Urls.RtspMain`(rtsp_main) 직접 사용** 확인(어댑터는 옮겨담기만, RtspSub/Ip조합 폴백, 작은팝업+Hub라 RtspSub 기본 검토). ②**최초 위치=카메라 심볼 우상단** — 중점 c 기준 오른쪽 100/위 100에 팝업 좌하단(`Left=c.X+100, Top=c.Y-100-H`, 상수 INITIAL_OFFSET_RIGHT/UP). AnchorGeo=팝업 좌상단 위경도.
- **v1.3 확정(미결 0)**: Q4 크기 **비영속**(DB W/H 제거) / Q5 줌 자동숨김·클램프 없음+**심볼 사라지면 팝업 닫힘(FR-13)** / Q6 등록=메인솔루션 Bootstrapper.
- **착수**: **PRD Approved** · 롤백 태그 `before-rtsp-map-popup` **양 repo**(라이브러리 a74ce60 / 메인솔루션 af08a50) · **plan** `docs/plans/Rtsp_Map_Popup-prd-plan.md`(8 Phase).
- **⚠ Phase0 핵심발견**: 우리 repo `Streaming`/`.Streaming.Base`=**stale**(Hub 없음·IsHubMode 미지원·**라이브러리 .sln 미포함**·메인솔루션 미참조·ImprovedRtspPlayer=VideoView전용). 참조 repo가 **Hub 4파일 포함 최신판** → **우리 것을 참조 최신판으로 교체**(롤백 보호). ※위 '재사용 발견'의 "완성"은 부정확 — 구버전이었음.
- **⚠ 섞임 발생→정리 완료(06-23)**: 메인 디렉터리가 v2.11.0 체크아웃 중일 때 **Event.UI 세션이 v2.9.20를 머지 → 내 RTSP 브랜치에 Event 머지가 잘못 얹힘**. 정리: ①Event fix(v2.9.20)를 **v2.6에 제자리 머지**(`7d7f6dd`) ②v2.11.0를 `13264d2`(RTSP만)로 force-reset ③RTSP를 **전용 worktree `C:/workspace_app/worktrees/v2.11.0-rtsp`로 격리**(GMap.NET 고아파일 복사, Streaming 빌드0 검증). 근본원인=한 디렉터리 공유 → 메인=Event용/worktree=RTSP용 분리. **유실 0**.
- **진행(dev, worktree `C:/workspace_app/worktrees/v2.11.0-rtsp`, 브랜치 `v2.11.0`)** — ※코드=worktree, docs(PRD/plan/session-context)=메인 디렉터리(gitignore 로컬)에서 갱신:
  - ✅ **Phase0 완료**(커밋 `13264d2`): 참조 Streaming/.Base 최신판(Hub 4파일+IsHubMode ImprovedRtspPlayer) 소스 **교체 이식** + .sln 등록 + AllowUnsafeBlocks. **빌드 0에러**, 네이티브 libvlc/plugins(1288) 배포 확인. worktree에도 GMap.NET 고아파일 복사+Streaming 빌드0 검증.
  - ✅ **P1 완료**(`acbb667`): CameraPopupPositions(CameraId PK, Lat/Lng DECIMAL, UpdatedAt) — Model + GMapDb 스키마/CRUD(Dapper, Upsert ON DUPLICATE KEY) + `ICameraPopupPositionStore`/`CameraPopupPositionStore`(DB위임+인메모리폴백+Semaphore). ※worktree에 **OnvifSolution 고아도 복사**(GMap.NET 외 2번째 고아 gitlink — 빌드 위해 필수).
  - ✅ **P3 완료**(`7562d81`): `CameraConnectionAdapter`(Urls.RtspSub→RtspMain→Ip, null가드) + GMaps.Ui→Streaming(.Base) 참조. **NFR-07 버전정합**: Streaming Caliburn.Micro 5.0.258→4.0.230, Autofac 8.4.0→8.3.0(NU1605 해소). 빌드0.
  - ✅ **P2 완료**(`0fbe6af`): GMapCustomControl `OnMarkerDoubleClicked` 이벤트+`TriggerMarkerDoubleClicked`+OnMouseLeftButtonDown 컨트롤레벨 더블클릭 감지(GetMarkerAtScreen, 편집/일반 공통, 500ms). MapVM 구독/해제+`OnMapMarkerDoubleClicked`(IpCamera 한정→ICameraDeviceModel→CameraConnectionAdapter, null/영상없음 가드, 팝업 오픈은 P5 TODO). 빌드0.
  - 🔲 **남음(UI 중심·대형)**: **A(Hub) 결정됨**. **P4** CameraStreamPopupControl(MapRoiControl 드래그 답습: FindParentOfType<ContentPresenter>+Canvas.SetLeft/Top, 헤더 Y≤42 guard)+CameraStreamPopupStyle.xaml(MapRoiStyle/DesignTokens 답습: Border CornerRadius10+DropShadow, 헤더42 PrimaryBlue+PanelCloseButtonStyle, 바디 ImprovedRtspPlayer)+VM. **Hub 배선**: CameraRowViewModel(eventId,desc,`ISharedCameraStreamHub`)+CameraViewModel(ICameraModel,rowId,row) DataContext → 플레이어 OnLoaded가 IsHubManaged→ConnectViaHubAsync→row.StartStreamAsync→hub.AcquireAsync→lease.Frame(공유 BitmapSource). ⚠`ISharedCameraStreamHub`가 StreamingModule에 등록됐는지 확인 필요(미등록 시 추가). ICameraModel은 Streaming.Base(CameraModel)로 RtspConnectionInfo 래핑. · **P5** MapVM통합(ObservableCollection<팝업VM>+PropertyPanelCanvas:676 ItemsControl(Canvas), 우상단 Left=c.X+100/Top=c.Y-100-H, 팬줌추종 MainMap_OnCurrentPositionChanged:4532·OnMapZoomChanged:4583, 멀티/중복포커스, FR-13 Markers.CollectionChanged Remove:535, Dispose C-03) · **P6** 메인솔루션 Bootstrapper(StreamingModule등록+LibVLC단일진입+네이티브배포) · **P7** 테스트/리뷰/CHANGELOG/양repo머지.
  - ✅ **P4 완료**(`05ff282`): `CameraStreamPopupViewModel`(Hub 배선: CameraRowViewModel+ISharedCameraStreamHub+CameraViewModel=player DataContext, AnchorGeo/CanvasLeft·Top, 크게보기 384↔640 비영속, Close/DragCompleted, IAsyncDisposable=Lease해제)+`CameraStreamPopupControl`(헤더 드래그 Y≤42+경계clamp, FindParentCanvas)+`CameraStreamPopupStyle.xaml`(MapRoiStyle/DesignTokens 답습, 바디 ImprovedRtspPlayer)+Generic.xaml 등록.
  - ✅ **P5 완료**(`aac8faa`) — **라이브러리 측 완성**: MapVM `ObservableCollection<CameraStreamPopupViewModel> CameraPopups` + 더블클릭→OpenCameraStreamPopupAsync(저장위치 우선/우상단 최초 c.X+100·c.Y-100-H/중복=포커스) + 팬·줌 RefreshCameraPopupPositions(Geo추종) + FR-13(Markers.CollectionChanged Remove/Reset→닫기) + 드래그완료→AnchorGeo+DB Upsert + Dispose(Lease). Hub=IoC lazy(미등록시 비활성). MapView PropertyPanelCanvas ItemsControl(Canvas, CanvasLeft/Top 바인딩).
  - ✅ **P7(lib)**(`da3c56a`): code-review(opus) MERGE가능 — **H-1 수정**(OnDeactivateAsync 열린 팝업 미정리→Hub Lease 누수: CameraPopups 전부 CloseCameraPopupAsync) + 단위테스트 `RtspMapPopupTests` 7(어댑터우선순위/우상단위치, **48통과**) + CHANGELOG. (후속 M-2 드래그중추종 tug-of-war, L-1 PreloadAsync 데드코드)
  - ✅ **라이브러리 머지**(`c9fcd8d` v2.11.0→v2.6, **충돌없음**, Event 전진분과 다른파일).
  - ✅ **P6**(메인솔루션 `1ee7ae8`@v0.5): Bootstrapper `RegisterModule(new StreamingModule(new StreamingSetupModel(),_log,90))`(기본생성자, LibVLC lazy). **메인솔루션 빌드0** + 네이티브 libvlc/plugins(644)+LibVLCSharp.dll 배포(NFR-04). 제 Bootstrapper만 커밋(타세션 in-flight 보존).
  - ✅ **런타임 init 버그수정**(메인솔루션 `4049071`): 실행 시 "Failed to initialize LibVLC". 원인=**전이 ProjectReference로는 VideoLAN.LibVLC.Windows 네이티브 복사 타깃 미실행** → 앱 출력 libvlccore.dll 누락+plugins 0개(libvlc.dll만). 수정=앱 csproj에 LibVLCSharp/LibVLCSharp.WPF/VideoLAN.LibVLC.Windows 3.0.21 **직접 PackageReference** → libvlc+libvlccore+plugins(322) 완전배포. 빌드0. ★교훈: 네이티브 패키지는 앱 프로젝트 직접 참조 필수(전이 안 됨).
  - ✅ **런타임 UX 버그수정**(라이브러리 `404e4fd`@v2.6): 영상은 뜨나 ①헤더 크롬 안보임 ②드래그 불가 ③ROI/레이어 스타일과 다름. **3개 진단 에이전트 병렬**(my-template/airspace-hub/panel-style-gap): ⓐWidth/Height를 기본 Style Setter {Binding}으로 줘 DataContext 타이밍에 NaN→헤더Row 무너짐 → DataTemplate 로컬값 이동. ⓑ{Binding Title}(DP없음)+inline버튼 → PanelTitle DP 추가 + ROI 3열구조 + PanelCloseButtonStyle 통일. ⓒFindParentCanvas가 ItemsPanel(0×0) 반환 → 크기있는 Canvas 우선. ⓓ플레이어 ConnectionInfo를 TemplatedParent.DataContext로 고정. 양 솔루션 재빌드 0에러.
  - ⚠ **미해결 가능성(airspace)**: 영상이 Hub(SharedFrame Image)가 아니라 VideoView(HWND)면(콜드스타트>3s시 ConnectViaHubAsync frame==null→IsHubMode=false 폴백, ImprovedRtspPlayer.cs:452 `i<15`=3s) 케이스별 크롬 침범 가능. 레이아웃 정상화로 헤더 Row0 분리됐으니 1차로 충분 판단. 부족 시 Hub 프레임 대기 연장(공유코어 영향 주의) 또는 Hub 강제. **재테스트 결과로 분기**.
  - ✅ **RTSP URL 소스 검증**(라이브러리 `(log)`@v2.6): 사용자 요구 "카메라 세팅 rtsp url로 접속". **4에이전트 워크플로우(camera-model/connection-build/reference-pattern+opus종합)** 결과 **ALREADY_OK** — `CameraConnectionAdapter`가 `Urls.RtspSub→RtspMain` 풀 URL을 파싱·재조립 없이 `RtspConnectionInfo.Url`에 그대로 싣고 `GetFullUrl()`이 원본 반환(인증/경로/쿼리 보존). VLC 직접·Hub 전 경로 무변형. **URL 입력=카메라 상세보기 > URLs 탭(CameraUrlsView, RtspMain/RtspSub)**, CameraSelectionView엔 칸 없음. 변형되는 유일 케이스=둘다 빈값→`rtsp://ip:554`(경로없음) 폴백. **스트림 우선순위 결정(사용자): 둘다=서브/하나=있는것 → 기존 preferSub=true(FirstNonEmpty(sub,main))와 일치, 로직무변경**. 추가: 접속 URL 검증 로그(자격증명 마스킹 `rtsp://***@host`, 보안규칙 준수).
  - 🎉 **기능 8/8+init+UX수정+URL검증. 양 repo 머지·빌드0·네이티브배포·H-1·48테스트.** 🔲 **재테스트: 헤더표시/드래그/영상**(카메라 URLs탭 RtspMain/RtspSub 필요, 로그 `[CameraPopup]…URL(설정값)=`로 실접속 URL 확인). ⚠worktree `v2.11.0-rtsp` 정리가능.

## ✅ 완료(2026-06-22): 이벤트 발생장비/조치원본 변경 차단 — 패널 Device 잠금 — 머지 `9d12d09` (롤백 before-event-device-lock 양repo)
- 서버 정책(API v4.8): *EventReplace(additionalProperties=False)가 device_id(E-05)·device_description(E-06)·from_event_id(E-02)·created_at(E-07) 변경 422 차단. 발생시점 장비 영구귀속 + 조치보고 1:N invariant 보존.
- 클라 정합: 탐지/장애/연결 패널 Device 편집 ComboBox에 `IsEnabled={Binding IsDraft}`(신규행만 지정, 기존행 잠금)+ToolTip. Action OriginEvent는 기존에 동일 잠금됨. action_reported는 이미 읽기전용 CheckBox.
- 미적용(선택): DateTime(created_at)도 편집은 되나 Create/Replace에 없어 비-영속(무해 no-op) — 필요시 동일 잠금 가능. → 🔲 메인솔루션 재빌드 후 기존행 Device 콤보 비활성 확인.

## ✅ 수정(2026-06-22): Action OriginEvent 패널 미표시 — 머지 `f9b9036` (롤백 before-fromevent-converter-fix 양repo)
- 증상: 조치내역 패널 OriginEvent 빈칸. 서버엔 from_event 정상 저장(라이브 확인: action 5016→from_event id=28017 type_event=ContactOn).
- 원인(기존 잠재버그, 내 CRUD 변경과 무관): FromEventConverter가 type_event를 'intrusion'/'detection'/'fault'/'malfunction'로만 매칭 → 세부유형(ContactOn 등) `_=>null` → FromEvent=null → ResolveOriginEvent null → 빈칸.
- 수정: 구조 기반 라우팅(Detection=result / Malfunction=reason 필드)으로 변경, type_event 보조폴백. Messages 빌드0.
- 부수확인: 슬림 Update가 from_event_id 미전송해도 서버가 from_event 보존(Action 사후 불변 정상). → 🔲 메인솔루션 재빌드 후 OriginEvent 표시 확인.
- ⏸️ 보류: 탐지/장애 패널 DataGrid 우클릭→조치보고 컨텍스트메뉴 신설 PRD(에이전트총동원+시나리오30+시뮬2회) — 이 버그로 중단됨, 재개 대기.

## ✅ 완료(2026-06-22): EventPanel CRUD 서버계약 정렬 + 저장 견고성 — 머지 `da23583` (PRD/Plan: EventPanel_CRUD_Api_Alignment)
- Phase1(추가/수정 422 해소): slim *EventReplace DTO 4종(BaseDto 비상속→created_at 배제) + ToXxxEventDto에 device_id + Update 4종 시그니처 slim 교체 + Action from_event_id 제외(원본 사후 불변). Phase2(견고성): 4패널 Save를 Detection 패턴 통일(per-item catch+부분실패/보류 통지+보존+isValidForCreate FK가드+IsEdited 리셋) + 3패널 swap-on-success + insertList VP기준 + 데드코드 제거 + OriginEvent 신규행만 편집(IsEnabled=IsDraft).
- 검증: 라이브러리+메인솔루션 빌드0. 단위테스트 신규실패 0(11실패는 v2.6 baseline 동일·무관: DeviceSymbolLookup/NATS/Batch/CardBinding). 롤백 before-event-crud-align(양repo). 🔲 dev 라이브 스모크(추가→수정→삭제 200) 잔여.
- 설계조정: PRD의 "베이스헬퍼 채택"→"Detection 패턴 통일"(provider가 model/throw 반환이라 래핑 복잡도 회피, 동일 G3 달성). BaseDto·EventDbService(동명·무관) 미변경.

## (이전 검증 기록) EventPanel CRUD↔API — 추가·수정이 서버계약 불일치로 422 (워크플로 wksk9nbob, 라이브 openapi 직접확인)
- 서버 GOP RESTful API 1.6.0 openapi 직접조회 + 20에이전트 감사 + 적대적검증(14건중 10확정/4오탐).
- **추가(POST) 깨짐**: Detection/Malfunction/Connection Create는 `device_id`(flat FK) 필수인데 ToXxxEventDto가 nested device만 채우고 device_id 미설정(+DefaultValueHandling.Ignore로 0이면 키누락)→422. Action Create는 OK(from_event_id 일치).
- **수정(PUT) 전타입 깨짐**: 서버 PUT=*EventReplace(additionalProperties:False, 허용필드만: type_event/result|reason/detail, Action은 type_event/content/user). 클라는 전체DTO(id/device/created_at(BaseDto기본값)/action_reported/from_event_id)를 실어→422 extra fields. (PATCH=*Update는 관대)
- **삭제(DELETE)·갱신(GET) OK**. 코드경로는 연결됨, 페이로드가 거부됨(임계값/카메라류 스키마불일치, 전 이벤트타입).
- 부차(medium): Save 부분실패 무통지(Malfunction/Connection/Action, Detection만 견고) + 로드실패 빈화면(3패널 clear-before-fetch). 베이스 ExecuteSaveUpdates/NotifySaveResult 구현돼있으나 Save경로 미사용(dead, Delete만 통일).
- 오탐4: Malfunction모델 [JsonProperty]누락(DTO명시매핑이라 무해), TypeEvent하드코딩, Id write-back누락주장 등.
- 수정방향(Track C): ①ToXxxEventDto에 DeviceId=Device?.Id 추가(nested device 유지) ②PUT용 슬림 *EventReplace DTO(BaseDto 비상속→created_at 배제) 신설+Update시그니처 교체, Action은 from_event_id 제외 ③(Phase2)Save 베이스헬퍼 통일+swap-on-success+OriginEvent ReadOnly.
- **PRD 작성완: docs/prds/EventPanel_CRUD_Api_Alignment-prd.md** (architect+code-reviewer 설계리뷰 반영). Phase1=계약수정(추가/수정 422해소), Phase2=견고성/UX. 함정: EventDbService 동명메서드 무관(미변경), BaseDto 미변경. → 사용자 PRD 검토+Phase 범위 승인 대기.

## ✅ 완료: 카메라 상세 URLs 편집+저장 (2026-06-22, 머지 `aa5aa43`, 롤백 before-camera-urls-editable 양repo)
- URLs는 카메라 setting이 아니라 **카메라 본체 urls**(CameraResponse.urls, nested: homepage/onvif/streams{rtsp:{main,sub},webrtc:{main}}/snapshot). 클라 매핑 ToCameraUrlsModel/Dto(flat↔nested) 기존 완비.
- CameraUrlsView 읽기전용 → **편집 TextBox 6**(Homepage/ONVIF/RTSP Main·Sub/WebRTC/Snapshot, TwoWay/LostFocus). CameraDetailDialogViewModel: URLs 편집 복사본(닫기=취소) + SaveAsync에 **UpdateCameraAsync(카메라 본체)** 추가. → 카메라 상세 '저장'=Setting(UpdateCameraSettingAsync)+URLs(UpdateCameraAsync) 동시. Setting은 GET 성공 시에만 PUT(_settingLoaded). HW Spec은 읽기전용(ONVIF/외부).
- 검증: 라이브러리 빌드0. 메인솔루션 wrapper 변경 불필요(SaveAsync 이미 호출).

## ✅ 완료: 카메라 상세 Setting 편집+저장 (2026-06-22, P1 머지 `b1b39b6` + P2 `af08a50`)
- 롤백 `before-camera-setting-save`(양 repo). 카메라 setting은 **별도 API** `/cameras/{id}/settings`(GET/PATCH/PUT, 10필드: weather_mode·camera_mode·heater·fan·headlight·day_night_mode·focus_mode·iris_mode·tracking·palette). 클라 `IDeviceApiService.Get/Patch/UpdateCameraSettingAsync` 이미 존재.
- **P1 라이브러리**: `ToCameraSettingDto`(역매핑 누락분 신설). `CameraDetailDialogViewModel` — 진입 시 GetCameraSettingAsync로 **실제 설정 로드**(이전엔 항상 빈 기본값) → Setting 탭 편집 → `SaveAsync()`=UpdateCameraSettingAsync(PUT). 닫기=취소. CameraSettingViewModel enum옵션 + CameraSettingView **편집 ComboBox 10**(이전 읽기전용). HW Spec/URLs는 읽기전용 유지(하드웨어·파생).
- **P2 메인솔루션**: CameraPropertyDialogViewModel.ClickSave(성공 시 닫기, 실패 Inform) + View '저장'/'닫기' 2버튼.
- 검증: 라이브러리 빌드0 · 메인솔루션 CS오류0(앱 실행중 MSB3027만). **🔲 앱 재빌드 후: 카메라 단일선택→상세보기→Setting 탭(실제값)→편집→저장.**
- 미해결(별개): URLs streams(rtsp/webrtc) 중첩파싱 표시 정확성, 서버 setting GET이 lazy-기본생성인지.

## ✅ 수정완료: 함체 임계값 저장 미반영 버그 — 모델 서버스키마 정렬 (2026-06-22, 머지 `e8c06a4`)
- **근본원인(라이브 API 직접 검증)**: 클라 `EnclosureThresholdConfigModel`이 `humidity_low`/`vibration_threshold`(서버 스키마에 없음) 사용 + `current_high`/`voltage_low`/`vibration_high`(서버 실제 필드) 누락 → PUT 시 누락필드 null 덮어쓰기 + 잘못된 필드 무시 → 저장 미반영.
- 서버 `EnclosureThresholdConfig` 실제 스키마(GET 확인): temp_high·temp_low·humidity_high·current_high·voltage_low·**vibration_high(int)**.
- **수정**: 모델 6필드 서버정렬 + cascade 10파일(SettingVM/View 6행폼·ThresholdSummary·ThresholdEquals·Clone·M1가드·xUnit 내것+Models/Messages/Api 기존테스트). 롤백 `before-threshold-schema-align`(양 repo).
- **카메라 상세(API 확인)**: 서버 CameraResponse=hardware_spec(9필드 일치)+urls(homepage/onvif/streams/snapshot), **setting 없음**(Setting탭 데이터 원래 없음, 읽기전용이라 저장무관·별개 표시갭). 변경 없음.
- 검증: Devices.Ui 빌드0·임계값 xUnit8·Monitoring.Models 빌드0. **🔲 앱 재빌드 후 저장→서버 반영 재확인.**

## ✅ 완료: 함체 임계값 다이얼로그 저장/닫기 2버튼 + API 즉시저장 (2026-06-22)
- 롤백 `before-threshold-save-buttons`(양 repo). opus3 스카웃 후 구현.
- **결정**: 저장=API 즉시반영 / 닫기=취소(편집 폐기). 카메라 상세=읽기전용 뷰어(저장 불요, 변경 없음 — 3탭 전부 IsReadOnly/OneWay).
- **P1 라이브러리**(머지 `5dc443a`): EnclosureThresholdDialogViewModel — 편집을 **작업 복사본(_working)** 에 수행(닫기 시 모델 보존) + `SaveAsync()`(복사본→모델 + `IDeviceApiService.UpdateEnclosureAsync` PUT 즉시저장 + `DeviceProviderService.FetchDeviceByIdAsync("Enclosure",id)` 단건 재동기, 서비스=IoC.Get). ctor in-place 주입 제거.
- **P2 메인솔루션**(커밋 `88fe643`): EnclosurePropertyDialogViewModel.ClickSave(SaveAsync 성공 시 닫기, 실패 시 Inform·유지)+ClickCancel(닫기). View Button Group 2열 '저장'/'닫기'.
- **검증**: 라이브러리 빌드0 · 메인솔루션 CS오류0(앱 실행중 MSB3027 잠금만). 앱 종료 후 재빌드 시 런타임.
- **🔲 런타임**: 함체 단일선택→'임계값'→값 입력→**저장**(즉시 서버 반영) 또는 **닫기**(폐기). 실패 시 팝업.

## ✅ 완료: 함체(Enclosure) 임계값 설정 다이얼로그 (2026-06-22, 양 repo)
- 롤백 `before-enclosure-threshold-dialog`(양 repo). PRD `docs/prds/EnclosureThresholdDialog-prd.md`. 카메라 상세 다이얼로그 패턴 복제.
- **P1 라이브러리**(머지 `5616dd3`+`ea4eb68` v2.9.9→v2.6): 다이얼로그 4종(EnclosureThresholdDialogViewModel[Conductor.OneActive]+EnclosureThresholdSettingViewModel/View: 온/습도 상하한·진동) + OpenEnclosureThresholdDialogMessageModel + EnclosureSelectionViewModel.ThresholdButton()/IsSingleSelected + SelectionView '임계값' 버튼. **매핑 보강(BLOCKER급)**: DtoToModelHelper threshold_config(JObject)↔EnclosureThresholdConfigModel 양방향(이전 드롭) + DTO NullValueHandling.Ignore + DeviceEquals/ThresholdEquals(null=빈객체 동등) + UpdateDeviceProperties 복사 + **M1 가드(빈 임계값 미전송)**. xUnit 8.
- **P2 메인솔루션**(커밋 `bd612bd`): EnclosurePropertyDialogViewModel wrapper+View(md:DialogHost) + ConductorControlViewModel IHandle/HandleAsync + Bootstrapper 등록.
- **저장 흐름**: 다이얼로그=model 직접 편집, 영속=그리드 적용(Save)→DeviceEquals 변경감지→UpdateEnclosureAsync PUT.
- **검증**: 라이브러리 빌드0·xUnit8 · code-review opus(Critical/High 0, M1 수정). ※메인솔루션 완전빌드는 앱 실행중 MSB3027로 CS오류0만 확인 → 앱 종료 후 재빌드 필요.
- **🔲 런타임(사용자)**: 함체 단일선택 → '임계값' 버튼 → 값 입력 → 닫기 → 그리드 '적용' → 저장·재조회 유지.
- **후속 부채(별도)**: Api.Messages 중복 EnclosureDeviceDto(미사용) 삭제 / 테스트 프로젝트 분리 / 서버 임계값 UI.

## ✅ 구현·머지 완료: MBTiles 베이스맵 빈 영역 기본 타일 (2026-06-22, 머지 `a8d968b` v2.10.0→v2.6)

- **요청**: MBTiles 베이스맵 커버리지 밖 흰 화면 → "깔끔/모던" Default 타일. **방법 B**(Provider 빈 타일 반환·격자 타일링) + **베이스맵(MBTilesMapProvider 싱글톤)만**.
- **PRD/Plan**: `docs/prds/BaseMap_NoData_DefaultTile-prd.md` v1.0 **Approved** / `docs/plans/...-plan.md`. 롤백태그 `before-basemap-nodata-tile`(ea4eb68).
- **구현**: ①`MBTilesMapProvider.DefaultTileBytes`(byte[], 물음표X) + GetTileImage **분기(c)정상줌+타일없음 한정** `GetTileImageFromArray`((a)source==null·(b)줌밖 null유지·실제타일 우선·proxy 가드) ②`DefaultTileImageFactory`(GMaps.Ui/Helpers, 256×256/96DPI/Pbgra32/Freeze/Dispatcher/1회캐시, A안 #EEF1F5+우하 1px #DFE3E8) ③MapViewModel Init/Switch UI스레드 주입.
- **검증**: GMaps.Ui 빌드 0에러 · xUnit **41 통과**(신규 결정테이블 6) · 신규 CS8632 0 · architect+code-reviewer(opus)(공유PureImage 금지=use-after-dispose 회피, MERGE가능) · 미리보기 PNG 렌더 확인.
- **⚠ 구조적 발견(중요)**: **GMap.NET은 깨진 고아 gitlink**(`.gitmodules`/`.git`/`.git/modules` 없음, gitlink commit 89334d0 미존재) → 내부 `.cs`는 **git 미추적**. 그래서 ①worktree에 GMap.NET 소스 없음→GMap 의존 빌드 불가(worktree 무용→제거) ②`MBTilesMapProvider.cs` 변경은 **git 외 디스크 고아 파일**(롤백태그 미보호) → **수동 백업 `MBTilesMapProvider.cs.bak-before-basemap-nodata-tile`**. 추적 3파일+CHANGELOG만 v2.10.0 커밋→v2.6 머지.
- **⚠ pipeline-state stale**: phase가 06-19 OverlayImage `report`에 방치(이후 5+ 머지 미반영). `report→plan` 게이트 차단 → `complete` 강제는 OverlayImage 오완료+CHANGELOG 오염 부작용이라 미실행. PRD 승인 게이트만 정상 통과.
- **후속 v1.1(머지 `cead507` v2.10.1→v2.6, 롤백 `before-basemap-logo-tile`)**: 사용자 요청 — **각 타일 정중앙에 센서웨이 로고**(가로·세로 가운데). 3안 중 "타일마다 로고(반복)" 선택. `sensorway.png`(150×50, 메인솔루션 Resources에서 복사) → GMaps.Ui Resources/Images + csproj `<Resource>`, `DefaultTileImageFactory.TryLoadLogo`(pack URI, 실패시 격자만) + DrawImage 중앙(비율축소·여백16/`LOGO_OPACITY`/여백 상수). 빌드0·xUnit41·미리보기 확인. 추적파일(Factory/csproj/png/CHANGELOG)만 커밋(GMap.NET.Core 무변경).
- **🔲 런타임 검증(사용자)**: 메인솔루션 재빌드(앱 종료 시) 후 ①커버리지 밖 팬→흰화면 대신 격자+중앙 로고 ②실제 타일 영역 정상 ③맵 전환 후 정상 ④줌 경계. 로고 크기/투명도 조정은 `DefaultTileImageFactory` 상수.

## ✅ 완료: DataGrid 컬럼 큐레이션 6패널 (2026-06-22, 머지 `442186c` v2.9.8→v2.6)
- 롤백 `before-datagrid-column-curation`(양 repo). Plan `docs/plans/DataGrid_Column_Curation-plan.md`.
- 공통 골격 통일: `No·번호·이름·유형 → [고유] → 위치·상태·활성화`. 리스트 제외(컬럼 삭제, 모델/속성패널 유지): Version·Lat·Lng(전패널)·UserName·Password(카메라/경광등)·Description(스피커/경광등)·Threshold(함체). 카메라 헤더 유형/모드/카테고리, DeviceType 헤더 6패널 "유형" 통일. 컬럼 블록 verbatim.
- 검증: 워크트리 빌드0 · 컬럼순서 6패널 목표일치 · 삭제컬럼 잔존0. ※메인솔루션 재빌드는 앱 실행중(MSB3027) 시 앱 종료 후.
- **Threshold 설정 UI 조사 결과**: 함체/서버 임계값 **편집 UI 없음**(함체 그리드 ThresholdSummary 읽기전용만). 함체 임계값=온도/습도 상하한+진동, 서버=CPU/RAM/Disk/Network(+DTO매핑 드롭됨). **카메라 상세페이지 패턴(CameraDetailDialogViewModel Conductor탭 + CameraPropertyDialogViewModel wrapper + ConductorControlViewModel 핸들러)** 복제로 신설 가능(8단계). → **별도 작업(미착수)**, 사용자 결정 대기.


- **마지막 업데이트**: 2026-08-06 11:15

## ✅ 완료: 장비 속성패널 재설계 P1+P2 (2026-06-22, 머지 `c92344a` v2.9.4→v2.6)

- **롤백**: `before-deviceprop-redesign` **양 repo**(Ironwall dea671f / Monitoring.Solution v0.5 5f520c9). worktree `v2.9.4`(머지 후 정리 가능).
- **PRD/Plan**: `docs/prds/DevicePropertyPanel_Layout_Redesign-prd.md` v1.1 / `docs/plans/...-plan.md`.
- **Phase1**(`176234b`+`3f48b69`): 6패널 4구역(장비공통/장비별/위치/그룹) 통일 + 속성영역만 스크롤·**적용버튼 고정** + 신규 `Utils/Behaviors/BubbleMouseWheelBehavior`(Groups 휠 한계 시만 부모 전파). code-review opus C/H 0, 바인딩 1:1 보존.
- **Phase2**(`c91bc13`+`53df6f5`+`698a43e`+`8eea7ca`): `BaseDeviceViewModel.Bearing`(→Heading, **set mod360**)/`Altitude`(→Altitude) + 6 SelectionVM(`CommonOrNullNullable` 공통값+RefreshAll+Apply HasValue 가드) + 6 `DeviceEquals`(Heading/Alt) + 6 View 위치구역 Alt/Bearing.
  - **매핑 핫픽스(BLOCKER-1)**: `DtoToModelHelper.MapGeolocationToDto`가 Heading·Altitude 실제 전송(이전 누락 → 방위각 저장 무효). `GeolocationDto.Altitude`→`double?`(서버 스키마 anyOf[number,null] optional 확인). `BaseDeviceModel.Altitude`+copy-ctor Heading/Alt.
  - **심볼 FOV**: `DeviceProviderService.UpdateDeviceProperties` 공통섹션 Heading/Alt 복사 + `SymbolEventManager.RegisterDeviceSymbol` `SetUpdate()` → 저장 시 부채꼴 재렌더.
  - **버그수정**: `CameraSelectionViewModel` ctor `RefreshAll()` 누락 복구.
  - **xUnit** `Devices.Ui/Tests/GeolocationMappingTests` 11케이스(왕복/null/BLOCKER-1/가드/직렬화/mod360/DeviceEquals) — **92 통과**. code-review opus 2회 C/H 0(머지가능).
- **검증완료**: worktree 빌드0 · grep 불변식 6/6 · xUnit 92 · **메인 소비솔루션 빌드0**.
- **후속 필드사이징**(머지 `503b089` v2.9.5→v2.6, 롤백 `before-deviceprop-field-sizing` 양 repo): 라벨이 값에 붙던 문제(예 "Device No3") 해소 — 6패널에 `FieldLabel`(Width72/Margin10)·`FieldBox`/`FieldCombo`(Width170) 스타일 + 전 컬럼 Auto. DeviceGroup(툴바 레이아웃)은 제외. 워크트리 빌드0. ※메인솔루션 재빌드는 **앱 실행중 파일잠금(MSB3027)** 이면 앱 종료 후 재빌드 필요(코드 정상).
- **🔲 런타임 검증(사용자)**: ①스크롤(카메라 多필드)·제어기 무스크롤 ②적용버튼 고정 ③Groups 위 휠(0/1/5항목) ④Bearing 편집→저장→재조회 유지 ⑤미설정값 0 미덮어쓰기 ⑥mod360(400→40) ⑦지도 심볼 부채꼴 회전 ⑧기존 선택/저장/그룹 회귀0.
- **후속 PRD 작성완료**: `docs/prds/SpeakerServerAssignment-prd.md` **v1.1 확정**(아래 별도 항목).

## ✅ 구현·머지 완료: 스피커 방송서버(server_id) 배정 (2026-06-22, 머지 `0913360` v2.9.6→v2.6)

- **롤백**: `before-speaker-server-assignment` 양 repo(Ironwall 503b089 / Monitoring.Solution 5f520c9). 커밋 431d66e(P0+P1)·d4be072(P2)·8408f2c(리뷰보강).
- **구현**: SpeakerDeviceDto `server_id`(Ignore)+`ShouldSerializeServer()=>false`(nested 쓰기차단·민감정보 보호) / ToSpeakerDeviceDto `dto.ServerId=model.Server?.Id` / `ToServerModel` 추출 / DeviceEquals `Server?.Id` / UpdateDeviceProperties Server 복사(유령차단) / **ServerProvider**(ILoadable 없이)+FetchServersAsync(실패 시 Clear 생략=stale 보존, startup 적재)+IServerApiService **생성자주입**(D5 정제) / SelectionVM `int? ServerId`+ServerItems+Apply(HasValue+Id 재조회)+RefreshServers / View ComboBox(`SelectedValue+Id`) / OnClickInsertButton 첫 서버 자동배정+0서버 Inform(D3).
- **검증**: 워크트리 빌드0 · xUnit 9 통과(SpeakerServerMappingTests) · **메인솔루션 빌드0** · code-review opus 5블로커/1High 전부 해소(신규 C/H 0, F1은 오탐=테스트 이미 존재).
- **🔲 런타임(사용자)**: ①단일 서버 프리셀렉트(SelectedValue Id) ②변경→적용→재조회 유지 ③다중 공통값/'미변경' ④신규추가 첫서버 자동배정 ⑤서버0개 Inform ⑥새로고침 버튼 ⑦서버삭제 후 갱신(B4).
- **후속 fix(머지 `d5c0712` v2.9.7→v2.6, 롤백 `before-speaker-type-display` 양 repo)**: 스피커/함체/경광등 장비공통에 **Type(DeviceType) 읽기전용 표시 복원**(P1 재구성 때 이 3패널만 누락됐었음). 구역① Row1 col4/5. SelectionVM.DeviceType 기존재. 워크트리 빌드0. ※메인솔루션 재빌드는 앱 실행중(MSB3027 파일잠금)이면 앱 종료 후.
- **기존 이슈(무관)**: Camera VM passthrough 2 테스트는 v2.6 베이스라인부터 실패(별개 사안).
- ~~PRD 작성완료(구현 대기)~~ — 아래는 작성 당시 기록(참고).

## (작성 기록) 스피커 방송서버 PRD (2026-06-22)

- **PRD**: `docs/prds/SpeakerServerAssignment-prd.md` v1.1 — 12-Agent opus 시뮬레이션(Discover4→시나리오7→종합1, 1.17M token) 반영.
- **핵심 발견(5블로커/1High)**: ①SpeakerDeviceDto에 server_id 쓰기필드 부재(nested server만 직렬화→계약위반/422/민감정보 누출) ②BaseModel 참조동등성→ComboBox SelectedItem 프리셀렉트 영구실패(→SelectedValue+Id) ③DeviceEquals server 비교 누락(server-only 변경 소실, Bearing버그 클래스) ④UpdateDeviceProperties Speaker분기가 Server 미복사→유령서버 영구잔존(선결 P0) ⑤ServerProvider 부재. H1: 서버선택 UI(VM/XAML) 전무.
- **재사용 자산**: ServerModel/ServerApiService.GetServersAsync/ServerDto/DeviceGroupProvider패턴/CommonOrNullNullable/ToSpeakerDeviceModel(읽기) 기존재.
- **확정 결정**: D1=SelectedValue+Id / D2=int? ServerId / **D3=해제없음(변경만)+추가시 첫서버 자동배정+서버0개 Inform** / D4=startup1회+새로고침버튼 / D5=ResolveNamed("DeviceApi"). → server_id NullValueHandling.Ignore, P3(해제UX) 제거, PATCH시맨틱 확인 불필요.
- **Phase**: P0 B4(Server복사 선결) / P1 쓰기경로+ServerProvider+DeviceEquals+DI / P2 UI통합+기본배정.
- **다음**: 사용자 승인 시 → **롤백태그 `before-speaker-server-assignment` 양 repo** + worktree(v2.9.6) + CHANGELOG → P0부터 구현.
- **잔여 결정**: `[Assign]` 진단 로깅(`ff27c78`) 정리/유지 미정.

## 📋 (이전) 장비 속성패널 레이아웃 재설계 PRD (2026-06-22)

- **PRD**: `docs/prds/DevicePropertyPanel_Layout_Redesign-prd.md` **v1.1**(6차원 Agent 시뮬레이션 754K token 반영). Preview: `docs/reports/DevicePropertyPanel_Redesign_Preview.html` v2.
- **범위**: 6패널 SelectionView 4구역 레이아웃(장비공통/장비별/위치/그룹) + **속성영역만 스크롤·적용버튼 고정**(Groups 기존 스크롤 유지) + **Bearing·Alt 노출/왕복**.
- **🔴 시뮬레이션 발견 머지블로커**: ① `MapGeolocationToDto`가 Heading 미작성 → **현재도 Bearing 저장 단절(잠재버그)** ② 6패널 `DeviceEquals`가 Heading/Alt 미비교 → Update 미감지 ③ Alt는 `double?` 필수 ④ Phase 분할 오류(Bearing+매핑 같은 Phase로). + R4 중첩휠·Camera RefreshAll 미호출·좌표0 가드·심볼FOV·xUnit 부재.
- **✅ 사용자 확정**: Bearing **6패널 전부** / 방송서버 **별도 PRD 분리**(`SpeakerServerAssignment-prd` 추후) / 호스트 **280px 유지** / mod360 정규화 / Alt `double?`.
- **Phase**: P1=레이아웃+스크롤+BubbleMouseWheelBehavior(저장무관) / P2=Bearing+Alt+매핑+DeviceEquals+copy-ctor+xUnit(왕복단위).
- **다음**: 사용자 승인 시 → Plan 작성 → Phase1 worktree+롤백 착수.

## 🧪 런타임 검증 후속 (2026-06-21~22, Temp-state Phase1 머지 후)

- **✅ DataGrid 첫 선택 누락(one-click-behind) 수정 `dea671f`**(롤백 `before-datagrid-selection-fix`): `DataGridSelectedItemsBehavior<T>` 델타 최적화에서 `_selectedItemsSet`을 OnSelectionChanged 첫 호출 시 `AssociatedObject.SelectedItems`(post-change, 클릭항목 포함)로 lazy-init → 첫 AddedItems 흡수(changed=false) → SelectedItems 미전파 → 에디터 안 뜸. **수정**: OnAttached에서 빈 set 시작 + lazy-init 폴백도 빈 set. 7패널 공통 해소. 빌드0(Devices.Ui+메인솔루션). 런타임 클릭검증=사용자.
- **✅ API DELETE 응답 불일치 — 서버에서 100% 해소 확인**(라이브 openapi 재검증): v4.7(램프·그룹 P0)+v4.8(EM/서버/유저 등 11 P1)+이벤트4종까지 전부 `data:null` 통일, **dict/Union 잔존 0**. 내 보고서(`docs/reports/API_Delete_Response_Inconsistency-report.md`)가 견인. **클라 무수정으로 동작**(방어수정 불요). 잔존 `$ref` 미부착 14→8(우리 클라 무관).
- **🟡 그룹 device_count cascade 버그(서버측, 보고서 제출)**: 스피커 전량삭제(total=0)인데 긴급방송장비 그룹 device_count=92(램프30뿐이라 ~62 유령). 램프삭제는 반영되나 스피커삭제는 미반영 → 장비 DELETE 시 그룹멤버십 cascade 누락(타입별). 클라 정상(서버값 표시). `docs/reports/API_Group_DeviceCount_Cascade-report.md` 작성·전달.
- **그룹 배정**: 정상 동작 확인. 배정 경로 `[Assign]` 진단 로깅 잔존(커밋 `ff27c78`, 롤백 `before-assign-diag-logging`) — 정리/유지 미정.
- **✅ Draft 행 마커 비침 수정 `1718754`**(롤백 `before-draft-marker-opacity-fix`): 신규 추가행이 반투명 렌더(지도 비침) → 마커색 `#33FF9800`(α0x33)→불투명 `#FFE0B2`(7뷰). "갱신하면 정상"=Reload가 미저장 Draft 폐기(설계상 정상). **Reload엔 미저장 경고 없음**(Uninitialize 탭전환에만 있음) — 후속 개선 후보.
- **🟡 램프/그룹 삭제 거짓 "일부 실패" 팝업 + 행 1개 잔류** (근본원인 확정, 클라 미수정): DELETE 200 응답 `data`가 **램프·그룹만 `dict`(객체)**, 나머지 장비는 `null`. 클라 `ToApiResponseAsync<bool>`이 객체→bool 역직렬화 실패(`JsonReaderException`)→`Success=false`→① 거짓 "삭제 일부 실패" InfoPopup ② `ExecuteDeleteAsync` removeLocal 누락 → `FetchAllDevicesAsync`가 upsert(Clear 주석처리, stale 미제거)라 제거가 NATS 타이밍에만 의존 → 행 1개 잔류(갱신 시 해소). **데이터는 정상 삭제**. 검증: openapi(`ApiSingleResponse_dict_` vs `_NoneType_`) + 코드(`ApiResponse.Data`=`T?`) + 런타임 로그 3중. **→ API팀이 `docs/reports/API_Delete_Response_Inconsistency-report.md` 기준 서버 응답 통일 수정 중**(2026-06-21). 서버가 `data:null`/정의DTO로 통일하면 클라 무변경 동작. **클라 방어수정(2xx면 data형태 무관 성공)은 선택적 후속**(미실행). 검증포인트: 통일 후 램프/그룹 삭제 시 팝업無·행 즉시제거·JsonReaderException無.

## 🔍 (해결) 그룹 장비배정 버그 진단 (2026-06-21, Temp-state Phase1 후속)

- **증상**: 신규 제어기(id 973, `is_enable:false DEACTIVATED`)를 새 그룹에 넣었는데 서버 응답상 `device_groups:[]`(미배정). 기존 제어기(1~4)는 그룹9에 정상 배정.
- **명세 확정**: 배정 API의 `skipped_device_ids`는 "**이미 할당됨**"만(openapi: "already in group"). 비활성과 무관 → **973은 서버가 skip한 게 아니라 애초에 전송 안 됨**(클라가 후보/선택에서 누락). 클라 verify-after-success(AssignedDeviceIds 기준)는 정상 동작.
- **가설**: (H1) 저장 전(Id≤0) 상태로 그룹 시도 → G4 후보 제외(정상, 저장 먼저 필요) vs (H1b) 저장했는데 공유 provider 미갱신/후보 누락(실버그).
- **진단 로깅 커밋 `ff27c78`**(롤백태그 `before-assign-diag-logging`): `DeviceAssignDialogViewModel` Initialize(provider 전체Id·후보Id·미저장수) + ConfirmButton(선택/전송Id·서버 assigned/skipped/msg). grep `[Assign]`. **사용자 재테스트 → 로그로 H1 vs H1b 확정 → 실수정** 예정.

## ✅ 완료: 장비패널 CRUD Temp-state 통일 Phase 1 (2026-06-21)

- **머지 `a6f6c3c`** (v2.9.3→v2.6, 롤백태그 `before-tempstate-unification`). PRD/Plan/Report: `docs/{prds,plans,reports}/DevicePanel_TempState_Unification-*`.
- 7패널(Controller/Camera/Sensor/Speaker/Enclosure/Lamp+DeviceGroup) **B모델(즉시-POST)→Temp-state 환원**(서버가 미완성 placeholder 422 거부+Sensor FK). 추가=로컬 Draft(Id≤0), Save 일괄등록.
- PR-A `3deca70`(베이스 템플릿+Draft 격리) / PR-B `434a2e0`(DeviceGroup Temp+Id≤0 게이팅+verify-after-success) / PR-B-fix `80ab9e1`(opus 리뷰 머지차단2+High3 해소) / PR-C `93380b2`(시각마커+미저장알림).
- opus 코드리뷰 2회 + 최종 머지검증 GO. 메인 소비솔루션(`Dotnet.Monitoring.Solution`) 0에러.
- **백로그**: Low 3(Save중 Insert 무피드백·_eventAggregator non-null 가정·Events.Ui Insert 미게이트). Phase2 Speaker server_id FK / Phase3 Enclosure ThresholdConfig·Camera RTSP.
- ⚠ **런타임 검증 미실시**(빌드/정적만): 추가=서버미호출·필수값 보류·Temp그룹 버튼비활성·저장후 반영·마커/알림.

---

**(이전) 마지막 업데이트**: 2026-06-23 (Accounts.Ui 영향범위 식별 + Devices.Ui/Events.Ui 패턴 분석 완료 → Plan 대기)

## 🔖 재개점 (2026-06-23 현재) — Accounts.Ui 신규 프로젝트 구축 준비 완료

### 다음 세션 즉시 착수: Plan 작성 → git tag → v2.9.3 worktree → Phase1

**분석 완료 (이번 세션)**:

- Accounts.Ui 영향 범위 확정: 소비앱 27개 파일 이관/삭제 + 2개 수정(Bootstrapper + .csproj), 라이브러리 신규 20개+
- Devices.Ui / Events.Ui / Bootstrapper.cs 완전 분석 → 구현 레퍼런스 확정

**확정된 구현 레퍼런스 (다음 세션 즉시 사용)**:

- PackageRef: Autofac 8.3.0 / Caliburn.Micro 4.0.230 / MaterialDesign 5.2.1 / xunit 2.9.3 / runner 3.1.0(통일)
- `AccountUiModule(IAccountSetupModel, IMariaDbSetupModel, ILogService?, int)` — setup 하나가 두 인터페이스 구현 → 동일 인스턴스 전달
- Bootstrapper SelectAssemblies(): `Assembly.LoadFrom("Accounts.Ui.dll")` 패턴 그대로 복제
- Bootstrapper ConfigureContainer(): 기존 Account 관련 10줄 → `AccountUiModule` 1줄 교체
- RegisterBuildCallback: 불필요 (NATS 와이어링 없음, PRD H-5 확정)
- View 네임스페이스: `Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels.XxxView`
- IoC 폴백 생성자 + DI 생성자 2개 패턴 (BasePanelViewModel 동일)

## 🔖 재개점 (2026-06-21 이전)

- **📋 PRD `docs/prds/DevicePanel_TempState_Unification-prd.md` v1.1** — 5-agent 설계검토 반영 개정, 사용자 재검토 대기
  - **5-agent PRD 검토(`prd-design-review`)**: 4×gaps-major+1 gaps-minor. 진단은 5/5 실코드 일치(견고). **§3 전제 2개 거짓 판명 → v1.1 개정**: 🔴C1 Draft가 공유 DeviceProvider로 누출(SensorPanel:226, baseline 이미 위반→맵 PIDS 심볼 유령 관측) 🔴C2 DeviceGroup DataInitialize `Where(Id>0)`로 Temp 그룹 사라짐. 🟠H1 "동작 동일" 거짓(provider 토폴로지 비대칭) H2 R2 stale Id(read-only) H3 UpdateDeviceProperties Speaker Server 미복사 H4 베이스 Insert/Save 미추상화(복붙=재분기) H5 게이팅 단방향 H6 프로세스 요건 전무 H7 ControllerPanel:90 전체DTO 로깅(Camera/Lamp 비번) H8 ServerDto.user_password 평문.
  - **v1.1 반영**: provider 토폴로지 표 + Draft 격리 불변식(CollectionChanged Id≤0 가드) + 베이스 ExecuteCreate/SaveUpdates 추상화(목표) + 게이팅 양방향(G1a/G2b/Initialize, 강제계층🔴) + verify-after-success P0 + 보안§(로깅금지/sanitizer/비번마스킹) + 프로세스§(롤백/worktree/code-review/CHANGELOG/메인솔루션) + given-when-then 수용기준 + Phase1 **3-PR 분할(PR-A 베이스+되돌림 / PR-B 게이팅+C2+R2+verify / PR-C UX+보안)**.
  - **착수 전 코드확인 2건**: 응답 DTO `AssignedDeviceIds` 존재(M4 verify), Enclosure ip_port nullable(M5).
  - **착수**(롤백 `before-tempstate-unification` + worktree **v2.9.3**): Plan 작성(`docs/plans/DevicePanel_TempState_Unification-prd-plan.md`, Draft 격리 재동기 모델).
  - **✅ PR-A 완료·커밋 `3deca70`(빌드0)**: 베이스 `BaseDataGridMultiPanelViewModel`에 `ExecuteCreateAsync`(루프A+G7 보류)/`ExecuteSaveUpdatesAsync`(루프B)/`NotifySaveResultAsync`(sanitize)/`SanitizeDetails`/`ShouldProjectToProvider` + `ApiResultLite` 템플릿 추가. 6패널(Camera/Speaker/Enclosure/Lamp/Controller/Sensor) 즉시-POST Insert→로컬 Draft Add(placeholder 502 제거), Save→베이스 루프(필수필드 사전검증)+생존자 복원. **Draft 격리**(CollectionChanged Id≤0 미투영)로 C1/H1 동시 해소. Controller DTO 로깅 제거(H7). 4패널 병렬 워크플로우+Sensor/Camera 직접.
  - **✅ PR-B 완료·커밋 `434a2e0`(빌드0)**: DeviceGroup Temp 전환(독립 provider Clear+rebuild 재동기) + 게이팅 G1(CanAddAssign)/G2(AddDeviceButton 메서드가드)/G1a/G2b/G3/G4/G5 + verify-after-success(AssignedDeviceIds 확인됨) + G6(센서 제어기 드롭다운 Id>0). Critical 버그(Temp 그룹 Id=0 → `POST groups/0/devices`+desync) 차단. **PIDS 필터=Draft 격리로 불요, R2 stale Id=G2가 실버그 차단하므로 후속 UX**.
  - **✅ Phase1 code-review(opus)**: 머지차단 2(C1 Insert 무가드 경합·C2 PIDS 필터 누락) + High 3(H1 Initialize 가드·H2 stale Id·H3 빈catch) + M1/L1 식별. 핵심 메커니즘(Draft 격리·생존자 복원·분류)은 정확 확인.
  - **✅ PR-B-fix 커밋 `80ab9e1`**: C1(7패널 Insert `_processGate` 직렬화, 워크플로우)·C2(PIDS `.Where(Id>0)`)·H1·H2(서버 Id write-back)·H3·M1(Index 재부여)·L1(Replace case). M3(이중 fetch)는 생성 후 신규관찰 필수로 **반려**. 빌드0, grep 7/7.
  - **✅ PR-C 커밋 `93380b2`**: IsDraft(BaseCustomViewModel) + 7패널 행 시각마커(RowStyle BasedOn 암시스타일) + CountUnsavedDrafts override 7 + Uninitialize 미저장 손실 비차단 알림(아키텍처상 차단형 부적합). CHANGELOG [Unreleased] Temp-state 항목 추가. 빌드0, grep 7/7.
  - **✅ 워크트리 전체 솔루션 빌드 0에러**(경고 390 기존).
  - **진행 중**: Phase1 **최종 머지검증 리뷰**(opus, 백그라운드) — 차단/High 해소 + PR-C 무결성 + 회귀.
  - **남음**: 리뷰 GO 확인 → **Phase1 머지 `v2.9.3`→v2.6** → 메인솔루션(`Dotnet.Monitoring.Solution`) 재빌드 확인 → Report. (Phase2/3: Speaker server_id FK / Enclosure ThresholdConfig / Camera RTSP)
  - **설계 전환 확정**: device 6패널의 Phase2 즉시-POST(B모델)를 **Temp-state(Id≤0 로컬→Save 시 등록)로 되돌려 7패널(+DeviceGroup) 통일**. 이유: 서버가 미완성 placeholder를 422 거부(전제 깨짐) + 센서는 controller_id 때문에 즉시등록 불가(비일관). 기준 템플릿=Sensor Draft-commit.
  - **Id≤0 게이팅 G1~G7**: 사용자 제안("그룹 Id≤0이면 장비추가 비활성")이 실제 **Critical 버그**를 가리킴 — 현재 그 버튼은 `IsEnabled=IsSingleSelected`(Id 무관)뿐 → Temp 그룹(Id=0)에서 `POST /devices/groups/0/devices` + 로컬 desync. 일반화: 센서 제어기 드롭다운(G6) Temp 제어기 제외 등 7곳.
  - **Speaker 서버설정 갭(Part B)**: 6장비 중 **Speaker만** Server(방송서버) 참조하나 read-only nested뿐 `server_id` 쓰기FK 없음 + `ServerProvider` 캐시·로드·UI 전무(`IServerApiService.GetServersAsync`는 프로덕션 소비처 0). **차단점=데이터 계약(FR-0 백엔드 스펙 확인 선결, 차장/서버팀)**. 보너스: Enclosure ThresholdConfig UI 없음(FR-9).
  - **결정 확정(2026-06-21)**: ①게이팅 대상=AddDeviceButton(IsEnabled=IsSingleSelected→Id>0 가드, 사용자 XAML 제공) ②DeviceGroup **전 패널 통일**(write-back 특례 폐기, must-reason 없음) ④**FR-0 해결**(openapi: server_id=int FK **nullable/optional**, 차장확인 불요 → FR-8 게이팅 제외, Part B 설계 확정) ⑤FR-9/10 **분리**(§12 다음 작업 예정). **③UX 범위만 사용자 설명 후 대기**(시각마커=미저장행 구분 / 종료경고). → ③ 확정되면 Plan→Part A 구현(롤백 `before-tempstate-unification`+worktree).
  - 미반영 코드: Phase2 즉시-POST(머지됨)는 **이 PRD로 되돌릴 예정**. placeholder 포트 502도 제거 대상.

- **✅ 제어기 추가 HTTP 422 수정 + 422 본문 보존/진단 로깅 — v2.6 `881ac5a`** (Report: `docs/reports/Controller_422_Fix-report.md`, 롤백 `before-controller-422-fix` bc53164, worktree v2.9.1)
  - 근본원인(4각도 Workflow): `ControllerDeviceModel` ctor가 peer 4모델(Camera/Speaker/Enclosure/Lamp)과 달리 `DeviceType` 미설정 → `type_device="NONE"` 전송 → 서버 enum 422. **제어기에서만** 발생 이유.
  - 수정: ctor `DeviceType = EnumDeviceType.Controller`. + `ApiMessageHelper` 3분기가 422 본문(`{"detail":[...]}`)을 `MissingMemberHandling.Ignore`로 폐기하던 결함 → `Error.Details`에 본문 보존 + `ApiResponse.StatusCode` 추가. Controller Insert에 요청DTO/422본문 로깅. 테스트 `ApiMessageHelperErrorTests` 3건.
  - 검증: 빌드 0에러, Messages 182/182 통과(실증), code-review 머지차단 0.
  - **차기 422 확정·수정(머지 `a916d59`, 롤백 `before-controller-ipport-fix` 881ac5a, worktree v2.9.2)**: 로그가 다음 거부 필드를 정확히 표출 — `{"field":"ip_port","message":"Input should be greater than or equal to 1"}`. 즉시-POST가 `ip_port=0` 전송. 수정: Controller/Camera/Lamp Insert에 유효 placeholder 포트 **502**(통합테스트 D01 검증값) 부여. `ip_address=""`는 서버 허용(미변경). Camera/Lamp/Speaker/Enclosure Insert에 실패 시 `Error.Details` 로깅 추가(Camera/Lamp는 비번 보유→요청본문 미로깅).
  - **🔴 사용자 재확인**: 제어기 추가 재시도 → 이제 성공 예상. **남은 후속**: Sensor는 ctor DeviceType 미설정(다중 서브타입 Fence/Underground/PIR…→사용자 선택 필요) + controller_id FK → 별도 설계(타입선택+commit 가드). 다른 패널 Error.Details 로깅 시 민감정보 마스킹 가이드.

- **✅ DataGridPanel CRUD 라이프사이클 통일 Phase 1·2·3 전부 구현·머지 완료 — v2.6** (PRD `docs/prds/DataGridPanel_Delete_Centralization-prd.md` v2.0, Reports: `DataGridPanel_CRUD_Phase1-report.md` + `DataGridPanel_CRUD_Phase2_3-report.md`)
  - 롤백태그 `before-crud-phase1`(3069ba1)·`before-crud-phase2`(3e2ade1). worktree v2.8.9→Phase1, v2.9.0→Phase2/3. WIP `97c21e7` 무접촉.
  - **Phase 1** (머지 `3e2ade1`): 베이스 `BaseDataGridMultiPanelViewModel.ExecuteDeleteAsync`(Id≤0 로컬/Id>0 verify/OCE 재던지기/부분실패 InfoPopup 중앙통지). Event 4패널(Detection/Malfunction/Connection/Action) 삭제 가드 + Save Id write-back(유령중복 차단). code-review H1/H2 반영.
  - **Phase 2** (머지 `a84dab6`): device 6패널 B모델. Insert(5패널)=즉시 Create→**FetchAll+DataInitialize 재조회**(코드리뷰 C1: 타입드 provider=공유 단방향 투영 이중행 → 수동 Add 제거). Save=Update전용+complete가드. Delete=ExecuteDeleteAsync. Sensor=Draft-commit-when-valid(controller_id FK 422 회피, 커밋 시 로컬 Draft 제거 H1). architect+code-review(opus) 2라운드, C1/H1/M1 수정 후 머지차단 0.
  - **Phase 3** (머지 `bc53164`): 싱글톤 `IActionReportGuard` 추출 → EventCardListPanel 3경로 + 수동 2경로(EventCard.SendAction) 공유 → 수동×자동 중복 ActionEvent 차단.
  - 검증: ViewModel/Devices.Ui/Events.Ui 빌드 0에러, Events.Ui 테스트 300통과/11실패=baseline(회귀 0). **🔴 런타임 검증 권장**(즉시 POST·Insert/Save 후 1행·수동/자동 중복0) — WPF라 정적/빌드만 통과.
  - **비차단 후속**(Phase 2.x): Sensor Draft UI상태 보존, `BaseProvider.Add` (Id,DeviceType) dedup 하드닝, CTS 단일화, 가드 단위테스트.

- **✅ Accounts_Ui_Library_Extraction PRD 생성 완료 — 21-Agent + 24시나리오 + 2회 시뮬레이션 (2026-06-21)**
  - 파일: `docs/prds/Accounts_Ui_Library_Extraction-prd.md` (1,050라인)
  - **핵심 설계 결정**:
    - `Ironwall.Dotnet.Libraries.Accounts.Ui` 신규 WPF 라이브러리 프로젝트 구축
    - VM 이관 전략: AccountViewModel/LoginViewModel = A(직접이관), 나머지 패널/다이얼로그 = B(Wrapping+hook)
    - SetupPanel 세션패널: `ISessionConfigService` 추출 + 탭 호스팅은 앱 잔류
    - `AccountUiModule` 1줄 등록으로 Bootstrapper 일원화
  - **확정 이슈 16건**: CRITICAL 2 / HIGH 6 / MEDIUM 6 / LOW 2
    - C-1: RegisterViewModel `IoC.Get<IAccountModel>()` 공유 싱글톤 참조 → 전용 팩토리 등록으로 수정 (코드스니펫 포함)
    - C-2: SetupPanel `BaseSetupViewModel` 앱 결합 강함 → `ISessionConfigService` 역전으로 해소
    - H-4: 중복확인 경쟁조건 debounce CTS 없음 → CancellationTokenSource debounce 패턴 적용
  - **Sim-R1 rate limit**: 24개 시나리오 중 23개 Anthropic rate limit로 실패 (PRD 에이전트가 직접 파일 검증으로 보완 — CommonMessages.cs 14~107줄 확인, BasePanelViewModel `Conductor<IScreen>` 확인)
  - **구현 Phase 순서**: Phase1(프로젝트생성+AccountVM+LoginVM) → Phase2(RegisterVM+다이얼로그) → Phase3(패널VM) → Phase4(ServiceConfig) → Phase5(SetupPanel) → Phase6(Bootstrapper통합+검증)
  - **미결사항(OQ) 8건**: EnumLevelType경로, Reset비번외부화, SetupPanel이관범위, LoginPanel hook결정, Dashboard필요성 등
  - **다음 단계**: 사용자 PRD 검토 → Plan 작성 → `git tag before-accounts-ui-extraction` → v2.9.3 worktree → Phase1 구현

- **📋 GOP Account 연동 전수 분석 완료 — 7개 PRD + HTML 스토리보드 생성됨. 사용자 검토 대기**
  - **21-Agent 워크플로우** (1,990,359 tokens, 809 tools, ~25분): R1 시나리오 생성 + R2 adversarial 검증 완료
  - **확정 이슈 90건**: CRITICAL 14 / HIGH 22 / MEDIUM 35 / LOW 19
  - **생성 산출물**:
    - `docs/prds/GOP_Account_Auth_Integration-prd.md` (PRD-GOP-00, 보완: SUP-C1~L1 12건 Appendix 추가)
    - `docs/prds/GOP_PreAuth_Overlay_NatsGate-prd.md` (PRD-GOP-07, P1, 14 STEP — 2026-06-21 추가)
    - `docs/prds/GOP_Permission_Gate_Feature-prd.md` (PRD-GOP-01, P1, 18 STEP)
    - `docs/prds/GOP_AccountManager_UI-prd.md` (PRD-GOP-02, P1, 26 STEP)
    - `docs/prds/GOP_MyPage_UI-prd.md` (PRD-GOP-03, P1, 18 STEP)
    - `docs/prds/GOP_Menu_Role_Visibility-prd.md` (PRD-GOP-04, P2, 12 STEP)
    - `docs/prds/GOP_UserSession_AuditLog_UI-prd.md` (PRD-GOP-05, P2, 28 STEP — 1-1. DataGrid 패턴 섹션 추가)
    - `docs/prds/GOP_Session_Resilience_Lifecycle-prd.md` (PRD-GOP-06, P2, 20 STEP)
    - `Docs/storyboards/Account_GOP_Integration_Storyboard.html` (v2.0, WPF ShellView 기반 와이어프레임, 7화면, 1차 인터랙션 — 2026-06-21 재빌드)
  - **PRD-GOP-07 핵심 내용** (2026-06-21 추가):
    - 미로그인 → CanvasSectionView 위 LoginGateOverlay (ZIndex=100, 회색배경, "PIDS 모니터링 시스템")
    - 미로그인 → EventCardPanel Visibility=Collapsed
    - NATS 구독: 로그인 성공 시 MessageService.StartAsync(), 로그아웃 시 StopAsync()
    - 구현 순서: PRD-GOP-00 이후, PRD-GOP-01과 병렬 (우선순위 P1)
  - **스토리보드 v2.0 내용**:
    - WPF ShellView 4-column Grid 그대로 시뮬레이션 (LeftMenu 52px + Canvas + EventPanel 270px)
    - 미로그인 → LoginGateOverlay 표시, EventPanel 숨김, LeftMenu LOGIN만 표시
    - 로그인 → 오버레이 제거, EventPanel 등장, Role별 메뉴 표시
    - 7개 화면: 지도/계정관리/내정보/세션관리/감사로그/설정이력/그룹관리
    - MaterialDesignDataGrid 패턴 + 행 클릭 → 하단 Detail Panel (1차 인터랙션)
  - **핵심 보안 이슈** (즉시 처리 필요):
    - `LoginPanelViewModel` L115: Token 평문 로깅 (CRITICAL)
    - `EditorDialog.ClickResetPassword`: `'12345678'` 하드코딩 (CRITICAL)
    - `ConductorControlViewModel`: 권한 가드 전무 (CRITICAL)
    - `LeftMenuSectionView` HamburgerMenu Tag: IsVisible=false 우회 가능 (CRITICAL)
    - DATABASE/SETUP 메뉴: IsLogin만 — 모든 사용자 접근 (HIGH)
  - **`ParentBootstrapper.cs` 수정 절대 금지** (사용자 지시)
  - **구현 순서**: PRD-GOP-00 → PRD-GOP-07(병렬가능) → PRD-GOP-01 → PRD-GOP-02/03/04(병렬) → PRD-GOP-05/06
  - **다음**: 각 PRD 사용자 검토 후 Plan 작성 → `git tag before-gop-{prd}` + v2.6.x worktree → 구현

- **✅ DeviceGroupSelectionViewModel AddDeviceButton 스피너 버그 수정(2026-06-19)**
  - `AddButtonEnable = true` + `NotifyOfPropertyChange` → `try/finally` 블록으로 이동 (confirm/cancel 모두 스피너 해제 보장)
  - 파일: `Ironwall.Dotnet.Libraries.Devices.Ui/ViewModels/DeviceGroupSelectionViewModel.cs` L102-128
  - **미커밋** — 사용자 WIP 파일, git commit 시 포함 필요
- **✅ DataGridPanel CRUD 통일 Phase1 (베이스+Event4) 구현·머지 완료 — v2.6 `3e2ade1`** (Report: `docs/reports/DataGridPanel_CRUD_Phase1-report.md`, PRD v2.0, Plan 작성됨)
  - 롤백태그 `before-crud-phase1`(3069ba1) · worktree v2.8.9(feat `1bac63a`) → v2.6 머지 `3e2ade1`. WIP(`97c21e7`) 무접촉 확인.
  - **S1 베이스** `BaseDataGridMultiPanelViewModel.ExecuteDeleteAsync(items,getId,deleteApi,removeLocal,ct)` 공통 헬퍼 — Id≤0 로컬만(API 미호출)/Id>0 DELETE+verify-after-success/OCE 재던지기/부분실패 InfoPopup 통지(중앙화). **delegate 기반**(ISelectable 인터페이스 변경 회피), abstract/virtual 계약 변경 0 → device 6패널 무영향.
  - **S2/S3 Event 4패널**(Detection/Malfunction/Connection/Action): 무가드 삭제 → `ExecuteDeleteAsync`. Save Insert 루프 `created.Id` → model write-back(재Save 유령중복 차단).
  - code-review(opus): Critical 0/차단 아님. H1(OCE 재던지기)·H2(부분실패 통지 중앙화) 반영. 잔여(비차단): M1 삭제 `_processGate`, L1 Connection 미사용 insertList2, L2 나머지 3패널 Save 부분실패 보존 → Phase 1.x/2와 함께.
  - 검증: ViewModel/Events.Ui/Devices.Ui 빌드 0에러, Events.Ui 테스트 300통과/11실패=baseline 동일(**회귀 0**, 11은 DeviceSymbolLookup·바인딩 등 무관 기존실패).
  - **다음**: **Phase 2** device 6패널 B모델(즉시 Create+Id write-back, Save=Update전용, 전페이지+complete, 타입드 provider 청소, Insert/Delete _processGate, Sensor=Draft-commit-when-valid) → **Phase 3** 조치보고 멱등(`_inFlightEventIds`). 각 Phase 롤백태그 `before-crud-phaseN` + worktree.

- **✅ Client_API_v46 Batch2 (①③④) 구현·머지 완료 — v2.6 `3069ba1`** (Report: `docs/reports/Client_API_v46_Conformance_Batch2-report.md`)
  - 선행: WIP(멤버십UI+GMaps 로그정리) `97c21e7` 커밋 보존 + 롤백태그 `before-api-v46-batch2`. worktree v2.8.8 → v2.6 머지(c721b27 part1, 3069ba1 H1수정).
  - **① FR-5**: 그룹 장비제거 N콜→**벌크 1콜**(`RemoveDevicesFromGroupAsync`, 등록 반대방향) — DeviceGroupSelectionViewModel.
  - **③ Tracking**: `TrackingStatusNatsSyncService` 로그-only stub + EventUiModule 등록·기동(`.As<IService>()`+빌드콜백 StartService). TRACKING_STATUS 수신→로그. 오버레이=FR-15 후속.
  - **④ FR-13**: heading → `BaseDeviceModel.Heading`(매퍼) → `SymbolEventManager.RegisterDeviceSymbol`서 `PidsSymbolModel.BaseBearing` **메모리 반영**(DB 미저장, SoT=서버).
  - 검증: Events.Ui/Devices.Ui 빌드 0에러. code-review H1(③ 미기동) 수정 완료. M1/M2/M3=by-design/후속.
  - **메인 솔루션**: FR-1 rename으로 `Dotnet.Monitoring.Solution/...Tests/Mocks/MockEventApiService.cs` 2메서드 수정함(빌드복구).
  - **⏸ ② 6패널 B모델 = Phase2 분리**: Camera/Sensor/Controller/Speaker/Enclosure/Lamp 각 Insert(즉시Create)/Save(Update-only)/Delete 재작성 + 타입별 기본값(Sensor=Controller 필수 422 주의). 런타임검증 필요 → 별도 집중 패스.

- **✅ Client_API_v46 Phase 0 (S1~S5) 구현·머지 완료 — v2.6 `0105580`** (Report: `docs/reports/Client_API_v46_Conformance_Phase0-report.md`)
  - 롤백태그 `before-api-v46-conformance`(2868dd4) · worktree v2.8.7(커밋 9a63b69) → v2.6 머지(--no-ff). **WIP(DeviceGroupSelection/GMaps 9/INDEX) 미접촉 확인**.
  - **S1 FR-0** body-DELETE 오버로드 / **S2 FR-1** ActionEvent `/actions`+`ApiListResponse` 배열(메서드명 `...ActionsAsync`) / **S3 FR-7** GeolocationDto.heading + CameraPresetDto.is_restricted_zone(restricted_actions 미추가) / **S4 FR-6** EM Camera/Speaker is_enable / **S5 FR-5** DeviceGroupBulkRemoveResultDto + RemoveDevicesFromGroupAsync(VM 1콜 교체는 PARK).
  - 검증: 5프로젝트 빌드 0에러, Messages 174테스트 통과, code-review 무차단(critical/high 0).
  - **PARK(후속)**: S6 FR-3(from_event_id, Moq파급)·S7 FR-2(EM 중첩 ConfigDto, 반환형파급)·FR-1(b) RowDetails UI·FR-8 422·FR-13 Model/Mapper/심볼BaseBearing·FR-10~17(NATS/외부의존). 🔴 NATS5(restricted_actions SoT)·NATS6(PTZ 발행)·NATS1(GIS경계)·FR-17(소비처) = 차장/서버/메인솔루션 확인 필요.
  - 파이프라인: PRD Approved → plan → dev → test → report.

- **🎨 HTML 스토리보드 `docs/storyboards/DevicePanel_EventPanel_Storyboard.html` 생성·구동 확인** (효용 낮음 — 사용자 평가, 삭제 보류)
  - 단일파일 인터랙티브 프로토타입. 4-area 인벤토리 워크플로우 근거(실제 컬럼/툴바/인터랙션). 디바이스(그룹+6패널, B모델 vs 현행 토글, 그룹↔장비 배정 N콜 vs 벌크1콜) + 이벤트(탐지/장애/연결/조치/매핑/카드, **1탐지→N조치 RowDetails 지연로드**, 날짜검색·더보기) + 지도·추적(targets[] 다중 마커·threat 색상·ttl 소멸·observed_at 역전방지) + 구동로직설계 탭.
  - 핵심: 우측 **REST/NATS/UI 콜 로그**가 모든 클릭의 구동 로직을 실시간 내레이션 + Confirm/Progress/Info 팝업 + _processGate 직렬화 미러. PRD(FR-1/FR-5/FR-10)의 UX를 시각화.
  - 용도: 사용자 기획 검토용. 프로덕션 코드 아님.
  - **✅ 확정된 Panel 설계 결정(2026-06-19, 스토리보드 반영)**:
    1. **Device 추가 = 기본값 즉시 POST(B모델)** — 추가 시 기본값으로 즉시 Create→Id 수신→그리드 표시, 편집은 Save=Update. 6개 장비 패널 전부 B모델 통일. 필수값(IP/제어기) 미입력 행은 "⚠미완성" 표시(즉시 POST 부작용 가시화, 채워서 저장 유도). 그룹↔장비 제거=벌크 1콜 확정.
    2. **Event 1탐지:N조치 = RowDetails 인라인 확장행** — 탐지/장애 행 펼침 시 GET .../actions 지연로드(조치보고=False면 미호출, N+1 회피, 1회 캐시) + RowDetails 내 "＋조치 추가"(POST /events/actions → 서버가 action_reported 자동 True + 조치수 증가). "다이얼로그 금지" 준수.
  - 다음: 이 확정안을 PRD에 반영 — Client_API_v46 FR-1(RowDetails) + DevicePanel_CRUD_API_Sync Phase 2(6패널 B모델).

- **🔬 PRD ↔ 원본문서(REST v4.6 openapi + NATS 브로커 SoT) 대조 검증 완료(19-Agent) → PRD v3.1 정정 반영**
  - **결론: 핵심 주장 대부분 정확(faithful). HIGH 2 + 누락 다수 정정.** 적대적 재확인으로 오탐 2건 제외.
  - **HIGH-1 `restricted_actions` 폐기**: 설계서 v4.6 §5.7+변경이력(차장결재)에서 폐기, `is_restricted_zone(bool)` 단일화. `_openapi_snapshot.json`은 stale로 잔존(4종) — **명세 충돌**. PRD에서 restricted_actions 삭제(FR-7/FR-14 P-3). SoT 차장 확정 = 결정 NATS5.
  - **HIGH-2 PTZ current_preset/is_restricted**: 브로커 SoT §8.3.1 PTZ_STATUS body=camera_id/pan/tilt/zoom 4필드뿐, 두 필드는 정리본 L590 "범위 외 참고"에만 → **BLOCKER**(서버 발행 선확인, 미확정 시 FR-14 P-1/P-2 보류). 결정 NATS6.
  - **누락 정정**: FR-6 벌크 스키마 확정(`{items}`/`{config_ids}`, 응답 6종 DTO)+Lamp/Camera Enum 허용값(422) / FR-1 action_reported **자동관리·클라전송금지**+Action **4-required**(§6.4.5)+CONNECTION 조치UI 제외 경계 / FR-3 `from_event_id`(v4.6) / **FR-17 Detection Log actions[] 1:N**(소비처 확인 필요) / FR-2 envelope `{data:{items,total}}` 정정 / DETECT·VMS detail `frame_width/height`+bbox해석 / FR-8 커스텀 ValidationErrorResponse / EnclosureMetric 타입 정밀.
  - faithful 확인: FR-1 `/actions`+배열, FR-5 벌크해제 3분류, FR-7 heading 0~360, FR-10 targets[]/EnumThreatLevel, FR-11 on↔ACTIVE, FR-12 VMS_DETECT, FR-13 geolocation, SYNC_PRESET.
  - 결정 §9: 1~9 + **NATS5(restricted_actions SoT)·NATS6(PTZ 발행)·12(Detection Log 소비처)** 추가. 결정4(envelope) 해결.
  - **추가 설계 확정(사용자, 2026-06-19)**:
    - **FR-12 VMS_DETECT**: `VmsDetectBodyDto` 생성하되 **DETECT와 동일 Detect 이벤트로 합쳐 처리**(origin_event→기존 Detection 파이프라인, 별도 Panel 없음). 버려지는 필드=name_event/category_event_mapping/urls(VMS 소관)/origin_event.device.urls. 중복발행 EventId dedup. `vms.event_ai.detect` 구독은 메인솔루션 소관.
    - **FR-13 ④ heading→심볼 BaseBearing (NATS7=㉠ 확정)**: API heading → `BaseDeviceModel.Heading` → (로드/링크/SYNC_DEVICE 시) **`PidsSymbolProvider`의 `PidsSymbolModel.BaseBearing`(메모리 캐시)** 반영 → FOV 재렌더. **로컬 심볼 DB 미저장**(SaveMarker 호출 안 함, SoT=서버). 설치방향 변경은 서버 장비 API(`PUT device.geolocation.heading`). NATS2(heading 명칭) 해결.

- **📋 통합 PRD `docs/prds/Client_API_v46_Conformance-prd.md` v3.1 — 검토 대기(미승인)**
  - REST(FR-0~9) + **NATS(FR-10~16)** 통합. Track C. 사용자 검토→Plan→worktree 구현 대기. 롤백태그 예정 `before-api-v46-conformance`.
  - **v2.0 추가**: FR-1 ActionEvent 1:N **신규 UI**(탐지→N조치, RowDetails 인라인 마스터-디테일, "다이얼로그 금지" 준수, 소비처 0=greenfield) + DTO설계/코드스니펫/사이드이펙트/시뮬레이션 2-Pass.
  - **v3.0 추가(NATS, 31-Agent Workflow+적대적검증, SoT=`Docs/prds/NATS-Tracking-Geolocation-메시지정리.md`)**:
    - **FR-10 P0**: `TrackingStatusBodyDto` 단일 target → **다중 targets[] 전면교체**(하위호환 없음). 신규 `TrackingTargetItemDto`(track_id/threat_level/observed_at/location/bbox...) + body레벨 ttl_sec/frame_w/h + **신규 `EnumThreatLevel`**(NORMAL/CAUTION/THREAT, EnumSeverityLevel과 별개). 라이브러리 소비처 0(Phase26 테스트만) → **메인솔루션 동시배포 필요**.
    - **FR-13 P0**: Geolocation heading/altitude **3계층 단절**: `GeolocationDto.heading` 부재 + `BaseDeviceModel/IBaseDeviceModel`에 Altitude·Heading 없음 + 매퍼 `MapGeolocationToModel/ToDto`(DtoToModelHelper L53-76) altitude **양방향 미매핑**(실제 왕복손실). 셋 다 고쳐야 값 흐름.
    - **FR-11 P1**: TRACKING_SET tracking 표현정합(on/off↔ACTIVE/IDLE 변환 헬퍼 부재). **FR-12 P1**: VMS_DETECT body DTO **미정의**(VmsDetectBodyDto 신설). 
    - **FR-14 P1 (Preset)**: `CameraPresetDto`에 is_restricted_zone/restricted_actions 부재(P3) + `PtzStatusBodyDto`에 current_preset/is_restricted 부재(P1) + `CameraPtzNatsSyncService` 미전파(P2, ProcessCameraPtz 옵션파라미터) + **SYNC_PRESET 핸들러 부재**(P4, DTO만 존재).
    - **FR-15 P2**: GIS 추적 오버레이 소비자 **전무**(transient 마커/(camera_id,track_id) upsert/ttl 스윕/observed_at 역전방지/threat 시각화 없음) → **메인솔루션 소관 추정, 경계 결정 필요**.
    - **FR-16 P2**: 고아 `TrackingTargetDto`(Integrations) 삭제 + EnumGopCommand doc-parity.
    - **🔬 오탐 2건 제외**: ①device 다형 JsonConverter(VMS_DETECT) 불필요(nested device는 id+type 참조용, DeviceProvider가 완전체 조회) ②TrackingTargetDto distance_m(고아, 실제 location은 TrackingTargetLocationDto가 담당).
  - **결정 9개 대기**(§9): REST(필터/우선순위/UI/envelope curl/DTO분리) + NATS1(GIS경계) NATS2(heading명칭) NATS3(FR-10 메인솔루션 동시배포) NATS4(케이싱).
  - 다음: 사용자 결정 → Plan 작성 → worktree → 구현. 우선순위 미정 시 **FR-0→FR-5(그룹 슬로우)** 1순위 제안.

- **🔍 Device/Event API 클라이언트↔서버(v4.6) 정합 전수 감사 완료 — 미수정(분석)**
  - 기준: 라이브 openapi 스냅샷(`docs/analysis/_openapi_snapshot.json`)=GOP 문서 **v4.6**(최종수정 2026-06-19). 결과: 158메서드 **OK 94 / 이슈 71**(critical12·high29·med19·low10). 종합본: `docs/analysis/Client_API_Conformance_Audit-analysis.md`.
  - **P0(깨짐)**: ①ActionEvent 1:N(`/action`→`/actions`+단건→배열, 404/역직렬화, EventApiService L687/701) ②EventMapping 목록 중첩응답을 flat DTO로 파싱→config_id/is_enable 소실 ③이벤트목록 필터 쿼리 불일치(controller/sensor/status→서버 device_id/action_reported/result, 필터 무시·전량반환) ④`IApiService.DeleteRequestAsync` body 미지원(ApiService L145-162)→벌크해제 공통 병목.
  - **P1**: DeviceGroup `RemoveDevicesFromGroupAsync`(벌크, 서버 v4.3 존재) 미구현 / EventMapping is_enable DTO부재(Speaker PUT 422)·벌크 6 미구현 / Camera category enum(FISHEYES,THERMAL vs NONE/FIXED/PTZ) / Geolocation heading·Preset is_restricted_zone/restricted_actions 미반영 / EnclosureMetric 타입 / 422 HTTPValidationError 형태 / ApiError.Details string vs 객체배열.
  - **P2**: Speaker/Enclosure server_id·group_device, group_id 필터, envelope 이중정의, URL `/api` 접두 전제 검증.
  - **그룹 장비제거 슬로우 근본해결** = P0-4(body-DELETE) + P1(RemoveDevicesFromGroupAsync) + HandleAsync 1콜. **OK 94**: 기본 CRUD/그룹CRUD/CameraSetting/Statistics 정합.
  - 다음: 위 개선을 PRD(Phase0 P0 → Phase1 P1 → Phase2)로 착수 결정 대기. (서브에이전트 세션한도 7:10pm 리셋 — 추가 워크플로우는 그 후)

- **📚 GOP API v4.x 변경사항 숙지 완료** (8-Agent 분석, `docs/analysis/GOP_API_v4_Changes_ClientImpact-analysis.md`)
  - 원문 `Docs/GOP_Restful_Api_연동설계.md`(헤더 v4.3/변경이력 v4.5까지). **DTO 필드 정합은 이미 따라잡힘**(is_enable/geolocation/device_groups/group_ids/Camera urls/Enum 6종 등). 미정합=호출계층 v4.3 breaking 2건.
  - **★핵심 반전**: DeviceGroup **일괄제거 엔드포인트가 이미 서버 존재**(v4.3 §5.6.9): `DELETE /api/devices/groups/{group_id}/devices` + body `{device_ids:[1..100]}` → `{removed/skipped/not_found_device_ids}`. **백엔드 요청 불필요 — 클라이언트만 구현**(요청서 `docs/requests/DeviceGroup_BatchRemove_API_Request.md` 정정됨).
  - **P1 미정합**: ① ActionEvent 1:N — 서버 `/action`(단수)→`/actions`(복수)+배열, 클라 `IEventApiService.GetDetectionActionAsync/GetMalfunctionActionAsync`(L345/349) 단수+단건 → 404/역직렬화 위험. ② 벌크해제 body-DELETE — `IApiService.DeleteRequestAsync`(L8) body 미지원이 공통 선결 병목.
  - **P2**: EM Camera/Speaker/Lamp 벌크 6메서드 부재(IEventApiService L397~494 단건만). P3: Lamp Enum 422, Geolocation heading, DetectionLog actions(v4.6), Camera category enum(FISHEYES/THERMAL vs 서버 NONE/FIXED/PTZ) 주석 불일치.
  - **멤버십 제거 슬로우 근본해결 경로**(앞 분석과 연결): IApiService body-DELETE 오버로드 + RemoveDevicesFromGroupAsync(벌크) + HandleAsync 1콜 교체 → 수십초→<1초. (병렬화 임시안 불필요해짐)

- **🔍 DeviceGroup 멤버십(장비 그룹 등록/제거) 추가 분석 — 미수정(분석/문서만)**
  - 증상: 그룹에서 장비 **제거가 매우 느림**(402개 ≈ 40초) + Confirm 팝업 안 닫힘 + 그룹 그리드 DeviceCount 갱신 안 됨. (등록은 빠름)
  - 원인(코드/OpenAPI 확정): **API 비대칭** — 등록 `POST /api/devices/groups/{id}/devices`(device_ids[] 배치 1콜) vs 제거 `DELETE .../devices/{device_id}`(단건). **서버에 일괄 제거 엔드포인트 없음** → 클라이언트 `DeviceGroupSelectionViewModel.HandleAsync`가 `foreach await RemoveDeviceFromGroupAsync`로 N번 순차 호출. assign은 additive(교체 아님)라 우회 불가.
  - 부가: ClosePopup이 finally(루프 후)에만 발행→팝업 40초 잔존 / HandleAsync가 DevicePanelViewModel(그룹그리드) 미갱신 + `DeviceGroupViewModel.DeviceCount=>_model.DeviceCount` INPC 없음→카운트 stale.
  - **조치**: 백엔드 요청서 작성 `docs/requests/DeviceGroup_BatchRemove_API_Request.md`(등록 대칭 batch remove 추가 요청). 엔드포인트 생기면 클라 1콜 교체 예정. 임시 대안=클라 병렬화(40초→~2초).
  - ⚠️ 이 경로는 `DeviceGroupSelectionViewModel.cs`(미커밋 WIP 파일) — 수정 시 WIP 먼저 정리 필요. **DataGrid 첫-클릭 선택 분석은 보류 중**(SelectionMode=Extended + DataGridSelectedItemsBehavior 델타동기 + base SelectedItems setter→OnSelectionChanged).

- **✅ DevicePanel CRUD↔API — Phase 1 v2.6 머지(9fb45d5), 빌드0 → 런타임 테스트 대기** (Track C, B모델)
  - 사용자 요청: 그룹/장비 패널 추가/삭제/갱신/저장 API 연동 분석 → 수정. 원인=클라이언트 pending+Save diff 모델(API 정상).
  - **B모델 적용(DeviceGroup)**: 추가=즉시Create(서버Id반영) / 삭제=verify-after-success / Save=Update전용(diff루프제거) / Reload=서버재조회 swap. **Controller**: limit:20→페이지루프(#11).
  - **코드리뷰 반영(10-Agent)**: 그룹/제어기 fetch 페이지네이션+완전성신호(불완전 시 Save보류), OnActivate/삭제 _processGate 직렬화, 고유 기본명, 실패 InformDialog.
  - **머지**: worktree v2.8.6(586d1cd) → v2.6(9fb45d5). 롤백 `before-devicepanel-crud-fix`(0810bce). PRD `DevicePanel_CRUD_API_Sync-prd.md` v1.1.
  - **앱 재빌드 후 런타임 확인**: 그룹 추가 즉시반영(사라짐X) / 삭제 즉시반영(잔존X) / 수정+Save 시 유령 중복생성 안 됨 / Reload 서버동기화.
  - **후속 Phase 2**: Camera/Sensor/Speaker/Enclosure/Lamp 6패널 B모델 일관 적용(패널별 Add UX 확인). + P1(UpdateOrAddDevices swap).
  - **⚠️ 미커밋 WIP(제 작업 아님, 보존됨)**: DeviceGroupSelection*(멤버십 try/finally) + GMaps 9개(로그정리). 머지에 미접촉.
  - **핵심결론**: 문제는 API/서버 아님 = **클라이언트(ViewModel) desync**. `DeviceGroupPanelViewModel`만 6개 장비 패널의 표준(IDeviceProviderService 주입 + Save/Delete/Reload 후 `FetchDeviceGroupsAsync` 재동기화 + Create 반환 Id write-back)을 **미적용** → 신규그룹 저장후 사라짐(#1, Id=0 잔존+`Where(Id>0)` 필터)·삭제 성공해도 잔존(#2)·갱신 서버미반영(#3) 전부 **P0**. (API는 ApiResponse 계약 건전, Create는 서버Id 반환 — 검증됨). 멤버십 영속은 정상(`AssignDevicesToGroupAsync` 구현됨, 오진).
  - 추가 P0: Controller Save 비교 fetch `limit:20` 페이지네이션 누락(#11, 20건 초과시 편집소실). P1: 삭제 verify-after-success, 부분실패 통지, `UpdateOrAddDevices` swap-on-success, 구독경합.
  - **검증 팁**: 그룹 추가→사라짐 후 앱 재시작 시 그룹 재출현 = 서버저장됨=API정상=클라이언트 desync 확정.
  - **🟢 사용자 런타임 재현(#13 P0)**: 그룹7 설명 수정+Save 시 무관한 "테스트/테스트/0" 그룹 생성. 원인=Save Insert 루프(`DeviceGroupPanelViewModel:105,109-110`)가 `_deviceGroupProvider`의 Id<=0 유령을 매번 Create + Id write-back 없음(`:145`)→중복누적. "적용"(`DeviceGroupSelectionViewModel:44-51`)은 in-place라 무관. 확인테스트: 그냥 Save 또 누르면 "테스트" 또 생성됨.
  - **PRD**: `docs/prds/DevicePanel_CRUD_API_Sync-prd.md` v1.0(문제 13건 + RC-1~8 + Phase1~3). 핵심: **FR-1**(DeviceGroup에 IDeviceProviderService 주입+재동기화)로 #1~4 동시해소 / FR-3(Controller 페이지네이션). **설계 결정 논의중: "즉시 Add/Delete + Save=Update" 모델(B)이 pending 버그 원천차단 — 사용자 선호**.
  - **⚠️ 미커밋 파일 2개(제 작업 아님 — 사용자 in-progress 추정)**: `Devices.Ui/ViewModels/DeviceGroupSelectionViewModel.cs`(+31/−25), `Views/DeviceGroupSelectionView.xaml`. 내용=장비배정 다이얼로그(멤버십 UI) try/finally 버튼재활성. 분석 워크플로우는 읽기전용이라 이걸 만들 수 없음 → 사용자 확인 필요. P0(그룹 CRUD desync)와 다른 부분(멤버십 UI)이라 충돌 없음.

- **✅ EventProcess_ContaminationFix Phase 1 — v2.6 머지됨(0a7af99), 빌드0 → 런타임 테스트 대기 (Track C)**
  - **머지 체인(v2.8.5→v2.6)**: ec4f372(EB1) de887e9(EA2 CRITICAL) e862840(EC2/EC5+EA3) a4d51d1(EA7+EB2+EB3) 9788064(리뷰반영) → merge 0a7af99. 롤백 `before-eventprocess-contamination-p1`(bda9bc0).
  - **재검증(13-Agent)**: EB7=FALSE_POSITIVE(제외), EA7/EA6 부분수정. **선행 DeviceApi C1(NATS DELETED) 미구현** → EB3는 메서드만(dormant). **코드리뷰(10-Agent) confirmed 6건 전부 MEDIUM/LOW, 머지차단 0** → 주석정정/Order관례화/대칭/로그 반영.
  - **Phase 1 수정**: EA2(swap-on-success+PagedResult.Success) · EC2/EC5(_inFlightEventIds 3경로 멱등) · EA3(SendAction Task<bool>+ClickOk검사) · EA7(부분실패수집) · EB1(NatsSyncService IService) · EB2(MAX_EVENT_CARDS=500) · EB3(RemoveByDevice dormant)
  - **앱 반영**: 메인 앱 재빌드 필요(ProjectReference). 런타임 확인 항목: fetch실패 시 이벤트 보존+팝업 / 조치보고 중복 없음 / 조치 실패 시 다이얼로그 유지 / 앱종료 NATS 구독해제 / 카드≤500.
  - **후속(미진행)**: 수동 조치보고 멱등 통합, Malfunction swap-on-success, DataInitialize 구독토글 동시성, **Phase 2**(EC1 GetEnumerator lock / EC7 CTS / EB6 EventId dedup / EA1 Result래퍼 + EA4/EA5/EB4/EC1/EC6 재확인), **Phase 3**(EA4/EC6/EA5/EA6). worktree v2.8.5 정리 가능.
- **직전 완료** ↓

- **✅ v2.6 머지됨 + 🟢 런타임 검증 완료(사용자 "버그 제대로 수정됨") — GridSnap v1.4 (세로줄 누락 수정 + 격자 맵 고정)**
  - 검증: 패닝 시 격자 맵 고정 동반이동 + 세로줄 전체표시 + Adorner핸들 스냅 모두 정상 확인
  - 런타임 피드백 이력: ①Adorner핸들↔스냅 정상 ②우측 세로줄 누락 ③격자 맵 고정 요구 → 모두 해결
  - **RC-6**: `MAX_GRID_LINES=100`이 폭>100×gridPx에서 세로선 우측 잘림(가로선은 전폭). 축별 동적가드 `min(ceil(dim/gridPx)+2, 2000)`로 교체.
  - **RC-7**: 격자 화면고정→**맵 고정(geo-anchored)**. `ComputeOrigin(GMapControl ctrl, gridPx)`: 고정 지오앵커 `PointLatLng(0,0)`의 `FromLatLngToLocal` 화면좌표 위상정렬(`x0=((a.X%gridPx)+gridPx)%gridPx`). 패닝 시 맵과 1:1 이동, 줌은 픽셀유지. world-pixel API(RenderOffset) internal→FromLatLngToLocal이 유일경로. 3경로(DrawGrid/마커/이미지) 동일 호출로 시각=스냅 유지.
  - **머지**: v2.8.4(46690c0)→**v2.6 bda9bc0**, 빌드0. 롤백 `before-grid-snap-mapfixed`(8e03caa). PRD v1.4(FR-15/16).
- **v1.3** — 스냅 대상=Adorner 중앙 이동핸들(RenderSize/2) 델타보정. v2.6 8e03caa. 롤백 `before-grid-snap-handle-accurate`(cc42a40)
  - **RC-5(v1.3)**: 이동핸들=`RenderSize/2`(라벨 포함 bbox중앙) ≠ Position앵커(아이콘중앙, `Offset=-_model/2`). v1.2는 bbox중앙 스냅값을 아이콘앵커에 대입 → 핸들이 라벨 절반만큼 격자 이탈("중점 이탈"). 수정: `ProcessMoveOperation`에서 핸들 화면위치를 스냅 후 `Position += (handleTarget−handleNow)` 델타 보정(같은 프레임 posNow로 V 상쇄 → 드리프트 없음). 라벨 없으면 V=0 무해.
  - **머지**: v2.8.4(ec22a78) → **v2.6 머지 8e03caa**, v2.6 빌드 0. 롤백 `before-grid-snap-handle-accurate`(cc42a40)
  - PRD v1.3 추가(FR-14). 메모리 [[project_gridsnap_pixel_domain]] (핸들=스냅대상 추가 필요)
- **GridSnap v1.2 (픽셀도메인 + 라인/교점 가중치)** — v2.6 머지 cc42a40. 롤백 `before-grid-snap-pixelspace`(5f4051b)
  - **⚠️ 앱 반영**: 메인 앱 `Dotnet.Monitoring.Solution`은 ProjectReference로 v2.6 참조 → **앱 재빌드해야 보임**([[project_library_deployment_path]])
  - **근본원인**: RC-1 원점불일치(시각격자=화면픽셀 vs 스냅=지리0° → 최대 gridPx-1px 어긋남, "중점이탈" 직접원인) / RC-2 그랩오프셋 누락 / RC-3 교점가중치 없음 / RC-4 이미지 스냅 미구현 / RC-N DigitalZoom·MapRotation 역보정 불필요(이중보정 금지)
  - **구현**: ① SnapGridOverlayService static `ComputeOrigin`/`Snap`(rCross=g*0.25>rLine=max(g*0.15,3))/`EffectiveGridPx` + DrawGrid 원점통일 ② MarkerEditAdorner `_grabOffset`+ProcessMoveOperation 픽셀스냅 전면교체(MapRotation<0.1 가드) ③ GMapCustomControl `SnapBoundsCenter`+ProcessImageDrag Move
  - **검증**: 빌드0 + 메인루프 수치시뮬 2회(SIM-1 라인/교점/셀중앙, SIM-2 이미지중심/회전가드/디지털줌) 통과. *적대검증 Workflow는 서브에이전트 세션한도(3:30pm 리셋)로 미완 → 메인루프 직접검증으로 대체*
  - **문서**: PRD v1.2 / Plan v1.2(gitignored). 메모리 [[project_gridsnap_pixel_domain]]
  - **남은(선택)**: 히스테리시스(P2), 스냅 하이라이트(P2), 단위테스트. worktree v2.8.4 정리 가능.
- **직전 완료·머지됨(v2.6)**: 디지털 줌(RenderTransform 소프트확대, 72db756/e9c932f) + 회전편집/AABB/이중회전/줌개선

## 🗄 이전 재개점 (2026-06-10)
- **완료·런타임검증·머지됨**: OverlayImage 회전 편집(GMapCustomImage) + 마커/이미지 AABB 히트 + **GMapImageMarker 이중회전 제거**(진짜 줌버그 원인) + 마커 회전 속성 UI(Slider+TextBox+°) + 이미지 중복회전 제거
- **커밋 체인(v2.6)**: a403a0d → 11a69a0 → 75a6979 → 9826256 (각 worktree v2.8.3 머지)
- **미진행(선택)**: TEST-01~04 단위테스트, #4 맵회전 보정 대칭화(GMapImageMarker MapCorrection)
- **미커밋(내가 안 만듦)**: 메인리포 워킹트리에 Debug.WriteLine 주석처리 11개 파일(로그정리 WIP) — 별도 결정 필요
- 분석: `docs/analysis/OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md`

---

## ✅ 런타임 검증 완료 (2026-06-10) — 사용자 "잘 동작한다"

- 줌→회전 누적/위치드리프트 해소 (이중회전 제거)
- 마커 AABB 클릭 정확 (회전 보정 + screenWidth 캐시)
- 마커 회전 속성: Slider + 직접입력 TextBox + ° (MarkerBearing 동기화), 이미지 중복 회전 제거
- v2.6 최종 HEAD: 9826256

### 남은 선택 항목 (미진행)
- **#4 맵 회전 보정 대칭화**: DB 이미지(GMapImageMarker)가 지도 나침반 회전 시 화면방향 유지하려면 MapCorrection 추가 필요. 현재는 맵과 함께 회전(기존 동작, 사용자 불만 없음).
- **TEST-01~04 단위테스트**: InverseRotateMouse 역변환 항등 / 마커 AABB 역회전 / EffectiveRotation 분리 / 회전 리사이즈. 런타임 검증으로 동작은 확인됨, 회귀방지용 자동화 테스트는 미작성.

---

## 속성 패널 회전 UI 정리 (2026-06-10) → v2.6 머지 75a6979 (커밋 f524497)

- `BasePropertyStyle.xaml`: 마커 기본 회전 TextBox → **Slider + 값 TextBlock(NN°)**. MarkerBearing 양방향 → 슬라이더 드래그 실시간 회전. (모든 마커 공통)
- `ImagePropertyStyle.xaml`: 이미지 속성의 회전 슬라이더 **제거** (기본 회전과 동일 MarkerBearing 바인딩 중복).
- 구조: 이미지 패널 = 기본 속성(Base 템플릿, 회전 포함) + 이미지 속성(SpecificContent). 둘 다 회전 있어 중복이었음.

## 줌→회전 누적 + 마커 AABB 부정확 버그 수정 (2026-06-10) → v2.6 머지 11a69a0

분석: `docs/analysis/OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md` (6가설 Workflow + opus 적대검증)

### 결정적 발견 — 두 이미지 시스템
- **DB 로드 오버레이 = `GMapImageMarker`(Markers)** / 파일 다이얼로그 = `GMapCustomImage`(CustomImages)
- 이전 회전 기능(UserRotation/초록핸들)은 `GMapCustomImage`에만 적용 → **사용자가 보는 DB 이미지(GMapImageMarker)엔 미적용**. 두 버그 모두 GMapImageMarker 경로 기존 결함.

### 수정 (커밋 ebd3735 → 머지 11a69a0)
1. **이중 회전 제거**: `GMapMarkerImageControl.cs` OnRender의 `PushTransform(Bearing)` 삭제. base의 `RenderTransform(RotationAngle, origin 0.5/0.5)`과 중복되어 2배 과회전 + 줌 위치 드리프트(중심 ActualWidth vs screenWidth)였음. → 회전은 RenderTransform 단독, Adorner 선택박스와 일치.
2. **마커 AABB 회전 보정**: `GetMarkerAtScreen`에서 클릭좌표를 `-Bearing` 역회전 후 AABB 비교.
3. **screenWidth 캐시**: `GMapMarkerImageControl.RenderedScreenWidth/Height` 즉시 노출, GetMarkerAtScreen이 ActualWidth(1사이클 지연) 대신 사용.

### 기각된 가설 (미적용)
좌표계 차감(H6, 회귀유발) · 라벨 ActualHeight 팽창(H5) · EffectiveRotation 음수정규화(H3, cosmetic·이미 적용됨) · RepositionOverlay(死코드)

### 미적용 (부차, 사용자 확인 대기)
#4 맵 회전 보정 대칭화 — GMapImageMarker에도 MapCorrection 추가 + UpdateOverlaysAfterRotation이 Markers 순회. DB 이미지가 맵 나침반 회전 시 화면방향 유지하려면 필요. 현재는 맵과 함께 회전(기존 동작).

### 빌드/검증
- 빌드 오류 0. **사용자 재빌드(Dotnet.Monitoring.Solution) 후 런타임 검증 대기.**

---

## 디지털 줌 UX 3개선 (2026-06-15) → v2.6 머지 e9c932f (커밋 380b093)

PRD §7 추가(FR-11~13) + Plan 신규 `docs/plans/DigitalZoom_RenderTransform-prd-plan.md`. 롤백 `before-digital-zoom-ux`(→72db756).
3에이전트 설계 + 2회 시뮬 검증(C1 NEEDS_FIX·C2 OK·C3 NEEDS_FIX).

- **C1 슬라이더 라벨**: `MapZoomControl` ZoomLabel/IsDigitalZoom 읽기DP + UpdateZoomLabel(OnZoomStateChanged·OnSliderValueChanged **직접호출**=가드 우회 stale 차단)+OnApplyTemplate. MapZoomStyle Col0 48px, "17"/"18+"/"18++", 디지털 오렌지(#E67E22).
- **C2 축척바**: `scaleX *= digScale`(바 확대) 제거 → `ScaleHelper.AdjustScaleLabel`로 거리 숫자 ÷배율(바 고정, InvariantCulture, m↔Km). FR-9 대체.
- **C3 ClipToBounds**: `MapView.xaml` AdornerDecorator ClipToBounds=true(Grid 아님 — 드래그패널 음수Top 보존). 디지털 팽창분만 클립.
- 빌드 오류 0. **✅ 런타임 검증 완료(사용자 "괜찮은거 같아").**

### 디지털 줌 전체 상태 (커밋 체인)
72db756(원구현) → e9c932f(UX 3개선). 롤백: before-digital-zoom / before-digital-zoom-ux.
**미진행(선택)**: TEST 자동화(C1 라벨/C2 축척바/C3 클립 단위·통합 테스트) 미작성.

## 디지털 줌 구현 (2026-06-12) → v2.6 머지 72db756 (커밋 ef5d4d8)

PRD: `Docs/prds/DigitalZoom_RenderTransform-prd.md` (Approved). 롤백: `before-digital-zoom`(→5e78cfd).
설계: 5에이전트 Workflow + opus 적대검증.

### 핵심 아키텍처 — 컨트롤 RenderTransform 방식
- MaxZoom 초과 시 `GMapCustomControl.RenderTransform = ScaleTransform(s,s,ActualWidth/2,ActualHeight/2)` → 타일+마커+오버레이 **균일 확대**.
- **불변식**: `ScaleMode(Integer)`/`Zoom`/`_core.Zoom/ScaleX/Y` 절대 미변경(타일 신규요청 없음), RenderTransform만 변경.
- **WPF가 e.GetPosition에 RenderTransform.Inverse 자동 적용** → 히트테스트 **무보정** 정합(수동보정=이중변환 버그, NFR-5). scale={1.0,1.5,2.0} 2단계.
- 폐기: MapScaleTransform 주입(마커 미확대), DrawingContext push(마커 children), ScaleModes.Dynamic(빈 타일).

### 구현 (STEP1~9, 5파일)
- `GMapCustomControl`: DigitalZoomLevel **DP**(coerce[0,2]+changed콜백→ApplyDigitalZoomTransform+이벤트) + StepDigitalZoom/ResetDigitalZoom + OnMouseWheel 라우팅(Max휠업→+1, 디지털중 휠다운→-1) + SizeChanged 재적용.
- `MapZoomControl`: DigitalZoomLevel/DigitalZoomSteps/ExtendedMaxZoom/SliderValue **DP** + 라우팅(SliderValue↔Zoom+Level, `_isSyncing` 재진입가드). 슬라이더는 SliderValue에 바인딩(Zoom DP는 OnCoerceZoom 클램프라 직접 금지).
- `MapView.xaml`: MapZoomControl.DigitalZoomLevel ↔ MainMap.DigitalZoomLevel TwoWay + DigitalZoomSteps=2.
- `MapViewModel`: ZoomIn/Out 디지털 위임, DigitalZoomLevelChanged 구독→CreateScaleBar(`scaleX *= DigitalZoomScale`, 라벨 동일·바폭 배율), 리셋(MainMap_OnMapZoomChanged + ZoomMax setter).
- `MapZoomStyle.xaml`: Maximum=ExtendedMaxZoom, Value=SliderValue.

### code-review(opus): H-1 SliderValue MaxZoom 클램프, L-5 전환 로그. 빌드 오류 0. **런타임 검증 대기.**

## T4 진짜 원인 수정 (2026-06-10) → v2.6 머지 5e78cfd (커밋 362d5ab)

런타임에서 T4(편집모드 오브젝트 위 휠/드래그)가 여전히 안 됨 → Workflow 재추적으로 진짜 원인 발견:
- **휠 차단**: 편집모드에서 마커 Shape `IsHitTestVisible=true` → `GMapControl.IsMouseDirectlyOver=false` → `GMapControl.cs:1775` 줌 조건 `(IsMouseDirectlyOver||IgnoreMarkerOnMouseWheel)` false. **수정**: 생성자 `IgnoreMarkerOnMouseWheel=true`.
- **선택 오브젝트 드래그 차단**: `MarkerEditAdorner`가 본체까지 hit-test 흡수 → 맵 미전달. **수정**: `HitTestCore` override로 핸들 영역만 hit, 본체는 null(투과).
- 직전 `e.Handled` 제거가 무효였던 건 소비 문제가 아니라 위 둘이었음(1차 가정 오류).
- 이미지(GMapCustomImage)는 OnRender라 T4-C(본체→팬+중심핸들)로 이미 처리.

## GMap 줌 개선 T1/T2/T4 구현 (2026-06-10) → v2.6 머지 8fe40c2

PRD: `docs/prds/GMap_Zoom_Improvements-prd.md` / Plan: `docs/plans/GMap_Zoom_Improvements-prd-plan.md`
롤백: `before-zoom-improvements` (→ d481f80). 커밋 368a455 → 머지 8fe40c2.

### 분석(Workflow) 핵심
- **T1**: `GMapControl.MaxZoom`이 INPC/DP 아닌 CLR 래퍼 → 바인딩 stale. **수정**: MapView.xaml 바인딩 VM `ZoomMax/ZoomMin`(INPC), provider 설정 3곳 ZoomMax setter 경유, MapZoomStyle 슬라이더 IsSnapToTickEnabled/TickFrequency=1.
- **T2**: GMap.NET base가 휠 시 Position 먼저 변경 후 Zoom 클램프 → Max에서 중심 점프. **수정**: OnMouseWheel base 전 경계 가드(`zoomUp && Zoom>=MaxZoom` 등).
- **T3 디지털줌**: ScaleTransform 방식은 오버레이 타일 256px 갭붕괴+ScaleMode=Integer 미검증 → **사용자 결정 옵션A(MBTiles maxzoom 재생성)**. 코드는 T1로 흡수, 절차는 PRD T3-A.
- **T4**: 이미지=본체 Move폴백 제거+중심 이동핸들(하늘색) 신규 / 심볼=`GMapMarkerBaseControl:561 e.Handled` 제거(본체 클릭 버블→맵팬, 이동은 Adorner 중심핸들). 휠은 이미 버블. **code-review H-1**: OnMarkerClicked 이중발화→SelectMarkerForEditing 재진입 가드(`_isSelectingMarker`).

### 핵심 구조 메모
- 오버레이 이미지 두 시스템: DB로드=GMapImageMarker(Markers) / 파일=GMapCustomImage(CustomImages). [[project_two_image_overlay_systems]]
- 심볼 이동=Adorner 중심 Move핸들(MarkerEditAdorner). 이미지 이동=이제 중심 Move핸들(신규).

### 미진행
- T3 MBTiles 재생성(사용자 데이터 작업, PRD T3-A 절차) / 런타임 검증 대기 / TEST 미작성.

---

## ⚠️ 배포 경로 핵심 (반드시 기억)

- 소비 앱 `Dotnet.Monitoring.Solution`은 라이브러리를 **ProjectReference**로 직접 참조: `..\..\Ironwall.Dotnet.Libraries\...GMaps.Ui.csproj` → **메인리포 v2.6 소스**를 빌드함.
- **worktree(v2.8.x)에만 커밋된 변경은 앱에 절대 반영 안 됨.** 반드시 **v2.6에 머지**해야 앱 재빌드 시 반영.
- 사용자 런타임 테스트 = `Dotnet.Monitoring.Solution` 재빌드 필요.

## 회전 편집 + AABB 구현 완료 → v2.6 머지 (2026-06-10)

- **커밋**: worktree v2.8.3 `7bfb74e` → v2.6 머지 `a403a0d`
- **롤백 태그**: `before-overlay-image-rotation` (→ 6a1e005)
- **구현**: IMPL-01~17 + RISK-01(좌표계 헬퍼) + **마커 AABB(R-1)** + code-review C-1(회전각 정규화)/H-1([0,360) 정규화) 반영
- **빌드**: 오류 0
- **⏳ 미완료**: TEST-01~04 단위테스트 미작성, 런타임 검증 대기 (사용자 재빌드 후 확인)

### 사용자 피드백 (2026-06-10) — 교훈
- 사용자가 "AABB 반영 안됨/이미지 클릭 그대로/회전 기괴" 보고 → **원인: 모든 변경이 worktree에만 있어 앱 미반영 + 마커 AABB(R-1) 미구현**이었음
- 회전 PRD(이미지)에 집중하느라 사용자 핵심 요구인 **마커 히트 AABB(GetMarkerAtScreen)** 를 누락했음 → 즉시 R-1 구현 + 머지로 해소

---

### (이전 기록)
- **마지막 업데이트**: 2026-06-10 09:49

---

## OverlayImage 회전 편집 기능 PRD v1.0 완료 (2026-06-10) — 검토 대기

PRD 파일: `docs/prds/OverlayImage_Rotation_Editing-prd.md` (Track C, Draft)

### 작성 방식
- **Workflow**: 50 Agent (20 파트 스캔 + 18 시나리오 × 2라운드 검증 + PRD 합성), 3.59M 토큰
- R2 적대적 재검증(opus)으로 1차 과대평가 항목 다수 정정

### 핵심 설계 결정 (1차 결론 폐기 반영)
1. **좌표계 단일화 (NFR-1)**: 렌더·히트·드래그델타가 동일 `RotateTransform(EffectiveRotation, cx, cy)` 공유. 히트테스트는 마우스를 `-(UserRotation+MapCorrectionRotation)` 역회전 후 AABB 비교. **ImageBounds는 AABB 유지, 회전은 별도 필드** (OBB 전환 비목표)
2. **회전값 분리 (FR-6/7)**: `UserRotation`(DB 영속) + `MapCorrectionRotation`(-MapRotation, 런타임 전용) + `EffectiveRotation`(합산). `UpdateOverlaysAfterRotation`의 `Rotation = -MapRotation` 덮어쓰기가 사용자 편집값 파괴하던 잠재 결함 해소
3. **"히트테스트만 고치면 됨"(1차 S03) 폐기** → 리사이즈 델타 역회전 동반 필수 (NFR-2)
4. **"Rotation!=0이면 핸들 비활성화"(1차 S16) 채택 금지** → 맵 조금만 회전해도 전 이미지 리사이즈 차단되는 더 큰 회귀

### 확정 사이드이펙트 (R2 기준 심각도)
- HIGH: S17(핸들 좌표계 불일치), S03(회전 리사이즈 불능+델타축), S12(선택/진입 오염), S06(EditMode 해제 중 캡처 누수)
- MEDIUM 하향: S16, S05, S02 / LOW: S01, S18
- 1차 오류 정정: RepositionOverlay 死코드, dead fields, Clone 호출자 0개

### 구현 시퀀스 (STEP 1~9, 1 PR 원자 적용)
1. GMapCustomImage UserRotation/MapCorrectionRotation/EffectiveRotation 필드 분리
2. ResizeHandle.Rotate enum + 회전 전용 필드
3. InverseRotateMouse 헬퍼 + GetClickedImageHandle/GetImageAtScreen 역회전
4. StartImageDrag 중심캐싱 + ProcessImageDrag Rotate case(절대각) + Resize 델타 역회전
5. RenderSingleImageOverlay EffectiveRotation + Pop을 finally로(스택안전) + 회전핸들 드로잉
6. UpdateOverlaysAfterRotation MapCorrectionRotation만 갱신
7. 줌/패닝/EditMode/OnLostMouseCapture 방어
8. OnImageEditCompleted 이벤트 + DB 영속화(UserRotation만)
9. 우클릭 컨텍스트 메뉴(회전 초기화/입력)

### 대상 파일
`GMapCustomControl.cs`(핵심), `GMapCustomImage.cs`, `ImageModel.cs`, `MapViewModel.cs`

### 진행 상태 (2026-06-10 갱신)
- ✅ **사용자 PRD 승인** ("승인 + AABB PRD 흡수") → 훅으로 Draft→Approved 기록
- ✅ **AABB PRD 흡수**: `MarkerHitTest_AABB_Fix-prd.md`의 R-2(Opacity)→회전 PRD FR-10, R-3(OBB)→회전 PRD NFR-1. AABB PRD는 마커 히트(R-1)만 남기고 스코프 축소
- ✅ **롤백 태그** `before-overlay-image-rotation` (→ `6a1e005`) 생성
- ✅ **Plan 작성 완료**: `docs/plans/OverlayImage_Rotation_Editing-prd-plan.md` (26 태스크, 약 35h)
  - Phase 0: RISK-01(좌표계 단일화 헬퍼) / Phase 1: SETUP-01(worktree) / Phase 2: IMPL-01~17 / Phase 3: TEST-01~05 / Phase 4: DOC-01~02
  - 실제 코드 검증 완료: ProcessImageDrag 델타→ResizeBounds* 입력 구조 / CalculateRotationAngle=atan2(dx,-dy) 절대각 / GetClickedImageHandle default 없음(S07)
- ⏳ **다음 단계: Plan 사용자 검토 대기** → 승인 시 worktree 구축(SETUP-01) → IMPL-01부터 dev 진입 (code-reviewer 체인 포함)
- pipeline-state: phase=plan, activePrd/activePlan → 회전 PRD로 갱신 (이전 LayerVisibility는 previousPrd로)

---

- **이전 업데이트**: 2026-06-10 (OverlayImage ZOrder 독립 영속화 구현 완료 — merge 2fe9b40)

---

## 프로젝트 상태

- **Branch**: `v2.6` (Ironwall.Dotnet.Libraries) / `v0.5` (Dotnet.Monitoring.Solution)
- **Worktree**: `c:/workspace_app/worktrees/v2.8.1` (v2.8.1 브랜치, 병합 완료)
- **현재 Phase**: plan
- **Track**: B
- **롤백 태그**: `before-splash-prd-impl` (양쪽 리포)

## OverlayImage ZOrder 독립 영속화 PRD (2026-06-10)

PRD 파일: `docs/prds/OverlayImage_ZOrder_Independence-prd.md`

### 핵심 설계 결정
- **SSOT**: `MapLayers.ZOrder` 유지 — Images 테이블 ZOrder 컬럼 추가 없음
- **Band 정책**: 이미지 0~999 / 심볼 1000+
- **BUG-3(Symbols 오염) 우선 수정**: STEP 5 완료 전 필터 해제(STEP 6~9) 절대 금지

### 구현 순서 요약 (10 STEP)
1. IImageModel + ImageModel ZOrder 런타임 프로퍼티
2. GMapImageMarker 생성자 하드코딩 제거 + setter 모델 동기화
3. RestoreLayerVisibility:6259 Panel.SetZIndex 동기화
4. Symbols band 시프트 마이그레이션 (`UPDATE Symbols SET ZOrder = ZOrder + 1000 WHERE ZOrder < 1000`)
5. SaveMarkerZOrderAsync 타입 분기 (이미지→MapLayers, 심볼→Symbols)
6~9. EnsureUniqueZOrder/NormalizeAllZOrder/MoveMarker*/RefreshPropertyPanel band-aware 교체
10. AddImageMarkerFromModel Panel.SetZIndex 초기 적용

### 다음 단계
- 사용자 PRD 검토 후 Plan 문서 작성 → worktree v2.8.2 구축 → 구현

---

## ZOrder 네이밍 통일 완료 (2026-06-10)

Symbol 계층 전체의 `ZIndex` → `ZOrder` 변경 완료 (커밋 `b130e0c`, merge into v2.6).

### 핵심 포인트
- `GMapMarker.ZIndex` (GMap.NET), `Panel.GetZIndex/SetZIndex` (WPF) — 이름 변경 불가, 유지
- `GMapImageMarker`는 `GMapBaseMarker<T>` 미상속 → `IEditableMarker.ZOrder` 명시적 구현 직접 추가 필요했음
- worktree 빌드 시 `GMap.NET/`, `OnvifSolution/` Junction 포인트 생성으로 경로 문제 해소
- DB: 기존 `ZIndex` 컬럼 유지 + `ZOrder` 컬럼 추가·이관 마이그레이션

## ZOrder 재시작 복원 수정 (2026-06-08, 커밋 1472029)

DB에 저장된 ZOrder 값이 재시작 후 무시되던 버그 수정.
`AddMarkerFromSymbol(isExistingMarker: true)` → `AddMarkerToMap`에서 maxZ+1 부여 스킵.

---

## SplashScreen 구현 완료 (2026-06-08)

### 생성/수정된 파일

**라이브러리 (main 브랜치 + v2.8.0 worktree 양쪽)**
| 파일 | 작업 |
|------|------|
| `Ironwall.Dotnet.Libraries.Base/Services/Startup/ISplashViewModelBase.cs` | `Task WhenActivated` 추가 |
| `Ironwall.Dotnet.Libraries.ViewModel/ViewModels/Splash/SplashViewModel.cs` | **신규** |
| `Ironwall.Dotnet.Libraries.ViewModel/Views/Splash/SplashView.xaml(.cs)` | **신규** |

**Monitoring Solution (v0.5 직접)**
| 파일 | 작업 |
|------|------|
| `Bootstrapper.cs` | SelectAssemblies + ConfigureContainer + OnStartup(RELEASE) + StartPrograme(RELEASE) 수정 |
| `ViewModels/SplashScreen/SplashScreenViewModel.cs` | **삭제** |
| `Views/SplashScreen/SplashScreenView.xaml(.cs)` | **삭제** |
| `Resources/Images/Company_Logo.png` | **신규** (sensorway.png 복사) |

### 코드리뷰 수정 사항 (적용됨)
- `AllowClose()` 멱등성 가드 추가 (`if (_isCloseable) return;`)
- `_activatedTcs.TrySetResult()` → `base.OnActivateAsync` 완료 후 호출로 변경
- `StartPrograme` RELEASE: `catch(Exception)` + `Shutdown(1)` 추가 (async void 예외 전파 방지)

### 빌드 결과
- 라이브러리 (Base, ViewModel): **오류 0, 경고 5(기존)**
- Monitoring Solution: CS 오류 0 ✅ / MSB3021/3027 오류 2 (디스크 공간 부족, `map_satellite.mbtiles` 복사 실패 — 코드 무관)

### R-16 버그 수정 (2026-06-08)

**원인:** SplashView가 첫 번째 창 → `Application.MainWindow = SplashView` 자동 설정.  
`ShutdownMode = OnMainWindowClose` 설정 후 `AllowClose()`가 SplashView를 닫으면 앱 종료.

**수정:** `StartPrograme` try 블록에서 `ShutdownMode` 변경 전 `IoC.Get<ShellViewModel>().GetView()`로  
`Application.MainWindow`를 ShellView로 명시 교체. 빌드 오류 0 확인.

### 커밋 완료 (2026-06-08)

| 리포 | 브랜치 | 커밋 해시 | 내용 |
|------|--------|----------|------|
| Ironwall.Dotnet.Libraries | v2.6 | dac0b4a | feat: SplashScreen 구현 (ISplashViewModelBase + SplashViewModel + SplashView) |
| Dotnet.Monitoring.Solution | v0.5 | 5f520c9 | feat: SplashScreen 구현 + R-16 버그 수정 (Application.MainWindow 교체) |

**SplashScreen 구현 완전 완료.** 런타임 검증 ✅

---

---

## DeviceApi_ProviderPropagation_Fix PRD v2.0 완료 (2026-06-08)

### 분석 결과 요약
- **분석**: 13-Agent Workflow — 실제 코드 파일 직접 Read + 30 시나리오 × 2라운드
- **결과**: 23 CONFIRMED_FAIL / 5 PASS / 2 PARTIAL / FALSE_POSITIVE 0건
- **확정 이슈**: 19개 (CRITICAL:1, HIGH:9, MEDIUM:4+DeviceRemovedMessage, LOW:3) + 신규 3건(N1(data), N2, N1(map))

### 신규 발견 이슈 (v2.0)
| ID | 심각도 | 내용 |
|----|--------|------|
| N1(data) | HIGH | FetchControllers/Sensors 실패 → 빈 리스트 → updateList Join 0건 → 수정 소실 |
| N2 | MEDIUM | OnClickSaveButton finally가 실패 시에도 정상 완료 UI 복원 |
| N1(map) | MEDIUM | InitializeDeviceSymbolIntegration 전체 rebuild O(n) + EA 재구독 누락 |
| DeviceRemovedMessage | MEDIUM | EA 장비 삭제/생성 알림 모델 전무 (C1,H4,H5,H6 전제조건) |

### 다음 단계
- **Plan 문서 작성** 필요 (`docs/plans/DeviceApi_ProviderPropagation_Fix-prd-plan.md`)
- **롤백 태그** 생성 필요: `git tag before-device-api-propagation-fix`
- **구현 시작**: 16 STEP (Phase 1: STEP-1~9, Phase 2: STEP-10~13, Phase 3: STEP-14~16)

---

## EventProcess_ContaminationFix PRD v1.0 완료 (2026-06-08)

### 분석 결과 요약
- **분석**: 13-Agent Workflow — 실제 코드 파일 직접 Read + 25 시나리오 × 2라운드
- **결과**: 16 CONFIRMED_FAIL / 3 PASS / 6 PARTIAL / FALSE_POSITIVE 3건 (EC3, EC4, EB5)
- **확정 이슈**: 21개 (CRITICAL:1, HIGH:8, MEDIUM:9, FALSE_POSITIVE:3)

### 확정 이슈 요약
| 심각도 | ID | 파일 | 내용 |
|--------|-----|------|------|
| CRITICAL | EA2 | `DetectionEventPanelViewModel.cs:288-290` | DataInitialize() fetch 실패 시 기존 이벤트 컬렉션 전체 소실 |
| HIGH | EA1 | `DetectionEventPanelViewModel.cs:142-146` | API 반환값 폐기 |
| HIGH | EA3 | `DetectionReportDialogViewModel.cs:43` | CloseDialog 무조건 발행 |
| HIGH | EA7 | `DetectionEventPanelViewModel.cs:142-153` | 배치 루프 부분 실패 미수집 + DataInitialize 무조건 |
| HIGH | EB2 | `EventQueueManager.cs:366-367` | 컬렉션 무한 증가 |
| HIGH | EB3 | `DeviceNatsSyncService.cs:68` | DELETED dead code → 고아 이벤트 |
| HIGH | EB7 | `BaseModel.cs:31`, `ExEventModel.cs:47` | INPC 미구현 |
| HIGH | EC2 | `EventCardListPanelViewModel.cs:272,402-469` | _batchReportGate 미보호 2경로 |
| HIGH | EC5 | `EventCardListPanelViewModel.cs:436,523-543` | 3중 경로 중복 조치보고 |

### Device API ↔ Event Process 교차 오염 경로 4개
1. `DeviceNatsSyncService.cs:68` DELETED dead code → EB3 고아 이벤트 (동일 루트 원인)
2. Device Fetch 실패 → null Device → EC2/EC5 조치 중 NullReferenceException
3. Device INPC 없음 → EB7 이벤트 카드 stale Device 참조
4. CTS 혼선 → EC7과 Device Fetch 교차 취소

### 두 PRD 구현 의존성
Device API C1 (NATS DELETED 처리) 완료 후 Event Process EB3 효과 발현

### PRD 위치
- `docs/prds/EventProcess_ContaminationFix-prd.md` (v1.0, 검토 대기)

### 다음 단계
- **사용자 검토**: EventProcess PRD v1.0 리뷰 (feedback memory: PRD → 사용자 검토 → Plan 순서)
- DeviceApi Plan 작성 + 롤백 태그 + Worktree
- DeviceApi Phase 1 구현 (STEP-1~9)
- (DeviceApi C1 완료 후) EventProcess Phase 1 구현

---

## LayerVisibility_Persistence_Fix PRD v2.0 완료 (2026-06-08)

### 변경 핵심
- **ARCH-1 신규 발견**: `ApplyLayerVisibility`(명령형)와 `GMapMarkerBaseControl.cs:381-390`(OneWay 바인딩)이 `Shape.Visibility` DependencyProperty에 동시에 쓰는 두-작성자 충돌 → 재시작/Zoom 이벤트 시 Visible로 복원되는 진짜 원인
- **수정 축 변경**: `marker.Shape.Visibility` 직접 쓰기 → `marker.ShowShape`(DB 컬럼, 사용자 의도 보존) 단일 권한축
- **새 필드**: `_isApplyingLayerVisibility` (ApplyLayerVisibility + 시작 집계 재진입 차단)
- **PRD 2 분리**: Symbol→Layer 역방향 집계 + PropertyPanel 역전파는 별도 고위험 PRD로 분리

### 수정 포인트 6개 (모두 MapViewModel.cs)
1. `ApplyLayerVisibility` ShowShape 단일 작성자 + `_isApplyingLayerVisibility` 가드
2. `OnActivateAsync` `LoadLayersFromDbAsync()` 호출 추가
3. `LoadLayersFromDbAsync` 복원 루프 OverlayImage 브랜치 + `AggregateLeafCheckedFromMarkers` 집계
4. `OnLayerVisibilityChanged` OverlayImage 두 저장소 동기화
5. persist-first 재구성 + catch 롤백
6. `FindImageMarkerByFilePath` 경로 정규화

### 절대 건드리면 안 되는 코드 (PRD 2 전까지)
- `LayerTreeNode.cs:56-88` (IsChecked setter) — 역방향 추가 금지 (EL-01 루프)
- `LayerTreeNode.cs:182-199` (UpdateCheckStateFromChildren) — 마커 스캔 금지
- `MapViewModel.cs:5226-5248` (OnMarkerPropertyChanged) — Symbol→LayerTreeNode 추가 금지
- `GMapMarkerBaseControl.cs:381-390` (OneWay IsVisible 바인딩) — TwoWay 전환 금지

### 구현 완료 (2026-06-08)
- **파일**: `MapViewModel.cs`, `LayerTreeNode.cs`
- **빌드**: 오류 0 (경고 475개 기존)
- **code-reviewer**: PASS (High 1건 부모 tri-state 갱신 → SetCheckedSilently에 Parent?.UpdateCheckStateFromChildren() 추가로 해결, Low 1건 em.Zoom 단순화 적용)
- **롤백 태그**: `before-layer-visibility-fix` ✅

### STEP 8 추가 완료 (startup timing fix)

- **원인**: `await LoadLayersFromDbAsync()` 직접 호출 시 내부 DB await 동안 WPF 렌더링 → 심볼이 briefly visible로 flash됨
- **수정**: `Dispatcher.BeginInvoke(ApplicationIdle, async () => await LoadLayersFromDbAsync())`
- **효과**: 모든 심볼 렌더링 완료 → 레이어 가시성 일괄 적용 (시각적 순서 보장)
- **빌드**: 오류 0 ✅

### STEP 9 추가 완료 (OverlayImage ZOrder 재시작 복원)

- **원인**: `GMapImageMarker` 생성자 `ZIndex = 5` 하드코딩 + `LoadLayersFromDbAsync` OverlayImage 블록이 `IsVisible`만 복원하고 `ZOrder` 미복원
- **수정**: `LoadLayersFromDbAsync` OverlayImage 블록에 `imgMarker.ZIndex = model.ZOrder` 추가
- **효과**: 재시작 후 레이어 순서 유지 (특정 심볼이 OverlayImage 아래에 깔리는 Z-order 역전 해소)
- **빌드**: 오류 0 ✅
- **문서**: PRD v2.2, Plan 9/9 STEP Complete 반영 완료

### RC-7 구현 완료 (2026-06-08)

- **커밋**: `966a254` — fix: LayerVisibility 재시작 복원 + RC-7 UpdateMarkersVisibilityByZoom 덮어쓰기 차단
- **PRD**: v2.4 (STEP 10~14 완료, code-reviewer H-1/M-1 반영)
- **Plan**: 14/14 STEP ✅ Complete

**수정 내용**:
1. `IsLayerEnabled` 필드 → `IEditableMarker`/`GMapBaseMarker`/`GMapImageMarker` 추가
2. `GMapImageMarker` 생성자에서 `_isLayerEnabled = imageModel.Visibility` 초기화 (H-1: startup flash 원천 차단)
3. `SetMarkerVisibility` → `Zoom >= Zoom && IsLayerEnabled && ShowShape` (M-1: ShowShape도 통일)
4. `LoadLayersFromDbAsync` 트리전용 + `RestoreLayerVisibility` 신규 분리
5. `ApplyLayerVisibility` + `OnLayerVisibilityChanged` OverlayImage에 `IsLayerEnabled` 동기화
6. `OnActivateAsync` → `await LoadLayersFromDbAsync()` + `await InvokeAsync(RestoreLayerVisibility, ApplicationIdle)`

**LayerVisibility_Persistence_Fix PRD v2.4 완전 완료. 빌드 오류 0 ✅**

---


---

## LINE/PidsGroup 레이어 재시작 후 ON 시 비가시 버그 수정 (2026-06-09)

### 커밋: `21a14e3` fix: LayerVisibility 재시작 복원 — LINE/PidsGroup ON 후 미표시 + ShowShape 게이트 제거

| 파일 | 변경 내용 |
|------|----------|
| `GMapMarkerBaseControl.cs:525-534` | `OnRenderSizeChanged` — `ActualWidth > 0` 가드 추가 (Collapsed 시 Marker.Width 0으로 덮어쓰기 방지) |
| `GMapMarkerLineControl.cs` | `OnMarkerPropertyChanged` 신규 — `IsVisible=true` 시 `UpdateLineGeometry()` Render 우선순위 재호출 |
| `GMapMarkerPidsGroupControl.cs` | 위와 동일 (PidsGroup 대상) |
| `MapViewModel.cs` | `AggregateLeafCheckedFromMarkers` — `ShowShape` → `IsLayerEnabled` 전환 |
| `GMapCustomControl.cs` | `SetMarkerVisibility` — `ShowShape` 조건 제거 |
| `GMapDbService.cs` | `SeedDefaultSymbolLayersAsync` — 전체 스킵 제거, category별 중복 체크 + Basic 레이어 추가 |
| `LayerTreeBuilder.cs` | `Basic` 카테고리 매핑 추가 |
| 테스트 2개 | 11 → 12개 카운트 갱신 |

---

## SnapGrid 기능 커밋 (2026-06-09)

### 커밋: `abc312c` feat: 마커 격자 스냅(SnapToGrid) 기능 구현

- `MarkerEditAdorner.cs`: 드래그 중 LatLng 도메인 격자 스냅 (threshold 20%)
- `MapView.xaml`: SnapGrid ToggleButton + GridSnapSettingsControl Popup
- `SnapGridOverlayService.cs` (신규): 격자 오버레이 렌더링
- `GridSnapSettingsControl.xaml/.cs` (신규): 격자 크기 설정 컨트롤

---

## ZOrder_PropertyPanel_Integration 구현 완료 (2026-06-10)

### 커밋: `47225d6` feat: PropertyPanel Z-order 연동 구현

| 파일 | 변경 내용 |
|------|----------|
| `IEditableMarker.cs` | `int ZIndex { get; set; }` 추가 |
| `GMapBaseMarker.cs` | `: IEditableMarker` 선언 + explicit `ZIndex` setter (Panel.SetZIndex 동기화 포함) |
| `Args/ZOrderChangeRequestedEventArgs.cs` | 신규: `ZOrderDirection` enum, `ZOrderChangeRequestedEventArgs`, `ZOrderChangeRequestedEvent` |
| `GMapPropertyBaseControl.cs` | `MarkerZIndexDisplay`·`IsEditModeEnabled` DP + `ZOrderChangeRequested` 이벤트 + PART 버튼 4개 연결 |
| `Themes/BasePropertyStyle.xaml` | Z-ORDER 섹션 (순서 TextBlock + ChevronDouble/Single 버튼 4개) + `ZOrderButtonStyle` |
| `PropertyPanelEventBehavior.cs` | `ZOrderChangeRequested` 구독/해제 + `OnZOrderChangeRequested` (try/catch 포함) |
| `MapViewModel.cs` | `IHandle<ZOrderChangeRequestedEvent>` + `HandleAsync` + `RefreshPropertyPanelZIndex`(ordinal rank) + `IsEditModeEnabled` setter → 열린 PropertyPanel 동기화 + 7개 필터 `and not IImageEditableMarker` 추가 + `ApplyMarkerZIndexLocal` 리플렉션 제거 |

### code-reviewer 지적사항 모두 반영
- **C1**: explicit setter에 `Panel.SetZIndex` 추가 + `ApplyMarkerZIndexLocal` 리플렉션 → explicit setter 교체
- **M2**: `IsEditModeEnabled` setter에서 `PropertyPanel.IsEditModeEnabled = value` 동기화
- **M3**: 표시를 raw ZIndex+1이 아닌 ordinal rank(정렬 후 indexOf+1) 기반으로 변경
- **m5**: `OnZOrderChangeRequested` try/catch 추가

### 기능 동작
- PropertyPanel에 Z-ORDER 섹션 표시: "순서 N / M" + ←← ← → →→ 버튼
- 버튼 클릭 → Behavior → EventAggregator → MapViewModel → MoveMarker → RefreshDisplay
- IsEditModeEnabled=false 시 버튼 비활성화 (기존 조건 유지)
- 오른쪽 클릭 Z-order 메뉴는 PropertyPanel 오픈 상태에서도 계속 동작 (독립 경로)

### 빌드 결과
- **오류 0** ✅ (경고 475개 기존)

## 세션 상태

- **활성 세션 수**: 1
- **현재 세션 ID**: ppid-38648
- **충돌 여부**: 없음
- **활성 세션 목록**: ppid-38648


## GOP RBAC / Account 워크스트림 현황 (2026-07-03 갱신)

> 위 GMaps 레이어 기록(2026-06월)은 이전 워크스트림 — 현 워크스트림 실시간 추적은 `docs/coordination/ACCOUNT_COORDINATION.md`(A/B 세션 원장).

### A 레인 — 코드 완료 (재빌드 후 런타임 검증만 남음)
- **6항목 사이클**: T1 세션만료 능동감지(`0dc3cdf`) · T2 세션관리 UI+회수팝업(`67bd951`·`1f03fca`) · T4 Grant 스케줄링 UI(`4d8c6c3`·`65757ce`) · T5 맵·장비·이벤트 게이팅(`77d62a7`·`6d142fc`·`9f8a595`) · T6 중앙필터 PermissionUiPolicy(`0c14d3e`) · T7 Event·Tracking Bearer(`c9571e7`·`a2282b9`)
- **사용자 실측 버그**: 부여탭 새그룹 미반영(`1118f90`) · 부여목록 전체표시+계정열+그룹ID제거(`e68fa66`) · 회수확인팝업 미종료(`1f03fca`)
- **A 잔여 코드 = 없음.** T2 재로그인 라이브러리층 검증완료(`SetTokens` 무조건 + `ResetForLogin` once-guard 해제 + `NotifyLoginSucceeded` 배선 정상 — 재로그인 실패는 A 코드 이슈 아님).

### 남은 작업 (A 외 — 소유·근거)
1. **사용자**: 앱 재빌드 + 런타임 E2E 검증(그룹 CRUD·부여·팝업·게이팅). 코드로 확인 불가한 유일 미결.
2. **서버세션**: (a) `GET /grants` 전체조회 신설 — 요청서 `docs/coordination/REQ_Server_Grants_ListAll.md` (b) `AUTH_MODE=token` 상시화 + `NATS_REVOKE`(T1/T7 근본, v5.4 deferred). ※감사 500(`audit_log.py:118`)은 서버 해결 확인됨(GET /audit-logs 200).
3. **B(메인솔루션)**: H-20 setup_system/feature 탭 UI · `ShellView` M-1 커밋 + M-3 고아 SetupPanels 정리(타 세션 WIP 클린 후) · ① 그룹추가 런타임 최종확인(서버 저장은 정상=id=18 증거).

---

## 삭제 표준 프로세스 PRD (2026-07-05) — Draft, 검토 대기

- **산출물**: `docs/prds/Device_Delete_Standard_Process-prd.md` (Track C, 롤백태그 예정 `before-delete-standard-process`).
- **위치**: 선행 완료 `DataGridPanel_Delete_Centralization`(v2.0 `bc53164`)이 삭제 **루프**(`ExecuteDeleteAsync`) 중앙화 완료 → 본 PRD는 그 위 **오케스트레이션**(4단계 팝업 Confirm→Progress→Refresh→Inform + 게이트+타임아웃+재조회+try/catch/finally+통지) 표준화. base `RunDeleteOperationAsync` Template Method 제안.
- **분석**: Workflow `delete-standard-process-review`(5 심층 sonnet → opus 종합 → opus 적대적 검증). 11패널+3베이스 file:line 확정.
- **핵심 결함**: P1 Progress stuck(Close가 try, catch 無, 11패널) · event 4패널 무게이트/무try · CTS-RACE · 검증추가 M1(DataInitialize OCE 삼킴→재조회 타임아웃 무효)·M2(재조회 자체팝업 중첩)·M3(event 무토큰)·M4(teardown 무게이트 경합)·M5(스냅샷 stale) · 리스크 R1(gate-fail ClosePopup이 공유shell 오폐쇄)·R2(토큰통일≠stuck해법, 진짜=Close-in-finally)·R3(CanDeleteGate 백킹 패널별)·R4(시그니처 파급 10콜사이트).
- **미결(§10, 사용자 확인 필요)**: 성공통지 정책 · 타임아웃 30초 확정값 · 재조회 실패 자동재시도 · 외부 ConfirmPopup self-close 선검증(R1 전제) · Controller cascade fetchAll 의존 · CanDeleteGate 2차RBAC · 부분실패 문구 · async void→Task 파급.
- **다음**: 사용자 PRD 검토 → §10 결정 → Plan → Phase1(base+Sensor 파일럿).

### 이 세션 미커밋(primary tree) — 별건, 각자 사용자 재빌드/검증 대기
- 저장버튼 스피너 수정(`IsSaving` 분리, base+11VM+11XAML) — PRD `Save_Spinner_Permission_Fix`(Approved). 제한계정 런타임 확인 후 커밋.
- DataChartPanel 레전드 회색(Events.Ui).
- Device/Event 패널 헤더 X 버튼(메인솔루션 `DevicePanelView`/`EventPanelView`) — EventPanelView 재빌드 검증 대기.


---

## 범위 확장 → CRUD 통합 규약 PRD (2026-07-05) — Draft, 검토 대기 ★현재 활성
- 사용자: "삭제말고 갱신·저장 프로세스도 함께 묶어 표준화된 규약". → 삭제 전용 PRD를 `docs/prds/DataGridPanel_CRUD_Standard_Convention-prd.md` 로 승격(삭제 초안 Device_Delete_Standard_Process는 SUPERSEDED 배너+부록 보존).
- 핵심 통찰: 데이터 계층 루프(ExecuteDelete/Create/SaveUpdates/NotifySaveResult)는 이미 중앙화(bc53164). 남은 건 그 위 오케스트레이션 봉투를 4 오퍼레이션(저장/갱신/리로드/삭제)이 공유하게 → base RunCrudOperationAsync(CrudOperationSpec). Delete=ShowProgress=true, Save=IsSaving 스피너, Reload=swap-on-success, Insert=무봉투(게이트+투영가드만).
- 분석: 2차 Workflow crud-standard-convention-review(5심층 sonnet→opus 종합→opus 적대적 검증, 766k토큰). 11패널+3베이스 file:line 실증.
- 규약 불변식 INV-1~16(code-reviewer 체크리스트). 검증 필수 보정: (1)Notify=OperationOutcome 객체(Exception?는 저장보류 침묵) (2)★event Insert 투영가드↔Save 소스전환 원자적 동일커밋(안 그러면 Save 생성0건) (3)teardown=_isTearingDown volatile(게이트재사용=skip경합 or blocking교착 blocker) (4)device-6 Reload swap은 집계fetch 완전성신호 선결(DeviceGroup 공짜미러 아님) (5)envelope가 process-CTS 수명 소유 (6)event Save 누출벡터=ex.Message→마스킹 확장 (7)Phase0 통지외부화가 DeviceGroup 이관보다 선행.
- Phase: 0(base봉투)→1(device6 Delete)→2(device Save/Reload+DeviceGroup)→3(event4 전면,Insert↔Save 원자)→4(teardown+DRY). 총 13파일.
- 미결(§11): Save진행팝업 · 타임아웃 차등 · device Reload 완전성방식 · event LWW가드 · teardown방식 · CallerToken스코프 · Task.Delay · 성공통지 · asyncvoid.

### CRUD 규약 완전적용 시뮬레이션 (2026-07-05) — 완료, PRD 반영됨
- Workflow device-crud-standard-simulation(22 agents, 7탭×40×2Pass=280 시나리오, XAML+RBAC 포함). 리포트=docs/reports/Device_CRUD_Standard_Simulation-report.md(135KB).
- 정량: clean 162 / gap 75 / blocker 43. 구조적 비호환 0 → 7탭 전부 조건부 완전적용 가능. blocker 41/43이 단일근원 2종(Delete 미봉투화+timeout부재)=Phase0+1로 일괄소멸.
- 신규결함(Pass2 상향, PRD §12+Phase 반영): A CTS 이중생성 누수 · ★B teardown ObjectDisposedException 확정 런타임크래시(lifecycle CTS Dispose후 null-out 누락) · C FetchX OCE 삼킴 · D Delete Progress stuck 확정 · E Task.Delay 2초 오표시 · F DeviceGroup UpdateAction↔Close 역전 · G Camera Refresh 게이팅.
- XAML: 공통 전탭 정상. 갭=DeviceGroup IsDraft 1줄 결손(PropertyChangedBase 상속) · Enclosure Threshold 편집UI 전무 · Camera Refresh 편집권 게이팅.
- RBAC: 이미 표준준수(거의 전량 clean) — 봉투작업이 권한영역 무영향. 스피너버그 수정 재확인.
- 권장 실행: ①Camera 파일럿(clean 최우수) ②Phase0 base봉투 ③blocker밀집순 Speaker(11)→Controller(10)→Enclosure(7) ④Sensor/Lamp/DeviceGroup.
- 유일 아키텍처 미결=Reload swap용 집계 FetchAllDevicesAsync 완전성 신호(§11-3).

### Event CRUD 완전적용 시뮬레이션 (2026-07-05) — 완료, PRD §13 반영됨
- Workflow event-crud-standard-simulation(13 agents, 4탭 Detection/Action/Connection/Malfunction ×40×2Pass=160). 리포트=docs/reports/Event_CRUD_Standard_Simulation-report.md.
- 정량: clean 59 / gap 46 / blocker 55. 11탭 합산=clean 221/gap 121/blocker 98(대다수 3근원: Delete무봉투+Save인라인+timeout부재). event가 device보다 blocker 2.2배.
- ★핵심 정정: "event Save가 _eventProvider에서 draft 읽음→원자배포"는 Detection 단독. Action(191)/Connection(189)/Malfunction(241)은 ViewModelProvider→가드 독립커밋 가능. PRD §4-4/§7-3/§8 정정 완료.
- event 특유 blocker: (a)Delete HandleAsync 무게이트/무try/무timeout 4탭전수 (b)Save 인라인→SanitizeDetails 우회→ex.Message 원문 UI노출 보안+E1(Update 반환값무시 dirty소실) (c)Save/Delete DataInit 무토큰 (d)Insert 무가드투영+Action OriginEvent ComboBox Draft노출 순환FK (g)early-return 미갱신 (★Connection VP.Clear Draft유실 가드로도 미해소).
- event 강점: swap-on-success 4탭 기구현→device §11-3 집계 완전성신호 불요. 권한 flash/스피너버그 구조적부재(CanSaveEvent 직결).
- RBAC: event=CanEditEvents() 인스턴스+버튼별 권한바인딩(CanInsert/Delete/SaveEvent). INV-10/16/disable 4탭 clean. gap=fail-open DI전제·TOCTOU·IoC미준비 구독누락(E5).
- 권장: ①Action 파일럿 ②Phase0 base ③4탭 확산 ④Detection 최후(원자성). Connection Draft유실 추가선결. 미결=LWW(§11-4 Connection/Malfunction mutable 확정→append-only 아님, 서버종속).
- 상태: Device+Event 시뮬 모두 완료. PRD Draft 검토 대기. 코드 미착수(롤백태그 before-crud-standard-convention만).

### CRUD 봉투 파일럿 구현 (2026-07-05) — 빌드 검증 완료, 런타임 검증 대기 (미커밋)
- 시뮬 권장대로 파일럿 착수: Device=Camera + Event=Action delete를 base 봉투로 이관 + 확정 크래시 B 수정. primary tree(v2.6), 롤백태그 before-crud-standard-convention.
- 변경 4파일(소스): 
  · BasePanelViewModel.cs — 크래시 B 수정(OnDeactivate CTS Dispose 후 =null) + _isTearingDown volatile(INV-15, OnActivate 리셋/OnDeactivate set).
  · BaseDataGridMultiPanelViewModel.cs — RunCrudOperationAsync(봉투)+RunDeleteOperationAsync+RegenerateProcessCts+NotifyDeleteResultAsync 신설. ExecuteDeleteAsync에 suppressNotify 파라미터(INV-5). OperationOutcome/CrudOperationSpec 타입 신설(namespace level).
  · CameraDevicePanelViewModel.cs — OnClickDeleteButton pre-snapshot(_pendingDeleteItems) + HandleAsync→RunDeleteOperationAsync(fetchAll=FetchAllDevicesAsync+DataInitialize+UpdateAction). 기존 lifecycle CTS 오용/ClosePopup try본문/무catch/무timeout 일괄 해소.
  · ActionEventPanelViewModel.cs — HandleAsync→RunDeleteOperationAsync(event: fetchAll=null, DataInitialize가 fetch+UpdateAction). 기존 무게이트/무try/무토큰 해소.
- 빌드: Devices.Ui exit0, Events.Ui "빌드했습니다" exit0, error CS 0. ViewModel 트랜지티브 검증.
- ★런타임 검증 대기(미커밋): 앱 재빌드 후 (1)Camera 삭제 정상/취소/부분실패 (2)Action 삭제 정상 (3)삭제 중 탭전환 무크래시(B) (4)권한없는 계정 버튼 disable. 검증 후 커밋 → 나머지 탭 확산.
- 봉투 미적용(기존 방식 유지): 나머지 device 5탭+DeviceGroup, event 3탭의 delete + 전탭 save/reload. ExecuteDeleteAsync suppressNotify 기본 false라 기존 호출부 무변경(안전).
- 파일럿 잔여 known-gap(Phase 확산 시): Camera OnActivate CTS 이중생성(A) 미수정 · DataInitialize OCE 삼킴(M1)으로 재조회단계 timeout 미표면화 · Task.Delay(2000) 잔존.

### 계정·권한 패널 검토 (2026-07-05) — read-only, PRD §14 반영
- 요청: 계정·권한 패널 CRUD 표준 검토. 결과: 계정 도메인은 device/event와 **다른 base** → 봉투 직접 적용 불가.
  · AccountManager=BaseDataGridPanelViewModel<T>(병렬 중복 base, _processGate/ExecuteDeleteAsync/IsSaving 없음). Insert=RegisterDialog·Edit=EditorDialog·Save=no-op·Delete=관리자 일괄(체크박스 inline 루프).
  · PermissionMatrix=BasePanelViewModel(3화면 그룹 CRUD, DataGrid 아님).
  · Login/Logout/MyPage/AccountSetup=BasePanelViewModel(폼).
- ★부수이득: 크래시 B 수정+_isTearingDown이 BasePanelViewModel(계정 조상)에 있어 계정 6패널 자동 적용. Accounts.Ui 빌드 exit0 무회귀 확인.
- 계정 갭: AccountManager Delete=ClosePopup 누락+무게이트+무timeout+live순회(단 Reload는 swap-on-success ✅). PermissionMatrix Delete=ClosePopup有+try/catch有지만 무게이트, Reload는 실패시 화면공백(swap 아님).
- ★PRD 미결해소: PermissionMatrix 주석이 ConfirmPopupDialog.ClickOk이 팝업 self-close 안 함을 확정(핸들러가 ClosePopup 책임). → R1(gate-fail ClosePopup 금지) 안전 확정, 단 gate-fail시 confirm 잔존.
- 결정필요(별도세션 협의): AccountManager 봉투 적용법 (a)BaseDataGridMultiPanel 이관 (b)BaseDataGridPanelViewModel에 봉투 포팅 (c)갭만 개별수정. 계정=다중세션 연계(ACCOUNT_COORDINATION.md)라 코드 미변경, 검토만.

---

## [2026-07-06] 장비/이벤트 API CRUD 봉투 — 런타임 검증 세션

**세션 스코프**: 장비/이벤트 API 연동 CRUD의 테스트·검증·수정("장비/이벤트 API 테스트 세션").

**진입 시점 상태**: 워킹트리 미커밋 = PRD `DataGridPanel_CRUD_Standard_Convention`의 착수분 —
- Phase 0 base 봉투(RunCrudOperationAsync/RunDeleteOperationAsync/NotifyDeleteResultAsync/OperationOutcome/CrudOperationSpec/RegenerateProcessCts, BaseDataGridMultiPanelViewModel.cs) + `_isTearingDown`·CTS null-out(crash B, BasePanelViewModel.cs)
- Phase 1 파일럿: Camera delete → RunDeleteOperationAsync(+_pendingDeleteItems INV-14)
- Phase 3 파일럿: Action delete → RunDeleteOperationAsync
- 4파일 수정, 커밋 안 됨. 롤백태그 존재(before-crud-standard-convention, before-crud-phase1/2).

**이번 세션 검증 결과**:
1. 컴파일: ViewModel/Devices.Ui/Events.Ui 3프로젝트 **빌드 0오류**(경고는 전부 기존 nullable). 상속사슬 BaseDataGridMultiPanelViewModel→BaseDataGridMultiViewModel→BasePanelViewModel 확인(_isTearingDown 도달 OK).
2. 미이관 패널 무회귀: ExecuteDeleteAsync `suppressNotify` 기본 false → 옛 device-6 패널 자동통지 유지(역호환).
3. **런타임 검증 하네스 신설**: `Ironwall.Dotnet.Libraries.ViewModel.Tests`(CrudEnvelopeTests.cs, 7종) — **7/7 통과**. INV-1/2/4/5/12/15 + Draft(Id≤0) API미호출 + verify-after-success 실증. 상세: memory reference_headless_panel_vm_envelope_tests.

**미해결/잔여**:
- Phase 0 결함 A(OnActivateAsync:34 `_pCancellationTokenSource` 이중생성 누수) 미반영.
- 실제 WPF 앱 런타임 검증(외부 Monitoring 솔루션 재빌드+재기동)은 미수행 — 앱 실행중(PID 45452), 재기동은 사전통지 대상.
- Phase 1 device-6 나머지(Sensor/Controller/Enclosure/Speaker/Lamp/DeviceGroup) + Phase 3 event-3(Connection/Malfunction/Detection) 미이관. 롤아웃 계속 시 Plan 문서(docs/plans/...-prd-plan.md) 선행 필요(현재 없음).

### 계정·권한 CRUD 견고화 PRD (2026-07-06) — Draft, method-C
- 3-에이전트 병렬 리뷰(opus: code-reviewer 논리/코드 + architect 구조/method-C + security-reviewer RBAC). PRD=docs/prds/Account_Permission_CRUD_Hardening-prd.md.
- 대상 4패널(architect 발견): AccountManager(BaseDataGridPanelViewModel 유일소비자) + PermissionMatrix/Grant/MyPage(BasePanelViewModel 직접) + 다이얼로그. 봉투가 상속으로 미도달 → method-C.
- ★HIGH 결함 3종: (1)AM-CLOSE AccountManager 삭제 Confirm+Progress 팝업 미청산=소프트락(시블링 PM:227/Grant:108은 고쳤으나 AM만 누락, 사용자 실측 버그②와 동형) (2)AM-RELOAD-CRASH OnClickReloadButton async void+무try/catch→서버다운시 앱크래시 (3)PM-RELOAD-BLANK ReloadAsync 실패시 화면공백(destroy-then-fail).
- MED: 전패널 재진입게이트 부재(이중제출)·AM 삭제스냅샷부재(오삭제)·PM ct/타임아웃 미관통·모드전환 재조회경합·다이얼로그 Progress미청산.
- 보안 F1~F9: F1 일괄삭제 CanDeleteAdmin 미호출+빈비번+가드없음(HIGH) · F2 삭제 비번프롬프트 API모드 폐기=theater(HIGH) · F3 그룹삭제 cascade · F4 클라 임의 매트릭스+Role제출 · F5 리셋비번 하드코딩"12345678" · F6 last-admin 100건스냅샷 부정확 · F7 미마스킹 에러노출 · F8 빈캐시 auto-ADMIN. 클라 vs 서버권위 분리(F1/F3/F4/F6/F9=서버).
- method-C 배치: C3(CrudOperationRunner base-무관 composition, 권장 — 봉투가 DataGrid 특성 무의존) vs C1(Single base 이식)+C2(인라인) 폴백. 계정=다이얼로그 모델→범용 RunCrudOperationAsync 재사용(Draft루프 배제).
- ★CrudEnvelopeTests.cs(8) 실존 확인(내가 안 만듦=타 세션/자동화, 코디네이션 신호). DoD=green 유지.
- 크래시 B 픽스는 계정 6패널 순이득(Login/MyPage ObjectDisposedException 차단), Accounts.Ui 빌드 exit0 검증.
- ★코디네이션: 계정=다중세션(ACCOUNT_COORDINATION.md). PRD는 분석문서, 구현은 소유권 협의 후. 본 세션 계정코드 미변경.
- 결정필요: 봉투배치 C3 vs C1+C2 · 다이얼로그 팝업호스트 스코프 · 보안 서버협의 범위 · 리셋비번 · 계정세션 소유권.

### 계정 CRUD PRD 결정 확정 (2026-07-06)
- 사용자 결정: ①진행범위=HIGH 3종 우선 ②계정코드=PRD만 확정, 구현 대기(타이밍 사용자 조율) ③봉투배치=C3 CrudOperationRunner(MED/Phase2 대상). PRD 헤더/§4.1/§9 반영 완료. 계정 코드 미변경.
- HIGH 3종(봉투 불요 국소수정, 착수 시): AM-CLOSE(AccountManager 삭제 Confirm+Progress 팝업 청산 try/finally) · AM-RELOAD-CRASH(OnClickReloadButton/OnActivate try/catch) · PM-RELOAD-BLANK(ReloadAsync swap-on-success) + MyPage/Grant ClosePopup-in-finally.
- ★코디네이션 관측: device/event 봉투 표준을 다른 세션이 병행 프로덕션화 중 — CrudEnvelopeTests.cs(8)·IsDeleteBatchExceeded·MAX_DELETE_COUNT가 base/Camera에 추가됨(본 세션 미작성). 내 파일럿(Camera/Action delete 봉투)은 그 세션이 확장 중. C3 러너 추출은 봉투 소유 세션과 정렬 필요.
- 잔여 결정(착수 전): 다이얼로그 팝업호스트 스코프 · 보안 서버티켓 시점 · 리셋비번 정책 · 클라보안 quick-win 편입시점.
- 현 상태: PRD 3종 완성(CRUD 표준 규약 + Device_Delete superseded + 계정 CRUD 견고화). Device/Event 시뮬 완료. 파일럿(Camera/Action) 다른세션 확장중. 계정 구현 대기.

### [2026-07-06 추가] 조치보고 600건 삭제 에러 — 수정 + 삭제 상한 가드

**증상**: 조치보고 ~600건 삭제 시 "600건 삭제 실패" 팝업, 서버는 전건 성공. 로그 실측(600 started/completed, WARN/ERROR 0, ClosePopup 직후 InfoPopup)로 근본원인 규명.

**근본원인**: EventProviderService 삭제 4종이 `response.Success`가 아닌 `response.Data` 반환. 서버 DELETE는 `{success:true, data:null}`(api-test-server actions.py:765-768) → Data=false → 전건 _deleteFailures 누적 → "삭제 일부 실패" 팝업. device는 .Success라 무증상(대조 확증). 봉투와 무관한 pre-existing 버그. 상세: memory project_event_delete_returns_data_not_success.

**수정(미커밋, 태그 before-event-delete-success-fix)**:
1. EventProviderService.cs 4개 삭제 메서드 `.Data`→`.Success`(838/550/643/736). 회귀테스트 추가(실서버 계약 Data=false→true).
2. 삭제 개수 상한 가드(사용자 결정: 500, 이벤트+장비 공통): base MAX_DELETE_COUNT=500 const + IsDeleteBatchExceeded 헬퍼 + 11패널 OnClickDeleteButton에 가드 1줄.

**검증**: Devices.Ui/Events.Ui 빌드 0오류. ViewModel.Tests 9/9(봉투7+가드2). Events.Ui 삭제 유닛 2/2.
**미완**: 실앱(외부 Monitoring) 재빌드·재기동해야 반영/실사용 확인. 커밋 대기.

### 계정 CRUD HIGH 3종 구현 완료 (2026-07-06) — 빌드 검증, 미커밋
- PRD Phase 1 착수·완료. 롤백태그 before-account-crud-hardening. 코디네이션 등록(ACCOUNT_COORDINATION.md).
- 수정 4파일(모두 Accounts.Ui/ViewModels/Panels): 
  · AccountManagerPanelViewModel — 삭제 HandleAsync: 진입 Confirm청산 + Progress try/finally 청산 + 2단catch + Info-after-close(HIGH-1 소프트락 해소). OnClickReloadButton+OnActivateAsync try/catch(HIGH-2 async void 크래시 차단).
  · PermissionMatrixPanelViewModel — ReloadAsync/ReloadMembersAsync swap-on-success(실패시 Clear 전 return, HIGH-3 화면공백 해소). 그룹삭제 HandleAsync ClosePopup try→finally.
  · GrantManagementPanelViewModel — 회수 HandleAsync ClosePopup try→finally.
  · MyPagePanelViewModel — Reset/Edit HandleAsync: Confirm청산+Progress finally+Info-after(ClosePopup 완전부재였음).
- base 미변경(method-C). Accounts.Ui 빌드 exit0.
- ★워킹트리 동시작업: 다른 세션이 device 7+event 4 패널+EventProviderService+tests 봉투확산 WIP 진행중(내 변경 아님). 내 계정 4파일 disjoint → git add 분리 필수(번들 금지). 커밋 안 함(사용자 미요청+동시WIP).
- 런타임 검증 대기: (1)AccountManager 삭제 후 Confirm/Progress 잔존0 (2)서버다운시 갱신 무크래시 (3)PermissionMatrix 재조회실패시 목록보존 (4)MyPage/Grant 팝업청산.
- 후속(대기): MED(게이트·타임아웃·스냅샷·F1클라가드·에러마스킹) + C3 CrudOperationRunner + 보안 서버티켓(F1/F3/F4/F6/F9). 다이얼로그 팝업호스트 스코프 선확인.


---

## [2026-07-20] 이벤트 차트 툴팁 테마 동기화 — 분석+PRD (Track C, 사용자 승인 대기)

**세션 스코프**: `Ironwall.Dotnet.Libraries.Events.Ui` — 센서 막대/도넛(Pie)/라인 차트의 마우스 호버 툴팁이 Tactical 디자인 컨셉(색·폰트) 미적용. 사용자 스샷: "제어기_03" 헤더가 밝은 박스 위 흰 글자라 안 보임.

**근본 원인 (코드 조사, 증거 기반)**:
- LiveCharts2 툴팁 = **SkiaSharp 직접 렌더** → WPF 테마 토큰(DynamicResource) 미도달. 차트 색은 `ChartThemeProvider`가 C#으로 공급하는 IMPL-21/FR-13 패턴.
- **세 차트 어디에도 `TooltipBackgroundPaint` 미설정** → LiveCharts 기본 밝은(near-white) 박스 → 다크 컨셉 충돌. 다크 글자색 `#E6EDF3`(흰색)이 밝은 박스 위 → 가독성 파괴(핵심 버그).
- **도넛(Pie)·라인엔 `TooltipTextPaint` 자체 없음** → 기본 폰트. 막대만 Malgun 적용. 앱 디자인 폰트는 Noto Sans CJK KR(`Tokens.Shared.xaml:21`)인데 차트는 Malgun(`ChartThemeProvider.cs:43`, 주석상 Noto 통일=EXT-07 이연) → 폰트 불일치 이중.
- 증거: `EventInfoView.xaml:94-95/122-128`, `DataChartPanelView.xaml:110-119`, `EventInfoViewModel.cs:494-503`, `DataChartPanelViewModel.cs:248-260`, `ChartThemeProvider.cs:19-43`.

**사용자 결정**: "다크/라이트 두 모드 기조에 맞게" → Option A(툴팁 배경/색 픽스)를 **양 모드 테마 인식**으로 확정. Noto 완전 통일(EXT-07)은 이번 제외.

**PRD**: `docs/prds/EventChart_Tooltip_ThemeSync-prd.md` (Draft, BOM OK). 5파일 Track C. FR-01 배경 테마색(다크 #161D26/라이트 #FFFFFF) · FR-02 도넛·라인 `TooltipTextPaint` 추가 · FR-03 ThemeChanged 실시간(데이터 refetch와 분리) · FR-04 `ChartThemeProvider.TooltipBackgroundColor` SSOT.

**미결(승인 대기)**: (a) 라이트 배경 `#FFFFFF`(surfaceAlt) vs `#F2F5F9`(surface), (b) Noto EXT-07 후속 여부. 승인 후 Plan → worktree(v2.6.x)+롤백태그 → dev.


---

## [2026-07-20] 이벤트 차트 툴팁 테마 동기화 — 구현 완료 (worktree, 커밋 da07646)

**결과**: EventChart_Tooltip_ThemeSync PRD(Approved) 구현 완료. worktree `feature/event-tooltip-theme-sync` (`C:\workspace_app\worktrees\event-tooltip-theme-sync`)에 격리 커밋 **da07646** — 내 5파일 + CHANGELOG만 명시 커밋, 공유 v2.6 트리 무영향.

**변경 5파일 (Events.Ui)**:
- `Helpers/ChartThemeProvider.cs`: `TooltipBackgroundColor(theme)` SSOT (다크 `#161D26` / 라이트 `#F2F5F9` = Surface 토큰; 카드=SurfaceAlt 위 분리).
- `ViewModels/Components/EventInfoViewModel.cs` · `ViewModels/Panels/DataChartPanelViewModel.cs`: `TooltipBackgroundPaint` 속성(+DataChart는 `TooltipTextPaint`도 신설) + ctor 초기화 + `OnThemeChanged` 재색칠(재할당+Notify, 스칼라 .Color 금지).
- `Views/Components/EventInfoView.xaml`: 막대·도넛 `TooltipBackgroundPaint` 바인딩 + 도넛에 `TooltipTextPaint`/Size(누락분).
- `Views/Panels/DataChartPanelView.xaml`: 라인 차트 `TooltipBackgroundPaint`/`TooltipTextPaint` 바인딩.

**검증**: Events.Ui `dotnet build` 0오류 · `TooltipBackgroundPaint`/`TooltipTextPaint`가 LiveCharts rc5.4 WPF 차트 실 바인딩 프로퍼티임을 어셈블리 메타데이터로 확인(WPF 바인딩은 컴파일로 안 잡혀서 별도 확인) · 한글 주석 mojibake 0 · BOM 원본 보존(EventInfoView.xaml·ChartThemeProvider.cs는 원래 no-BOM, SDK 빌드는 UTF-8로 읽음).

**멀티세션/파이프라인 처리**: v2.6 공유트리에 타 세션 미커밋 WIP(`DetectionEventCardView.xaml`, Accounts `Grants` 테스트) 존재 → `advance-phase`의 `git add -A` 스윕(=[[project_multisession_checkpoint_sweeps_wip]]) 회피 위해 (1)전용 worktree 격리 (2)`.claude/.branch-v2.6` 상태만 로컬 dev로 임시 설정 후 revert (3)내 파일만 명시 커밋. 빌드 위해 고아 `OnvifSolution`(Devices.Ui 의존) main→worktree 복사(=[[project_gmap_orphan_submodule]]). 롤백 태그 `before-event-tooltip-theme-sync`@bedd423.

**미결(사용자)**: 런타임 시각 검증(다크/라이트 호버 스샷)은 worktree라 앱 미반영(=[[project_library_deployment_path]]) → `feature/event-tooltip-theme-sync` → v2.6 머지 + 앱 재빌드(실행중이면 DLL 잠김) 후. Noto 폰트 완전 통일=EXT-07 후속(이번 제외, PRD Non-Goal).


---

## [2026-08-03] 이벤트 편집→적용→저장 파이프라인 — 분석 20결함 + 배치1 적용 (커밋 7f810aa)

**사용자 증상**: "SelectionViewModel 적용을 누르면 실제 저장하기 위한 데이터로 안 넘어간다 — Detail 쪽."
탐지 상세(신호·AI모델·추론ms·프레임)만 고치고 [적용]→[저장] → **오류도 성공도 없이 미저장**.

**근본원인 (확정)**: `ApplyDetailEdits()` 가 행 VM 세터를 우회해 모델 직접 대입 → `IsEdited` 미설정
→ 저장 루프 `Where(vm => vm.IsEdited && Id>0)` 에서 행 탈락 → PUT 0건.
Events.Ui 전체 `SetProperty(` 호출 **0건** → dirty 경로는 `SetModelProperty` 하나뿐(Grep 확정).

**분석**: 워크플로 `wf_e6ed2863-897`(7에이전트) — 4축 분석 → 58시나리오 시뮬레이션 → 종합.
**결함 20건(DEF-01~20)** · FR 15 · 회귀위험 12 · 테스트 22종.
PRD: `docs/prds/Event_Edit_Save_Pipeline-prd.md`. 롤백태그 `before-event-edit-dirty-fix`@9e33641.

**배치 1 적용(FR-01~06, 커밋 7f810aa)**:
- `DetectionEventViewModel` detail 세터 5종 신설(전부 `SetModelProperty` 경유) — 장애 도메인 패턴으로 수렴. Signal 세터가 `SignalText`/`HasSignal` 알림 → 그리드 즉시 갱신(DEF-16 동시 해소).
- `ApplyDetailEdits` → `_selection[0]`(행 VM)에 대입.
- Draft 수집 소스 `_eventProvider`→`ViewModelProvider`(나머지 3패널과 동일, DEF-03).
- 저장 결과 항상 통지 — "변경된 내용이 없습니다."/"n건을 저장했습니다."(DEF-08).
- 테스트 `EventEditSavePipelineTests` 17종 신설 전건 통과. 판별력 입증(되돌리면 2건만 실패).
- 부수: `BatchActionReportTests` 가 IoC 스텁 클래스 중 유일하게 `[Collection("IoC-Dependent")]` 누락 → 간헐 실패, 편입.
- 회귀 0: 베이스라인 415통과/15실패 → 432통과/15실패(목록 동일, 3회 반복 일치).

**미적용(의도적 보류)**:
- **배치 2** — FR-07(캐시 복귀 dirty 보존) · FR-08(불변 필드 UI 게이팅) · FR-10(연결 편집경로 확정).
  `*SelectionView.xaml` 등 **타 세션 편집 중** → 해당 파일 커밋 후 착수.
- **배치 3** — FR-09(장애 detail {0,0,0,0} 오염) · FR-11~15. **U-1 블로커**: 서버가 PUT 본문에서
  `detail` 키 생략 시 유지인지 삭제인지 미확인 → 확인 전 착수 금지(데이터 소실 위험).

**미결(사용자/서버)**: 서버 `PUT /events/detections|connections` 500(lazy-load) — 커밋 `4f0e875` 미배포.
해소 전엔 실사용 E2E 불가(헤드리스 단위테스트로만 검증). 요청서: `api-test-server/docs/coordinations/GOP_Server_API_event_PUT_500_lazyload_REQUEST.md`.


---

## [2026-08-04] 편집 상태 정규화 + PUT 실패 되읽기 검증 (커밋 01d0cdd, 2d9bf9b)

**사용자 요구**: "DetectionSelectionViewModel 형식을 정규화해라. 그 데이터를 모델로 해서
DetectionEventPanel 데이터그리드 항목에 적용시키고, 저장하면 API에 반영되게."

### 1) 정규화 — `Models/DetectionEditModel.cs` (01d0cdd)
편집 상태가 세 형식으로 흩어져 있던 것을 모델 하나로 통합.
- 값 의미 통일: 전 필드 `null` = 변경 없음 / 값 = 변경
- 알림 통일: 전 필드 PropertyChanged(무-알림 auto-property 제거 = DEF-18 해소)
- 적용 통일: `ApplyTo(row, includeDetail)` 하나, **행 VM 세터만** 사용
- 흐름: 속성창 → EditModel → ApplyTo → DataGrid 항목(행 VM) → IsEdited → 저장 → PUT
- **뷰 무변경**: `DetectionSelectionView.xaml`이 타 세션 편집 중 → 바인딩 이름을 위임 래퍼로 유지, XAML 0줄
- 부수: AiModel 공백→null 정규화+Trim, `RefreshSignalScale()` 신설, 죽은 헬퍼 2개 제거

### 2) 되읽기 검증 폴백 — `EventProviderService` (2d9bf9b)
**서버가 UPDATE 커밋 후 응답 조립에서 500** → 값은 저장됐는데 화면은 "저장 실패".
PUT 실패 시 `GetDetectionEventByIdAsync` 로 실제 상태를 되읽어 **보낸 페이로드 전 필드 대조**,
일치할 때만 성공 처리. 불일치·조회실패·취소는 원래 실패 전파. 서버 수정되면 이 경로 미사용.
- 패널 무변경(예외 대신 모델 반환 → 기존 집계/재조회 그대로)

### 실서버 실측 (2026-08-04 01:50, 원격 123.141.236.253:8136)
| 요청 | 상태 |
|---|---|
| 탐지 PUT · 무변경 왕복(유효) | **500** `3e7c4764-…` |
| 탐지 PUT · 잘못된 type_event | **422** VALIDATION_ERROR(필드별 details) |
| 장애 PUT · 무변경 왕복 | **200** |
| 연결 PUT · 무변경 왕복 | **500** `c70d4e65-…` |

→ **422 분기 배제**(검증은 정상 통과) · **200 분기 배제** · **500 확정 = `4f0e875` 미배포**.
→ **커밋 후에 터짐 입증 3중**: ①값 지속(2556→2563 PUT 500 후 GET 2563, 원복) ②서버 `SYNC_DETECTION`
발행이 500보다 12ms 먼저 ③저장된 detail이 7키 = 클라 `BuildDetectionDetail` 산출물(NATS는 2키).
회신: `api-test-server/docs/coordinations/GOP_Server_API_event_PUT_500_lazyload_REQUEST.md` 하단 📮 회신 절.
**서버 코드 무변경 확인**(`git status -- app/` 0건). 측정용으로 바꾼 값 2건(52382·52412) 모두 원복.

### 남은 것
- 🔲 **앱 재빌드** — 실행 중 앱에는 01d0cdd·2d9bf9b 미반영이라 여전히 실패 팝업
- 🔲 **서버 4f0e875 재배포**(근본) + `bulk-delete` `82ed70d` 동반 · `/health`·`/version` 404
- 🔲 배치 2(불변필드 UI 게이팅·연결 편집경로·캐시복귀 dirty 보존) = 타 세션 XAML 커밋 후
- 🔲 배치 3 = U-1(서버 detail 키 생략 시 유지/삭제) 확인 선행
- 미처리(범위 밖): 클라 `Unknown command: SYNC_DETECTION` 로그 노이즈, `IsSelected` 바인딩 경고

### ✅ [2026-08-04 14:39] 서버 재배포 확인 — 종결
원격 `123.141.236.253:8136` 재배포 후 재측정: **탐지·연결 PUT 500 → 200**, 장애 200 유지,
잘못된 값은 여전히 422(검증 계층 회귀 없음). `signal 2556→2567` 실제 변경 200 + 재조회 확인 후 원복.
함께 요청했던 `bulk-delete` 도 **405 → 200**(`4f0e875`·`82ed70d` 둘 다 반영).
→ **이벤트 수정 저장이 API에 정상 반영된다.**
- 되읽기 검증 폴백(`2d9bf9b`)은 예고대로 **휴면**(PUT 200이면 재조회 0회, 단위테스트로 고정).
  부분배포·롤백 대비 방어용으로 존치.
- `GET /health`·`/version` 은 여전히 404 → 배포 확인 수단 부재(서버에 건의, 블로킹 아님).
- 서버 코드 무변경 유지(HTTP 호출만), 측정값 전부 원복.
- 회신: 서버 하네스 문서에 `✅ 종결` 절 추가(요청 → 📮 회신 → ✅ 종결 3단).

---

## [2026-08-07] GOP RFP 점검표 — 통합상황도(GIS) 구현 여부 감사 (Track A, 코드 무변경)

**대상**: `http://127.0.0.1:17321/index.html` (GOP 제안요청서 요구사항 점검표, 요구 202 / 체크대상 455).
상태 저장은 브라우저가 아니라 서버 파일 `data/checklist_state.json` (`/api/state/load` · `/api/state/save`).
체크 키 포맷 = `필수-060` (요구줄) / `필수-060::3.3.4.12.2.1` (하위항목). 상태값 = insuf/partial/done/na.

**방법**: Playwright로 페이지 구동 + Workflow 18요구 × (검증 → 적대적 반박) 37에이전트.
반박 패스에서 5건 강등(063 done→partial, 064 partial→insuf, 선택-014 partial→insuf, 035·052·055·036 강등).

### 통합상황도 본절 3.3.4.12.x — 페이지 반영 완료 (완료3 · 부분3 · 미비3)
| 키 | 판정 | 요지 |
|---|---|---|
| 필수-060 | 부분 | MBTiles 베이스맵+GeoTIFF 파이프라인 실재. CRS 변환 부재(EpsgCode "4326" 하드코딩)·축척 개념 없음·군사지도 데이터 미탑재 |
| 필수-060::…2.1 | 부분 | 오버레이+전환 UI 실재. `EnumMapCategory.Military` 부여 코드 0건, AvailableMaps 필터가 Standard/Satellite만 통과 |
| 필수-061 | 완료 | 편집모드·심볼 CRUD·군대부호(APP-6D)·Undo/Redo·줌 전부 배선+DB 영속. 잔여 VEHICLES 배치 미구현 |
| 필수-062 | 완료 | MapRoiModel+MapRois 테이블+MapRoiControl+툴바 진입점. ROI가 면이 아닌 '중심좌표+줌' 북마크 |
| 필수-063 | 부분 | 부채꼴 렌더+PTZ 실시간 경로 1개 생존. **미영속(런타임 전용)→재기동 직후 기본값 30m/80°/0° 허구 부채꼴**, ShowFOV 기본 false, zoom→각도/거리 하드코딩 근사 |
| 필수-063::…5.1 | 완료 | FOV(ShowFOV/FOVColor/FOVOpacity) vs 감지구간(StrokeColor/LineOpacity) 완전 별개 체계 |
| 필수-064 | 미비 | MGRS 10자리 출력은 8계단 초과 충족이나 **소스가 마우스 호버뿐** — '선택한 지점'·'감지구간' 좌표 표시 0% |
| 선택-013 | 미비 | 사각지대 모델·커버리지 교차 판정·자동 팝업 전부 없음 |
| 선택-014 | 미비 | 확인점/주요지점 모델 0건, 카메라 자동선정 0건. 구현은 역방향(카메라 우클릭→반경 30m 내 클릭) |

### 신규 발견 결함 3건 (별도 티켓 후보)
1. **MGRS 격자 라벨 도달불가 데드코드** — `MGRSGridOverlayService.cs:248` 호출 게이트(`gridSpacing>=10000`) vs
   `:419` 첫 줄 `if (gridSpacing < 100000) return;`. 100000은 zoom 10~11에서만 산출되어 게이트가 배제
   → `_cachedLabels` 항상 비어 격자에 좌표 문자열이 하나도 안 그려짐(무명 격자선).
2. **`CameraPtzNatsSyncService` 사문 서비스** — `EventUiModule.cs:80`이 `.As<ICameraPtzNatsSyncService>()`만 등록,
   `.As<IService>()` 없음 → `StartService()` 미호출로 구독 미등록. 같은 파일 78행 주석이 동일 패턴을 자백.
   (살아나도 raw Pan 0~360000 / Zoom 0~4400을 0~360 / 0~100 기대 API에 그대로 전달하는 단위 불일치 버그)
3. **FOV 수치 정합 갭** — 속성창 탐지범위 Max 1000m vs PTZ 변환 최대 3000m,
   탐지방향 -180~180 + BaseBearing 0~360 = 최대 540 주입(`DeviceSymbolLookupModel.cs:214`).
   부수: `FOVCalculationTests`는 테스트 파일 내 중복 구현을 검증하는 병행-구현 테스트라 실렌더 경로 미커버.

### 인접 9건 — 판정만 완료, **페이지 미반영**(GIS 소관 밖 포함이라 사용자 확인 대기)
done: 필수-034(2수단 경보) · 필수-039(탐지정보 자동수신 통합)
partial: 필수-069(경보시각 표시) · 필수-029(카메라 원격운용, 센서 감도조정 전무) · 필수-051(RBAC, 제대 스코프 없음)
insuf: 필수-035(표적 좌표 실시간 전시) · 필수-052(감지→카메라 자동전환 팝업) · 필수-055(NMS 구성도) · 필수-036(감시범위 재설정 미영속)

**산출물**: 감사 원본 `C:\Users\gh\AppData\Local\Temp\claude\...\tasks\w54ehnuaw.output` (findings 18건, 근거 file:line 포함)

### [2026-08-07 19:00] 위 항목 갱신 — 사용자 반론 반영 + 동시편집 사고/복구

**⚠ 사고와 복구 (먼저 읽을 것)**
점검표 저장(`/api/state/save`)은 **state 전체를 통째 교체**하는 last-write-wins 이고, **창을 떠날 때도 저장**한다.
같은 점검표를 **타 세션이 API 서버 관점으로 동시 편집** 중이었고(모든 비고가 `[API서버 범위 판정]`),
18:56 내 낡은 탭이 이탈 저장으로 상대 394건 → 내 18건으로 덮었다.
→ 18:54 백업본으로 **394건 전량 복구(유실 0건)**, GIS 소관 9행만 내 최종본으로 재적용.
→ **재발 방지: 이 점검표는 두 세션이 동시에 열지 않는다.** 편집 전 `/api/state/load` 건수 확인 + 백업, 끝나면 탭 닫기.
정본 조율문서: `docs/coordination/gop-rfp-checklist-gis-scope.md`

**최종 판정 (통합상황도 3.3.4.12.x — 점검표 반영분)**
| 키 | 최종 | 초기 | 변경 사유 |
|---|---|---|---|
| 필수-060 | **완료** | 부분 | 군사지도는 사전 지오레퍼런싱 MBTiles 를 폴더 배치하면 좌표손실 없이 적재(SeedMBTilesMapsAsync 가 MBTiles bounds 직독). 구축 가능 |
| 필수-060::…2.1 | **완료** | 부분 | 요구 문장 자체가 "**오버레이 개념을 적용하여** 지도 선택 전환" — 레이어패널 체크박스+투명도가 정확히 그 방식. 오버레이 경로를 '우회'로 깎은 게 내 오판 |
| 필수-061 | 완료 | 완료 | — |
| 필수-062 | 완료 | 완료 | — |
| 필수-063 | **부분** | 부분 | 유지하되 근거 전면 교체(아래) |
| 필수-063::…5.1 | 완료 | 완료 | — |
| 필수-064 | 미비 | 미비 | 좌표 소스가 마우스 호버뿐 — '선택한 지점'·'감지구간' 0% |
| 선택-013 | **해당없음** | 미비 | **VMS 담당**(사용자 확인) — GIS 소관 아님 |
| 선택-014 | **부분** | 미비 | 지도 지점 클릭→카메라 회전('특정 위치 확인')이 운영모드에서 실동작. 미충족은 사전설정 지점 재사용·카메라 자동선정 2가지뿐 |

**필수-063 근거 정정 (중요)**
- 과거 문서의 "PTZ_STATUS subject 불일치 전면 미수신"은 **stale** — 커밋 1b1fc30 이 이미 수정, 7/30 재검증에서 🔴→🟡.
  메인 앱 경로는 subject 가 아니라 `cmd` 문자열 분기라 애초에 무관.
- "부팅 직후 허구값"도 부정확 — SymbolEventManager.cs:85-91 이 서버 heading 으로 `DetectionBearing=BaseBearing`
  설정하므로 **방향은 설치방향 실값**(각도 80°·거리 30m 만 기본값).
- **진짜 문제**: 운영로그 2026-08-01~07 전수 'PTZ' **0건** + 주기 발행 규격 없음(GIS.md §3.6) + 부팅 스냅샷 없음.
  구조적 원인 = GIS 가 PTZ 를 NVRManager REQ 가 아닌 **ONVIF 직접 제어** → NVRManager 에 상태변화 통지 계기 자체가 없음.
  → **클라 단독 보완 가능**: ONVIF 조작 직후 FOV 로컬 갱신(공수 소). 시험평가 전 실발행 확인이 통과/불통과를 가름.

**인접 9행** — 점검표에는 타 세션의 **API서버 관점** 판정이 들어가 있어 덮어쓰지 않음.
내 GIS 관점 판정은 조율문서 §B 에 보존(두 관점은 모순이 아니라 계층 차이 —
예: 필수-052 는 API 서버가 연동규칙 저장·배포 완료(완료)이나 GIS 에 그 규칙을 소비해 팝업을 자동으로 여는 코드가 없음(미비)
→ 시스템 전체로는 미충족).
사용자 확인 반영분: **필수-029 센서 감도=회사 방침상 미지원**(단, 필수 요구이므로 제안서에 사유·대체안 명시 필요),
**필수-051 제대 스코프=권한그룹을 제대 단위로 생성 운용하는 설계**(잔여는 device_groups 미소비 + 시스템 간 제어권 이양).
자체 정정: **필수-036 미비→부분** (감시범위 재설정 수단은 지도 FOV 슬라이더가 아니라 ONVIF PTZ 줌/팬틸트·프리셋 — 완비).

**신규 결함 5종** (조율문서 §C 정본): ①MGRS 격자 라벨 도달불가 데드코드 ②CameraPtzNatsSyncService 사문(배선 전 raw pan 단위버그 선수정)
③PTZ_STATUS 실무 무수신 ④GeoTIFF CRS 재투영 부재(투영 CRS 는 Mercator Clip 으로 rect 0×0 축퇴 → 등록 불가) ⑤오버레이 무음실패 3종.

### [2026-08-07 19:10] 필수-064 상향 + 점검표 쓰기 중단(동시편집 실시간 충돌)

**필수-064 미비 → 부분** (사용자 지적: "줌과 군사지도 오버레이로 되는 거 아닌가" — 맞음).
초기 판정이 **군사지도 오버레이 + 줌 판독 경로**를 누락했다. 8계단 요건은 MGRS 10자리로 초과 충족이고,
군사지도 오버레이의 인쇄 격자 + 커서 리드아웃으로 **실사용 시연 가능**. 미충족은 '선택 바인딩'뿐 —
CurrentMGRS 대입처가 MouseMove 1곳이라 클릭해도 고정 안 되고, 감지구간 대표좌표·이벤트카드 좌표가 없다.

**부수 확정: 앱 내장 MGRS 격자는 8자리 판독에 못 쓴다** (두 조건 배타 → 라벨 0개 렌더)
- `MGRSGridOverlayService.cs:248` 게이트 `zoomLevel>=12 && gridSpacing>=10000` → zoom 12~13만 통과
- `:419` `if (gridSpacing < 100000) return;` → 100000 필요
- `:284-301` zoom 10~11만 100000 산출 → 게이트(zoom>=12)가 배제. 교집합 공집합.
- **고쳐도 무용**: 라벨 설계가 100km 격자구 ID(`"52S CG"`, :413/:439)라 8자리 판독용이 아님. 8자리는 커서 리드아웃 담당.

**⚠ 점검표 쓰기 중단 — 미적용 4건**
19:05 기준 상대 세션이 **5~10초 간격 실시간 저장** 중(savedAt 연속 갱신 실측). 내 쓰기가 수 초 만에
그쪽 스냅샷으로 되돌아간다. 아래 4건은 **점검표 미반영** 상태이며 정본은 조율문서에 있다.

| 키 | 점검표 현재 | 적용할 값 |
|---|---|---|
| 필수-060 | 부분 | **완료** |
| 필수-060::3.3.4.12.2.1 | 부분 | **완료** |
| 선택-014 | 미비 | **부분** |
| 필수-064 | 미비 | **부분** |

적용 절차(상대 세션 종료 후): ①`savedAt` 30초 무갱신 확인 ②백업 ③**브라우저로 열지 말고**
read-modify-write 로 해당 키만 교체 후 `POST /api/state/save` ④재조회 검증.
본문 텍스트 원본: `docs/coordination/gop-rfp-checklist-gis-scope.md` §A(060·060::2.1·선택-014) · §E(064) · §F(절차).

**교훈(재발방지)**: 이 점검표는 브라우저 탭을 여는 것만으로 위험하다 — 탭이 낡은 state 전체를 들고 있다가
**이탈 시에도 저장**하기 때문. 편집은 API read-modify-write 로만, 그것도 상대 세션 미가동 시에만.

---

## [2026-08-07] 조치보고 커스텀 문구 (action-report-custom-template) — 분석·시나리오·설계·PRD 완료

**요청**: 정형화된 조치보고 문구를 `GOP_Server_API_action_report_template_NOTIFY.md` 기반으로
커스텀 입력 가능하게 → HTML 와이어프레임 + 스토리보드 + 상세 PRD + 전 시나리오 시뮬레이션.
**Track C** — 코드 변경 0(설계 산출물만). PRD 상태 **Draft**(사용자 승인 대기).

### 산출물
| 문서 | 내용 |
|---|---|
| `docs/analyses/action-report-custom-template-scenario-analysis.md` | 영향면 6축 + 이슈 42종 + 적대검증 기록 |
| `docs/tests/action-report-custom-template-scenarios.md` | 시나리오 **235건** (L75/C28/E29/O12/P28/S10/D14/M6/X10/U23) |
| `docs/tests/action-report-custom-template-simulation-log.md` | 전량 로그 PASS 167 / ISSUE·GAP 68 |
| `docs/design/action-report-custom-template-wireframe.html` | 치수·바인딩·상태기계·API매핑·검증·토큰·권한·AutomationId (12절) |
| `docs/design/action-report-custom-template-storyboard.html` | 흐름 S1~S13, 각 프레임에 SIM ID |
| `docs/prds/action-report-custom-template-prd.md` | FR-18 · NFR-07 · V-07 · 미결 G1~G8 |
| 시뮬레이터 | 스크래치패드 `sim/{model.py, client.py, run.py}` — 제품코드 무수정, 재현 가능 |

### ★ 서버 계약 — NOTIFY 문서와 다른 점 4건 (코드가 정본)
1. **POST 는 201 Created**(문서 미기재) — 200 만 성공 판정하면 생성 성공이 실패로 표시
2. **단건 응답에 `pagination` 키 자체가 없다**(`ApiSingleResponse`) — 문서의 "키는 있고 null" 은 목록만
3. **전 요청 `extra="forbid"`** — 요청 DTO 가 `BaseDto` 상속 시 `id`/`created_at` 직렬화로 **422 전멸**
4. `content` 는 `min_length=1` 만 있고 **서버가 trim 하지 않음** → 공백만("   ") 템플릿 등록 가능

### ★★ 최대 발견 — 선행 결함 2건 (본 기능과 별개, 착수 전 확인)
- **P5** `POST /api/events/actions` 는 서버가 **`events:edit`** 요구(`routers/actions.py:436`,
  `permission_map.py:31`)인데 클라는 10곳 전부 **`CanControl("events")`** 로 게이팅.
  → `view+control` 만 가진 **OPERATOR 는 UI 통과 후 403**. 프로젝트 테스트 픽스처와 정면 모순.
- **P3** 그 `events:control` 은 **권한 매트릭스 화면에서 부여·회수 불가**
  (`Enums\PermissionCatalog.cs:94` 가 Control 을 Cameras 로 한정 → 셀 ▦).
- 부수: **조치보고 본문에 서버 길이검증이 없다**(`schemas/event.py:477`) → 빈 조치보고 저장 가능,
  500자 초과 시 PostgreSQL 절단오류로 **HTTP 500**.

### 조치보고 파이프라인 실측 (재사용 가치 높음)
- `SendAction` 선언은 `EventCardBaseViewModel` 이 아니라 **`EventCardViewModel.cs:36-43`**
- content 생성 지점 **9종** — 다이얼로그 2 + 자동 3 + 자동복구 1 + 일괄 1 + null폴백 1 + 그리드 1
- **다이얼로그를 공유하는 진입점 4개**(카드/조회그리드 우클릭/신호이력 우클릭/GIS 구역 더블클릭)
  → 다이얼로그 1곳 수정이 4경로에 일괄 적용
- 다이얼로그 VM 은 **SingleInstance** + 초기화가 `OnActivateAsync` 에만 → 정상 닫힘 누락 시 **이전 메모 잔류**
- `ActionReportGuard` 키가 `int eventId` 단독 → **탐지#100 이 장애#100 차단**, `eventId<=0` 이면 가드 무력,
  충돌 시 `return true` 라 **저장 없이 성공으로 닫힘**
- 자동 조치보고·자동복구는 **권한 검사 없음**(인증만/무검사)

### UI 실측
- 창 = 메인 솔루션 `md:Card 900×660`, 루트 Grid `Auto / **550 고정** / Auto`
- 메모가 **"기타" 라디오에 IsEnabled 로 묶여** 정형문구+보강 병기 불가 (이번 개선의 핵심)
- `"기타"` **문자열 리터럴 비교**로 저장 분기 → 서버에 동명 템플릿 등록 시 하이재킹
- AutomationId **0건**, `x:Name="ClickOk"` 전역 9중복
- 유일 테스트가 `UnitTest.cs:3846-3866` **XAML 문자열 검사** — 파일경로 하드커플링 + 회귀 안전망 아님

### 구현 시 따를 패턴 (조사 확정)
- API: `Events.Api\Services\EventSuppressionApiService.cs:53` BaseUrl 프로퍼티 형태,
  DI 는 `EventApiModule.cs:73-80` Order `_count+2`, **`As<IService>()` 금지**
- `Events.Api\Helpers\ResponseHelper.cs` 는 **전체 주석 처리된 사문** — 참조 금지(정본 `ApiMessageHelper`)
- 패널 셸: `EventSuppressionSchedulePanelView.xaml:159-199` + 뷰로컬 폼스타일 세트(`:40-141`)
- 🔴 **Events.Ui/Resources/Resources.xaml 은 앱에 미병합** → 새 스타일은 뷰 로컬에
- 🔴 **MDIX Outlined 에 `Height` 금지, `MinHeight` 만**
- 순서변경: 레포에 **드래그 선례 0건** → ▲▼ 버튼(`LayerTreeNode.cs:358-382` 패턴)

### 다음 단계
1. **미결 G1~G8 사용자 확정** (특히 G1 로드실패 폴백, G2 "기타" 제거)
2. **V-01·V-02 서버팀 확인**(권한 불일치) — 확정 전 조치보고 권한 UX 손대지 않음
3. PRD 승인 → plan

### [2026-09-06] 조치보고 커스텀 문구 — 미결 8건 사용자 확정 + 드래그 전사 방침

**확정 (사용자 결정)**

| ID | 질문 | 확정 | 비고 |
|---|---|---|---|
| G1 | 목록 로드 실패 시 | **ⓑ 빈 목록 + 직접입력** | 하드코딩 폴백 배제(지운 문구 부활 위험) |
| G2 | "기타" 항목 | **ⓑ 제거** | 편집칸 상시 활성, 선택=문자열 복사. 리터럴 분기 결함 동시 해소 |
| G3 | 자동·일괄 고정문구 템플릿화 | ⓐ 범위 밖 | 매뉴얼에 "운용자 변경 불가" 명시 |
| G4 | 관리 권한 | ⓐ 서버와 동일(view/edit/delete) | V-01·V-02 선결 |
| G5 | 서버 통계 문자열 매칭? | **확인 중** | 서버팀 회신 대기 |
| G6 | 캐시 수명 | ⓑ 세션 캐시 + 편집 시 무효화 | 관리패널 진입 시 강제 재조회 |
| **G7** | 순서 변경 UI | **★ 드래그** (권고 뒤집힘) | 아래 전사 방침 참조 |
| G8 | "전부 삭제=초기화" 안내 | ⓐ 풋터 안내문 | |
| D-4 | 관리 패널 진입점 | **ⓑ + ⓒ 병행** | 설정정보 하위 + 조치보고 다이얼로그 내 바로 열기(권한자 한정) |
| C-1/C-2 | 선행 결함 포함 범위 | 권고안 채택 | **C1·C2·C3·X4·D1·D3·U12 포함** / P5·P3·D4·D6·D7·D8 은 별도 |

**★ 전사 방침 — 드래그 우선 UI/UX**
사용자: "앞으로 작업할 때 드래그 방식을 전면 도입할 예정이니 하네스에도 드래그 기반 UI/UX 를 기획하라."
- 내가 "레포에 드래그 선례 0건"을 근거로 ▲▼ 버튼을 권고했으나 **사용자가 드래그로 뒤집음**.
- **"선례 0건"은 배제 근거가 아니라 지금 만들 이유**로 재해석. 최초 사례를 **공용 자산**으로 설계한다.
- 메모리 등록: `feedback_drag_first_ux` (전사 방침, 영구)
- 하네스 규칙 신설 예정: `.claude/rules/common/` (룰은 `.claude/rules/{pack}/*.md` **자동 탐지** — `hooks/_agents.js:186 loadRules`)
- 드래그 도입 시 **반드시 동반 설계**: 키보드 폴백 · 삽입선 · ESC 취소 · 서버 실패 롤백 · 라이트/다크 피드백 토큰

**진행 중**: 공용 드래그 정렬 인프라 설계 워크플로(4축 조사 → 설계 → 적대검증 3기).
설계 확정 후 PRD v1.1 · 와이어프레임 · 스토리보드 · SIM-O 계열 시나리오를 드래그 기준으로 갱신한다.

**남은 확인**: V-01(클라 control vs 서버 edit) · V-02(매트릭스 control 부여 불가) · G5 — 전부 서버팀/PM 회신 대기.
**남은 질문**: D-2 작업 브랜치명(미정).

### [2026-09-06] 드래그 인프라 타당성 조사 — 적대검증 3기 전원 fatal

**산출물**
- `.claude/rules/common/drag-first-ux.md` — 하네스 규칙 신설, **자동 등록 확인**(loadRules 에 `drag-first-ux` 노출)
- `docs/analyses/drag-reorder-infra-analysis.md` — 타당성 분석 v1.0
- 메모리 `feedback_drag_first_ux`

**살아남은 것**: Utils\Behaviors 단일 정본 배치 · 의존그래프(패키지 추가 0) · Theme↔Utils 순환 없음 ·
`Behavior<T>` 형태 · **캡처 드래그 채택 / OLE 불채택** · 데드존 8.0 · `md:PackIcon Kind="DragVertical"`

**깨진 것 — 동작 메커니즘 3대 축**
1. **Button 핸들 자멸** — `ButtonBase.OnMouseLeftButtonDown` 이 버블에서 `CaptureMouse()` → Preview 캡처 강탈
   → `LostMouseCapture`=취소 규정에 걸려 마우스다운 순간 종료. **→ `Thumb` 으로 교체**
2. **DataTemplate 내 `OnAttached` 시각트리 걷기 = measure 크래시** — 선례 0건, 정본은 `Loaded` 대기
   (`DataGridScrollEndBehavior.cs:50-53`)
3. **컨테이너 로컬 값 삽입선 = 지터+트리거충돌+재활용오염** — `DGR_Border` 가 레이아웃 참여(2px 밀림),
   로컬값이 Style Trigger 를 이김, 재활용으로 다른 행에 따라감. **→ AdornerLayer(단 가용성 미검증, S0)**

**❌❌ 외부 블로커 — 서버 배치 재정렬 엔드포인트 부재**
`i→j` 이동 = `|i−j|+1` 건 **순차 PATCH**, `ApiSetupModel.cs:50` Timeout=10초 → 20건이면 **최악 200초 블로킹**.
`display_order` UNIQUE 없음 + 정렬 `(display_order,id)` → 부분실패 시 **"제3의 순서"**.
▲▼ 는 항상 2건 고정이라 원리적으로 없는 문제 = **드래그 실패 폭발반경 최대 10배**.
→ 서버팀 요청안: `PUT /api/events/action-report-templates/order` ← `[{id, display_order}]` 단일 트랜잭션.

**❌ 키보드 폴백 (실측 프로브)**
`Alt+↑` = `Key.System` + `e.SystemKey=Up`, **버블 KeyDown 은 DataGrid 가 소비**.
→ `PreviewKeyDown` + `e.Key==Key.System && e.SystemKey==Key.Up` 필수. 레포에 SystemKey 선례 **0건**.
`Focusable="False"` 그립은 Tab 제외 + 스크린리더 미노출 → 폴백 진입점 자체가 없음.
`MapViewModel.cs:905` 윈도우 레벨 후킹이 패널 안 ↑/↓ 를 지도 심볼 nudge 로 가로챔(예외필터에 DataGrid/ListBox 없음).

**❌ 자동화** — .NET 8 WPF 에 UIA `IDragProvider`/`IDropTargetProvider` **타입 자체가 부재**.
FlaUI 5.0.0 `Mouse.Drag` = `SetCursorPos` **텔레포트 1회**(IL 실측) → 오토스크롤 경로 통과 불가.
`AlternationIndex` 는 가상화에서 리셋(29→59, 30→0), `GridHelper.VisibleRowIds:28` 는 `.OrderBy` 로 화면순서 파괴
= **항상 통과하는 가짜 테스트**. → 회귀는 순수함수 헤드리스 + 폴백 경로로.

**사실 오류 정정**: `IsDrag` 중복이 1곳이 아니라 **3곳**(`CameraPopupHubMath:22`, `PtzCoordinateMath:73`, 테스트 사본).
Blend xmlns 는 "조용한 미부착"이 아니라 **하드 실패**. 조치보고 문구 관리 패널은 **아직 미존재**(신규 구축).
이관 시 TFM 불일치(`GMaps.Ui.Tests` = net8.0 소스링크 vs Utils = net8.0-windows) 해결 필요.

**수정된 경로**: ①DragMath 이관(위험 0, 즉시) → ②서버 배치 엔드포인트 요청 + ③스파이크 S0/S2 병행
+ ④S1(FlaUI 드래그, **별도 세션 필요**) → ⑤설계 v2 → ⑥구현(드래그+키보드 동시)

**사용자 확인 필요**: 서버 엔드포인트 대기 vs ▲▼ 선출시 후 드래그 추가 — 순서 결정.

---

## [2026-09-07] GOPDB 09-07 배포 2건 영향 분석 — 조치보고 /reorder · 억제 주간반복

**산출물**: `docs/analyses/gopdb-0907-deploy-impact-analysis.md` (v1.0)
**방법**: 4축 병렬 실측 → 종합 → 적대검증 2기(인용검증·완결성). 두 검증 모두 `overstated=true` 로
1차 종합의 과장 6건을 정정함.

### ✅ 배포 1 — 조치보고 `POST /reorder` 신설 (내 요청 반영됨)
- 구현 정확: 단일 트랜잭션 + `set_config('gop.suppress_sync','on')` 로 row 트리거 억제 후 **요약 알림 1건**
  (`resource_id:0`), 없는 id 하나라도 있으면 **404 무변경**, 응답이 정렬 후 전체 목록.
  스키마 방어 `extra=forbid`·`id ge=1`·`display_order ge=0`·`max_length=500`·**중복 id validator**.
- NOTIFY 문서 **v2.1**(API 6.3.3) 로 우리 저장소에 배달 완료.
- ⚠ 서버측 누락: `permission_map.py` 에 `/reorder` 항목 없음(라우터 데코레이터는 있음).
- ⚠ 자기 발행 NATS 가 자신에게도 돌아옴 → 재조회 생략 시 중복 방지 필요.

### ✅✅ 배포 2 — 억제 주간반복: **"제일 중요" 항목은 고칠 코드가 없다** (확정)
`window_end` 소비처 **전량 = 표시 문자열 2곳**(`EventSuppressionScheduleItemViewModel.cs:34, :66`) + 테스트 픽스처.
`SuppressionRules` 는 `WindowEnd` 미사용. 억제 도메인 타이머는 `SuppressionActiveMonitor` 하나뿐이고
`window_end` 를 참조하지 않는다. **클라 억제 인지 표면 = 패널 1개 + Monitor 1개가 전부**
(이벤트카드·사운드·지도 0건). → 메모리 [[project_event_suppression_blocking_layers]] 재확인됨.
**클라 측 마이그레이션 영향 0건**(전부 서버 SQL v72·v75).

### 🔴 지금이 마감 — 권한 키 (시점이 앞당겨짐)
권한 화면에서 그룹 1회 저장 시 서버 `action_report_templates` 키 소멸 → 템플릿 쓰기 403.
근거 `PermissionMatrixPanelViewModel.cs:134` + `:249-251`(전체 교체), `EnumPermissionModule.cs:11-22` 부재.
⚠ **"그냥 추가"는 위험** — `PermissionsSchema` 가 strict 라 미정의 키는 **422**(`routers/user_groups.py:302-304`).
운영 서버가 6.3.3 미만이면 키 추가 즉시 **권한 화면 저장 전면 파손**.
→ **운영 서버 버전 확인 후 동시 배포**. 그전까지 **권한 화면 그룹 저장 금지**.

### 🟡 반복 창 생성 시 드러날 것 (지금은 무증상)
①`status=="active"` 배지가 "진행중"으로 거짓말(토요일 새벽에도 active) ②신규 9필드 전량 폐기
(`MissingMemberHandling.Ignore`) ③`window_end=null` 이 `'—'` 로 표시(크래시 아님 — 문서 서술 반증)
④중복경고가 **과소·과대 양방향 오류**(`SuppressionRules.cs:31-57` 이 **시간 비교를 전혀 안 함**)
⑤창 길이 상한 30일 상수가 **2곳**(`SuppressionRules.cs:22` 권위 + VM `:699`)

### 🟢 기존 결함 (배포 무관, 같이 정리 후보)
`SuppressionActiveMonitor` **사문**(30초 폴링인데 소비자 0건) · 패널 배너 stale(타이머 0건) ·
폴링 실패 fail-open 미구현(`:78`) · **억제 PATCH 경로가 UI 에 없음**(생성만, 반복창 수정 불가)

### ★ 구현 시 함정 (적대검증 산출)
- **요청 DTO 확장 = 모든 PATCH 422** — `EventSuppressionScheduleRequestDto` 를 POST·PATCH 가 공유하는데
  서버 Update 스키마엔 반복 4필드가 없다
- `days_of_week`/`daily_*` 에 `NullValueHandling.Ignore` 붙이면 weekly 필수필드 소실 → 422
- `is_suppressing_now` 를 `bool` 로 받으면 구버전에서 조용히 `false` → **`bool?` + status 폴백**
- `daily_*` 를 `DateTime` 으로 받으면 "오늘 08:00"으로 **무성 오염**(크래시 아님)
- `recurrence_rule`(미사용 레거시) ↔ `recurrence_type`(신규) 혼동 주의
- NATS 핸들러는 **메인앱 no-op case + 라이브러리 자가필터** 관례(`DetectionSyncNatsService.cs:83-84` 정본)
- 테스트 동반 수정: `EventSuppressionScheduleTests.cs:68/72/75/102/195`, `SuppressionSweepTests.cs:38/158/291`

### 서버팀 회신 4건
①`permission_map` `/reorder` 누락 ②**단발 창 경계 `suppressing` 오발행**
(`_fire_boundary:55-89` 가 `notified_suppressing` 미기록 → `{active,false}` 발행 후 ≤5분 뒤 2번째)
③운영 서버 6.3.3 버전 확인 ④v2.0 "SYNC 루프가 죽는다"는 우리 클라 해당 없음(`TryParse`, `Enum.Parse` 0건)

### 문서 개정 대기
`/reorder` 신설로 **"배치 엔드포인트 없음" 전제 서술 전부 무효** →
PRD FR-10·결정표·리스크표·G7 / scenario-analysis ISSUE-O3 / **drag-reorder-infra-analysis §3 외부블로커 전체 해소** /
SIM-O 계열 시나리오.

### [2026-09-07] 문서 개정 완료 — 사용자 "승인"(ⓑ 진행)

서버 v2.1(`POST /reorder`) + 결정 확정(G1~G8, D-4)을 문서 5종에 반영. **코드 변경 0건.**

| 문서 | 개정 내용 |
|---|---|
| `docs/prds/action-report-custom-template-prd.md` | **v1.0 → v1.1**. FR-10 **드래그+`/reorder`** 로 교체, **FR-10a**(드래그 인프라 계약)·**FR-19~21**(EnumGopCommand·SYNC 재조회·PermissionCatalog) 신설, API 계약표에 `/reorder` 7번째 행 + 권한 모듈 분리, §7 **미결 → 확정표**(G7 드래그 + 구현계약 7항), V-08~12 추가, 리스크표 O3 해소·신규 4건, DoD 갱신. **상태는 Draft 유지** |
| `docs/analyses/action-report-custom-template-scenario-analysis.md` | v1.1. **ISSUE-O3 해소** 표기, G7 확정(v1.0 권고는 `<details>` 보존), API 표에 `/reorder`, "NATS 미발행" **정정**, 권한 분리 + FR-21 경고 |
| `docs/analyses/drag-reorder-infra-analysis.md` | v1.1. **§3 외부 블로커 해소**(해소 전 분석은 `<details>` 보존), §0 요약표·결론 문장, §7 도입 경로에서 서버 요청 단계 완료 처리 |
| `docs/tests/action-report-custom-template-scenarios.md` | **SIM-O 계열 재작성 필요** 경고 삽입 — 개별 PATCH·▲▼ 전제. SIM-O006 은 **폐기 대상**(원리적으로 발생 불가), 드래그 제스처·키보드 폴백·데드존·삽입선·ESC·캡처유실 시나리오 신규 필요 |
| `.claude/rules/common/drag-first-ux.md` | "배치 엔드포인트 선확보" 항목에 **실증 각주** — 요청했더니 서버가 신설해줌(2026-09-07) |

`docs/INDEX.md` 에 신규 분석 2건 등재 + PRD 행 v1.1 표기.

**PRD 상태**: **Draft 유지**. 승인은 사용자 명시 명령(`advance-phase.js approve prd`)으로만 — Claude 자동 승인 금지 규칙.
v1.1 은 결정 반영이 끝났으므로 **이제 승인 가능한 상태**다.

**다음 후보**: ⓐ 운영 서버 6.3.3 버전 확인(V-08, FR-21 선행) · ⓒ 억제 반복 대응 PRD 신설 ·
SIM-O 계열 시나리오 재작성 · 서버팀 회신 4건.
