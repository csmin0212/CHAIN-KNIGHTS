using System.Collections.Generic;
using UnityEngine;

namespace ChainKnights.Client.UI
{
    /// <summary>
    /// 효과음과 배경음악. Assets/Resources/Audio/의 파일 이름(확장자 없이)으로 부른다.
    /// 파일이 없으면 조용히 넘어간다.
    /// </summary>
    public static class Sound
    {
        private const float SfxVolume = 0.8f;
        private const float MusicVolume = 0.35f;

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private static AudioSource sfx;
        private static AudioSource music;

        private static AudioClip Clip(string name)
        {
            if (!cache.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                cache[name] = clip;
            }
            return clip;
        }

        private static void Ensure()
        {
            if (sfx != null)
                return;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : new GameObject("AudioListener")).AddComponent<AudioListener>();
            }
            var go = new GameObject("Sound");
            Object.DontDestroyOnLoad(go);
            sfx = go.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            music = go.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = MusicVolume;
        }

        /// <summary>효과음. fallback은 그 이름의 파일이 아직 없을 때 대신 낼 소리.</summary>
        public static void Play(string name, string fallback = null, float volume = 1f)
        {
            Ensure();
            var clip = Clip(name);
            if (clip == null && fallback != null)
                clip = Clip(fallback);
            if (clip != null)
                sfx.PlayOneShot(clip, SfxVolume * volume);
        }

        public static void Music(string name)
        {
            Ensure();
            var clip = Clip(name);
            if (clip == null || music.clip == clip)
                return;
            music.clip = clip;
            music.Play();
        }
    }
}
