# 사망·관전 및 F 상호작용 연결

## 기존 구조와 변경 이유

기존 `PlayerHealth`는 로컬 체력과 사망, `NetPlayerHealth`는 서버 체력 판정,
`NetPlayerStatus`는 공개 상태 동기화, `PlayerEvents`는 판정 요청을 담당한다.
이 구조와 기존 클래스/public API를 유지했다. 사망 판정은 기존 서버 어댑터,
카메라와 관전 대상 선택은 새 `PlayerSpectatorController`가 담당한다.

`PlayerPrototype`의 F 획득은 `WorldItem.Interact → PlayerEvents.PickupRequested`까지
요청했지만 오프라인에서 이를 받는 `LocalGameplayJudge`가 씬에 없었다.
기존 컴포넌트 한 개를 추가했다. 네트워크 세션에서는 기존 NetPlayerInventory가
판정하며 LocalGameplayJudge의 핸들러는 실행되지 않는다.

## 추가 파일

| 파일 | 역할 |
| --- | --- |
| Health/PlayerActionGuard.cs | 활성/사망 여부에 따른 공통 월드 행동 검사 |
| Spectator/PlayerSpectatorController.cs | 로컬 자유 비행, 생존자 순환 추적, 대상 소멸 대응 |
| Tests/PlayMode/DeathSpectatorTests.cs | F 획득, 사망 입력, 권한, 관전, 전멸 회귀 테스트 7개 |

경로 기준은 `Assets/Project/Scripts/PlayerGameplay`, 테스트는 `Assets/Project`이다.

## 수정한 기존 파일

| 파일 또는 그룹 | 변경 내용 |
| --- | --- |
| Health/PlayerHealth.cs | 기존 Died 유지, 권한 검사, 공개 사망 적용, 읽기 전용 상태, 활성 플레이어 목록 |
| Input/PlayerInputReader.cs | 사망 중 월드 입력만 차단, Tab/메뉴 및 관전 입력 유지 |
| Player/PlayerController.cs, PlayerCameraController.cs | 사망 후 일반 이동·시점 변경 차단 |
| Interaction/PlayerInteractor.cs, PlayerViewModeController.cs | F/조사/관찰 차단 및 현재 모드 해제 |
| Interaction/InspectInteractable.cs, NPCObservationInteractable.cs | 직접 호출되는 조사/NPC 상호작용도 검사 |
| Exorcism/ExorcismFlagInteractable.cs | 사망자의 의식 상호작용 차단 |
| Items/PlayerItemController.cs | 사용·장착·슬롯 변경 차단, 사망 시 HeldVisual 즉시 정리 |
| Items/PlayerInventory.cs, WorldItem.cs | 직접 획득/슬롯 편집 요청도 차단, 기존 기록은 보존 |
| Items/BellItem/BellItem.cs, CoinItem/CoinItem.cs, SaltItem/SaltItem.cs, CompassItem/CompassItem.cs | 사용 진입점 생존 검사 |
| Event/PlayerEvents.cs | 죽은 플레이어의 획득 요청 거절 |
| DevTools/LocalGameplayJudge.cs | 네트워크 중 로컬 판정 방지, 생존/상호작용 검사, 스트레스 중복 경로 방지 |
| Scripts/Manager/UIManager.cs | InputReader 비활성화 대신 SetGameplayInputBlocked 사용 |
| Scripts/Manager/VoteManager.cs | 로컬 요청과 서버 투표 진입점의 사망자 검사 |
| Scripts/Network/Player/NetPlayer.cs | 로컬 컴포넌트 종료 후 자기 카메라/리스너 최종 상태 설정 |
| Scripts/Network/Player/NetPlayerHealth.cs | 서버 직접 체력 변경도 동기화, 사망 통지 중복 제거 |
| Scripts/Network/Player/NetPlayerStatus.cs | 서버가 전파한 사망만 클라이언트에 적용 |
| Scripts/Network/Player/NetPlayerInventory.cs, NetPlayerEquipment.cs, NetPlayerMovement.cs | 서버 사망자 요청 차단, 사망 시 장착 ID 해제 |
| Scripts/Network/Managers/PlayerRoster.cs | 생존자 수 변경 및 전멸 이벤트 추가 |
| Stress/PlayerStressController.cs, StressZone.cs, StressEventTrigger.cs 및 Scripts/Network/Player/NetPlayerStress.cs | 기존 스트레스 로컬/서버 중복 적용 및 영역 추적 수정; Stress/README 참조 |
| Tests/PlayMode/Project.Stress.Tests.asmdef | Mirror/InputSystem 테스트 참조 |

