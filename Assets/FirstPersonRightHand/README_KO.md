# 아이템을 교체해서 들 수 있는 Unity 1인칭 오른손

`Korean_Exorcist_Delivery.zip`의 오른손·소매 메시를 사용했습니다. 몸과 왼손은 포함하지 않았습니다. 사용자 참조에 맞춰 소매를 남색, 소맷단을 어두운 남색으로 변경했습니다. 팔·손목 2개 및 손가락 15개, 총 17개 본과 스킨 웨이트를 추가했습니다. 손목의 축을 따라 회전하도록 수정했고, 손목 경계를 보강하는 안쪽 소맷단을 추가했습니다.

## 바로 확인하기

1. `FirstPerson_RightHand_Unity.unitypackage`를 Unity 프로젝트에 Import합니다. 방울 테스트 아이템도 포함되어 있습니다.
2. `Assets/FirstPersonRightHand/Scenes/RightHandDemo.unity`를 열고 Play를 누릅니다.
3. `Bell`은 방울 장착, `Empty hand`는 아이템 해제입니다. `Cylinder`와 `Pinch`는 새 아이템을 연결할 때 사용할 **빈 자세 템플릿**입니다.
4. `Shake held item`을 켜면 방울의 개별 흔들림과 소리를 확인할 수 있습니다.

데모의 카메라는 제자리에 있습니다. 이동·마우스 시점·인벤토리·아이템 획득 시스템은 포함하지 않습니다.

## 기존 플레이어에 붙이기

`Assets/FirstPersonRightHand/Prefabs/FirstPersonRightHand.prefab`을 1인칭 카메라의 자식으로 둡니다. 프리팹에 저장된 위치·회전을 출발점으로 사용하고, 프로젝트의 카메라 FOV와 시야에 맞춰 조정하세요. 데모의 FOV는 60도, Near Clip Plane은 0.02입니다.

카메라가 회전하면 손과 장착 아이템이 같이 따라갑니다. 손 자체에는 자동 흔들기 동작이 없고, 데모 씬에서만 `RightHandDemo`가 미세한 움직임을 추가합니다.

## 다른 아이템 연결하기

1. Project 창에서 `Create > Exorcist > Right Hand Item Profile`을 선택하거나, `Profiles/Cylinder_Template` 또는 `Pinch_Template`을 복제합니다.
2. `Item Prefab`에 사용할 아이템 프리팹을 넣습니다.
3. `Local Position`, `Local Euler Angles`, `Local Scale`을 조정해 손바닥 장착점에 맞춥니다. Scale은 아이템 프리팹의 원래 스케일에 곱해집니다.
4. `Thumb / Index / Middle / Ring / Pinky`의 0~1 값으로 손가락마다 쥐는 정도를 조정합니다. 0은 펴기, 1은 설정된 최대 굽힘입니다.
5. `Thumb Opposition`으로 엄지가 손바닥 쪽으로 돌아오는 각도를 조절합니다.
6. 게임 시작 시 들 아이템은 `First Person Right Hand > Starting Item`에 지정합니다. 비워 두면 빈손으로 시작합니다.

런타임 장착 예시:

```csharp
using Exorcist.FirstPerson;
using UnityEngine;

public class YourInventory : MonoBehaviour
{
    public FirstPersonRightHand rightHand;
    public HandItemProfile selectedItem;

    public void EquipSelected() => rightHand.Equip(selectedItem);
    public void EmptyHand() => rightHand.Unequip();
}
```

교체할 때 이 컴포넌트가 만든 이전 아이템 인스턴스만 제거합니다. 프리팹 원본은 변경하지 않습니다. `Grip Transition Seconds`로 빈손↔쥐기 자세 전환 속도를 조절할 수 있습니다.

아이템의 모양을 자동으로 인식해 손가락을 맞추는 IK는 아닙니다. 새로운 아이템마다 위치와 손가락 값을 저장하는 방식입니다. 방울 이외의 사용자 아이템은 제공되지 않았으므로 실제 아이템별 맞춤은 해당 모델을 연결한 뒤 해야 합니다.

## 재질과 원본

- Unity 패키지는 Built-in Standard 재질을 기본으로 합니다.
- URP/HDRP에서는 `Tools > Exorcist Right Hand > Adapt Materials to Current Pipeline`을 실행합니다. 방울 재질은 `Tools > Ornate Bell Rattle > Adapt Materials to Current Pipeline`에서 별도로 변환합니다. 해당 파이프라인에서의 시각 검증은 별도로 필요합니다.
- `FirstPerson_RightHand.blend`에는 오른손·소매 메시, 17개 본, 웨이트와 내장 텍스처가 있습니다. Blender의 리그 편집용 파일입니다. Unity의 아이템 프로필과 실행 코드는 Unity 패키지에 있습니다.
- 전신 Humanoid 리그가 아닌 1인칭 오른손 전용 리그입니다. 범용 모션 캡처를 바로 재생하는 Avatar는 아닙니다.
- 손·소매는 8,616 triangles이고 안쪽 소맷단은 816 triangles입니다. 피부·남색 소매·소맷단을 별도 재질로 분리했습니다. 손의 변형을 부드럽게 하기 위해 손 메시를 세분화하고, Unity 메시의 UV·노멀 경계를 보존하기 위해 정점을 분리했습니다.
- 원본 손의 간단한 형상과 피부 텍스처를 유지했습니다. 손톱이나 피부 디테일을 새로 제작한 모델은 아닙니다.

벽에 가까이 갈 때의 가림 처리는 기존 게임의 뷰모델 전용 카메라/렌더 레이어 설정에 맞춰 연결하세요. 데모는 일반 카메라 하나로 렌더링합니다.

구현 참고: [Unity Mesh.bindposes](https://docs.unity3d.com/es/current/ScriptReference/Mesh-bindposes.html), [SkinnedMeshRenderer.bones](https://docs.unity3d.com/kr/2020.3/ScriptReference/SkinnedMeshRenderer-bones.html).
