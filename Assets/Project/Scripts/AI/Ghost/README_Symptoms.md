# 빙의 NPC 증상 (Symptoms)

증상은 일과 "오염"(도구·행동·장소·시간 교란)과 **별개**다. 오염은 `CorruptionPlanner`/`CorruptionAxis`, 증상은 `PossessionSymptoms`/`SymptomRule`이 맡는다.

## 두 종류

| 구분 | 증상 | 나타나는 때 | 방식 |
|---|---|---|---|
| 행위 | Tremble, StareIntoVoid, Nonsense, NeckTwist, WaistBend, Grotesque | **이동 중 또는 휴식 중에만** (작업·대화·수면 중에는 안 나옴) | 한 번 재생되는 애니메이션 (+ Nonsense는 혼잣말) 후 쿨다운 |
| 외형 | NoShadow, PaleSkin, BlackEyes | 상시, 간헐적 (에피소드로 시작하고 스스로 끝남) | `AI_Core.AppearanceMask` → SyncVar로 모든 피어에 동기화 |

모든 증상은 목격된다. 본 주민은 기억하고, 대화에서 "요즘 이상한 걸 본 적 없소?" → "다른 건 또 본 거 없소?" 순으로 하나씩 묻는다.

## 음기 단계 (최소 음기, 기본값)

`AIBehaviourConfig` → Possession Symptoms → Symptom Rules에서 조정. 음기는 20(최소)~100(최대).

| 증상 | 최소 음기 | 발생 상황 | 아이템 반응 |
|---|---|---|---|
| Tremble (걷다가 몸 떨기) | 20 | 이동 | O |
| StareIntoVoid (허공 응시) | 20 | 휴식 | O |
| Nonsense (혼잣말) | 30 | 휴식 | O |
| PaleSkin (창백한 피부) | 30 | 외형 20~60초 | - |
| NeckTwist (목 꺾기) | 50 | 이동·휴식 | O |
| BlackEyes (눈 까매짐) | 50 | 외형 10~30초 | - |
| WaistBend (허리 꺾기) | 65 | 휴식 | - |
| NoShadow (그림자 없음) | 70 | 외형 15~40초 | - |
| Grotesque (괴상한 걸음) | 80 | 이동 | 폭력 구간에서 25% |

- 발생 확률은 음기에 비례한다 (`YinForMaxFrequency`로 정규화): 행위 10%→50%, 외형 3%→20% (판정 간격 6초마다).
- 아이템(소금·콩쑥·방울·징)에 놀란 숙주는 **약한 증상(아이템 반응 O)** 위주로 보인다. `ItemViolentYin` 이상에서만 25% 확률로 Grotesque.

## 새 증상을 추가하려면

1. `SymptomType`에 값 추가 (**뒤에 추가**, 순서 유지)
2. `SymptomRule.CreateDefaults`와 `AIBehaviourConfig`의 Symptom Rules에 행 추가 (최소 음기, 가중치, 상황)
3. `ObservationVerbs` + `ObservationService.SymptomVerb` 매핑, `AIDialogue.json`의 `verbs`에 증언 문장
4. 연출: 행위 = Animator 상태 `Symptom_<이름>` / 외형 = `AI_SymptomVisual`

## 행위 증상 애니메이션 제작 사양 (Gemini 전달용)

### 공통
- Humanoid 리깅, 제자리(in-place, Root Motion 없음), 30fps, 발은 바닥에 고정되지 않아도 됨 (상체만 쓰므로 하체 키는 넣지 않음)
- **상체 마스크 `AM_Villager_UpperBody` 레이어(`UpperAction`)에서 재생**된다. 걷는 중에도 하체 걷기 애니가 유지되므로 허리 위(척추·팔·목·머리)만 움직일 것
- 클립 길이 3~5초, 루프 없음. 클립이 끝나면 코드가 `Empty`로 페이드 아웃하므로(루프 클립은 최대 6초), 끝 포즈는 서 있는 기본 자세로 돌아오게 만들 것
- 네이밍: `Symptom_<이름>` (아래 표 그대로, 대소문자 포함)
- 분위기: 조선시대 마을 주민이 귀신에 씌어 보이는 이상 행동. 과장되지 않고 **섬뜩할 정도로 느리거나 어색하게**

| 클립 이름 | 동작 | 길이 | 사용 상황 |
|---|---|---|---|
| `Symptom_Tremble` | 어깨·팔·상체가 부들부들 잔떨림. 서서히 커졌다 잦아듦 | 3~4초 | 걷는 중 |
| `Symptom_StareIntoVoid` | 고개를 천천히 들어 허공의 한 점을 응시, 몸은 굳음. 눈 깜빡임 없이 정지 | 4~5초 | 휴식 중 |
| `Symptom_Nonsense` | 고개를 살짝 갸웃하며 입을 달싹이듯 머리가 미세하게 끄덕임. 손가락이 허공을 더듬음 | 4초 | 휴식 중 (말풍선 혼잣말과 함께) |
| `Symptom_NeckTwist` | 목이 천천히 한쪽으로 90도 가까이 꺾였다가 툭 하고 제자리 | 3~4초 | 이동·휴식 |
| `Symptom_WaistBend` | 허리가 뒤로(또는 옆으로) 부자연스럽게 접히다 천천히 일어남. 팔은 힘없이 늘어짐 | 4~5초 | 휴식 중 |
| `Symptom_Grotesque` | 걸으면서 상체·팔이 어색하게 비틀리고 관절이 반대로 꺾이는 듯한 경련. 가장 강한 증상 | 4~5초 | 이동 |

### Unity 적용 (10/31 적용 완료: `Animations/Npc/Clip/Symptom_*.anim` 6종, UpperAction 레이어)
1. 클립을 가져와 `AC_Villager`의 **UpperAction 레이어**에 위 이름 그대로 상태로 추가 (기본 상태 `Empty`에서 Any State/직접 전환은 필요 없음. 코드가 이름으로 `CrossFade` 한다)
2. 상태에서 `Empty`로 가는 전환은 만들지 않아도 된다 (클립이 한 번 재생되면 코드가 `Empty`로 페이드 아웃)
3. 상태가 없으면 콘솔에 "no Animator state 'Symptom_…'" 경고가 한 번만 뜨고 증상은 애니메이션 없이 목격 기록만 남는다

## 외형 증상 연출 (코드 구현됨, 에셋 규칙)

- `AI_SymptomVisual`이 런타임에 주민에게 붙는다.
- **그림자**: 몸 렌더러의 `shadowCastingMode`를 끈다 (성별 모델 공통).
- **피부**: 머티리얼 이름에 `Body` 또는 `Skin`이 있는 슬롯을 회색빛으로 (남 `Mat_Body`, 여 `M_NPC_Female_Skin`)
- **눈**: 머티리얼 이름에 `Eye`가 있는(눈썹 제외) 슬롯을 검게 (남 `Mat_Eye`, 여 `M_NPC_Female_Eyes`)
- 새 모델을 넣을 때는 위 머티리얼 이름 규칙을 지키고 눈은 **별도 머티리얼 슬롯**으로 분리해야 한다
- 색은 렌더러별 머티리얼 인스턴스에서 `_BaseColor`만 바꾸고 끝나면 원래 색으로 복구한다 (공유 머티리얼은 건드리지 않는다)
- 색 배율과 전환 시간은 `AI_SymptomVisual`의 인스펙터 값(Pale Tint, Black Eye Tint, Fade Seconds)

## 로그 키워드
`Symptom <이름>`, `Appearance <이름> on/off`, `no Animator state 'Symptom_`
