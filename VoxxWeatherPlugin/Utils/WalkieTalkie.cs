using UnityEngine;
using System;
using Unity.Mathematics;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin.Patches;

namespace VoxxWeatherPlugin.Tests
{
    // Decompiled with JetBrains decompiler
    // Type: WalkieTalkie
    // Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
    // MVID: 0B5B8298-8734-4817-A212-14132EA92EEF
    // Assembly location: C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\Lethal Company_Data\Managed\Assembly-CSharp.dll

    using GameNetcodeStuff;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Netcode;
    using UnityEngine;

    #nullable disable
    public class WalkieTalkie : GrabbableObject
    {
        public PlayerControllerB playerListeningTo;
        public AudioSource thisAudio;
        private PlayerControllerB previousPlayerHeldBy;
        public bool isHoldingButton;
        public bool speakingIntoWalkieTalkie;
        public bool clientIsHoldingAndSpeakingIntoThis;
        public bool otherClientIsTransmittingAudios;
        private Coroutine speakIntoWalkieTalkieCoroutine;
        public AudioClip[] stopTransmissionSFX;
        public AudioClip[] startTransmissionSFX;
        public AudioClip switchWalkieTalkiePowerOff;
        public AudioClip switchWalkieTalkiePowerOn;
        public AudioClip talkingOnWalkieTalkieNotHeldSFX;
        public AudioClip playerDieOnWalkieTalkieSFX;
        public static List<WalkieTalkie> allWalkieTalkies = new List<WalkieTalkie>();
        public bool playingGarbledVoice;
        public Material onMaterial;
        public Material offMaterial;
        public Light walkieTalkieLight;
        public AudioSource target;
        [SerializeField]
        private float recordingRange = 6f;
        [SerializeField]
        private float maxVolume = 0.6f;
        private List<AudioSource> audioSourcesToReplay = new List<AudioSource>();
        private Dictionary<AudioSource, AudioSource> audioSourcesReceiving = new Dictionary<AudioSource, AudioSource>();
        public Collider listenCollider;
        private int audioSourcesToReplayLastFrameCount;
        public Collider[] collidersInRange = new Collider[30];
        public List<WalkieTalkie> talkiesSendingToThis = new List<WalkieTalkie>();
        private float cleanUpInterval;
        private float updateInterval;

        private void OnDisable()
        {
            this.OnDestroy();
            if (!WalkieTalkie.allWalkieTalkies.Contains(this))
                return;
            WalkieTalkie.allWalkieTalkies.Remove(this);
            if (WalkieTalkie.allWalkieTalkies.Count > 0)
                return;
            WalkieTalkie.allWalkieTalkies.TrimExcess();
        }

        private void OnEnable()
        {
            if (WalkieTalkie.allWalkieTalkies.Contains(this))
                return;
            WalkieTalkie.allWalkieTalkies.Add(this);
        }

        public void SetLocalClientSpeaking(bool speaking)
        {
            if (this.previousPlayerHeldBy.speakingToWalkieTalkie == speaking)
                return;
            this.previousPlayerHeldBy.speakingToWalkieTalkie = speaking;
            Debug.Log((object)string.Format("Set local client speaking on walkie talkie: {0}", (object)speaking));
            if (speaking)
                this.SetPlayerSpeakingOnWalkieTalkieServerRpc((int)this.previousPlayerHeldBy.playerClientId);
            else
                this.UnsetPlayerSpeakingOnWalkieTalkieServerRpc((int)this.previousPlayerHeldBy.playerClientId);
        }

        [ServerRpc]
        public void SetPlayerSpeakingOnWalkieTalkieServerRpc(int playerId)
        {
            this.SetPlayerSpeakingOnWalkieTalkieClientRpc(playerId);
        }

