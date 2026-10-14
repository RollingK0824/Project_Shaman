# Player & Item 후속 기능 — 2026-10-13

## 부적

`Data/Items/Talismans/BlankTalisman.asset`과 `ReservedTalisman.asset`은 기존 ItemData를 상속한
TalismanItemData다. 빈 부적만 Allow Drawing이 켜져 있다. 두 번째 부적은 기능 미정이며
UseRequested 이벤트만 제공한다. 정답 문양, 방어/퇴마/지목 효과, 소비 규칙은 구현하지 않았다.

각 데이터는 대응하는 `Prefabs/Items/Talismans/*World.prefab`, `*Held.prefab`,
`Data/Items/Talismans/*Grip.asset`에 연결되어 있다. 월드는 기존 WorldItem, 손에 드는 물체는
기존 ItemBase를 상속한 TalismanItem을 사용한다. 기존 손 Mesh/리그/스크립트는 변경하지 않았다.
표현은 기본 종이 사각형이며 최종 아트가 아니다. 종이 메시의 왼쪽 위가 부착 기준점이다.
아이콘은 빈 종이 모양이며 특정 문양을 암시하지 않는다.

WorldPrefab을 월드에 배치하는 작업은 Scene 담당자에게 맡긴다. 이번 작업은 Scene을 저장하지
않았다. 로컬 기존 획득 판정기가 WorldItem을 처리하면 인벤토리에 들어가고 기존 규칙으로
퀵슬롯에 등록된다. 빈 부적을 선택한 뒤 사용 버튼을 누르면 드로잉 UI가 열린다.

마우스 왼쪽 드래그로 그린다. 종이 영역 밖의 좌표는 데이터에 들어가지 않으며, 밖으로 나갔다가
돌아오면 새 획으로 시작한다. 초기화는 현재 장만 지운다. '다음 부적'은 같은 ItemData의 다른
실물을 선택한다. 완료 또는 Esc로 닫는다. UI를 여는 동안 기존 PlayerInputReader를 잠시
비활성화하여 이동·사용·인벤토리/수첩 중복 열기를 막고, 닫거나 장착 해제/사망하면 복원한다.
기존 UI로 입력이 차단된 상태에서는 드로잉을 열지 않는다.

### 데이터 수명 / 기존 인벤토리와의 연결

- PlayerTalismanCollection은 각 장의 GUID와 TalismanDrawingData를 플레이어에 보관한다.
- 장착물이나 UI를 파괴해도 플레이어의 해당 부적 인스턴스가 남아 있으면 그림이 유지된다.
- 기존 인벤토리는 계속 ItemData별 수량/슬롯을 관리한다. 필요한 추가 연결은
  `ItemUnitsChanged(ItemData, signedAmount)` 이벤트뿐이다. 기존 수량/UI/제작 코드는 재구현하지 않았다.
- 소비 시 현재 선택한 장부터 제거한다. 같은 부적 1장을 소비하여 같은 부적 1장을 제작하는
  경우에도 새 ID의 빈 장으로 교체한다. 수량 차이만 관찰해서 기존 그림을 잘못 재사용하지 않는다.
- 제작 결과가 2장이면 서로 다른 ID의 빈 그림 2개를 만든다. 기존 CraftingRecipeData의
  Result Item에 부적 데이터 참조를 넣으면 된다. 실제 레시피와 재료는 미정이므로 만들지 않았다.
- TalismanDrawingData는 정규화된 획 좌표만 가진다. UI 이벤트, 렌더러, 텍스처에 의존하지 않는다.
  표시용 TalismanDrawingSurface가 256×512 Texture2D로 렌더링한다. 각 표시 객체는 자신의
  텍스처를 만들고 파괴할 때 해제한다. MaterialPropertyBlock을 사용하여 공유 머티리얼을 변경하지 않는다.
