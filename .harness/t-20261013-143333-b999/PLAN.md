# PLAN — 센서가 공중에 뜨지 않게 + 펜스 보기 제어기

## 목표
- 불변식: 센서 최종 자리(프리셋 ⊕ 따로 고친 값 ⊕ 이관 ⊕ 끌기/키/수치)는 늘 실제 물체 하나(기둥 · 상단 레일 · 메쉬 · 윤형 코일 · 담 위/앞면 · 바닥)에 붙는다.
- 결선 펜스 보기(입체/평면)에 제어기를 [케이블 보기] 와 상관없이 늘 그린다(왼쪽/오른쪽 끝 · 끌어서 반대쪽).

## 원인 전수 조사(코드 근거)
| # | 경로 | 원인 |
|---|---|---|
| 1 | `SensorMountResolver.Clamp` | 높이를 `0.05 ~ TopLimitM` 로만 자름 — 갈래(메쉬/레일/담)별 물체 범위를 안 봄. 메쉬 2.5 m(망 2.4) · 레일 1.0 m · 담 위 +0.3 · 철조망 칸 코일 아래 빈 곳 = 허공 |
| 2 | 기둥 갈래 + 가로 몫 0.3(따로 고친 값 · Alt) | 기둥이 없는 칸 가운데 허공 |
| 3 | 펜스와 거리(depth) > 브래킷 | 비지중인데 ±5 m 까지 허용 = 허공 |
| 4 | `MoveFree`(Alt · 화살표 · 수치) | 가장 가까운 "점" 갈래를 따라감 — 점 사이 허공 자리도 그대로 |
| 5 | `FenceWorld.FromLayout` 프리셋 칸 | 칸을 `BayIndexOf(mount, n, pose.AlongRatio)` 로 다시 셈 — 뷰모델은 프리셋 몫으로 셈 → 기둥 자리 + 몫 < 0.5 면 옆 칸에 그림 |
| 6 | `FenceWorld.Separate` · `FenceCanvas` 화면 벌리기 | 프리셋 센서도 옆으로 밀어 기둥/물체에서 떨어뜨림(83169 칸 사이 허공) |
| 7 | 칩 올리기(`LiftFor(.., PanelCenter, h)`) | 몸 가운데를 붙는 높이에 — 레일 점이면 몸 절반이 레일 위 허공, 꼭대기 점도 몸 절반이 위 · 아래 점이면 땅 뚫음 |
| 8 | 복합센서 칩 | 자기 기둥(0~H+16)을 같이 그리는데 올리면 기둥 밑이 허공 |
| 9 | 기둥 솎기 | 프리셋 기둥 센서의 기둥이 솎여 없어질 수 있음 |
| 10 | 저장 | 옛/깨진 따로 고친 값을 그대로 다시 씀 |
| C | 제어기 | `FenceCanvas.SyncChips` 가 링 + 펜스 구성에서 [케이블 보기] 일 때만 제어기 칩을 세움 → 기본 화면에 제어기 없음 |

## 결정
- D1 순수 함수 하나: `SensorMountResolver.Attach(pose, bay) → SensorMountAttachment(Pose, Target, Clamped, Reason)`. `Clamp` 는 이것을 부른다(편집기 · 결선맵 · 이관 · 그림 · 저장이 모두 같은 계산).
  - 갈래 유지 우선: 붙은 갈래 물체면 위로 투영. 그 물체 밖인데 다른 (지중 아닌) 물체가 그 자리를 품으면 그 물체로 갈래를 바꿈(예: 기둥 프리셋을 칸 가운데로 → 메쉬). 지중 ↔ 비지중은 바꾸지 않는다(v1.1 결정 유지).
  - 물체 범위: 기둥 = 몫 0/1 · 0.05~TopLimit(철조망 칸이면 Y 받침 · 코일까지) / 레일 = 높이 H / 메쉬 = 0.05~H / 코일 = 코일 아래~꼭대기(철조망 칸만) / 담 위 = H / 담 앞면 = 0.05~H / 바닥 = −2~0.
  - 비지중의 펜스와 거리는 ±브래킷 안(브래킷이 물체까지 닿게). 브래킷을 늘리면 더 멀리.
  - 기존 시험 MNT-011(기둥 + 철조망 칸 높이 9 → 코일 꼭대기)을 지키려 기둥 꼭대기 한도는 `TopLimitM`(철조망 칸 = 코일 꼭대기).
