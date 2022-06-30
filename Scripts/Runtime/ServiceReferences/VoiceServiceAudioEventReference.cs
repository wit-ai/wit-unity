using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Utilities;
using UnityEngine;

namespace Facebook.WitAi.ServiceReferences
{
    public class VoiceServiceAudioEventReference : MicrophoneServiceReference
    {
        [SerializeField] private VoiceServiceReference _voiceServiceReference;
        public override IAudioInputEvents AudioInputEvents => _voiceServiceReference.VoiceService.VoiceEvents;
    }
}
