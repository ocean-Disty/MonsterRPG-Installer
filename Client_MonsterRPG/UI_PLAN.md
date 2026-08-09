# MonsterRPG UI plan: loading, character, attributes, appearance

Status as of 2026-07-31.

---

## DONE

### Attribute spending after creation (was: dead buttons)
`CS_attrAdj` used to `return` outright when `$CS_Locked`, so once a character existed the
`+`/`-` buttons did nothing and a level-up had nowhere to spend points.

Now the same buttons serve two systems:

| | before creation | after creation |
|---|---|---|
| source | local pool (`$CS_Attr[]`, `$CS_AttrSpent`) | `%profile.SkillPoints` (server) |
| spend | batched, committed by `MRPG_CharSet` | `commandToServer('MRPG_AllocStat')`, one at a time |
| `-` button | shown | hidden, server spends are permanent |

- No client-side prediction: `+` sends and redraws from the server push. Only the server
  knows the true `SkillPoints`.
- `clientCmdMRPG_StatDone` (RPGPanels.cs) now refreshes the character screen **before** its
  early-out. That early-out fires exactly when the Stats tab is hidden, i.e. while the
  character screen is open, so without this the point was spent and the number never moved.
- `CS_effStat` returns the server value when locked and does **not** re-add race mods
  (`MRPG_statOf` already folds them in; re-adding double-counts and would make the physique
  readout disagree with the numbers directly above it).

Reachable now: **N** toggles the screen and is not gated on `$CS_Locked`.

### Profile persistence (three sites)
No autosave exists. Any `serverCmd` mutating `%client.profile` must save it:
`serverCmdMRPG_AllocNode`, `serverCmdMRPG_TreeRespec`, `ServerCmdMRPG_AllocStat`.

### Character screen backdrop
`MRPG_CharBg` was `"0 0 0 185"`, a dim, not a cover, so the half-built world, the observer
camera and the first-person held item all rendered through it. Now `"8 10 14 255"`,
**exactly matching** `MRPG_LoadingProfile`'s fillColor so the loading→character handoff has
no flash of world and no colour pop. *Change both or neither.*

### Predictive loading bar
Groove + fill + gloss + percentage, swatches only (no bitmaps to fail, nothing 3D, this
screen must never contain a `GuiObjectView`, see the file header).

Three prediction sources, in trust order:
1. **Phase**: `MRPG_loadingPhasePct` maps status substrings to the % that phase *ends* at.
   Substring matching so server wording can drift without silently falling back to time.
2. **Time**: paced against `$pref::MRPG::LastLoadMs`, re-measured every load and blended
   `0.6*prev + 0.4*new`. Timeouts are never recorded (that would teach it to expect failure).
3. **Creep**: asymptotic easing toward, but never reaching, the current phase ceiling.

Invariants: never goes backwards; never overtakes a confirmed phase; always animates out to
100 before the screen lifts (`MRPG_loadingFinish`).

`MRPG_buildLoadingScreen` now rebuilds if the existing dialog predates the bar, a script
re-exec would otherwise keep the old dialog and the bar would silently never appear.

---

### Equip vs character-selection appearance: FIXED
Not drift between two similar renderers. The equipment doll was styled from the **stock
Blockland avatar prefs** (`$pref::Avatar::HeadColor`, `$Pref::Avatar::FaceName`, the parts
list) and knew nothing about MonsterRPG at all, so it drew your Blockland blockhead while
character selection drew your MonsterRPG character. Two different sources of truth.

`MRPG_applyCharLookToView(%view, %obj)` (CharacterScreen.cs) is now the single place a
character is styled onto a 3D view; both screens call it. It returns 0 when no character
look is loaded (fresh join before `MRPG_CharGet` answers, or no character created), and the
equip path falls through to the stock behaviour rather than painting a half-built default.

- Hair stays the caller's job, `CS_applyHair` tracks one `$CS_HairOn` flag against
  `CS_View`, so styling one view must not unmount the other's hair.
