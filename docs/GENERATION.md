# MIDILab Generation Engine

This document describes how MIDILab currently generates drum patterns. It is intended for contributors who want to understand or extend the musical rules behind the generator.

The generator is rule-based rather than AI or machine learning. It constructs patterns from musical rules plus controlled randomness, then applies velocity and timing humanization.

> This document describes the current implementation. Generator behavior may evolve as MIDILab adds more genres, meters, fill behavior, and performance realism.


The core groove-generation system described below was established before v1.2 and remains the basis of the current generator. MIDILab is not choosing from a library of finished MIDI loops. It constructs a new pattern from musical rules plus controlled randomness. The result is therefore partly deterministic and partly probabilistic: some hits are treated as structural anchors, while other hits, fills, accents, and decorative percussion are chosen according to probabilities influenced by the selected settings.

The generator is intentionally rule-based rather than an AI or machine-learning model. The goal is to produce a musically plausible starting point that can be regenerated and then edited manually on the grid.

## 1. Inputs used by the generator

When Generate or New Variation is pressed, the generator receives:

- BPM
- Number of bars
- Energy, converted from the 0-100 slider to a 0.0-1.0 value
- Humanize, also converted to 0.0-1.0
- Genre
- Time signature and, when applicable, grouping
- Section type
- The currently enabled drum/percussion pieces

Export Target is deliberately **not** passed into the musical generation algorithm. The generator creates instrument roles first. Export Target is applied later, when those roles are translated to MIDI note numbers. This prevents a destination plugin from changing the musical composition simply because it uses a different drum map.

BPM currently controls playback and exported MIDI tempo. It does not directly make the composition busier or simpler. For example, a groove generated at 80 BPM and the same groove settings at 160 BPM use the same placement rules; the 160 BPM version simply plays twice as fast.

## 2. Section type first modifies the effective energy

Before individual bars are generated, MIDILab adjusts the requested Energy value according to the selected song section.

The current adjustments are:

| Section | Energy adjustment |
| --- | ---: |
| General | no change |
| Intro | -0.18 |
| Verse | -0.08 |
| Pre-Chorus | +0.05 |
| Chorus | +0.16 |
| Bridge | -0.02 |
| Breakdown | -0.24 |
| Outro | +0.02 |

The adjusted value is clamped between 0.0 and 1.0.

This means a Chorus at Energy 50 is internally treated roughly like Energy 66 for the initial groove-generation stage, while a Breakdown at Energy 50 is treated roughly like Energy 26. Section type therefore changes the density and intensity of the basic groove before any section-specific finishing rules are applied.

The original requested Energy value is still retained because some later section-shaping behavior deliberately refers to what the user actually selected.

## 3. MIDILab chooses one of two main generation paths

The time signature determines which broad algorithm is used.

### 4/4

4/4 uses a dedicated genre-specific generator. Rock, Indie, Folk, and Metal each have their own kick, snare, cymbal, ghost-note, and fill rules.

### Every currently supported non-4/4 meter

3/4, 6/8, 5/4, and 7/8 use a shared meter-aware generator. That generator combines:

- the chosen time signature
- its internal grouping
- genre-based velocity tendencies
- energy-dependent density
- kick/snare emphasis at group boundaries

This distinction is important. At the moment, the 4/4 genre algorithms are more specialized than the odd-meter algorithms. A 7/8 Metal pattern and a 7/8 Folk pattern do differ, but both are created by the same meter-aware framework rather than completely separate 7/8 genre engines.

## 4. The 4/4 Rock algorithm

Rock begins with a conventional backbeat-oriented structure.

### Hi-hat

The generator always considers eighth-note closed hi-hats. Additional sixteenth-note hi-hats become increasingly likely once effective Energy rises above approximately 0.48.

The probability for an in-between sixteenth is:

`clamp((energy - 0.48) * 1.6, 0, 0.85)`

Quarter-note positions are accented more strongly than the intervening eighths. The normal base closed-hat velocity is 72, quarter-note accents add 16, and Energy can add another 0-10 velocity points before humanization.

