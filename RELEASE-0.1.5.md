# SenseBridge 0.1.5

## 한국어

- 실크송 전용 데이터를 본체와 별도로 배포합니다. Steam 목록에서 실크송을 선택하고 **데이터 다운로드**를 누르면 검증 후 가져옵니다. 실크송은 기본 내장 게임이 아닙니다.
- 처음 설치할 때 게임을 종료해 주세요. 다운로드 후 연결 모듈을 설치하며, 경로가 없으면 실행 파일을 지정합니다. 효과는 기본적으로 모두 꺼져 있습니다.
- 실크송 데이터: 효과음 기반 햅틱 220개, 패드 스피커 효과음 7개, 동작 상태 트리거 규칙 100개. 현재 버튼 배정을 읽어 L2 작살 대시와 R2 대시 등에 연결합니다.
- 한국어·영어·일본어 다운로드 UI, 파일 무결성 확인, 프리셋 JSON 가져오기·내보내기, 상단 중앙 메뉴를 제공합니다.
- 배포판의 디버그 모드는 숨겨져 있습니다. ZIP을 풀고 SenseBridge.exe를 실행하면 됩니다.

실크송 데이터는 Steam 빌드 22479045의 게임 어셈블리를 기준으로 제작했으며 다른 빌드는 연결을 거부합니다. 이동·공격 햅틱, 회복 스피커 및 일부 트리거를 실제 플레이에서 확인했습니다. 전체 100개 규칙의 모든 기술을 플레이로 검증한 것은 아닙니다. AI 분류는 효과 선택을 돕는 도구이며, 모든 음원을 완벽하게 분류했다는 의미는 아닙니다.

## English

Silksong is an optional, separately downloaded data pack, not a built-in game. Select Silksong in the Steam library and click **Download data**. Close the game for the initial connection-module installation. All effects start disabled. The pack includes 220 sound-based haptic effects, 7 controller-speaker effects and 100 action-state trigger rules, with current L2/R2 bindings resolved at runtime.

Supports the verified Steam build 22479045 only. Basic movement/attack haptics, healing speaker playback and some triggers were tested in gameplay; not every ability in the 100 rules was gameplay-tested. The portable ZIP includes dependencies, preset import/export and the centered navigation UI. Release builds hide debug tools.

## 日本語

シルクソングは本体に内蔵せず、専用データを別途配布します。Steam一覧で選択して **データをダウンロード** を押してください。初回接続モジュールの導入時はゲームを終了してください。各効果は初期状態ではオフです。

効果音ベースのハプティクス220件、コントローラースピーカー効果音7件、動作状態のトリガールール100件を収録。現在のボタン設定に合わせてL2/R2に接続します。対応確認済みのSteamビルドは22479045です。全100ルールのすべての技を実機プレイ検証したわけではありません。
