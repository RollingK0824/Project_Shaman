# Player & Item 연결 안내 — 2026-10-10

기존 PlayerInventory / WorldItem / ItemBase 경로를 유지했다. 이 문서의 변경은 첨부 요청의
PlayerGameplay Inventory·Items·Stress·UI, Player/UI 프리팹 범위 안에서만 진행했다.
이전에 완료한 스트레스/사망/관전 작업의 미커밋 변경은 별개이며 되돌리지 않았다.

## 구현 내용

- 같은 ItemData는 수량으로 합산한다. 6개 슬롯, 1~6 입력, 기존 배경 선택 표시와 아이콘,
  2개 이상일 때 xN 표기를 유지했다. 0개가 되면 슬롯과 현재 장착물도 해제한다.
- Material은 보유 가능하지만 자동·수동 퀵슬롯 등록은 거절한다. 빈 슬롯이 없어도 획득한다.
- InventoryPanelUI.Bind(inventory)는 아이콘/이름/수량을 종류별 한 칸에 표시한다.
  InventoryChanged 이벤트로 자동 갱신하며 Bind(null)은 이전 내용을 비운다.
- CraftingRecipeData는 ItemData 참조와 양수 수량으로 구성한다. 중복 재료 행을 합산한다.
  PlayerCraftingController.CanCraft/TryCraft는 모든 비용을 확인한 뒤 수량과 슬롯을 한 번에
  갱신한다. 부족/잘못된 레시피는 아무것도 차감하지 않는다. 이벤트 콜백의 중복 제작도 막는다.
- 제작 UI는 레시피 선택, 결과 아이콘/수량, 보유량/필요량, 제작 버튼만 제공한다.
  재료 변경과 제작 후 즉시 갱신한다. 사망한 플레이어의 제작도 차단한다.
- StressHUD는 화면 상단 중앙의 독립 Canvas다. Player (1)에는 작은 Bootstrap만 있으며
  PlayerStressController/PlayerHealth는 그대로 남아 있다. Bind(stress, health)를 사용하고
  부모 탐색은 하지 않는다. 사망 시 숨김은 Inspector 옵션이다.
- CoinItem은 기존 중앙 Raycast → ICoinReactable.ReactToCoin(Owner) 호출을 유지한다.
  기본 0.5초 재사용 제한은 장착물을 교체해도 유지된다. 짧게 아이템만 내미는 피드백을
  추가했다. 대상 없음/실패는 비소비, 성공도 Consume On Successful Use가 false면 비소비다.
  귀신 여부를 알려 주는 UI는 없다. 손 리그/손 핵심 스크립트는 변경하지 않았다.

## 수정 파일

아래 경로는 Assets/Project 기준이다. 함께 생성되는 Unity .meta도 포함한다.

- Scripts/PlayerGameplay/Items/ItemData.cs
- Scripts/PlayerGameplay/Items/PlayerInventory.cs
- Scripts/PlayerGameplay/Items/PlayerItemController.cs
- Scripts/PlayerGameplay/Items/CoinItem/CoinItem.cs
- Scripts/PlayerGameplay/Inventory/InventoryItemSlotUI.cs
- Scripts/PlayerGameplay/Inventory/InventoryPanelUI.cs
- Scripts/PlayerGameplay/Inventory/QuickSlotUI.cs
- Scripts/PlayerGameplay/UI/PlayerStressHUD.cs
- Scripts/PlayerGameplay/Stress/README.md
- Prefabs/Player/Player (1).prefab
- Prefabs/UI/StressHUD.prefab
- Prefabs/UI/Inventory/ItemSlot.prefab

## 새 파일

