// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2012-2026 Giuseppe “Pino” De Francesco (DFT Games Studios)
//
// Project: Generically Typed FSM for Unity
// File: GTFSM.cs
// Summary: Core implementation of the generically typed finite state machine for Unity.
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
using UnityEngine;

namespace DFTGames.Tools.GTFSM
{
    /// <summary>
    /// Generic Typed Finite State Machine - A high-performance, type-safe FSM for Unity.
    /// 
    /// Key Features:
    /// - Per-instance state management (each client gets its own state instances)
    /// - Zero boxing/unboxing (generic dictionaries throughout)
    /// - No GetComponent calls during runtime
    /// - Automatic state transition handling
    /// - Support for all Unity update loops (Update, FixedUpdate, LateUpdate)
    /// - Global transitions for interrupt states (stun, death, etc.)
    /// - State history and debugging support
    /// - Comprehensive error handling and validation
    /// 
    /// Usage:
    /// 1. Implement IGTFSMClient<TClient> on your MonoBehaviour
    /// 2. Register states in Start(): GTFSM.RegisterState<Client, StateType>(this)
    /// 3. Set initial state: GTFSM.SetState(this, typeof(StateType))
    /// 4. Call GTFSM.Execute(this) in Update()
    /// 5. Call GTFSM.Cleanup(this) in OnDestroy()
    /// </summary>
    public class GTFSM
    {
        // Singleton instance - ensures only one FSM manager exists
        private static GTFSM instance;
        
        // Maps each client instance to its own state manager
        // Key: Client instance (e.g., specific Enemy #1, Enemy #2)
        // Value: InstanceStateManager for that specific client
        private Dictionary<object, object> instanceStates = new Dictionary<object, object>();

        /// <summary>
        /// Singleton accessor - lazily creates the GTFSM instance on first access
        /// </summary>
        private static GTFSM Me
        {
            get
            {
                if (instance == null)
                    instance = new GTFSM();

                return instance;
            }
        }

        /// <summary>
        /// Manages all states for a single client instance.
        /// Each client (e.g., Enemy #1) has its own InstanceStateManager,
        /// ensuring states are not shared between different game objects.
        /// 
        /// This solves the critical issue where multiple instances would
        /// interfere with each other's state data.
        /// </summary>
        private class InstanceStateManager<TClient> where TClient : class, IGTFSMClient<TClient>
        {
            // Fast lookup by state Type (e.g., typeof(PatrolState))
            private Dictionary<Type, GTFSMState<TClient>> statesByType = new Dictionary<Type, GTFSMState<TClient>>();
            
            // Alternative lookup by state name string
            private Dictionary<string, GTFSMState<TClient>> statesByName = new Dictionary<string, GTFSMState<TClient>>();
            
            // Global transition state - when set, overrides normal state transitions
            // Used for interrupt states like stun, death, cutscenes, etc.
            private Type globalTransitionState;

            /// <summary>
            /// Registers a new state for this client instance.
            /// Creates a new state instance and initializes it.
            /// If the state is already registered, returns the existing instance.
            /// </summary>
            /// <typeparam name="TState">The state type to register (must be a class with parameterless constructor)</typeparam>
            /// <param name="client">The client instance that will use this state</param>
            /// <returns>The registered state instance</returns>
            public TState RegisterState<TState>(TClient client) 
                where TState : class, GTFSMState<TClient>, new()
            {
                Type stateType = typeof(TState);
                
                // Return existing state if already registered (idempotent operation)
                if (statesByType.ContainsKey(stateType))
                    return statesByType[stateType] as TState;

                // Create and configure new state instance
                TState newState = new TState();
                newState.StateName = stateType.Name;
                
                // Store in both dictionaries for flexible lookup
                statesByType[stateType] = newState;
                statesByName[newState.StateName] = newState;
                
                // Initialize state with client reference (one-time setup)
                newState.Init(client);
                return newState;
            }

            /// <summary>
            /// Retrieves a registered state for this client by its Type.
            /// Returns null if the state is not registered.
            /// </summary>
            /// <param name="stateType">The Type of the state to retrieve</param>
            /// <returns>The state instance, or null if not found</returns>
            public GTFSMState<TClient> GetState(Type stateType)
            {
                if (stateType == null || !statesByType.ContainsKey(stateType))
                    return null;

                return statesByType[stateType];
            }

