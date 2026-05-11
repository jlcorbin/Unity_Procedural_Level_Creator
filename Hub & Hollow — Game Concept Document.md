# Hub & Hollow — Game Concept Document
*Unity 6.4 URP · Android / iOS · PC*
*Updated: 2026-05-11*
*Status: In production*

---

## Elevator Pitch

> **A stylized low-poly ARPG dungeon crawler. Delve a hand-crafted dungeon, fight through snappy combo combat, and bring loot home to a merchant who pays well — but die before you extract and you lose everything you didn't carry out.**

Test: A new player understands the loop — *enter dungeon → fight → find loot → extract → sell → upgrade → go deeper* — in 10 seconds. ✓

---

## Core Loop

```
Enter dungeon → Fight enemies → Find loot → Extract → Sell to merchant → Upgrade gear → Go deeper
                                                  ↘ or die → lose it all
```

---

## Design Pillars

| # | Pillar | Description |
|---|--------|-------------|
| 1 | **Town is sanctuary, dungeon is stakes** | Clean emotional contrast. Town = safe, warm, social. Dungeon = tense, kinetic, unforgiving. |
| 2 | **Snappy hands, strategic mind** | Fast readable combat — combo + dodge + stamina. Meaningful decisions at extract time. |
| 3 | **Discovery is the reward** | Every secret is hand-placed. No procedural shortcuts. Finding things is the headline. |
| 4 | **Built to grow** | Self-contained first chapter. Lieutenant boss at the end opens the door to more. |

---

## Project Identity

| Aspect | Detail |
|--------|--------|
| **Engine** | Unity 6.4 URP, IL2CPP |
| **Platforms** | PC primary · Android + iOS (touch input + perf budgets designed in parallel) |
| **Art foundation** | Fantastic Dungeon Pack URP + Whitebox mirror pack |
| **Character art** | RPG Tiny Hero World Bundle |
| **Genre** | Single-player third-person ARPG dungeon crawler with extraction tension |
| **Comparable titles** | *Moonlighter* (most direct), *Death's Door*, *Tunic* |
| **Session target** | 30 min casual · 60–90 min typical · 120 min deep dive |
| **Monetisation** | Premium — one-time purchase. No F2P / IAP / battle pass. |
| **Scope** | 6–9 months MVP (solo) · expandable into larger installments |

---

## Core Fantasy

**You are the treasure-hunter with a home to come back to.**

Two emotional states alternate in clean contrast:
- **In the dungeon** — tense, kinetic, in flow. Every cleared room asks: push deeper or play safe?
- **In town** — calm, social, warm. The merchant remembers you. Your gold is safe here.

The promise is *the joy of returning home with something rare in your pack* — and the slow, visible growth of your character and your town as that pack fills.

---

## Unique Hook

**Like *Moonlighter*, AND ALSO every room is hand-placed, every secret is hand-hidden, and the entire game world fits inside one town + one dungeon — a deliberately small but deeply crafted single-location ARPG, built to be a starter chapter.**

1. **Tight extraction tension** — Moonlighter's "extract or push?" loop
2. **Hand-crafted everything** — no procedural shortcuts, every secret intentional
3. **Starter-as-foundation framing** — lieutenant boss at the end is revealed to be a lieutenant, opening into sequels without leaving this game feeling unfinished

---

## Systems Inventory — Current Build State

### Done (M1–M11)

| System | Notes |
|--------|-------|
| **Player movement** | Camera-relative WASD, sprint (stamina-gated), jump. CharacterController. New Input System. |
| **Combo attack (3-hit)** | Input-buffered chain, animation-event hitbox windows, per-swing dedup set. |
| **Stamina system** | Sprint drain + regen. CharacterStats SO per-character rates. HUD bar live. |
| **Damage pipeline** | Central `ApplyDamage` — IsDead guard, dedup, OnHit event. Bidirectional: player hits enemy, enemy hits player. |
| **Hit reactions** | Enemy and player sides both complete. Random reaction clips, gated by death state. |
| **Enemy death** | Terminal Death state, capsule disabled, 5s despawn delay. |
| **Player death** | PlayerDeathOverlay with triple-redundant restart input. |
| **Floating damage numbers** | World-space TMP_Text, singleton spawner, subscribes to `AnyTargetableHit`. |
| **Enemy AI (Dummy)** | NavMeshAgent FSM — Idle / Chase / Attack / Cooldown. Baked NavMesh via AI Navigation 2.x. |
| **Interact system** | Abstract `Interactable` base. `AssassinateInteractable` + `OpenInteractable` shipped. `PlayerInteractor` on player. |
| **Player HUD** | HP + Stamina bars. Snap-on-damage, lerp-on-heal. Passive observer pattern. |

