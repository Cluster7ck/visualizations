using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class RayMarchController : MonoBehaviour
{
    public OSC osc;

    [SerializeField] private Renderer rend;
    [SerializeField] private AnimationCurve curve;
    [Header("ColorPush")]
    [SerializeField] private string colorPropertyName;
    [SerializeField] private AnimationCurve colorCurve;
    [SerializeField] private float colorPushtime;
    [Header("SpikePush")]
    [SerializeField] private AnimationCurve spikeIncreaseCurve;
    [SerializeField] private float spikeDecreaseSpeed;
    private const float minSpikyness = 0.1f;
    [SerializeField] private float addSpikyness = 0.1f;
     private float spikyness = 0.1f;
    private bool spikeIncreasing = false;

    [Header("Size")]
    [SerializeField] private float sizeTime = 0.2f;

    private Dictionary<int, Action> channelToAction = new Dictionary<int, Action>();
    private float[] lastValues = new float[16*128];

    private void Start()
    {
        rend.material.SetFloat("_Spikyness", minSpikyness);
        osc.SetAddressHandler("/kick", OnSpeedMsg);
        osc.SetAddressHandler("/color", OnColorMsg);
        osc.SetAddressHandler("/spike", OnSpikeMsg);
        osc.SetAddressHandler("/snare", OnLobMsg);

        channelToAction[0] = () => StartCoroutine(AnimateValue("_Size", sizeTime, curve, 1f, 0));
        channelToAction[1] = () => StartCoroutine(AnimateValue("_ColorPowerG", sizeTime, curve, 1f, 0));
        channelToAction[2] = () => AddSpikyness(0.25f);
    }

    private void OnSpeedMsg(OscMessage msg)
    {
        StartCoroutine(AnimateValue("_Size", sizeTime, curve, 1f, 0));
    }

    private void OnColorMsg(OscMessage msg)
    {
        float x = msg.GetFloat(0);
        StartCoroutine(AnimateValue(colorPropertyName, colorPushtime, colorCurve, 1.0f, 1));
    }

    private void OnSpikeMsg(OscMessage msg)
    {
        float additionalSpikyness = msg.GetFloat(0);
        AddSpikyness(additionalSpikyness);
    }

    private void OnLobMsg(OscMessage msg)
    {
        AddSpikyness(addSpikyness);
        StartCoroutine(AnimateValue("_ColorPowerG", sizeTime, curve, 1f, 0));
    }

    private void AddSpikyness(float additionalSpikyness)
    {
        var endvalue = spikyness + additionalSpikyness;
        spikeIncreasing = true;
        DOTween.To(() => spikyness, x => spikyness = x, endvalue, additionalSpikyness).OnComplete(() => spikeIncreasing = false);
    }

    // Update is called once per frame
    void Update()
    {
        if(spikeIncreasing)
        {
            rend.material.SetFloat("_Spikyness", spikyness);
        }
        if(spikyness > minSpikyness && !spikeIncreasing)
        {
            spikyness -= Time.deltaTime * spikeDecreaseSpeed;
            spikyness = Mathf.Clamp(spikyness, minSpikyness, 12);
            rend.material.SetFloat("_Spikyness", spikyness);
        }
        if(Input.GetKeyDown(KeyCode.Space))
        {
            AddSpikyness(0.4f);
            StartCoroutine(AnimateValue("_ColorPowerR", colorPushtime, colorCurve, 1.0f, 1));
            StartCoroutine(AnimateValue("_ColorPowerG", colorPushtime, colorCurve, 1.0f, 1));
            StartCoroutine(AnimateValue("_ColorPowerB", colorPushtime, colorCurve, 1.0f, 1));
        }

        for(int ch = 0; ch < 16; ch++)
        {
            for(int note = 0; note < 128; note++)
            {
                var down = MidiJack.MidiMaster.GetKeyDown((MidiJack.MidiChannel)ch, note);
                var val = MidiJack.MidiMaster.GetKey((MidiJack.MidiChannel)ch, note);
                var lastValIdx = ch * 128 + note;
                var lastValue = lastValues[lastValIdx];
                if(down)
                {
                    channelToAction[ch]();
                }
                else if(lastValue == 0 && val > 0)
                {
                    channelToAction[ch]();
                }
                lastValues[lastValIdx] = val;
            }
        }
    }

    private IEnumerator AnimateValue(string value, float targetTime, AnimationCurve curve, float curveMult, float offset)
    {
        float timeStep = 0.01f;
        float time = 0;

        while(time < targetTime)
        {
            var val = curve.Evaluate(time/targetTime) * curveMult + offset;
            rend.material.SetFloat(value, val);

            yield return new WaitForSeconds(timeStep);
            time += timeStep;
        }
    }

    private IEnumerator SmoothIncrease(Action<float> adder, float increase, float targetTime)
    {
        float timeStep = 0.01f;
        float time = 0;
        float step = increase/(targetTime/timeStep);

        while(time < targetTime)
        {
            adder(step);

            yield return new WaitForSeconds(timeStep);
            time += timeStep;
        }
    }

    float map(float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }
}
