# 결정 기록 — 운영 이벤트에 "어느 부품이 어떻게" 표시

## 1. 부품 유형 이름 · 상태 말을 목표 예시(「문 접점(감지)」 · 「액추에이터」 · 「동작 중」)로 새로 둘까, 기존 정본 `ComponentDisplay` 를 쓸까
- options: (a) 운영 이벤트 전용 사전을 새로 두어 DOOR_SENSOR → "문 접점(감지)" · DOOR_ACTUATOR → "액추에이터" · RUNNING → "동작 중" / (b) 지도 · 장비 콘솔이 이미 쓰는 단일 정본 `ComponentDisplay.ComponentName` · `StateName`(서버 카탈로그 라벨 → 내장 사전) — DOOR_SENSOR "도어 센서" · DOOR_ACTUATOR "문 구동부" · RUNNING "구동 중"
- chosen: (b)
- why: pattern: reuse · follow-recommended. 목표 3번이 "하드코딩은 실측으로 확인"을 요구했고, 실측한 서버 카탈로그 라벨이 "도어 센서" · "문 구동부" 다(예시와 다르다). 같은 부품을 장비 콘솔 부품 목록 · 지도는 "문 구동부" 라 부르는데 이벤트 카드만 "액추에이터" 라 하면 사용자가 둘을 맞춰 볼 수 없다 — component-display-unify 가 없앤 "두 화면이 다르게 말하던" 문제를 다시 만든다. 장비 등록 때 붙인 이름(label)이 가장 먼저라 사용자가 부품을 「통문 접점」 으로 등록하면 그 이름이 그대로 보인다. RUNNING 은 서버 라벨이 "동작 중" 이지만 정본은 일부러 "구동 중"(스토리보드 §A)이라 지도 통문 상태와 같은 말을 쓴다.
- effect: 카드 예: 「도어 센서 닫힘 → 열림」 · 「문 구동부 구동 중 → 열림」. 바꾸려면 `Ironwall.Dotnet.Monitoring.Models/Components/ComponentDisplay.cs` 의 TypeNames · StateNames(전 화면 함께 바뀜) 또는 서버 카탈로그 라벨, 이벤트만 다르게 하려면 `OperationEventText.ComponentChange` 에서 이름을 덮는다.

## 2. 같은 장비에서 짧은 시간 안에 이어진 부품 전이(목표 2번)를 어떻게 보일까
- options: (a) 하지 않음(각 카드에 부품만) / (b) 카드를 하나로 합침 / (c) 카드는 그대로 두고 나중 카드의 부품 줄을 30초 안 흐름(「문 구동부 열림 → 도어 센서 열림」, 최근 3칸)으로 바꿈
- chosen: (c)
- why: pattern: broad · follow-recommended. 합치면 종류 + 번호 중복 판정 · 조치보고(카드 하나 = 서버 이벤트 하나) · 500장 상한 · 원격 조치보고 닫기 규칙이 모두 흔들린다. (c) 는 보이기만 하므로 기존 규칙을 건드리지 않는다. 카드 높이(200)에 한 줄만 더 들어가(경고 줄까지 있으면 두 줄은 넘침 — 계산 근거는 PLAN.md) 흐름은 부품 줄을 대신하고, 제 전이 전체(이전 → 지금)는 말풍선에 둔다. 흐름은 카드 목록이 아니라 받은 전이를 장비별로 기억해 앞 카드를 닫아도 남는다. 기준 시각은 서버 created_at.
- effect: 나중 카드만 흐름 줄(시계 아이콘). 끄려면 `EventCardListPanelViewModel.HandleAsync(OperationEventCardMessage)` 의 `FlowText` 대입 한 줄을 지운다. 시간 · 칸 수는 `OperationComponentFlow.Window` · `MaxSteps`.

## 3. 기존 시험의 기대값(`component` 를 화면에 내지 않음)을 바꿀까
- options: (a) 옛 `DetailText(JObject)` 는 그대로 두고 새 오버로드만 부품을 보임 / (b) 한 함수로 바꾸고 기존 단정 2줄의 기대값을 새 규칙으로 고침
- chosen: (b)
- why: pattern: verify. 사용자가 이번에 뒤집은 규칙(부품을 보인다)을 옛 경로에 남기면 쓰지 않는 길만 시험하게 된다. `EventConsoleFourKindsTests` 의 InlineData 2줄(`component` 가 있는 detail)만 "door 닫힘 → 열림" · "actuator 열림 · 장비 점검 중" 으로 고쳤고 나머지 단정은 그대로다.
- effect: 장비 캐시 없이 부르면 key 가 앞에 붙는다. 되돌리려면 `OperationEventText.DetailText` 의 `ComponentChange(...)?.Text ??` 를 지운다.