### In Progress / Queued (Combat Phase)

| System | Milestone | Notes |
|--------|-----------|-------|
| **Dodge** | M12 — next | 4-way directional roll, V key, scripted impulse, 0.5s i-frames, 25 stamina cost, 0.8s cooldown. Cancels attack. |
| **Target lock** | M13 | Sphere cast to nearest enemy, camera follow, world-space lock indicator, auto-clear on death. |
| **Enemy health bar + Defense** | M14 | World-space billboard widget. Defense stat subtracted in `ApplyDamage`. |
| **WeaponStats SO** | M15 | Replace hardcoded `attackDamage = 10`. Enables weapon variety from World Bundle's 8 weapon sets. |
| **Enemy archetypes ×3** | M16+ | Promote Dummy FSM pattern to 3 distinct characters from World Bundle MC* prefabs. |
| **Lieutenant boss** | Later | Terminal dungeon encounter. Connective tissue to sequel framing. |

### Later (Game Loop Phase)

| System | Notes |
|--------|-------|
| **Loot system** | Item drops, inventory, 3 rarity tiers, flat gold values. After combat phase complete. |
| **Town hub** | Separate scene. Merchant, innkeeper, 2–3 NPCs. |
| **Extraction mechanic** | Checkpoints + 1 emergency scroll per delve. Die = lose unbanked loot. |
| **Save / load** | Innkeeper or autosave-on-extract. Design TBD. |

### Separate Pipeline (Post-Combat)

| System | Notes |
|--------|-------|
| **Dungeon layout** | Hand-crafted, 3 zones, ~25–35 rooms. LevelGenerator + RoomWorkshop tooling already built. Revisit after combat + enemies complete. |

---

## Current Milestone Sequence

| Milestone | Description | State |
|-----------|-------------|-------|
| M1–M11 | Combat foundation — movement, combo, stamina, damage pipeline, hit reactions, death, enemy AI, player takes damage | ✅ Done |
| **M12** | **Dodge — directional roll, i-frames, stamina cost, V key** | ⏩ Next |
| M13 | Target lock — sphere cast, camera follow, lock indicator | Queued |
| M14 | Enemy health bar + Defense stat wired into damage calc | Queued |
| M15 | WeaponStats SO + weapon variety | Queued |
| M16+ | Enemy archetypes ×3 + lieutenant boss | Queued |
| Phase 2 | Loot system + town hub + extraction + save/load | Later |
| Phase 3 | Dungeon layout (hand-crafted, 3 zones) | Separate pipeline |

---

## MVP Definition

**Core hypothesis**: *Players will engage with a hand-crafted ARPG dungeon + town hub loop for 30+ minute sessions, returning across multiple sessions to push deeper, because the extract-or-push decision creates compelling tension and the town hub provides satisfying release.*

### Required for MVP

1. Combat re-tuned for snappy/forgiving feel (existing combo + dodge + stamina, properly tuned)
2. 3 enemy archetypes + 1 lieutenant boss with AI and animations
3. 1 dungeon, 3 zones, ~25–35 hand-crafted rooms, 3 checkpoints
4. Loot system — drops, inventory, 3 rarity tiers, flat gold values
5. Extraction mechanic — checkpoints + 1 emergency scroll per delve (purchased from merchant)
6. Town hub — merchant (buy/sell), innkeeper (save/heal), 2–3 NPCs
7. Progression — gold → buy gear (3 tiers) + consumables
8. Light story spine — ~5 lore notes, 3 NPC dialogue states tied to dungeon milestones
9. Save/load — innkeeper or autosave on extract
10. Main menu / pause / options

### Explicitly NOT in MVP

- Item identification puzzle (flat gold values only in MVP)
- Named unique items
- Camera shake / knockback (open design questions)
- Additional combat abilities (heavy attack, spells, ranged)
- Town building / cosmetic NPC adds beyond 2–3
- Audio polish beyond minimum SFX
- VFX polish
- NG+ / difficulty modifiers
- Touch input baseline (Target tier — PC is primary commercial outcome)

