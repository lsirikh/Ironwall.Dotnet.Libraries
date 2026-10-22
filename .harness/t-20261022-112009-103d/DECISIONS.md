# DECISIONS — t-20261022-112009-103d

## 1. 무응답 카메라에 매달린 ONVIF 호출을 어떻게 풀까
- options: (a) WCF 송신 제한(8초)을 줄인다 — OnvifSolution 서브모듈 수정이라 AGENTS.md 금지 · (b) 부른 쪽이 포기하면 게이트만 먼저 풀고 호출은 뒤에서 계속 — 같은 WCF 클라이언트 병렬 호출 · 정지가 이동보다 먼저 도착할 수 있어 정지 우선 설계(FR-22)를 깬다 · (c) 부른 쪽이 포기한 뒤에도 문턱(1초) 넘게 답이 없으면 그 연결을 끊고(Abort) 다음 명령이 다시 붙는다
- chosen: (c) — `PtzController.CallAsync` · `AbandonStalled` · `LiveModelAsync`, 초기화는 자기 클라이언트라 포기 즉시 게이트를 푼다
- why: 순서(정지 우선)를 그대로 지키면서 줄 막힘만 없앤다. 정상 호출은 수 ms 라 문턱 전에 끝나 끊기지 않는다. pattern: follow-recommended · verify(가짜 카메라로 7초 → 3초 미만 측정)
- effect: 카메라가 돌아온 뒤 첫 명령 · 팝업 다시 열기 지연 6.5~7초 → 수백 ms. 응답이 1초 넘게 걸리는 아주 느린 링크에서 부른 쪽이 먼저 포기하면 연결을 다시 만든다(초기화 왕복 3번). 문턱은 `PtzMotionPolicy.StallThreshold` · `BackgroundCallTimeout` 로 바꾼다.

## 2. "연결됨"의 기준 — 프로파일을 못 읽은 모델을 캐시할까
- options: 지금처럼 클라이언트만 생기면 캐시 · GetProfiles 성공(프로파일 1개 이상)까지 와야 캐시
- chosen: 프로파일 1개 이상일 때만 캐시(아니면 연결 실패, 다음 준비에서 재시도)
- why: 클라이언트는 계정 없이 답하는(PRE_AUTH) 호출만으로 생겨 틀린 비밀번호도 "연결됨"이 됐다. PTZ · 이미징 · 영상 주소가 모두 프로파일 토큰을 써서 프로파일 없는 모델은 쓸 데가 없다. pattern: follow-recommended
- effect: 틀린 계정은 팝업에서 연결 안 됨으로 보이고, 고치면 호스트 재시작 없이 붙는다. 프로파일이 0개인 카메라(ONVIF Profile S 위반)는 붙지 않는다. 되돌리려면 `InitCoreAsync` 의 `model.Profiles is not { Count: > 0 }` 분기를 뺀다.

## 3. 같은 카메라 id 의 접속 정보가 바뀌면
- options: 무시(호스트 재시작까지 옛 연결) · 주소 · 포트 · 계정(비밀번호 포함)이 다르면 캐시를 버리고 새로 붙음
- chosen: 버리고 새로 붙음(`CtxFor`, 키 = 주소:포트 + 계정 + 비밀번호, 메모리 안에서만 비교)
- why: 장비 설정에서 비밀번호 · 주소를 고친 뒤 팝업을 다시 열어도 계속 실패했다. pattern: broad
- effect: 정보가 같으면 지금과 똑같이 캐시를 재사용한다(추가 왕복 없음).

## 4. 고치지 않고 남긴 것(서브모듈 · 범위 밖)
- options: OnvifSolution 의 실패 뭉뚱그림(GoPTZPreset 등이 거부 · 시간 초과를 모두 false) · `SoapSecurityHeader.PasswordDigest` 매개변수 무시를 이 저장소에서 고친다 · 남긴다
- chosen: 남긴다(보고서 "남은 한계"에 적음)
- why: AGENTS.md — OnvifSolution 은 서브모듈이라 이 저장소에서 고치지 않는다. 두 가지 모두 지금 동작에는 결함을 만들지 않는다(팝업은 실패를 한 가지로 보여 준다 · 호출부가 늘 같은 비밀번호를 넘긴다).
- effect: 서브모듈 저장소에서 따로 고칠 수 있다.

## 5. 가짜 카메라 팜에 이미징을 어떻게 넣을까
- options: 옵션으로 켤 때만 · 모든 가짜 카메라(고정 포함)가 이미징 서비스를 광고
- chosen: 모두 광고(GetCapabilities · GetServices 에 Imaging XAddr), 초점 속도 범위는 ±0.5(클라이언트가 GetMoveOptions 를 지키는지 드러나게)
- why: 실제 카메라는 고정 · PTZ 모두 주야간 · 초점을 준다. 기존 시험 115개가 그대로 통과하는 것을 확인했다. pattern: reuse
- effect: 팝업 옵션 탭이 가짜 카메라에서도 켜진다(헤디드 확인 가능).