- 한 장당 최대 512획/8192점이다. 자동 저장/디스크 저장/세션 재접속/아이템 드롭·양도와 네트워크
  그림 복제는 미구현이다. 현재 보장은 **동일 플레이어 런타임 인벤토리 안에서 해제·재장착 유지**다.
- 기존 시스템에는 개별 인스턴스를 드롭/거래하는 API가 없으므로 그림을 가진 부적을 다른 소유자에게
  옮길 때는 이후 담당자가 ID·획 데이터를 함께 넘겨야 한다. 네트워크에서 각 클라이언트가 임의 ID를
  따로 만드는 상태를 영구 식별자로 취급해서는 안 된다.

## 지속 스트레스 피해

PlayerStressController/HUD/영역 처리는 그대로다. 새 PlayerStressDamage가 별도로 판정한다.

Inspector 임시값: Damage Threshold 85, Hold Duration 10초, Damage Per Tick 2,
Damage Interval 2초. 일정 수치 이상이 계속 유지되면 대기시간이 끝날 때 첫 피해가 발생하고
그 이후 지정 간격으로 피해를 준다. 임계값 미만·비활성·사망·권한 상실 시 두 타이머를 초기화한다.
한 프레임에 지연된 피해 여러 번을 몰아서 주지 않는다. 시간은 게임의 Time.deltaTime을 따른다.

피해는 `PlayerHealth.ReceiveDamage(amount, StressDamage 오브젝트)`를 사용한다.
체력을 직접 빼거나 사망을 별도 판정하지 않는다. `[StressDamage]` 로그와 Source=StressDamage로
일반 공격과 구분된다. 이전 '스트레스는 절대 체력을 감소시키지 않는다'는 규칙은 이번 요청으로
확장되었으며, 이 컴포넌트가 붙은 플레이어는 지속 노출로 사망할 수 있다.

온라인에서는 기본적으로 비활성 판정이다. `SetServerAuthorityCheck(Func<bool>)`에 서버의
진행 중 라운드 판정을 연결해야 한다. 내부적으로 NetworkServer.active도 검사하여 클라이언트
delegate만으로 피해를 실행할 수 없게 했다. Network/Manager 코드는 수정하지 않았다.

## 사망 외형

PlayerHealth의 기존 사망 흐름 중 `DisableGameplay()` 바로 뒤, `Died` 이벤트 앞에
PlayerDeathVisual.ApplyDeathVisual 호출을 추가했다. 따라서 아이템 정리 → 월드 외형 처리 →
기존 관전 구독자 실행 순서가 된다. 기존 관전 코드는 변경하지 않았다.

PlayerDeathVisual의 Character Visual에는 기존 CharacterVisual을 지정했다. 유효한 기존 사망
Animator 상태를 지정하면 CrossFade로 재생하고 Hide After Seconds 뒤 숨긴다. 상태가 비어 있거나
없으면 즉시 CharacterVisual을 끈다. 현재 사용 가능한 사망 상태가 없어 기본은 즉시 숨김이다.
Root와 NetworkIdentity는 유지한다. CharacterController 및 CharacterVisual 하위 Collider는
끄고, 하위 Rigidbody는 kinematic으로 바꾼다. UI·카메라·관전 루트는 제거하지 않는다.

현재 게임은 사망 후 같은 루트를 되살리는 기능이 없으므로 부활용 복원은 구현하지 않았다.
향후 같은 오브젝트를 부활/풀링할 경우 visual/collider/animator/상태의 명시적 초기화가 필요하다.

## Inspector와 담당자 연결

Player (1)에 PlayerTalismanCollection / PlayerStressDamage / PlayerDeathVisual을 추가했다.
부적 Held의 Drawing Panel Prefab, 데이터의 Icon/World/Visual/HandProfile/3인칭 참조는 연결 완료다.
원하는 부착 위치는 새 부적 Grip의 Local Position/Euler/Scale 및 ItemData의 3인칭 오프셋에서 조절한다.
기존 다른 아이템의 프로필과 애니메이션은 변경하지 않았다.

