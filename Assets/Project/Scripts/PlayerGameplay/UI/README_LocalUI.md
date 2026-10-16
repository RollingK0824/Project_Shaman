# Player 로컬 UI 생성

현재 `Player (1)`은 PlayerLocalUIBootstrap 하나를 사용한다. 이전 PlayerStressHUDBootstrap/PlayerCrosshairBootstrap 타입은 백업 프리팹의 GUID 참조 보존을 위해 남기되 현재 Player에는 붙이지 않는다.

공통 책임은 local ownership 확인, UI 생성, 기존 컴포넌트 Bind, Player/Bootstrap 해제 시 UI 정리다. HUD와 조준점은 기존처럼 Player 밖의 독립 UI root로 생성된다.

| UI | 기존 표시 규칙 |
|---|---|
| StressHUD | 로컬 Player이고 StressController가 존재하면 표시. 입력 차단/사망만으로 숨기지 않음 |
| Crosshair | 로컬이며 생존/행동 가능, InputReader 활성, GameplayInputBlocked=false일 때 표시 |

생성과 소유만 합쳤으며 두 규칙은 별도 메서드로 유지했다. StressHUD/Crosshair 자체 코드·아트·바인딩 API·수치 표시와 네트워크 설정은 변경하지 않았다. 기존 HUD Prefab/Stress/Health 참조와 Crosshair Prefab을 현재 PlayerLocalUIBootstrap에 이전했다. Disable 시 두 인스턴스를 제거하고 재활성화 시 각각 한 번 생성한다.
