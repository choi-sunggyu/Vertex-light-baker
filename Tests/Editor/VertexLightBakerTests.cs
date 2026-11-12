using System.Collections.Generic;
using VertexLightBaking;
using NUnit.Framework;
using UnityEngine;

// Window > General > Test Runner > EditMode 에서 실행
public class VertexLightBakerTests
{
    readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created)
            if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    // 그림자 지도 좌표: 영역 전체가 지도 안(0~1)에 들어가고, 빛 쪽에 있는 점이 더 "앞"(그림자를 드리우는 쪽)이어야 함
    [Test]
    public void ShadowMatrix_CoversAreaAndOrdersDepthTowardLight()
    {
        var area = new Bounds(new Vector3(10f, 1f, -5f), new Vector3(30f, 4f, 20f));
        var lightDir = new Vector3(0.4f, 0.8f, -0.45f).normalized; // 표면에서 빛 쪽
        var m = BakedShadowMap.ComputeMatrices(lightDir, area, 2048, out _, out _, out float texel);

        for (int i = 0; i < 8; i++)
        {
            var corner = area.center + Vector3.Scale(area.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            var c = m.MultiplyPoint(corner);
            Assert.That(c.x, Is.InRange(0f, 1f), "x");
            Assert.That(c.y, Is.InRange(0f, 1f), "y");
            Assert.That(c.z, Is.InRange(0f, 1f), "z");
        }

        // 빛 쪽으로 옮긴 점은 같은 지도 위치에 찍히고 깊이만 다름 (= 아래 점을 가림)
        var ground = area.center;
        var above = ground + lightDir * 3f;
        var g = m.MultiplyPoint(ground);
        var a = m.MultiplyPoint(above);
        Assert.AreEqual(g.x, a.x, 1e-4f);
        Assert.AreEqual(g.y, a.y, 1e-4f);
        Assert.AreNotEqual(g.z, a.z);
        Assert.Greater(texel, 0f);
    }

    // 원인 재현: 지역 10개(x 0~1800m)가 한 영역에 들어가 픽셀이 수 m 가 됐음. 꺼진 지역은 빠져야 함
    [Test]
    public void ActiveBounds_SkipsInactiveAreas()
    {
        var here = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var farArea = new GameObject("FarArea");
        var far = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(here); created.Add(farArea); created.Add(far);
        far.transform.SetParent(farArea.transform);
        far.transform.position = new Vector3(1800f, 0f, 0f);
        farArea.SetActive(false);

        var renderers = new Renderer[] { here.GetComponent<Renderer>(), far.GetComponent<Renderer>(), null };
        Assert.IsTrue(BakedShadowMap.ActiveBounds(renderers, out var area));
        Assert.Less(area.size.x, 2f);

        here.SetActive(false);
        Assert.IsFalse(BakedShadowMap.ActiveBounds(renderers, out _));
    }

    // 움직이는 물체 그림자 대상: 움직이는 물체 재질 + 그림자 드리우기 켜짐 (실시간 그림자와 같은 규칙)
    [Test]
    public void IsDynamicCaster_UsesDynamicMaterialAndCastSetting()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mat = new Material(Shader.Find("Custom/DynamicToon"));
        created.Add(go); created.Add(mat);
        var r = go.GetComponent<MeshRenderer>();
        Assert.IsFalse(BakedShadowMap.IsDynamicCaster(r)); // 기본 재질 = 정적·기타

        r.sharedMaterial = mat;
        Assert.IsTrue(BakedShadowMap.IsDynamicCaster(r));

        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Assert.IsFalse(BakedShadowMap.IsDynamicCaster(r));
    }

    [Test]
    public void BakedShadowCaster_ShownWhileActive()
    {
        var go = new GameObject("CasterTest");
        created.Add(go);
        var caster = go.AddComponent<BakedShadowCaster>();
        Assert.IsTrue(caster.Shown); // 에디터에서는 켜지면 바로

        go.SetActive(false);
        Assert.IsFalse(caster.Shown);

        go.SetActive(true);
        Assert.IsTrue(caster.Shown);
    }
}