### Snare

Rock places strong snares on beats 2 and 4. In the 16-step 4/4 grid these are steps 4 and 12.

Base velocity is approximately 110/112.

There is also a 30% chance per bar of adding a quiet ghost snare. The ghost is randomly placed at step 10 or 15 and begins around velocity 44.

### Kick

Rock always places kicks on:

- beat 1, step 0
- beat 3, step 8

It then has additional possible kicks:

- step 6: 62% probability
- step 14: probability `0.30 + energy * 0.55`
- step 10: only considered above Energy 0.72, with probability `energy * 0.75`

This is an example of how MIDILab uses randomness. The main pulse is stable, but syncopation changes from one generated variation to another.

### Crash and fill

The first Rock bar begins with a crash.

If the final bar has effective Energy of at least 0.55, MIDILab replaces the end of the bar with a four-hit fill:

Snare -> High Tom -> Mid Tom -> Floor Tom

The cymbal pattern is cleared from the fill area so the fill does not simply stack over the existing hat/ride pattern.

## 5. The 4/4 Indie algorithm

Indie keeps the normal rock backbeat but deliberately increases syncopation and variation.

### Hi-hat

Like Rock, the base is an eighth-note closed-hat pulse. Sixteenths start becoming likely at a higher threshold, approximately 0.62, so lower-energy Indie grooves can remain comparatively open.

The base hi-hat velocity is slightly lighter than Rock.

### Snare

Strong backbeats still occur on 2 and 4, but Indie uses a 48% ghost-note chance, considerably higher than Rock.

### Kick

The required kick is beat 1. Other positions are probabilistic:

- step 3: `0.22 + energy * 0.35`
- step 6: 55%
- step 8: 72%
- step 11: `0.22 + energy * 0.35`
- step 14: `0.50 + energy * 0.30`

These off-beat locations are a major reason an Indie variation can feel less square than the default Rock generator.

At effective Energy above 0.52, there is also a 55% chance of changing the hat at step 14 into an open hi-hat.

### Crash and fill

The first bar has a 75% chance of starting with a crash instead of making the crash mandatory.

On the final bar above Energy 0.45, MIDILab creates one of two short fills at random:

- two snare hits
- high tom followed by floor tom

## 6. The 4/4 Folk algorithm

Folk intentionally removes much of the density used by the other genres.

### Hi-hat

Closed hi-hats are placed on eighth notes only. Quarter-note positions are stronger than the off-beat eighths.

The velocities are substantially lighter than Rock or Metal.

### Snare

Snares remain on beats 2 and 4, but at lower velocity, roughly around 90 plus a small Energy contribution.

### Kick

Beat 1 is mandatory.

Beat 3 has a 72% probability.

A later syncopated kick at step 10 is only likely as Energy rises above approximately 0.48.

### Crash and fill

Even the opening crash is restrained. It is only considered above Energy 0.72 and then has a 35% probability.

A very simple two-snare ending is added only when the last bar is above Energy 0.70.

The intent is for Folk to produce a usable rhythmic foundation without automatically making the drummer occupy too much space.

## 7. The 4/4 Metal algorithm

Metal deliberately increases cymbal and kick density while reducing the amount of loose timing variation applied to the core performance.

### Cymbal choice

At effective Energy above 0.60, there is a 45% chance that the bar uses Ride instead of Closed Hi-Hat.

Eighth-note cymbal hits form the base pattern. In-between sixteenths become more likely as Energy rises.

The probability is based on:

`clamp((energy - 0.40) * 1.7, 0, 0.95)`

Quarter-note positions receive stronger accents.

### Snare

The normal 2-and-4 backbeat is retained at approximately velocity 118. Ghost notes are uncommon, with only an 8% chance.

### Kick

Metal has the densest kick rule set. Beats 1 and 3 are mandatory, and additional hits are probabilistic:

- step 2: `0.22 + energy * 0.50`
- step 3: becomes increasingly likely above Energy 0.58
- step 6: `0.45 + energy * 0.40`
- step 10: `0.30 + energy * 0.55`
- step 14: `0.55 + energy * 0.35`
- step 15: becomes increasingly likely above Energy 0.62

