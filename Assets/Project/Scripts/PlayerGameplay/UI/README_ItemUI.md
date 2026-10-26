# Player Item UI — 6.5단계

기존 Inventory / QuickSlot / Crafting 데이터와 API를 유지하며 표시만 정돈했다. 기준 폰트는 기존 `Visuals/Fonts/PlayerItemFont.asset`이다. 새 폰트·Canvas·UI 프레임워크를 추가하지 않는다.

## 크기와 배치

- Inventory / Crafting: 1280×720 기준 980×520, 가운데 정렬. 16:9 화면 면적의 약 55.3%를 사용한다.
- `PlayerItemUIStyle`은 기존 Canvas/부모 배율에 맞춰 표시 단위를 환산한다. CanvasScaler 설정은 변경하지 않는다. 좁은 Game View에서는 가로·세로 중 작은 배율로 화면 안에 맞춘다.
- Inventory: 4열, 셀 210×174, 간격 16, 아이콘 98, 이름 20, 수량 18. 이름은 두 줄 영역을 확보한다. 재료 이름 앞 `[공용]`을 유지한다.
- 현재 테스트 Scene의 InventoryPanel은 프리팹 인스턴스가 아닌 별도 UI다. `InventoryPanelUI`가 런타임에 기존 Grid를 ScrollRect/RectMask2D로 감싸 동일한 표시 규칙을 적용한다. 기존 `_itemGrid`와 아이템 인스턴스 참조를 유지하며 Scene asset은 저장하지 않는다. 이미 ScrollRect가 있으면 재사용한다.
- 부적 목록: 행 높이 124, 썸네일 50×100, 이름/효과 22. 왼쪽 스크롤 목록, 오른쪽 상세·등록 버튼·6개 슬롯 선택기를 사용한다. 서브 화면에서는 메인 화면 하단 안내만 숨긴다.
- QuickSlot: 기존 장식 아트와 6칸의 상대 배치, selection accent/tint/하단 marker 그대로. 기준 표시 너비 560, 화면 아래 중앙. 수량 19. 기존 `_iconAreaRatio` 설정과 드로잉 썸네일 규칙을 유지한다.
- Crafting: 제목 32, 섹션 23, 결과 이름 28, 재료 22, 상태 21. 왼쪽 기존 드롭다운, 오른쪽 결과·재료 상세. 제작 버튼 180×56. 대량 제작·제작 시간·장소 제한은 추가하지 않는다.

## 제작 피드백

`CraftingPanelUI.Bind` / `SetRecipes` / `SelectRecipe` / `CraftSelected` API를 유지한다. 제작은 항상 기존 `PlayerCraftingController.RequestCraft`를 호출한다.

- 재료는 `보유 / 필요`로 표시하며 부족한 행을 붉은색과 `부족` 글자로 구분한다.
- 재료 부족, 비활성/사망 등의 현재 상태, 제작 권한 및 공용 재료 변경 권한을 구분한다.
- 성공은 `Crafted` 이벤트에서만 표시한다. 요청만 전달한 경우 `제작 요청을 보냈습니다.`로 표시하며, 임의로 성공 처리하지 않는다.
- 성공 메시지는 Inspector의 Feedback Duration(기본 4초) 동안 유지된다. 그 아래에는 다음 제작 가능 여부를 즉시 표시한다. 마지막 재료를 쓴 경우 성공 메시지와 다음 제작의 부족 이유가 함께 표시된다.
- InventoryChanged → Refresh로 공용 수량과 버튼을 즉시 갱신한다. 변경된 재료가 부족 조건을 해소하면 과거 부족 메시지는 제거한다. 인벤토리 이벤트 없이 바뀌는 권한/상태는 UI가 열린 동안 0.25초마다 재평가한다.
- 성공/실패 외부 음원은 추가하지 않았다.

## 현재 개발 메뉴 레시피

| 레시피 자산 | 공용 비용 | 개인 결과 |
|---|---|---|
| `Data/Items/SaltRecipe.asset` | 굵은소금 2 | 소금 1 |
| `Data/Items/Debug/Test_RegistryTalismanRecipe.asset` | 한지 2 + 닭피 1 | 효과 부적 1 |
| `Data/Items/Debug/Test_BarrierTalismanRecipe.asset` | 한지 2 + 닭피 1 + 굵은소금 1 | 결계 부적 1 |
| `Data/Items/BeanMugwortRecipe.asset` | 팥 2 + 쑥 1 + 실타래 1 | 팥쑥타래 1 |

효과 부적 자산의 Test/Registry 파일명은 GUID·기존 참조 호환 때문에 유지한다. 표시명은 이미 현행 기획이다. 소금·팥쑥 레시피는 기존 자산이 없어 추가했다. 2026-10-16 확정: 효과 부적은 한지 2 + 닭피 1, 결계 부적은 한지 2 + 닭피 1 + 굵은소금 1. 결계 제작 결과는 실제 BarrierTalisman(item_barrier_talisman)이며, Player (1)/(2)의 수첩 및 F7 개발 메뉴에 위 4개 레시피를 연결한다.

## 진입 및 후속 연결

- F7은 별도 Development 오브젝트에서 사용하는 개발 전용 진입이다. Player root에 Debug를 다시 붙이지 않는다. F8/F9 범위는 `../Debug/README.md` 참고.
- 실제 Release 게임의 제작 진입 버튼/장소는 이번 작업에서 새로 결정하지 않았다. 외부 UI 진입 담당자는 기존 패널에 `Bind(playerCraftingController)`, `SetRecipes(...)`를 호출하고 입력·커서 소유권을 관리해야 한다.
- `CanCraft`, `CanRequestCraft`, `SetAuthorityCheck`, `CraftRequested` 확장 지점은 그대로다. 일반 제작 장소/Phase 전용 CanOpen 훅은 아직 없다. 장소 조건은 기획 확정 후 외부 진입과 서버 검증에 연결해야 한다.
- Network 담당자는 기존 요청 경계를 이용해 서버 판정, 공용 재료 동시 변경, 개인 결과 지급과 확인 응답을 연결해야 한다. 현재 요청 전달 UI를 서버의 확인된 성공 응답으로 바꾸는 클라이언트 연결도 후속 대상이다. 이번 단계에서는 Network API를 변경하거나 실제 전송을 구현하지 않았다.
- 기존 PlayerLocalUIBootstrap 통합과 StressHUD/Crosshair의 서로 다른 표시 규칙은 변경하지 않는다.