UI prefab은 독립 Canvas이며 기존 EventSystem/InputSystemUIInputModule을 사용한다. Scene/Manager에
새 시스템을 설치하지 않는다. 수첩 담당자는 기존 UI 입력 잠금 규칙과 함께 드로잉 열기를 조율한다.

네트워크 담당자:

1. NetPlayer prefab은 이번 작업에서 수정하지 않았다. 위 세 컴포넌트를 NetPlayer에 붙이고
   기존 CharacterVisual/Animator 참조를 연결해야 한다.
2. 서버의 확정 스트레스 값으로만 타이머를 진행하게 하고, 시작 시
   `SetServerAuthorityCheck(() => 서버이고 라운드 진행 중)`을 연결한다. 기존 PlayerHealth 권한/
   HealthChanged/사망 복제 흐름을 유지하고 Host에서 중복 tick하지 않도록 한다.
3. 기존 사망 상태 복제가 각 복사본의 PlayerHealth.ApplyReplicatedDeath를 호출하면 해당 객체의
   PlayerDeathVisual도 같은 순서로 실행된다. 다른 클라이언트/늦게 참가한 클라이언트에서도
   이 경로가 실행되는지 확인한다. Root나 NetworkIdentity를 제거하지 않는다.
4. 부적 획득/제작/소비는 기존 서버 인벤토리 권한에서 확정한다. 인스턴스 ID와 소유권을 서버에서
   관리하고 획 좌표의 유한값·0~1 범위·개수·전송 빈도를 검증한다. 장착 동기화에는 ItemData뿐 아니라
   선택한 부적 ID를 포함하여 3인칭에 동일한 그림이 보이게 연결한다. 데이터 복제는 미구현이다.
5. TalismanItem 사용/효과 판정과 현재 예약 부적의 향후 효과는 해당 담당자가 정의한다.

## 경면주사 징

기존 ItemData → WorldItem → Inventory/QuickSlot → ItemBase.OnUseStarted 경로로 추가 가능하다.
새 ItemBase 파생 클래스가 사용 요청을 내보내고 귀신 담당자가 파동/감각 방해를 판정하면 된다.
이번 작업에서는 징 데이터/효과, Ghost/AI 수정, 정답 판정은 하지 않았다.

## 파일 변경

기존 수정: PlayerHealth.cs(사망 외형 호출), PlayerInventory.cs(실물 수량 이벤트),
PlayerItemController.cs(선택적 IItemVisualContext 연결), Player (1).prefab(컴포넌트 3개), Stress/README.md.

추가 스크립트: Stress/PlayerStressDamage.cs, Health/PlayerDeathVisual.cs, Items/IItemVisualContext.cs,
Items/Talisman/{TalismanItemData,TalismanDrawingData,PlayerTalismanCollection,TalismanDrawingSurface,TalismanItem}.cs,
UI/{TalismanDrawingCanvas,TalismanDrawingPanel}.cs. 이 문서 및 각 .meta 포함.

추가 데이터/프리팹: Data/Items/Talismans/{BlankTalisman,ReservedTalisman,BlankTalismanGrip,ReservedTalismanGrip}.asset,
Prefabs/Items/Talismans/{BlankTalismanHeld,ReservedTalismanHeld,BlankTalismanWorld,ReservedTalismanWorld}.prefab,
같은 폴더의 TalismanPaper.mat/TalismanPaper.asset/TalismanIcon.png,
Prefabs/UI/TalismanDrawingPanel.prefab. 각 .meta 포함.

기존 Inventory/Crafting/StressHUD/Coin/Bell/Salt/Incense 구현을 교체하거나 리팩터링하지 않았다.
테스트용 Editor 도구는 최종 게임 Assets 밖으로 보관한다. 기존 사용자의 Scene/폰트 변경은 보존한다.
