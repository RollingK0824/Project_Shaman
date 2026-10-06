# Unity 방울 흔들림 모델

원본 `ornate+bell+rattle+3d+model.zip`의 42개 메시와 색상·노멀 텍스처를 사용했습니다. 방울 12개를 독립적으로 회전하도록 구성했으며, 인체용 뼈대와 스키닝을 제거한 메시를 사용합니다.

## 가져오기

1. Unity 프로젝트에서 `OrnateBellRattle_Unity.unitypackage`를 열고 Import합니다.
2. `Assets/BellRattle/Scenes/BellRattleDemo.unity`를 엽니다.
3. Play를 누르면 2초 흔들기 → 3초 멈추기를 반복합니다. Game 창의 체크박스로 흔들기를 끌 수 있습니다.
4. 실제 프로젝트에서는 `Assets/BellRattle/Prefabs/OrnateBellRattle.prefab`을 캐릭터의 손이나 잡기 대상 아래에 배치합니다. 손잡이 중심이 프리팹 원점입니다.
5. 프리팹 루트 또는 부모의 위치·회전을 움직이면 방울이 뒤따라 흔들립니다. 프리팹에는 자동 흔들기 스크립트가 없으며, 자동 흔들기는 데모 씬에서만 사용합니다.

## 재질

기본 패키지는 Built-in Render Pipeline의 Standard 재질입니다. URP/HDRP 프로젝트에서는 가져온 뒤 `Tools > Ornate Bell Rattle > Adapt Materials to Current Pipeline`을 실행하세요. 이 메뉴가 해당 파이프라인의 Lit 셰이더에 색상·노멀 텍스처를 다시 연결합니다. URP/HDRP에서의 시각 결과는 별도로 확인해야 합니다.

## 움직임과 소리 조절

각 `Bell_숫자`의 `Bell Swing` 컴포넌트에서 설정합니다.

| 항목 | 효과 |
|---|---|
| Max Angle | 최대 흔들림 각도. 기본 12도 |
| Frequency | 왕복 속도 |
| Damping | 높일수록 빨리 멈춤 |
| Response | 손의 움직임에 반응하는 강도 |
| Chimes | 재생할 AudioClip 목록 |
| Volume / Pitch | 방울별 소리 크기·높이 |
| Sound Speed Threshold | 소리를 낼 최소 흔들림 속도 |
| Minimum Sound Interval | 연속 재생 최소 간격 |

소리는 직접 합성한 테스트용 금속성 샘플 3개입니다. 실제 녹음한 방울 소리로 교체하면 더 자연스러워집니다. 씬에 활성 AudioListener가 있어야 들립니다. 흔들림이 중심을 통과하거나 설정 각도에 닿을 때 소리가 재생됩니다.

방울 메시 번호: **8, 12, 14, 15, 16, 17, 18, 20, 21, 26, 27, 29**. 다른 장식과 술은 원형을 유지하며 고정되어 있습니다.

## 구현 범위

- 실제 손 움직임에 반응하는 절차적 보조 애니메이션입니다. 녹화된 AnimationClip이 아닙니다.
- 스프링·감쇠 계산을 사용합니다. 방울끼리의 실제 충돌이나 방울 내부 알갱이의 물리 시뮬레이션은 포함하지 않습니다.
- 연결부 위쪽에 회전 중심을 추정해 배치했습니다. 원본은 장식 일부가 겹쳐 있는 모델이므로 각도를 크게 늘리면 관통이 보일 수 있습니다.
- 원본 텍스처와 42개 메시·재질을 유지한 초안입니다. 대량 배치나 모바일 최적화를 위한 재질 아틀라스 작업은 적용하지 않았습니다.
- 모델 높이는 약 45cm입니다. 루트의 균일 스케일로 크기를 조정할 수 있습니다. 비균일 스케일은 피하세요.
- 순간이동 등 큰 위치 변화는 보조 움직임을 초기화합니다. 필요하면 각 컴포넌트의 `ResetMotion()`을 호출하세요.

## 참고 API

[Unity PrefabUtility.SaveAsPrefabAsset](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityeditor/prefabutility/saveasprefabasset), [Unity AudioSource.PlayOneShot](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audiosource/playoneshot).
