[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "        Unity Smart Merge (UnityYAMLMerge) Git 설정       " -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Git 설치 확인
try {
    $gitVersion = git --version
    Write-Host "[✓] Git 확인됨: $gitVersion" -ForegroundColor Green
} catch {
    Write-Host "[X] Git이 설치되어 있지 않거나 환경 변수 PATH에 등록되지 않았습니다." -ForegroundColor Red
    exit 1
}

# 2. Git 저장소 루트 확인
$gitRoot = git rev-parse --show-toplevel 2>$null
if (-not $gitRoot) {
    Write-Host "[X] 현재 폴더가 Git 저장소가 아닙니다. Git 저장소 내에서 실행해주세요." -ForegroundColor Red
    exit 1
}
$gitRoot = $gitRoot.Trim()
Write-Host "[✓] Git 저장소: $gitRoot" -ForegroundColor Green

# 3. 프로젝트 Unity 버전 파싱
$projectVersionFile = Join-Path $gitRoot "ProjectSettings\ProjectVersion.txt"
$targetUnityVersion = ""

if (Test-Path $projectVersionFile) {
    $versionContent = Get-Content $projectVersionFile -Raw
    if ($versionContent -match "m_EditorVersion:\s*([^\r\n]+)") {
        $targetUnityVersion = $matches[1].Trim()
        Write-Host "[✓] 프로젝트 Unity 버전 감지: $targetUnityVersion" -ForegroundColor Green
    }
}

if (-not $targetUnityVersion) {
    Write-Host "[!] ProjectSettings/ProjectVersion.txt 에서 버전을 찾지 못했습니다. 설치된 Unity를 탐색합니다." -ForegroundColor Yellow
}

# 4. UnityYAMLMerge.exe 탐색 함수
function Find-UnityYAMLMerge {
    param(
        [string]$version
    )

    # [1단계] Windows 레지스트리 탐색 (임의의 드라이브/커스텀 폴더에 설치된 경우도 완벽 감지)
    $regRoots = @(
        "HKLM:\SOFTWARE\Unity Technologies\Installer",
        "HKCU:\Software\Unity Technologies\Installer",
        "HKLM:\SOFTWARE\WOW6432Node\Unity Technologies\Installer"
    )

    $anyInstalledFallback = $null

    foreach ($regRoot in $regRoots) {
        if (Test-Path $regRoot) {
            $subKeys = Get-ChildItem -Path $regRoot -ErrorAction SilentlyContinue
            foreach ($subKey in $subKeys) {
                $props = Get-ItemProperty -Path $subKey.PSPath -ErrorAction SilentlyContinue
                $loc = $props.'Location x64'
                if (-not $loc) { $loc = $props.'Location' }
                $ver = $props.Version

                if ($loc) {
                    $candidateExe = Join-Path $loc "Editor\Data\Tools\UnityYAMLMerge.exe"
                    if (-not (Test-Path $candidateExe)) {
                        $candidateExe = Join-Path $loc "Data\Tools\UnityYAMLMerge.exe"
                    }

                    if (Test-Path $candidateExe) {
                        # 프로젝트 버전과 정확히 일치하는 경우 즉시 반환
                        if ($version -and ($ver -eq $version -or $subKey.PSChildName -like "*$version*")) {
                            return $candidateExe
                        }
                        if (-not $anyInstalledFallback) {
                            $anyInstalledFallback = $candidateExe
                        }
                    }
                }
            }
        }
    }

    # [2단계] 모든 고정 드라이브(C:, D:, E:, F: 등) 및 Unity Hub 기본 경로 탐색
    $drives = [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' } | ForEach-Object { $_.RootDirectory.FullName }

    if ($version) {
        $candidateSubPaths = @(
            "Program Files\Unity\Hub\Editor\$version\Editor\Data\Tools\UnityYAMLMerge.exe",
            "Program Files (x86)\Unity\Hub\Editor\$version\Editor\Data\Tools\UnityYAMLMerge.exe",
            "Unity\Hub\Editor\$version\Editor\Data\Tools\UnityYAMLMerge.exe",
            "Unity\$version\Editor\Data\Tools\UnityYAMLMerge.exe",
            "Program Files\Unity\$version\Editor\Data\Tools\UnityYAMLMerge.exe"
        )

        foreach ($drive in $drives) {
            foreach ($sub in $candidateSubPaths) {
                $fullPath = Join-Path $drive $sub
                if (Test-Path $fullPath) {
                    return $fullPath
                }
            }
        }
    }

    # [3단계] 레지스트리에서 찾은 타 버전이 있다면 반환
    if ($anyInstalledFallback) {
        return $anyInstalledFallback
    }

    # [4단계] 드라이브 내 Unity Hub 폴더 전체 탐색
    foreach ($drive in $drives) {
        $hubEditorDir = Join-Path $drive "Program Files\Unity\Hub\Editor"
        if (Test-Path $hubEditorDir) {
            $found = Get-ChildItem -Path $hubEditorDir -Filter "UnityYAMLMerge.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { return $found.FullName }
        }

        $customHubDir = Join-Path $drive "Unity\Hub\Editor"
        if (Test-Path $customHubDir) {
            $found = Get-ChildItem -Path $customHubDir -Filter "UnityYAMLMerge.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { return $found.FullName }
        }
    }

    return $null
}

Write-Host "UnityYAMLMerge.exe 경로 탐색 중..." -ForegroundColor Gray
$yamlMergePath = Find-UnityYAMLMerge -version $targetUnityVersion

# [5단계] 자동 탐색 실패 시 파일 선택창(GUI 팝업) 실행
if (-not $yamlMergePath) {
    Write-Host "[!] UnityYAMLMerge.exe 경로를 자동으로 찾지 못했습니다." -ForegroundColor Yellow
    Write-Host "    열리는 파일 탐색기 창에서 UnityYAMLMerge.exe(또는 Unity.exe)를 선택해주세요..." -ForegroundColor Yellow

    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = "UnityYAMLMerge.exe 또는 Unity.exe 를 선택해주세요"
    $dialog.Filter = "Unity 실행 파일 (UnityYAMLMerge.exe;Unity.exe)|UnityYAMLMerge.exe;Unity.exe|모든 파일 (*.*)|*.*"

    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $selectedPath = $dialog.FileName
        if ($selectedPath -like "*Unity.exe") {
            # Unity.exe를 선택한 경우 Data\Tools\UnityYAMLMerge.exe 경로 유추
            $inferredPath = Join-Path (Split-Path $selectedPath) "Data\Tools\UnityYAMLMerge.exe"
            if (Test-Path $inferredPath) {
                $yamlMergePath = $inferredPath
            } else {
                $yamlMergePath = $selectedPath
            }
        } else {
            $yamlMergePath = $selectedPath
        }
    }
}

if (-not $yamlMergePath -or (-not (Test-Path $yamlMergePath))) {
    Write-Host ""
    $manualPath = Read-Host "수동으로 UnityYAMLMerge.exe 전체 경로를 입력해주세요 (취소하려면 Enter)"
    if ($manualPath -and (Test-Path $manualPath.Trim('"', "'"))) {
        $yamlMergePath = $manualPath.Trim('"', "'")
    } else {
        Write-Host "[X] 유효한 UnityYAMLMerge.exe 경로가 지정되지 않았습니다. 설정을 중단합니다." -ForegroundColor Red
        exit 1
    }
}

Write-Host "[✓] UnityYAMLMerge 발견: $yamlMergePath" -ForegroundColor Green

# 5. Git Config 적용 (Global ~/.gitconfig 및 Local .git/config 모두 적용)
$escapedPath = $yamlMergePath.Replace('\', '/')
$driverCmd = "'$escapedPath' merge -h -p -- '%O' '%B' '%A' '%A'"
$mergetoolCmd = "'$escapedPath' merge -h -p -- `"`$BASE`" `"`$REMOTE`" `"`$LOCAL`" `"`$MERGED`""

Write-Host "Git 설정 적용 중 (Global & Local)..." -ForegroundColor Gray

# (1) Global 설정 (~/.gitconfig 에 기록)
git config --global merge.unityyamlmerge.name "Unity SmartMerge (UnityYamlMerge)"
git config --global merge.unityyamlmerge.driver $driverCmd
git config --global merge.unityyamlmerge.recursive "binary"
git config --global mergetool.unityyamlmerge.cmd $mergetoolCmd
git config --global mergetool.unityyamlmerge.trustExitCode "false"

# (2) Local 설정 (.git/config 에 기록)
git config --local merge.unityyamlmerge.name "Unity SmartMerge (UnityYamlMerge)"
git config --local merge.unityyamlmerge.driver $driverCmd
git config --local merge.unityyamlmerge.recursive "binary"
git config --local mergetool.unityyamlmerge.cmd $mergetoolCmd
git config --local mergetool.unityyamlmerge.trustExitCode "false"

$userProfile = [Environment]::GetFolderPath("UserProfile")
$globalGitConfigFile = Join-Path $userProfile ".gitconfig"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "         ★ Git Config 설정이 완료되었습니다! ★            " -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "• 적용된 UnityYAMLMerge: $escapedPath" -ForegroundColor Cyan
Write-Host "• Global 설정 파일: $globalGitConfigFile" -ForegroundColor Cyan
Write-Host "• Local 설정 파일: $gitRoot\.git\config" -ForegroundColor Cyan
Write-Host "• merge.unityyamlmerge.driver 설정 완료" -ForegroundColor White
Write-Host "• mergetool.unityyamlmerge.cmd 설정 완료" -ForegroundColor White
Write-Host "=========================================================" -ForegroundColor Green
Write-Host ""
