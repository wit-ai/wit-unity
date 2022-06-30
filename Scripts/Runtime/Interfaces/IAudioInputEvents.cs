using Facebook.WitAi.Events;
using UnityEngine.Events;

namespace Facebook.WitAi.Interfaces
{
    public interface IAudioInputEvents
    {
        public WitMicLevelChangedEvent OnMicAudioLevelChanged { get; }
        public UnityEvent OnMicStartedListening { get; }
        public UnityEvent OnMicStoppedListening { get; }
    }
}
