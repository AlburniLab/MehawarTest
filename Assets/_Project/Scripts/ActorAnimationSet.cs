#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>Every visual state an actor can express. Gameplay states map onto these.</summary>
    public enum AnimState
    {
        Idle,
        Run,
        Jump,
        Fall,
        Windup,
        Active,
        Recovery,
        Telegraph,
        Hurt,
        Death,
        Parry,
        Fury
    }

    /// <summary>
    /// CONTRACT for sprites: one asset per actor listing state -> frames. SACRED RULE: one-shot
    /// states get their fps DERIVED at runtime from the gameplay duration (frames.Length / duration),
    /// which always comes from the state machines' tunables — never from this asset. Loops play at
    /// <c>loopFps</c>. In the greybox, sets are built at runtime by PlaceholderAnimationFactory; when
    /// real sprites arrive, author .asset instances with the same states and assign them instead.
    /// </summary>
    [CreateAssetMenu(fileName = "ActorAnimationSet", menuName = "Mehawar/Actor Animation Set")]
    public sealed class ActorAnimationSet : ScriptableObject
    {
        [Serializable]
        public sealed class StateAnimation
        {
            public AnimState state = AnimState.Idle;
            [Tooltip("Frames in playback order.")]
            public Sprite[] frames = Array.Empty<Sprite>();
            [Tooltip("Loop (idle/run/jump/fall) or one-shot stretched to the gameplay duration.")]
            public bool loop;
            [Tooltip("Playback fps for LOOPING states only; one-shots derive fps from duration.")]
            public float loopFps = 8f;
        }

        [SerializeField] private List<StateAnimation> states = new List<StateAnimation>();

        private Dictionary<AnimState, StateAnimation>? _byState;

        public StateAnimation? Get(AnimState state)
        {
            if (_byState == null)
            {
                _byState = new Dictionary<AnimState, StateAnimation>();
                foreach (StateAnimation sa in states)
                    _byState[sa.state] = sa;
            }
            return _byState.TryGetValue(state, out StateAnimation? found) ? found : null;
        }

        /// <summary>Used by the runtime placeholder factory to fill a CreateInstance'd set.</summary>
        public void SetStates(List<StateAnimation> entries)
        {
            states = entries;
            _byState = null;
        }
    }
}
