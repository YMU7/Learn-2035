using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// A monitor beside an exhibit that plays a lesson video, with Play/Pause and Restart buttons.
    ///
    /// To add a video, set ONE of these in the Inspector:
    /// <list type="bullet">
    /// <item><b>Clip</b>: a video file imported into the project (most reliable, works offline).</item>
    /// <item><b>Video Url</b>: a direct link to a video file (ending in .mp4 or .webm, H.264 MP4 works best on
    /// Quest), e.g. from a school web server or cloud storage "direct download" link.</item>
    /// </list>
    /// YouTube page links (youtube.com/watch, youtu.be) cannot be played: Unity's video player needs the video
    /// file itself, and YouTube does not provide one. Download the video (with permission) and use Clip, or host
    /// the .mp4 somewhere and use its direct link. The screen says so if a YouTube link is entered.
    /// Playback is local to each headset (not synced between players).
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [DisallowMultipleComponent]
    public sealed class ExhibitVideoMonitor : MonoBehaviour
    {
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Video (set one)")]
        [SerializeField, Tooltip("A video file imported into the project.")]
        VideoClip m_Clip;

        [SerializeField, Tooltip("A DIRECT link to a video file (.mp4/.webm). YouTube page links do not work.")]
        string m_VideoUrl = "";

        [SerializeField] bool m_Loop;

        [Header("Parts")]
        [SerializeField, Tooltip("The screen quad. It shows the video while one is loaded.")]
        Renderer m_Screen;

        [SerializeField, Tooltip("Message shown on the screen when there is no video or it cannot be played.")]
        TMP_Text m_Message;

        [SerializeField] ExhibitPokeButton m_PlayPauseButton;
        [SerializeField] ExhibitPokeButton m_RestartButton;
        [SerializeField] AudioSource m_Audio;

        VideoPlayer m_Player;
        RenderTexture m_Texture;
        MaterialPropertyBlock m_Block;

        public VideoClip clip
        {
            get => m_Clip;
            set => m_Clip = value;
        }

        public string videoUrl
        {
            get => m_VideoUrl;
            set => m_VideoUrl = value;
        }

        static bool IsYouTube(string url) =>
            url.Contains("youtube.com") || url.Contains("youtu.be");

        void Awake()
        {
            m_Player = GetComponent<VideoPlayer>();
            m_Player.playOnAwake = false;
            m_Player.isLooping = m_Loop;
            m_Player.renderMode = VideoRenderMode.RenderTexture;
            m_Texture = new RenderTexture(1280, 720, 0) { name = $"{name} Video" };
            m_Player.targetTexture = m_Texture;
            if (m_Audio != null)
            {
                m_Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                m_Player.SetTargetAudioSource(0, m_Audio);
            }
            m_Player.errorReceived += OnError;
            m_Player.prepareCompleted += _ => ShowVideo(true);
        }

        void OnEnable()
        {
            if (m_PlayPauseButton != null) m_PlayPauseButton.pressed += TogglePlay;
            if (m_RestartButton != null) m_RestartButton.pressed += Restart;
        }

        void OnDisable()
        {
            if (m_PlayPauseButton != null) m_PlayPauseButton.pressed -= TogglePlay;
            if (m_RestartButton != null) m_RestartButton.pressed -= Restart;
        }

        void Start()
        {
            ShowVideo(false);
            if (m_Clip == null && IsYouTube(m_VideoUrl ?? ""))
                SetMessage("YouTube links can't be played here.\nUse a downloaded video file (Clip) or a direct .mp4 link.");
            else if (!HasVideo())
                SetMessage("No video yet.\nSet a Clip or a direct video URL on the\nExhibit Video Monitor component.");
            else
                SetMessage("Press Play to watch the video.");
        }

        void OnDestroy()
        {
            if (m_Texture != null)
            {
                m_Texture.Release();
                Destroy(m_Texture);
            }
        }

        bool HasVideo() => m_Clip != null || (!string.IsNullOrWhiteSpace(m_VideoUrl) && !IsYouTube(m_VideoUrl));

        bool ApplySource()
        {
            if (m_Clip != null)
            {
                m_Player.source = VideoSource.VideoClip;
                m_Player.clip = m_Clip;
                return true;
            }
            if (HasVideo())
            {
                m_Player.source = VideoSource.Url;
                m_Player.url = m_VideoUrl.Trim();
                return true;
            }
            return false;
        }

        /// <summary>Plays the video, or pauses it if it is playing.</summary>
        public void TogglePlay()
        {
            if (m_Player.isPlaying)
            {
                m_Player.Pause();
                SetMessage("");
                return;
            }
            if (!m_Player.isPrepared && !ApplySource())
                return;
            SetMessage(m_Player.isPrepared ? "" : "Loading...");
            m_Player.Play();
        }

        /// <summary>Starts the video again from the beginning.</summary>
        public void Restart()
        {
            if (!m_Player.isPrepared && !ApplySource())
                return;
            m_Player.time = 0;
            m_Player.Play();
        }

        void Update()
        {
            // Clear "Loading..." once frames arrive.
            if (m_Player.isPlaying && m_Message != null && m_Message.text == "Loading...")
                SetMessage("");
        }

        void OnError(VideoPlayer player, string message)
        {
            ShowVideo(false);
            SetMessage("Couldn't play this video.\nCheck the link is a direct .mp4/.webm file.");
            Debug.LogWarning($"[ExhibitVideoMonitor] {name}: {message}", this);
        }

        void ShowVideo(bool show)
        {
            if (m_Screen == null)
                return;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.Clear();
            if (show)
            {
                m_Block.SetTexture(k_BaseMap, m_Texture);
                m_Block.SetColor(k_BaseColor, Color.white);
            }
            else
            {
                m_Block.SetColor(k_BaseColor, new Color(0.06f, 0.07f, 0.09f));
            }
            m_Screen.SetPropertyBlock(m_Block);
        }

        void SetMessage(string text)
        {
            if (m_Message != null)
                m_Message.text = text;
        }
    }
}