This does not attempt to model full double-bass drumming yet, but high Energy can produce considerably denser kick movement.

### Crash and fill

The first bar always receives a crash. At Energy above 0.78, every fourth bar can also receive a crash.

The final bar gets a descending Snare -> High Tom -> Mid Tom -> Floor Tom fill at Energy above 0.42.

## 8. How non-4/4 meters are generated

For 3/4, 6/8, 5/4, and 7/8, MIDILab starts from the denominator pulse and the meter's grouping rather than trying to truncate a 4/4 groove.

Examples:

- 3/4 uses grouping `3`
- 6/8 uses `3+3`
- 5/4 can use `3+2` or `2+3`
- 7/8 can use `2+2+3`, `2+3+2`, or `3+2+2`

MIDILab converts those groups into grid positions. Group beginnings are treated as stronger structural points.

### Cymbal pulse

A cymbal hit is placed on each denominator beat.

At a group boundary the velocity is stronger. The exact velocity also depends on Genre. Folk is lightest, Metal is strongest, with Indie and Rock in between.

At Energy above 0.68, MIDILab can add extra subdivision hits between the normal pulse positions.

Metal may use Ride at Energy above 0.62; otherwise the meter-aware generator normally uses Closed Hi-Hat.

### Kick and snare

The opening step always gets a kick.

Additional group boundaries are then emphasized with kick or snare according to their position in the grouping. This is what makes 7/8 grouped `2+2+3` differ structurally from `3+2+2`.

MIDILab also adds meter-specific anchors:

- 3/4 receives a snare on its second quarter-note beat and may receive a kick on beat 3.
- 6/8 receives a snare at the beginning of the second group, creating the common two-large-pulse feeling. Additional kicks can appear within the two groups.
- Odd meters alternate kick/snare emphasis across the selected internal group boundaries.

The first bar receives a crash unless the genre is Folk.

If the final bar has Energy above 0.58, the final four grid steps become a short Snare -> High Tom -> Mid Tom -> Floor Tom fill.

## 9. Section shaping happens after the basic groove

Once all bars have been generated, MIDILab performs a second pass based on Section Type.

This is in addition to the Energy adjustment described earlier.

### General

No additional section shaping is applied. This preserves the ordinary genre-generator behavior.

### Intro

The automatic opening crash is removed.

For multi-bar intros there is a 55% chance that quieter snare notes in the first bar are removed, leaving more space at the beginning.

### Verse

Crashes after the first bar are removed.

Some quiet snare ghost notes are probabilistically removed, making the groove more controlled.

### Pre-Chorus

The final portion of the last bar receives a small snare lift. The generator alternates snare strikes through the final few subdivisions with increasing velocity.

The intention is to imply forward motion into the next section without turning the entire bar into a large tom fill.

### Chorus

A crash is placed at the start of every other bar.

If the user's requested Energy is above 0.45, a late closed hi-hat in each bar can be replaced by an open hi-hat. This makes the cymbal texture feel wider without changing the fundamental groove.

### Bridge

Each bar has a 65% chance of adding a Ride accent at its beginning.

When the bar is long enough, there is also a 50% chance of adding a Floor Tom around the middle.

The purpose is not simply to make the Bridge louder or quieter, but to change its orchestration relative to Verse and Chorus.

### Breakdown

Many intervening closed-hat and ride hits are removed so the cymbal pulse becomes much sparser.

Crashes are removed except at the very beginning.

### Outro

The final grid positions receive extra Floor Tom and Snare punctuation so the section ends more deliberately.

## 10. Optional/custom kit pieces are added in a separate color pass

After the normal groove and section shaping are complete, MIDILab checks which optional kit pieces have been enabled.

Optional instruments are not all treated equally. They have contextual rules designed to keep them from appearing everywhere merely because the user enabled them.

Current examples include:

### Crash 2

Can be added at the beginning of a bar in Chorus sections or Metal grooves.

Probability:

`0.22 + energy * 0.30`

