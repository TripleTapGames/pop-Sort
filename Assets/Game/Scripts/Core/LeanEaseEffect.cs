using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
public class LeanEaseEffect : MonoBehaviour
{
    [Header("Normal Loop")]
    public float loopDuration;
    [Header("First Time Delay")]
    public float delay;
    [Header("Scale Duration")]
    public float scaleDuration = 0.5f;
    [Header("Loop Count")]
    public int loopCount = 100;
    [Header("Loop Delay")]
    public float loopDelay = 5;

    public Vector3 scaleValue = Vector3.one;
    public Vector3 initialScaleValue = Vector3.zero;
    public bool loop;
    public bool customLoop;
    public AnimationCurve graph;


    private CancellationTokenSource _cancellationTokenSource;
    private void OnEnable()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        gameObject.transform.localScale = initialScaleValue;

        if(customLoop)
        {
            CustomEffect();
           
        }
        else
        {
            Open();
        }
        
    }

    async void Open()
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), false, cancellationToken: _cancellationTokenSource.Token);
            if (!loop)
            {
                LeanTween.scale(gameObject, scaleValue, scaleDuration).setEase(graph);
            }
            else
            {
                LeanTween.scale(gameObject, scaleValue, scaleDuration).setLoopPingPong(loopCount);

                if (!customLoop)
                {
                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(loopDuration), false, cancellationToken: _cancellationTokenSource.Token);
                        LeanTween.cancel(gameObject);
                    }
                    catch (OperationCanceledException e)
                    {
                        Debug.Log(e.Message);
                    }
                }



            }
        }
        catch (OperationCanceledException e)
        {
            Debug.Log(e.Message);
        }
    }

    async void CustomEffect()
    {
        int i = 0;
       
        while( i != 1)
        {
            Open();
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(loopDelay), false, cancellationToken: _cancellationTokenSource.Token);
                LeanTween.cancel(gameObject);
            }
            catch (OperationCanceledException e)
            {
                i = 1;
                Debug.Log(e.Message);
            }
        }
    }

    private void OnDisable()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        LeanTween.cancel(gameObject);
        gameObject.transform.localScale = initialScaleValue;
    }


}
