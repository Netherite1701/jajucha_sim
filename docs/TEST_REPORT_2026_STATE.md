# 2026 상태 기반 전수 검증 보고서

검증 시각: 2026-08-20 (Asia/Seoul, 최종 재검증 06:35)  
빌드: `dist/JajuchaSimulator/JajuchaSimulator.exe`  
Unity: `6000.3.20f1`

## 최종 결과

- Python: 34 passed
- Unity EditMode: 493 passed, 0 failed
- Unity PlayMode: 55 passed, 0 failed
- 프로젝트/코스/PDF 검증: passed
- Windows 브리지 smoke: passed
- Windows 실제 입력 UI smoke: passed
- Windows 실제 센서 프레임 smoke: passed
- Windows 실제 시나리오·출발 신호·좌표 smoke: passed
- Windows 실제 짧은 주행·지면 판정 smoke: passed (`grounded=true`)
- 라이다: 360-ray full-circle scan, Unity 물리 레이캐스트·브리지 binary·Python manual API 모두 통과

## 실제 주행

브리지에 `set_motor(left=0,right=0,speed=30)`을 watchdog 주기 안에 반복 전송하고 `get_status` 좌표를 비교했다.

- 시작: `(350.000000, 3.966504, 45.000000) cm`, 회전 `y=270°` (공식 첫 도로 방향)
- 종료: `(155.563700, 4.014203, 12.350120) cm`
- 수평 이동: `197.15854 cm` (서쪽·북쪽으로 이동)
- 속도: `153.9000 cm/s` 근처, 회전 `y=273.2049°`, `driven_wheel_grounded=true`
- 높이 변화 `0.0477 cm`; 보드 밖으로 떨어지지 않음
- 모터 명령 수락, pause 중 tick/좌표 정지, step 정확히 1 tick, reset 후 tick/속도/속도벡터 0
- 정지 명령 중 WheelCollider 잔류 드리프트가 발생하지 않도록 post-physics 정지 pose 고정을 적용했고, gate trace에서 위치·회전·속도 모두 불변을 재확인했다.

증거:

- [주행 전 화면](../test-artifacts/drive/drive_before_1280x720.png)
- [주행 후 화면](../test-artifacts/drive/drive_after_1280x720.png)
- [최신 좌표·주행 화면 JSON](../test-artifacts/drive/drive_visual_20260820_063300.json)
- [최신 pause/step/reset 브리지 결과](../test-artifacts/bridge/bridge_smoke_20260820_063355.json)
- [최신 Python 통합 결과](../test-artifacts/python/python_live_smoke_20260820_063355.json)

## 실제 2026 시나리오 주행

실제 Win32 마우스로 미션을 설정한 뒤, 같은 실행 파일의 브리지와
`state-trace.jsonl`을 동시에 읽어 카운트다운과 차량 좌표를 검증했다.

- `Ready → Countdown → Running → Aborted` 전환 확인
- 램프 첫 tick 기준 간격: `1.50 / 1.51 / 1.51 s` (목표 1.5 s)
- seed 기반 release 대기: `3.399239 s` (공식 범위 3–6 s)
- 소등 전 `set_motor`: 차량 수평 좌표 변화 `0 cm`, 실제 command speed `0`, `falseStart=true`
- 해제 후 모터 입력: 최대 좌표 이동 `39.14137 cm`
- 결과 JSON과 trace 일치: mission/후보/seed/release delay/false start/aborted 상태
- 종료 후 `get_result` 두 번의 JSON이 동일

증거:

- [시나리오 결과·브리지 응답·좌표 스냅샷](../test-artifacts/scenario/scenario_smoke_20260820_063518.json)
- [미션 미설정 실제 시작 차단 결과](../test-artifacts/scenario/mission_gate_20260820_062914.json)
- [동적 장애물 미션 활성화·좌표·접근 전 정지](../test-artifacts/scenario/dynamic_obstacle_20260820_062850.json)
- [예선·결선 공식 코스 런타임 계약 결과](../test-artifacts/stages/stage_runtime_20260820_063427.json)
- 시작 방향과 물리 스텝 뒤 명령 속도 보존을 수정하고, 터널 U자 중심선을 5cm 샘플링 곡선으로 생성한 뒤 매뉴얼의 전체 조향 범위 `[-10,10]`를 사용하는 폐루프 시험을 재실행했다. 실제 좌표는 `s_curve → right_angle → u_tunnel → straight_hill` 구간을 통과했지만, 180초 연습 제한시간과 차량 조향 반경 때문에 예선·결선 전체 체크포인트 완주는 아직 **실패/미완료**다. 마지막 혼합속도 실행은 `status=timedout`, `completed=false`, `collisions=116`, `courseDepartures=13`으로 종료되어 터널 벽 접촉과 급곡선 이탈이 남아 있음을 확인했다. `Finished` 상태만으로 통과 처리하지 않도록 브리지 `completed` 결과와 마지막 경로 좌표를 함께 검사한다. 대표 증거는 [구조물 포함 전체 시도](../test-artifacts/scenario/checkpoint_drive_complete_check.json), [고속 경로 시도](../test-artifacts/scenario/checkpoint_drive_fast_complete.json), [최종 혼합속도 결과](../test-artifacts/scenario/checkpoint_drive_mixedspeed_final.json)다. 이 진단 조종기는 공식 브리지 pose/command만 사용하는 검증용 조종기이며 카메라·LiDAR 자율주행 정책의 대체물이 아니다.
- 실행 상태 trace JSONL: `C:\Users\user\AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs\state-trace.jsonl`
- [시나리오 실행 스크립트](../scripts/test_scenario_windows.ps1)

## 맵 빌더