            /// <summary>
            /// Retrieves a registered state for this client by its name string.
            /// Returns null if the state is not registered.
            /// </summary>
            /// <param name="stateName">The name of the state to retrieve</param>
            /// <returns>The state instance, or null if not found</returns>
            public GTFSMState<TClient> GetState(string stateName)
            {
                if (string.IsNullOrEmpty(stateName) || !statesByName.ContainsKey(stateName))
                    return null;

                return statesByName[stateName];
            }

            /// <summary>
            /// Checks if a state is registered for this client without retrieving it.
            /// </summary>
            /// <param name="stateType">The Type to check</param>
            /// <returns>True if the state is registered, false otherwise</returns>
            public bool HasState(Type stateType)
            {
                return stateType != null && statesByType.ContainsKey(stateType);
            }

            /// <summary>
            /// Sets a global transition state that will override normal state transitions.
            /// On the next Execute() call, the FSM will immediately transition to this state.
            /// Useful for interrupt states like stun, death, or forced animations.
            /// </summary>
            /// <param name="stateType">The state type to transition to globally</param>
            public void SetGlobalTransition(Type stateType)
            {
                globalTransitionState = stateType;
            }

            /// <summary>
            /// Gets the currently set global transition state, if any.
            /// </summary>
            /// <returns>The global transition state Type, or null if none is set</returns>
            public Type GetGlobalTransition()
            {
                return globalTransitionState;
            }

            /// <summary>
            /// Clears the global transition, returning to normal state transition behavior.
            /// </summary>
            public void ClearGlobalTransition()
            {
                globalTransitionState = null;
            }

            /// <summary>
            /// Cleans up all states for this client instance.
            /// Called when the client is destroyed to prevent memory leaks.
            /// </summary>
            public void Cleanup()
            {
                statesByType.Clear();
                statesByName.Clear();
                globalTransitionState = null;
            }
        }

        #region Public API

        /// <summary>
        /// Executes the current state's Update logic.
        /// Call this in your client's Update() method.
        /// 
        /// This method:
        /// 1. Checks for global transitions (interrupt states)
        /// 2. Executes the current state's logic
        /// 3. Handles automatic state transitions based on return value
        /// 4. Updates debug information
        /// </summary>
        /// <typeparam name="TClient">The client type (e.g., Enemy)</typeparam>
        /// <param name="client">The client instance to execute</param>
        public static void Execute<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            Me._Execute(client);
        }

