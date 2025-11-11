using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

public class StartButton : MonoBehaviour
{
    private float m_startScale;
    private float m_animDuration = 0.1f;
    void Start()
    {
        m_startScale = transform.localScale.x;
    }
    public void StartGame()
    {
        if (PlayerData.instance.ftueLevel == 0)
        {
            PlayerData.instance.ftueLevel = 1;
            PlayerData.instance.Save();
#if UNITY_ANALYTICS
            AnalyticsEvent.FirstInteraction("start_button_pressed");
#endif
        }

#if UNITY_PURCHASING
        var module = StandardPurchasingModule.Instance();
#endif

        transform.DOScale(0, m_animDuration).SetEase(Ease.InBack).OnComplete(() =>
        {
            transform.DOScale(m_startScale, m_animDuration).SetEase(Ease.InBounce).OnComplete(() =>
            {
                SceneManager.LoadScene("main");
            });
        });
    }
}
