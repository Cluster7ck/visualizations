using UnityEngine;

[CreateAssetMenu(fileName = "RayMarchSettings", menuName = "Viz/RayMarchSettings", order = 100)]
public class ReactionDiffusionSettings : ScriptableObject
{
    [Header("BaseSettings")]
    [Range(0, 1)] public float dA = 1.0f;
    [Range(0, 1)] public float dB = 0.5f;
    [Range(0, 0.3f)] public float feed = 0.055f;
    [Range(0, 0.4f)] public float k = 0.062f;
    [Range(0, 1)] public float m = 0.2f;
}