- Scripts/PlayerGameplay/Inventory/CraftingRecipeData.cs
- Scripts/PlayerGameplay/Inventory/PlayerCraftingController.cs
- Scripts/PlayerGameplay/Inventory/CraftingPanelUI.cs
- Scripts/PlayerGameplay/Inventory/README_PlayerItem.md (이 문서)
- Scripts/PlayerGameplay/UI/PlayerStressHUDBootstrap.cs
- Prefabs/UI/CraftingPanelUI.prefab
- Prefabs/UI/Inventory/InventoryPanelUI.prefab
- Prefabs/UI/Fonts/PlayerItemFont.asset (기존 한글 폰트의 UI 전용 복사본)

## Inspector / 수첩 담당자 연결

Player (1)은 PlayerCraftingController와 PlayerStressHUDBootstrap 연결을 완료했다.
Bootstrap의 Hud Prefab은 StressHUD의 PlayerStressHUD, Stress/Health는 같은 Player의 컴포넌트다.
게임 씬은 저장하거나 바꾸지 않았다.

1. Canvas 안에 InventoryPanelUI.prefab 또는 CraftingPanelUI.prefab을 넣는다.
   프리팹 내부 글자/아이콘/그리드/버튼 참조는 연결되어 있다. 둘 다 Canvas 자체는 포함하지 않는다.
2. Canvas에 GraphicRaycaster와 씬의 기존 EventSystem/InputSystemUIInputModule이 필요하다.
   수첩 열기/닫기, 커서 해제, 이동·아이템 입력 잠금은 수첩 담당자가 기존 입력 방식으로 처리한다.
3. 로컬 플레이어가 정해지면 inventoryPanel.Bind(playerInventory),
   craftingPanel.Bind(playerCraftingController)를 호출한다. 재연결과 비활성/재활성도 지원한다.
4. Create > Project Shaman > Crafting Recipe로 SO를 만든다. 승인된 데이터는 Data/Items 아래에
   저장하고 Ingredients Item/Amount, Result Item/Amount를 설정한다. 아이콘은 선택 사항이다.
   UI Recipes 목록 또는 SetRecipes(...)에 레시피를 전달한다.
5. QuickSlotUI를 재사용할 때 Bind(inventory, inputReader)로 명시적으로 연결할 수 있다.
6. 엽전 프리팹의 CoinItem에서 Reuse Delay(기본 0.5), Feedback Duration(0.22),
   Feedback Offset, Consume On Successful Use(기본 false)를 조절한다.

첨부 기획서의 제작 재료/레시피가 미정이므로 게임용 레시피 SO는 임의 생성하지 않았다.
Play Mode 캡처의 '검증 재료/결과물'은 메모리에만 생성한 테스트 데이터이며 게임 에셋이 아니다.

## 네트워크 담당자 연결 — 이번 작업에서 수정하지 않음

- NetPlayer.prefab의 기존 자식 StressHUD 및 Local Only Components의 그 HUD 참조를 제거하고,
  PlayerStressHUDBootstrap을 붙여 위와 같이 연결한다. Bootstrap은 NetworkIdentity.isLocalPlayer를
  읽어 로컬 플레이어만 독립 HUD를 생성한다. **이 연결 전에는 NetPlayer의 기존 미바인딩 HUD가
  표시되지 않는다.** NetPlayerStress/NetworkIdentity/Authority 설정은 이번 작업에서 변경하지 않았다.
- NetPlayer에도 PlayerCraftingController를 추가한다. 오프라인은 TryCraft로 즉시 실행되지만
  온라인은 어댑터가 연결되기 전까지 제작을 비활성화한다. 기존 NetPlayerInventory는 추가 중심
  동기화 경로이므로 재료 소모와 결과 지급까지 전파하는 처리가 별도로 필요하다.
- 클라이언트 CraftRequested를 기존 서버 요청 경로에 연결한다. 승인된 레시피 ID, 소유자,
  생존/수첩 상태, 재료 수량, 요청 빈도를 서버에서 다시 검사한다. 서버의 컨트롤러에
  SetAuthorityCheck(() => NetworkServer.active)를 지정하고 TryCraft를 한 번 실행한 뒤,
  최종 수량/슬롯을 소유 클라이언트에 동기화한다. 클라이언트 요청만으로 권한을 허용하면 안 된다.
