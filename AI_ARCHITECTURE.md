# IRONFRONT AI architecture audit

Baseline 0.14.0: Battle.ts owns perception, squad assignment, combat and vehicles. Four roles and four-member squads, geometric LOS, smoke, last-seen memory, support/rescue, deterministic simulation and ticket/major-event rules are reusable.

Problems verified in source: assignSquads reads true hostile localStrength; local combat strength can count unseen enemies; lost-target branch tests live coordinates; commander runs every 2.2 seconds; infantry Navigation uses one clearance for all vehicles and returns unreachable direct goals; jets/helicopters share flyVehicle; lock and missile warnings share one timer.

0.15.0 boundaries: Intelligence receives immutable copied observations only. Commander receives public map geometry, decaying reports, friendly squad summaries and tickets; no Battle or hostile lookup. Squad decisions use observed contacts, friendly member status and assigned mission, then existing individual cover/fire/support/navigation executes. Vehicle navigation uses hull clearance and validates every edge. Flight and missile guidance are separate pure modules.

Authoritative simulation stays in Battle, including LAN host. Rendering only reads results. Normal HUD receives own-faction recommendation; diagnostic contact details stay in development F3. Reports are bounded and throttled; commander 4 seconds, squad 1 second, existing staggered individual decisions retained.
