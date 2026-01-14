// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2012-2026 Giuseppe “Pino” De Francesco (DFT Games Studios)
//
// Project: Generically Typed FSM for Unity
// File: Enemy.cs
// Summary: Example implementation of IGTFSMClient for an enemy character.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Attribution: Please retain this header and the NOTICE file when redistributing.
// Citation: If you use this work in a publication, please cite it (see CITATION.cff).

using DFTGames.Tools.GTFSM;
using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Example Enemy AI component demonstrating GTFSM usage.
/// 
/// This component shows:
/// - How to implement IGTFSMClient<Enemy>
/// - Proper FSM initialization and cleanup
/// - Integration with NavMeshAgent
/// - Unity Inspector debugging
/// - Scene Gizmos for visualization
/// 
/// FSM Lifecycle:
/// 1. Awake() - Cache components and create debug info
/// 2. Start() - Register states and set initial state
/// 3. Update/FixedUpdate/LateUpdate() - Execute current state
/// 4. OnDestroy() - Cleanup to prevent memory leaks
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour, IGTFSMClient<Enemy>
{
    #region Inspector Configuration

    [Header("Patrol Configuration")]
    [Tooltip("Waypoints for patrol route. Enemy will cycle through these in order.")]
    public Transform[] path;

    [Header("Debug Settings")]
    [Tooltip("Enable FSM debug information in Inspector and Scene view")]
    [SerializeField] private bool enableDebug = true;

    [Header("FSM Debug Info")]
    [Tooltip("Current active state name (read-only, updated at runtime)")]
    [SerializeField] private string currentStateName;
    
    [Tooltip("How long enemy has been in current state (read-only)")]
    [SerializeField] private float timeInState;
    
    [Tooltip("Total number of state transitions (read-only)")]
    [SerializeField] private int transitionCount;

    #endregion

    #region FSM Required Properties

    // Current index in patrol path (used by PatrolState)
    [HideInInspector]
    public int currentDestination;

    /// <summary>
    /// Currently active FSM state.
    /// Set by GTFSM - do not modify manually.
    /// </summary>
    public GTFSMState<Enemy> CurrentState { get; set; }

    /// <summary>
    /// Previously active FSM state (for state history).
    /// Set by GTFSM - do not modify manually.
    /// </summary>
    public GTFSMState<Enemy> PreviousState { get; set; }

    /// <summary>
    /// Debug information for Unity Inspector visualization.
    /// Tracks state transitions, timing, and history.
    /// Only active when enableDebug is true.
    /// </summary>
    public FSMDebugInfo<Enemy> DebugInfo { get; set; }

    /// <summary>
    /// Event fired when state transitions occur.
    /// Subscribe to this event to react to state changes.
    /// Invoked by NotifyStateTransition implementation.
    /// </summary>
    public event Action<string, string> OnStateTransition;

    /// <summary>
    /// Called by GTFSM after state transitions.
    /// Fires the OnStateTransition event and can trigger other behaviors.
    /// </summary>
    public void NotifyStateTransition(string fromState, string toState)
    {
        // Fire the event for any subscribers
        OnStateTransition?.Invoke(fromState, toState);
    }

    #endregion

    #region Cached Components

    /// <summary>
    /// Cached NavMeshAgent component.
    /// Cached in Awake() to avoid GetComponent calls during gameplay.
    /// Public read-only access for states to use.
    /// </summary>
    public NavMeshAgent Agent { get; private set; }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Cache components and initialize debug info.
    /// Called before Start().
    /// </summary>
    private void Awake()
    {
        // Cache NavMeshAgent once at startup
        Agent = GetComponent<NavMeshAgent>();
        
        // Initialize debug info only if debugging is enabled
        if (enableDebug)
        {
            DebugInfo = new FSMDebugInfo<Enemy>();
        }
    }

    /// <summary>
    /// Initialize FSM - register states and set initial state.
    /// Called after Awake(), before first Update().
    /// </summary>
    private void Start()
    {
        // Subscribe to state transition events (optional - for demonstration)
        if (enableDebug)
        {
            OnStateTransition += OnStateChanged;
        }

        // Register all states this enemy will use
        // Each registration creates a state instance for THIS specific enemy
        GTFSM.RegisterState<Enemy, PatrolState>(this);
        GTFSM.RegisterState<Enemy, AttackState>(this);
        
        // Set initial state with validation
        if (!GTFSM.SetState(this, typeof(PatrolState)))
        {
            Debug.LogError("[Enemy] Failed to set initial state!");
        }
    }

    /// <summary>
    /// Example event handler for state transitions.
    /// This demonstrates how external systems can react to state changes.
    /// </summary>
    private void OnStateChanged(string fromState, string toState)
    {
        Debug.Log($"[Enemy {gameObject.name}] State transition: {fromState ?? "None"} -> {toState}");
        
        // Example: You could trigger different behaviors here:
        // - Play state-specific sounds
        // - Update UI
        // - Send analytics
        // - Trigger animations
    }

    /// <summary>
    /// Execute current state logic every frame.
    /// This is where state transitions are automatically handled.
    /// </summary>
    private void Update()
    {
        // Execute current state - handles transitions automatically
        GTFSM.Execute(this);
        
        // Update Inspector debug fields only if debugging is enabled
        if (enableDebug)
        {
            UpdateDebugInfo();
        }
    }

    /// <summary>
    /// Execute current state's physics logic.
    /// Use for physics-based state behaviors.
    /// </summary>
    private void FixedUpdate()
    {
        GTFSM.ExecuteFixed(this);
    }

    /// <summary>
    /// Execute current state's late logic.
    /// Runs after all Updates - useful for camera follow, etc.
    /// </summary>
    private void LateUpdate()
    {
        GTFSM.ExecuteLate(this);
    }

    /// <summary>
    /// Copies debug info to Inspector-visible fields.
    /// Called every frame to keep Inspector updated.
    /// Only runs when debugging is enabled.
    /// </summary>
    private void UpdateDebugInfo()
    {
        if (DebugInfo != null)
        {
            currentStateName = DebugInfo.CurrentStateName;
            timeInState = DebugInfo.TimeInCurrentState;
            transitionCount = DebugInfo.StateTransitionCount;
        }
    }

    /// <summary>
    /// Cleanup FSM state when destroyed.
    /// CRITICAL: Prevents memory leaks by removing state references.
    /// </summary>
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (enableDebug)
        {
            OnStateTransition -= OnStateChanged;
        }

        GTFSM.Cleanup(this);
    }

    #endregion

    #region Scene Visualization

    /// <summary>
    /// Draws debug visualization in Scene view.
    /// Shows current state and patrol path.
    /// Only active when enableDebug is true.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Skip if debugging is disabled
        if (!enableDebug)
            return;

        // Draw current state info above enemy
        if (CurrentState != null)
        {
            Gizmos.color = Color.green;
            Vector3 labelPos = transform.position + Vector3.up * 2f;
            
#if UNITY_EDITOR
            // Show state name and time in Scene view
            UnityEditor.Handles.Label(labelPos, $"State: {CurrentState.StateName}\nTime: {timeInState:F1}s");
#endif
        }

        // Draw patrol path visualization
        if (path != null && path.Length > 0)
        {
            Gizmos.color = Color.yellow;
            
            // Draw waypoint spheres and connecting lines
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] != null)
                {
                    // Draw waypoint sphere
                    Gizmos.DrawWireSphere(path[i].position, 0.5f);
                    
                    // Draw line to next waypoint
                    if (i < path.Length - 1 && path[i + 1] != null)
                    {
                        Gizmos.DrawLine(path[i].position, path[i + 1].position);
                    }
                }
            }
            
            // Draw line from last to first waypoint (complete the loop)
            if (path.Length > 1 && path[0] != null && path[path.Length - 1] != null)
            {
                Gizmos.DrawLine(path[path.Length - 1].position, path[0].position);
            }
        }
    }

    #endregion
}