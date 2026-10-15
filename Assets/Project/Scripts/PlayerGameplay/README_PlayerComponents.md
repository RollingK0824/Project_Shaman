> **6단계 후속 적용:** 현재 Player는 Root 24 / Child 107 / 총 131 Component, MonoBehaviour 24개다. PlayerCraftingDebug는 별도 개발 오브젝트로 이동했고 두 HUD Bootstrap은 PlayerLocalUIBootstrap으로 통합했다. 아래 조사 기록 중 통합 전 판단은 당시 근거이며 현재 구성은 [개발 도구](Debug/README.md), [Local UI](UI/README_LocalUI.md)를 우선한다.

# Player (1) Component 구성 — 6단계

현재 Player는 PL_1, FirstPersonItemAnchor/ItemFeedbackPivot 구조를 사용한다. 옛 손/Grip/수첩 표현을 복구하지 않는다. 6단계 조사 결과 80개 GameObject, Root 26개/Child 107개 Component(Transform 포함), MonoBehaviour 26개, Renderer 14개를 유지했다. 안전한 제거/통합 조건을 모두 만족하는 중복 Component는 없었다.

## Inspector 순서

Transform / CharacterController → Input / Movement / Crouch → Camera / View / Sway → Interaction / Prompt / Crosshair → Inventory / Item / Crafting / Notebook → Talisman → Stress / Damage / HUD / Feedback → Health / DeathVisual / Spectator → CraftingDebug.

정렬은 root m_Component 순서만 변경했다. 타입/fileID, Component 데이터, Transform 계층과 실행 순서 attribute는 그대로다. NetworkBehaviour인 PlayerController의 목록 위치와 설정도 그대로다. Inspector 순서는 Unity 실행 순서를 보장하는 수단이 아니다.

## 명시적 참조와 의도된 None

- PlayerCraftingController.Inventory: 같은 Player의 PlayerInventory. 기존 Awake 자동 검색과 같은 대상이다.
- PlayerDeathVisual.Animator: CharacterVisual/PL_1의 기존 Animator. Death State는 비어 있으므로 현재 즉시 숨김 흐름을 그대로 따른다.
- PlayerInventory.SharedMaterials: 세션 공용 저장소를 런타임에 ResolveOffline/BindSharedMaterials로 연결한다. Player 아래 개인 저장소를 추가하지 않는다.
- PlayerItemController.ItemHoldDriver: 현재 PL_1의 선택적 3인칭 계약. 옛 리그를 연결하지 않는다.
- PlayerFeedbackController.HitVolume/HandFeedbackPivot: 선택적 연출 대상이다. 현재 HitFeedbackPivot과 Stress Volume/심박/호흡을 사용한다.
- PlayerSpectatorController.FlightBounds: 미지정이면 자유 비행.
- InteractionPromptHUD.Panel/Text/KeyIcon: Panel이 없으면 기존 OnGUI 표시를 사용한다.
- PlayerController.Animator: 기존 None 상태를 유지했다. 이 컴포넌트는 NetworkBehaviour이며 6단계 Network 설정 변경 금지 대상이다. 이동 애니메이터 연결은 담당자 검토 항목이다.

## 분리 유지 이유

- ViewModeController가 본체 Renderer hide/show를 이미 소유한다. CameraController의 yaw/pitch, Focus, Spectator는 독립 제어가 필요하다.
- PlayerHandSway는 FirstPersonItemAnchor의 이동 흔들림(실행 순서100), PlayerFeedbackController는 HitFeedbackPivot의 피격/스트레스 연출이다. 서로 다른 대상/상태/수명이므로 통합하지 않았다.
- 심박/호흡/암전은 이미 Feedback 하나에 모여 있다. Stress state, StressDamage, HUD는 별도 책임이다.
- Stress HUD는 입력 차단/사망 때도 유지하지만 Crosshair는 숨긴다. 두 Bootstrap은 같은 생명주기가 아니다.
- ItemController의 장착 해제는 전환/소진에도 사용한다. DeathVisual에 흡수하면 상태 소유권이 섞인다. Health → 행동/아이템 종료 → DeathVisual → Spectator 순서를 유지한다.
- Crouch는 독립 ceiling check와 높이/속도 상태를 가진다. NetworkBehaviour인 Movement에 합치지 않는다.
- Inventory/Equip/Crafting, Talisman Collection/Effect, Health/Stress는 각각 별도 상태 소유자다.
- PlayerCraftingDebug는 현재 F7 제작 화면 진입과 F8/F9 개발 입력을 제공한다. 제거하면 기존 테스트/제작 동선이 사라지므로 유지한다.
- PlayerNotebook는 HasNotebook/RequestOpen/NotebookOpenRequested만 유지한다.

## 레거시와 인계

Player_Backup.prefab은 BACKUP, NotebookPresentation.prefab은 LEGACY이며 각각 Missing Script 1개를 가진다. 전체 Assets GUID/C# 참조 검색에서는 사용처를 찾지 못했으므로 DELETE_CANDIDATE로 기록했지만 보존 자료를 이번 단계에서 삭제하지 않았다. 활성 Player (1)은 Missing Script 0이다.

현재 PlayerController의 NetworkBehaviour, 기존 NetworkIdentity 부재, 권한/Sync/Network 컴포넌트와 외부 어댑터는 변경하지 않았다. 공유 재료/아이템/피해/부적 API의 서버 연결은 별도 담당자가 처리한다. 추가 기능이나 다음 단계 작업은 이 정리에 포함하지 않는다.

