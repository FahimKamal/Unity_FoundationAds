using System;
using System.Collections;
using System.Collections.Generic;
using Pancake.Monetization;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : MonoBehaviour
{
    // Start is called before the first frame update

    public Text logText;

    void Start()
    {
        // ShowBanner();
    }

    public void Log(string log)
    {
        logText.text = log;
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void ShowBanner()
    {
        try
        {
            Advertising.Banner.Show()
                .OnDisplayed(() => { Log("[ADVERTISING]: banner displayed"); })
                .OnClosed(() => { Log("[ADVERTISING]: banner closed"); })
                .OnLoaded(() => { Log("[ADVERTISING]: banner loaded"); })
                .OnFailedToLoad(() => { Log("[ADVERTISING]: banner failed to load"); })
                .OnFailedToDisplay(() => { Log("[ADVERTISING]: banner failed to display"); });
        }
        catch (Exception e)
        {
            Log(e.Message);
            // throw e.StackTrace;
        }
    }

    public void ShowRewardedVideo()
    {
        try
        {
            Advertising.Reward.Show()
                .OnDisplayed(() => { Log("[ADVERTISING]: rewarded displayed"); })
                .OnClosed(() => { Log("[ADVERTISING]: rewarded closed"); })
                .OnLoaded(() => { Log("[ADVERTISING]: rewarded loaded"); })
                .OnFailedToLoad(() => { Log("[ADVERTISING]: rewarded failed to load"); })
                .OnFailedToDisplay(() => { Log("[ADVERTISING]: rewarded failed to display"); })
                .OnCompleted(() => { Log("[ADVERTISING]: rewarded completed"); })
                .OnSkipped(() => { Log("[ADVERTISING]: rewarded skipped"); });
        }
        catch (Exception e)
        {
            Log(e.Message);
            throw;
        }
    }

    public void ShowInterstitial()
    {
        try
        {
            Advertising.Inter.Show()
                .OnDisplayed(() => { Log("[ADVERTISING]: interstitial displayed"); })
                .OnClosed(() => { Log("[ADVERTISING]: interstitial closed"); })
                .OnLoaded(() => { Log("[ADVERTISING]: interstitial loaded"); })
                .OnFailedToLoad(() => { Log("[ADVERTISING]: interstitial failed to load"); })
                .OnFailedToDisplay(() => { Log("[ADVERTISING]: interstitial failed to display"); })
                .OnCompleted(() => { Log("[ADVERTISING]: interstitial completed"); })
                .OnSkipped(() => { Log("[ADVERTISING]: interstitial skipped"); });
        }
        catch (Exception e)
        {
            Log(e.Message);
            throw;
        }
    }
}