using System;
using UnityEngine;
using UnityEngine.UI;

public class GameLoose : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button retryButton;

    public void Show(Action onRetry)
    {
        SetVisible(true);

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            if (onRetry != null) retryButton.onClick.AddListener(() => onRetry());
        }
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