`Scripts/Manager`, `Scripts/Network`, `Tests` 경로는 `Assets/Project` 기준이다.
별도로 진행 중인 Inventory UI/QuickSlot UI와 씬 정리 변경은 이 작업의 변경 목록에 포함하지 않는다.

## Prefab / Inspector

다음 두 프리팹에 `PlayerSpectatorController`를 추가하고 Player Camera를 기존 Main Camera에 연결했다.

- `Assets/Project/Prefabs/Player/Player (1).prefab`
- `Assets/Project/Prefabs/Network/Player/NetPlayer.prefab`

NetPlayer의 Local Only Components에는 관전 컴포넌트를 추가했다.
NetPlayer에 누락되어 있던 기존 스트레스 피드백의 Volume/심박/호흡 참조도 연결했다.
기존 NetworkIdentity, NetworkTransform, NetworkAnimator 설정 및 SteamTransport는 유지했다.
캐릭터 Mesh/Material/Texture/Animator와 아이템 Hold 시스템을 교체하지 않았다.

Inspector: Move Speed 6, Fast Multiplier 2, Look Sensitivity 0.08,
Follow Offset (0.5, 2, -3), Follow Blend Speed 10.
Flight Bounds는 선택 사항이며 지정한 Collider의 **월드 AABB**로 위치를 제한한다.
회전된 임의 체적의 정밀 내부 판정은 아니다. 지정하지 않으면 충돌 없는 자유 비행이다.

씬 변경은 기존 `Assets/Project/Scenes/PlayerPrototype/PlayerPrototype.unity`에
오프라인용 `LocalGameplayJudge`를 한 개 추가한 것이다. 네트워크 씬에는 추가하지 않는다.
이 씬의 기존 NetworkIdentity 제거 Override 때문에 발생하는 PlayerController 경고는
임의로 네트워크 설정을 변경하지 않고 남겨 두었다. 네트워크 검사는 NetPlayer 프리팹으로 수행했다.

## 입력 / 동작

기존 ShamanInput asset과 생성 코드는 변경하지 않았다.
기존 PlayerInputReader에 Inspector 편집 가능한 관전용 InputAction 5개만 추가했다.

| 상태 | 입력 |
| --- | --- |
| 자유 관전 | WASD 이동, 마우스 회전, Space 상승, 왼쪽 Ctrl 하강, Shift 가속 |
| 생존자 선택 | `[` 이전, `]` 다음 |
| 자유/추적 전환 | 백틱 키 |
| 메뉴 | 기존 Tab Notebook, I Inventory, Cancel 유지 |

관전 전용 키는 사망 상태에서만 동작한다. UI가 열려 GameplayInputBlocked인 동안에는
관전 이동도 멈추지만 메뉴 열기/닫기 입력은 유지한다.
실제 Notebook UI는 현재 프로젝트에 없으며 새로 구현하지 않았다.
Notebook 담당자는 아래 읽기 전용 API로 새 기록/자동 증거 등록을 막아야 한다.

사망 후 자신의 원래 카메라와 AudioListener만 끄고 로컬 런타임 카메라를 만든다.
다른 플레이어 카메라는 제어하지 않는다. 몸은 이동하지 않는다.
생존자 사망/비활성화/삭제/연결 해제 시 다음 생존자를 선택한다.
생존자가 없으면 추적 대상을 null로 정리하고 자유 관전을 유지하며 NoLivingTargets를 알린다.
게임오버 전환 여부는 Core 담당자가 결정한다.

## 동기화와 권한

- 서버: 기존 NetPlayerHealth가 피해 판정, PlayerHealth가 사망 확정,
  PlayerEvents.DeathConfirmed → NetPlayerStatus.IsDead SyncVar로 모든 관찰자에 전파.
- HP/스트레스 수치: 기존 Owner 동기화 유지. 공개 사망 상태/스트레스 단계는 NetPlayerStatus.
- 장착 해제: 기존 NetPlayerEquipment의 장착 ID를 서버에서 -1로 변경.
- 로컬 전용: 관전 카메라/입력/대상 선택/Notebook 표시.
- Dedicated 서버는 관전 카메라를 만들지 않도록 소유자 검사를 사용한다.
- 숨겨진 AI/귀신 정보 메시지나 관전자 권한을 새로 추가하지 않았다.
- 기존 NetworkTransform의 클라이언트 권한 이동 구조를 서버 권한 이동/안티치트 구조로
  재설계하지 않았다. 정상 입력/기존 Command 진입점의 사망자 행동을 차단한다.

