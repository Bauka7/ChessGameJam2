// CheckWarningUI.cs
using UnityEngine;
using System.Collections;

public class CheckWarningUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panel;     // CheckPanel (объект панели)
    [SerializeField] private CanvasGroup group;    // CanvasGroup на CheckPanel

    [Header("Animation Settings")]
    [SerializeField] private float fadeInTime = 0.25f;
    [SerializeField] private float visibleTime = 1.2f;
    [SerializeField] private float fadeOutTime = 0.25f;

    private Coroutine routine;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (group != null) group.alpha = 0f;
    }

    public void ShowCheck()
    {
        if (panel == null || group == null)
        {
            Debug.LogWarning("[CheckWarningUI] panel/group not assigned!");
            return;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    public void HideImmediate()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (group != null) group.alpha = 0f;
        if (panel != null) panel.SetActive(false);
    }

    private IEnumerator Run()
    {
        panel.SetActive(true);
        group.alpha = 0f;

        // Fade In
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / fadeInTime);
            yield return null;
        }

        group.alpha = 1f;

        // Hold
        yield return new WaitForSeconds(visibleTime);

        // Fade Out
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeOutTime);
            yield return null;
        }

        group.alpha = 0f;
        panel.SetActive(false);
        routine = null;
    }
}