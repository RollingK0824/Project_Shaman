# 스트레스 시스템

## 화면 상단 HUD

`Assets/Project/Prefabs/UI/StressHUD.prefab`은 독립 Canvas 프리팹이다.
Player (1)의 PlayerStressHUDBootstrap이 런타임 UI 루트에 생성한다. Player의 자식이 아니다.
상단 중앙에 스트레스 수치(소수 1자리 / 100), 게이지, 안정·긴장·위험 단계를 표시한다.
PlayerStressHUD.Bind(stress, health)로 대상을 명시하고 변경 이벤트만 구독한다.
부모 탐색이나 수치 변경을 하지 않는다. Bootstrap은 네트워크 로컬 소유자만 UI를 생성한다.
NetPlayer는 이번 Player & Item 작업의 수정 금지 영역이다. 기존 자식 HUD를 제거하고
Bootstrap을 연결하는 네트워크 담당자 작업은 Inventory/README_PlayerItem.md를 참고한다.
사망 후에는 기본적으로 숨기며 Inspector의 Hide On Death에서 조절할 수 있다.
Stage Colors로 단계별 색상을, TopCenterPanel RectTransform으로 위치/크기를 조정한다.
CanvasScaler는 화면 크기에 맞춰 배율을 조절한다. 입력을 받지 않는 표시 전용 UI이다.
폰트는 기존 ChosunCentennial의 UI 영역 복사본을 사용한다.

기존 PlayerStressController / StressZone / StressEventTrigger / PlayerFeedbackController를
확장했다. 별도 스트레스 매니저를 만들지 않았다. 기존 DarkStressZone,
SafeStressZone, AnomalyStressTest 구성과 IStressReceiver 진입점을 유지한다.

## 규칙과 Inspector

- 범위 0~100, 기본 Mid 35 / High 70. PlayerStressController에서 임계값 조정.
- StressZone: Increase/Reduce, Stress Per Second와 Cause 설정. Collider는 Is Trigger.
  어둠 기본 +10/초, 안전 구역 예시는 -15/초. 실제 씬의 Inspector 값이 적용된다.
- 같은 플레이어의 여러 Collider는 영역 하나의 증가율로 묶는다.
  여러 증가 영역은 합산하며, 안전 구역 안에서는 지속 증가보다 회복이 우선한다.
- StressEventTrigger: Cause/Stress Amount/Once Per Entry. 기본 20.
  중복 Collider 진입을 한 번으로 묶되 다른 플레이어와 재진입은 별도 처리한다.
- 영역 비활성화, Collider 비활성화, 순간이동으로 Exit 누락 시 잔여 효과를 정리한다.
- 스트레스 자체는 체력을 변경하지 않으며 100에 도달해도 사망하지 않는다.

어둠은 기존 영역 기반이다. 렌더링 픽셀 밝기를 자동 측정하는 시스템은 추가하지 않았다.
귀신/이상현상 감지자는 실제 노출이 확정되는 지점에서 기존
`IStressReceiver.ReceiveStress(amount, StressCause.GhostSight/AnomalySight, source)`를 사용한다.
지속 노출은 `SetSourceRate(source, rate)` / `RemoveSource(source)`로 연결할 수 있다.
이번 작업에서 별도의 귀신 AI나 시야 감지 시스템을 중복 구현하지 않았다.

## 피드백

기존 PlayerFeedbackController의 피격 피드백과 함께 동작한다.

| 단계 | 연출 |
| --- | --- |
| Low | 스트레스 흔들림/루프음 없음 |
| Mid | 심박·호흡, 카메라와 오른손의 약한 흔들림 |
| High | 점차 강한 흔들림, 화면 가장자리 Vignette |

스트레스 회복 시 연출도 부드럽게 감소한다. 카메라/리스너가 비활성인 원격 복사본에서는
개인 피드백을 초기화하고 오디오를 멈춘다. 사망 관전 진입 때도 종료한다.
카메라 Look, 이동 속도, 체력, 기존 Animator를 덮어쓰지 않고 별도 Pivot의 로컬 오프셋을 사용한다.

Inspector 연결: Feedback Pivot → 기존 HitFeedbackPivot, Feedback Camera → Main Camera,
Hand Feedback Pivot → FirstPersonRightHand, Stress Volume → StressFeedback의 Volume,
Heartbeat/Breathing Source → 전용 AudioSource. 기존 Hit Volume은 유지한다.
음량, pitch, shake 크기/속도, blend 속도는 Inspector에서 조정한다.

관련 에셋: `Assets/Project/Settings/StressVolumeProfile.asset`,
`Assets/Project/Audio/Stress/Stress_Heartbeat.wav`, `Stress_Breathing.wav`.
루프음은 이 작업에서 생성한 기본 효과음이며 팀 오디오로 교체 가능하다.

Player (1)과 NetPlayer 모두 기존 Main Camera 아래 StressFeedback/Heartbeat/Breathing을
연결했다. NetPlayer에는 현재 FirstPersonRightHand가 없으므로 그 참조는 비워 두었다.
새 손 모델을 생성하지 않았으며, 기존 카메라 흔들림은 현재 카메라 아래 아이템에도 적용된다.

## 기존 네트워크 구조와 연결

NetPlayerStress가 있으면 UseExternalAuthority를 설정하고 스트레스 변화 요청을 기존
PlayerEvents.StressRequested로 전달한다. NetPlayerStress의 서버 판정에서 한 번만 적용한다.
오프라인 일반 Player는 바로 계산한다. LocalGameplayJudge를 사용하는 외부 판정 경로에서도
SetStress로 확정하여 요청 재귀나 이중 증가를 피한다.
기존 GameManager.Ongoing 조건과 Owner 수치/공개 단계 동기화는 유지했다.

## API / 테스트

상태: CurrentStress, MaxStress, NormalizedStress, CurrentLevel, FeedbackIntensity, HighIntensity.
알림: StressChanged, StressLevelChanged, StressReceived.
설정/테스트: SetStress, AddStress, ReduceStress. 게임 노출 요청은 위 IStressReceiver 경로 사용.
서버 판정 프로젝트에서는 외부 클라이언트 코드가 SetStress로 확정값을 직접 수정하지 않는다.

Unity Test Runner PlayMode의 StressSystemTests 8개 통과:
범위/비정상 입력, 임계값 이벤트, 귀신·이상현상 시 HP 불변,
안전 우선순위, 다중 Collider, 비활성/순간이동 정리, 재진입, 오디오/Volume/손 흔들림/피격 보존.
기존 Game View에서 Low/Mid/High 및 100 스트레스에서 HP100 생존을 확인했다.
High 상태의 걷기/달리기 속도도 유지됨을 확인했다.
회귀 테스트는 사망/관전 7개와 함께 총 15개 모두 통과했다.
추가로 기존 NetPlayer를 사용한 실제 localhost Host + Client 2개에서,
서버가 Client A의 스트레스를 100으로 올렸을 때 A의 수치/피드백만 증가하고
공개 High 단계는 모든 클라이언트에 전파되며 사망하지 않는 것을 검증했다.

수동: DarkStressZone 진입 → 수치 증가 → SafeStressZone 진입 → 감소 → 영역 이탈 후 고정.
AnomalyStressTest 재진입과 두 플레이어 진입을 확인하고, 35/70 경계에서 오디오와
화면 효과를 관찰한다. 스트레스 100에서도 체력 감소나 사망이 없어야 한다.
