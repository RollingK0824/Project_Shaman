# PlayerGameplay 현재 구조

5단계는 코드/문서 정리 단계다. 게임 규칙, public API, serialized field, Player 프리팹 구조와 PL_1은 유지한다. Player Component 정리는 별도 6단계이며 아직 수행하지 않는다.

## 책임별 위치

| 경로 | 책임 |
|---|---|
| Input | Input System 생성 바인딩과 플레이어 입력 이벤트 |
| Player | 이동·카메라·앉기·로컬 표현·기본 수첩 진입점 |
| Interaction | F 상호작용·조준·조사/집중 관찰·1/3인칭 전환 |
| Items | 데이터/장착/획득, 개인 인벤토리의 기존 API, 아이템별 기능 |
| Items/Combat | 공격 1회 중복 방지·HP/CC 계약·권한 경계 |
| Items/Talisman | 그림 소유/저장·효과 적용·표현 데이터 |
| Inventory | 개인/공용 UI·6퀵슬롯·제작 레시피/트랜잭션 연결 |
| UI | 독립 HUD·조준점·부적 드로잉 입력/화면 |
| Health / Stress / Feedback / Spectator | HP/사망, 스트레스, 시청각 표현, 로컬 관전 |
| Event / DevTools | 기존 요청 버스 / 오프라인 판정기 |
| Exorcism / Debug | 의식 깃발 상호작용 / 개발용 수신기·테스트 의식·제작 단축키 |

책임이 다른 컴포넌트를 파일 수 감소 목적으로 합치지 않는다. PlayerInventory의 기존 위치는 API 변경/불필요한 이동을 피하려고 유지한다. Debug의 컴포넌트도 실제 Scene/Prefab에 참조되어 있어 삭제하지 않는다. 일회성 Stage QA 도구는 프로덕션 Runtime에 포함하지 않는다.

## 현재 아이템·인벤토리 규칙

- 일반 아이템 획득은 개인 인벤토리에 들어가며 처음 획득한 종류는 빈 퀵슬롯에 자동 등록된다. 슬롯이 가득 차도 획득은 가능하다.
- Material은 세션 SharedMaterialInventory에만 보관한다. 직접 장착/퀵슬롯 등록은 거절한다. 오프라인은 같은 Scene의 저장소를 공유하며 온라인 저장소/권한/스냅샷 연결은 네트워크 담당자 작업이다.
- 제작은 CraftingRecipeData → PlayerCraftingController → 기존 개인/공용 저장소 트랜잭션을 사용한다. 현재 효과 부적 레시피는 공용 한지1 + 닭피1 → 개인 빈 효과 부적1이다.
- 숫자키 1~6은 선택된 슬롯만 accent tint와 하단 마커로 강조한다. 기존 QuickSlot 아트를 사용한다. 빈 슬롯도 선택 표시를 옮기며 장착물은 해제한다.
- 1인칭은 FirstPersonItemAnchor 아래 ItemData.VisualPrefab을 표시한다. HandProfile/Grip/옛 손 Mesh는 사용하지 않는다. 위치/회전/크기는 ItemData의 First Person 설정으로 조정한다.
- PL_1은 유지한다. 선택적 3인칭 ItemHold API는 1인칭 Grip 레거시와 다른 계약이므로 보존한다. 기존 NetworkIdentity 문제를 이 정리 단계에서 고치지 않는다.

## 부적·사용 기능

- 효과 부적의 canonical 데이터는 기존 GUID를 유지한 `Talismans/RegistryTalisman.asset`이다. Registry enum alias=0과 Designation enum 값은 직렬화 호환 때문에 남겨 둔다.
- 빈 효과 부적 사용 → 자유 드로잉 → 그림/이름/효과 지정 → 저장 성공 시 빈 부적1 소모 + 고유 ID 그림1 보관.
- 드로잉된 효과 부적 그룹 → 개별 그림 선택 → 퀵슬롯 등록 → 숫자키 장착 → 사용. 저장 즉시 자동 장착하지 않는다. 같은 ID의 중복 슬롯 등록은 거절한다.
- 그림은 collection이 소유하므로 해제/재장착 후에도 유지된다. 사용 성공 시 ConsumeOnUse에 따라 그림과 바인딩/장착물/아이콘을 정리한다. 실패는 비소모다.
- Ctrl+Z 기능은 유지하며 별도 Undo 버튼은 없다. 다음 장/주민 지목/정답 패턴/제출 판정은 활성화하지 않는다.
- 결계 부적은 별도 유지하며 UseRequested 인계 지점을 제공한다. 미확정 효과를 새로 만들지 않는다.
- 초혼경은 사용 시작 LIVE, 취소 Freeze. 해제·사망 시 RT/런타임 리소스를 정리한다.
- 징은 HP와 별개인 SensoryInterference CC를 요청한다. 실제 Ghost의 감각 처리 및 최종 AudioClip 연결은 담당자 작업이다.
- 수첩은 HasNotebook 및 RequestOpen → NotebookOpenRequested만 제공한다. 펼치기 UI/애니메이션/퀵슬롯 아이템으로 복구하지 않는다.

## 공통 계약과 후속 연결

- HP 피해: IDamageable + DamageSource/PlayerEvents. CC: IItemControlReceiver. 공격 1회: ItemAttackSession.
- IBellReactable/ISaltReactable/IBeanMugwortReactable는 실제 fallback이다. 공통 계약과 이중 호출하지 않는다. ICoinReactable는 수취 계약으로 별도 유지한다.
- PlayerActionGuard는 생존/활성 월드 행동을 검사한다. 사망 후 아이템 정리 → CharacterVisual 처리 → 관전으로 이어진다. Player root는 삭제하지 않는다.
- 스트레스 수치/노출은 PlayerStressController, 지속 노출 HP 피해는 PlayerStressDamage, 표시/연출은 HUD/Feedback이 담당한다. 각각의 역할을 유지한다.
- SaveRequested/UseRequested/PickupRequested/CraftRequested, SetAuthorityCheck, Export, ApplyReplicated 계열은 네트워크 인계 API다. adapter가 없다고 unused로 제거하지 않는다.
- 실제 서버/다중 클라이언트, late join, 라운드 reset, 공유 재료/그림 ID/효과/원격 장착 동기화는 기존 담당자 연결 후 종단 검증이 필요하다.

## 상세 문서 우선순위

1. 현재 전체 구조는 이 문서.
2. 부적/그림별 퀵슬롯/레시피/복제 인계는 [4단계 인계](Items/Talisman/StageFour_Integration.txt).
3. 공용 재료/아이템의 기반은 [3단계 인계](Inventory/StageThree_Integration.txt), 공격 계약은 [Combat 안내](Items/Combat/README.md).
4. 스트레스는 [Stress 안내](Stress/README.md), 관전 API는 [Spectator 안내](Spectator/README.md).
5. Legacy 표시된 초기 README의 HandProfile/미정 레시피/미구현 징 설명은 과거 이력이다. 현재 연결 방식으로 재적용하지 않는다.
