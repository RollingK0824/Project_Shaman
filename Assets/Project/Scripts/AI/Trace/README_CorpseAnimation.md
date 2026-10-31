# 시신 애니메이션 제작 요청서 (Gemini 전달용)

주민(NPC)이 밤에 귀신에게 죽은 뒤, 다음 날 아침 그 자리에 남는 **시신**에 쓸 애니메이션 2종을 만들어 주세요.
조선시대 마을 주민이고, 공포 게임이라 시신은 **섬뜩하지만 고어 없이** 표현합니다.

## 공통 사양
- Humanoid 리깅 (Unity Mecanim Humanoid 아바타에 리타깃되는 머슬 커브 `.anim`, 기존 `Symptom_*.anim`과 같은 형식)
- 30fps, Root Motion 없음, 제자리(in-place)
- **전신** 애니메이션 (마스크 없음, Base Layer에서 재생)
- 몸의 원점(엉덩이)은 바닥 위에 있어야 하고, 누운 상태에서 몸이 바닥 아래로 파고들지 않을 것
- 남녀 공용 (남성·여성 모델 모두 같은 Humanoid 아바타)

## 클립 목록

| 클립 이름 | 동작 | 길이 | 루프 |
|---|---|---|---|
| `Corpse_Die` | 선 자세에서 힘이 빠지며 무릎부터 꺾여 옆으로 쓰러지고, 마지막에 몸을 웅크린 채 옆으로 눕는다. 마지막 프레임은 `Corpse_Dead`의 포즈와 **완전히 같아야** 한다 | 1.5~2.5초 | 없음 |
| `Corpse_Dead` | 옆으로 누워 몸을 웅크린 자세 (무릎을 가슴 쪽으로 당기고, 팔은 몸 앞에 모으고, 고개는 살짝 숙임). 움직임 없는 **1프레임** | 1프레임 | 루프 |

- 웅크린 방향: 몸의 오른쪽이 바닥에 닿게 (오른쪽으로 누움)
- 얼굴은 바닥이 아니라 앞을 향하도록 (플레이어가 다가가면 얼굴이 보이게)
- 경직된 느낌: 손가락은 살짝 오그라들고, 팔다리는 자연스럽게 겹치되 몸을 관통하지 않을 것

## Unity 적용 (애니메이션 담당)
1. 클립을 `Assets/Project/Animations/Npc/Clip/`에 넣는다
2. 새 Animator Controller `AC_Corpse`를 만들어 Base Layer에 상태 2개를 추가한다
   - `Die` (클립 `Corpse_Die`) : **기본 상태**
   - `Dead` (클립 `Corpse_Dead`)
   - 전환 `Die → Dead`: Has Exit Time 켜기, Exit Time 1, Transition Duration 0
3. `Prefabs/Trace/Temp_Villager_Corpse`의 `AI_CorpseVisual` → Variants의 Male, Female 둘 다
   - `Pose Controller`에 `AC_Corpse` 지정
   - `Local Euler`를 **(0, 0, 0)** 으로 바꾼다 (지금은 클립이 없어서 -90으로 눕혀 둔 임시 값)
   - `Local Position` y도 0으로 (클립이 바닥 높이를 맞춤)

코드는 시신이 나타날 때 `Die`를 재생하고, 전환으로 `Dead`에 멈춰 있게 둔다. 컨트롤러가 없으면 지금처럼 서 있는 첫 프레임을 눕혀서 멈춘다.

## 가축 (참고, Gemini 작업 아님)
가축은 Animal pack에 죽는 클립이 이미 있다: `Cattle_Die.fbx`, `Chicken_Die.fbx`, `iron_age_pig_die_anim.FBX`.
각 가축마다 `Die`(이 클립) → `Dead`(같은 클립의 마지막 프레임, 1프레임 루프) 컨트롤러를 만들어 `Temp_Livestock_Corpse`의 Variants(Cattle, Chicken, Pig)에 지정하고 `Local Euler`를 0으로 바꾸면 된다.
