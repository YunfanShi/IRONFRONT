# PROJECT IRONFRONT — Multi-Map, Vehicle and Game Mode Expansion

Repository: https://github.com/YunfanShi/IRONFRONT

## Objective

Expand the existing PROJECT IRONFRONT 0.15.0 game with multiple distinctive battlefield maps, new fictional vehicle categories, additional game modes, and improved cooperation between maps, AI commanders, and vehicles.

This is an upgrade of an existing Three.js and TypeScript project, not a rewrite.

Preserve the existing FPS controls, Conquest and Breakthrough modes, eight vehicle categories, AI commanders, squad system, optional LAN functionality, equipment, UI, and gameplay systems.

## Step 1 — Audit and Multi-Map Architecture

Inspect the repository, especially:

- src/world/Layout.ts
- src/core/math.ts
- src/core/Battle.ts
- src/rendering/WorldView.ts
- src/rendering/EnvironmentArt.ts
- src/vehicles/Vehicle.ts
- src/ai/Commander.ts
- src/ui/UI.ts

The current implementation relies on global constants and a fixed height function.

Refactor the map system to support independently selectable maps.

Introduce an appropriate MapDefinition structure containing:

- Map identifier and display name
- Dimensions and boundaries
- Terrain generation parameters
- Objective positions
- Base and spawn positions
- Buildings and environmental objects
- Collision data
- Navigation information
- Vehicle spawn zones
- Road and accessible area definitions
- Weather and visual configuration
- Supported game modes
- Allowed vehicle categories

Do not keep hidden dependencies on the original map.

Ensure map switching correctly updates rendering, navigation, objectives, AI behavior, collision, respawning, minimaps, and match state.

## Step 2 — Preserve and Upgrade the Existing Map

Retain the current Industrial Frontier map.

Improve its environmental diversity, road connectivity, recognizable landmarks, building appearance, and performance.

Do not alter its existing gameplay balance unnecessarily.

Use this map as the regression-testing baseline.

## Step 3 — Implement Dust Horizon

Create a new original desert battlefield approximately 950 × 950 world units.

Include:

- Desert terrain and hills
- A canyon area
- Industrial ruins
- A small settlement
- A road junction
- An energy facility
- Five distinct capture points
- Two valid faction bases
- Connected vehicle routes
- Accessible infantry paths
- Environmental details and distant scenery

Ensure that important gameplay routes remain navigable.

Avoid generating a large empty plane with randomly placed boxes.

The new map should have a distinct visual identity and meaningful gameplay geography.

Include suitable vehicle parking areas and navigation routes.

## Step 4 — Additional Map Concepts

Prepare extensible definitions for:

Metro Collapse:
- Dense urban environment
- Streets, intersections, elevated roads
- Accessible interiors where navigation works
- Pedestrian-oriented gameplay
- Smaller overall scale

Tidebreaker:
- Industrial coastal environment
- Islands, harbor structures, bridges
- Future support for water vehicles
- Ground, air, and water navigation separated appropriately

Do not declare these maps completed until their terrain, navigation, objectives, spawns, and actual gameplay are implemented.

Prioritize one finished map over several incomplete ones.

## Step 5 — Expand the Vehicle Framework

Preserve all existing vehicle models and functions.

Introduce a data-driven vehicle catalog supporting different fictional vehicle categories and map-specific availability.

Planned additions:

1. L6 NOMAD — two-seat off-road buggy.
2. B12 CARRIER — eight-seat transport vehicle.
3. M6 ATLAS — mobile service vehicle.
4. S4 TIDAL — amphibious transport vehicle.
5. R5 LIFTER — transport helicopter.
6. P3 MARINER — small watercraft.

Do not implement all six immediately.

First complete the buggy and larger transport vehicle.

Later introduce support vehicles, amphibious travel, and watercraft after their navigation systems have been validated.

Each vehicle must have appropriate dimensions, collision, seat logic, movement behavior, animations, UI support, and lifecycle management.

Vehicle types must differ meaningfully in gameplay, not just appearance or speed.

## Step 6 — Vehicle and Commander Integration

Extend the existing intelligence-restricted AI Commander.

Introduce relevant game missions including:

