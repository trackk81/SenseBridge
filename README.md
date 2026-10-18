# SenseBridge

Windows용 DualSense USB 보조 프로그램입니다. 게임별로 사용할 기능을 선택하고 **자동 적용 시작**을 누르면, 현재 전면 게임 창에 맞춰 프로필을 적용합니다.

## 기능

- 둥근 게임 카드 UI와 카드별 트리거·햅틱·스피커 설정 창
- Steam 로컬 라이브러리 표지 표시 및 하위 캐시 폴더 검색
- Steam 설치 목록 자동 검색 및 수동 다시 가져오기
- 실행 파일로 게임 추가, 게임별 설정 자동 저장
- 모든 효과 기본값 꺼짐
- 전면 게임에만 적용; 다른 앱으로 이동하면 효과 중지
- 공통 엔진: 출력 장치의 오디오를 이용한 햅틱, L2/R2 저항 설정
- 한국어 / English / 日本語 및 Windows 언어 자동 감지
- 트레이 실행, 설정 초기화, 배포판에서 디버그 UI 숨김
- 실험적 Bloodborne 연결 소스: 무기별 트리거, 아이템 사용음, 내장 공격 햅틱

0.1.4는 AI 분석으로 선별한 발소리·착지음 441개와 일반 충격음 166개를 추가합니다. 기존 57개를 유지해 총 664개 햅틱 파형을 제공합니다. 별도 AI 옵션 없이 오디오 햅틱 체크 하나로 사용하며, 상세 설정에는 전체 강도와 스피커 볼륨만 표시합니다.

지원 목록의 정확한 압축 효과음 식별값과 실제 디코딩 결과를 확인해 재생합니다. 음원 분석은 개발 과정에서 로컬 AI 모델로 수행했으며, 실행 중에는 포함된 캐시를 사용합니다. AI가 모든 소리를 정확히 구분하는 것은 아니며, 같은 소리를 쓰는 주변 캐릭터의 발소리에도 반응할 수 있습니다. 물리적인 충돌 재질을 직접 판독하는 기능은 아닙니다. 일반 게임용 공통 엔진의 동작은 기존과 같습니다.

## 다운로드 — 0.1.4