### China

Only considered for Metal, only above Energy 0.55.

Probability:

`0.16 + energy * 0.22`

### Splash

Can appear away from the opening bar, approximately halfway through a bar.

Probability:

`0.08 + energy * 0.12`

### Pedal Hi-Hat

Considered in Folk and Indie.

There is a 35% chance a bar will use the pedal-hat rule, after which individual pulse positions have their own Energy-influenced probabilities.

### Tambourine

Considered in Folk and Indie above Energy 0.35.

Individual pulse positions use probability:

`0.25 + energy * 0.28`

### Cowbell

Currently associated primarily with Bridge sections. A bar has a 40% chance of entering the cowbell rule, then each eligible pulse has a 42% chance of receiving a hit.

### Hand Clap

Can reinforce strong snare hits in Choruses and Indie grooves.

For each qualifying snare, probability is:

`0.28 + energy * 0.30`

The clap velocity is intentionally lower than the snare it reinforces.

### Rimshot

Currently used only for lower-energy Intro or Verse contexts. It can reinforce suitable quieter snare events with a 35% probability.

### Low Tom

On the ending bar at Energy above 0.55, an enabled Low Tom has a chance to appear near the end.

## 11. Disabled kit pieces are filtered out

After generation is complete, MIDILab removes every hit whose instrument is not currently enabled in Customize Kit.

This means disabling Ride, for example, guarantees that the resulting generated pattern contains no Ride hits even if an earlier genre rule initially attempted to create one.

An important current limitation follows from this design: disabling a core piece does not cause MIDILab to completely recompute the groove around its absence. If Snare is disabled, the snare notes are removed; MIDILab does not yet automatically decide that another instrument should take over the snare's musical role.

That is an area where a future generator could become more kit-aware.

## 12. Velocity humanization

Every generated hit eventually passes through the same `AddHit` routine.

MIDILab first determines a musical base velocity from the rule that created the note. For example:

- a backbeat snare receives a high base velocity
- a ghost snare receives a low base velocity
- an accented quarter-note hi-hat receives more velocity than an off-beat hat
- Metal generally starts from stronger velocities than Folk

Humanize then adds a small random signed amount.

The maximum velocity variation is:

`round(8 * humanize)`

So:

- Humanize 0 produces no random velocity variation.
- Humanize 50 produces roughly +/-4 MIDI velocity.
- Humanize 100 produces up to roughly +/-8 MIDI velocity.

The result is clamped to the valid MIDI range 1-127.

The important point is that Humanize does not replace musical dynamics with arbitrary random values. A ghost note remains much quieter than a backbeat because randomness is applied around each note's musically chosen base velocity.

## 13. Timing humanization

MIDILab also stores a timing offset on every generated hit.

The maximum random timing offset is:

`round(7 * humanize)` MIDI ticks

MIDILab uses 480 ticks per quarter note, so the current humanization range is intentionally small.

At Humanize 0, every generated hit is exactly on the grid.

At higher Humanize settings, each note can fall slightly early or late. The amount is randomized independently for each hit.

Some Metal rules deliberately pass a reduced Humanize amount to `AddHit`, which keeps high-intensity Metal parts somewhat tighter even when the global Humanize control is high.

## 14. Duplicate-hit protection

`AddHit` first checks whether the same instrument already has a note at the same grid position.

If it does, MIDILab does not add a duplicate.

This matters because several rule passes can target the same location. For example, a section rule and a genre rule may both request a crash on the first step. The pattern still receives only one crash event at that location.

Different instruments may of course occupy the same step. A Kick, Crash, and Closed Hi-Hat can all occur simultaneously.

## 15. What New Variation actually does

MIDILab uses a normal pseudorandom number generator.

Every time Generate or New Variation runs, all probability checks are evaluated again. Therefore the structural rules remain the same, but optional decisions can change.

For example, with identical settings:

- the main Rock snare backbeat remains on 2 and 4
- the main kick anchors remain stable
- one variation may contain a ghost snare while another does not
- one may receive a syncopated kick that another omits
- optional percussion may appear in different bars
- each hit receives newly randomized velocity and timing offsets

