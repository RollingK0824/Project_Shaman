# Project Shaman — 재사용 가능한 오른손 Hold System

이번 작업은 **현재 프로젝트에 실제로 들어 있는** `Korean_Exorcist_Waist_Fixed.fbx`를 기준으로 했습니다. 캐릭터 메시, 얼굴, 헤어, 의상, 체형, Material, Texture, Bone, Weight를 변경하지 않았습니다. 제공 FBX는 작업 시작 시 프로젝트 FBX와 바이트 단위로 동일합니다. `.blend`에는 같은 캐릭터의 기존 메시를 유지하고 오른팔 Action 9개와 소켓 Empty만 추가했습니다.

## 실제 프로젝트에 적용된 내용

프로젝트: `D:/Jun/Project_Shaman`

- `Assets/ReusableItemHold/` — 독립적인 로컬 Hold 모듈.
- `Assets/Project/Animations/ShamanPlayer.controller` — 기존 Layer/상태/파라미터는 유지하고 `Upper Body Item Layer`, Integer `ItemHoldType`만 추가.
- `Assets/Project/Prefabs/Player/Player (1).prefab` — 기존 Humanoid Animator 오브젝트에 `ItemHoldDriver`, 기존 오른손 Bone 아래 `RightHand_ItemSocket` 추가.
- 변경 전 Prefab/Controller: 프로젝트 루트 `ItemHold_Backups/`.

**Mirror 코드나 설정은 수정하지 않았습니다.** NetworkIdentity, NetworkAnimator, NetworkTransform, Command, SyncVar, Authority 설정 및 기존 스크립트 필드는 유지됩니다. 기존 컴포넌트 17개의 직렬화 내용을 비교했고, 보호 대상 소스/아트 파일 2,661개의 해시도 동일합니다.

현재 인벤토리의 슬롯 선택 코드는 수정하지 않았습니다. 외부 담당 코드가 아래 API를 호출하고, 실제 아이템 Prefab을 소켓에 붙이면 됩니다. 기존 프로젝트에 남아 있는 이전 `OriginalRightArm`/`Right Hand Items` 방식과 새 API를 동시에 호출하지 마세요. 이전 네트워크 컴포넌트는 요청대로 그대로 두었으며, 담당자가 새 인터페이스에 연결할 수 있습니다.

## Hold Type

| 값 | 이름 | 용도 |
|---:|---|---|
| 0 | Empty | 빈손. 새 Layer Weight 0, 기존 팔 동작 사용 |
| 1 | OneHandSmall | 작은 물체를 엄지·검지로 집기 / 작은 물체 쥐기 |
| 2 | OneHandVertical | 세로 손잡이, 방울, 막대 |
| 3 | OneHandFront | 거울 등 앞쪽으로 들어 보기 |
| 4 | OneHandPalm | 펼친 손바닥 위의 나침반 등 |
| 5 | BookHold | 오른손으로 작은 수첩/책을 받쳐 보기 |

BookHold는 **오른손 한 손 자세**입니다. 왼팔을 책에 고정하는 양손 IK는 포함하지 않습니다.

## Animator와 공통 동작

`Upper Body Item Layer`는 Override Layer이며 `RightUpperBody.mask`로 오른쪽 어깨·팔·손·손가락만 제어합니다. Spine/Head/Left Arm/Legs/Root는 기존 locomotion에 남습니다.

`Empty → Equip → Hold_[Type] → Use → Hold_[Type] → Unequip → Empty`

- **Equip.anim 1개, Unequip.anim 1개**를 모든 아이템과 Hold Type이 공유합니다.
- Hold Pose 5개를 여러 아이템이 재사용합니다.
- 공통 Equip에서 선택한 Hold로 CrossFade합니다. 장착 중 Hold Type을 바꾸면 두 Hold Pose 사이를 약 0.26초 동안 Blend합니다.
- Unequip에서는 팔을 내리면서 Layer Weight를 0으로 줄여 현재 Idle/Walk/Run 팔 동작으로 돌아갑니다.
- `UseBell`은 손목 흔들기 0.72초, `UseObserve`는 관찰 위치로 올렸다가 돌아오기 1.15초입니다.
- 애니메이션에는 아이템 Mesh나 아이템 Transform 곡선이 없습니다.

## 외부 제어 API

`ItemHoldDriver`는 기존 Animator가 있는 오브젝트에 있습니다. Player 루트에서 찾을 때는 `GetComponentInChildren`을 사용합니다.

```csharp
using ProjectShaman.ItemHold;

ItemHoldDriver hold = GetComponentInChildren<ItemHoldDriver>();

hold.SetHoldType((int)HoldType.OneHandVertical);
hold.Equip();

hold.SetHoldType((int)HoldType.OneHandPalm); // 이미 장착 중이면 부드럽게 자세 전환
hold.Use();
hold.Unequip();
```

`SetHoldType()`은 타입을 선택합니다. 빈손에서 실제 장착하려면 `Equip()`도 호출합니다. `SetHoldType(0)`은 해제합니다. `Animator.SetInteger("ItemHoldType", value)`로 타입을 바꿔도 Driver가 반영합니다. Layer의 Play/CrossFade/Weight는 Driver가 관리하므로 같은 Layer를 다른 로컬 스크립트가 동시에 제어하지 않도록 합니다.

사용 가능한 이벤트: `Equipped`, `Unequipped`, `UseRequested(int)`, `UseCompleted`, `HoldTypeChanged(int)`.

## 소켓과 아이템 Offset

기존 오른손 Bone의 직접 자식:

