using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine.SceneManagement;

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
    private static readonly int RingDistortion = Shader.PropertyToID("_RingDistortion");
    private static readonly int colorPushProp = Shader.PropertyToID("_ColorPush");
    private static readonly int redChannelProp = Shader.PropertyToID("_ColorR");
    private static readonly int greenChannelProp = Shader.PropertyToID("_ColorG");
    private static readonly int blueChannelProp = Shader.PropertyToID("_ColorB");

    #endregion

    private bool initialized;
    
    void Awake()
    {
        var scene = SceneManager.GetSceneByName("StartUp");
        if (!scene.IsValid())
        {
            OnInit(null);
        }
    }

    public void OnInit(OSC osc)
    {
        if (initialized)
        {
            Debug.LogError("This should not happen");
            return;
        }
        
        rayMarchMat = rend.material;

        if (osc != null)
        {
            this.osc = osc;
            osc.SetAddressHandler("/kick", OnKickMsg);
            osc.SetAddressHandler("/snare", OnSnareMsg);
            osc.SetAddressHandler("/hihat", OnHatsMsg);
        }

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

    private Sequence colorPushSequence;
    private void OnColor()
    {
        colorPushSequence?.Kill();
        
        rayMarchMat.SetFloat(redChannelProp, 0);
        rayMarchMat.SetFloat(greenChannelProp, 0);
        rayMarchMat.SetFloat(blueChannelProp, 0);
        
        colorPushSequence = DOTween.Sequence()
            .Append(rayMarchMat.DOFloat(cfg.colorPushStrength, redChannelProp, cfg.colorPushtime).SetEase(cfg.colorCurveR))
            .Join(rayMarchMat.DOFloat(cfg.colorPushStrength, greenChannelProp, cfg.colorPushtime).SetEase(cfg.colorCurveG))
            .Join(rayMarchMat.DOFloat(cfg.colorPushStrength, blueChannelProp, cfg.colorPushtime).SetEase(cfg.colorCurveB))
            .Play();

    }

    private void OnHatsMsg(OscMessage msg)
    {
        OnHihats();
    }

    private void OnSnareMsg(OscMessage msg)
    {
        OnSnare();
    }

    private void OnHihats()
    {
        AddSpikyness(cfg.addSpikynessOnSignal);
        StartCoroutine(Ext.AnimateMaterialValue(rayMarchMat, redChannelProp, cfg.sizeTime, cfg.sizeCurve, 1f, 0));
    }

    private TweenerCore<float, float, FloatOptions> ringDistortionTweener;

    private void OnSnare()
    {
        ringDistortionTweener?.Kill();
        rayMarchMat.SetFloat(RingDistortion, 0);
        ringDistortionTweener = rayMarchMat.DOFloat(1, "_RingDistortion", cfg.hatTime).SetEase(cfg.hatCurve);
    }

    private void AddSpikyness(float additionalSpikyness)
    {
        var endvalue = spikyness + additionalSpikyness;
        spikeIncreasing = true;
        DOTween.To(() => spikyness, x => spikyness = x, endvalue, additionalSpikyness)
            .OnUpdate(() => rayMarchMat.SetFloat(spikynessProp, spikyness))
            .OnComplete(() => spikeIncreasing = false);
    }

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
            OnHihats();
            OnColor();
        }

        if (cfg.MidiEnabled)
        {
            Ext.HandleMidiInputPerChannel(channelToAction, lastValues);
        }

        Time.timeScale = cfg.timeScale;
        controlledTime += Time.deltaTime;
        rayMarchMat.SetFloat(timeProp, controlledTime);
    }

    public void OnUnload()
    {
        if (osc)
        {
            osc.RemoveAllMessageHandlers();
        }
    }
}