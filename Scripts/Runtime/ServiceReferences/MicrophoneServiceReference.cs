using Facebook.WitAi.Interfaces;
using UnityEngine;

namespace Facebook.WitAi.ServiceReferences
{
    public abstract class MicrophoneServiceReference : MonoBehaviour
    {
        public abstract IAudioInputEvents AudioInputEvents { get; }
    }
}
