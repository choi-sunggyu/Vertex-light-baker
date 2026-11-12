using System;
using UnityEngine;

namespace VertexLightBaking
{
    /// <summary>
    /// 실행 중 켜졌다 꺼졌다 하는 오브젝트(해금 오브젝트 등) 표식. 이 오브젝트와 자식은 켜져 있을 때만 그림자를 드리운다.
    /// 켜지거나 꺼지면 저장된 그림자 지도(BakedShadowMap)를 다시 찍게 한다. 매 프레임 비용은 없다.
    /// - 켜질 때: 등장 연출(IsAnimating 이 true 인 동안)이 끝난 뒤 그림자를 넣는다
    /// - 꺼질 때: 꺼지는 순간까지 그림자를 유지하고, 꺼지면 뺀다
    /// 직접 붙이거나, VertexLightBaker.ToggleObjectProviders 에 찾는 함수를 등록하면 굽기 때 자동으로 붙는다.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class BakedShadowCaster : MonoBehaviour
    {
        /// <summary>
        /// 이 Transform 이 등장 연출 중인지. 기본은 연출 없음(바로 그림자 표시).
        /// 예: DOTween 을 쓰면 <c>BakedShadowCaster.IsAnimating = t => DOTween.IsTweening(t);</c>
        /// </summary>
        public static Func<Transform, bool> IsAnimating = _ => false;

        /// <summary>그림자 지도에 이 오브젝트(자식 포함)를 그려도 되는지</summary>
        public bool Shown { get; private set; }

        void OnEnable()
        {
            if (Application.isPlaying)
                StartCoroutine(ShowAfterAppear());
            else
                Show();
        }

        void OnDisable() // 꺼질 때 코루틴은 자동으로 멈춘다
        {
            if (!Shown)
                return;
            Shown = false;
            BakedShadowMap.RequestRender();
        }

        System.Collections.IEnumerator ShowAfterAppear()
        {
            // 등장 연출은 SetActive(true) 바로 다음에 걸리는 경우가 많아 한 프레임 기다린 뒤 확인
            yield return null;
            while (IsAppearing())
                yield return null;
            Show();
        }

        // 자기 자신이나 바깥 표식 오브젝트가 연출 중인지
        bool IsAppearing()
        {
            for (var t = transform; t != null; t = t.parent)
                if ((t == transform || t.GetComponent<BakedShadowCaster>() != null) && IsAnimating(t))
                    return true;
            return false;
        }

        void Show()
        {
            Shown = true;
            BakedShadowMap.RequestRender();
        }
    }
}
