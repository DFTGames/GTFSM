// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2012-2026 Giuseppe “Pino” De Francesco (DFT Games Studios)
//
// Project: Generically Typed FSM for Unity
// File: Declarations.cs
// Summary: Core interfaces and enums for GTFSM system.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Attribution: Please retain this header and the NOTICE file when redistributing.
// Citation: If you use this work in a publication, please cite it (see CITATION.cff).

using System;
using System.Collections.Generic;

namespace DFTGames.Tools.GTFSM
{
    /// <summary>
    /// Priority levels for states, determining interruption behavior.
    /// Higher priority states can interrupt lower priority ones.
    /// </summary>
    public enum StatePriority
    {
        /// <summary>
        /// Low priority - can be interrupted by any other state.
        /// Use for: Idle, patrolling, wandering.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority - standard state priority.
        /// Use for: Most gameplay states, attacking, moving.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - only high or critical states can interrupt.
        /// Use for: Important animations, special attacks, reactions.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority - cannot be interrupted except by other critical states.
        /// Use for: Death, cutscenes, scripted sequences.
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Base interface for FSM states with complete lifecycle support.
    /// Implement this interface to create custom states for your FSM.
    /// 
    /// Lifecycle Order:
    /// 1. Init(client) - Called once when state is first created
    /// 2. CanEnter(client) - Validates if transition is allowed
    /// 3. OnEnter(client) - Called each time state becomes active
    /// 4. Execute(client) - Called every frame (Update)
    /// 5. ExecuteFixed(client) - Called every physics frame (FixedUpdate)
    /// 6. ExecuteLate(client) - Called every late frame (LateUpdate)
    /// 7. CanExit(client) - Validates if exit is allowed
    /// 8. OnExit(client) - Called when leaving state
    /// 
    /// The generic TClient parameter ensures type-safe access to the client
    /// without needing GetComponent or casting.
    /// 
    /// Example:
    /// public class PatrolState : GTFSMState<Enemy>
    /// {
    ///     public StatePriority Priority => StatePriority.Normal;
    ///     
    ///     public bool CanEnter(Enemy enemy)
    ///     {
    ///         return enemy.path != null && enemy.path.Length > 0;
    ///     }
    ///     
    ///     public override Type Execute(Enemy enemy)
    ///     {
    ///         // Directly access enemy.Agent, enemy.path, etc.
    ///         if (enemy.PlayerDetected())
    ///             return typeof(AttackState); // Transition
    ///         return null; // Stay in patrol
    ///     }
    /// }
    /// </summary>
    /// <typeparam name="TClient">The client type that will use this state (must implement IGTFSMClient)</typeparam>
    public interface GTFSMState<TClient> where TClient : class, IGTFSMClient<TClient>
    {
        /// <summary>
        /// The name of this state (usually the class name).
        /// Used for debugging and Inspector display.
        /// </summary>
        string StateName { get; set; }

        /// <summary>
        /// The priority level of this state.
        /// Determines which states can interrupt this one.
        /// 
        /// Rules:
        /// - Low priority states can be interrupted by anything
        /// - Normal priority states can be interrupted by Normal+ priority
        /// - High priority states can only be interrupted by High or Critical
        /// - Critical priority states can only be interrupted by other Critical states
        /// 
        /// Example:
        /// public StatePriority Priority => StatePriority.High; // For important animations
        /// </summary>
        StatePriority Priority { get; }

        /// <summary>
        /// Whether this state can be interrupted by global transitions.
        /// If false, global transitions will be queued or ignored.
        /// 
        /// Set to false for states that must complete (death, cutscenes).
        /// Set to true for normal gameplay states.
        /// </summary>
        bool AllowInterruption { get; }

        /// <summary>
        /// Called only once when the state instance is first created.
        /// Use for one-time initialization, caching references, etc.
        /// 
        /// This is NOT called every time you enter the state - use OnEnter for that.
        /// </summary>
        /// <param name="client">The client instance that owns this state</param>
        void Init(TClient client);

        /// <summary>
        /// Validates whether this state can be entered.
        /// Called BEFORE OnEnter() during state transitions.
        /// 
        /// Use to check preconditions:
        /// - Does the character have required items/abilities?
        /// - Is the environment suitable?
        /// - Are prerequisites met?
        /// 
        /// If this returns false, the transition is blocked and the FSM stays
        /// in the current state.
        /// 
        /// Example:
        /// public bool CanEnter(Enemy client)
        /// {
        ///     // Can only attack if has weapon
        ///     return client.HasWeapon && client.IsAlive;
        /// }
        /// </summary>
        /// <param name="client">The client attempting to enter this state</param>
        /// <returns>True if state can be entered, false to block the transition</returns>
        bool CanEnter(TClient client);

