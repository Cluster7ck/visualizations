using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using DG.Tweening;

public class VideoController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AnimationCurve zoomCurve;
    [SerializeField] private Renderer targetRenderer;

    private Dictionary<int, Action> channelToAction = new Dictionary<int, Action>();
    private float[] lastValues = new float[16*128];

    private void Start()
    {
        videoPlayer.playbackSpeed = 4.0f;
        channelToAction[1] = () => {
            var progress = UnityEngine.Random.value;
            var frame = Mathf.FloorToInt(progress * videoPlayer.frameCount);
            Debug.Log("Frame: "+frame);
            videoPlayer.frame = frame;
        };
        channelToAction[0] = () => {
            Debug.Log("Zoom:");
            targetRenderer.material.DOFloat(1.0f, "_Zoom", 0.3f).SetEase(zoomCurve);
        };
        channelToAction[2] = () => {

            var replicate = Mathf.Floor(map(UnityEngine.Random.value, 0, 1, 4, 32));

            targetRenderer.material.SetFloat("_Replicate", replicate);
        };
    }

    void Update()
    {
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
                    Debug.Log($"Ch: {ch} Note: {note} Val: {val}");
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

    float map(float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }
}
