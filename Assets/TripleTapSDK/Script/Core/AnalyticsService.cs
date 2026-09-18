using System.Collections.Generic;
using System;
using UnityEngine;
using Facebook.Unity;
// using Singular;
using Firebase.Analytics;

namespace TripleTapSDK
{
    public class TTAnalyticsService : MonoBehaviour
    {
        private const string FirstLoginDateKey = "FirstLoginDate";
        private const string LastLoginDayKey = "LastLoginDay";

        [SerializeField] private TTAdsService adsService;

        private void Start()
        {
            if (adsService != null)
            {
                adsService.AdRevenuePaid += OnAdRevenuePaid;
            }
            Invoke(nameof(CalculateRetention), 2.0f);
        }

        private void OnDestroy()
        {
            if (adsService != null)
            {
                adsService.AdRevenuePaid -= OnAdRevenuePaid;
            }
        }

        private void CalculateRetention()
        {
            try
            {
                if (PlayerPrefs.HasKey(FirstLoginDateKey))
                {
                    // Retrieve stored first login date (in UTC)
                    string storedDate = PlayerPrefs.GetString(FirstLoginDateKey);
                    DateTime firstLoginDateUtc = DateTime.Parse(storedDate, null,
                                            System.Globalization.DateTimeStyles.RoundtripKind);

                    // Get today's date (UTC) without time
                    DateTime currentDateUtc = DateTime.UtcNow.Date;

                    // Calculate the difference in days (ignores hours/min/seconds)
                    int daysSinceFirstLogin = (currentDateUtc - firstLoginDateUtc).Days;

                    Debug.Log($"[TTAnalyticsService] Days since first login: {daysSinceFirstLogin}");

                    if (PlayerPrefs.GetInt(LastLoginDayKey, -1) != daysSinceFirstLogin)
                    {
                        try
                        {
                            string retentionEvent = $"day_{daysSinceFirstLogin}_retention";
                            // SingularSDK.Event(retentionEvent);
                            TTManager.Instance.GAService?.LogDesignEvent(retentionEvent);
                            FB.LogAppEvent(retentionEvent);
                            FirebaseAnalytics.LogEvent(retentionEvent);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[TTAnalyticsService] Retention event failed: {e}");
                        }

                        PlayerPrefs.SetInt(LastLoginDayKey, daysSinceFirstLogin);
                    }

                }
                else
                {
                    // If it's the first login, store just the date portion in UTC
                    DateTime todayUtc = DateTime.UtcNow.Date;

                    // Store as an ISO-8601 string with no time component (because we used .Date)
                    // Round-trip specifier "o" is still fine here; it just ensures consistent reads/writes
                    PlayerPrefs.SetString(FirstLoginDateKey, todayUtc.ToString("o"));
                    PlayerPrefs.Save();

                    Debug.Log($"[TTAnalyticsService] First login date stored (UTC): {todayUtc}");

                    if (PlayerPrefs.GetInt(LastLoginDayKey, -1) != 0)
                    {
                        try
                        {
                            // SingularSDK.Event("day_0_retention");
                            TTManager.Instance.GAService?.LogDesignEvent("day_0_retention");
                            FB.LogAppEvent("day_0_retention");
                            FirebaseAnalytics.LogEvent("day_0_retention");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[TTAnalyticsService] Retention event failed: {e}");
                        }

                        PlayerPrefs.SetInt(LastLoginDayKey, 0);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTAnalyticsService] Retention calculation failed: {e}");
            }
        }

        // Handle centralized ad revenue analytics
        private void OnAdRevenuePaid(String ad_platform, string ad_source, string ad_format, double value, MaxSdkBase.AdInfo adInfo)
        {
            try
            {
                float roundedValue = (float)Math.Round(value, 5);
                // Facebook App Event for impression + purchase-style event
                var softPurchaseParameters = new Dictionary<string, object>
                {
                    { "ad_platform", ad_platform },
                    { "ad_source",  ad_source },
                    { "ad_format", ad_format },
                    { "fb_currency", "USD" },
                    { "_valueToSum", roundedValue },
                    { "value", value }
                };
                FB.LogAppEvent("AdImpression", roundedValue, softPurchaseParameters);

                FB.LogPurchase((float)value, "USD",
                    new Dictionary<string, object>
                    {
                        { "ad_platform", ad_platform },
                        { "ad_source", ad_source },
                        { "ad_format", ad_format },
                        { "currency", "USD" },
                        { "_valueToSum", roundedValue },
                        { "value", value }
                    });

                // Threshold tracking for cumulative revenue
              
                // Firebase ad impression
                var impressionParameters = new[]
                {
                    new Parameter("ad_platform", ad_platform),
                    new Parameter("ad_source", ad_source),
                    new Parameter("ad_unit_name", adInfo.AdUnitIdentifier),
                    new Parameter("ad_format", ad_format),
                    new Parameter("value", adInfo.Revenue),
                    new Parameter("currency", "USD"),
                };
                FirebaseAnalytics.LogEvent("ad_impression", impressionParameters);

                // GameAnalytics design event
                TTManager.Instance.GAService?.LogAdRevenue(ad_format, (float)value);

               // Singular ad revenue
                // var adData = new SingularAdData(ad_source, "USD", roundedValue);
                // SingularSDK.AdRevenue(adData);

                  CheckAndFireRevenueThresholdEvents(adInfo.Revenue);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTAnalyticsService] OnAdRevenuePaid failed: {e}");
            }
        }

