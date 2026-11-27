# XO Card Battle — prototype V0.1

## Run
1. Fetch origin in GitHub Desktop; switch to `feature/xo-card-battle`; pull if needed.
2. Open the existing Unity project (6000.3.27f1, according to ProjectVersion.txt).
3. In Unity: Tools > XO Card Battle > Open or Create Scene.
4. Press Play. Use Tools > XO Card Battle > Run Rules Smoke Tests to check core rules.
The scene is created locally on demand, without overwriting SampleScene or requiring prefabs.

## Controls
Local hot-seat for two people. Reveal your hand when the device is passed to you.
Click an empty cell to tick X/O. Click a card to use it; board cards ask for target(s).
For region cards, choose the upper-left cell of the region. SWAP/MUTUAL choose own mark first, enemy second.
CLEANSE: click Cleanse myself or a frozen cell. U-TURN: place a mark first.
NOPE/REFLECT: react before the effect resolves or click Pass. 50/50: choose a card.
Click End turn after placing a mark; pass if no valid cells remain.

## Fixed rules
9x9; five or more active consecutive marks, horizontal/vertical/diagonal; no blocked-end restriction.
Check both sides after every resolved board change and timer step; simultaneous winners draw.
One mark per turn. Freeze excludes a mark from win checks, but does not prohibit other card effects.
SWAP exchanges marks only; all region transformations move whole cells and effects.
THANOS destroys a cell, REVIVE restores an empty cell.
LOCKDOWN suspends effects, restores destroyed cells temporarily, blocks all cards. Timers continue.
CLEANSE ends the turn even if canceled. NOPE can counter another NOPE and opponent CLEANSE.
REFLECT redirects personal attacks only, not board effects or symmetric TRADE.
All 28 agreed card types are represented in Match.cs.

## Provisional rules for the first playable prototype (not final balance)
- Shared draw pile, 3 copies per card, 5 initial cards per player, then one draw at the beginning of each turn including the first.
- One active card plus at most one mark per turn. NOPE/REFLECT are reactions and do not consume the active-card allowance.
- Timed board effects, immunity and lockdown count subsequent global turns; SILENCE counts affected player's personal turns. Newly cast effects also apply during casting turn.
- PEEK selects a random opponent card. FORESIGHT sees remaining draw pile (up to 3). SHUFFLE targets shared deck.
- Shield/immortal refresh duration rather than stack; freeze/silence stack.
- IMMORTAL targets own mark; region effects cannot select a region containing an immortal mark. CLEANSE can remove freeze without removing beneficial effects.
- All played cards, including canceled effects, go to discard; empty draw pile recycles discard. RECYCLE excludes itself.
- Trade is permitted with an empty hand after playing it; an opponent with a shield cannot be targeted.
- Win is checked immediately, so a finished match cannot be undone with U-TURN.
- Full board is not an immediate draw; a provisional 250-turn safety cap prevents endless matches. Review after playtesting.
- Interface is functional debug UI in English; no final art, animation, AI, online mode or build.

## Status
Core and local UI added; editor smoke tests provided. Unity Editor is not available in the authoring environment; compile, smoke tests and actual gameplay must be verified locally. No claim of completed playtesting.
