using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class RulePanelAnimator : MonoBehaviour
{
    [Header("Animation")]
    public float fadeInTime = 0.18f;
    public float holdTime = 2.2f;
    public float fadeOutTime = 0.22f;

    [Header("Pop (scale)")]
    public float popStartScale = 0.92f;
    public float popEndScale = 1.00f;

    private CanvasGroup cg;
    private Coroutine routine;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        // Сразу прячем
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        transform.localScale = Vector3.one * popStartScale;
    }

    /// <summary>
    /// Показать панель плавно, подержать и скрыть.
    /// </summary>
    public void ShowOnce()
    {
        gameObject.SetActive(true);

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowOnceRoutine());
    }

    /// <summary>
    /// Shows panel and keeps it open until HideNow is called.
    /// </summary>
    public void ShowPersistent(bool interactable = true)
    {
        gameObject.SetActive(true);

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        cg.alpha = 1f;
        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;
        transform.localScale = Vector3.one * popEndScale;
    }

    /// <summary>
    /// Hides panel immediately.
    /// </summary>
    public void HideNow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private IEnumerator ShowOnceRoutine()
    {
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Fade In + Pop
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeInTime <= 0f ? 1f : Mathf.Clamp01(t / fadeInTime);

            cg.alpha = Mathf.Lerp(0f, 1f, EaseOutCubic(k));
            transform.localScale = Vector3.one * Mathf.Lerp(popStartScale, popEndScale, EaseOutCubic(k));
            yield return null;
        }
        cg.alpha = 1f;
        transform.localScale = Vector3.one * popEndScale;

        // Hold
        if (holdTime > 0f)
            yield return new WaitForSecondsRealtime(holdTime);

        // Fade Out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeOutTime <= 0f ? 1f : Mathf.Clamp01(t / fadeOutTime);

            cg.alpha = Mathf.Lerp(1f, 0f, EaseInCubic(k));
            yield return null;
        }
        cg.alpha = 0f;

        routine = null;
        gameObject.SetActive(false);
    }

    private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private float EaseInCubic(float x) => x * x * x;
}