        /// <summary>
        /// Validates whether this state can be exited.
        /// Called BEFORE OnExit() during state transitions.
        /// 
        /// Use to prevent premature exits:
        /// - Animation must complete
        /// - Action cannot be cancelled
        /// - Critical sequence in progress
        /// 
        /// If this returns false, the transition is blocked and the FSM stays
        /// in this state.
        /// 
        /// Example:
        /// public bool CanExit(Enemy client)
        /// {
        ///     // Can't exit until attack animation completes
        ///     return stateTimer >= ATTACK_DURATION;
        /// }
        /// </summary>
        /// <param name="client">The client attempting to exit this state</param>
        /// <returns>True if state can be exited, false to block the transition</returns>
        bool CanExit(TClient client);

        /// <summary>
        /// Called every time this state becomes the active state.
        /// Use for setup that should happen each time you enter the state.
        /// 
        /// Examples:
        /// - Start animations
        /// - Set NavMeshAgent destinations
        /// - Reset state-specific variables
        /// - Play sound effects
        /// </summary>
        /// <param name="client">The client instance entering this state</param>
        void OnEnter(TClient client);

        /// <summary>
        /// Called every frame while this state is active (Update loop).
        /// This is where your main state logic goes.
        /// 
        /// Return Values:
        /// - null: Stay in current state
        /// - typeof(NextState): Transition to NextState
        /// 
        /// Examples:
        /// public Type Execute(Enemy client)
        /// {
        ///     if (client.Health <= 0)
        ///         return typeof(DeathState);
        ///     
        ///     if (client.PlayerInRange())
        ///         return typeof(AttackState);
        ///     
        ///     return null; // Continue patrolling
        /// }
        /// </summary>
        /// <param name="client">The client instance in this state</param>
        /// <returns>The Type of the next state, or null to stay in current state</returns>
        Type Execute(TClient client);

        /// <summary>
        /// Called every fixed frame while this state is active (FixedUpdate loop).
        /// Use for physics-based logic:
        /// - Rigidbody forces
        /// - Physics raycasts
        /// - Movement that should sync with physics
        /// 
        /// This runs at a fixed timestep (default 0.02s / 50Hz),
        /// separate from the variable frame rate Update.
        /// </summary>
        /// <param name="client">The client instance in this state</param>
        void ExecuteFixed(TClient client);

        /// <summary>
        /// Called every frame after Execute, late in the frame (LateUpdate loop).
        /// Use for logic that should run after all Updates:
        /// - Camera follow
        /// - Final position adjustments
        /// - IK adjustments
        /// - Looking at targets
        /// 
        /// This ensures your logic runs after all other objects have updated.
        /// </summary>
        /// <param name="client">The client instance in this state</param>
        void ExecuteLate(TClient client);

        /// <summary>
        /// Called when leaving this state (transitioning to another state).
        /// Use for cleanup:
        /// - Stop animations
        /// - Clean up timers
        /// - Restore default values
        /// - Stop sound effects
        /// 
        /// This is called AFTER CanExit() returns true.
        /// </summary>
        /// <param name="client">The client instance exiting this state</param>
        void OnExit(TClient client);
    }

    /// <summary>
    /// Interface that must be implemented by client components using the FSM.
    /// Your MonoBehaviour class should implement this interface.
    /// 
    /// The self-referencing generic constraint ensures type safety:
    /// - Enemy implements IGTFSMClient<Enemy>
    /// - Player implements IGTFSMClient<Player>
    /// 
    /// This allows states to receive the correct client type without casting.
    /// 
    /// Example:
    /// public class Enemy : MonoBehaviour, IGTFSMClient<Enemy>
    /// {
    ///     public GTFSMState<Enemy> CurrentState { get; set; }
    ///     public GTFSMState<Enemy> PreviousState { get; set; }
    ///     public FSMDebugInfo<Enemy> DebugInfo { get; set; }
    ///     
    ///     // Define your own event
    ///     public event Action<string, string> OnStateTransition;
    ///     
    ///     // Implement the notification method
    ///     public void NotifyStateTransition(string fromState, string toState)
    ///     {
    ///         OnStateTransition?.Invoke(fromState, toState);
    ///     }
    ///     
    ///     // Your game-specific properties
    ///     public NavMeshAgent Agent { get; private set; }
    ///     public Transform[] PatrolPath;
    /// }
    /// </summary>
    /// <typeparam name="TClient">The client type (usually the class implementing this interface)</typeparam>
    public interface IGTFSMClient<TClient> where TClient : class, IGTFSMClient<TClient>
    {
        /// <summary>
        /// The currently active state.
        /// Set by GTFSM.SetState() - do not set manually.
        /// </summary>
        GTFSMState<TClient> CurrentState { get; set; }

