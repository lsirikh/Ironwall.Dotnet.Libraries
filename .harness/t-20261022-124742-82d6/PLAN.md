# PLAN — 운영 이벤트에 "어느 부품이 어떻게 바뀌었나" 표시

## 목표 (사용자 원문 2026-10-22)
"우리 운영이벤트에서 예를들어서 통문이 열림이 감지 되면 통문 열리고 어떤 부품이나 항목이 동작한지도 같이 표시할 수 있는건가? (이벤트 카드에?)" · "통문 접점 감지, 액추에이터 열림 뭐 이렇게"

1. 운영 이벤트 카드 · 상세 · 목록 행 · 보고서(조치보고 창) 글자에 부품 전이를 쉬운 말로.
2. 같은 장비 짧은 시간(30초) 안의 부품 전이를 카드에 흐름으로(기존 카드 규칙을 해치지 않는 범위).
3. 부품 key · 유형은 로컬 시험 서버(127.0.0.1)로 실측. 원격 운영 서버에는 쓰지 않는다.
4. 시험: detail 조합별 글자 단정 · 카드/상세/목록에 실제로 보이는지 · 고치기 전 실패 → 고친 뒤 통과.

## 진행
- [x] 코드 위치 조사 — 글자 규칙 `Events.Ui/Helpers/OperationEventText.cs`, 변환 `Helpers/DtoToModelHelper.ToOperationEventModel`(콘솔 목록 · 상세 · NATS 카드 · SYNC 갱신이 모두 이 한 곳), 카드 `ViewModels/Events/OperationEventCardViewModel.cs` + `Views/Events/OperationEventCardView.xaml`, 카드 패널 `ViewModels/Panels/EventCardListPanelViewModel.HandleAsync(OperationEventCardMessage)`, 부품 이름 정본 `Ironwall.Dotnet.Monitoring.Models/Components/ComponentDisplay.cs`, 장비 부품 선언 `IBaseDeviceModel.Axes.HardwareSpec.Components`(key · type · label).
- [x] 실측(아래 「발견」).
- [x] 구현 — 부품 전이 글자 `OperationEventText.ComponentChange` + `OperationComponentChange` 레코드, `DetailText(detail, components, catalog)`, 모델 `OperationEventModel.ComponentChange`, 카드 `ComponentText · ComponentLineText · ComponentToolTip · FlowText`, 카드 XAML 부품 한 줄, 흐름 `Helpers/OperationComponentFlow.cs`, 패널에서 흐름 대입.
- [x] 시험 작성 · 통과 · 고치기 전 실패 증명.
- [x] CHANGELOG 항목.
- [x] 임시 사본 정리.

## 결정 (자세히는 DECISIONS.md)
1. 이름 · 상태 말은 기존 정본 `ComponentDisplay`(등록 이름 → 서버 카탈로그 한글 → 내장 한글 → key, RUNNING = "구동 중") — 목표 예시의 「문 접점(감지)」 · 「액추에이터」 · 「동작 중」 대신. 실측 카탈로그 라벨이 "도어 센서" · "문 구동부" 이고, 지도 · 장비 콘솔과 같은 이름이어야 사용자가 맞춰 본다. 사용자가 부품을 「통문 접점」 으로 등록하면 그 이름이 먼저 나온다.
2. 흐름은 카드를 합치지 않고 나중 카드의 부품 줄을 흐름으로 바꿔 보인다(카드 높이에 한 줄만 여유). 제 전이 전체는 말풍선.
3. 기존 시험 `EventConsoleFourKindsTests.should_spell_out_known_operation_detail_keys_for_people` 의 InlineData 2줄(`component` 있는 detail) 기대값만 새 규칙으로("door 닫힘 → 열림" · "actuator 열림 · 장비 점검 중").

## 발견 (실측 · 근거)
- 로컬 시험 서버 HTTPS 127.0.0.1:8000 `/api/devices/spec` 은 401(인증 필요). admin 로그인은 단일 세션 evict 로 사용 중인 앱 세션을 끊을 수 있어(메모리 e2e_injector 규칙) 하지 않았다. 대신 같은 서버의 DB(`pids-api-postgres` 컨테이너)를 **읽기 전용**으로 조회했다 — 쓰기 · 계정 생성 없음.
  - `component_type_catalog`: DOOR_SENSOR "도어 센서" states [OPEN, CLOSED] applies_to [enclosure, gate] · DOOR_ACTUATOR "문 구동부" states [OPEN, CLOSED, RUNNING] · component_state OPEN "열림" · CLOSED "닫힘" · RUNNING "동작 중".
  - `operation_events.detail` 실제 행: `{"state":"OPEN","previous":null,"component":"door","device_status":"ACTIVATED"}`(ENCLOSURE_DOOR_OPEN) · `{"state":"CLOSED","previous":"OPEN","component":"actuator",...}`(GATE_CLOSED). component 값 분포: door 2 · actuator 2 · 없음 19(환경 경보).
