using System;
using UnityEngine;
using UnityEngine.UI;

public class GameLoose : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button retryButton;
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void Show(Action onRetry)
    {
        SetVisible(true);
        if (animator != null)
        {
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.Play("Base Layer.UIWindow_Loose", 0, 0f);
            animator.Update(0f);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            if (onRetry != null) retryButton.onClick.AddListener(() => onRetry());
        }
    }

    public void Hide()
    {
        SetVisible(false);
        if (animator != null) animator.enabled = false;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