        /// <summary>
        /// The state that was active before the current state.
        /// Used for state history and TransitionToPreviousState().
        /// Set automatically by the FSM - do not set manually.
        /// </summary>
        GTFSMState<TClient> PreviousState { get; set; }

        /// <summary>
        /// Debug information for Unity Inspector visualization.
        /// Initialize in Awake(): DebugInfo = new FSMDebugInfo<Enemy>();
        /// 
        /// Tracks:
        /// - Current state name
        /// - Time in current state
        /// - Number of transitions
        /// - State history
        /// </summary>
        FSMDebugInfo<TClient> DebugInfo { get; set; }

        /// <summary>
        /// Optional callback method for state transition notifications.
        /// Called by GTFSM after a successful state transition.
        /// 
        /// Implement this method to receive notifications, then fire your own events
        /// or trigger behaviors. This pattern allows you to define events in your
        /// concrete class while GTFSM can still notify you.
        /// 
        /// Parameters:
        /// - string fromState: Name of the state being exited (null if first state)
        /// - string toState: Name of the state being entered
        /// 
        /// Example implementation:
        /// public event Action<string, string> OnStateTransition;
        /// 
        /// public void NotifyStateTransition(string fromState, string toState)
        /// {
        ///     OnStateTransition?.Invoke(fromState, toState);
        ///     Debug.Log($"State changed: {fromState} -> {toState}");
        /// }
        /// 
        /// Use cases:
        /// - UI updates (show attack icon when entering AttackState)
        /// - Analytics tracking
        /// - Sound/VFX triggering
        /// - Animation synchronization
        /// </summary>
        /// <param name="fromState">The state being exited (null if first state)</param>
        /// <param name="toState">The state being entered</param>
        void NotifyStateTransition(string fromState, string toState);
    }

    /// <summary>
    /// Debug information container for FSM visualization in Unity Inspector.
    /// Tracks state transitions, timing, and history for debugging purposes.
    /// 
    /// Usage:
    /// - Create in Awake(): DebugInfo = new FSMDebugInfo<Enemy>();
    /// - Updated automatically by GTFSM
    /// - Visible in Inspector when fields are serialized
    /// 
    /// Example Inspector fields:
    /// [SerializeField] private string currentStateName;
    /// 
    /// void UpdateDebugInfo()
    /// {
    ///     if (DebugInfo != null)
    ///         currentStateName = DebugInfo.CurrentStateName;
    /// }
    /// </summary>
    /// <typeparam name="TClient">The client type this debug info belongs to</typeparam>
    [Serializable]
    public class FSMDebugInfo<TClient> where TClient : class, IGTFSMClient<TClient>
    {
        /// <summary>
        /// Name of the currently active state.
        /// Updated automatically on state transitions.
        /// </summary>
        public string CurrentStateName;

        /// <summary>
        /// Name of the previously active state.
        /// Updated automatically on state transitions.
        /// </summary>
        public string PreviousStateName;

        /// <summary>
        /// How long the client has been in the current state (in seconds).
        /// Reset to 0 on state transitions.
        /// Updated every frame by GTFSM.Execute().
        /// </summary>
        public float TimeInCurrentState;

        /// <summary>
        /// Total number of state transitions that have occurred.
        /// Incremented each time SetState() is called successfully.
        /// Useful for detecting frequent state changes (flipping).
        /// </summary>
        public int StateTransitionCount;

        /// <summary>
        /// History of recent state transitions.
        /// Format: "TransitionNumber: StateName"
        /// Most recent at index 0.
        /// Limited to MaxHistorySize entries.
        /// </summary>
        public List<string> StateHistory = new List<string>();

        /// <summary>
        /// Maximum number of state transitions to keep in history.
        /// Older entries are removed to prevent unbounded growth.
        /// </summary>
        public const int MaxHistorySize = 10;

        /// <summary>
        /// Records a state change for debugging purposes.
        /// Called automatically by GTFSM.SetState().
        /// Updates all tracking fields and adds to history.
        /// </summary>
        /// <param name="newStateName">The name of the state being entered</param>
        public void RecordStateChange(string newStateName)
        {
            PreviousStateName = CurrentStateName;
            CurrentStateName = newStateName;
            TimeInCurrentState = 0f;
            StateTransitionCount++;

            // Add to history (most recent first)
            StateHistory.Insert(0, $"{StateTransitionCount}: {newStateName}");
            
            // Trim history to max size
            if (StateHistory.Count > MaxHistorySize)
                StateHistory.RemoveAt(StateHistory.Count - 1);
        }

        /// <summary>
        /// Updates the time-in-state counter.
        /// Called automatically by GTFSM.Execute() every frame.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame (typically Time.deltaTime)</param>
        public void UpdateTime(float deltaTime)
        {
            TimeInCurrentState += deltaTime;
        }
    }