`Right_Hand / RightHand_ItemSocket`

```csharp
GameObject visual = Instantiate(itemPrefab, hold.ItemSocket, false);
visual.transform.localPosition = itemOffsetPosition;
visual.transform.localRotation = Quaternion.Euler(itemOffsetEuler);
visual.transform.localScale = itemOffsetScale;
```

ItemHoldDriver는 아이템을 생성/삭제/복제하지 않습니다. 위치·회전·크기는 별도 아이템 데이터 또는 Prefab에서 조정합니다. 원본 Rig의 손 Bone에는 누적 Scale 100이 있으므로 Offset과 Scale은 소켓의 **로컬 좌표**로 조정해야 합니다. 임의로 Armature 스케일을 바꾸지 마세요. 예를 들어 기본 Unity 단위 크기의 프리미티브를 붙일 경우 필요한 스케일에 이 배율을 반영해야 합니다.

아이템을 내린 다음 교체하려면:

1. `Unequip()` 호출.
2. `Unequipped` 이벤트에서 이전 시각 오브젝트 제거, 새 Prefab/Offset 적용.
3. `SetHoldType(newType)`와 `Equip()` 호출.

같은 Hold Type을 사용하는 아이템은 클립을 새로 만들지 않고 Prefab과 Offset만 달리합니다. 물체 모양에 따른 자동 손가락 접촉 IK는 아니므로, 크기가 다른 아이템은 알맞은 Hold Type과 Offset을 선택해야 합니다.

## Use 확장

`Use()`의 기본 동작:

- OneHandVertical → Use ID 1 → `UseBell`.
- OneHandFront → Use ID 2 → `UseObserve`.
- 다른 타입 → 애니메이션 없이 UseRequested / UseCompleted 이벤트만 호출.

`Use(int useId)`로 명시적인 동작을 선택할 수도 있습니다. 새 동작을 추가할 때는 새 Layer에 상태와 오른팔 전용 클립을 추가하고, Driver Inspector의 `Use Motions`에 ID / State 이름 / Duration을 등록합니다. 기존 공통 Equip·Unequip이나 Hold Pose를 아이템별로 복제할 필요는 없습니다.

Driver는 네트워크 상속, Command, SyncVar, NetworkAnimator 호출, 소유권 판정을 포함하지 않습니다. 네트워크 담당자가 적절한 외부 이벤트에서 API를 호출하도록 연결하면 됩니다.

## 확인용 Scene과 재설치

`Assets/ReusableItemHold/Examples/ItemHold_Review.unity`를 열고 Play합니다. 화면 버튼으로 5개 Hold Type, Use, Unequip, Idle/Walk/Run을 확인합니다. 단순한 동전·막대·거울·나침반·책 대체 모양은 **확인 Scene용 샘플**이며 실제 아이템 리소스는 아닙니다.

기존 Player에 다시 설치해야 한다면 Project 창에서 해당 Prefab을 선택하고:

`Tools > Project Shaman > Add Reusable Item Hold to Selected Prefab`

이미 Layer/Driver/Socket이 있으면 중복 생성하지 않습니다. 기존 Base Layer를 새 Controller로 교체할 필요가 없습니다. `Examples/ShamanPlayer_ItemHold.controller`는 확인용 복사본입니다.

Unity 패키지는 `Assets/ReusableItemHold/`만 포함합니다. 기존 모델·텍스처·Mirror·기존 Controller 파일을 패키지로 덮어쓰지 않습니다. 현재 Project Shaman의 기존 에셋 참조를 사용하므로 빈 프로젝트용 독립 캐릭터 패키지는 아닙니다.

## 산출물과 검증

- `Korean_Exorcist_Waist_Fixed_ItemHold.blend`: 기존 메시 보존, 오른팔 Action 9개, 소켓.
- `Korean_Exorcist_Waist_Fixed.fbx`: 현재 프로젝트 FBX의 동일 사본. Unity에서는 별도의 `.anim` 클립을 사용합니다.
- `ProjectShaman_ReusableItemHold.unitypackage`: Mask, 클립 9개, 로컬 제어 스크립트, Layer 설치 도구, 확인용 Prefab/Controller/Scene.
- `preview.gif`, `hold-types.png`: 실제 Unity 재생/렌더.
- `validation.txt`, `project-preservation.json`, `blend-preservation.json`: 기능 검사와 보존 비교 결과.

Unity 6000.3.23f1의 검사 시퀀스에서:

- 빈손 Idle/Walk/Run의 오른팔·손가락과 기존 Base Layer의 회전 차이: **0도**.
- 아이템 장착 중 오른팔 외 관절과 기존 locomotion의 회전 차이: **0도**.
- Hold/장착/사용/해제 중 손목 회전 변화: 60fps 기준 프레임당 최대 약 **4.7도**.
- 해제 후 Layer Weight 0 복귀, 중간에 타입을 바꾸거나 해제를 요청한 경우도 확인.
- 소켓 자식 위치 오차 0. 팔과 몸 간격은 근사적인 몸통 축 검사와 렌더로 확인했습니다. 모든 캐릭터 자세·모든 실제 아이템 조합에 대한 정밀 충돌 보장은 아닙니다.

검사에서 Speed를 즉시 바꾼 순간에는 기존 locomotion 자체의 큰 손목 변화도 기록됩니다. 이 때 새 Layer는 Weight 0이며, 비교 캐릭터와 같은 결과입니다. 네트워크 동기화 테스트나 코드 변경은 이번 작업 범위에 포함하지 않았습니다.