- 서버 원본 `C:/workspace_python/api-test-server/app/services/operation_event_service.py:401~455` `record_component_transition`: 새 상태가 OPEN/CLOSED 일 때만 이벤트(RUNNING 으로 바뀔 때는 이벤트 없음 — 그래서 RUNNING 은 `previous` 로만 온다), 문 위치 판정은 유형(DOOR_SENSOR · DOOR_ACTUATOR)만, key 는 자유(관례 door · actuator). v8.0.8 부터 어느 장비에나 달 수 있음(함체 밖 장비는 ENCLOSURE_DOOR_* 사유).
- 카드는 이전 요청("운영이벤트에 작은 글씨는 빼라 … 개발자들이 볼거잖아", OPC-016)으로 상세 원문을 싣지 않는다 — 그 규칙은 유지하고(`DetailText` 바인딩 없음) 쉬운 말 부품 줄만 더했다.
- 카드 높이 200 고정: 머리 35 + 바닥(단추 32 + 여백 10) 42 + 구분선 → 본문 약 122px. 구역 · 장비(19pt) + 사유(16pt) + 경고 줄에 부품 줄(14pt) 하나는 들어가지만 두 줄(부품 + 흐름)은 약 129px 로 넘쳐 경고 줄이 잘린다 → 흐름이 있으면 부품 줄을 흐름으로 바꾼다.
- 시나리오 시험 VM 은 생성자 없이 만들 수 있어(메모리 uninit_vm_field_initializer_trap) 패널의 흐름 기억은 `LazyInitializer` 로 지연 초기화했다.

## 바뀐 파일
- `Ironwall.Dotnet.Libraries.Events.Ui/Helpers/OperationEventText.cs` — `ComponentChange` · `DetailText` 오버로드 인자(components · catalog) · `DoorStateText` 를 공용 사전과 맞춤(RUNNING "움직이는 중" → "구동 중") · `OperationComponentChange` 레코드.
- `Ironwall.Dotnet.Libraries.Events.Ui/Helpers/OperationComponentFlow.cs`(새) — 장비별 30초 · 최근 3칸 흐름.
- `Ironwall.Dotnet.Libraries.Events.Ui/Helpers/DtoToModelHelper.cs` — 캐시 장비 부품 선언 · IoC 카탈로그(`IComponentTypeLabels`, 없으면 null)로 DetailText · ComponentChange.
- `Ironwall.Dotnet.Libraries.Events.Ui/Models/OperationEventModel.cs` — `ComponentChange`.
- `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Events/OperationEventCardViewModel.cs` — 부품 줄 속성 · SYNC 갱신 반영.
- `Ironwall.Dotnet.Libraries.Events.Ui/ViewModels/Panels/EventCardListPanelViewModel.cs` — 운영 카드에 흐름 대입.
- `Ironwall.Dotnet.Libraries.Events.Ui/Views/Events/OperationEventCardView.xaml` — 사유 아래 부품 한 줄(흐름이면 History 아이콘).
- 시험: `Tests/OperationComponentTextTests.cs`(새, 4클래스 31건) · `Tests/EventConsoleFourKindsTests.cs`(목록 · 상세 1건 추가, 기대값 2줄 수정).
- `CHANGELOG.md`.

## 검증 (이 세션에서 돌린 명령과 결과)
- `dotnet build Ironwall.Dotnet.Libraries.Events.Ui/...csproj` → 오류 0.
- `dotnet test Ironwall.Dotnet.Libraries.Events.Ui/...csproj --filter "FullyQualifiedName~OperationComponent|...EventConsoleFourKindsTests|...OperationEventCardScenarioTests|...OperationReasonCategoryWiringTests|...OperationEventActionReport"` → 통과 112 / 실패 0.
- **고치기 전 실패 증명**: `git archive HEAD` 로 고치기 전 원본을 $TEMP 에 꺼내(서브모듈 OnvifSolution 은 작업 트리에서 복사) 옛 API 만 쓰는 증명 시험 `ProofBeforeFixTests` 3건을 넣고 `dotnet test ... --filter ProofBeforeFixTests` → **실패 3**: P1 Expected "door 닫힘 → 열림" Actual "닫힘 → 열림" · P2 Expected "문 구동부 구동 중 → 열림" Actual "움직이는 중 → 열림" · P3 카드 XAML 에 "ComponentLineText" 없음. 같은 파일을 고친 코드에 넣어 돌리면 → **통과 3**(돌린 뒤 지움).
- `dotnet test Ironwall.Dotnet.Libraries.Events.Ui/...csproj`(전체) → 통과 2257 / 실패 0(증명 3건 포함 — 증명 파일 삭제 뒤 2254).
- `dotnet build Ironwall.Dotnet.Libraries.sln` → 오류 0.
- `dotnet test Ironwall.Dotnet.Libraries.GMaps.Ui/...csproj --no-build`(Events.Ui 참조) → 통과 736 / 실패 0.
- 솔루션 밖 도구 빌드: `tools/events-console-preview` · `tools/live-api-roundtrip` 오류 0. `tools/live-nats-pipeline` 은 메인 솔루션 상대 경로(`..\..\..\Dotnet.Monitoring.Solution`)를 worktree 깊이에서 못 찾는 **기존 환경 문제**(이번 변경과 무관).
- 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` 은 사용자 작업 방식(Sounds 시험은 실제 소리 · Libraries.Db 는 다른 PC DB 에 씀)대로 돌리지 않았다 — 바뀐 코드가 닿는 Events.Ui · GMaps.Ui 만 돌렸다.

## 남은 일 · 확인 안 한 것
- 실제 앱 화면(헤디드)으로 카드 줄 높이 · 말줄임을 보지 않았다 — 헤드리스로 VM · XAML 바인딩까지만 확인(사용자 규칙: 헤드리스 우선, 헤디드는 요청 시). 위 높이 계산이 근거.
- 로컬 서버 HTTP API 로 spec · 운영 이벤트를 읽지 않고 DB 읽기로 대신했다(인증 · 세션 evict 회피). 전용 시험 계정이 생기면 `/api/devices/spec` 과 `/api/events/operations` 응답으로 다시 확인할 수 있다.
- 흐름의 시간 기준은 서버 created_at. 서버가 created_at 을 안 실으면 수신 시각(DateTime.Now)이다.
