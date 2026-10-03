# PLAN — 카메라 프리셋 관리 화면

## 목표
직전 작업(t-20261003-232523-cc9b)에서 맵핑 워크벤치 카메라 연동 설정에 타깃 · 홈 프리셋 콤보를 넣었다.
사용자 답: "딜레이타임은 PTZ 이동하는 데 걸리는 시간으로 그냥 정한 것, 프리셋 관리 화면이 필요하다."
→ ① 카메라 프리셋(서버 `/devices/cameras/{id}/presets`)을 만들고 · 고치고 · 지우는 관리 화면, ② 지연(초)의 뜻을 "PTZ 이동 대기"로 바로잡는다.

## 발견
- 서버 프리셋 CRUD 는 `IDeviceApiService`(Get/Create/Patch/Delete PresetAsync)에 있지만 **화면에서 쓰는 곳 0**.
- 워크벤치는 `IDeviceApiService` 가 아니라 자체 게이트웨이(`MappingWorkbenchGateway`, IApiService 직접)로 왕복한다 — 같은 길로 간다.
- 프리셋 DTO 키: `preset_index`, `preset_name`, `touring_time`(기본 10), `is_restricted_zone`.
- Devices.Ui 의 "프리셋 관리"(PresetManagerView)는 **장비 조립 프리셋**(로컬 파일)이라 다른 개념 — 모양(T4 · L 720 · 목록+버튼)만 따른다.

## 결정
- 입구: 워크벤치 카메라 연동 설정 칸에 **[프리셋 관리…]** 버튼(그 카메라 것). 창은 런처가 `IWindowManager` 로 띄운다 —
  뷰모델은 `Func<MappingPresetManagerViewModel, Task>? ShowPresetManager` 만 안다(헤드리스 시험 가능).
- 새 인터페이스 `IMappingPresetEditor`(목록 상세 · 생성 · 수정 · 삭제). `IMappingWorkbenchGateway` · `IMappingPresetSource` 는 넓히지 않는다(목·페이크 보호). 게이트웨이가 함께 구현.
- 관리 창 = `MappingPresetManagerViewModel`(Screen) + `MappingPresetManagerView`(ConsoleDialogFrame T4 · L). 목록(번호 · 이름 · 투어링 · 🔒) + 편집 폼(번호 · 이름 · 투어링(초) · 감시금지구역) + [새 프리셋] [저장] [삭제].
  저장 · 삭제는 바로 서버로(Draft 아님 — 맵핑 속성 폼과 같은 규약). 삭제는 같은 창 안 확인 줄([삭제 확인] / [취소]) — raw MessageBox 금지.
- 검증(화면): 이름 필수, 번호 1 이상 · 같은 카메라 안 중복 금지, 투어링 0 이상.
- 창을 닫으면 그 카메라 프리셋 캐시를 버리고 다시 받아 콤보를 새로 세운다. 지금 행이 가리키던 프리셋이 지워졌으면 상태 줄에 알린다.
- PTZ 위치를 카메라에 저장(ONVIF SetPreset)하는 것은 범위 밖 — 서버 프리셋 레코드 관리만.
- 지연(초): 문서 주석 "타깃에 머무는 시간" → "PTZ 가 타깃 프리셋까지 이동하는 데 기다리는 시간". 설명은 화면 글이 아니라 섹션 "?"(HelpKey `Events.Mapping.Camera`).

- 권한: 프리셋 쓰기도 워크벤치 권한(`integrations` CanEdit)으로 막는다 — 서버가 최종 판정.
- 이름이 바뀐 프리셋은 보드 칩 이름만 고친다(이름은 Draft 판정에 안 든다 → 행이 '수정'으로 바뀌지 않는다).

## 진행
- [x] 계약 · 게이트웨이(`IMappingPresetEditor`, `MappingPresetDetail` · `MappingPresetDraft`, `MappingWorkbenchGateway.PresetBody`)
- [x] 관리 뷰모델 · 뷰(`MappingPresetManagerViewModel` · `MappingPresetManagerView.xaml`)
- [x] 워크벤치 입구([프리셋 관리…] `Integrations.Board.ManagePresets`) · `ShowPresetManager` · 런처 배선(IWindowManager, L 폭 × 560) · 닫은 뒤 콤보 재조회 · 지워진 프리셋 경고
- [x] 도움말 `Events.Mapping.Camera`(지연 = PTZ 이동 대기) · `Events.Mapping.Presets`
- [x] 헤드리스 시험 `MappingPresetManagerTests` 15건 — Events.Ui Mapping/Wording 필터 231/231 통과, Utils HelpContract 7/7 통과
- [x] 새 파일 UTF-8 BOM
- [x] 솔루션 전체 `dotnet test` — 종료 코드 0(보인 마지막 10개 프로젝트 모두 실패 0)

## 남은 일 / 메모
- 실창(WPF) 확인 안 함: 워크벤치 → 카메라 탭 → 행 하나 → [프리셋 관리…] → 만들기/저장/삭제 → 닫기 → 콤보에 반영.
- 실서버 왕복 미확인: 프리셋 POST/PATCH 본문 키(`preset_index` · `preset_name` · `touring_time` · `is_restricted_zone`)는 `CameraPresetDto` · 시더(tools/dummy-cameras)와 같다.
