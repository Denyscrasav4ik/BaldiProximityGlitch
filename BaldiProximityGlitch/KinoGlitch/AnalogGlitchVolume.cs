using System;
using UnityEngine.Rendering;

namespace BaldiProximityGlitch.KinoGlitch
{
    [Serializable]
    [VolumeComponentMenu("Analog Glitch")]
    public class AnalogGlitchVolume : VolumeComponent
    {
        public ClampedFloatParameter scanLineJitter =
            new ClampedFloatParameter(0f, 0f, 1f);

        public ClampedFloatParameter verticalJump =
            new ClampedFloatParameter(0f, 0f, 1f);

        public ClampedFloatParameter horizontalShake =
            new ClampedFloatParameter(0f, 0f, 1f);

        public ClampedFloatParameter colorDrift =
            new ClampedFloatParameter(0f, 0f, 1f);

        public bool IsActive =>
            scanLineJitter.value > 0f ||
            verticalJump.value > 0f ||
            horizontalShake.value > 0f ||
            colorDrift.value > 0f;
    }
}