실제 Win32 마우스 입력으로 `코스 편집 → 연습용 복사본 → 도로 칠하기 → Undo → Redo → 시험 주행 → 복원 → 저장 → 재로드 → 주행 탭`을 수행했다.

- 공식 원본은 `OfficialReadOnly`로 유지되고 편집 버튼/저장이 차단됨
- 복사 후 `PracticeCopy`, `readOnly=false`
- 검증 문구가 녹색 `규격 검증 통과`로 표시됨
- trace의 도로 타일 수: `12399 → 12400 → 12399 → 12400`
- 공식 문서 해시는 변경되지 않음
- 시험 주행 중 `appMode=Drive`, 종료 후 `appMode=MapEditor`와 편집 문서 해시 복원
- 저장 파일은 `Courses/Practice/practice_2026_preliminary(_n).json` 번호 규칙으로 생성됨

증거:

- [공식 코스 읽기 전용](../test-artifacts/ui/02_course_official_1280x720.png)
- [연습 복사본 검증 통과](../test-artifacts/ui/03_course_practice_1280x720.png)
- [맵 편집·Undo/Redo 화면](../test-artifacts/ui/06_map_editor_edited_1280x720.png)
- [시험 주행·저장·재로드 화면](../test-artifacts/ui/09_course_lifecycle_1280x720.png)
- [UI 상태 trace JSONL](../test-artifacts/ui/state-trace_ui_final.jsonl)

## 카메라·depth

실행 중 브리지 `get_image(left|center|right)`와 `get_depth`를 호출해 바이너리 길이, 포맷, PNG를 검증했다.

- 좌/중앙/우: `640×480`, `rgb24`, `921600 bytes`, non-zero
- depth: `640×480`, `gray8`, `307200 bytes`, non-zero
- 센서 RenderTexture에는 대시보드 UI가 포함되지 않음
- 세 카메라의 실제 대시보드 미리보기 표시도 확인
- Python LiDAR 증거 그림은 mm 단위 API의 관측 범위(최대 10m)에 맞춰 자동 스케일링해, 원거리 ray가 1m 외곽에 겹쳐 보이는 표시 오해를 제거했다.

증거:

- [좌 카메라 프레임](../test-artifacts/sensors/left_camera.png)
- [중앙 카메라 프레임](../test-artifacts/sensors/center_camera.png)
- [우 카메라 프레임](../test-artifacts/sensors/right_camera.png)
- [depth 프레임](../test-artifacts/sensors/center_depth.png)
- [센서 탭 화면](../test-artifacts/ui/08_sensors_dashboard_1280x720.png)
- [라이다 polar 프레임](../test-artifacts/sensors/lidar.png)
- [최신 센서 결과 JSON](../test-artifacts/sensors/sensor_smoke_20260820_063402.json)
- [최신 Python 센서·라이다 결과](../test-artifacts/python/python_live_smoke_20260820_063355.json)

검증 중 발견해 수정한 센서 결함은 다음과 같다.

1. Windows GPU의 RGB24 async readback 부분 viewport 문제를 피하기 위해 standalone은 native RGBA32 async readback, Editor는 동기 readback으로 분기하고 RGB24로 정규화
2. Unity bottom-left texture origin을 top-left JCHM/OpenCV 방향으로 행 반전
3. depth 셰이더가 전역 파라미터를 받지 못하고 초기 depth RenderTexture를 렌더하지 않던 문제를 수정
4. depth 셰이더를 Windows 빌드의 Always Included Shaders에 등록

## 라이다·Python 매뉴얼 계약

- Unity `LidarSensorSystem`: 차량 자체 Collider를 제외한 수평 360 raycast, 1° 간격, 최대 1000cm
- bridge `get_lidar`: `float32_le`, 360개, 각도·tick·frame metadata, cm 거리
- `jchm.lidar.get_lidar()`: 매뉴얼과 동일하게 `(theta_array, dist_array)`를 반환하며 각도는 `[0,360)` 도, 거리는 mm로 변환(500 = 50cm)
- `jchm.lidar.show_lidar()`와 극좌표→직교좌표 계산 예제를 실행 가능한 형태로 제공
- PlayMode 물리 테스트에서 50cm 전방 BoxCollider를 약 40cm(라이다 mount 기준)로 감지하고 reset 후 재생성 확인

## 해상도 확인

1024×576, 1280×720, 1600×900, 1920×1080에서 실제 창을 캡처했다. 탭·버튼·문구가 겹치거나 화면 밖으로 잘리지는 않았으며, 남는 회색 영역은 관찰 카메라의 배경 영역이다.

- [1024×576](../test-artifacts/ui/01_ready_1024x576.png)
- [1280×720](../test-artifacts/ui/01_ready_1280x720.png)
- [1600×900](../test-artifacts/ui/01_ready_1600x900.png)
- [1920×1080](../test-artifacts/ui/01_ready_1920x1080.png)

## 미지원/주의 사항

- 주행 좌표 이동과 출발 신호·미션 결과는 실제 입력 smoke로 검증했다. 명시적 cm/s 이동 모드의 보드 지지면 판정을 수정한 뒤 짧은 주행은 `grounded=true`로 통과했다. 다만 실제 차량 조향 반경과 180초 연습 제한시간을 고려한 예선·결선 전체 체크포인트 완주는 아직 미완료다. 이 미완료 상태를 숨기지 않고 경로 JSON에 `completed`, `timedOut`, 마지막 좌표를 보존한다.
- Python 브리지의 한 바이트씩 UTF-8 디코드 결함을 수정하여 한국어 `get_result` 필드도 정상 파싱하고 단위·실행 테스트를 재통과했다.
- Unity 로그의 `d3d12 failed to query info queue interface` 및 일부 셰이더 경고는 기능 실패로 재현되지 않았고, 전체 테스트는 통과했다.