- D2 `MoveFree` 는 면 투영(`NearestSurface`) — 모델 허용 갈래가 아니라 물리 물체 기준(Alt 자유는 물체 면 위에서만).
- D3 저장: 편집기는 열 때 보정(+ "바뀜" · 상태 줄 사유), 저장 값 = 보정 값. 결선 저장은 따로 고친 값을 `Attach` 결과로 다시 씀(`WiringPresetPlan.Normalize`). 서버 · DB 에 몰래 쓰지 않음(저장할 때만).
- D4 펜스 보기: 프리셋 센서는 x 를 옮기지 않음(Separate · 화면 벌리기 제외) · 칸은 뷰모델과 같은 규칙(새 FromLayout 겹침) · 칩 올리기는 갈래별(꼭대기/담 위/코일 위 = 몸 밑이 물체 위, 레일 = 몸 위가 레일, 그 밖 = 몸이 물체 범위 안) · 복합센서는 자기 기둥 대신 짧은 받침 · 브래킷 선.
- D5 제어기: 기존 시험(`should_keep_cables_and_the_enclosure_off_the_fence_when_the_cable_toggle_is_off` — 케이블 끔이면 `ControllerChip` null)을 지키려 케이블을 끈 펜스 구성 화면은 **정적 층에 제어기 그림**(펜스 끝 땅 위 기둥 + 상자 "제어기")을 그리고, 그 그림을 눌러 끌면 기존 함체 끌기(놓으면 반대쪽 끝)와 같은 길로 옮긴다. 케이블 보기에서는 기존 칩 그대로.
- 시험 이름 `MNT-4nn`.

## 진행
- [x] 원인 조사
- [x] Models: `SensorMountAttachment.cs`(Attachment · Surface · Surfaces) · `SensorMountResolver.Attach/ResolveAttached/MoveFreeAttached/Normalize` · Clamp/MoveFree 교체 · NormalizeYaw 멱등
- [x] `WiringPresetPlan.MoveAttachment/OverrideAttachment/CorrectionOf/NormalizeOverride` + 저장 경로(`MigrateMountForSave`) + 상태 줄 까닭 + "보정: …(저장하면 반영)"
- [x] 편집기 열 때 보정 · "바뀜" · 저장 값 보정 · 끌기/화살표/수치 칸 까닭 · 브래킷 선 · 철조망 칸 Y 받침
- [x] FenceWorld: `FromLayout(…, bayOf)` · `FenceMountMark` · `SeatOf` · `MountLift` · Separate 제외 · `PostMountXs` · 복합 자기 기둥 대신 브래킷(`FenceInk.MountBracket`)
- [x] FenceCanvas: 화면 벌리기 제외 · bayOf 전달 · 제어기 정적 그림(`FenceScene.EndController`, `FenceInk.EndController`) + 누르면 함체 끌기
- [x] 시험: Models `SensorMountAttachTests`(MNT-400~422) · Devices.Ui `SensorMountAttachViewTests`(MNT-430~455, 픽셀 + 대조군) — 모두 통과
- [x] 도움말 "?" 설치 위치 항목에 붙임 규칙 두 줄
- [x] PRD: 첨부 PRD 는 저장소 밖(복사 권한 거부) → `docs/prds/sensor-mount-preset-prd-v1.2-fr22.md` 에 §6 FR-22/23 · §10 행 · 결정 · "뜰 수 있던 경로" 표 · 헤디드 목록
- [x] 검사: `dotnet test Ironwall.Dotnet.Libraries.sln` = 실패 0 · 통과 2729(작업 전 실패 2 · 통과 2712) · Devices.Ui 헤드리스(창 시험 3클래스 제외) 1229 통과

## 남은 일 / 메모
- 창 띄우는 시험(WiringFenceViewTests · WiringFenceHeightViewTests · WiringFenceEditorViewTests)은 규칙상 돌리지 않았다. `should_keep_cables_and_the_enclosure_off_the_fence_when_the_cable_toggle_is_off` 는 `ControllerChip` null · 리턴선 0 을 그대로 지키도록 설계(제어기는 정적 그림 · 다른 잉크).
- 헤디드 확인 목록은 PRD 덧붙임 문서 끝 절.
- 정본 PRD(`sensor-mount-preset-prd.md`, 호스트 쪽)에 덧붙임 문서의 행을 옮겨 적어야 한다.
