# SenseBridge 0.1.6

## 한국어

- 상단 중앙 메뉴에 **업데이트** 버튼을 추가했습니다. 새 정식 버전이 있을 때만 활성화됩니다.
- 프로그램 시작 시와 실행 중 6시간마다 GitHub 최신 릴리스를 확인합니다.
- 버튼을 누르면 별도 업데이트 ZIP을 다운로드하고 SHA-256 및 파일 목록을 검증합니다. SenseBridge를 정상 종료한 뒤 파일을 교체하고 다시 실행합니다.
- 게임 목록, 실행 경로, 옵션, 다운로드한 게임 데이터 팩을 보존합니다. 교체 실패 시 변경한 파일을 복구하며 백업은 임시 업데이트 폴더에 남깁니다.
- 전체 설치 ZIP과 프로그램 파일만 포함하는 업데이트 ZIP, `update-manifest.json`을 별도로 제공합니다. 업데이트 ZIP은 바이너리 차분 파일이 아니므로 용량이 전체 ZIP과 비슷할 수 있습니다.

**0.1.5 이하에는 업데이트 기능이 없습니다. 이번 0.1.6은 전체 ZIP으로 교체해야 하며, 0.1.6부터 이후 버전을 앱 안에서 업데이트할 수 있습니다.** 업데이트 ZIP은 앱이 사용하는 파일이므로 직접 기존 폴더에 풀 필요가 없습니다. 실행 중인 모듈 때문에 교체가 불가능하면 게임을 정상 종료하고 다시 시도하세요.

검증: 버전 비교, 다운로드 해시·압축 경로 검증, 보호된 설정 경로 거부, 파일 잠금 시 롤백, 실제 별도 업데이터 프로세스의 종료 대기·파일 교체·재실행, 게임 목록과 데이터 팩 보존.

## English

Added a centered **Update** button, enabled only when a newer stable release is available. Checks run on startup and every six hours. The app downloads a separate update ZIP, verifies SHA-256 and the file manifest, exits normally, replaces program files and restarts. Game profiles, settings and downloaded packs are preserved. Failed replacements roll back changed files; backups remain in the temporary update folder.

Full ZIP, update ZIP and update-manifest.json are separate release assets. The update contains all immutable program files, not binary deltas. **Versions 0.1.5 and earlier need one manual full-ZIP upgrade to 0.1.6; in-app updating works from 0.1.6 onward.**

## 日本語

上部中央に **更新** ボタンを追加しました。新しい正式版がある場合のみ有効になります。起動時と6時間ごとに確認し、専用ZIPのハッシュとファイル一覧を検証してから終了・更新・再起動します。ゲーム一覧、設定、ダウンロード済みデータを保持します。更新失敗時は変更済みファイルを復元します。

**0.1.5以前には更新機能がないため、今回だけ全体ZIPで0.1.6へ移行してください。以降はアプリ内から更新できます。**
