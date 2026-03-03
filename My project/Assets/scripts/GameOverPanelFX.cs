// GameOverPanelFX.cs
// Повесь на GameOverPanel (на нём должен быть CanvasGroup)

using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class GameOverPanelFX : MonoBehaviour
{
    [Header("Fade + Pop")]
    public float fadeIn = 0.20f;       // как ты просил
    public float popTime = 0.18f;
    public float startScale = 0.90f;   // как ты просил

    [Header("Optional: small settle")]
    public float settleTime = 0.10f;
    public float overshootScale = 1.02f;

    private CanvasGroup cg;
    private Coroutine routine;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        // Важно: OnEnable сработает даже если объект был выключен в сцене
        cg.alpha = 0f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        transform.localScale = Vector3.one * startScale;
    }

    // Вызывать, когда показываешь GameOverPanel
    public void PlayShow()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        cg.alpha = 0f;
        transform.localScale = Vector3.one * startScale;

        // Fade In
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeIn);
            cg.alpha = EaseOutCubic(k);
            yield return null;
        }
        cg.alpha = 1f;

        // Pop to overshoot
        t = 0f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popTime);
            transform.localScale = Vector3.Lerp(
                Vector3.one * startScale,
                Vector3.one * overshootScale,
                EaseOutCubic(k)
            );
            yield return null;
        }

        // Settle back to 1.0
        t = 0f;
        while (t < settleTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / settleTime);
            transform.localScale = Vector3.Lerp(
                Vector3.one * overshootScale,
                Vector3.one,
                EaseOutCubic(k)
            );
            yield return null;
        }

        transform.localScale = Vector3.one;
        routine = null;
    }

    private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
}