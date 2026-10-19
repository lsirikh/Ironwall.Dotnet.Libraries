# DECISIONS — 3D 만들기 취소

## 1. 없는 작업 · 끝난 작업 취소의 응답 모양
- options: ① 없는 작업 404 · 끝난 작업 409 ② 늘 200 + `result`(cancelled · requested · too_late · already · missing)
- chosen: ② 늘 200 + result
- why: 목표가 "무해한 응답"을 요구했고, 404 를 쓰지 않아야 GIS 가 404 · 405 를 "옛 서버(취소 API 없음)"로 확실히 가를 수 있다.
- effect: GIS `Map3DClient.CancelJobAsync` 는 404 · 405 를 Unsupported 로 본다. 서버 응답 모양을 바꾸면 그 판정(Map3DClient) 한 곳만 고치면 된다.

## 2. 취소 확인 방식(파이프라인을 어떻게 멈추나)
- options: ① generate 에 cancel 인자를 넘겨 모든 함수에 전달 ② 작업 스레드에 붙인 threading.Event(map3d/cancel.py) 를 긴 반복 · 진행 콜백이 check()
- chosen: ② 스레드 지역 취소 표시 + `Cancelled(BaseException)`
- why: 함수 서명을 바꾸지 않아 변경 줄이 가장 적고, 표시가 없는 CLI · 기존 시험은 동작이 그대로다. BaseException 이라 파이프라인 곳곳의 실패 대체(`except Exception`)에 먹히지 않는다.
  네트워크 대기(OSM · DEM · LLM)는 별도 데몬 스레드에서 돌리며 0.2 s 마다 확인(`cancel.run`) — 취소되면 그 호출 결과는 버린다.
- effect: 새 긴 반복을 만들면 `cancel.check()` 한 줄을 넣어야 1~2초 안에 멈춘다.

## 3. GLB 쓰기 단계에서의 취소
- options: ① 어느 때나 취소(반쯤 쓴 파일은 지움) ② GLB 쓰기(진행 0.85) 뒤로는 취소하지 않고 `too_late` 로 알림
- chosen: ②
- why: 조정(edit)처럼 같은 폴더에 다시 쓰는 경우 쓰는 도중 멈추면 옛 장면이 깨진다. 쓰기는 짧다. 판정은 jobs 의 잠금 안에서 진행 콜백과 맞춰(경합 없음).
- effect: GIS 는 "거의 다 만들어 취소하지 않습니다 — 곧 끝납니다" 안내 후 그대로 결과를 기다린다. `WRITE_FRAC` 상수로 조정.

## 4. GIS 가 취소를 언제 "끝"으로 보나
- options: ① 서버 상태가 cancelled 가 될 때까지 진행 조회 ② 서버가 취소 요청을 받으면(cancelled · requested · missing) 곧바로 만들기 전 상태로
- chosen: ②
- why: 목표의 "취소 중 다시 「만들기」 가능"을 바로 만족한다. 서버는 1~2초 안에 멈추고, 새 굽기는 서버 대기열(작업자 1)에서 그 뒤에 선다.
  늦게 끝난 앞 굽기의 조회는 CancellationToken 으로 끊고, 굽기마다 CancellationTokenSource 를 대조해 새 굽기 상태를 건드리지 않는다.
- effect: `already`(막 끝남) · `too_late` 는 조회를 계속해 실제 결과를 보인다.

## 5. 옛 서버(취소 API 없음) 처리
- options: ① 단추를 처음부터 숨김(서버 판 미리 묻기) ② 누르면 "이 3D 서버는 취소를 지원하지 않습니다" 안내 후 그 굽기 동안 단추 숨김
- chosen: ②
- why: 서버 판을 미리 물을 API 가 없어(새 API 를 더 만들지 않음) 첫 요청 결과로 판정하는 것이 가장 적은 코드. 굽기마다 다시 판정해 서버를 바꿔도 맞는다.
- effect: `_map3DBakeCancelUnsupported` 는 새 굽기마다 false 로 돌아간다.

## 6. 영역 배지 · 3D 영역 목록의 「만들기 취소」 위치
- options: ① 굽는 동안 모든 영역 항목에 ② 「다시 만들기」 중인 그 영역 항목에만(맨 위, 「다시 만들기」 대신) — 새 영역은 진행 판 단추로
- chosen: ②
- why: 목표 "굽는 영역 항목에". 새 영역은 다 만들기 전엔 목록에 항목이 없다. 만드는 중엔 다시 만들기가 막혀 있으므로 그 자리를 취소가 대신한다. 목록 줄에는 " · 다시 만드는 중".
- effect: `Map3DAreaItem` 새 생성자(cancelBake 인자) — 기존 생성자는 그대로(공개 형식 더하기만).

## 7. 이전 장면 보존 규칙
- options: ① 다시 만들기를 같은 작업 폴더에 덮어쓰게 바꿈 ② 지금 규칙 유지(다시 만들기 = 새 작업 폴더, 옛 장면은 그대로 · 최신 장면이 이김)
- chosen: ② — 확인만(코드 변경 없음)
- why: 지금 규칙이 이미 "성공했을 때만 바뀐다"를 만족한다. 취소하면 새 폴더만 지운다. 조정(edit) 취소는 같은 폴더지만 GLB 쓰기 전에만 멈추므로 옛 파일 그대로(시험으로 확인).
- effect: 없음.