[Windows x64 실행용 ZIP](https://github.com/trackk81/SenseBridge/releases/tag/v0.1.4)을 내려받아 압축을 풀고 `SenseBridge.exe`를 실행하세요. **배포 ZIP 사용자에게는 아래 빌드 과정, MSYS2, 환경 변수 설정이 필요하지 않습니다.** Windows 10/11 x64와 .NET Framework 4.8을 기준으로 합니다.

0.1.4 ZIP에는 공통 엔진과 필요한 런타임 DLL, 외부 라이선스가 포함됩니다. 갱신된 ZIP에는 Bloodborne 전용 DLL·연결 프로파일·효과음이 포함됩니다. 게임 폴더의 `bin\bb-probe.exe`를 지정하세요. 검증된 실행 파일과 boot-linked.bin의 해시가 일치하는 버전만 연결됩니다. 게임 본체와 세이브는 포함하지 않습니다.

## 현재 배포 범위

**소스 저장소와 포터블 ZIP을 제공합니다. 게임 패키지는 제공하지 않습니다.** 공통 엔진과 UI는 아래 방법으로 빌드할 수 있습니다. Bloodborne 연결은 특정 로컬 bb-probe 빌드에서 검증된 실험 기능으로, 릴리스 ZIP에는 검증된 한 버전의 연결 정보와 효과음이 포함됩니다. 소스 빌드에 필요한 생성 자료는 별도입니다.

게임 파일, 게임 코드의 추출 바이트, 세이브, 추출 음원, 사용자 프로필, 로그, 외부 도구 바이너리는 이 저장소에 포함하지 않습니다. 기존 로컬 설치 폴더와 별도로 정리한 소스입니다.

## 빌드

Windows x64, .NET Framework 4.x C# compiler, PowerShell을 사용합니다.

```powershell
.\build.ps1
```

공통 엔진에는 MSYS2 CLANG64의 clang++, SDL3 개발 파일이 필요합니다. MSYS2에서 `mingw-w64-clang-x86_64-clang` 및 `mingw-w64-clang-x86_64-SDL3` 패키지를 준비합니다.

```powershell
$env:SENSEBRIDGE_TOOLCHAIN = 'C:\msys64\clang64'
.\build-engine.ps1
.\SenseBridge.exe
```

엔진은 지정한 도구 체인의 `bin`에서 DLL을 찾습니다. 도구 체인이 다른 위치이면 **실행할 때도** `SENSEBRIDGE_TOOLCHAIN` 환경 변수를 설정해야 합니다. 빌드 스크립트의 기본 도구 체인 위치는 `C:\msys64\clang64`입니다. 배포 ZIP에서는 실행 파일 옆 DLL을 사용하므로 이 설정이 필요 없습니다. Git 소스에는 외부 바이너리를 커밋하지 않습니다.

## 사용

1. USB로 DualSense 또는 DualSense Edge 한 대를 연결합니다.
2. 게임 카드를 눌러 설정 창을 열고 원하는 기능만 체크합니다. 변경 내용은 자동 저장됩니다.
3. 하단 **자동 적용 시작**을 누릅니다.
4. 게임 창을 전면으로 가져옵니다. 선택한 기능이 있는 등록 게임에만 적용합니다.
5. 중지하려면 **자동 적용 중지**, 프로그램을 끝내려면 **종료하기** 또는 트레이 **종료**를 사용합니다. 창의 X는 트레이로 숨깁니다.

Steam 게임은 설치 폴더로 실행 프로세스를 식별합니다. 런처나 보호된 프로세스 등은 감지하지 못할 수 있으며 실제 게임 실행 파일을 지정해 보완할 수 있습니다. 라이브러리 전체 반복 검색 대신 전면 PID를 100ms 간격으로 확인하고, 창이나 설정이 바뀔 때만 프로필을 다시 확인합니다. 실제 효과 시작에는 장치 초기화 시간이 추가될 수 있습니다.

공통 오디오 햅틱은 **선택한 출력 장치의 전체 소리**에 반응하며 게임별 효과음을 분리하지 않습니다. 공통 트리거 설정은 게임의 발사 판정 위치를 변경하지 않습니다. 컨트롤러 스피커 이벤트 재생은 Bloodborne 전용 기능입니다.

Steam Input 설정을 자동 변경하지 않습니다. 기존 컨트롤러 도구와 동시에 효과를 보내면 충돌할 수 있습니다. 모든 체크를 끄면 SenseBridge 효과를 사용하지 않습니다.

## Bloodborne 개발 연결

목록의 Bloodborne 항목은 초기화 후에도 유지됩니다. **실행 파일 지정**으로 호환되는 `bin\bb-probe.exe`를 지정한 뒤에 옵션이 나타납니다. 일반적인 Steam 게임에는 DLL 연결을 사용하지 않습니다.

이 어댑터는 로컬 검증된 게임 버전에 종속적입니다. 다음 자료를 개발자가 직접 준비해야 하며, 아무 버전에서나 작동한다고 보장하지 않습니다.

- 일치하는 bb-probe 실행 파일과 `out/boot-linked.bin`
- 해당 빌드의 `import_names.inc`
- 실제 사용한 압축 오디오 버퍼 8개(기존 어댑터 event ID 순서)
- 로컬에서 추출한 재생용 음원

```powershell
python source/make_attach_profile.py <game-root> <import_names.inc>
python source/make_item_signatures.py <buffer0> <buffer1> <buffer2> <buffer3> <buffer4> <buffer5> <buffer6> <buffer7>
python source/build_attach.py
```

생성되는 `attach-profile.json`, `source/attach_profile.h`, `source/bloodborne/item_encoded.h`는 로컬 전용이며 Git에서 제외됩니다. 연결 프로파일 생성은 실행 파일 호환성 검증을 대신하지 않습니다. 메모리 구조/무기 판독 위치 역시 버전별 검증이 필요합니다.

어댑터 설정 및 음원은 실행 파일 옆 `Bloodborne/runtime` 아래에 둡니다. 음원 경로는 `source/bloodborne/runtime_dualsense.c`에 정의되어 있습니다. 배포판에서는 이전 설정에 디버그 표시가 켜져 있어도 버튼과 설정 항목이 숨겨집니다. 개발자가 `build.ps1 -Development`로 빌드하여 디버그 실행을 쓰려면 별도로 `Bloodborne/tools/start.ps1`을 준비해야 합니다. 디버그 게임과 세이브는 제공하지 않습니다.

## 검증과 제한

- UI 빌드의 `--self-test`: 설정 저장 및 프로필 직렬화 검사
- 공통 엔진 `--self-test`: DSP 출력 제한과 유한값 검사
- `tests/ForegroundTest.cs`: 전면 프로세스 선택, 경로 경계, 보조 프로세스 제외
- 실제 DualSense USB/Edge 및 특정 Bloodborne 환경에서 수동 확인한 기능을 기반으로 합니다. 모든 Steam 게임을 검증한 것은 아닙니다.
- Bluetooth, 다른 컨트롤러, 게임별 반치트와의 호환성을 보장하지 않습니다.

## 프로젝트 구성

- `source/*.cs`: Windows Forms UI, 프로필 저장, Steam 검색, 전면 창 감지
- `source/generic_engine.cpp`: WASAPI 오디오 캡처 및 SDL3 컨트롤러 출력
- `source/live_bridge.c`, `source/bloodborne/`: 선택적인 Bloodborne 개발 어댑터
- `ui.en.json`, `ui.ja.json`: 번역

## 권리 및 외부 구성요소

게임 및 컨트롤러 명칭은 각 권리자의 상표입니다. Sony, FromSoftware, Valve와 제휴한 프로젝트가 아닙니다. SDL3/MSYS2 등 외부 의존성에는 각 프로젝트의 라이선스가 적용됩니다. 이 첫 공개판에는 별도의 오픈소스 라이선스를 지정하지 않았습니다.

## 자동 햅틱 개발 자료

생성된 `material_encoded.h`, `auto_haptic_catalog.h`, 음원·캐시는 Git에서 제외됩니다. 로컬 게임 자료로 아래 순서로 생성합니다. `vgmstream-cli`는 별도로 준비합니다.

```powershell
$env:SB_SOUND_BANKS = 'D:\Game\CUSA03173\dvdroot_ps4\sound'
python source/index_sound_assets.py
python source/make_material_signatures.py
python source/build_auto_haptics.py --catalog local-sound-work/material-sound-catalog.json --banks $env:SB_SOUND_BANKS --effects Bloodborne/runtime/effects --vgmstream D:\tools\vgmstream-cli.exe --runtime Bloodborne/runtime
python source/build_attach.py
```

위 생성 절차는 기존 57개 기본 캐시를 생성합니다. 0.1.4의 607개 AI 선별 추가분에는 별도 로컬 분석에서 생성한 식별 헤더와 캐시가 필요합니다. 게임 버전이 바뀌면 재검증해야 합니다. 배포 ZIP에는 664개 캐시가 포함되어 있으므로 사용자가 분석하거나 생성할 필요는 없습니다.

0.1.3 검증: 시험용 게임의 패드 체감은 사용자 확인 완료. 배포용 DLL은 별도 어댑터이므로 효과음 파서·57개 캐시·출력 제한·UI 자체 검사를 수행했습니다. 배포 DLL에서 전체 게임 장면을 다시 수동 검증하지는 않았습니다.


## 0.1.4 AI 선별과 검증

504개 뱅크의 70,516개 항목, 중복 제거 후 35,027개 PCM 음원을 로컬 CLAP으로 분석했습니다. 신뢰도·음성/음악 제외·길이·동작 연계 조건으로 607개 추가 효과를 선별했습니다. 금속/나무 등 세부 재질은 모델 판정만으로 확정하지 않습니다. 발소리와 일반 충격음 가족으로 사용합니다. 기존 57개는 AI 대조 후에도 파형을 그대로 보존했습니다.

시험 빌드 체감은 사용자 확인을 받았으며, 배포 DLL은 전체 657개 충돌음/발소리 식별 경로, 아이템 파서, 664개 캐시 로드, 출력 제한, UI/엔진 및 포터블 검사를 통과했습니다. 모든 장면을 다시 수동 검증한 것은 아닙니다. 자세한 변경 내용은 [0.1.4 릴리스 노트](RELEASE-0.1.4.md)를 참고하세요.
