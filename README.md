# RimWorld Goal Planner

A RimWorld mod that lets you set goals and shows everything you need to reach them: prerequisite research, workbenches, and resources. Plan ahead and stockpile materials before you start building.

## Features

- **Multiple goals**: Add as many goals as you like (e.g. "Build a shuttle") and open each one to see its full breakdown.
- **Prerequisite research**: The full research chain the goal depends on, including the research benches and facilities those projects require (e.g. hi-tech research bench, multi-analyzer).
- **Prerequisite buildings**: Buildings and furniture you have to build first, with their own material costs.
- **Total resources**: A combined bill of materials for the goal and everything it depends on (e.g. steel, components, plasteel).
- **Fuel calculation**: How much fuel a refuelable building needs to fill up completely (e.g. chemfuel for a shuttle).

## Requirements

- RimWorld **1.7** (Waylith Engine, currently on the Steam unstable beta branch)
- No other mods required. Goal Planner only reads game data and does not patch game code, so it does not need Harmony.

## Tech Stack

| Area | Technology |
|---|---|
| Language | C# (latest), XML |
| Runtime | .NET 10 (`net10.0`), matching RimWorld 1.7's runtime |
| Build | .NET 10 SDK (`dotnet build`) |
| Game APIs | `Assembly-CSharp.dll` (Verse / RimWorld), `UnityStubs.dll` (Unity types such as `Rect` and `Vector2`) |
| Reverse engineering | ILSpy (`ilspycmd`), for reading the game's code |

How it works:
- **Data source**: Reads definitions (Defs) loaded in the game, so values from patches, DLCs and other mods are included automatically. Examples: `BuildableDef.CostList`, `researchPrerequisites`, `ResearchProjectDef.requiredResearchBuilding` / `requiredResearchFacilities`, `CompProperties_Refuelable.fuelCapacity`.
- **Saving goals**: A `GameComponent` stores goals in the save file.
- **UI**: A `MainTabWindow` adds a "Goals" button to the bottom bar.

## Project Structure

```
About/About.xml          Mod metadata
Defs/                    XML definitions (main tab button)
Languages/               English and Korean translations
Source/GoalPlanner/      C# project
Assemblies/              Build output (GoalPlanner.dll, not committed)
```

## Building

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Build:
   ```sh
   cd Source/GoalPlanner
   dotnet build -c Release
   ```
   By default the project references the game at `~/Library/Application Support/Steam/steamapps/common/RimWorld` (macOS Steam). Use a different install with `-p:RimWorldDir=/path/to/RimWorld`.
3. Link the repository into the game's `Mods` folder:
   ```sh
   ln -s "$(pwd)/../.." ~/Library/Application\ Support/Steam/steamapps/common/RimWorld/Mods/GoalPlanner
   ```

## Installation

Subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3841892388).

## Development

This mod was written with [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant.

## License

This project is licensed under the [MIT License](LICENSE).

# 림월드 목표 플래너

림월드에서 목표를 설정하면 그 목표를 이루는 데 필요한 선행 연구, 작업대, 자원을 한눈에 보여주는 모드입니다. 건설을 시작하기 전에 필요한 자원을 미리 준비해 둘 수 있습니다.

## 주요 기능

- **여러 개의 목표**: 목표(예: "우주 왕복선 만들기")를 원하는 만큼 추가하고, 각 목표를 눌러 필요한 내용을 자세히 볼 수 있습니다.
- **선행 연구**: 목표에 필요한 연구를 처음부터 끝까지 보여주고, 그 연구에 필요한 연구대와 시설(예: 첨단 연구대, 복합 분석기)도 함께 알려줍니다.
- **선행 건물**: 먼저 지어야 하는 건물과 가구, 그리고 각각의 건설 재료를 보여줍니다.
- **총 필요 자원**: 목표와 선행 건물에 들어가는 자원을 모두 합산해 보여줍니다(예: 강철, 부품, 플라스틸).
- **연료 계산**: 연료를 쓰는 건물을 가득 채우는 데 필요한 연료량을 계산합니다(예: 우주 왕복선의 화학 연료).

## 요구 사항

- 림월드 **1.7** (Waylith Engine, 현재 Steam unstable 베타 브랜치)
- 다른 모드는 필요하지 않습니다. 게임 데이터를 읽기만 하고 게임 코드를 수정하지 않기 때문에 Harmony도 필요 없습니다.

## 기술 스택

| 분야 | 기술 |
|---|---|
| 언어 | C# (최신 버전), XML |
| 런타임 | .NET 10 (`net10.0`), 림월드 1.7 런타임과 동일 |
| 빌드 | .NET 10 SDK (`dotnet build`) |
| 게임 API | `Assembly-CSharp.dll` (Verse / RimWorld), `UnityStubs.dll` (`Rect`, `Vector2` 같은 Unity 타입) |
| 게임 코드 분석 | ILSpy (`ilspycmd`) |

동작 방식:
- **데이터 출처**: 게임에 로드된 정의(Def)를 직접 읽습니다. 그래서 패치, DLC, 다른 모드의 수치도 자동으로 반영됩니다. 예: `BuildableDef.CostList`, `researchPrerequisites`, `ResearchProjectDef.requiredResearchBuilding` / `requiredResearchFacilities`, `CompProperties_Refuelable.fuelCapacity`.
- **목표 저장**: `GameComponent`가 목표를 세이브 파일에 저장합니다.
- **UI**: `MainTabWindow`로 하단 바에 "목표" 버튼을 추가합니다.

## 프로젝트 구조

```
About/About.xml          모드 정보
Defs/                    XML 정의 (메인 탭 버튼)
Languages/               영어, 한국어 번역
Source/GoalPlanner/      C# 프로젝트
Assemblies/              빌드 결과물 (GoalPlanner.dll, 커밋하지 않음)
```

## 빌드 방법

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)를 설치합니다.
2. 빌드합니다.
   ```sh
   cd Source/GoalPlanner
   dotnet build -c Release
   ```
   기본적으로 `~/Library/Application Support/Steam/steamapps/common/RimWorld`(macOS Steam)에 있는 게임을 참조합니다. 다른 경로에 설치했다면 `-p:RimWorldDir=/path/to/RimWorld`로 지정합니다.
3. 저장소를 게임의 `Mods` 폴더에 링크합니다.
   ```sh
   ln -s "$(pwd)/../.." ~/Library/Application\ Support/Steam/steamapps/common/RimWorld/Mods/GoalPlanner
   ```

## 설치 방법

[Steam 창작마당](https://steamcommunity.com/sharedfiles/filedetails/?id=3841892388)에서 구독하세요.

## 개발

이 모드는 Anthropic의 AI 코딩 도구인 [Claude Code](https://claude.com/claude-code)로 작성했습니다.

## 라이선스

이 프로젝트는 [MIT 라이선스](LICENSE)를 따릅니다.