- TRANSPORT
- SERVICE
- RESUPPLY
- REGROUP
- RESERVE

Ensure these missions operate on actual game entities and state.

Vehicle AI should accept missions appropriate to its type.

Do not give vehicles impossible destinations.

AI drivers should be capable of boarding, transporting passengers, reaching accessible destinations, and reporting failed navigation.

Preserve the rule that the AI Commander cannot directly access hidden enemy positions.

## Step 7 — Expand Game Modes

Preserve Conquest and Breakthrough.

Implement the following modes incrementally.

### Escalation

- Multiple active objectives initially.
- Objective availability changes during the match.
- The battlefield progressively focuses on fewer active objectives.
- AI assignments adapt to the changing objectives.
- Match victory and scoring operate correctly.

### King of the Hill

- One active scoring zone at a time.
- The active zone changes periodically.
- Both AI teams autonomously compete for zone ownership.
- Score is awarded according to control duration.
- The match ends at a configured score threshold.

### Supply Line

- Teams move fictional supply resources between designated game locations.
- Deliveries affect game scoring and objective resources.
- Transport AI receives logistics missions.
- Valid deliveries require actual vehicle movement and completion.
- The system must not rely on fake timers that award points without gameplay events.

### Air Rally

- Optional independent flying challenge.
- Aircraft pass through aerial checkpoints.
- Valid timing and checkpoint detection.
- No requirement to integrate this mode into Conquest.

## Step 8 — Map Selection and Settings

Add a polished map-selection interface.

Each map should display:

- Name
- Preview image or rendered preview
- Environment description
- Recommended player count
- Supported modes
- Available vehicles

Players should select a map and mode before starting a match.

Persist selections locally.

Optional LAN hosts must retain authority over map and mode selection, and all participants must load the same map definition.

## Step 9 — Performance

Preserve efficient Three.js rendering.

Use instancing, asset reuse, appropriate culling, and configurable quality.

Ensure the new maps do not cause avoidable performance degradation.

Test at 8v8, 16v16, and 32v32.

Do not assume short automated FPS samples guarantee sustained performance on consumer hardware.

## Step 10 — Acceptance Tests

Verify:

1. The original map still works.
2. The new map can be selected and loaded.
3. Every capture point is accessible.
4. All spawn positions are valid.
5. No vehicles spawn inside obstacles.
6. AI can navigate between important locations.
7. Vehicles only use accessible routes.
8. New modes reach valid victory conditions.
9. The game remains active without player input.
10. Loading a new match clears the previous map's state.
11. The map selection is consistent across LAN participants.
12. The build, TypeScript checks, and automated regression tests pass.

Create explicit test cases for each supported map and mode combination.

## Implementation Priorities

Phase A:
Refactor the multi-map architecture while preserving the original game.

Phase B:
Implement and polish Dust Horizon.

Phase C:
Introduce the two new ground transport vehicles and improve AI transportation.

Phase D:
Add Escalation and King of the Hill.

Phase E:
Expand the maps and specialized vehicle categories.

Phase F:
Introduce Supply Line and optional Air Rally.

At the end of each phase, run tests and document actual results.

Do not claim completion based on source files alone.

## Final Requirement

Make PROJECT IRONFRONT feel like a collection of distinct, living virtual battlefields, not one procedural arena repeated with different colors.

Each map should provide a unique environment, movement experience, vehicle selection, and gameplay rhythm.

Preserve the existing working systems, and deliver each phase as a playable improvement.# IRONFRONT — Intelligent Squad Behavior Overhaul

Repository: https://github.com/YunfanShi/IRONFRONT

Improve the existing v0.15.0 AI so soldiers behave like disciplined, highly coordinated squads in a tactical FPS video game, instead of independent NPCs constantly changing movement directions.

Do not rebuild the game or replace the existing Commander, Intelligence, Navigation, and SquadLeader systems.

## Confirmed Problems to Investigate

1. In `Battle.ts`, individual soldiers frequently recalculate destinations based on local combat conditions.
2. In `SquadLeader.ts`, squad plans do not fully control individual movement decisions.
3. Infantry navigation can fall back to unreachable direct destinations.
4. Squad formation mainly influences movement after separation becomes significant.
5. The existing combat movement and cover logic may cause unnecessary movement oscillation.

