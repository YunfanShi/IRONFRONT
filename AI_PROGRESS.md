# AI overhaul 0.15.0 milestones

1. Audit completed: AI_ARCHITECTURE.md, clean main and five identical local 0.14.0 copies confirmed.
2. Intelligence completed and tested before commander integration: copied immutable reports, decay, bounded storage, opaque-obstacle/no-report and frozen-contact tests. Original 93 tests passed at this milestone.
3. Commander core completed: one per team, restricted DTO input, utility assignments, 12-second commitment, observed pressure and route failure feedback. Event target selection also uses reports instead of hidden hostile strength.
4. Squad core completed: cohesion, bounded regroup wait, health withdrawal, role support, reserved cover and bounding. Full lane reservations and formal order rejection protocols remain future work.
5. Recommendations completed for accept/dismiss, reasons, cooldown and host authority. Request another/explicit personal objective UI remains future work.
6. Vehicle core completed: clearance-specific graph, valid connectors, continuous steering, collision avoidance, failure reports, squad transport seats/disembark. Full road lanes and dedicated escort/transport dispatcher remain future work.
7. QA completed: results/logs under docs/qa and QA_RESULTS.json. Long-run assertions check bounded reports/projectiles, seat uniqueness, finite transforms, combat and navigation failures.

RESUPPLY/ESCORT/RESERVE are not claimed as completed commander missions. Existing individual supply/repair/revive behaviors are real and preserved. Flight is an arcade model, not six-degree rigid-body aerodynamics.