- `$CS_LookKnown` is set only on the *created* branch of `clientCmdMRPG_CharData`; before
  that the selectors hold defaults, not a character.
- `MRPG_refreshEquip` requests `MRPG_CharGet` when the look is unknown, so a session where
  the character screen was never opened still shows the right person. Verified safe:
  `CS_showRaceMods` and `CS_sldRefresh` both guard on `isObject`, so the reply handler is
  harmless when the character screen has never been built.

---

### Attribute screen: DONE (`AttributeScreen.cs`, press **J**)
Its own GUI, not a morph of the character screen. The creation screen is ~2,200 lines built
around pickers, sliders and a race-mod model an attribute screen has no use for; hiding
them leaves that state live and forces every future edit to reason about two modes, and
creation is the one flow that must never break.

Shares the server side completely: same `MRPG_AllocStat`, same `$MRPG_StatBuf`.

- **Stock `GuiBitmapButtonCtrl`, not the character screen's plate+catcher system.** That
  system keys every button into one global registry (`$CS_BtnPlate`/`$CS_BtnN`) hit-tested
  by one shared catcher, so a second screen using it would have its buttons hit-tested by
  the character screen's catcher and vice versa.
- No local prediction, no `-` button (a spent point is permanent), no creation controls.
- `+` hides itself when no points remain rather than inviting a dead click.
- Backdrop matches the loading and character screens exactly, all three are the same
  "you are looking at your character, not the world" surface.

### Level-up notice: DONE
`MRPG_attrNotePoints` watches `$MRPG_StatPoints` for an increase on every stat push; no new
server message needed. **Deliberately does not open the screen**: levelling happens on a
kill, and stealing the screen mid-fight is worse than not saying anything. Bottom-print
(not centre-print, which is where the player is aiming) offering **J**.

---

## TODO

### 1. Hair/hat scale on scaled bodies: NOT FIXABLE AS ASKED; needs a decision

Investigated and settled against the TGE 1.5.1 source. Mounted images **never** inherit the
parent's object scale:

- body: `ShapeBase::renderImage` (shapeBase.cc:2482): `dglMultMatrix(renderTransform)`
  **then `glScalef(mObjScale…)`**
- hat/hair: `renderMountedImage` (:2440) and `Player::renderImage` (player.cc:3877):
  `dglMultMatrix(mat)` and **no `glScalef` at all**

Placement is unscaled too, `getRenderImageTransform` (shapeImage.cc:1096) builds from
`renderTransform × nodeTransform`, neither of which carries scale. So on a 0.6 bot the hat
is **both too big and floating off the head**: the head node draws at 0.6×nodePos, the hat
is placed at 1.0×nodePos. (This is why the character screen already has a note about hair
not lining up.)

Same root cause as the bone-capsule bug, `mObjScale` sits outside `mObjToWorld`.

**Why the obvious fixes do not work.** There is no scale field on a mounted image, and
`mountImage` takes no scale argument. More importantly it is **client-side rendering**: a
native hook in BHOctree.dll would only correct the view of players who have that DLL, so
every other client still sees full-size hats. A server cannot fix this.

**Options, all with a real cost, needs a call:**
1. **Scaled `.dts` variants per hat, per size band.** Datablocks network, so it works for
   everyone. Cost: hats × bands against the ~8000 budget that already carries a 2048-entry
   voxel palette. Realistically only affordable for the few hats bots actually wear, with
   coarse bands (say 0.7 / 1.0 / 1.3).
2. **Constrain bot scale range** so the mismatch is not noticeable. Free, and reduces the
   thing that made small enemies feel wrong in the first place.
3. **Drop mounted hair for scaled bodies**: bald or node-based only below some scale.

Recommendation: (2) now, (1) later and only for the hats bots actually use.


---

## BLOCKING, UNRELATED TO UI

**BHOctree.dll is not deployed.** Both collision fixes (small-enemy bone capsules, and the
close-quarters sword detector) are inert until Blockland is closed and `compile.bat` re-run.
Build succeeds; the copy step fails with the DLL in use.
