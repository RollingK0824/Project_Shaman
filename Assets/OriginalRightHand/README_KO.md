# 원본 캐릭터 양손 수정본

기준 모델은 `Korean_Exorcist_Waist_Fixed`입니다. 별도로 만들었던 `KoreanExorcist_ThirdPerson` 캐릭터는 사용하지 않습니다.

## 이번 수정

- 벌어지고 판처럼 돌출됐던 **양손 피부 부분의 면 구조를 국소적으로 다시 구성**했습니다. 단순히 포즈만 바꾼 파일은 아닙니다.
- 기존 67개 본을 유지하고, 새 손 표면의 관절 웨이트를 다시 배치했습니다.
- 손가락별 굽힘과 엄지 위치를 따로 조절했습니다.
- 손 외의 원본 얼굴, 헤어, 몸, 남색 의상과 장식은 유지했습니다. 원본 면 28,422개가 남아 있으며, 삭제한 기존 정점은 모두 양손 영역 안에 있습니다.
- 기존 Material과 Texture 파일은 유지했습니다. 수정한 손의 UV는 기존 텍스처의 피부색을 사용하며, 손톱·피부 주름을 새로 그린 사실적인 손 텍스처는 포함하지 않습니다.

## 파일

- `Korean_Exorcist_Waist_Fixed_Hands_Animated.blend`: 원본 캐릭터 + 수정된 양손 + Equip/Hold/Unequip/Use Action. Blender Action Editor에서 각 Action을 선택합니다. 기본 화면은 원래 휴식 자세입니다.
- `Korean_Exorcist_Waist_Fixed_Hands.fbx`: 수정된 원본 캐릭터의 Mesh와 기존 Rig. Unity용 애니메이션은 패키지의 `.anim` 파일을 사용합니다.
- `Korean_Exorcist_Waist_Fixed_Hands.fbm`: FBX의 기존 텍스처.
- `OriginalCharacter_Hands_Unity.unitypackage`: 수정 FBX, 애니메이션 4개, 오른팔 Avatar Mask, Layer 설치 도구, 원본 캐릭터 예제 Prefab, 확인용 Scene, Mirror 연동 코드, 방울.
- `preview.gif`: Unity에서 Idle/Walk/Run 위에 장착·사용·해제·아이템 교체를 재생한 미리보기.
- `validation.txt`, `hand-repair-report.json`: 실행 검사와 메시 변경 범위.

## Unity 적용

대상은 기존 `Project_Shaman` 프로젝트입니다. Mirror는 이미 설치된 것을 사용하며 패키지에 Mirror 자체를 넣지 않았습니다.

1. 프로젝트를 백업한 다음 `OriginalCharacter_Hands_Unity.unitypackage`를 Import합니다. 패키지는 기존 GUID를 유지한 `Assets/Project/Visuals/Models/Korean_Exorcist_Waist_Fixed.fbx`를 **양손 수정본으로 갱신**합니다. 모델 GUID는 `99d0ea0cbe190fc41b79503b7c379e5c`입니다. 원본 `ShamanPlayer.controller`는 패키지로 덮어쓰지 않습니다.
2. 먼저 `Assets/OriginalRightHand/Scenes/OriginalCharacter_HandReview.unity`를 열고 Play하여 손 모양과 동작을 확인합니다. 화면의 버튼으로 장착, 해제, 흔들기, Idle/Walk/Run을 바꿉니다.
3. 기존 캐릭터가 들어 있는 Player Prefab을 Project 창에서 선택하고 `Tools > Exorcist Original Rig > Add Right Hand Layer to Selected Player`를 실행합니다. 확인한 프로젝트에서는 `Assets/Project/Prefabs/Player/Player (1).prefab`이 해당 모델을 사용합니다. `Player.prefab`과 혼동하지 마세요.
4. 설치 도구는 현재 AnimatorController에 `Right Hand Items` Layer와 Mask를 추가합니다. 기존 Base Layer와 파라미터를 유지하고, 기존 NetworkAnimator가 있으면 해당 Animator를 연결합니다. 수정 전 Prefab과 Controller는 프로젝트 루트의 `RightHand_Backups`에 저장합니다.
5. 실제 인벤토리의 슬롯 선택/아이템 사용 코드에서 아래 API를 호출합니다. **현재 게임의 `PlayerItemController` 코드를 자동 수정하는 패키지는 아닙니다.**

```csharp
using Exorcist.OriginalRig;

// 소유 플레이어의 입력/인벤토리 코드에서 호출
OriginalRightArmNetwork hand = GetComponent<OriginalRightArmNetwork>();
hand.RequestItem(1);      // 방울 장착
hand.RequestItem(0);      // 해제
hand.RequestUse(true);    // 사용 시작
hand.RequestUse(false);   // 사용 종료
```

Mirror Command가 장착 ID와 시작 시간을 서버에 전달하고, SyncVar와 기존 NetworkAnimator로 다른 플레이어에게 표시합니다. 교체는 현재 아이템 내리기 → 소켓의 아이템 교체 → 새 아이템 들기 순서입니다. 빠른 전환 요청은 서버가 마지막 요청을 이어서 처리합니다.

이 코드는 **외형 표시 동기화**를 담당합니다. 게임의 아이템 소유권·획득·소모는 기존 서버 인벤토리에서 검증해야 합니다. `ServerCanPresent`에 서버 검증 함수를 연결하거나 서버에서 검증 후 `ServerSetItem(id)`를 호출할 수 있습니다.

## 아이템 추가

`Assets/OriginalRightHand/Animations/OriginalItems.asset`에 고유 ID, 표시 Prefab, 손 소켓 기준 Position/Rotation/Scale을 추가합니다. 방울은 ID 1, 미장착은 ID 0입니다. 본의 스케일이 100인 원본 모델에 맞춘 소켓 값이므로 Transform 값을 임의로 1로 정규화하지 마세요.

제공된 손가락 포즈는 방울/막대처럼 손잡이를 감싸는 자세입니다. 총, 부적, 큰 도구 등 잡는 방식이 다른 아이템은 해당 손가락 포즈와 클립을 별도로 작성해야 합니다. 모든 모양의 아이템을 자동으로 잡아 주는 IK 솔버는 아닙니다.

1인칭에서 사용하는 기존 별도 손 Prefab은 이 패키지가 자동 교체하지 않습니다. 이번 파일은 원본 전신 캐릭터의 양손과 3인칭 오른팔 동작 수정본입니다.

## 확인 범위

- Unity 6000.3.23f1 Play Mode: 장착·Hold·Use·해제·방울↔막대 교체, 기존 Idle/Walk/Run 재생.
- 원본 locomotion만 재생하는 비교 캐릭터와 오른팔 외 관절의 회전 차이 0도.
- 미장착 시 아이템 제거 및 오른팔 Layer Weight 0 복귀.
- Mirror 96.11.3의 별도 실행 프로세스 3개: 호스트/클라이언트의 장착 0/1/2와 사용 상태, 원격 Animator Layer, 중도 접속의 현재 아이템 복원 확인.
- 실제 게임 프로젝트에서 인벤토리·카메라·Steam 전송까지 연결한 통합 실행은 아직 수행하지 않았습니다. 실제 프로젝트 파일은 이 작업에서 직접 수정하지 않았습니다.

미리보기는 수정본의 실제 Unity 렌더입니다. 애니메이션·통신 검사 통과는 손 형태에 대한 미술적 최종 승인을 뜻하지 않습니다.
