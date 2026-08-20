# Jajucha Simulator 2026

2026 자주차 경진대회 예선·결선 코스를 사용하는 자율주행 시뮬레이터입니다.
공식 코스, 4등 출발 신호, 옐로 플래그/돌발 장애물 미션, 3대 카메라와
LiDAR, Python 브리지를 포함합니다.

## 먼저 확인할 것

이 저장소는 **개발용 Unity 프로젝트**입니다. 학생이 Unity를 설치할 필요는
없습니다. 학생에게는 GitHub Releases의 standalone ZIP만 배포하세요.

### 학생용 실행

1. GitHub의 [Releases](https://github.com/Netherite1701/jajucha_sim/releases)에서
   `JajuchaSimulator-2026-Student.zip`을 다운로드합니다.
2. 원하는 폴더에 압축을 풉니다. 관리자 권한이나 설치 프로그램은 필요하지
   않습니다.
3. 압축을 푼 폴더의 `JajuchaSimulator.exe`를 실행합니다.

학생용 standalone 실행에는 Unity Hub, Unity Editor, Unity 라이선스가 필요하지
않습니다. 설정과 연습 코스는 Windows 사용자 데이터 폴더에 저장되므로 ZIP을
교체해도 유지됩니다.

> GitHub의 **Code → Download ZIP**은 Unity 소스 코드입니다. 학생용 실행 파일이
> 필요하면 반드시 Releases의 standalone ZIP을 사용해야 합니다.

## 개발용 실행

개발자는 Unity 프로젝트 루트(이 README가 있는 폴더)를 VS Code로 엽니다.

필요한 개발 도구:

- Unity 6 LTS (현재 개발 버전: `6000.3.20f1`)
- Python 3.9 이상
- VS Code 확장: Python, Debugpy, Unity Tools

처음 한 번만 PowerShell에서 Python 환경을 만듭니다.

```powershell
.\scripts\setup_python.ps1
```

그 다음 VS Code의 `Terminal → Run Task`에서 다음 작업을 사용할 수 있습니다.

- `Jajucha: Run standalone simulator`
- `Jajucha: Run user controller`
- `Jajucha: Check bridge`
- `Jajucha: Run Python tests`
- `Jajucha: Open Unity Editor` (개발자 전용)

standalone 빌드가 아직 없다면 개발자 PC에서 다음을 실행합니다.

```powershell
.\scripts\build_windows.ps1
```

생성된 배포 폴더는 `dist/JajuchaSimulator/`입니다.

## Python 사용자 프로그램

```text
python/
├─ jchm/          실제 차량과 호환되는 제어 API
├─ jchm_sim/      시뮬레이터 전용 수명주기/테스트 API
├─ examples/      예제 01~06
├─ user/          사용자 프로그램(main.py)
└─ tests/         Python 테스트
```

개발 환경에서 실행:

```powershell
.\.venv\Scripts\python.exe .\python\user\main.py
.\.venv\Scripts\python.exe -m pytest python\tests\ -q
```

학생용 ZIP에서 Python까지 무설치로 사용하려면 embedded Python 런타임이 포함된
Student 패키지를 사용해야 합니다. 일반 소스 ZIP은 Python 환경을 자동으로
설치하지 않습니다.

## 코스와 기본값

- 첫 실행 코스: `2026_preliminary`
- 선택 가능한 코스: `2026_preliminary`, `2026_final`
- 공식 코스 원본은 읽기 전용입니다.
- 연습용 복사본은 `Courses/Practice`에 저장됩니다.
- 설정 파일과 마지막 코스 선택은 사용자 데이터에 저장됩니다.

## 테스트

Python 테스트:

```powershell
.\.venv\Scripts\python.exe -m pytest python\tests\ -q
```

Unity EditMode/PlayMode 테스트는 Unity가 설치된 개발자 PC에서 실행합니다.
자세한 명령은 `docs/TESTING.md`를 참고하세요.

## 문서

- `docs/STUDENT_QUICKSTART_KO.md` — 학생용 버튼·키보드·카메라 조작법
- `docs/USER_WORKFLOW.md` — 주행·코스 편집·시험 주행
- `docs/COMPETITION_2026.md` — 2026 규격과 연습 기본값
- `docs/VSCODE_WORKFLOW.md` — VS Code 개발 흐름
- `docs/MANUAL_COMPATIBILITY.md` — 매뉴얼 근거와 비공식 연습값
- `docs/TEST_REPORT_2026_STATE.md` — 내부 상태 검증 기록

## 단위와 규칙

- Unity 1 unit = 1 cm
- 기본 중력: `-981 cm/s²`
- 공식 PDF와 코스 JSON을 우선하며, 문서에 없는 값은 비공식 연습값으로 표시합니다.
