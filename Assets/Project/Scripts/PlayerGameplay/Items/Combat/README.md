# 공통 아이템 피격·방해 — 2단계

기존 `IDamageable`, `DamageSource`, `PlayerEvents.DamageRequested`, `PlayerHealth`를 유지한다.
HP는 아이템이 직접 수정하지 않는다. 오프라인에서는 기존 `LocalGameplayJudge`가 요청을
`IDamageable.ReceiveDamage`로 전달하고, 플레이어 사망은 기존 Health/관전 흐름을 따른다.

## 최소 구성

- `ItemAttackSession`: 공격 1회에 하나 생성하는 일반 C# 객체. 소유자, 피해/CC 값, 대상 LayerMask,
  종료 시각, 처리한 대상 집합을 보관한다. 초기 Overlap과 모든 Trigger 접촉이 같은 객체를 사용한다.
- `DamageTrigger`: 기존 환경 피해 Trigger를 유지하면서 `BeginAttack(session)` 경로를 추가했다.
  아이템 공격에서는 `OnTriggerEnter`가 세션에 접촉을 전달한다. Exit/재진입은 기록을 초기화하지 않는다.
  `CreateSphere`/`CreateBox`는 Rigidbody(kinematic) + Trigger Collider와 짧은 수명을 가진 월드 볼륨이다.
  시각 모델/Anchor의 크기와 독립적이며 Ignore Raycast Layer를 사용한다.
- `IItemControlReceiver`: HP와 별개인 `Slow`, `Stagger`, `SensoryInterference` 요청 계약.
  대상이 수락 여부를 반환한다. 지속시간, 둔화 배율, 시야/청각 플래그를 전달한다.
- `ItemAttackAuthority`: 공격자에 선택적으로 연결하는 승인 지점. 오프라인 기본 허용,
  온라인 클라이언트 차단, 서버도 명시적인 delegate 승인 없으면 차단한다.

피해는 `DamageSource.RequestDamage`를 통해 기존 이벤트 경로를 사용한다.
반환값/`HitApplied`는 유효 피해 요청 발행 또는 CC/기존 반응의 수락을 의미한다.
서버의 최종 피해 승인이나 실제 HP 감소 응답을 의미하지 않는다.

## 기존 아이템 연결

| 아이템 | 유지한 부분 | 공통 처리 |
|---|---|---|
| 소금 | 기존 카메라/상호작용 사거리·차폐 레이, Spray VFX, 성공 시 선택적 소비 | 기존 조준 사거리 내 전방 Trigger 상자(가까운 고체 표면에서 길이 제한). IDamageable 피해 우선, 없으면 ISaltReactable |
| 팥·쑥 | 투척 프리팹, 방향/힘/회전/수명, 재사용 시간, 투척 시 소비 | Initialize에서 Collider를 Trigger로 전환. 접촉 시 Slow 전달, 기존 Collision 변형도 동일 처리 경로 사용 |
| 재령 방울 | 기존 효과 반경/대상 마스크, Pulse VFX, 비소모 동작 | 짧은 Trigger 범위 + 초기 Overlap. 공통 Stagger 우선, 없으면 IBellReactable |
| 경면주사 징 | 3단계에서 GongItem 구현, HP 피해 0 | SensoryInterference + CreateSphere, 재사용 제한. 실제 AI 수신 및 최종 음원은 담당자 연결 |

초기 Overlap은 이미 범위 안에 있는 대상과 기존 즉시 사용 반응을 보존한다.
다음 물리 프레임의 OnTriggerEnter와 기록을 공유하므로 두 번 적용되지 않는다.
소금은 고체 조준 대상이 없어도 Trigger 전용 히트박스를 검출하며, 고체 벽 뒤로 사거리를 늘리지 않는다.
소금의 소비 콜백은 공격당 한 번만 실행한다. 타래는 기존처럼 투척 시 한 번 소비한다.
타래는 관계없는 Trigger 센서(예: 스트레스 영역)에는 소멸하지 않고, 유효 적중 또는 일반
월드 충돌 후 기존 지연 시간으로 정리한다. 사망/권한 상실 후에는 날아가던 공격도 효과를 전달하지 않는다.

## 인터페이스 유지 판단

- `IDamageable`: 공통 HP 계약으로 그대로 유지. 파일 변경 없음.
- `ISaltReactable`: HP가 없는 기존 소금 반응 대상 호환용으로 유지.
- `IBeanMugwortReactable`: 기존 타래 전용 대상의 둔화 요청 호환용으로 유지.
- `IBellReactable`: HP/CC 외의 소리 반응 등을 받는 기존 대상 호환용으로 유지.
- `ICoinReactable`: 이번 공격 공통화 대상이 아니므로 그대로 유지.
- `IItemControlReceiver`: 새 공통 CC 계약. 실제 AI, 효과 타이머/중첩/면역을 구현하지 않는다.