        /// <summary>
        /// Executes the current state's FixedUpdate logic.
        /// Call this in your client's FixedUpdate() method.
        /// Used for physics-based state logic.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance to execute</param>
        public static void ExecuteFixed<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            Me._ExecuteFixed(client);
        }

        /// <summary>
        /// Executes the current state's LateUpdate logic.
        /// Call this in your client's LateUpdate() method.
        /// Useful for camera follow, final position adjustments, etc.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance to execute</param>
        public static void ExecuteLate<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            Me._ExecuteLate(client);
        }

        /// <summary>
        /// Registers a state for a specific client instance.
        /// Must be called before the state can be used.
        /// 
        /// Example: GTFSM.RegisterState<Enemy, PatrolState>(this);
        /// 
        /// Note: Each client instance gets its own state instance,
        /// so multiple enemies don't share the same PatrolState.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <typeparam name="TState">The state type to register (must have parameterless constructor)</typeparam>
        /// <param name="client">The client instance</param>
        /// <returns>The registered state instance</returns>
        public static TState RegisterState<TClient, TState>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
            where TState : class, GTFSMState<TClient>, new()
        {
            return Me._RegisterState<TClient, TState>(client);
        }

        /// <summary>
        /// Sets the current state by Type, with validation.
        /// 
        /// Example: GTFSM.SetState(this, typeof(AttackState));
        /// 
        /// This triggers:
        /// 1. CurrentState.OnExit() on the old state
        /// 2. State transition
        /// 3. NewState.OnEnter() on the new state
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="stateType">The Type of the state to transition to</param>
        /// <returns>True if transition succeeded, false if state not found or same as current</returns>
        public static bool SetState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._SetState(client, stateType);
        }

        /// <summary>
        /// Sets the current state directly using a state instance.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="state">The state instance to transition to</param>
        /// <returns>True if transition succeeded, false if state is null or same as current</returns>
        public static bool SetState<TClient>(TClient client, GTFSMState<TClient> state) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._SetState(client, state);
        }

        /// <summary>
        /// Transitions back to the previous state.
        /// Useful for returning from interrupt states (e.g., stun back to attack).
        /// 
        /// Example:
        /// - Was in AttackState
        /// - Got stunned (transitioned to StunState)
        /// - Call TransitionToPreviousState() to return to AttackState
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <returns>True if transition succeeded, false if no previous state exists</returns>
        public static bool TransitionToPreviousState<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._TransitionToPreviousState(client);
        }

        /// <summary>
        /// Sets a global transition that will override normal state transitions.
        /// On the next Execute() call, the FSM will immediately transition to this state.
        /// 
        /// Use cases:
        /// - Stun effects (interrupt combat to stun)
        /// - Death animations (interrupt any action for death)
        /// - Cutscenes (force character into scripted state)
        /// - Emergency behaviors (interrupt patrol for alarm response)
        /// 
        /// Example:
        /// public void OnStunned()
        /// {
        ///     GTFSM.SetGlobalTransition<Enemy>(this, typeof(StunState));
        /// }
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="stateType">The state type to transition to</param>
        public static void SetGlobalTransition<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            Me._SetGlobalTransition(client, stateType);
        }

        /// <summary>
        /// Clears any active global transition, returning to normal state behavior.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        public static void ClearGlobalTransition<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            Me._ClearGlobalTransition(client);
        }

        /// <summary>
        /// Gets a registered state by Type.
        /// Returns null if the state is not registered.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="stateType">The state type to retrieve</param>
        /// <returns>The state instance, or null if not found</returns>
        public static GTFSMState<TClient> GetState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._GetState(client, stateType);
        }

        /// <summary>
        /// Gets a registered state by name string.
        /// Returns null if the state is not registered.
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="stateName">The name of the state to retrieve</param>
        /// <returns>The state instance, or null if not found</returns>
        public static GTFSMState<TClient> GetState<TClient>(TClient client, string stateName) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._GetState(client, stateName);
        }

        /// <summary>
        /// Checks if a state is registered for this client.
        /// Use this before attempting to transition to a state to avoid errors.
        /// 
        /// Example:
        /// if (GTFSM.HasState(this, typeof(AttackState)))
        /// {
        ///     GTFSM.SetState(this, typeof(AttackState));
        /// }
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance</param>
        /// <param name="stateType">The state type to check</param>
        /// <returns>True if the state is registered, false otherwise</returns>
        public static bool HasState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            return Me._HasState(client, stateType);
        }

        /// <summary>
        /// Cleans up all states for a specific client instance.
        /// MUST be called in the client's OnDestroy() method to prevent memory leaks.
        /// 
        /// This removes:
        /// - All registered states for this instance
        /// - State references
        /// - Global transition data
        /// - Debug information
        /// 
        /// Example:
        /// private void OnDestroy()
        /// {
        ///     GTFSM.Cleanup(this);
        /// }
        /// </summary>
        /// <typeparam name="TClient">The client type</typeparam>
        /// <param name="client">The client instance to clean up</param>
        public static void Cleanup<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            Me._Cleanup(client);
        }

        #endregion

        #region Private Implementation

        /// <summary>
        /// Internal implementation of Execute.
        /// Handles global transitions, state execution, and automatic transitions.
        /// Respects state priorities and interruption rules.
        /// </summary>
        private void _Execute<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            // Safety check - ensure client and state exist
            if (client == null || client.CurrentState == null)
                return;

            // Get the state manager for this specific client instance
            InstanceStateManager<TClient> manager = GetManager(client);
            if (manager == null)
                return;

            // Update debug timer for Inspector visualization
            if (client.DebugInfo != null)
                client.DebugInfo.UpdateTime(Time.deltaTime);

            // Check for global transition (interrupt states) - these take priority
            Type globalTransition = manager.GetGlobalTransition();
            if (globalTransition != null)
            {
                GTFSMState<TClient> globalState = manager.GetState(globalTransition);
                if (globalState != null && globalState != client.CurrentState)
                {
                    // Check if current state allows interruption
                    if (client.CurrentState.AllowInterruption)
                    {
                        // Execute the global transition and clear it
                        if (_SetState(client, globalState))
                        {
                            manager.ClearGlobalTransition();
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[GTFSM] Global transition to {globalState.StateName} blocked: " +
                                       $"{client.CurrentState.StateName} does not allow interruption");
                        manager.ClearGlobalTransition(); // Clear the failed transition
                    }
                    return;
                }
            }

            // Execute current state logic - returns next state Type or null to stay
            Type nextStateType = client.CurrentState.Execute(client);
            
            // Handle state transition if requested
            if (nextStateType != null)
            {
                GTFSMState<TClient> nextState = manager.GetState(nextStateType);
                if (nextState != null && nextState != client.CurrentState)
                {
                    // SetState will handle priority checking and guards
                    _SetState(client, nextState);
                }
                else if (nextState == null)
                {
                    // Helpful error message for debugging
                    Debug.LogWarning($"[GTFSM] State transition failed: State type '{nextStateType.Name}' is not registered for client.");
                }
            }
        }

        /// <summary>
        /// Internal implementation of ExecuteFixed.
        /// Calls the current state's physics update logic.
        /// </summary>
        private void _ExecuteFixed<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || client.CurrentState == null)
                return;

            client.CurrentState.ExecuteFixed(client);
        }

        /// <summary>
        /// Internal implementation of ExecuteLate.
        /// Calls the current state's late update logic.
        /// </summary>
        private void _ExecuteLate<TClient>(TClient client) where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || client.CurrentState == null)
                return;

            client.CurrentState.ExecuteLate(client);
        }

        /// <summary>
        /// Internal implementation of RegisterState.
        /// Creates or retrieves the state manager and registers the state.
        /// </summary>
        private TState _RegisterState<TClient, TState>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
            where TState : class, GTFSMState<TClient>, new()
        {
            if (client == null)
            {
                Debug.LogError("[GTFSM] Cannot register state: client is null");
                return default(TState);
            }

            InstanceStateManager<TClient> manager = GetOrCreateManager(client);
            return manager.RegisterState<TState>(client);
        }

        /// <summary>
        /// Internal implementation of SetState by Type.
        /// Validates the state exists before attempting transition.
        /// </summary>
        private bool _SetState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || stateType == null)
                return false;

            InstanceStateManager<TClient> manager = GetManager(client);
            if (manager == null)
            {
                Debug.LogError($"[GTFSM] Cannot set state: No states registered for client");
                return false;
            }

            GTFSMState<TClient> state = manager.GetState(stateType);
            if (state == null)
            {
                Debug.LogError($"[GTFSM] Cannot set state: State '{stateType.Name}' is not registered. Call RegisterState first.");
                return false;
            }

            return _SetState(client, state);
        }

        /// <summary>
        /// Internal implementation of SetState by instance.
        /// Handles the complete state transition process:
        /// 1. Check state priority and interruption rules
        /// 2. Validate CanExit on current state
        /// 3. Validate CanEnter on new state
        /// 4. Exits old state (OnExit)
        /// 5. Updates state references
        /// 6. Records debug info
        /// 7. Enters new state (OnEnter)
        /// 8. Notifies client of transition
        /// </summary>
        private bool _SetState<TClient>(TClient client, GTFSMState<TClient> newState) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || newState == null)
                return false;

            // Don't transition to same state
            if (client.CurrentState == newState)
                return false;

            // Check priority-based interruption rules
            if (client.CurrentState != null)
            {
                // Check if new state has sufficient priority to interrupt current state
                if (newState.Priority < client.CurrentState.Priority)
                {
                    Debug.LogWarning($"[GTFSM] State transition blocked: {newState.StateName} (Priority: {newState.Priority}) " +
                                   $"cannot interrupt {client.CurrentState.StateName} (Priority: {client.CurrentState.Priority})");
                    return false;
                }

                // Check if current state allows exit
                if (!client.CurrentState.CanExit(client))
                {
                    Debug.LogWarning($"[GTFSM] State transition blocked: {client.CurrentState.StateName}.CanExit() returned false");
                    return false;
                }
            }

            // Check if new state allows entry
            if (!newState.CanEnter(client))
            {
                Debug.LogWarning($"[GTFSM] State transition blocked: {newState.StateName}.CanEnter() returned false");
                return false;
            }

            // Store state names for notification
            string previousStateName = client.CurrentState?.StateName;
            string newStateName = newState.StateName;

            // Exit current state
            if (client.CurrentState != null)
                client.CurrentState.OnExit(client);

            // Update state references
            client.PreviousState = client.CurrentState;
            client.CurrentState = newState;

            // Record transition in debug info for Inspector
            if (client.DebugInfo != null)
                client.DebugInfo.RecordStateChange(newStateName);

            // Enter new state
            client.CurrentState.OnEnter(client);

            // Notify client of transition (client can fire events, update UI, etc.)
            try
            {
                client.NotifyStateTransition(previousStateName, newStateName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GTFSM] Error in NotifyStateTransition: {e.Message}");
            }

            return true;
        }

        /// <summary>
        /// Internal implementation of TransitionToPreviousState.
        /// </summary>
        private bool _TransitionToPreviousState<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || client.PreviousState == null)
                return false;

            return _SetState(client, client.PreviousState);
        }

        /// <summary>
        /// Internal implementation of SetGlobalTransition.
        /// Validates the state exists before setting the global transition.
        /// </summary>
        private void _SetGlobalTransition<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null)
                return;

            InstanceStateManager<TClient> manager = GetManager(client);
            if (manager == null)
            {
                Debug.LogWarning("[GTFSM] Cannot set global transition: No states registered for client");
                return;
            }

            if (!manager.HasState(stateType))
            {
                Debug.LogWarning($"[GTFSM] Cannot set global transition: State '{stateType.Name}' is not registered");
                return;
            }

            manager.SetGlobalTransition(stateType);
        }

        /// <summary>
        /// Internal implementation of ClearGlobalTransition.
        /// </summary>
        private void _ClearGlobalTransition<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null)
                return;

            InstanceStateManager<TClient> manager = GetManager(client);
            manager?.ClearGlobalTransition();
        }

        /// <summary>
        /// Internal implementation of GetState by Type.
        /// </summary>
        private GTFSMState<TClient> _GetState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || stateType == null)
                return null;

            InstanceStateManager<TClient> manager = GetManager(client);
            return manager?.GetState(stateType);
        }

        /// <summary>
        /// Internal implementation of GetState by name.
        /// </summary>
        private GTFSMState<TClient> _GetState<TClient>(TClient client, string stateName) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || string.IsNullOrEmpty(stateName))
                return null;

            InstanceStateManager<TClient> manager = GetManager(client);
            return manager?.GetState(stateName);
        }

        /// <summary>
        /// Internal implementation of HasState.
        /// </summary>
        private bool _HasState<TClient>(TClient client, Type stateType) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null)
                return false;

            InstanceStateManager<TClient> manager = GetManager(client);
            return manager != null && manager.HasState(stateType);
        }

        /// <summary>
        /// Internal implementation of Cleanup.
        /// Removes the client's state manager and cleans up all states.
        /// </summary>
        private void _Cleanup<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || !instanceStates.ContainsKey(client))
                return;

            // Call cleanup on the state manager
            if (instanceStates[client] is InstanceStateManager<TClient> manager)
                manager.Cleanup();

            // Remove from dictionary
            instanceStates.Remove(client);
        }

        /// <summary>
        /// Gets or creates an InstanceStateManager for the given client.
        /// Ensures each client has its own isolated state management.
        /// </summary>
        private InstanceStateManager<TClient> GetOrCreateManager<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (!instanceStates.ContainsKey(client))
                instanceStates[client] = new InstanceStateManager<TClient>();

            return instanceStates[client] as InstanceStateManager<TClient>;
        }

        /// <summary>
        /// Gets the InstanceStateManager for the given client, if it exists.
        /// Returns null if the client has not registered any states yet.
        /// </summary>
        private InstanceStateManager<TClient> GetManager<TClient>(TClient client) 
            where TClient : class, IGTFSMClient<TClient>
        {
            if (client == null || !instanceStates.ContainsKey(client))
                return null;

            return instanceStates[client] as InstanceStateManager<TClient>;
        }

        #endregion
    }
}