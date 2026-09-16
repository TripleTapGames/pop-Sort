using System;
using UnityEngine;
using UnityEngine.UI;

public class GameWin : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button nextButton;
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void Show(Action onNext)
    {
        SetVisible(true);
        if (animator != null)
        {
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.Play("Base Layer.UIWindow_Win", 0, 0f);
            animator.Update(0f);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            if (onNext != null) nextButton.onClick.AddListener(() => onNext());
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