해당 공격의 공통 계약이 있으면 기존 전용 반응도 함께 호출하지 않는다.
공통 CC를 거절해도 기존 인터페이스로 우회하지 않는다. 전용 반응이 거절한 경우도 같은
공격에서 다른 Collider를 통해 재시도하지 않는다. 콜백 재진입을 막기 위해 호출 전에 기록한다.

## Inspector / 대상 연결

소금 새 임시값: Damage Amount=10, Contact Half Width=0.2m, Contact Duration=0.15초,
Target Mask=Everything. 방울: 기존 반경/마스크 유지, Pulse Duration=0.15초,
Stagger Duration=0.5초. 타래: 기존 Slow Multiplier=0.6, Slow Duration=3초.
수치는 임시값이며 실제 밸런스가 아니다. 프리팹/ItemData를 다시 만드는 작업은 필요 없다.

Ghost/NPC 담당자는 실제 대상으로 사용할 최상위 배우 오브젝트에 `IDamageable` 및 필요한
`IItemControlReceiver`를 구현한 컴포넌트를 배치하고, 그 아래에 여러 Collider를 둔다.
공격 판정은 Collider에서 부모의 계약을 탐색한다. 동일 배우의 히트박스마다 별도의 HP/CC
수신자를 만들면 별개 대상으로 취급될 수 있으므로 수신자를 공유해야 한다.
모든 대상 히트박스의 Layer를 일관되게 지정하고 Target Mask에서 허용 대상을 선택한다.
현재 Everything은 테스트용이며 아군 피해/팀 규칙은 정하지 않았다.

CC 수신자는 게임 규칙에 맞춰 면역, 중첩/갱신, 효과 만료와 원래 상태 복원을 담당한다.
둔화는 SpeedMultiplier, 경직은 Duration, 감각 방해는 Duration + Senses를 사용한다.
HP는 반드시 자기 `ReceiveDamage`/기존 사망 처리를 통해 적용한다.

기존 환경 `DamageTrigger`의 `_oncePerEntry=true`는 대상의 마지막 Collider가 나갈 때에만
다시 피해를 허용한다. 다른 대상은 독립적으로 피해를 받는다. 아이템 공격의 중복 방지는
이 옵션과 무관하게 공격 세션 전체 동안 유지된다. 재사용 볼륨은 비활성화 후 새 세션으로
BeginAttack하고 재활성화하여 새 진입을 발생시키거나 CreateSphere로 새 볼륨을 만든다.

## 네트워크 담당 인계

1. 서버가 기존 소유권/아이템 보유/소비/재사용 시간/라운드 상태를 검증한 뒤 사용 동작을 실행한다.
2. 공격자에 ItemAttackAuthority를 연결하고 `SetAuthorityCheck`에 지속 승인 조건을 연결한다.
   조건은 접촉 때마다 호출된다. 호출할 때마다 탄약을 차감하거나 쿨다운을 시작하면 안 된다.
   NetworkServer.active가 false인 클라이언트는 delegate가 true여도 효과를 적용하지 못한다.
3. 기존 NetPlayerHealth는 자기 PlayerHealth의 DamageRequested만 받는다. 귀신 HP는
   Ghost 담당 서버 어댑터가 해당 대상의 DamageRequested를 받아 서버 ReceiveDamage로
   전달해야 한다. 중복 구독/중복 처리하지 않는다. 온라인에서 LocalGameplayJudge를 사용하지 않는다.
4. CC 어댑터도 서버에서 실제 AI 상태를 바꾸고 필요한 시각/상태만 복제한다.
5. 투척물/범위 공격 생성, 사용·소모 승인, VFX 복제와 적중 결과 전달을 서버 경로에 연결한다.
   여기에는 Command/SyncVar/스폰 등록/NetworkIdentity 변경이 포함되어 있지 않다.
6. 서버 승인 전에는 소금·팥쑥·방울·징의 온라인 Gameplay 판정이 의도적으로 동작하지 않는다.
   로컬 VFX 재생만으로 피해가 승인된 것으로 취급하지 않는다.

실제 Ghost AI/Network 코드는 변경하지 않았다. Player (1)의 기존 NetworkIdentity 누락,
PL_1, ItemVisual Anchor, 초혼경 작업, 수첩 상태도 이 단계에서 변경하지 않는다.