This is why New Variation should sound like another performance built from the same musical instructions rather than an unrelated random sequence.

MIDILab currently does not expose a random seed. Therefore a generated take cannot be reconstructed later solely from its parameter values. Reset Edits works differently: MIDILab keeps a clone of the most recently generated take in memory and restores that exact clone.

## 16. Generation order summarized

At a high level, the current algorithm is:

1. Read the user's BPM, bar count, Energy, Humanize, Genre, Section, meter, grouping, and active kit.
2. Convert Energy/Humanize from percentages into 0.0-1.0 values.
3. Adjust effective Energy based on Section.
4. Create an empty pattern with the requested bar count and meter.
5. For each bar:
   - if the meter is 4/4, call the selected genre's specialized generator;
   - otherwise, call the shared meter-aware generator.
6. Add mandatory structural hits.
7. Evaluate probabilistic kicks, cymbal subdivisions, ghost notes, crashes, and fills.
8. Apply the section-specific shaping pass to the complete pattern.
9. Examine enabled optional kit pieces and probabilistically add context-appropriate percussion.
10. Remove any hits belonging to disabled kit pieces.
11. For every generated hit, retain its musically selected base velocity plus humanized velocity/timing variation.
12. Store a clone as the baseline used by Reset Edits.
13. Display the resulting notes in the editor, where the user is free to add or remove hits manually.
14. Preview uses the resulting pattern directly.
15. MIDI export converts the same pattern into a Standard MIDI File, preserving tempo, time signature, note placement, velocity, and humanized timing.

## 17. Current philosophy and limitations

MIDILab is designed as a starting-point generator rather than an attempt to replace a drummer or automatically compose an entire finished performance.

Several choices are intentionally conservative:

- Genre controls tendencies rather than forcing every stereotypical genre feature.
- Energy changes density and dynamics through multiple rules rather than acting as a simple volume control.
- Section Type influences both overall intensity and orchestration.
- Randomness is concentrated mostly in optional musical choices.
- Humanization is small and centered around musically chosen velocities/timings.
- Optional percussion is deliberately sparse.
- The user always gets the final decision through the grid editor.

There are also current limitations worth understanding:

- The specialized 4/4 genre engines are more detailed than the shared non-4/4 engine.
- There is no learned model or analysis of existing songs.
- BPM does not currently influence compositional density.
- MIDILab does not yet generate relationships among multiple song sections.
- It does not yet reason about a bass line, melody, chord progression, or other instruments.
- Removing a core kit instrument filters it out but does not fully re-orchestrate the groove around the missing piece.
- The dedicated Fill Generator now has several fill families, but its vocabulary is still intentionally compact compared with a full drummer-performance engine.
- The random seed is not exposed or saved.

These are deliberate boundaries of the current version rather than fundamental architectural limits. The internal pattern model keeps note position, instrument, velocity, timing offset, tempo, and meter separate from the MIDI exporter, so the generation system can become substantially more sophisticated without changing the basic editor/export workflow.


## 18. Fill Generator

The v1.2 Fill Generator is intentionally separate from full-pattern generation. It takes the current pattern, identifies the selected ending region of a target bar, removes hits in that region, and generates a replacement fill without changing the earlier part of the groove.

The user selects:

- target bar
- 1 Beat, 2 Beats, or Full Bar
- intensity from 1 to 10

Fill length is meter-aware. In compound or odd eighth-note meters, the beat-sized options follow the configured musical grouping rather than assuming that one literal eighth note equals one musical beat.

The initial fill vocabulary includes:

- snare builds
- mixed snare/tom fills
- descending tom runs
- kick accents
- occasional crash/China color at higher intensity when those pieces are enabled

The fill generator respects the active kit and applies velocity/timing humanization to generated fill hits. Fill intensity influences note density and dynamics rather than acting only as a volume control.

Each generated fill is treated as an editable pattern change and participates in the Undo/Redo history. This allows users to audition several fill ideas without losing the surrounding groove.
