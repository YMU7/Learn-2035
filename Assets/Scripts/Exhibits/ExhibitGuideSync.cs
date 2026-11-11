using System;
using Unity.Netcode;
using UnityEngine;

namespace XRClassroom.Exhibits
{
    /// <summary>
    /// Shares an exhibit's step guide with everyone in the session, so all players see the same step and hint,
    /// and Reset restarts the exhibit for everyone.
    ///
    /// Network authority: the exhibit's NetworkObject owner holds the state in one NetworkVariable. Any player's
    /// button press is sent to the owner as an RPC; the owner updates the state and every headset (owner included)
    /// applies the change to its own guide. Offline (not spawned), the guide runs locally.
    /// Pieces on the bench (potato, wires, plugs) are not synced: each headset has its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitGuideSync : NetworkBehaviour, IExhibitGuideSync
    {
        struct GuideState : INetworkSerializable, IEquatable<GuideState>
        {
            public int step;
            public int restarts;
            public bool hint;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref step);
                serializer.SerializeValue(ref restarts);
                serializer.SerializeValue(ref hint);
            }

            public bool Equals(GuideState other) => step == other.step && restarts == other.restarts && hint == other.hint;
        }

        [SerializeField] ExhibitGuide m_Guide;

        readonly NetworkVariable<GuideState> m_State = new NetworkVariable<GuideState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            m_State.OnValueChanged += OnStateChanged;
            if (IsOwner)
                m_State.Value = new GuideState { step = m_Guide.stepIndex, hint = m_Guide.hintShown }; // Others follow the first player in.
            else
                Apply(m_State.Value, restartChanged: false);
            m_Guide.sync = this;
        }

        public override void OnNetworkDespawn()
        {
            m_State.OnValueChanged -= OnStateChanged;
            if (ReferenceEquals(m_Guide.sync, this))
                m_Guide.sync = null;
        }

        void OnStateChanged(GuideState previous, GuideState current) => Apply(current, current.restarts != previous.restarts);

        void Apply(GuideState state, bool restartChanged)
        {
            if (restartChanged)
                m_Guide.ApplyRestart();
            if (state.step != m_Guide.stepIndex)
                m_Guide.ApplyStep(state.step);
            if (state.hint != m_Guide.hintShown)
                m_Guide.ApplyHint(state.hint);
        }

        public void RequestStep(int index) => StepRpc(index);

        public void RequestHint(bool show) => HintRpc(show);

        public void RequestRestart() => RestartRpc();

        [Rpc(SendTo.Owner)]
        void StepRpc(int index)
        {
            var state = m_State.Value;
            state.step = index;
            state.hint = false;
            m_State.Value = state;
        }

        [Rpc(SendTo.Owner)]
        void HintRpc(bool show)
        {
            var state = m_State.Value;
            state.hint = show;
            m_State.Value = state;
        }

        [Rpc(SendTo.Owner)]
        void RestartRpc()
        {
            var state = m_State.Value;
            state.restarts++;
            state.step = 0;
            state.hint = false;
            m_State.Value = state;
        }
    }
}
