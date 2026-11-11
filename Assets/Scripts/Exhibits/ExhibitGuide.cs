using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// One step of an exhibit's guided activity: an open question ("What do you think will happen…?") or a task.
    /// Steps never state the answer: students work it out from what they see. Steps are written in code by
    /// each exhibit.
    /// </summary>
    public sealed class GuideStep
    {
        /// <summary>Short heading, e.g. "Build a series circuit".</summary>
        public string title;

        /// <summary>The question or instruction shown on the backsplash.</summary>
        public string prompt;

        /// <summary>Shown when the Hint button is pressed: what to consider, never the answer.</summary>
        public string hint;

        /// <summary>For doing steps: true once the student has done it. Null means Next is always available.</summary>
        public Func<bool> isComplete;

        /// <summary>
        /// Live, neutral observations while on this step (what is happening, not why), e.g. "2 of 4 bulbs lit".
        /// Optional.
        /// </summary>
        public Func<string> liveFeedback;

        /// <summary>Called when the step starts. Optional.</summary>
        public Action onEnter;
    }

    /// <summary>Shares an exhibit's guide with everyone in the session (see <see cref="ExhibitGuideSync"/>).</summary>
    public interface IExhibitGuideSync
    {
        void RequestStep(int index);
        void RequestHint(bool show);
        void RequestRestart();
    }

    /// <summary>
    /// Runs an exhibit's steps on its backsplash text and control-strip buttons: Hint, Next, Reset.
    /// Next unlocks once the step's task (if any) is done. Reset puts every piece back and starts over;
    /// on the last step Next ("Again") does the same. Online, presses are shared through
    /// <see cref="ExhibitGuideSync"/> so everyone sees the same step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitGuide : MonoBehaviour
    {
        const string k_HintPrompt = "Stuck? Press Hint.";

        [SerializeField, Tooltip("The exhibit whose pieces Reset puts back.")]
        Transform m_ExhibitRoot;

        [Header("Text")]
        [SerializeField] TMP_Text m_Title;
        [SerializeField] TMP_Text m_Prompt;
        [SerializeField] TMP_Text m_Feedback;
        [SerializeField] TMP_Text m_Hint;

        [Header("Buttons")]
        [SerializeField] ExhibitPokeButton m_HintButton;
        [SerializeField] ExhibitPokeButton m_NextButton;
        [SerializeField] ExhibitPokeButton m_ResetButton;

        readonly List<GuideStep> m_Steps = new List<GuideStep>();
        int m_Index;
        bool m_ShowHint;

        /// <summary>Raised when the exhibit restarts (Reset, or Next on the last step), after the pieces are put back.</summary>
        public event Action restarted;

        /// <summary>Set by <see cref="ExhibitGuideSync"/> while online, so presses are shared with everyone.</summary>
        public IExhibitGuideSync sync { get; set; }

        public int stepIndex => m_Index;

        public int stepCount => m_Steps.Count;

        public bool hintShown => m_ShowHint;

        GuideStep current => m_Index < m_Steps.Count ? m_Steps[m_Index] : null;

        bool isLastStep => m_Index >= m_Steps.Count - 1;

        /// <summary>Sets the activity and starts it from the first step.</summary>
        public void SetSteps(IEnumerable<GuideStep> steps)
        {
            m_Steps.Clear();
            m_Steps.AddRange(steps);
            GoTo(0);
        }

        void OnEnable()
        {
            if (m_HintButton != null) m_HintButton.pressed += OnHint;
            if (m_NextButton != null) m_NextButton.pressed += OnNext;
            if (m_ResetButton != null) m_ResetButton.pressed += Restart;
        }

        void OnDisable()
        {
            if (m_HintButton != null) m_HintButton.pressed -= OnHint;
            if (m_NextButton != null) m_NextButton.pressed -= OnNext;
            if (m_ResetButton != null) m_ResetButton.pressed -= Restart;
        }

        static bool IsComplete(GuideStep step) => step.isComplete == null || step.isComplete();

        void Update()
        {
            var step = current;
            if (step == null)
                return;

            m_NextButton.interactable = isLastStep || IsComplete(step);
            if (step.liveFeedback != null)
                SetText(m_Feedback, step.liveFeedback());
        }

        void GoTo(int index)
        {
            m_Index = Mathf.Clamp(index, 0, Mathf.Max(0, m_Steps.Count - 1));
            m_ShowHint = false;
            var step = current;
            if (step == null)
                return;

            step.onEnter?.Invoke();
            SetText(m_Title, $"Step {m_Index + 1} of {m_Steps.Count}: {step.title}");
            SetText(m_Prompt, step.prompt);
            SetText(m_Feedback, "");
            SetText(m_Hint, string.IsNullOrEmpty(step.hint) ? "" : k_HintPrompt);
            m_NextButton.SetLabel(isLastStep ? "Again" : "Next");
        }

        // ---------- Button presses: sent through the network sync when online, applied directly when offline ----------

        void OnNext()
        {
            if (isLastStep)
            {
                Restart();
            }
            else if (IsComplete(current))
            {
                // Completion is checked here, on the presser's headset: tasks depend on pieces that are not synced.
                if (sync != null)
                    sync.RequestStep(m_Index + 1);
                else
                    ApplyStep(m_Index + 1);
            }
        }

        void OnHint()
        {
            if (string.IsNullOrEmpty(current?.hint))
                return;
            if (sync != null)
                sync.RequestHint(!m_ShowHint);
            else
                ApplyHint(!m_ShowHint);
        }

        /// <summary>Puts every piece of the exhibit back and starts the activity over (for everyone, when online).</summary>
        public void Restart()
        {
            if (sync != null)
                sync.RequestRestart();
            else
                ApplyRestart();
        }

        /// <summary>For testing without a headset: presses Next.</summary>
        public void PressNext() => OnNext();

        // ---------- State changes: called locally when offline, or by the network sync on every headset ----------

        public void ApplyStep(int index) => GoTo(index);

        public void ApplyHint(bool show)
        {
            m_ShowHint = show;
            var step = current;
            if (step != null && !string.IsNullOrEmpty(step.hint))
                SetText(m_Hint, m_ShowHint ? $"Hint: {step.hint}" : k_HintPrompt);
        }

        public void ApplyRestart()
        {
            if (m_ExhibitRoot != null)
                ExhibitResettable.ResetExhibit(m_ExhibitRoot);
            restarted?.Invoke();
            GoTo(0);
        }

        static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
                text.text = value;
        }
    }
}
