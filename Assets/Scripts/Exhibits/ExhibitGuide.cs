using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// One step of an exhibit's guided activity. A step can ask a multiple-choice question, wait for the
    /// student to build or do something, or just explain. Steps are written in code by each exhibit.
    /// </summary>
    public sealed class GuideStep
    {
        /// <summary>Short heading, e.g. "Series circuits".</summary>
        public string title;

        /// <summary>The question or instruction.</summary>
        public string prompt;

        /// <summary>Answer choices for buttons A, B, C (up to 3). Null for steps without a question.</summary>
        public string[] choices;

        /// <summary>What to say after each choice is picked (same order as <see cref="choices"/>).</summary>
        public string[] choiceFeedback;

        /// <summary>Shown when the Hint button is pressed: what to consider, not the answer.</summary>
        public string hint;

        /// <summary>For doing steps: true once the student has done it. Null means nothing to do.</summary>
        public Func<bool> isComplete;

        /// <summary>Live status shown while on this step (e.g. what the circuit is doing). Optional.</summary>
        public Func<string> liveFeedback;

        /// <summary>Called when the step starts. Optional.</summary>
        public Action onEnter;

        /// <summary>Called with the choice index when an answer is picked. Optional.</summary>
        public Action<int> onAnswered;
    }

    /// <summary>Shares an exhibit's guide with everyone in the session (see <see cref="ExhibitGuideSync"/>).</summary>
    public interface IExhibitGuideSync
    {
        void RequestAnswer(int choice);
        void RequestStep(int index);
        void RequestHint(bool show);
        void RequestRestart();
    }

    /// <summary>
    /// Runs an exhibit's steps on its backsplash text and control-strip buttons: Hint, A, B, C, Next, Reset.
    /// Next unlocks once the step's question is answered (any answer: wrong answers are part of learning)
    /// and its task is done. Reset puts every piece back and starts over; on the last step Next does the same.
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
        [SerializeField] TMP_Text m_Choices;
        [SerializeField] TMP_Text m_Feedback;
        [SerializeField] TMP_Text m_Hint;

        [Header("Buttons")]
        [SerializeField] ExhibitPokeButton m_HintButton;
        [SerializeField] ExhibitPokeButton m_NextButton;
        [SerializeField] ExhibitPokeButton m_ResetButton;
        [SerializeField] ExhibitPokeButton[] m_ChoiceButtons;

        readonly List<GuideStep> m_Steps = new List<GuideStep>();
        int m_Index;
        int m_Answer = -1;
        bool m_ShowHint;

        /// <summary>Raised when Reset is pressed (or Next on the last step), after the pieces are put back.</summary>
        public event Action restarted;

        public int stepIndex => m_Index;

        public int stepCount => m_Steps.Count;

        /// <summary>The answer picked on the current step, or -1.</summary>
        public int answer => m_Answer;

        /// <summary>Sets the activity and starts it from the first step.</summary>
        public void SetSteps(IEnumerable<GuideStep> steps)
        {
            m_Steps.Clear();
            m_Steps.AddRange(steps);
            GoTo(0);
        }

        Action[] m_ChoiceHandlers;

        void Awake()
        {
            m_ChoiceHandlers = new Action[m_ChoiceButtons.Length];
            for (int i = 0; i < m_ChoiceButtons.Length; i++)
            {
                int choice = i;
                m_ChoiceHandlers[i] = () => OnChoice(choice);
            }
        }

        void OnEnable()
        {
            if (m_HintButton != null) m_HintButton.pressed += OnHint;
            if (m_NextButton != null) m_NextButton.pressed += OnNext;
            if (m_ResetButton != null) m_ResetButton.pressed += Restart;
            for (int i = 0; i < m_ChoiceButtons.Length; i++)
                m_ChoiceButtons[i].pressed += m_ChoiceHandlers[i];
        }

        void OnDisable()
        {
            if (m_HintButton != null) m_HintButton.pressed -= OnHint;
            if (m_NextButton != null) m_NextButton.pressed -= OnNext;
            if (m_ResetButton != null) m_ResetButton.pressed -= Restart;
            for (int i = 0; i < m_ChoiceButtons.Length; i++)
                m_ChoiceButtons[i].pressed -= m_ChoiceHandlers[i];
        }

        GuideStep current => m_Index < m_Steps.Count ? m_Steps[m_Index] : null;

        bool isLastStep => m_Index >= m_Steps.Count - 1;

        bool IsComplete(GuideStep step)
        {
            bool answered = step.choices == null || m_Answer >= 0;
            return answered && (step.isComplete == null || step.isComplete());
        }

        void Update()
        {
            var step = current;
            if (step == null)
                return;

            m_NextButton.interactable = isLastStep || IsComplete(step);

            // Live status (e.g. the circuit's state) is shown unless an answer's feedback is up.
            if (step.liveFeedback != null && m_Answer < 0)
                SetText(m_Feedback, step.liveFeedback());
        }

        void GoTo(int index)
        {
            m_Index = Mathf.Clamp(index, 0, Mathf.Max(0, m_Steps.Count - 1));
            m_Answer = -1;
            m_ShowHint = false;
            var step = current;
            if (step == null)
                return;

            step.onEnter?.Invoke();

            SetText(m_Title, $"Step {m_Index + 1} of {m_Steps.Count}: {step.title}");
            SetText(m_Prompt, step.prompt);
            SetText(m_Feedback, "");
            SetText(m_Hint, k_HintPrompt);

            int choiceCount = step.choices?.Length ?? 0;
            var choiceText = new System.Text.StringBuilder();
            for (int i = 0; i < choiceCount; i++)
                choiceText.Append(i == 0 ? "" : "      ").Append((char)('A' + i)).Append(")  ").Append(step.choices[i]);
            SetText(m_Choices, choiceText.ToString());
            for (int i = 0; i < m_ChoiceButtons.Length; i++)
            {
                m_ChoiceButtons[i].SetVisible(i < choiceCount);
                m_ChoiceButtons[i].interactable = true;
            }

            m_NextButton.SetLabel(isLastStep ? "Again" : "Next");
        }

        // ---------- Button presses: sent through the network sync when online, applied directly when offline ----------

        void OnChoice(int choice)
        {
            var step = current;
            if (step?.choices == null || choice >= step.choices.Length)
                return;
            if (sync != null)
                sync.RequestAnswer(choice);
            else
                ApplyAnswer(choice);
        }

        void OnNext()
        {
            if (isLastStep)
                Restart();
            else if (IsComplete(current))
            {
                // Completion is checked here, on the presser's headset: build steps depend on pieces that are not synced.
                if (sync != null)
                    sync.RequestStep(m_Index + 1);
                else
                    ApplyStep(m_Index + 1);
            }
        }

        void OnHint()
        {
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

        // ---------- State changes: called locally when offline, or by the network sync on every headset ----------

        /// <summary>Set by <see cref="ExhibitGuideSync"/> while online, so presses are shared with everyone.</summary>
        public IExhibitGuideSync sync { get; set; }

        public bool hintShown => m_ShowHint;

        public void ApplyStep(int index) => GoTo(index);

        public void ApplyAnswer(int choice)
        {
            var step = current;
            if (step?.choices == null || choice < 0 || choice >= step.choices.Length)
                return;
            m_Answer = choice;
            step.onAnswered?.Invoke(choice);
            string feedback = step.choiceFeedback != null && choice < step.choiceFeedback.Length ? step.choiceFeedback[choice] : "";
            SetText(m_Feedback, $"You picked {(char)('A' + choice)}. {feedback}");
        }

        public void ApplyHint(bool show)
        {
            m_ShowHint = show;
            var step = current;
            SetText(m_Hint, m_ShowHint && step != null ? $"Hint: {step.hint}" : k_HintPrompt);
        }

        public void ApplyRestart()
        {
            if (m_ExhibitRoot != null)
                ExhibitResettable.ResetExhibit(m_ExhibitRoot);
            restarted?.Invoke();
            GoTo(0);
        }

        /// <summary>For testing without a headset: answers the current question.</summary>
        public void Answer(int choice) => OnChoice(choice);

        /// <summary>For testing without a headset: presses Next.</summary>
        public void PressNext() => OnNext();

        static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value)
                text.text = value;
        }
    }
}