## 검증

2026-10-10 Unity 6000.3.23f1:

- Unity Test Runner PlayMode: **15/15 통과** (Stress 8 + DeathSpectator 7).
- 기존 NetPlayer로 Windows 개발 빌드 성공, localhost KCP Host + 실제 standalone Client 2개:
  **Host 20 / Client A 21 / Client B 10 검사 모두 통과**.
- A 사망 전파, A만 관전, Host/B 생존, 죽은 A의 F 차단·Tab 콜백 허용,
  자유 카메라 이동/몸 고정, 생존자 순환, B 연결 해제 후 Host 추적,
  마지막 Host 사망/전멸 이벤트 1회/추적 대상 null 확인.
- 실제 PlayerPrototype Play Mode에서 InputSystem F 이벤트 → TestWorldItem 획득 → 슬롯 1 등록 확인.
  사망 후 InputReader 활성, 상호작용 차단, 자유 카메라 이동 확인.
- Computer Use로 Editor와 Game View를 선택했으나 네이티브 창 캡처는 timeout.
  검증 화면은 Unity ScreenCapture로 저장했다. F 검증은 InputSystem 이벤트 주입을 사용했다.

재실행: Window → General → Test Runner → PlayMode → Project.Stress.Tests.
수동: PlayerPrototype에서 아이템을 바라보고 F → 슬롯 등록 확인 → 피해로 HP 0 →
WASD/Space/Ctrl 관전 → F/슬롯/사용 차단 → Tab 입력과 UI 연결 확인.
네트워크 QA의 임시 씬·스크립트는 게임 Assets에서 제거하고 작업 폴더에 보관한다.
실제 Steam 로비/원격 인터넷 연결 및 별도 Dedicated 빌드는 이번에 실행하지 않았다.

현재 `NetGM`에 이미 존재하던 전멸 시 GameState.Lose 처리는 변경하지 않았다.
이번 관전 코드 자체는 게임오버/UI/아이템 드롭/부활/시체/음성 채널을 구현하지 않는다.

## Integration API

| 목적 | 연결점 |
| --- | --- |
| Player 사망 감지 | `PlayerHealth.Died += Action<GameObject>` (인자는 피해 원인; 해당 인스턴스가 죽은 Player) |
| 서버 사망 통지 | `PlayerEvents.DeathConfirmed += Action<PlayerHealth, GameObject>` (서버 또는 오프라인 확정) |
| 모든 플레이어 사망 | 서버 `PlayerRoster.AllPlayersDead += Action` |
| 서버 생존 수 | `PlayerRoster.AliveCount`, `AliveCountChanged += Action<int>` |
| 공개 사망 상태 | `NetPlayerStatus.IsDead`, `DeadChanged += Action<bool>` |
| 관전 상태 | `PlayerSpectatorController.IsSpectating`, `IsFollowing`, `SpectateTarget` |
| 관전 대상 변경 | `SpectateTargetChanged += Action<GameObject>` (대상 없음이면 null) |
| 관전 UI | `SpectatorModeEntered`, `SpectatorModeExited`, `NoLivingTargets` |
| Notebook 읽기 전용 | `PlayerHealth.IsDead`, `IsNotebookReadOnly`, `CanRecordObservations` |
| 일반 월드 행동 | `PlayerHealth.CanAffectWorld`, `PlayerActionGuard.CanAct(player)` |
| 관전 UI 버튼 | `PreviousTarget()`, `NextTarget()`, `UseFreeCamera()`, `ToggleFollow()` |
| UI 입력 잠금 | `PlayerInputReader.SetGameplayInputBlocked(bool)` |
| 아이템 정리 | 기존 Died 구독 또는 `PlayerItemController.ClearHeldVisualsOnDeath()` (현재 사망 처리에서 호출) |

이벤트는 구독한 컴포넌트의 OnDisable에서 해제한다. UI 구독 전에 이미 사망했을 수 있으므로
처음 연결할 때 현재 Property도 읽는다. ActivePlayers는 로컬의 활성 복사본 목록이며
서버 생존 인원 판정에는 PlayerRoster를 사용한다. 부활/재접속 복구는 별도 확장 과제이다.
