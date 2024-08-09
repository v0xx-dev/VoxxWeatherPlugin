using UnityEngine;
using UnityEngine.Audio;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Utils;


namespace VoxxWeatherPlugin.Tests
{

    public class SoundManager: MonoBehaviour
    {
        public int currentMixerSnapshotID = 0;
        public bool overridingCurrentAudioMixer;
        public PlayerControllerB localPlayer;

        public AudioMixerSnapshot[] mixerSnapshots;
        public void SetDiageticMasterVolume(float volume)
        {}

        public void SetDiageticMixerSnapshot(float transitionTime)
        {}

        public void ResumeCurrentMixerSnapshot(float transitionTime)
        {}

        private void SetAudioFilters()
        {
            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead && this.currentMixerSnapshotID != 0)
            {
            this.SetDiageticMasterVolume(0.0f);
            this.SetDiageticMixerSnapshot(transitionTime: 0.2f);
            }
            else if (( StartOfRound.Instance.drunknessSideEffect.Evaluate(this.localPlayer.drunkness) > 0.6f || PlayerTemperatureManager.heatSeverity > 0.85f) && !this.overridingCurrentAudioMixer)
            {
            this.overridingCurrentAudioMixer = true;
            this.mixerSnapshots[4].TransitionTo(6f);
            }
            else
            {
            if ((double) StartOfRound.Instance.drunknessSideEffect.Evaluate(this.localPlayer.drunkness) >= 0.40000000596046448 || !this.overridingCurrentAudioMixer)
                return;
            this.overridingCurrentAudioMixer = false;
            this.ResumeCurrentMixerSnapshot(8f);
            }
        }
    }
}
