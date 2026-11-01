using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "AIBehaviourConfig", menuName = "ProjectShaman/AI/Behaviour Config")]
    public class AIBehaviourConfig : ScriptableObject
    {
        [Tooltip("일과표가 반복되는 주기(일). 이 일수만큼 서로 다른 일과를 미리 짬")]
        [SerializeField] private int _cycleDays = 3;
        [Tooltip("일과 칸이 휴식 일과로 정해질 확률. 0이면 칸은 모두 작업이고, 휴식은 하루 일이 끝난 뒤 자유 시간에 함. 할 일이 없는 주민(아이·양반)은 그래도 휴식 일과를 받음 (C131)")]
        [SerializeField, Range(0f, 1f)] private float _restRatio = 0f;
        [Tooltip("하루에 하는 작업 일과 수")]
        [SerializeField] private int _routinesPerDay = 2;
        [Tooltip("일과 한 칸의 길이 = 하루 길이 × 이 비율. 작업 장소에 도착한 뒤부터 셈 (이동은 자유 시간에서 씀)")]
        [SerializeField, Range(0.05f, 0.5f)] private float _routineDayRatio = 0.2f;
        [Tooltip("아침 기상 시각을 이 시간(시) 안에서 주민마다 다르게 해 한꺼번에 나서지 않게 함")]
        [SerializeField] private int _maxStartDelayHours = 2;
        [Tooltip("귀가 출발이 가장 이른 시각(시). 주민마다 여기서 Home Leave Spread Hours 사이의 무작위 시각에 집으로 감")]
        [SerializeField] private int _homeLeaveEarliestHour = 16;
        [Tooltip("귀가 출발 시각을 퍼뜨리는 폭(시간)")]
        [SerializeField, Min(0f)] private float _homeLeaveSpreadHours = 2f;
        [Tooltip("밤까지 남은 시간이 일과 길이의 이 비율보다 적으면 그 일과는 시작하지 않고 건너뜀")]
        [SerializeField, Range(0f, 1f)] private float _minRemainingRatio = 0.5f;
        [Tooltip("일과표 탭 추정용: 일과 전 도구 정리·준비에 걸리는 시간(초)")]
        [SerializeField] private float _toolTidySecondsEstimate = 5f;
        [Tooltip("일과표·귀가 계산에 쓰는 걷기 속도 추정치(m/s)")]
        [SerializeField] private float _walkSpeedEstimate = 2.5f;
        [Tooltip("밤까지 집에 못 갈 것 같으면 걷기 속도의 이 배수로 뜀 (C121)")]
        [SerializeField, Min(1f)] private float _lateRunSpeedMultiplier = 1.8f;
        [Tooltip("모든 NavMesh 에이전트가 프레임당 함께 쓰는 길찾기 계산량 (유니티 기본 100). 너무 낮으면 귀가하는 주민이 길을 늦게 찾아 멈춰 있음")]
        [SerializeField, Min(100)] private int _pathfindingIterationsPerFrame = 500;

        [Header("Villager Attributes")]
        [Tooltip("주민 성별 비율 가중치: 남자")]
        [SerializeField, Min(0f)] private float _maleWeight = 55f;
        [Tooltip("주민 성별 비율 가중치: 여자")]
        [SerializeField, Min(0f)] private float _femaleWeight = 45f;
        [Tooltip("주민 신분 비율 가중치: 양반")]
        [SerializeField, Min(0f)] private float _yangbanWeight = 15f;
        [Tooltip("주민 신분 비율 가중치: 상민")]
        [SerializeField, Min(0f)] private float _sangminWeight = 50f;
        [Tooltip("주민 신분 비율 가중치: 천민")]
        [SerializeField, Min(0f)] private float _cheonminWeight = 35f;
        [Tooltip("주민 나이 비율 가중치: 노인")]
        [SerializeField, Min(0f)] private float _elderWeight = 20f;
        [Tooltip("주민 나이 비율 가중치: 어른")]
        [SerializeField, Min(0f)] private float _adultWeight = 55f;
        [Tooltip("주민 나이 비율 가중치: 아이")]
        [SerializeField, Min(0f)] private float _childWeight = 25f;

        [Header("Tool")]
        [Tooltip("주민이 지나가다 도구를 보고 기억하는 거리(m). 정면 시야각 안이고 벽에 가리지 않아야 함")]
        [SerializeField] private float _toolSightRadius = 8f;
        [Tooltip("창고 밖에 놓인 도구(귀신이 숨긴 것 등)는 이 거리(m)까지, 정면이 아니어도 보이면 기억함")]
        [SerializeField] private float _toolOutOfPlaceNoticeRadius = 15f;
        [Tooltip("주민이 주변 도구를 훑어보는 간격(초)")]
        [SerializeField] private float _toolSightInterval = 1f;
        [Tooltip("도구를 집을 수 있는 거리(m)")]
        [SerializeField] private float _toolPickupDistance = 1.5f;
        [Tooltip("도구 종류마다 기억하는 목격 수 (최근 것부터)")]
        [SerializeField] private int _toolSightingsPerType = 2;
        [Tooltip("도구 찾기에서 확인할 장소 수(창고·기억 장소·들은 단서). 모두 비면 포기하고 찾기 의뢰를 냄")]
        [SerializeField, Min(1)] private int _toolSearchSpots = 3;
        [Tooltip("도구를 찾으러 가는 길에 이 거리(m) 안에서 오늘 아직 안 물어본 주민을 만나면 멈춰서 물어봄")]
        [SerializeField, Min(0f)] private float _toolAskOnTheWayRadius = 6f;
        [Tooltip("여유 있을 때 다 쓴 도구를 창고에 갖다 두러 가는 최대 거리(m). 더 멀면 들고 다님")]
        [SerializeField] private float _tidyRelaxedMaxDistance = 15f;
        [Tooltip("바쁠 때 다 쓴 도구를 창고에 갖다 두러 가는 최대 거리(m). 더 멀면 들고 집에 감")]
        [SerializeField] private float _tidyRushedMaxDistance = 35f;

        [Header("Ghost / Corruption")]
        [Tooltip("게임 시작 때 귀신의 음기")]
        [SerializeField] private float _initialYin = 30f;
        [Tooltip("증상·일과 오염 확률이 최대치(…AtMaxYin)에 도달하는 음기")]
        [SerializeField] private float _yinForMaxFrequency = 100f;
        [Tooltip("음기 최솟값. 사냥으로 음기를 다 써도 아침에 이 값까지 회복")]
        [SerializeField] private float _minimumYin = 20f;
        [Tooltip("음기 최댓값")]
        [SerializeField] private float _maxYin = 100f;
        [Tooltip("빙의 주민의 일과가 오염될(엉뚱한 도구·행동·장소·시간) 확률: 음기 0일 때")]
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtZeroYin = 0.2f;
        [Tooltip("빙의 주민의 일과가 오염될 확률: 음기 최대일 때 (사이는 비례)")]
        [SerializeField, Range(0f, 1f)] private float _corruptionChanceAtMaxYin = 0.8f;
        [Tooltip("하루 마지막 일과 칸의 오염 확률 배수")]
        [SerializeField, Range(0f, 1f)] private float _lastSlotChanceMultiplier = 0.5f;
        [Tooltip("빙의 주민이 하루에 최소로 보이는 일과 오염 수")]
        [SerializeField] private int _minCorruptionsPerDay = 1;
        [Tooltip("시간 오염(늦게 시작) 때 늦어지는 정도: 일과 길이의 최소 비율")]
        [SerializeField, Range(0f, 1f)] private float _timeDelayMinRatio = 0.15f;
        [Tooltip("시간 오염(늦게 시작) 때 늦어지는 정도: 일과 길이의 최대 비율")]
        [SerializeField, Range(0f, 1f)] private float _timeDelayMaxRatio = 0.25f;
        [Header("Possession Symptoms")]
        [Tooltip("증상별 최소 음기, 가중치, 나타나는 상황, 외형 증상 지속 시간. 행위 증상은 걷거나 쉴 때만, 외형 증상(그림자·피부·눈)은 저절로 나타났다 사라짐")]
        [SerializeField] private List<SymptomRule> _symptomRules = SymptomRule.CreateDefaults();
        [Tooltip("빙의 주민의 증상을 굴리는 간격(초) (행위·외형 공통)")]
        [SerializeField, Min(0.5f)] private float _symptomCheckInterval = 6f;
        [Tooltip("한 번 굴릴 때 행위 증상이 나올 확률: 음기 0일 때")]
        [SerializeField, Range(0f, 1f)] private float _behaviourSymptomChanceAtZeroYin = 0.1f;
        [Tooltip("한 번 굴릴 때 행위 증상이 나올 확률: 음기 최대일 때")]
        [SerializeField, Range(0f, 1f)] private float _behaviourSymptomChanceAtMaxYin = 0.5f;
        [Tooltip("행위 증상 뒤 다른 행위 증상이 나오지 않는 시간(초)")]
        [SerializeField, Min(0f)] private float _behaviourSymptomCooldown = 12f;
        [Tooltip("한 번 굴릴 때 외형 증상이 새로 시작될 확률: 음기 0일 때 (음기가 허용하는 증상마다)")]
        [SerializeField, Range(0f, 1f)] private float _appearanceSymptomChanceAtZeroYin = 0.03f;
        [Tooltip("한 번 굴릴 때 외형 증상이 새로 시작될 확률: 음기 최대일 때")]
        [SerializeField, Range(0f, 1f)] private float _appearanceSymptomChanceAtMaxYin = 0.2f;
        [Tooltip("외형 증상이 끝난 뒤 같은 증상이 다시 나오지 않는 시간(초)")]
        [SerializeField, Min(0f)] private float _appearanceSymptomRest = 20f;
        [Tooltip("숙주가 아이템에 놀랄 때 음기가 격한 반응 기준 이상이면, 약한 증상 대신 무서운 증상(뒤틀림)이 나올 확률")]
        [SerializeField, Range(0f, 1f)] private float _itemViolentSymptomChance = 0.25f;
        [Tooltip("이 음기 이상이면 빙의 주민이 도구 없이 빈손으로 도구 일을 하는 증상이 나올 수 있음")]
        [SerializeField] private float _emptyHandToolYinThreshold = 80f;
        [Tooltip("빈손 도구 일 증상이 나올 확률 (음기가 기준 이상일 때)")]
        [SerializeField, Range(0f, 1f)] private float _emptyHandToolChance = 0.25f;
        [Tooltip("도구 오염(엉뚱한 도구를 씀) 때 대신 쓸 도구를 찾는 거리(m)")]
        [SerializeField] private float _toolCorruptionSearchRadius = 25f;

        [Header("Night Hunt")]
        [Tooltip("사냥 토큰을 받은 귀신의 음기 단계별 사냥 수와 플레이어 추격 여부. 토큰을 받으면 음기를 모두 쓰고 그 양으로 단계를 정함")]
        [SerializeField] private List<HuntTier> _huntTiers = new List<HuntTier>
        {
            new HuntTier(0f, 0, false),
            new HuntTier(20f, 1, false),
            new HuntTier(40f, 2, false),
            new HuntTier(60f, 3, false),
            new HuntTier(80f, 4, true)
        };
        [Tooltip("귀신 사냥 명령 하나의 제한 시간(초)")]
        [SerializeField] private float _ghostHuntTimeoutSeconds = 90f;
        [Tooltip("귀신 활동 반경 최솟값(m) (음기 0)")]
        [SerializeField] private float _huntRadiusMin = 10f;
        [Tooltip("귀신 활동 반경 최댓값(m) (음기 최대)")]
        [SerializeField] private float _huntRadiusMax = 60f;
        [Tooltip("밤이 된 뒤 귀신 스폰을 확인하기 시작하는 지연(초). 실제 스폰은 숙주가 누운 뒤")]
        [SerializeField] private float _ghostSpawnDelaySeconds = 3f;

        [Header("Stress")]
        [Tooltip("도구가 있을 줄 안 곳에 없을 때 주민 스트레스")]
        [SerializeField] private float _toolMissingStress = 5f;
        [Tooltip("도구 찾기를 포기할 때 주민 스트레스")]
        [SerializeField] private float _toolSearchFailedStress = 10f;

        [Header("Night Action")]
        [Tooltip("밤 사건(사냥 등)을 근처 주민이 알아채는 거리(m)")]
        [SerializeField] private float _nightWitnessRadius = 15f;
        [Tooltip("집 지점에서 이 거리(m) 안이면 집에 있는 것으로 봄 (귀가 도착·가위눌림 대상)")]
        [SerializeField] private float _sleepParalysisHomeDistance = 4f;
        [Tooltip("밤에 밖에 있던 주민이 귀신을 볼 때 스트레스. 귀신마다 하룻밤 한 번 (C122)")]
        [SerializeField, Min(0f)] private float _ghostSightingStress = 25f;
        [Tooltip("밤에 밖에 있는 주민이 귀신을 알아보는 거리(m) (정면, 벽에 가리지 않을 때)")]
        [SerializeField, Min(1f)] private float _ghostSightingRadius = 20f;
        [Tooltip("귀신이 도구를 숨길 때 원래 자리에서 떨어뜨리는 최소 거리(m)")]
        [SerializeField] private float _ghostHideMinDistance = 8f;
        [Tooltip("귀신이 도구를 숨길 때 원래 자리에서 떨어뜨리는 최대 거리(m)")]
        [SerializeField] private float _ghostHideMaxDistance = 20f;
        [Tooltip("귀신 밤 행동 사이 쉬는 시간 최솟값(초)")]
        [SerializeField] private float _ghostActionCooldownMin = 15f;
        [Tooltip("귀신 밤 행동 사이 쉬는 시간 최댓값(초)")]
        [SerializeField] private float _ghostActionCooldownMax = 30f;
        [Tooltip("귀신 행동이 실패하거나 대상이 없을 때 다시 고르기까지 시간(초)")]
        [SerializeField] private float _ghostCommandRetrySeconds = 1f;
        [Tooltip("귀신 행동 명령 하나의 제한 시간(초). 대상까지 걷는 시간은 따로 더함")]
        [SerializeField] private float _ghostCommandTimeoutSeconds = 30f;
        [Tooltip("귀신 행동 채널링(게이지)의 최소 시간(초)")]
        [SerializeField] private float _ghostMinChannelSeconds = 1.5f;

        [Header("Ghost Channel Items")]
        [Tooltip("소금에 맞아 행동이 끊길 때 귀신이 얻는 음기 (화가 나 대담해짐)")]
        [SerializeField] private float _ghostSaltAngerYin = 5f;
        [Tooltip("소금에 맞은 귀신이 순간이동하는 곳: 던진 플레이어에게서 최소 이 거리(m), 자기 반경 안")]
        [SerializeField] private float _ghostSaltTeleportMinDistance = 15f;
        [Tooltip("방울 소리에 귀신이 빛나 보이는 시간(초)")]
        [SerializeField] private float _ghostBellRevealSeconds = 6f;
        [Tooltip("귀신이 같은 아이템에 다시 반응하지 않는 시간(초)")]
        [SerializeField] private ItemReactionCooldowns _ghostItemCooldowns = new ItemReactionCooldowns(2f, 3f, 6f, 10f);

        [Header("Ghost Idle")]
        [Tooltip("귀신이 플레이어를 따라다닐 때 유지하는 최소 거리(m)")]
        [SerializeField] private float _ghostStalkMinDistance = 3f;
        [Tooltip("귀신이 플레이어를 따라다닐 때 유지하는 최대 거리(m)")]
        [SerializeField] private float _ghostStalkMaxDistance = 6f;
        [Tooltip("귀신이 플레이어를 따라갈 때 속도(m/s)")]
        [SerializeField] private float _ghostStalkSpeed = 3f;
        [Tooltip("귀신이 플레이어 가까이서 슬금슬금 다가가는 속도(m/s)")]
        [SerializeField] private float _ghostStalkCreepSpeed = 1f;
        [Tooltip("귀신 존재 반경 안의 플레이어가 받는 초당 스트레스 (따라다니든 아니든). 결계·스트레스 부적을 무시하고 그대로 적용")]
        [SerializeField] private float _ghostStalkStressPerSecond = 2f;
        [Tooltip("귀신 존재 반경(m): 음기 0일 때. 따라다닐 최대 거리보다 작아지지 않음")]
        [SerializeField] private float _ghostStressRadiusMin = 7f;
        [Tooltip("귀신 존재 반경(m): 음기 최대일 때")]
        [SerializeField] private float _ghostStressRadiusMax = 12f;
        [Tooltip("할 일이 없을 때 귀신이 배회하는 속도(m/s)")]
        [SerializeField] private float _ghostWanderSpeed = 1.5f;
        [Tooltip("배회 중 멈춰 있는 시간 최솟값(초)")]
        [SerializeField] private float _ghostWanderPauseMin = 2f;
        [Tooltip("배회 중 멈춰 있는 시간 최댓값(초)")]
        [SerializeField] private float _ghostWanderPauseMax = 5f;

        [Header("Ghost Player Chase")]
        [Tooltip("추격 단계 귀신이 플레이어를 공격 대상으로 알아채는 거리(m)")]
        [SerializeField] private float _ghostPlayerDetectDistance = 15f;
        [Tooltip("귀신이 플레이어를 다시 공격하기까지 시간(초)")]
        [SerializeField] private float _ghostPlayerAttackCooldown = 10f;
        [Tooltip("플레이어 공격 휘두르기 시간(초). 끝날 때 타격 (채널링 게이지 없음)")]
        [SerializeField] private float _ghostPlayerAttackSwingSeconds = 0.6f;
        [Tooltip("주민이 눕는 클립(Sleep_LieDown) 길이(초). 숙주가 다 누운 뒤에 귀신이 나옴")]
        [SerializeField] private float _sleepLieDownSeconds = 2.6f;

        [Header("Interaction")]
        [Tooltip("주민이 주변 상호작용 대상(플레이어·물건)을 알아채는 거리(m)")]
        [SerializeField] private float _interactionNoticeRadius = 10f;
        [Tooltip("주변 상호작용 대상을 훑는 간격(초)")]
        [SerializeField] private float _interactionScanInterval = 0.5f;
        [Tooltip("흘끗 쳐다보는 시간(초)")]
        [SerializeField] private float _glanceDuration = 1.5f;
        [Tooltip("쉬는 중 다른 상호작용에 끌리지 않는 정도(0~100, 높을수록 안 끌림)")]
        [SerializeField, Range(0, 100)] private int _restResistance = 20;
        [Tooltip("대화 중 다른 상호작용에 끌리지 않는 정도(0~100)")]
        [SerializeField, Range(0, 100)] private int _conversationResistance = 30;
        [Tooltip("이동 중 다른 상호작용에 끌리지 않는 정도(0~100)")]
        [SerializeField, Range(0, 100)] private int _movingResistance = 40;
        [Tooltip("도구를 찾는 중 다른 상호작용에 끌리지 않는 정도(0~100)")]
        [SerializeField, Range(0, 100)] private int _toolSearchResistance = 50;
        [Tooltip("작업 중 다른 상호작용에 끌리지 않는 정도(0~100)")]
        [SerializeField, Range(0, 100)] private int _workResistance = 60;
        [Tooltip("귀가 중 다른 상호작용에 끌리지 않는 정도(0~100)")]
        [SerializeField, Range(0, 100)] private int _returningHomeResistance = 70;

        [Header("Conversation")]
        [Tooltip("주민끼리 대화를 시작할 수 있는 거리(m)")]
        [SerializeField] private float _conversationRadius = 5f;
        [Tooltip("주민끼리 대화할지 확인하는 간격(초)")]
        [SerializeField] private float _conversationCheckInterval = 2f;
        [Tooltip("확인할 때 대화를 시작할 확률")]
        [SerializeField, Range(0f, 1f)] private float _conversationChance = 0.3f;
        [Tooltip("일과 한 칸에서 하는 주민끼리 대화 최대 수")]
        [SerializeField] private int _conversationsPerRoutine = 2;
        [Tooltip("주민끼리 대화 길이 최솟값(초)")]
        [SerializeField] private float _conversationDurationMin = 8f;
        [Tooltip("주민끼리 대화 길이 최댓값(초)")]
        [SerializeField] private float _conversationDurationMax = 12f;
        [Tooltip("한 대화에 끼는 최대 인원")]
        [SerializeField] private int _conversationGroupMax = 4;
        [Tooltip("같은 주민과 다시 대화하기까지 시간(초)")]
        [SerializeField] private float _conversationPartnerCooldown = 60f;
        [Tooltip("플레이어 대화·주민 혼잣말이 들리는 거리(m)")]
        [SerializeField, Min(0f)] private float _hearingDistance = 12f;

        [Header("Ghost Trace")]
        [Tooltip("치우지 않은 귀신 흔적 하나가 하루마다 주는 주민 스트레스")]
        [SerializeField, Min(0f)] private float _traceNeglectStressPerDay = 0.5f;
        [Tooltip("방치된 흔적 하나가 줄 수 있는 스트레스 최대 누적")]
        [SerializeField, Min(1f)] private float _traceNeglectStressMax = 3f;

        [Header("Memory")]
        [Tooltip("주민이 기억을 유지하는 일수")]
        [SerializeField, Min(1)] private int _memoryDays = 2;
        [Tooltip("기억 종류마다 남기는 최대 개수")]
        [SerializeField, Min(1)] private int _memoryEntriesPerKind = 10;

        [Header("Villager Stress")]
        [Tooltip("주민 스트레스 최댓값")]
        [SerializeField, Min(1f)] private float _villagerStressMax = 100f;
        [Tooltip("이 스트레스 이상이면 불안 상태 (혼잣말·굿 의뢰 등)")]
        [SerializeField, Min(0f)] private float _villagerStressAnxious = 50f;
        [Tooltip("이 스트레스에 닿으면 한계 상태")]
        [SerializeField, Min(0f)] private float _villagerStressLimit = 100f;
        [Tooltip("아침마다 줄어드는 주민 스트레스")]
        [SerializeField, Min(0f)] private float _villagerStressMorningDecay = 5f;

        [Header("Player Item Reaction")]
        [Tooltip("소금·콩쑥·방울·징에 놀란 주민이 받는 스트레스")]
        [SerializeField, Min(0f)] private float _itemStartleStress = 3f;
        [Tooltip("방울을 울린 플레이어에게서 이 거리(m) 안의 주민만 놀람. 귀신이 빛나는 거리는 더 멀리 감 (C111)")]
        [SerializeField, Min(0f)] private float _bellStartleRadius = 5f;
        [Tooltip("주민이 같은 아이템에 다시 반응하지 않는 시간(초). 쿨다운 없는 아이템을 연타해 스트레스를 쌓거나 숙주를 마음대로 드러내지 못하게 함")]
        [SerializeField] private ItemReactionCooldowns _villagerItemCooldowns = new ItemReactionCooldowns(6f, 6f, 6f, 6f);
        [Tooltip("숙주의 음기가 이 값 이상이면 아이템에 놀랄 때 몸에 귀신 기운이 드러남(떨림 등 약한 증상). 미만이면 일반 주민처럼 놀라기만 함")]
        [SerializeField, Min(0f)] private float _itemReactionYin = 40f;
        [Tooltip("숙주의 음기가 이 값 이상이면 아이템 반응이 격해짐(뒤틀림 등 무서운 증상, Item Violent Symptom Chance 확률)")]
        [SerializeField, Min(0f)] private float _itemViolentYin = 80f;

        [Header("Vision")]
        [Tooltip("주민 시야 반각(도). 정면에서 이 각도 안만 봄")]
        [SerializeField, Range(10f, 180f)] private float _visionHalfAngle = 60f;
        [Tooltip("이 거리(m) 안은 시야각과 상관없이 알아챔")]
        [SerializeField, Min(0f)] private float _visionNearRadius = 2f;
        [Tooltip("시야를 재는 눈 높이(m)")]
        [SerializeField, Min(0f)] private float _visionEyeHeight = 1.6f;
        [Tooltip("시야를 가리는 레이어(건물·벽·지형). 비우면 가림 검사 안 함")]
        [SerializeField] private LayerMask _visionObstacleMask = 0;

        [Header("Observation")]
        [Tooltip("주민이 다른 주민의 모습·증상을 목격하는 거리(m)")]
        [SerializeField, Min(1f)] private float _observationRadius = 10f;
        [Tooltip("목격 검사 간격(초)")]
        [SerializeField, Min(0.1f)] private float _observationInterval = 1f;
        [Tooltip("목격 증언에 담기는 인상착의 수 최솟값")]
        [SerializeField, Range(1, 3)] private int _witnessTraitMin = 2;
        [Tooltip("목격 증언에 담기는 인상착의 수 최댓값")]
        [SerializeField, Range(1, 3)] private int _witnessTraitMax = 2;
        [Tooltip("불안한 주민이 혼잣말을 할지 확인하는 간격(초)")]
        [SerializeField, Min(5f)] private float _anxiousMutterInterval = 30f;
        [Tooltip("확인할 때 혼잣말을 할 확률")]
        [SerializeField, Range(0f, 1f)] private float _anxiousMutterChance = 0.35f;

        [Tooltip("귀가 길에 이웃집을 들여다볼 때 머무는 시간(초)")]
        [SerializeField, Min(0f)] private float _neighbourVisitLingerSeconds = 3f;

        [Header("Dialogue")]
        [Tooltip("주민끼리 대화할 때 들은 소문을 전할 확률")]
        [SerializeField, Range(0f, 1f)] private float _rumorChance = 0.5f;
        [Tooltip("소문이 전해지는 최대 단계 (직접 본 사람에서 몇 다리까지)")]
        [SerializeField, Range(1, 4)] private int _rumorMaxHops = 2;
        [Tooltip("일과마다 '같이 있던 사람'(알리바이)으로 기억하는 주민 수")]
        [SerializeField, Range(1, 5)] private int _alibiCompanionsPerRoutine = 3;

        [Header("Villager Request")]
        [Tooltip("주민 요청 종류와 그 요청으로 나오는 퀘스트 ID")]
        [SerializeField] private List<RequestQuestEntry> _requestQuests = new List<RequestQuestEntry>
        {
            new RequestQuestEntry("FindTool", "q_find_tool"),
            new RequestQuestEntry("Trace", "q_clear_trace"),
            new RequestQuestEntry("TraceCleanse", "q_cleanse_trace"),
            new RequestQuestEntry("CheckOn", "q_check_on"),
            new RequestQuestEntry("Gut", "q_gut")
        };
        [Tooltip("요청 퀘스트를 해결하면 줄어드는 주민 스트레스")]
        [SerializeField, Min(0f)] private float _requestCompletedStressRelief = 30f;
        [Tooltip("요청 퀘스트가 실패하면 주민이 받는 스트레스")]
        [SerializeField, Min(0f)] private float _requestFailedStress = 10f;

        [Header("Debug")]
        [SerializeField, Tooltip("테스트용: 모든 클라에 빙의 주민을 알려 F1 테스트 패널에서 켰을 때만 노란 외곽선으로 표시(기본 꺼짐). 클라가 읽을 수 있으니 빌드에서는 끌 것")]
        private bool _showPossessedMaterial = true;
        [Tooltip("빙의 주민 표시(테스트용 노란 외곽선) 재질")]
        [SerializeField] private Material _possessedDebugMaterial;

        public int CycleDays => _cycleDays;

        public float WeightOf(VillagerGender gender)
        {
            return gender == VillagerGender.Male ? _maleWeight : _femaleWeight;
        }

        public float WeightOf(VillagerSocialClass socialClass)
        {
            switch (socialClass)
            {
                case VillagerSocialClass.Yangban: return _yangbanWeight;
                case VillagerSocialClass.Sangmin: return _sangminWeight;
                default: return _cheonminWeight;
            }
        }

        public float WeightOf(VillagerAgeGroup ageGroup)
        {
            switch (ageGroup)
            {
                case VillagerAgeGroup.Elder: return _elderWeight;
                case VillagerAgeGroup.Adult: return _adultWeight;
                default: return _childWeight;
            }
        }
        public float RestRatio => _restRatio;
        public int RoutinesPerDay => Mathf.Max(1, _routinesPerDay);
        public float RoutineSeconds => TimeManager.DAY_DURATION * _routineDayRatio;
        public float MaxStartDelaySeconds => DayClock.HoursToSeconds(Mathf.Max(0, _maxStartDelayHours));
        public int HomeLeaveEarliestHour => Mathf.Clamp(_homeLeaveEarliestHour, DayClock.DAY_START_HOUR, DayClock.DAY_END_HOUR);
        public float HomeLeaveSpreadSeconds => DayClock.HoursToSeconds(_homeLeaveSpreadHours);
        public float MinRemainingRatio => _minRemainingRatio;
        public float ToolTidySecondsEstimate => _toolTidySecondsEstimate;
        public float WalkSpeedEstimate => _walkSpeedEstimate;
        public float LateRunSpeedMultiplier => _lateRunSpeedMultiplier;
        public int PathfindingIterationsPerFrame => _pathfindingIterationsPerFrame;
        public float ToolSightRadius => _toolSightRadius;
        public float ToolOutOfPlaceNoticeRadius => Mathf.Max(0f, _toolOutOfPlaceNoticeRadius);
        public float ToolSightInterval => _toolSightInterval;
        public float ToolPickupDistance => _toolPickupDistance;
        public int ToolSightingsPerType => _toolSightingsPerType;
        public int ToolSearchSpots => Mathf.Max(1, _toolSearchSpots);
        public float ToolAskOnTheWayRadius => Mathf.Max(0f, _toolAskOnTheWayRadius);
        public float TidyRelaxedMaxDistance => _tidyRelaxedMaxDistance;
        public float TidyRushedMaxDistance => _tidyRushedMaxDistance;
        public float InitialYin => _initialYin;
        public float YinForMaxFrequency => _yinForMaxFrequency;
        public float MinimumYin => _minimumYin;
        public float MaxYin => Mathf.Max(_minimumYin, _maxYin);
        public float CorruptionChanceAtZeroYin => _corruptionChanceAtZeroYin;
        public float CorruptionChanceAtMaxYin => _corruptionChanceAtMaxYin;
        public float LastSlotChanceMultiplier => _lastSlotChanceMultiplier;
        public int MinCorruptionsPerDay => _minCorruptionsPerDay;
        public float TimeDelayMinRatio => _timeDelayMinRatio;
        public float TimeDelayMaxRatio => _timeDelayMaxRatio;
        public IReadOnlyList<SymptomRule> SymptomRules => _symptomRules;
        public float SymptomCheckInterval => _symptomCheckInterval;
        public float BehaviourSymptomChanceAtZeroYin => _behaviourSymptomChanceAtZeroYin;
        public float BehaviourSymptomChanceAtMaxYin => _behaviourSymptomChanceAtMaxYin;
        public float BehaviourSymptomCooldown => _behaviourSymptomCooldown;
        public float AppearanceSymptomChanceAtZeroYin => _appearanceSymptomChanceAtZeroYin;
        public float AppearanceSymptomChanceAtMaxYin => _appearanceSymptomChanceAtMaxYin;
        public float AppearanceSymptomRest => _appearanceSymptomRest;
        public float ItemViolentSymptomChance => _itemViolentSymptomChance;
        public float EmptyHandToolYinThreshold => _emptyHandToolYinThreshold;
        public float EmptyHandToolChance => _emptyHandToolChance;
        public float ToolCorruptionSearchRadius => _toolCorruptionSearchRadius;
        public IReadOnlyList<HuntTier> HuntTiers => _huntTiers;
        public float GhostHuntTimeoutSeconds => Mathf.Max(_ghostCommandTimeoutSeconds, _ghostHuntTimeoutSeconds);
        public float HuntRadiusMin => _huntRadiusMin;
        public float HuntRadiusMax => _huntRadiusMax;
        public float GhostSpawnDelaySeconds => _ghostSpawnDelaySeconds;
        public float ToolMissingStress => _toolMissingStress;
        public float ToolSearchFailedStress => _toolSearchFailedStress;
        public float NightWitnessRadius => _nightWitnessRadius;
        public float SleepParalysisHomeDistance => _sleepParalysisHomeDistance;
        public float GhostSightingStress => _ghostSightingStress;
        public float GhostSightingRadius => _ghostSightingRadius;
        public float GhostHideMinDistance => _ghostHideMinDistance;
        public float GhostHideMaxDistance => Mathf.Max(_ghostHideMinDistance, _ghostHideMaxDistance);
        public float GhostActionCooldownMin => Mathf.Max(0f, _ghostActionCooldownMin);
        public float GhostActionCooldownMax => Mathf.Max(GhostActionCooldownMin, _ghostActionCooldownMax);
        public float GhostCommandRetrySeconds => _ghostCommandRetrySeconds;
        public float GhostCommandTimeoutSeconds => _ghostCommandTimeoutSeconds;
        public float GhostMinChannelSeconds => Mathf.Max(0f, _ghostMinChannelSeconds);
        public float GhostSaltAngerYin => Mathf.Max(0f, _ghostSaltAngerYin);
        public float GhostSaltTeleportMinDistance => Mathf.Max(0f, _ghostSaltTeleportMinDistance);
        public ItemReactionCooldowns GhostItemCooldowns => _ghostItemCooldowns;
        public float GhostBellRevealSeconds => Mathf.Max(0f, _ghostBellRevealSeconds);
        public float GhostStalkMinDistance => Mathf.Max(0f, _ghostStalkMinDistance);
        public float GhostStalkMaxDistance => Mathf.Max(GhostStalkMinDistance, _ghostStalkMaxDistance);
        public float GhostStalkSpeed => Mathf.Max(0.1f, _ghostStalkSpeed);
        public float GhostStalkCreepSpeed => Mathf.Max(0.1f, _ghostStalkCreepSpeed);
        public float GhostStalkStressPerSecond => Mathf.Max(0f, _ghostStalkStressPerSecond);
        public float GhostStressRadiusMin => Mathf.Max(0f, _ghostStressRadiusMin);
        public float GhostStressRadiusMax => Mathf.Max(GhostStressRadiusMin, _ghostStressRadiusMax);
        public float GhostWanderSpeed => Mathf.Max(0.1f, _ghostWanderSpeed);
        public float GhostWanderPauseMin => Mathf.Max(0f, _ghostWanderPauseMin);
        public float GhostWanderPauseMax => Mathf.Max(GhostWanderPauseMin, _ghostWanderPauseMax);
        public float GhostPlayerDetectDistance => Mathf.Max(0f, _ghostPlayerDetectDistance);
        public float GhostPlayerAttackCooldown => Mathf.Max(0f, _ghostPlayerAttackCooldown);
        public float GhostPlayerAttackSwingSeconds => Mathf.Max(0f, _ghostPlayerAttackSwingSeconds);
        public float SleepLieDownSeconds => Mathf.Max(0f, _sleepLieDownSeconds);
        public float InteractionNoticeRadius => Mathf.Max(0f, _interactionNoticeRadius);
        public float InteractionScanInterval => Mathf.Max(0.05f, _interactionScanInterval);
        public float GlanceDuration => Mathf.Max(0f, _glanceDuration);
        public float ConversationRadius => Mathf.Max(0f, _conversationRadius);
        public float ConversationCheckInterval => Mathf.Max(0.1f, _conversationCheckInterval);
        public float ConversationChance => _conversationChance;
        public int ConversationsPerRoutine => Mathf.Max(0, _conversationsPerRoutine);
        public float ConversationDurationMin => Mathf.Max(0f, _conversationDurationMin);
        public float ConversationDurationMax => Mathf.Max(ConversationDurationMin, _conversationDurationMax);
        public int ConversationGroupMax => Mathf.Max(2, _conversationGroupMax);
        public float ConversationPartnerCooldown => Mathf.Max(0f, _conversationPartnerCooldown);
        public float HearingDistance => Mathf.Max(0f, _hearingDistance);
        public float TraceNeglectStressPerDay => Mathf.Max(0f, _traceNeglectStressPerDay);
        public float TraceNeglectStressMax => Mathf.Max(1f, _traceNeglectStressMax);
        public int MemoryDays => Mathf.Max(1, _memoryDays);
        public int MemoryEntriesPerKind => Mathf.Max(1, _memoryEntriesPerKind);
        public float VillagerStressMax => Mathf.Max(1f, _villagerStressMax);
        public float VillagerStressLimit => Mathf.Clamp(_villagerStressLimit, 0f, VillagerStressMax);
        public float VillagerStressAnxious => Mathf.Clamp(_villagerStressAnxious, 0f, VillagerStressLimit);
        public float VillagerStressMorningDecay => Mathf.Max(0f, _villagerStressMorningDecay);
        public ItemReactionCooldowns VillagerItemCooldowns => _villagerItemCooldowns;
        public float ItemStartleStress => Mathf.Max(0f, _itemStartleStress);
        public float BellStartleRadius => Mathf.Max(0f, _bellStartleRadius);
        public float ItemReactionYin => Mathf.Max(0f, _itemReactionYin);
        public float ItemViolentYin => Mathf.Max(ItemReactionYin, _itemViolentYin);
        public float VisionHalfAngle => _visionHalfAngle;
        public float VisionNearRadius => _visionNearRadius;
        public float VisionEyeHeight => _visionEyeHeight;
        public LayerMask VisionObstacleMask => _visionObstacleMask;
        public float ObservationRadius => _observationRadius;
        public float ObservationInterval => _observationInterval;
        public int WitnessTraitMin => Mathf.Clamp(_witnessTraitMin, 1, 3);
        public int WitnessTraitMax => Mathf.Clamp(_witnessTraitMax, WitnessTraitMin, 3);
        public float AnxiousMutterInterval => _anxiousMutterInterval;
        public float AnxiousMutterChance => _anxiousMutterChance;
        public float NeighbourVisitLingerSeconds => _neighbourVisitLingerSeconds;
        public float RumorChance => _rumorChance;
        public int RumorMaxHops => _rumorMaxHops;
        public int AlibiCompanionsPerRoutine => Mathf.Max(1, _alibiCompanionsPerRoutine);
        public float RequestCompletedStressRelief => _requestCompletedStressRelief;
        public float RequestFailedStress => _requestFailedStress;
        public bool ShowPossessedMaterial => _showPossessedMaterial;
        public Material PossessedDebugMaterial => _possessedDebugMaterial;

        // "Kind:Detail" wins over "Kind", so one tool or trace kind can get its own quest
        public bool TryGetRequestQuest(string kind, string detail, out string questId)
        {
            questId = null;

            if (_requestQuests == null || string.IsNullOrEmpty(kind))
            {
                return false;
            }

            string exactKey = string.IsNullOrEmpty(detail) ? null : $"{kind}:{detail}";

            foreach (RequestQuestEntry entry in _requestQuests)
            {
                if (string.IsNullOrEmpty(entry.QuestId))
                {
                    continue;
                }

                if (entry.RequestKey == exactKey)
                {
                    questId = entry.QuestId;
                    return true;
                }

                if (entry.RequestKey == kind && questId == null)
                {
                    questId = entry.QuestId;
                }
            }

            return questId != null;
        }

        public int GetInterruptResistance(VillagerActivity activity)
        {
            switch (activity)
            {
                case VillagerActivity.Rest:
                    return _restResistance;
                case VillagerActivity.Conversation:
                    return _conversationResistance;
                case VillagerActivity.Moving:
                    return _movingResistance;
                case VillagerActivity.ToolSearch:
                    return _toolSearchResistance;
                case VillagerActivity.Work:
                    return _workResistance;
                case VillagerActivity.ReturningHome:
                    return _returningHomeResistance;
                default:
                    return _workResistance;
            }
        }

        public bool TryGetHuntTier(float yin, out HuntTier tier)
        {
            tier = default;
            bool found = false;

            if (_huntTiers == null)
            {
                return false;
            }

            foreach (HuntTier candidate in _huntTiers)
            {
                if (candidate.Kills > 0 && yin >= candidate.MinYin && (!found || candidate.MinYin > tier.MinYin))
                {
                    tier = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
