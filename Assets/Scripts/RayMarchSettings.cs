using UnityEngine;

[CreateAssetMenu(fileName = "RayMarchSettings", menuName = "Viz/RayMarchSettings", order = 100)]
public class RayMarchSettings : ScriptableObject {
    [Header("Size")]
    public AnimationCurve sizeCurve;
    public float sizeTime = 0.2f;

    [Header("ColorPush")]
    public AnimationCurve colorCurveR;
    public AnimationCurve colorCurveG;
    public AnimationCurve colorCurveB;
    public float colorPushStrength;
    public float colorPushtime;

    [Header("SpikePush")]
    public AnimationCurve spikeIncreaseCurve;
    public float spikeDecreaseSpeed;
    public float minSpikyness = 0.1f;
    public float addSpikynessOnSignal = 0.1f;

    [Header("Meta")]
    public float timeScale;
    public bool MidiEnabled;
    
    [Header("Size")]
    public AnimationCurve hatCurve;
    public float hatTime = 0.2f;
}