- 인벤토리 복제 적용 후 UI가 InventoryChanged/QuickSlotChanged를 받도록 기존 경로에 연결한다.
  이번 작업에서 SyncList, Command, SyncVar, NetworkAnimator, Transport는 바꾸지 않았다.
- 엽전 NPC 수취 판정과 선택적 소비의 서버 권한 처리는 기존 네트워크/NPC 담당 영역이다.
  여기서는 기존 ICoinReactable 인터페이스와 로컬 사용 제한만 유지·보완했다.

## 새 아이템 추가

ItemData 생성 → Category/Icon/WorldPrefab → HandProfile/VisualPrefab 및 3인칭 오프셋 연결 →
필요하면 기존 ItemBase를 상속한 기능 컴포넌트 → 필요하면 CraftingRecipeData에 참조한다.
Material은 HandProfile이 없어도 된다. 인벤토리/제작/퀵슬롯에 아이템 이름 분기를 추가하지 않는다.
새로운 손 자세, 실제 제작 재료 밸런스와 네트워크 아이템 ID 등록은 각 담당자가 정한다.

## 검증 및 한계

Unity Editor의 PlayerPrototype Play Mode에서 런타임 테스트 오브젝트로 46개 검증을 통과했다.
실제 씬은 저장하지 않았다. 테스트 오브젝트와 임시 레시피는 Play 종료 시 제거했다.

- 기존 WorldItem 판정 경로로 획득, 중복 수량, 0개 제거, Material 차단, 슬롯 가득 찬 경우 획득.
- Input System 키 1~6 입력, 선택 배경/아이콘/x2, 선택 아이템 소진 시 장착물 해제.
- 제작 부족/정확한 비용/복수 결과/반복 제작/중복 재료/잘못된 수량/이벤트 재진입 차단.
- 실제 프리팹의 제작 버튼 이벤트와 즉시 표시 갱신, 인벤토리 종류별 병합/이름/재바인딩.
- Player 자식 HUD 없음, 독립 HUD 루트 생성, 로컬 값 표시.
- 실제 엽전 프리팹의 대상 없음/실패/성공/소비 옵션/연타 및 재장착 우회 방지.
- 기존 방울·소금 프리팹의 반응 인터페이스 호출, 음기향 연기/감지 갱신.
- 사망 시 제작 및 버튼 차단. 최종 스크립트 컴파일 오류 없음.

반응 검증은 런타임 모의 ICoinReactable/IBellReactable/ISaltReactable 대상이다.
NPC 일과/귀신 AI를 검증하거나 변경했다는 의미는 아니다. 이번 제작/HUD 분리의 온라인
종단 테스트는 네트워크 연결이 금지 범위여서 미실시했다. 이전 스트레스/사망 네트워크 테스트와
구분한다. 기존 로컬 씬의 NetworkIdentity 제거 Override 관련 경고는 그대로 남아 있다.

수동 재현: 기존 아이템 F 획득 → 중복 획득 → 1~6 선택 → 소비 후 슬롯 확인.
별도 테스트 Canvas에 패널 Bind 후 승인된 임시 레시피 지정 → 부족/충분/반복 제작 확인.
DarkStressZone/SafeStressZone에서 화면 상단 수치 증가/회복을 확인한다.

미구현: 실제 게임 레시피/재료 선정, 완성형 수첩, Scene 배치, 서버 제작·소모 동기화,
NetPlayer HUD 이관, 다른 담당자 영역의 게임 시스템. 첨부 요청에 따라 의도적으로 제외했다.

작업 직전 스냅샷과 완료 시 파일 해시/Git diff를 비교해 허용 범위 밖 추가 변경이 없음을
확인한다. 기존 미커밋 변경을 이번 작업의 변경으로 혼동하지 않도록 별도 기준선을 보관했다.
