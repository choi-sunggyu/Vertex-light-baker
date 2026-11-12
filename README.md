# Vertex Light Baker

URP 프로젝트에서 **방향광과 실시간 그림자를 끄고도 같은 모습**을 내는 Unity 에디터 도구 + 런타임 셰이더입니다.
빛 방향이 고정된 게임(아이들·타이쿤·퍼즐 등)의 모바일 렌더링 비용을 줄이는 용도입니다.

| 대상 | 밝기(명암) | 그림자 |
|---|---|---|
| 정적 배경 (Static) | 정점마다 미리 구움 (메시 복사본 UV3) | **정적 그림자 지도**: 시작·해금·철거 때만 찍음 |
| 켜졌다 꺼지는 오브젝트 | 위와 같음 (꺼져 있어도 구워 둠) | 정적 지도에 포함, 켜져 있고 등장 연출이 끝난 뒤부터 꺼질 때까지만 |
| 움직이는 물체 (캐릭터·아이템) | 정점 셰이더에서 고정 빛 방향으로 계산 | **동적 그림자 지도**: 움직이는 물체만 매 프레임 찍음 |

배경 셰이더는 두 지도를 같은 좌표로 읽어 더 어두운 쪽을 씁니다. 그래서 움직이는 물체 그림자도 바닥 셰이더 자신의 공식(툰 그림자 색 포함)으로 그려져 정적 그림자와 같은 색이 되고, 그림자끼리 겹쳐도 한 번만 어두워집니다.

## 요구 사항

- Unity 6 (6000.0 이상, 6000.3 에서 개발·확인)
- Universal Render Pipeline 17 이상
- 기기가 `RenderTextureFormat.Shadowmap` 을 지원해야 그림자가 나옵니다 (지원하지 않으면 그림자 없이 그림)

## 설치

Window > Package Manager > **+** > **Add package from git URL...**

```
<이 저장소의 git URL>
```

특정 버전을 고정하려면 URL 끝에 `#v1.0.0` 처럼 태그를 붙입니다.

## 사용 방법

1. 씬에 방향광을 하나 두고 원하는 방향·색으로 맞춥니다.
2. 배경 오브젝트를 **Static** 으로 표시합니다.
3. (권장) Lighting 창에서 **Generate Lighting** 을 한 번 실행합니다 → 씬 환경광이 계산됩니다.
4. 씬을 버전 관리에 커밋합니다 (적용은 씬을 바꿉니다. 원본 재질·모델은 수정하지 않고 복사본을 만듭니다).
5. **Tools > Vertex Light Baker** → **한 번에 적용**.
6. 씬을 저장하고 Play 모드에서 확인합니다.

한 번에 적용이 하는 일
1. 방향광을 끕니다 (방향·색은 굽기 기준으로만 사용).
2. Static 오브젝트의 밝기를 굽고, 씬에 `BakedShadowMap` 을 두어 그림자를 드리울 물체 목록을 채웁니다.
3. 재질을 복사본으로 교체합니다.
   - 정적 + URP Lit 계열 → `Custom/BakedVertexLit`
   - 정적 + 툰 셰이더(이름에 toon) → `Custom/BakedToon` (밝은 색·그림자 색·경계 값을 옮김. Toony Colors Pro 2 속성 이름 지원)
   - 움직이는 물체 + URP Lit 계열 → `Custom/LowCostCharacter`
   - 움직이는 물체 + 툰 셰이더 → `Custom/DynamicToon`
   - 외곽선·기타 커스텀 셰이더 → 그대로 둠 (결과 창에 목록 표시)
4. 실행 중 프리팹에서 생성되는 오브젝트용 재질 짝을 `BakedMaterialRemap` 에 저장합니다.
5. 더 이상 쓰지 않는 구운 메시·재질 복사본을 지웁니다.

## 프로젝트에 연결할 것

### 실행 중 생성되는 오브젝트
프리팹에서 생성하는 곳(풀 포함)에서 **처음 만들 때 한 번** 부릅니다. 재질을 복사본으로 바꾸고 움직이는 물체 그림자에 넣습니다.

```csharp
var go = Instantiate(prefab);
VertexLightBaking.BakedMaterialRemap.Apply(go);
```

### 켜졌다 꺼지는 오브젝트 (해금·철거 등)
켜져 있을 때만 그림자를 드리워야 하는 오브젝트에는 `BakedShadowCaster` 를 붙입니다.
직접 붙이거나, 프로젝트의 타입으로 찾는 함수를 에디터 스크립트에서 등록하면 굽기 때 자동으로 붙습니다.

```csharp
// Editor 폴더
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class MyShadowToggles
{
    static MyShadowToggles() =>
        VertexLightBaker.ToggleObjectProviders.Add(() =>
            Object.FindObjectsByType<MyUnlockable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Select(u => u.gameObject));
}
```

등장 연출(튀어나오기 등)이 끝난 뒤에 그림자를 넣고 싶으면 "연출 중인가" 판정을 연결합니다. DOTween 예:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void HookShadowAppear() =>
    VertexLightBaking.BakedShadowCaster.IsAnimating = t => DG.Tweening.DOTween.IsTweening(t);
