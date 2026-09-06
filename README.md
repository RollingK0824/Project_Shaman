# Project_Shaman

주민들 속에 숨어든 빙의자를 색출하고 퇴마를 통해 봉인하는 4인 협동 PvE 오컬트 추리 게임

---

## 개발 환경 및 도구

- **Engine** : Unity 6 (`6000.3.23f1`)
- **Render Pipeline** : URP (Universal Render Pipeline)
- **IDE** : Visual Studio 2026
- **Version Control** : Git / GitHub (Fork / GitHub Desktop)
- **CI / Pipeline** : GitHub Actions (Game-CI 자동 컴파일 및 빌드 검증)
- **Git 확장 및 협업 도구** : 
  - **Git LFS** (대용량 그래픽/사운드 바이너리 관리)
  - **Unity SmartMerge (UnityYAMLMerge)** (씬, 프리팹 충돌 자동 병합)

**팀원 최초 필수 세팅 (원클릭 환경 구성):**
> 저장소를 Clone 또는 Pull 받은 후, **`Tools/GitSetup/setup_git.bat`** 파일을 더블 클릭하여 실행합니다.  
> (Git LFS 활성화 및 Unity SmartMerge 등록이 자동으로 완료됩니다.)

---

## 디렉토리 구조 규칙

외부 에셋과의 혼선을 줄이고 에셋 충돌을 방지하기 위해 우리 팀이 제작하는 모든 리소스와 코드는 `Assets/Project/` 폴더 내부에서만 관리합니다.

```text
Assets/
├── Project/                   # 메인 작업 공간 (팀 자체 제작 리소스)
│   ├── Animations/            # Animation 클립, Animator Controller
│   ├── Audio/                 # BGM, SFX 등 음원 파일
│   ├── Data/                  # ScriptableObject, JSON, 데이터 테이블
│   ├── Prefabs/               # 기능별 완성형 프리팹 (씬 직접 수정 지양)
│   ├── Scenes/                # 담당자별 / 기능별 분리된 씬 공간
│   ├── Scripts/               # C# 스크립트 코드 (Assembly Definition 적용)
│   ├── Settings/              # URP, Volume Profile, Input Actions 등 세팅 에셋
│   └── Visuals/               # 비주얼/그래픽 리소스
│       ├── Fonts/             # 폰트 파일 및 TextMeshPro 폰트 에셋
│       ├── Materials/         # 머티리얼 및 물리 머티리얼
│       ├── Models/            # 3D 모델(FBX, OBJ) 및 메시
│       ├── Shaders/           # 커스텀 셰이더 및 셰이더 그래프
│       ├── Sprites/           # 2D 스프라이트 및 UI 이미지
│       ├── Textures/          # 텍스처 맵 (Albedo, Normal 등)
│       └── VFX/               # 파티클 시스템 및 이펙트 프리팹
│
└── ThirdParty/                # 에셋스토어 패키지 및 외부 다운로드 플러그인 보관
```

---

## 코딩 컨벤션 (Coding Convention)

유니티 공식 C# 스타일을 기반으로 한 우리 팀의 최소한의 규칙입니다.

### 1. 명명 규칙
- **Class / Method / Enum / Struct**: `PascalCase` (앞 글자 대문자)
  - `public class PlayerController : MonoBehaviour`
  - `public void MoveToTarget()`
- **Public 변수**: `PascalCase` (인스펙터 노출용)
  - `public float MoveSpeed;`
- **Private / Protected 필드**: `_camelCase` (언더바 `_` 접두사 필수)
  - `private int _currentHp;`
  - `[SerializeField] private float _attackRange;`
- **지역 변수 / 매개 변수**: `camelCase` (첫 글자 소문자)
  - `float currentDistance = Vector3.Distance(...);`
- **상수(const) / Static Readonly**: `UPPER_SNAKE_CASE` (대문자와 언더바)
  - `private const int MAX_WAVE_COUNT = 5;`

### 2. Bool 변수 규칙
- 상태를 직관적으로 알 수 있도록 `is`, `has`, `can` 등의 접두사를 사용합니다.
  - 예: `isDead`, `hasKey`, `canJump`, `isInitialized`

### 3. 코드 스타일 및 주의사항
- 중괄호(`{ }`)는 생략하지 않고 항상 줄바꿈하여 가독성을 높입니다.
- 스크립트 파일명을 바꿀 때는 **반드시 유니티 에디터 내부**에서 변경합니다. (메타 파일 깨짐 방지)
- 주석은 왜(Why) 이렇게 작성했는지를 중심으로 간결하게 작성합니다.

---

## 커밋 컨벤션 (Commit Convention)

우리 팀은 커밋 메시지의 일관성을 위해 아래 규칙을 준수합니다.

### 1. 커밋 메시지 구조
기본적으로 `[태그]: [제목]` 형태로 작성하며, 상세 내용이 필요한 경우 본문을 추가합니다.

```text
태그: 요약문

- 상세 작업 내용 1 (선택 사항)
- 상세 작업 내용 2 (선택 사항)
```

### 2. 커밋 태그 종류
모든 태그는 소문자로 작성하고 콜론(`:`) 뒤에 한 칸을 띕니다.

| 태그 | 설명 | 예시 |
| :--- | :--- | :--- |
| `feat` | 새로운 기능 추가, 새로운 스크립트/에셋 생성 | `feat: 플레이어 이동 및 점프 기능 구현` |
| `fix` | 버그, 에러, 씬/프리팹 깨짐 현상 수정 | `fix: 셰이더 Y축 뒤집힘 및 암전 오류 수정` |
| `refactor` | 기능 변화 없이 코드 구조 개선, 변수명 변경, 구조 최적화 | `refactor: 웨이브 매니저 루프 구조 최적화` |
| `chore` | 빌드/패키지/설정 파일/README 수정 등 기타 작업 | `chore: 프로젝트 README.md 코딩 컨벤션 추가` |
| `docs` | 문서 작성 및 주석 수정 | `docs: 스킬 시스템 기획 문서 링크 추가` |

### 3. 작성 규칙
- **명령문/현재형 요약**: 제목은 "~했음", "~수정함" 보다는 명확한 명사형 종결(`구현`, `수정`, `제거`, `추가`) 형태로 작성합니다.
- **제목과 본문 분리**: 상세 설명이 필요할 경우, 제목을 쓰고 한 줄을 비운 뒤 대시(`-`)를 활용해 본문을 적습니다.

---

## 협업 골든 룰 (Golden Rules)

1. **`dev` / `main` 브랜치 직접 Push 금지**:
   - 모든 작업은 `dev` 브랜치에서 파생된 **`feature/기능이름`** 브랜치에서 진행합니다.
   - 작업 완료 후 GitHub에서 **`feature/* -> dev` Pull Request(PR)**를 생성하여 팀원 리뷰 및 CI 빌드 통과 후 머지합니다.
2. **작업 시작 전 Pull 필수**:
   - 작업을 시작하기 전 무조건 `dev` 브랜치에서 **Fetch 및 Pull**을 받아 최신 상태로 동기화한 뒤 내 작업 브랜치를 생성하거나 이동합니다.
3. **1인 1씬(Scene) 및 프리팹(Prefab) 작업 원칙**:
   - 씬 파일 충돌을 최소화하기 위해 씬 직접 수정보다는 **프리팹(Prefab)** 단위로 작업하여 머지합니다.
   - 씬 작업이 필요할 경우 담당자별 테스트 씬을 분리하여 작업합니다.
