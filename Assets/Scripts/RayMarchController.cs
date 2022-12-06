using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

public class RayMarchController : MonoBehaviour, ILifecycleReceiver
{
    [SerializeField] private RayMarchSettings cfg;
    [SerializeField] private Renderer rend;
    [SerializeField] private string colorPropertyName;

    [ReadOnly] public float spikyness = 0.1f;
    private bool spikeIncreasing = false;

    private OSC osc;

    private Dictionary<int, Action> channelToAction = new Dictionary<int, Action>();
    private float[] lastValues = new float[16 * 128];

    private Material rayMarchMat;
    private float controlledTime;

    #region Shader props

    private static readonly int timeProp = Shader.PropertyToID("_ControlledTime");
    private static readonly int sizeProp = Shader.PropertyToID("_Size");
    private static readonly int spikynessProp = Shader.PropertyToID("_Spikyness");
    private static readonly int redChannelProp = Shader.PropertyToID("_ColorR");
    private static readonly int greenChannelProp = Shader.PropertyToID("_ColorG");
    private static readonly int blueChannelProp = Shader.PropertyToID("_ColorB");
    private static readonly int Hats = Shader.PropertyToID("_Hats");

    #endregion

    private bool initialized;

    public void OnInit(OSC osc)
    {
        Debug.Log("Initializing");
        this.osc = osc;
        rayMarchMat = rend.material;


        osc.SetAddressHandler("/kick", OnKickMsg);
        osc.SetAddressHandler("/snare", OnSnareMsg);

        // not yet tested
        osc.SetAddressHandler("/spike", OnSpikeMsg);
        osc.SetAddressHandler("/color", OnColorMsg);

        if (cfg.MidiEnabled)
        {
            channelToAction[0] = () =>
                StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat, sizeProp, cfg.sizeTime, cfg.sizeCurve, 1f, 0));
            channelToAction[1] = () =>
                StartCoroutine(
                    Ext.AnimateMaterialValue(rayMarchMat, redChannelProp, cfg.sizeTime, cfg.sizeCurve, 1f, 0));
            channelToAction[2] = () => AddSpikyness(0.25f);
        }
        OnReset();
        initialized = true;
    }

    public void OnReset()
    {
        controlledTime = 0;
        rayMarchMat.SetFloat(timeProp, controlledTime);
        rayMarchMat.SetFloat(spikynessProp, cfg.minSpikyness);
    }

    private void OnKickMsg(OscMessage msg)
    {
        OnKick();
    }

    private void OnKick()
    {
        StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat, sizeProp, cfg.sizeTime, cfg.sizeCurve, 1f, 1f));
    }

    private void OnColorMsg(OscMessage msg)
    {
        float x = msg.GetFloat(0);
        StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat,
                                                colorPropertyName,
                                                cfg.colorPushtime,
                                                cfg.colorCurve,
                                                1.0f,
                                                1));
    }

    private void OnSpikeMsg(OscMessage msg)
    {
        float additionalSpikyness = msg.GetFloat(0);
        AddSpikyness(additionalSpikyness);
    }

    private void OnSnareMsg(OscMessage msg)
    {
        OnSnare();
    }

    private void OnSnare()
    {
        AddSpikyness(cfg.addSpikynessOnSignal);
        StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat, redChannelProp, cfg.sizeTime, cfg.sizeCurve, 1f, 0));
    }

    private TweenerCore<float, float, FloatOptions> hatsTweener;

    private void OnHats()
    {
        hatsTweener?.Kill();
        rayMarchMat.SetFloat(Hats, 0);
        hatsTweener = rayMarchMat.DOFloat(1, "_Hats", cfg.hatTime).SetEase(cfg.hatCurve);
        //StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat, "_Hats", cfg.hatTime, cfg.hatCurve, 1f, 0f));
    }

    private void AddSpikyness(float additionalSpikyness)
    {
        var endvalue = spikyness + additionalSpikyness;
        spikeIncreasing = true;
        DOTween.To(() => spikyness, x => spikyness = x, endvalue, additionalSpikyness)
            .OnUpdate(() => rayMarchMat.SetFloat(spikynessProp, spikyness))
            .OnComplete(() => spikeIncreasing = false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized) return;

        if (spikyness > cfg.minSpikyness && !spikeIncreasing)
        {
            spikyness -= Time.deltaTime * cfg.spikeDecreaseSpeed;
            spikyness = Mathf.Clamp(spikyness, cfg.minSpikyness, 12);
            rayMarchMat.SetFloat(spikynessProp, spikyness);
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            OnKick();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            OnSnare();
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            OnHats();
        }

        if (cfg.MidiEnabled)
        {
            Ext.HandleMidiInputPerChannel(channelToAction, lastValues);
        }

        Time.timeScale = cfg.timeScale;
        controlledTime += Time.deltaTime;
        rayMarchMat.SetFloat(timeProp, controlledTime);
    }

    public void OnUnload() { }
}