```

### 지역·스테이지 전환
여러 지역을 한 씬에 두고 하나만 켜는 구조라면, 전환 직후 다시 찍게 합니다. 그림자 지도는 **그 순간 켜진 물체만** 감싸므로 지역 하나 크기로 선명해집니다.

```csharp
previous.SetActive(false);
next.SetActive(true);
VertexLightBaking.BakedShadowMap.RequestRender();
```

## 창 옵션

| 옵션 | 설명 |
|---|---|
| 방향광 | 굽기 기준 빛. 비워 두면 씬의 주 방향광을 찾음 |
| 씬 환경광 사용 | 씬 환경광(SH)을 법선 방향별로 읽어 원본 URP 와 그늘 밝기를 맞춤. 끄면 "주변광 색" 사용 |
| 그림자 지도 설정 | 끄면 그림자 없이 밝기만 구움 |
| 그림자를 만드는 레이어 | 이 레이어의 물체만 정적 그림자를 드리움 |
| 그림자 지도 해상도 | 정적 지도 한 변 픽셀 수. 2048 = 8MB |
| 그림자 길이 줄이기 | 0 = 빛 방향 그대로, 1 = 바로 위에서. 그림자 방향만 바뀌고 명암은 그대로 |
| 해금 오브젝트 그림자 따로 | `BakedShadowCaster` 가 붙은 오브젝트는 켜져 있을 때만 그림자를 드리움 |
| 그림자 제외 | 넣은 오브젝트(자식 포함)는 그림자를 드리우지 않음 |
| 저장 폴더 | 메시·재질 복사본 위치 (기본 `Assets/BakedMeshes`) |

`BakedShadowMap` 컴포넌트

| 필드 | 설명 |
|---|---|
| Resolution / Dynamic Resolution | 정적 지도(기본 2048) / 동적 지도(기본 1024, 매 프레임) 크기 |
| Depth Bias / Normal Bias | 픽셀 크기 대비 밀기. 표면 얼룩이 보이면 올리고, 그림자가 물체에서 떨어져 보이면 내림 |

배경 셰이더의 **Debug: Show Baked Light** 를 켜면 흰색 = 빛 받음, 검정 = 그림자·뒷면으로 보입니다.

## 실시간 그림자와 비교

| | 실시간 (URP) | Vertex Light Baker |
|---|---|---|
| 매 프레임 그림자 지도에 그리는 것 | 그림자 거리 안의 모든 물체 × 캐스케이드 수 | 움직이는 물체만, 1장 |
| 바닥 픽셀의 그림자 계산 | 캐스케이드 선택 + 부드러운 그림자 4~9회 읽기 | 지도 2장을 각 1회 읽기 |
| 빛 계산 | 픽셀마다 | 배경은 미리 구움, 움직이는 물체는 정점마다 |
| 먼 곳 그림자 | 그림자 거리·캐스케이드에 따라 흐려지거나 끊김 | 켜진 영역 전체가 같은 해상도 |

제한
- 빛 방향이 고정입니다 (바꾸면 다시 굽기).
- 움직이는 물체는 그림자를 **받지** 않습니다.
- 원래 셰이더를 그대로 쓰는 물체(외곽선 등)는 그림자가 없습니다.
- 메모리: 정적 8MB + 동적 2MB 상시 (기본 해상도 기준).
- 정적 지도를 다시 찍는 순간(해금·전환) 배경이 매우 많으면 짧은 끊김이 생길 수 있습니다.

## 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| 편집 화면에서 그림자가 흐리고 떠 보임 | 편집 중에는 모든 지역이 켜져 있어 영역이 넓음. Play 모드에서 확인 |
| Play 중에도 그림자가 흐리거나 떠 보임 | 멀리 있는 정적 물체가 영역을 넓힘. "그림자 제외"에 넣거나 Static 해제. 지역 전환 시 `RequestRender()` 호출 확인 |
| 표면에 얼룩·줄무늬 | Depth Bias / Normal Bias 를 조금 올림 |
| 배경이 검게 보임 | 굽지 않은 메시에 배경 셰이더가 쓰임. 다시 굽기 |
| 생성된 물체가 원래 색이거나 그림자 없음 | 생성 지점에서 `BakedMaterialRemap.Apply` 를 부르지 않음 |
| 해금 오브젝트 그림자가 미리 보임 / 안 사라짐 | 해당 오브젝트에 `BakedShadowCaster` 가 없음 |
| 화면 밖 캐릭터 그림자 자세가 멈춤 | 화면 밖 스킨 메시는 애니메이션 갱신을 건너뜀. 필요하면 Update When Offscreen 켜기 |

## 테스트

`Packages/manifest.json` 에 다음을 추가하면 Test Runner(EditMode)에 `VertexLightBaking.Editor.Tests` 가 나타납니다.

```json
"testables": ["com.vlb.vertex-light-baker"]
```

## 라이선스

MIT — [LICENSE.md](LICENSE.md)
