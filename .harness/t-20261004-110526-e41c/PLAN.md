# PLAN — t-20261004-110526-e41c

## Goal
ONVIF 검색 → 조회 → [추가] 로 연 '새 카메라 등록' 상세 칸의 필수 '종류 *'(type_axis → `TypeAxisCode`)가 비어 [등록]이 꺼져 있다.
조회로 PTZ 여부를 알았으면 종류를 채워 [추가] 직후 바로 [등록] 가능하게. 모르면(PTZ?) 비워 둔다.

## Progress
- [x] 원인: `OnvifCameraDraftFactory.Create` 가 `Category`(enum)만 채우고 `TypeAxisCode` 는 비워 둠(이전 작업 결정 D5).
- [x] 수정: `TypeAxisCode = (IsPtz ? PTZ : FIXED).ToString()` — 서버 type_camera 어휘(FIXED · PTZ)가 enum 이름과 같다.
- [x] 시험: OnvifDiscoveryTests(PTZ/FIXED 각각) · DeviceDashboardConsoleTests(상세 칸 draft.TypeAxisCode == "FIXED") 단언 추가.
- [ ] `dotnet test Ironwall.Dotnet.Libraries.sln`

## Decisions
- D1: 이전 D5("종류축 비움")를 뒤집는다. "모르면 비움" 조건은 이미 구조로 보장된다 — [추가] 는 조회 성공(LookedUp) 줄에만 열리고, 조회 성공이면 IsPtz 가 확정(false=고정)이다. 'PTZ?' 는 조회 전 힌트일 뿐 [추가] 불가.