        /// <summary>
        /// Accumulate ad revenue into PlayerPrefs and fire a Facebook event once when
        /// cumulative revenue crosses configured thresholds.
        /// </summary>
        private void CheckAndFireRevenueThresholdEvents(double revenue)
        {
            try
            {
                float total = PlayerPrefs.GetFloat("TotalAdRevenue", 0f);
                total += (float)revenue;
                PlayerPrefs.SetFloat("TotalAdRevenue", total);
                PlayerPrefs.Save();

                double[] thresholds = new double[] { 0.1, 0.2, 0.5, 1.0, 1.5, 2.0, 3.0 };

                foreach (var t in thresholds)
                {
                    string key = $"RevenueReached_{t.ToString("0.0")}";
                    if (total >= (float)t && PlayerPrefs.GetInt(key, 0) == 0)
                    {
                        try
                        {
                            var parameters = new Dictionary<string, object>
                            {
                                { "threshold", t },
                                { "total_revenue", total }
                            };
                            FB.LogAppEvent("AdRevenue_ThresholdReached", total, parameters);
                            string eventName = "AdRevenueThreshold_" + (double)t;
                            FB.LogAppEvent(eventName);


                            TTManager.Instance.GAService?.LogAdRevenueThreshold(t);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[TTAnalyticsService] Failed to log threshold event for {t}: {e}");
                        }

                        PlayerPrefs.SetInt(key, 1);
                        PlayerPrefs.Save();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTAnalyticsService] CheckAndFireRevenueThresholdEvents failed: {e}");
            }
        }

        public void LogLevelDesignEvent(string eventId)
        {
            try
            {
                TTManager.Instance.GAService?.LogDesignEvent(eventId);
                FB.LogAppEvent(eventId);
              
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTAnalyticsService] LogDesignEvent failed: {e}");
            }
        }

        private static readonly HashSet<int> MilestoneLevels = new() { 5, 10, 15, 20 };

        public void LogMilestoneLevelCompleted(int levelNumber)
        {
            if (!MilestoneLevels.Contains(levelNumber)) return;

            string key = $"MilestoneLevel_{levelNumber}";
            if (PlayerPrefs.GetInt(key, 0) == 1) return;

            try
            {
                string eventId = $"level_{levelNumber}_completed";
                TTManager.Instance.GAService?.LogDesignEvent(eventId);
                FB.LogAppEvent(eventId);
                FB.LogAppEvent(
                    AppEventName.UnlockedAchievement,
                    null,
                    new Dictionary<string, object>
                    {
                        { AppEventParameterName.Description, eventId }
                    });
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();

                // SingularSDK.Event(eventId);

                FirebaseAnalytics.LogEvent(eventId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTAnalyticsService] LogMilestoneLevelCompleted failed: {e}");
            }
        }

        
    }
}