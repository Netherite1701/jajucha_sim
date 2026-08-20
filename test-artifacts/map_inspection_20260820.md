# 2026 코스 실제 월드 검사 기록 (2026-08-20)

실행 파일 `dist/JajuchaSimulator/JajuchaSimulator.exe`를 1280×720 창으로
실행하고 Win32 실제 마우스/키 입력을 보냈다. 화면은 보조 증거로 캡처했고,
최종 판정은 Unity `state-trace.jsonl`, 브리지 `get_status`, 코스 JSON을 함께
비교했다.

## 촬영 목록

- 예선: `ui/preliminary_chase_1280x720.png`, `ui/preliminary_topdown_1280x720.png`, `ui/preliminary_free_1280x720.png`
- 결선: `ui/final_chase_1280x720.png`, `ui/final_topdown_1280x720.png`, `ui/final_free_1280x720.png`
- 마스크 겹침 검사: `map_mask_overlay_fixed.png`
- 실제 차량 전/후: `drive/drive_before_1280x720.png`, `drive/drive_after_1280x720.png`
- 센서/편집 화면: `ui/08_sensors_dashboard_1280x720.png`, `ui/06_map_editor_edited_1280x720.png`, `ui/09_course_lifecycle_1280x720.png`

## 확인 결과

| 항목 | 결과 |
|---|---|
| 예선/결선 | 두 코스 모두 41패널, 구조물 2, 오브젝트 2, 트리거 2, 공식 읽기 전용 |
| 도로 마스크 | 두 코스 모두 9,472 타일, 1개 연결 경로, 둘러싸인 녹지 영역 1개 |
| 터널 메시 | 선분별 접합으로 생기던 U자 외곽 쐐기 틈 제거; S자 경로는 곡선 보간 |
| 텍스처 | 패널 문자·분할선 없음; 실제 차선/빨강-흰색 커브 유지 |
| 차량 좌표 | 시작 `(350, 3.97, 45) cm`에서 브리지 속도 명령 후 `(149.93, 4.40, 45) cm`, 이동 200.07cm, 접지 true |
| 센서 | 중앙 카메라 640×480, 라이다 360 ray 상태 확인 |
| UI 입력 | 코스 복사, 도로 칠하기, undo/redo, 시험 주행 복원, 저장/재로드, 센서 탭 통과 |

## 수정 원인

기존 마스크 생성기가 녹색 이력을 어두운 도로로 잘못 분류해 U/S자 내부
녹지까지 도로로 등록했다. 중성 회색 픽셀만 도로로 세도록 분류 조건을
수정하고, 그 결과를 두 공식 JSON에 재생성했다. 터널은 5cm 샘플 경계
리본과 S자 곡선 보간으로 재생성했다.

## 자동 검증

- Python: 35 passed
- Unity EditMode: 494 passed, 0 failed
- 실제 UI camera view smoke: passed
- 실제 stage runtime smoke (예선/결선, bridge, lidar, countdown/abort): passed
- 실제 drive visual smoke: passed
