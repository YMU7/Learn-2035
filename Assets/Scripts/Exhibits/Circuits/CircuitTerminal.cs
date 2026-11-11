using System.Collections.Generic;
using UnityEngine;

namespace XRClassroom.Exhibits.Circuits
{
    /// <summary>
    /// A metal post on a battery or bulb holder that wire plugs clip onto. Each post has a few slots, so
    /// several wires can meet at one post (needed for parallel circuits).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CircuitTerminal : MonoBehaviour
    {
        static readonly List<CircuitTerminal> s_All = new List<CircuitTerminal>();

        [SerializeField, Tooltip("Where plugs sit when clipped on. One plug per slot.")]
        Transform[] m_Slots;

        [SerializeField, Tooltip("False for posts on pre-built example circuits: loose plugs never clip onto them.")]
        bool m_AcceptsLoosePlugs = true;

        WirePlug[] m_Occupants;

        WirePlug[] occupants => m_Occupants ??= new WirePlug[m_Slots.Length];

        public bool hasFreeSlot
        {
            get
            {
                foreach (var occupant in occupants)
                {
                    if (occupant == null)
                        return true;
                }
                return false;
            }
        }

        void OnEnable() => s_All.Add(this);

        void OnDisable() => s_All.Remove(this);

        /// <summary>The nearest terminal with a free slot within <paramref name="radius"/> of a point, or null.</summary>
        public static CircuitTerminal FindNearestFree(Vector3 position, float radius)
        {
            CircuitTerminal best = null;
            float bestDistance = radius;
            foreach (var terminal in s_All)
            {
                float distance = Vector3.Distance(terminal.transform.position, position);
                if (distance <= bestDistance && terminal.m_AcceptsLoosePlugs && terminal.hasFreeSlot)
                {
                    best = terminal;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Clips a plug into the first free slot. Returns the slot, or null if the post is full.</summary>
        public Transform Attach(WirePlug plug)
        {
            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] == plug)
                    return m_Slots[i];
            }
            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] == null)
                {
                    occupants[i] = plug;
                    return m_Slots[i];
                }
            }
            return null;
        }

        public void Detach(WirePlug plug)
        {
            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] == plug)
                    occupants[i] = null;
            }
        }
    }
}