        [ClientRpc]
        public void SetPlayerSpeakingOnWalkieTalkieClientRpc(int playerId)
        {
            StartOfRound.Instance.allPlayerScripts[playerId].speakingToWalkieTalkie = true;
            this.clientIsHoldingAndSpeakingIntoThis = true;
            this.SendWalkieTalkieStartTransmissionSFX(playerId);
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        [ServerRpc]
        public void UnsetPlayerSpeakingOnWalkieTalkieServerRpc(int playerId)
        {
            this.UnsetPlayerSpeakingOnWalkieTalkieClientRpc(playerId);
        }

        [ClientRpc]
        public void UnsetPlayerSpeakingOnWalkieTalkieClientRpc(int playerId)
        {
            StartOfRound.Instance.allPlayerScripts[playerId].speakingToWalkieTalkie = false;
            this.clientIsHoldingAndSpeakingIntoThis = false;
            this.SendWalkieTalkieEndTransmissionSFX(playerId);
            this.updateInterval = 0.2f;
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        private void SendWalkieTalkieEndTransmissionSFX(int playerId)
        {
            for (int index = 0; index < WalkieTalkie.allWalkieTalkies.Count; ++index)
            {
                if (!((UnityEngine.Object)StartOfRound.Instance.allPlayerScripts[playerId] == (UnityEngine.Object)WalkieTalkie.allWalkieTalkies[index].playerHeldBy) && !this.PlayerIsHoldingAnotherWalkieTalkie(WalkieTalkie.allWalkieTalkies[index]) && WalkieTalkie.allWalkieTalkies[index].isBeingUsed)
                    RoundManager.PlayRandomClip(WalkieTalkie.allWalkieTalkies[index].thisAudio, WalkieTalkie.allWalkieTalkies[index].stopTransmissionSFX);
            }
        }

        private void SendWalkieTalkieStartTransmissionSFX(int playerId)
        {
            Debug.Log((object)"Walkie talkie A");
            double num = (double)UnityEngine.Random.Range(0.0f, this.talkingOnWalkieTalkieNotHeldSFX.length - 0.1f);
            for (int index = 0; index < WalkieTalkie.allWalkieTalkies.Count; ++index)
            {
                Debug.Log((object)string.Format("Walkie talkie #{0} {1} B", (object)index, (object)WalkieTalkie.allWalkieTalkies[index].gameObject.name));
                Debug.Log((object)string.Format("is walkie being used: {0}", (object)WalkieTalkie.allWalkieTalkies[index].isBeingUsed));
                if (!this.PlayerIsHoldingAnotherWalkieTalkie(WalkieTalkie.allWalkieTalkies[index]) && WalkieTalkie.allWalkieTalkies[index].isBeingUsed)
                {
                    RoundManager.PlayRandomClip(WalkieTalkie.allWalkieTalkies[index].thisAudio, WalkieTalkie.allWalkieTalkies[index].startTransmissionSFX);
                    Debug.Log((object)string.Format("Walkie talkie #{0}  {1} C", (object)index, (object)WalkieTalkie.allWalkieTalkies[index].gameObject.name));
                }
            }
        }

        private void BroadcastSFXFromWalkieTalkie(AudioClip sfx, int fromPlayerId)
        {
            for (int index = 0; index < WalkieTalkie.allWalkieTalkies.Count; ++index)
            {
                if (!((UnityEngine.Object)StartOfRound.Instance.allPlayerScripts[fromPlayerId] == (UnityEngine.Object)WalkieTalkie.allWalkieTalkies[index].playerHeldBy))
                {
                    if (this.PlayerIsHoldingAnotherWalkieTalkie(WalkieTalkie.allWalkieTalkies[index]))
                        break;
                    WalkieTalkie.allWalkieTalkies[index].thisAudio.PlayOneShot(sfx);
                }
            }
        }

        private bool PlayerIsHoldingAnotherWalkieTalkie(WalkieTalkie walkieTalkie)
        {
            if ((UnityEngine.Object)walkieTalkie.playerHeldBy == (UnityEngine.Object)null)
            {
                Debug.Log((object)"False A");
                return false;
            }
            if ((UnityEngine.Object)walkieTalkie.playerHeldBy.currentlyHeldObjectServer == (UnityEngine.Object)null)
            {
                Debug.Log((object)"False B");
                return false;
            }
            if ((UnityEngine.Object)walkieTalkie.playerHeldBy.currentlyHeldObjectServer.GetComponent<WalkieTalkie>() == (UnityEngine.Object)null)
            {
                Debug.Log((object)"False C");
                return false;
            }
            Debug.Log((object)string.Format("{0}", (object)walkieTalkie.isPocketed));
            return (UnityEngine.Object)walkieTalkie.playerHeldBy != (UnityEngine.Object)null && (UnityEngine.Object)walkieTalkie.playerHeldBy.currentlyHeldObjectServer != (UnityEngine.Object)null && (UnityEngine.Object)walkieTalkie.playerHeldBy.currentlyHeldObjectServer.GetComponent<WalkieTalkie>() != (UnityEngine.Object)null && walkieTalkie.isPocketed;
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            this.isHoldingButton = buttonDown;
            if (!this.isBeingUsed || this.speakingIntoWalkieTalkie || !buttonDown)
                return;
            this.previousPlayerHeldBy = this.playerHeldBy;
            if (this.speakIntoWalkieTalkieCoroutine != null)
                this.StopCoroutine(this.speakIntoWalkieTalkieCoroutine);
            this.speakIntoWalkieTalkieCoroutine = this.StartCoroutine(this.speakingIntoWalkieTalkieMode());
        }

        private IEnumerator speakingIntoWalkieTalkieMode()
        {
            WalkieTalkie walkieTalkie = this;
            walkieTalkie.PlayerHoldingWalkieTalkieButton(true);
            walkieTalkie.SetLocalClientSpeaking(true);
            yield return (object)new WaitForSeconds(0.2f);
            // ISSUE: reference to a compiler-generated method
            yield return (object)new WaitUntil(() => !this.isHoldingButton);
            walkieTalkie.SetLocalClientSpeaking(false);
            walkieTalkie.PlayerHoldingWalkieTalkieButton(false);
        }

        private void PlayerHoldingWalkieTalkieButton(bool speaking)
        {
            this.speakingIntoWalkieTalkie = speaking;
            this.previousPlayerHeldBy.activatingItem = speaking;
            this.previousPlayerHeldBy.playerBodyAnimator.SetBool("walkieTalkie", speaking);
        }

        public void EnableWalkieTalkieListening(bool enable)
        {
            if ((UnityEngine.Object)this.playerHeldBy != (UnityEngine.Object)null)
                this.playerHeldBy.holdingWalkieTalkie = enable;
            if (!this.IsPlayerSpectatedOrLocal())
                return;
            this.thisAudio.Stop();
            StartOfRound.Instance.UpdatePlayerVoiceEffects();
        }

        public override void UseUpBatteries()
        {
            base.UseUpBatteries();
            this.SwitchWalkieTalkieOn(false);
        }

        public override void PocketItem()
        {
            base.PocketItem();
            this.walkieTalkieLight.enabled = false;
        }

        public override void ItemInteractLeftRight(bool right)
        {
            base.ItemInteractLeftRight(right);
            if (right)
                return;
            this.SwitchWalkieTalkieOn(!this.isBeingUsed);
        }

        public void SwitchWalkieTalkieOn(bool on)
        {
            this.isBeingUsed = on;
            this.EnableWalkieTalkieListening(on);
            if (on)
            {
                this.mainObjectRenderer.sharedMaterial = this.onMaterial;
                this.walkieTalkieLight.enabled = true;
                this.thisAudio.PlayOneShot(this.switchWalkieTalkiePowerOn);
            }
            else
            {
                this.mainObjectRenderer.sharedMaterial = this.offMaterial;
                this.walkieTalkieLight.enabled = false;
                this.thisAudio.PlayOneShot(this.switchWalkieTalkiePowerOff);
            }
        }

        public override void EquipItem()
        {
            base.EquipItem();
            if (this.isBeingUsed)
                this.walkieTalkieLight.enabled = true;
            this.playerHeldBy.equippedUsableItemQE = true;
            if (!this.isBeingUsed)
                return;
            this.EnableWalkieTalkieListening(true);
        }

        public override void DiscardItem()
        {
            if (this.playerHeldBy.isPlayerDead && this.clientIsHoldingAndSpeakingIntoThis)
                this.BroadcastSFXFromWalkieTalkie(this.playerDieOnWalkieTalkieSFX, (int)this.playerHeldBy.playerClientId);
            this.EnableWalkieTalkieListening(false);
            this.playerHeldBy.equippedUsableItemQE = false;
            base.DiscardItem();
        }

        private bool IsPlayerSpectatedOrLocal()
        {
            if (this.IsOwner)
                return true;
            return GameNetworkManager.Instance.localPlayerController.isPlayerDead && (UnityEngine.Object)this.playerHeldBy == (UnityEngine.Object)GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript;
        }

        public override void Start()
        {
            base.Start();
            this.GetAllAudioSourcesToReplay();
            this.SetupAudiosourceClip();
        }

        public override void Update()
        {
            base.Update();
            if ((double)this.cleanUpInterval >= 0.0)
            {
                this.cleanUpInterval -= Time.deltaTime;
            }
            else
            {
                this.cleanUpInterval = 15f;
                if (this.audioSourcesReceiving.Count > 10)
                {
                    foreach (KeyValuePair<AudioSource, AudioSource> keyValuePair in this.audioSourcesReceiving)
                    {
                        if ((UnityEngine.Object)keyValuePair.Key == (UnityEngine.Object)null)
                            this.audioSourcesReceiving.Remove(keyValuePair.Key);
                    }
                }
            }
            if ((double)this.updateInterval >= 0.0)
            {
                this.updateInterval -= Time.deltaTime;
            }
            else
            {
                this.updateInterval = 0.3f;
                this.GetAllAudioSourcesToReplay();
                this.TimeAllAudioSources();
            }
        }

        private void TimeAllAudioSources()
        {
            AudioSource audioSource;
            for (int index1 = 0; index1 < WalkieTalkie.allWalkieTalkies.Count; ++index1)
            {
                if (!((UnityEngine.Object)WalkieTalkie.allWalkieTalkies[index1] == (UnityEngine.Object)this))
                {
                    if ((UnityEngine.Object)WalkieTalkie.allWalkieTalkies[index1].playerHeldBy != (UnityEngine.Object)null && WalkieTalkie.allWalkieTalkies[index1].clientIsHoldingAndSpeakingIntoThis && WalkieTalkie.allWalkieTalkies[index1].isBeingUsed && this.isBeingUsed)
                    {
                        if (!this.talkiesSendingToThis.Contains(WalkieTalkie.allWalkieTalkies[index1]))
                            this.talkiesSendingToThis.Add(WalkieTalkie.allWalkieTalkies[index1]);
                        for (int index2 = WalkieTalkie.allWalkieTalkies[index1].audioSourcesToReplay.Count - 1; index2 >= 0; --index2)
                        {
                            AudioSource key = WalkieTalkie.allWalkieTalkies[index1].audioSourcesToReplay[index2];
                            if (!((UnityEngine.Object)key == (UnityEngine.Object)null))
                            {
                                if (this.audioSourcesReceiving.TryAdd(key, (AudioSource)null))
                                {
                                    this.audioSourcesReceiving[key] = FlarePatches.SplitWalkieTarget(this.target.gameObject);
                                    this.audioSourcesReceiving[key].clip = key.clip;
                                    try
                                    {
                                        if ((double)key.time >= (double)key.clip.length)
                                        {
                                            Debug.Log((object)string.Format("walkie: {0}, {1}, {2}", (object)key.time, (object)key.clip.length, (object)key.clip.name));
                                            this.audioSourcesReceiving[key].time = (double)key.time - 0.05000000074505806 >= (double)key.clip.length ? key.time / 5f : Mathf.Clamp(key.time - 0.05f, 0.0f, 1000f);
                                            Debug.Log((object)string.Format("sourcetime: {0}", (object)this.audioSourcesReceiving[key].time));
                                        }
                                        else
                                            this.audioSourcesReceiving[key].time = key.time;
                                        this.audioSourcesReceiving[key].spatialBlend = 1f;
                                        this.audioSourcesReceiving[key].Play();
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.LogError((object)string.Format("Error while playing audio clip in walkie talkie. Clip name: {0} object: {1}; time: {2}; {3}", (object)key.clip.name, (object)key.gameObject.name, (object)key.time, (object)ex));
                                    }
                                }
                                else
                                {
                                    float num = Vector3.Distance(key.transform.position, WalkieTalkie.allWalkieTalkies[index1].transform.position);
                                    Debug.Log((object)string.Format("Receiving audiosource with name: {0}; recording distance: {1}", (object)key.gameObject.name, (object)num));
                                    if ((double)num > (double)this.recordingRange + 7.0)
                                    {
                                        Debug.Log((object)("Recording distance out of range; removing audio with name: " + key.gameObject.name));
                                        this.audioSourcesReceiving.Remove(key, out audioSource);
                                        FlarePatches.DisposeWalkieTarget(audioSource, target.gameObject);
                                        WalkieTalkie.allWalkieTalkies[index1].audioSourcesToReplay.RemoveAt(index2);
                                    }
                                    else
                                    {
                                        this.audioSourcesReceiving[key].volume = Mathf.Lerp(this.maxVolume, 0.0f, num / (this.recordingRange + 3f));
                                        if (key.isPlaying && !this.audioSourcesReceiving[key].isPlaying || (UnityEngine.Object)key.clip != (UnityEngine.Object)this.audioSourcesReceiving[key].clip)
                                        {
                                            this.audioSourcesReceiving[key].clip = key.clip;
                                            this.audioSourcesReceiving[key].Play();
                                        }
                                        else if (!key.isPlaying)
                                            this.audioSourcesReceiving[key].Stop();
                                        this.audioSourcesReceiving[key].time = key.time;
                                    }
                                }
                            }
                        }
                    }
                    else if (this.talkiesSendingToThis.Contains(WalkieTalkie.allWalkieTalkies[index1]))
                    {
                        this.talkiesSendingToThis.Remove(WalkieTalkie.allWalkieTalkies[index1]);
                        foreach (AudioSource key in WalkieTalkie.allWalkieTalkies[index1].audioSourcesToReplay)
                        {
                            for (int index3 = 0; index3 < WalkieTalkie.allWalkieTalkies.Count; ++index3)
                            {
                                if (WalkieTalkie.allWalkieTalkies[index3].audioSourcesReceiving.ContainsKey(key))
                                {
                                    WalkieTalkie.allWalkieTalkies[index3].audioSourcesReceiving.Remove(key, out audioSource);
                                    FlarePatches.DisposeWalkieTarget(audioSource, target.gameObject);
                                }
                            }
                        }
                        WalkieTalkie.allWalkieTalkies[index1].audioSourcesToReplay.Clear();
                    }
                }
            }
        }

        public static void TransmitOneShotAudio(AudioSource audioSource, AudioClip clip, float vol = 1f)
        {
            if ((UnityEngine.Object)clip == (UnityEngine.Object)null || (UnityEngine.Object)audioSource == (UnityEngine.Object)null)
                return;
            for (int index1 = 0; index1 < WalkieTalkie.allWalkieTalkies.Count; ++index1)
            {
                if (!((UnityEngine.Object)WalkieTalkie.allWalkieTalkies[index1].playerHeldBy == (UnityEngine.Object)null) && WalkieTalkie.allWalkieTalkies[index1].clientIsHoldingAndSpeakingIntoThis && WalkieTalkie.allWalkieTalkies[index1].isBeingUsed)
                {
                    float num1 = Vector3.Distance(WalkieTalkie.allWalkieTalkies[index1].transform.position, audioSource.transform.position);
                    if ((double)num1 < (double)WalkieTalkie.allWalkieTalkies[index1].recordingRange)
                    {
                        for (int index2 = 0; index2 < WalkieTalkie.allWalkieTalkies.Count; ++index2)
                        {
                            if (index2 != index1 && WalkieTalkie.allWalkieTalkies[index2].isBeingUsed)
                            {
                                float num2 = Mathf.Lerp(WalkieTalkie.allWalkieTalkies[index1].maxVolume, 0.0f, num1 / (WalkieTalkie.allWalkieTalkies[index1].recordingRange + 3f));
                                WalkieTalkie.allWalkieTalkies[index2].target.PlayOneShot(clip, num2 * vol);
                            }
                        }
                    }
                }
            }
        }

        private void SetupAudiosourceClip() => this.target.Stop();

        private void GetAllAudioSourcesToReplay()
        {
            if ((UnityEngine.Object)this.playerHeldBy == (UnityEngine.Object)null || !this.playerHeldBy.speakingToWalkieTalkie || !this.isBeingUsed)
                return;
            int num = Physics.OverlapSphereNonAlloc(this.transform.position, this.recordingRange, this.collidersInRange, 11010632, QueryTriggerInteraction.Collide);
            for (int index = 0; index < num; ++index)
            {
                if (!(bool)(UnityEngine.Object)this.collidersInRange[index].gameObject.GetComponent<WalkieTalkie>())
                {
                    AudioSource component = this.collidersInRange[index].GetComponent<AudioSource>();
                    if ((UnityEngine.Object)component != (UnityEngine.Object)null && component.isPlaying && (UnityEngine.Object)component.clip != (UnityEngine.Object)null && (double)component.time > 0.0 && !this.audioSourcesToReplay.Contains(component))
                        this.audioSourcesToReplay.Add(component);
                }
            }
        }
    }
}
