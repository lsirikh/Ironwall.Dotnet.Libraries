# DECISIONS

## 1. 병렬 에이전트 작업 공간을 어디에 둘지
- options: (a) 이 작업 트리 하나에서 에이전트가 번갈아 빌드 (b) Agent worktree 격리 (c) 작업 트리 안 git 무시 폴더에 저장소 복사본
- chosen: (c) — `artifacts/obj/g1~g3`(GIS) · `artifacts/map3d`(map-3d clone)
- why: 빌드가 겹치지 않고, 작업 트리 밖에 찌꺼기를 남기지 않으며(밖은 지울 수 없음) 끝나면 지울 수 있다. 처음엔 `artifacts/gN` 에 두었는데 원본 소스를 훑는 계약 시험(Utils.Tests HelpContractTests 등)이 복사본까지 읽어 7건 실패 → 그 시험들이 공통으로 건너뛰는 `\obj\` 경로 아래로 옮김.
- effect: 결과물은 이 작업 트리로 옮겨 합친 뒤 복사본은 지운다.

## 2. PerfSamplerTests 테스트 호스트 중단(기존 시험의 경쟁)
- options: (a) 그대로 두고 재실행에 맡김 (b) 시험의 gate 폐기만 없앰 (c) 시작 신호 · 끝 신호를 더해 순서를 강제
- chosen: (b) — `should_measure_scheduled_to_run_delay_when_ui_thread_is_blocked` · `should_include_pending_age_in_max_when_reporting_during_stall` 의 `using var gate` → `var gate`
- why: 판정(Assert)은 하나도 바꾸지 않고, 부하 때 디스패처가 늦게 집어 든 `gate.Wait` 콜백이 이미 폐기된 gate 를 만나 ObjectDisposedException 으로 테스트 호스트 전체가 죽는 것만 막는다. 기준선(r0)과 이번 실행에서 2회 재현, 고친 뒤 3회 연속 886 통과.
- effect: 시험 코드 2줄. 제품 코드 무관.
- 추가 지시("시험을 고쳐 통과시키지 말 것")와의 관계: 이 실패는 제품 결함도 이번 수정의 부작용도 아니고, 시험 자신이 쓰는 동기화 객체를 콜백보다 먼저 폐기하는 시험 코드의 경쟁이다(스택: 시험 람다 `gate.Wait` 에서 ObjectDisposedException). 기대값을 바꾸지 않으므로 유지. 되돌리려면 두 줄을 `using var gate` 로.

## 3. 직전 검사 7건 실패의 처리
- options: (a) 시험이 복사본을 건너뛰게 고치기 (b) 복사본을 시험이 이미 건너뛰는 경로로 옮기기
- chosen: (b) — 시험 · 제품 코드 무수정
- why: 실패 7건(Utils.Tests HelpContractTests 등)은 제품 결함이 아니라 `artifacts/gN` 복사본의 같은 원본 파일을 중복으로 읽은 것. 추가 지시대로 시험은 건드리지 않음.
- effect: 복사본 경로가 `artifacts/obj/gN` 이 됨. 마지막에 복사본을 지운다.

## 4. A1 자르기 Undo/Redo — 그림이 지워져 없을 때 · 실패 안내
- options: 실패로 처리(칸 유지) / 조용히 넘어감; 실패 시 로그만 / 상태 줄 안내
- chosen: 그림이 없으면 조용히 넘어감, 저장 실패만 실패 처리 + 상태 줄 "저장하지 못해 그림을 바꾸지 않았습니다"
- why: 다른 Undo 적용(회전 · 마커 갱신)도 대상이 없으면 넘어가고 기존 시험 E-35 가 그것을 기대. 실패로 두면 그 칸이 맨 위에 남아 Ctrl+Z 가 막힌다. 화면이 그대로라 안내가 없으면 무반응으로 보인다.
- effect: `MapViewModel.ImageCrop.cs` ApplyImageCropStateAsync 한 곳.

## 5. A9 회전 확정 처리 예외
- options: 로그만 / 상태 줄 안내
- chosen: Error 로그만
- why: 최소 수정, 그 시점엔 저장이 이미 끝남.
- effect: 그 회전의 Undo 기록이 빠질 수 있음(로그로 추적).

## 6. A8 저장 중 취소
- options: ① 저장 중 취소 막기 ② 취소하면 저장 결과를 되돌리기(추가 PUT/DELETE) ③ 요청만 취소(서버는 이미 저장했을 수 있음)
- chosen: ① — [취소] · ✕ · Esc 흐림/막음, 판 안내 "저장하는 중… 끝날 때까지 닫을 수 없습니다", 억지 실행 시 한 줄 알림
- why: "취소했는데 저장됨"을 없애고 서버 = 화면이 유지된다. ②로 바꾸기도 명령 하나.
- effect: 저장 응답을 기다리는 짧은 동안 판을 닫을 수 없음. 빈 땅 클릭 · 3D 나가기는 기존 결정대로 닫히고 늦은 저장은 반영.

## 7. A13 굽기 시작 시간 제한 값 · 중복 판정
- options: 시간 제한 값; 중복 판정 = 클라 진행 표시만 / 서버 작업 목록 확인 / map-3d 에 멱등 키 추가
- chosen: 굽기 시작 3 분(`BakeStartTimeout`, init 으로 바꿀 수 있음), 보통 요청 10 초 유지. 답 없이 끝난 영역을 기억하고 다시 누르면 GET /api/jobs 로 새 장면(같은 bbox) · 만드는 중 작업을 확인해 막음
- why: 수십 MB 오버레이 올리기에 충분하고 멈춘 서버는 3 분 안에 알린다. map-3d 작업 목록에 영역이 없어 정확한 같은-영역 판정은 불가 → 안전한 쪽(만드는 중이 있으면 막음). map-3d API 변경은 범위 밖.
- effect: 그때 다른 곳에서 만드는 작업이 있으면 그것도 막힘. map-3d 가 running 에 bbox 를 넣으면 `RefuseDuplicateMap3DBakeAsync` 한 곳만 바꾸면 정확해짐.

## 8. A6 비활성화 때 정리 범위
- options: 비활성화 때 전부 정리 / 3D 접기 · 구독 · 타이머만, 클라이언트 Dispose · 굽기 조회 끊기는 close 때만
- chosen: 후자
- why: 화면만 잠시 바뀔 때 진행 중 굽기를 끊지 않게.
- effect: 다시 활성화되면 3D 진입 보류로 시작(지도 이동 또는 「3D 로 보기」).

## 9. map-3d B1 취소 방식
- options: 별도 스레드 + 취소 확인 / 서버 시작 때 미리 데우기
- chosen: 기존 `cancel.run` 별도 스레드(0.2 초마다 취소 확인), 취소돼도 로드는 끝까지 해 캐시에 넣음
- why: 코드가 가장 적고 기존 장치 재사용. 미리 데우기는 LaMa · SAM 을 안 쓰는 서버도 시작이 15 초 늦어지고 메모리를 씀.
- effect: 취소 직후 다시 구우면 로드가 두 번 돌 수 있음(낭비만, 결과는 맞음).

## 10. map-3d B2 적용 범위 · 솟음 상한
- options: 직접 그린 건물만 / 사진 · 지도 탱크까지; 솟음 상한 = 박공처럼 min(…, 6.0, H×0.45) / H×0.45 만
- chosen: 직접 그린 건물(출처 drawn)만, 솟음 = min(반지름×비율(돔 0.2 · 원뿔 0.18), H×0.45)
- why: 사진 탱크 규칙 변경은 범위 밖이고 기존 장면 모양이 바뀐다. 돔 비율을 사진 탱크와 같게 둠.
- effect: OSM 탱크(height 태그 = 꼭대기)도 맞출지는 나중에(조건 한 줄). 반지름이 아주 작으면 테두리(0.9 m)가 입력 높이보다 조금 위로 나옴.

## 11. map-3d B3 기록 방식
- options: roof_shape 를 실제 모양으로 + 고른 것은 새 필드 / roof_shape 그대로 + 새 필드만
- chosen: roof_shape = 실제 지은 모양, roof_shape_requested = 고른 모양(다를 때만). 직접 그린 건물 파일의 roof 는 고른 값 그대로, 목록 응답에 roof_built 추가
- why: 지시("실제 지은 모양과 어긋나지 않게"). GIS 는 scene.json roof_shape 를 읽지 않음. 윤곽을 고치면 다시 박공이 될 수 있어 저장값은 사용자 선택 유지.
- effect: REST 는 더하기만.

## 13. A3 되돌리기 때 job 찾는 방법
- options: (a) 다시 만들기 때 옛→새 job 짝 기억 (b) 건물 가운데를 덮는 지금 영역 (c) map-3d 에 영역 키 REST 추가
- chosen: (b) — 옛 job 이 목록에 남아 있으면 그대로
- why: map-3d 자신의 규칙("장면 범위에 가운데가 든 건물")과 같고, 다른 PC 의 다시 만들기 · 목록 빠짐까지 덮으며 map-3d API 를 바꾸지 않는다.
- effect: 겹친 영역이 여럿이면 가장 새 장면. 가운데 = 꼭짓점 평균(map-3d 는 넓이 중심)이라 영역 경계에 걸친 건물만 드물게 다를 수 있음. `ResolveUserBuildingJob` 한 곳.

## 14. A4 "지도 화면" 범위
- options: 팝업 종류(확인 창 등)를 골라 내기 / 지도를 품은 MapView 범위로 가르기
- chosen: MapView 범위(+ 판 · 창 조상) + 그리기 시작 · 지도 누름 때 지도로 초점. 같은 종류인 조준 Esc 처리기(OnMapPreviewKeyDownForAim)도 함께 막음
- why: 이미지 자르기 IsImageCropPanelKey 와 같은 관용구이고 특정 팝업 종류에 묶이지 않는다.
- effect: 그리는 중 지도 화면 밖(좌측 메뉴 등)에 초점을 둔 채 누른 Enter 는 그쪽 몫. 지도를 누르면 초점이 돌아온다.

## 15. B3 GIS 안내 방식
- options: 박공 선택 막기 / 지붕 줄 밑 짧은 상태 글
- chosen: 상태 글 "이 모양은 평지붕만 됩니다"(경고색, 원뿔 · 돔이 안 되는 윤곽도 같은 글) + 규칙 설명은 기존 "?" 말풍선에 한 줄
- why: 박공을 먼저 고르고 그려도, 그린 뒤 윤곽을 고쳐도 같은 판정이 바로 보인다. 화면엔 상태만, 설명은 "?" 뒤(전 UI 방침).
- effect: 저장 본문의 roof 는 고른 값 그대로 — map-3d 가 실제 지은 모양을 scene.json 에 기록(11번).

## 12. map-3d A10 · A11 · A12
- A10: 장면 파일(GLB → scene.json)을 먼저 고치고(되돌릴 사본 보관) 저장 파일은 맨 끝 — 저장 파일은 다시 굽기의 기준이라 실패한 값이 남으면 안 됨.
- A11: 캐시 무효화 = scene.json (mtime_ns, 크기), 폴더 목록은 매번 훑음 — 가장 단순하고 서버 밖 변경도 반영.
- A12: 깨진 파일이 있으면 그 저장소 쓰기는 전부 거절(500 + 파일 이름), 읽기 · 굽기는 건너뛰고 경고 — 피해 쓰면 중복 · 지운 건물 부활 위험. 깨진 파일을 고칠 때까지 그 저장소 쓰기가 막힘.
