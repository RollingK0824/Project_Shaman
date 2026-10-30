# 주민 작업 애니메이션 제작 요청서 (Gemini 전달용)

조선시대 마을 주민(NPC)이 일과 중 하는 작업 동작 클립을 만들어 주세요.
기존 프로젝트 클립(Kevin Iglesias Human Animations)으로 대신할 수 없는 동작만 요청합니다.

## 공통 사양
- Humanoid 리깅 (Unity Mecanim Humanoid 아바타에 리타깃되는 머슬 커브 `.anim`, `Symptom_*.anim`과 같은 형식)
- 30fps, Root Motion 없음, 제자리(in-place), 발은 바닥에 고정
- **루프 클립** (Loop Time 켜기). 첫 프레임과 마지막 프레임 포즈가 같아 이음새가 없어야 함
- 남녀 공용, 손에 든 도구는 클립에 포함하지 않음 (도구는 코드가 오른손 소켓에 붙임: 오른손으로 쥐는 동작)
- 분위기: 과장 없이 담담하고 반복적인 생활 노동

## 클립 목록

| 우선 | 클립 이름 | 동작 | 길이 | 재생 레이어 | 쓰는 일과 |
|---|---|---|---|---|---|
| 1 | `Work_Sweep` | 허리를 약간 숙이고 두 손으로 긴 빗자루를 쥐고 좌우로 바닥을 쓴다 (오른손이 아래쪽) | 2~3초 | Action (전신) | 마당 쓸기, 주막 허드렛일 |
| 1 | `Work_Hawk` | 서서 한 손을 입가에 대거나 손짓으로 손님을 부른다. 몸을 좌우로 조금씩 돌리며 외치는 느낌 | 3~4초 | UpperAction (상체) | 호객 행위 |
| 2 | `Work_Bargain` | 서서 상대를 보며 손가락으로 값을 셈하거나 손을 내젓는 흥정 몸짓 | 3~4초 | UpperAction (상체) | 흥정하기, 주막 거래 |
| 2 | `Work_Sow` | 한 팔에 씨앗 바구니를 낀 채 다른 손으로 앞쪽에 씨앗을 흩뿌린다. 걷지 않고 제자리 | 2~3초 | Action (전신) | 씨 뿌리기 |
| 3 | `Work_Ledger` | 쪼그려 앉거나 선 채로 한 손에 장부, 다른 손에 붓을 쥐고 글씨를 쓴다. 가끔 고개를 들었다 다시 숙임 | 4초 | UpperAction (상체) | 장부 정리 |
| 3 | `Work_NetMend` | 바닥에 쪼그려 앉아 두 손으로 그물을 당기고 매듭을 묶는다 | 3~4초 | Action (전신) | 그물 손질 |

- 상체(UpperAction) 클립은 허리 위만 움직인다 (하체는 서 있는 기본 자세가 유지됨)
- 전신(Action) 클립은 하체 자세(숙임, 쪼그림)를 포함한다

## Unity 적용 (애니메이션 담당)
1. 클립을 `Assets/Project/Animations/Npc/Clip/`에 넣는다
2. `AC_Villager`의 위 표의 레이어에 **클립 이름과 같은 이름**의 상태를 추가한다 (예: Action 레이어에 `Work_Sweep`). 전환은 필요 없음 (코드가 이름으로 CrossFade 하고 작업이 끝나면 Empty로 돌림)
3. `Resources/Data/AIData_SampleTest.json`의 `actions`에서 animation 값을 바꾼다

| actionId | 지금 (임시) | 바꿀 값 |
|---|---|---|
| Sweep | Farming | Work_Sweep |
| Hawk | (없음, 서 있기) | Work_Hawk |
| Bargain | (없음, 서 있기) | Work_Bargain |
| Sow | Crouching | Work_Sow |
| Count | (없음, 서 있기) | Work_Ledger |
| Mend | Crouching | Work_NetMend |

## 기존 클립으로 연결한 동작 (요청 안 함)

| actionId | Animator 상태 | 원본 클립 |
|---|---|---|
| Till (밭 갈기) | Farming | HumanM@FarmingWithPlow01 |
| Chop (장작 패기·벌목·연료 모으기) | Chopping | HumanM@MiningOneHand01_R - Ground |
| Fish (낚시) | Fishing | HumanM@Fishing01 - Loop |
| Weed (잡초 뽑기) | Crouching | HumanF@Gathering02 |
| Stack·Carry·Arrange (쌓기·나르기·진열) | Gathering | HumanM@Gathering01 |
| Forge·Pound·Wash (망치질·다듬이질·빨래) | Hammering | HumanF@HammeringGround01_R - Loop |
