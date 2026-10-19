# DECISIONS — t-20261019-183759-f6fc

## 1. 3D 로 보던 장면이 지워졌을 때 — 2D 복귀 vs 새 장면으로 다시 불러오기
- options: (a) 2D 로 돌아오고 짧은 안내, 같은 자리에서 곧바로 다시 들어가지 않음 (b) 같은 자리를 덮는 새 장면이 있으면 그것으로 다시 불러오기
- chosen: (a)
- why: 지워진 이유(다른 사람이 지움 · 다시 만들기 · 서버 정리)를 GIS 가 모르고, 다른 장면으로 몰래 바꾸면 사용자가 무엇이 바뀌었는지 모른다. 새 장면은 「3D 로 보기」 단추로 바로 들어갈 수 있다.
- effect: `RetireMap3DJob` 이 보던 중이면 `_map3DEntrySuppressed = true` + `Map3DGuide.SceneMissingText`. 바꾸려면 그 줄을 빼면 자동(줌) 방식에서 게이트가 새 장면으로 다시 들어간다.

## 2. 「다시 만들기」가 끝나면 옛 job 을 서버에서 지울까
- options: (a) GIS 가 새 장면 완료 직후 옛 job 을 DELETE (b) 서버에 그대로 두고 GIS 에서만 새 장면을 앞세움
- chosen: (a)
- why: 목표가 "다시 만들기 후 영역 목록 · 배지까지 옛 job 참조 0". 서버에 남기면 다음 목록 새로 고침에서 옛 영역 · 배지가 되살아난다. 보고 기록에서도 사용자가 다시 만든 뒤 옛 영역을 손으로 지웠다. 직접 입력 건물 높이는 map-3d 덮어쓰기 파일(영역 단위)에 있어 다시 굽기가 다시 붙인다.
- effect: `ReplaceMap3DJobAsync`. 지우기 실패(견본 405 · 서버)면 로그만 남기고 새 장면이 캐시 · 목록 앞에 있어 먼저 잡힌다. 바꾸려면 그 메서드의 DeleteSceneAsync 를 빼면 된다.

## 3. 겹친 영역에서 어느 장면을 고를까
- options: (a) 최근 장면 먼저(굽기 완료 장면을 캐시 맨 앞에, 서버 목록은 이미 최신순) (b) job id(시각) 최댓값
- chosen: (a)
- why: 기존 시험(`Map3DEntryScenarioTests` 겹침 행)이 "목록 앞 영역이 가운데 영역" 규칙에 기대고 있고, 서버 목록이 created 내림차순이라 같은 결과다.
- effect: `BakeMap3DAsync` 의 `_map3DScenes.Insert(0, …)`.

## 4. 회전을 어느 신호로 받을까
- options: (a) 지도 뷰포트 스냅샷(회전 동기 계약 FR-05 — 모든 회전 경로 · 프레임당 1회) (b) MapRotation DP 값 변경 구독
- chosen: (a) — 이미 VM 이 구독하는 `OnViewportSnapshotForVm` 에 한 줄
- why: 키 · 나침반 · 3D 동기 · 회전 토글 · 앵커 보정이 모두 이 스냅샷을 낸다. DP 구독은 해제 관리가 따로 필요하다. 방위가 바뀐 때만 게이트 큐에 넣어 이동 · 줌과 같은 프레임당 1회 갱신을 쓴다.
- effect: `OnMap3DAreaBearingChanged`.

## 5. map-3d 뷰어 실패 이유 분류
- options: (a) scene-missing(404) · scene-broken(그 밖 · 형식 아님) · server-unreachable 3종 (b) 404 만 따로
- chosen: (a)
- why: 서버가 꺼진 경우와 지워진 경우를 사용자 글로 구분해야 "서버 꺼진건가?" 질문에 답이 된다. GIS 는 scene-missing 만 job 을 지우고, 나머지는 지금 진입만 취소한다(영역은 남김).
- effect: `sceneload.js` LOAD_TEXT, `Map3DGuide.SceneLoadFailedText`.
