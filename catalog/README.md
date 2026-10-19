# 게임 데이터 자료실 / Game data library

모든 게임 전용 데이터 팩은 [게임 데이터 자료실](https://github.com/trackk81/SenseBridge/releases/tag/game-data)에 모읍니다. 본체 릴리스와 독립적으로 관리합니다.

- [Hollow Knight: Silksong 1.0](https://github.com/trackk81/SenseBridge/releases/download/game-data/Silksong-1.0.sensebridge.json): 효과음 기반 햅틱 220개, 스피커 효과음 7개, 동작 상태 트리거 규칙 100개. 게임 빌드 호환성은 가져온 데이터에서 검사합니다.
- Elden Ring: 조사·개발 중. 아직 다운로드 가능한 검증 완료 데이터가 아닙니다.

프로그램에서 게임을 선택해 다운로드하거나, JSON을 받아 **데이터 → 데이터 불러오기**로 가져옵니다. Bloodborne은 계속 본체 내장입니다. 기존 0.1.5의 실크송 다운로드 주소는 구버전 호환성을 위해 유지합니다.

The shared catalog is `index.json`. Entries identify the game, compatible adapter/app version, immutable versioned download URL, size and SHA-256. Publish assets before adding a catalog entry. Never overwrite an existing versioned asset with different bytes. New games must pass validation before becoming downloadable. Demo/trial games and 3DMark are excluded from the development routine.