Verify these issues in the current source before making changes.

## Required Improvements

### 1. Persistent Intent

Introduce a persistent action-intent system for each AI soldier.

Possible intentions:

- MOVE_WITH_SQUAD
- HOLD_POSITION
- OBSERVE
- INTERACT_WITH_OBJECTIVE
- ASSIST_TEAMMATE
- ENGAGE_IN_GAME_COMBAT
- REGROUP
- RECOVER_FROM_NAVIGATION_FAILURE

Each intent should have a clear start condition, completion condition, interruption condition, and minimum reasonable commitment time.

Avoid resetting intentions during every AI decision update.

### 2. Real Squad Coordination

Make `SquadLeader` responsible for managing squad-level behavior.

The squad should share:

- Current mission
- Leader position
- Group destination
- Member assignments
- Cohesion status
- Important observations
- Regroup requests

The leader decides the squad's general movement state.

Individual agents retain local autonomy for immediate game interactions.

### 3. Better Group Movement

Improve squad movement with:

- Stable relative positions
- Smooth movement transitions
- Member spacing
- Group waiting behavior
- Cohesion recovery
- Reduced unnecessary direction changes
- Local obstacle avoidance

Do not constantly recalculate relative positions directly from the leader's instantaneous rotation.

### 4. Eliminate Random Movement

Investigate why soldiers repeatedly:

- Change destinations
- Stop and restart
- Run away from their assigned squad
- Wander around objectives
- Oscillate between different positions
- Attempt unreachable movement

Fix the underlying decision and navigation causes.

Never use random movement merely to create the illusion of intelligence.

### 5. Squad Behavior Profiles

Support two configurable profiles:

**Regular Squad**
- Normal responsiveness
- Basic group coordination
- Flexible movement
- Standard game AI behavior

**Elite Squad**
- More stable intentions
- Stronger squad cohesion
- Better local observation
- Improved support behaviors
- More deliberate movement
- Faster recognition of failed game tasks
- More consistent interaction with objectives

Elite squads must not gain omniscient vision, perfect aim, or artificial immunity to damage.

They should appear smarter because of better decisions and coordination.

### 6. Animation and Behavior Integration

Connect movement states to appropriate visual animations.

Soldiers should visibly:

- Walk
- Sprint
- Stop
- Observe
- Crouch
- Interact
- Wait for teammates
- Resume movement

Animations must reflect the actual AI state.

Do not create cosmetic behaviors with no gameplay significance.

### 7. Diagnostics

Add useful AI metrics:

- Destination changes per minute
- State changes per minute
- Time spent moving without progress
- Squad cohesion
- Navigation failures
- Mission completion rate
- Time spent idle without a valid reason

Record reasons for important decisions.

### 8. Acceptance Criteria

Test both regular and elite squads in the existing Conquest environment.

Verify that:

- AI squads maintain coherent objectives.
- Soldiers do not repeatedly switch destinations without meaningful causes.
- Squad leaders influence actual member movement.
- AI does not move directly toward unreachable goals.
- Squad members can wait, regroup, and resume missions.
- Elite squads behave more consistently without cheating.
- AI-versus-AI matches continue to completion.
- Existing vehicles, Conquest, Breakthrough, Commander intelligence restrictions, and LAN gameplay remain functional.

Use deterministic gameplay tests and actual browser observation.

Do not claim success simply because new states or classes were added.

## Development Order

1. Audit existing AI movement and decision transitions.
2. Fix navigation and destination instability.
3. Introduce persistent intentions.
4. Integrate squad-level movement coordination.
5. Add elite squad behavior profiles.
6. Improve animation and visual feedback.
7. Run full regression and performance tests.

Prioritize fixing root causes before adding more complexity.

The desired experience is an autonomous squad that moves with purpose, coordinates naturally, completes assignments, and feels like a coherent team of intelligent game characters.我觉得现在的AI的外观太不好了能不能做的真实一些，不要卡通化了，另外现在地对空导弹太超模了，应该设置打2发才会炸，而且有距离限制，AI我想的是类似于战队，特种兵，现在真的是到处跑，而且AI载具应该掩护步兵啊，
