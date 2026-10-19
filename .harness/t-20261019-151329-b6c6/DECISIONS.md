# DECISIONS

## 1. "모델" 을 연결 장비(LinkedDevice)로 볼지, 3D 모양 프리셋(ModelVariant)으로 볼지
- options: 연결 장비 1:1 강제 / ModelVariant 1:1 강제
- chosen: 연결 장비(LinkedDevice)
- why: ModelVariant 는 고정형 · 돔형 · PTZ 3종 모양이라 여러 마커가 공유하는 게 정상이고 "널로 비울" 대상이 아님. 반면 이벤트 조회표는 장비당 심볼 1개만 가져 중복 연결 시 한 마커만 이벤트를 받고, 부팅 재바인딩은 첫 마커에만 장비 객체를 붙여 다른 마커의 카메라 팝업 · 문 제어가 죽는다(PLAN 근거).
- effect: ModelVariant 는 그대로. 바꾸려면 MapViewModel.DeviceReassign.cs 의 판정을 다른 속성으로 옮기면 된다.

## 2. 확인 팝업에 '아니오' 콜백이 없는 문제
- options: (a) 고르는 즉시 B 를 원래 값으로 되돌리고 '확인' 콜백에서만 재할당 / (b) 먼저 바꾸고 취소 콜백 신설
- chosen: (a)
- why: 표준 OpenConfirmPopupMessageModel 은 '확인' 때만 MessageModel 을 재발행한다. 공용 팝업 인프라(다른 저장소 · 여러 화면 공용)를 바꾸지 않고 저장도 '확인' 전에는 한 건도 하지 않는다.
- effect: 팝업이 떠 있는 동안 B 콤보는 원래 값을 보인다. '확인' 시 바뀐다.

## 3. 저장 순서와 실패 시 원복
- options: A 비움 먼저 / B 연결 먼저
- chosen: A(기존 마커) 비움 저장 → B 연결 저장. B 실패 시 A 를 다시 연결해 저장(원복), A 실패 시 아무것도 안 바꿈.
- why: 중간에 끊겨도 DB 에 같은 장비가 두 마커에 남는 순간이 없다(최악은 '아무 데도 연결 안 됨').
- effect: MapViewModel.DeviceReassign.cs HandleAsync 한 곳.

## 4. 이미 중복된 자료를 불러올 때 어느 마커를 유효로 볼지
- options: 첫 마커(지도 마커 순서 = DB 불러온 순서) / 가장 최근 마커(Id 큰 쪽)
- chosen: 첫 마커 — 기존 InitializeDeviceSymbolIntegration 의 FirstOrDefault 동작 그대로
- why: 이미 운영 중인 동작을 바꾸지 않아 다른 기능(조회표 · 재바인딩)과 어긋나지 않는다. 자동 수정 저장은 하지 않는다(로그 경고 + 속성 판 표시만).
- effect: 바꾸려면 InitializeDeviceSymbolIntegration 의 1차 매칭을 OrderByDescending(Id) 로.

## 5. 복제 · 붙여넣기 시 장비 연결
- options: 복제본 연결 비우기 / 확인 후 옮기기
- chosen: 비우기(기존 ApplyCopyTransform 동작 유지, 시험으로 고정)
- why: 복제는 보통 같은 모양의 다른 장비를 놓으려는 것이고, 이미 그렇게 동작한다 — 코드 변경 최소.
- effect: 없음.

## 6. Undo 범위
- options: 재할당만 Undo / 연결 변경 전부 Undo 가능
- chosen: LinkedDevice 를 replay 가능 속성으로 추가 → 일반 연결 변경도 Undo 됨(재할당은 배치 1단위)
- why: EditRecorder 가 replay 불가 속성은 버리므로 재할당 Undo 를 만들려면 등록이 필요하고, 단일 연결 변경만 빼는 예외가 더 복잡하다.
- effect: UndoableCommandBase 의 "LinkedDevice" 3줄.