    /// <summary>
    /// Base class for states providing common functionality.
    /// Inherit from this instead of implementing GTFSMState directly for convenience.
    /// 
    /// Provides:
    /// - Automatic state timer (stateTimer)
    /// - Helper method for updating timer (UpdateTimer)
    /// - Default implementations of lifecycle methods
    /// - Default state priority (Normal)
    /// - Default interruption allowance (true)
    /// - Default transition guards (always allow)
    /// 
    /// You only need to override the methods you actually use.
    /// 
    /// Example:
    /// public class AttackState : GTFSMStateBase<Enemy>
    /// {
    ///     public override StatePriority Priority => StatePriority.High;
    ///     private const float ATTACK_DURATION = 2f;
    ///     
    ///     public override Type Execute(Enemy client)
    ///     {
    ///         UpdateTimer(Time.deltaTime);
    ///         
    ///         if (stateTimer >= ATTACK_DURATION)
    ///             return typeof(PatrolState);
    ///         
    ///         return null;
    ///     }
    /// }
    /// </summary>
    /// <typeparam name="TClient">The client type this state works with</typeparam>
    public abstract class GTFSMStateBase<TClient> : GTFSMState<TClient> where TClient : class, IGTFSMClient<TClient>
    {
        /// <summary>
        /// The name of this state (set automatically to the class name).
        /// </summary>
        public string StateName { get; set; }

        /// <summary>
        /// The priority of this state.
        /// Default is Normal. Override to change:
        /// public override StatePriority Priority => StatePriority.High;
        /// </summary>
        public virtual StatePriority Priority => StatePriority.Normal;

        /// <summary>
        /// Whether this state allows interruption by global transitions.
        /// Default is true. Override to prevent interruptions:
        /// public override bool AllowInterruption => false;
        /// </summary>
        public virtual bool AllowInterruption => true;

        /// <summary>
        /// Tracks how long this state has been active (in seconds).
        /// Reset to 0 in OnEnter by default.
        /// Update manually with UpdateTimer(Time.deltaTime) in your Execute method.
        /// 
        /// Example:
        /// public override Type Execute(Enemy client)
        /// {
        ///     UpdateTimer(Time.deltaTime);
        ///     if (stateTimer >= 5f)
        ///         return typeof(NextState);
        ///     return null;
        /// }
        /// </summary>
        protected float stateTimer;

        /// <summary>
        /// Called once when state is first created.
        /// Override if you need one-time initialization.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void Init(TClient client) { }

        /// <summary>
        /// Validates if this state can be entered.
        /// Default implementation always returns true.
        /// Override to add precondition checks:
        /// 
        /// public override bool CanEnter(Enemy client)
        /// {
        ///     return client.IsAlive && client.HasWeapon;
        /// }
        /// </summary>
        public virtual bool CanEnter(TClient client) => true;

        /// <summary>
        /// Validates if this state can be exited.
        /// Default implementation always returns true.
        /// Override to prevent premature exits:
        /// 
        /// public override bool CanExit(Enemy client)
        /// {
        ///     return stateTimer >= MIN_DURATION;
        /// }
        /// </summary>
        public virtual bool CanExit(TClient client) => true;
        
        /// <summary>
        /// Called when entering the state.
        /// Default implementation resets the state timer.
        /// Override and call base.OnEnter() if you want to keep the timer reset.
        /// </summary>
        public virtual void OnEnter(TClient client) 
        {
            stateTimer = 0f;
        }

        /// <summary>
        /// Called every frame while state is active.
        /// MUST be overridden - this is where your state logic goes.
        /// Return typeof(NextState) to transition, or null to stay.
        /// </summary>
        public abstract Type Execute(TClient client);

        /// <summary>
        /// Called every fixed frame for physics logic.
        /// Override if you need FixedUpdate behavior.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void ExecuteFixed(TClient client) { }

        /// <summary>
        /// Called every late frame after all Updates.
        /// Override if you need LateUpdate behavior.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void ExecuteLate(TClient client) { }

        /// <summary>
        /// Called when exiting the state.
        /// Override if you need cleanup logic.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void OnExit(TClient client) { }

        /// <summary>
        /// Helper method to update the state timer.
        /// Call this in your Execute method to track time in state.
        /// 
        /// Example:
        /// public override Type Execute(Enemy client)
        /// {
        ///     UpdateTimer(Time.deltaTime);
        ///     Debug.Log($"Been in state for {stateTimer} seconds");
        /// }
        /// </summary>
        /// <param name="deltaTime">Time to add (typically Time.deltaTime)</param>
        protected void UpdateTimer(float deltaTime)
        {
            stateTimer += deltaTime;
        }
    }
}