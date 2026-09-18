using GameAnalyticsSDK;
using UnityEngine;

namespace TripleTapSDK
{
    /// <summary>
    /// Service for GameAnalytics events.
    /// </summary>
    public class TTGAService : MonoBehaviour
    {
        /// <summary>
        /// Log ad revenue design event.
        /// </summary>
        public void LogAdRevenue(string adFormat, float value)
        {
            try
            {
                GameAnalytics.NewDesignEvent($"_Ads:AdRevenue:{adFormat}:value", value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogAdRevenue failed: {e}");
            }
        }

        /// <summary>
        /// Log ad revenue threshold event.
        /// </summary>
        public void LogAdRevenueThreshold(double threshold)
        {
            try
            {
                GameAnalytics.NewDesignEvent($"_Ads:AdRevenueThreshold_:{threshold}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogAdRevenueThreshold failed: {e}");
            }
        }

        /// <summary>
        /// Log a custom design event with a value.
        /// </summary>
        public void LogDesignEvent(string eventId, float value)
        {
            try
            {
                GameAnalytics.NewDesignEvent(eventId, value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogDesignEvent failed: {e}");
            }
        }

        /// <summary>
        /// Log a custom design event without a value.
        /// </summary>
        public void LogDesignEvent(string eventId)
        {
            try
            {
                GameAnalytics.NewDesignEvent(eventId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogDesignEvent failed: {e}");
            }
        }

        /// <summary>
        /// Log progression start event.
        /// </summary>
        public void LogProgressionStart(string progression01, string progression02 = null, string progression03 = null)
        {
            try
            {
                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, progression01, progression02, progression03);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogProgressionStart failed: {e}");
            }
        }

        /// <summary>
        /// Log progression complete event.
        /// </summary>
        public void LogProgressionComplete(string progression01, string progression02 = null, string progression03 = null)
        {
            try
            {
                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, progression01, progression02, progression03);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogProgressionComplete failed: {e}");
            }
        }

        /// <summary>
        /// Log progression complete event with score.
        /// </summary>
        public void LogProgressionComplete(string progression01, int score, string progression02 = null, string progression03 = null)
        {
            try
            {
                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, progression01, progression02, progression03, score);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogProgressionComplete failed: {e}");
            }
        }

        /// <summary>
        /// Log progression fail event.
        /// </summary>
        public void LogProgressionFail(string progression01, string progression02 = null, string progression03 = null)
        {
            try
            {
                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, progression01, progression02, progression03);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogProgressionFail failed: {e}");
            }
        }

        /// <summary>
        /// Log progression fail event with score.
        /// </summary>
        public void LogProgressionFail(string progression01, int score, string progression02 = null, string progression03 = null)
        {
            try
            {
                GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, progression01, progression02, progression03, score);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TTGAService] LogProgressionFail failed: {e}");
            }
        }
    }
}
