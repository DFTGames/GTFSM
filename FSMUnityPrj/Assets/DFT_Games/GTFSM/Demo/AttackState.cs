// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2012-2026 Giuseppe “Pino” De Francesco (DFT Games Studios)
//
// Project: Generically Typed FSM for Unity
// File: AttackState.cs
// Summary: Example implementation of an attack state for an enemy character.
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
/// Example attack state for Enemy AI.
/// 
/// Behavior:
/// - Stops movement during attack
/// - Attacks for a fixed duration
/// - Cannot be interrupted until minimum duration passes
/// - Returns to patrol after attack completes
/// 
/// This demonstrates:
/// - Timer-based state transitions
/// - Controlling NavMeshAgent during state
/// - Using state timer from GTFSMStateBase
/// - Higher priority to prevent interruption
/// - CanExit guard to ensure attack completes
/// - Automatic transition after time expires
/// </summary>
public class AttackState : GTFSMStateBase<Enemy>
{
    // How long the attack animation/action takes
    private const float ATTACK_DURATION = 2f;
    
    // Minimum time before attack can be cancelled
    private const float MIN_ATTACK_DURATION = 0.5f;

    /// <summary>
    /// High priority - attack cannot be interrupted by normal or low priority states.
    /// Only other high or critical priority states can interrupt this.
    /// This ensures attack animations complete properly.
    /// </summary>
    public override StatePriority Priority => StatePriority.High;

    /// <summary>
    /// Attack allows interruption by global transitions (e.g., death, stun).
    /// Set to false if you want attack to be completely uninterruptible.
    /// </summary>
    public override bool AllowInterruption => true;

    /// <summary>
    /// Validates that the enemy can enter attack state.
    /// Add conditions like: has weapon, target in range, etc.
    /// </summary>
    public override bool CanEnter(Enemy client)
    {
        // Add your attack preconditions here
        // Example: return client.HasWeapon && client.TargetInRange;
        return true;
    }

    /// <summary>
    /// Prevents attack from being cancelled too early.
    /// Attack must run for at least MIN_ATTACK_DURATION before it can be exited.
    /// This prevents animation glitches and ensures attacks feel impactful.
    /// </summary>
    public override bool CanExit(Enemy client)
    {
        // Don't allow exit until minimum duration has passed
        return stateTimer >= MIN_ATTACK_DURATION;
    }

    /// <summary>
    /// Called when entering attack state.
    /// Stops agent movement and logs state entry.
    /// </summary>
    public override void OnEnter(Enemy client)
    {
        base.OnEnter(client); // Resets stateTimer to 0
        
        // Stop movement during attack
        if (client.Agent != null)
            client.Agent.isStopped = true;

        Debug.Log("[AttackState] Entered attack state");
    }

    /// <summary>
    /// Main attack logic - called every frame.
    /// Counts time and transitions back to patrol when attack completes.
    /// </summary>
    public override Type Execute(Enemy client)
    {
        // Update the timer (stateTimer is from GTFSMStateBase)
        UpdateTimer(Time.deltaTime);

        // Check if attack duration has elapsed
        if (stateTimer >= ATTACK_DURATION)
        {
            // Transition back to patrol state
            return typeof(PatrolState);
        }

        // Stay in attack state
        // Could check for player death, interrupt conditions, etc. here
        return null;
    }

    /// <summary>
    /// Called when exiting attack state.
    /// Resumes agent movement and logs state exit.
    /// </summary>
    public override void OnExit(Enemy client)
    {
        base.OnExit(client);
        
        // Resume movement
        if (client.Agent != null)
            client.Agent.isStopped = false;

        Debug.Log("[AttackState] Exited attack state");
    }
}