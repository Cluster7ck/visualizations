using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Ext
{
    public static float Remap(this float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }

    public static IEnumerator AnimateMaterialValue(Material material, string value, float targetTime, AnimationCurve curve, float curveMult, float offset)
    {
        float timeStep = 0.01f;
        float time = 0;

        while(time < targetTime)
        {
            var val = curve.Evaluate(time/targetTime) * curveMult + offset;
            material.SetFloat(value, val);

            yield return new WaitForSeconds(timeStep);
            time += timeStep;
        }
    }

    public static IEnumerator AnimateMaterialValue(Material material, int propId, float targetTime, AnimationCurve curve, float curveMult, float offset)
    {
        float timeStep = 0.01f;
        float time = 0;

        while(time < targetTime)
        {
            var val = curve.Evaluate(time/targetTime) * curveMult + offset;
            material.SetFloat(propId, val);

            yield return new WaitForSeconds(timeStep);
            time += timeStep;
        }
    }

    public static void HandleMidiInputPerChannel(Dictionary<int, Action> channelToAction, float[] lastValues)
    {
        for (int ch = 0; ch < 16; ch++)
        {
            for (int note = 0; note < 128; note++)
            {
                var down = MidiJack.MidiMaster.GetKeyDown((MidiJack.MidiChannel)ch, note);
                var val = MidiJack.MidiMaster.GetKey((MidiJack.MidiChannel)ch, note);
                var lastValIdx = ch * 128 + note;
                var lastValue = lastValues[lastValIdx];
                if (down)
                {
                    channelToAction[ch]();
                }
                else if (lastValue == 0 && val > 0)
                {
                    channelToAction[ch]();
                }
                lastValues[lastValIdx] = val;
            }
        }
    }
}