### Scope Tiers

| Tier | Content | Timeline |
|------|---------|----------|
| **MVP** | 1 town, 3-NPC hub; 1 dungeon, 3 zones, ~25–35 rooms; 3 enemies + 1 boss; 15 items, 3 rarity tiers; core combat; extraction; basic save/load; basic menus | 4–6 months |
| **Target** | MVP + identification puzzle; 5 enemies + 2 mid-bosses; 25 items + 5 named uniques; 2 added abilities; basic audio + VFX pass; touch input baseline | 6–9 months |
| **Full Vision** | Target + cosmetic town growth + secret NPC + difficulty modifier | 9–12 months |
| **Aspirational** | Full vision + named uniques with special effects + Endless Mode procedural variant | 12+ months |

---

## Active Risks

### High

**R1 — Touch input for snappy ARPG is unproven.**
Combo + dodge + stamina + camera on a phone screen requires careful UX work. No existing reference in current code.
*Mitigation*: PC is primary commercial target. Mobile can be deferred to post-launch port if timeline slips. Prototype touch controls early as a research spike before committing.

### Medium

**R2 — Combat re-tuning required.**
Current foundations have Souls-adjacent bones (i-frames, stamina, combo windows). Target feel is snappy and forgiving — a tuning pass is planned post-M14, not a rewrite, but requires sustained playtest attention.
*Mitigation*: Define explicit combat-feel targets (e.g. "player wins a 1v1 with 0–1 chip damage on competent play") before tuning begins.

**R3 — Loot + economy is entirely unbuilt.**
Item drops, inventory, rarity tiers, merchant valuation, gold currency, and all associated UI are not yet started.
*Mitigation*: Build minimum viable economy first (flat gold values, no identification puzzle). Promote identification puzzle to Target tier.

### Low

**R4 — Door geometry placement not yet wired.**
ExitPoint connections exist in the level generator but door prefab placement at connections is deferred.
*Mitigation*: Pairs naturally with `OpenInteractable` (already shipped) when dungeon layout phase begins.

**R5 — Solo 6–9 month scope is ambitious.**
MVP needs combat re-tuning, 3 enemy archetypes, loot system, town hub, save/load, full UI, basic audio. That's substantial for one person.
*Mitigation*: MVP discipline must be ruthless. Aspirational features cut without ceremony if timeline slips.

---

## Open Design Questions

| Question | Status | Resolution path |
|----------|--------|-----------------|
| **Knockback** — distance, direction, does it interrupt enemy current action? | Open | Resolve during combat tuning pass post-M14 |
| **Camera shake on hit** — magnitude and falloff? | Open | Resolve during combat tuning pass |
| **Stamina regen model** — auto-regen always, delay post-use, or items only? | Open | Current impl is auto-regen; revisit during tuning |
| **Extraction details** — scrolls per delve? Partial dungeon state persist? | Open | Prototype before committing |
| **Save model** — autosave on extract vs. save point at innkeeper only? | Open | Resolve during town hub phase |
| **Touch input UX** — virtual stick + buttons, context-sensitive, or tap-to-move? | Open | Research spike before mobile commitment final |

---

## Architecture Notes (Unity-Specific)

- **Namespace**: All scripts use `namespace LevelGen`
- **Damage convention**: `ApplyDamage` takes a positive float — damage is subtracted from HP inside the method. Do not pass negative values.
- **Single-writer-per-Animator-parameter**: Each Animator parameter has exactly one owning script. Do not cross-write.
- **Central damage processing**: All damage flows through `CharacterStatsRuntime.ApplyDamage` — i-frame check (`IsInvulnerable`), IsDead guard, and Defense reduction all live there. Never bypass it with direct field writes.
- **Static events**: `Targetable.AnyTargetableHit` must be paired with `OnEnable +=` / `OnDisable -=` in every subscriber. Static event lifetime survives domain reloads.
- **Level generation**: RoomWorkshop + V2 LevelGenerator are the authoring tools for hand-crafted dungeon content. Scenes are baked, not runtime-generated.
- **CLAUDE.md**: Canonical architecture document. Read before every CC session. `Documentation/Session_Handoff.md` is the current-state layer on top.

---

*End of document.*