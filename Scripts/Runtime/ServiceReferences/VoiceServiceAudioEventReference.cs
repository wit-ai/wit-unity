using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Utilities;
using UnityEngine;

namespace Facebook.WitAi.ServiceReferences
{
    public class VoiceServiceAudioEventReference : AudioInputServiceReference
    {
        [SerializeField] private VoiceServiceReference _voiceServiceReference;
        public override IAudioInputEvents AudioEvents => _voiceServiceReference.VoiceService.AudioEvents;
    }
}
