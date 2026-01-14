// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2012-2026 Giuseppe “Pino” De Francesco (DFT Games Studios)
//
// Project: Generically Typed FSM for Unity
// File: PatrolState.cs
// Summary: Example implementation of a patrol state for an enemy character.
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

/// <summary>
/// Example patrol state for Enemy AI.
/// 
/// Behavior:
/// - Moves through waypoints in a loop
/// - Automatically transitions to next waypoint when destination reached
/// - Validates patrol path exists before entering
/// - Uses Low priority (can be interrupted by any state)
/// 
/// This demonstrates:
/// - Using GTFSMStateBase for convenience (timer, etc.)
/// - Accessing client properties directly (no GetComponent)
/// - Transition guards (CanEnter validation)
/// - State priority system
/// - Clean navigation logic with NavMeshAgent
/// </summary>
public class PatrolState : GTFSMStateBase<Enemy>
{
    // Tolerance percentage for destination arrival detection
    private const float DESTINATION_TOLERANCE = 0.3f;

    /// <summary>
    /// Low priority - patrol can be interrupted by any other state.
    /// This is appropriate since patrolling is a default/idle behavior.
    /// </summary>
    public override StatePriority Priority => StatePriority.Low;

    /// <summary>
    /// Patrol allows interruption by global transitions.
    /// This means stun, death, or other critical states can interrupt patrolling.
    /// </summary>
    public override bool AllowInterruption => true;

    /// <summary>
    /// Validates that the enemy can enter patrol state.
    /// Checks if a valid patrol path is configured.
    /// 
    /// This prevents entering patrol state without proper setup,
    /// avoiding runtime errors and providing clear feedback.
    /// </summary>
    public override bool CanEnter(Enemy client)
    {
        if (client.path == null || client.path.Length == 0)
        {
            Debug.LogWarning($"[PatrolState] Cannot enter: No patrol path configured for {client.gameObject.name}");
            return false;
        }

        if (client.Agent == null)
        {
            Debug.LogWarning($"[PatrolState] Cannot enter: No NavMeshAgent found on {client.gameObject.name}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Patrol state can always be exited - no restrictions.
    /// Override this if you want to prevent early exits.
    /// </summary>
    public override bool CanExit(Enemy client)
    {
        // Patrol can be interrupted at any time
        return true;
    }

    /// <summary>
    /// Called when entering patrol state.
    /// Sets up initial destination for patrolling.
    /// </summary>
    public override void OnEnter(Enemy client)
    {
        base.OnEnter(client); // Resets stateTimer
        
        // Set initial destination
        SetNextDestination(client);
    }

    /// <summary>
    /// Main patrol logic - called every frame.
    /// Checks if destination reached and moves to next waypoint.
    /// </summary>
    public override Type Execute(Enemy client)
    {
        // Update timer (available from GTFSMStateBase)
        UpdateTimer(Time.deltaTime);

        // Calculate arrival threshold with tolerance
        float changeDistance = client.Agent.stoppingDistance + 
                              (client.Agent.stoppingDistance * DESTINATION_TOLERANCE);
        
        // Check if arrived at current destination
        // pathPending check prevents premature waypoint switching during path calculation
        if (client.Agent.remainingDistance <= changeDistance && !client.Agent.pathPending)
        {
            // Cycle to next waypoint (wraps around using modulo)
            client.currentDestination = (client.currentDestination + 1) % client.path.Length;
            SetNextDestination(client);
        }

        // Return null to stay in patrol state
        // Could return typeof(AttackState) if player detected, for example
        return null;
    }

    /// <summary>
    /// Called when exiting patrol state.
    /// Override if you need cleanup (e.g., stop animations).
    /// </summary>
    public override void OnExit(Enemy client)
    {
        base.OnExit(client);
        // Cleanup logic here if needed
    }

    /// <summary>
    /// Helper method to set the NavMeshAgent's next destination.
    /// Includes null safety check for waypoints.
    /// </summary>
    private void SetNextDestination(Enemy client)
    {
        if (client.path[client.currentDestination] != null)
        {
            client.Agent.SetDestination(client.path[client.currentDestination].position);
        }
    }
}