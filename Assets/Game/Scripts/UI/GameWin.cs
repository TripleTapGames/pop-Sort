using System;
using UnityEngine;
using UnityEngine.UI;

public class GameWin : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button nextButton;

    public void Show(Action onNext)
    {
        SetVisible(true);

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            if (onNext != null) nextButton.onClick.AddListener(() => onNext());
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
