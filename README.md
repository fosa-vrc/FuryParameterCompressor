# Fury Parameter Compressor (net.fosa.fury-parameter-compressor)

# 中文说明见 [README.zh-Hans.md](README.zh-Hans.md)。

An NDMF port of VRCFury's parameter compressor, with Modular Avatar's cross-platform parameter alignment in place of VRCFury's own.

## Usage

Add [Fury Parameter Compressor](https://fosa-vrc.github.io/FuryParameterCompressor/) to VCC / ALCOM

Add Fury Parameter Compressor to your project in VCC / ALCOM

Add a **FOSA / Fury Parameter Compressor** component to an avatar. Avatars without the component are left completely alone.

## Development Statement

This project is a product of **Vibe Coding**:

* **Core Development**: Feature porting performed by **Claude Opus 5**, with manual fine-tuning by humans.
* **Review & Bug Fixes**: Code reviewed and debugged jointly by **Claude Fable 5** and humans.

> **License:** this package is a derivative work of VRCFury and is therefore restricted to
> **personal (non-commercial) use**. See [LICENSE.md](LICENSE.md).

## Upstream Versions

| | |
| --- | --- |
|VRCFury | 1.1389.0 |
|Modular Avatar | 1.18.0 |
|NDMF | 1.14.3 |

## Requirements

| | |
| --- | --- |
| Unity | 2022.3 |
| NDMF | >= 1.14.3 |
| VRChat SDK | Avatars 3.0 |
| Modular Avatar | **optional** - see below |

Modular Avatar is not a hard dependency. Only one file names an MA type
(`Editor/SyncParameterSequence/MaSyncParameterSequenceDetector.cs`, behind the `FPC_MODULAR_AVATAR` version
define), and it exists solely to find MA Sync Parameter Sequence components for the mutual-exclusion check.
Without MA installed there can be no such component, so the check never fires. Everything else is either
NDMF API or a copy of MA code that lives in this package.

Note that the parameter counts shown in the inspector come from NDMF's parameter introspection API, which is
what MA plugs its own components into - so with MA absent, the count simply covers fewer sources.

## What it does

When an avatar's synced expression parameters exceed VRChat's budget,
the compressor picks a subset of them and time-slices those through a small set of shared sync slots,
dropping the total cost below the limit.
The picked parameters stop being network-synced individually;
instead an FX layer named `Parameter Compressor` copies them in and out of the shared slots, batch by batch.

The trade-off is sync latency: a full round takes roughly `batches * 0.1s` at best and twice that at worst.
The actual figures are reported to the NDMF console after every build.

## Component options

| Option | Meaning |
| --- | --- |
| Primary Platform | `PC` (default) / `Android` / `iOS`. The platform whose solver result is authoritative - build it first, the others replay its result. |
| Write Defaults | `Auto` / `Force ON` / `Force OFF`. `Auto` matches the rest of the FX controller using the same rule as MA Merge Animator's "Match Avatar Write Defaults"; an avatar that mixes both settings resolves to OFF. |
| Parameter Whitelist | Parameters that are never compressed. They keep syncing at full speed and keep costing their full bit count. |

The inspector also shows the avatar's current parameter usage,
counted through NDMF's parameter introspection API - the same source "MA Information" uses,
so Modular Avatar components are included when Modular Avatar is installed.

### Parameter whitelist

The list is a standard multi-selection list. Click the handle at the left of a row to select it (dragging it reorders),
use Ctrl-click / Shift-click to select several, and press `Delete` (or the *Delete* button) to remove them.
Each row also has a `-` button that removes just that row. Rows are editable text fields,
and the list is reorderable (the order has no effect on the build).

Three ways to fill it, which can be mixed freely:

* **Add** - appends a blank row you can type into.
* **Pick** - opens a picker window listing the avatar's synced parameters (again through NDMF introspection, so MA components are included).
  It supports Ctrl-click / Shift-click multi-selection plus *Select All* / *Invert* / *Clear* / *Refresh*;
  `Enter` does the same as *Add*. **Selection is additive only** - the window never removes anything from the whitelist,
  and parameters that are already whitelisted are not listed at all.
  *Close After Add* (on by default) controls whether the window closes once you press *Add*.
  The *Search* field shows only the parameters containing the text (case-insensitive). *Select All* / *Invert* work on what is shown,
  and a selected entry that the search hides is deselected, so *Add* only ever adds what you can see.
* **Import from Asset** - drop a `VRC Expression Parameters` asset on the field to append every parameter name in it.

*Clear* empties the whole list, asking for confirmation first.

Duplicate and blank entries are allowed on purpose; they are only collapsed at the moment the whitelist is applied during a build.

## Build behaviour

Three NDMF passes:

* **Check for conflicts** - `BuildPhase.Resolving`, i.e. before Modular Avatar deletes its own components.
* **Compress parameters** - `BuildPhase.Optimizing`, declared `AfterPlugin` against *every* NDMF plugin found in the project
  (discovered by scanning assemblies for `[assembly: ExportsPlugin]`),
  so that it sees the final parameter list. It requires NDMF's `AnimatorServicesContext`.
* **Remove component** - `BuildPhase.Optimizing`, unlike the two above it runs on *every* NDMF platform.
  On VRChat builds it is a no-op (the compressor pass already removed the component);
  on other platforms it is the only pass that runs, so exports never ship the component.

The component is destroyed at the end of the pass in every case, including when compression was skipped.

> **`Constraint loop detected`:** *Compress parameters* is ordered after every other NDMF plugin,
> so a plugin that in turn insists on running after this one (for example, another tool that also orders itself after every plugin)
> forms a loop with it, and NDMF then fails every build with `Constraint loop detected`.
> If you see this error, please [report it](https://github.com/fosa-vrc/FuryParameterCompressor/issues) as soon as possible,
> together with the NDMF plugins installed in your project.

### Aborts and skips

| Situation | Result |
| --- | --- |
| More than one component on the avatar | `[FPC-0001]` error, build aborts |
| **MA Sync Parameter Sequence** on the same avatar | `[FPC-0002]` error, build aborts |
| **VRCFury** detected in the project (package or `Assets/` copy), with *Tools > VRCFury > Settings > When avatar is over parameter limit* left at *Compress parameters to fit* (the default) | `[FPC-0003]` warning, nothing is done - VRCFury's own compressor takes over. Set it to *Ask* or *Fail the build* to have this tool compress instead |
| Cannot fit even at maximum compression | `[FPC-0004]` error, build aborts |
| Secondary platform, no primary-platform record (or no blueprint ID) | `[FPC-0005]` error, build aborts |
| Secondary platform has a synced parameter the primary doesn't | `[FPC-0006]` error, build aborts |
| Parameter types differ across platforms | `[FPC-0007]` error, build aborts |
| Merged parameters still exceed the budget after replaying the compression | `[FPC-0008]` error, build aborts |
| Recorded result came from another version of this tool, or no longer matches | `[FPC-0009]` error, build aborts |
| Primary Platform setting differs from the recorded one | `[FPC-0010]` warning, build continues |
| The compressed result doesn't match the primary platform's | `[FPC-0011]` error, build aborts |
| Merged parameter set differs from the primary platform's (type / default value / synced flag) | `[FPC-0012]` error, build aborts |
| Compression is needed but the avatar has no FX controller | `[FPC-0013]` error, build aborts |
| Alignment failed, but this is play mode rather than an upload | `[FPC-0014]` warning, parameters are solved locally |

Every abort above only aborts an *upload*.
Under NDMF's "apply on play" processing the alignment messages are reported as warnings instead and the avatar's parameters are solved on their own,
so it stays testable before its primary platform has ever been built. Play mode never writes a record.

### Primary platform

The expression parameters are copied into a build-time asset (the original is never modified) and sorted by name;
like upstream VRCFury, the solver sees duplicate names as-is and they are only collapsed after solving.
The solver then tries progressively more aggressive menu-type selections and keeps the one that fits while syncing fastest.
Whitelisted parameters and parameters that aren't driven from the expressions menu are never picked.
Parameters driven by an `Add`-type parameter driver are excluded too, except in the attempts that allow FourAxisPuppet controls.

The result is written to `Library/net.fosa.fury-parameter-compressor/AvatarParameterInfo/<blueprint id>`:
tool version, save version, the synced parameter set before compression, the resulting parameter set, slot counts,
the ordered list of compressed parameters, and a SHA-256 over the parts a secondary platform replays
(the resulting parameter set is stored for the final consistency check but is not covered by the hash - see below).
Nothing is ever written outside the project.
An avatar without a blueprint ID is still compressed, but no record is kept, so it cannot serve as a cross-platform reference.

### Secondary platforms

No solving happens. The primary platform's record is loaded and validated (save version, tool version, hash),
parameters that only exist on the primary platform are added back, and the recorded solution is then replayed verbatim.
After merging, the parameter set is hashed again and compared against the record;
a mismatch aborts the build (naming the first differing parameter) rather than producing a differently-shaped avatar.

Finally, once the layer has been generated, the resulting synced parameter set is compared against the primary platform's.
The generated `FPC/*` slot names are created after the hash input was snapshotted, so they cannot be covered by the hash;
this last check catches the case where a name collision that only exists on one platform forces a slot to be renamed there.

Build targets other than Windows / Linux / Android / iOS are not tracked:
those builds simply solve on their own and neither read nor write a record.

### Determinism

Two builds of the same avatar produce the same result: parameters are sorted with an ordinal comparison before solving,
the solver works on ordered lists rather than hash sets,
generated parameter names are derived deterministically (`FPC/<name>`, then `_1`, `_2`, ... on collision),
and nothing depends on a random seed or a timestamp.

### Generated parameters

All under the `FPC/` prefix: `SyncIndex<i>` (synced bool, the batch index),
`SyncDataNum<i>` and `SyncDataBool<i>` (the shared synced slots),
one unsynced latch per compressed parameter whenever the solution needs more than one batch (a single-batch solution needs no latches),
and one animator-only `True` used as an unconditional transition condition.

### Console output

Every build that reaches the compressor writes at least one entry to the NDMF console - including "no compression was needed".
This is deliberate: NDMF only refreshes the error report window when the new build produced at least one entry,
so a silent build would leave the previous build's failure on screen.

When compression happens, the report lists the old and new bit totals,
which menu types were compressed, the sync delay range, slots per batch and batch count.
If the solver noticed parameters that probably shouldn't be synced at all
(unused in every controller, generated by a contact or PhysBone, driven by a momentary button, or only reachable via OSC),
those are reported as a second entry.

## Localization

English and Simplified Chinese, following NDMF's editor language setting.
Strings live in `Editor/Localization/*.json`; UI elements are tagged with NDMF's `ndmf-tr` class,
so the inspector re-localizes itself when the language changes.

## Source

Every file starts with a comment naming the upstream file it was ported from and what was changed:

* `Editor/Editor-Avatars/**`, `Editor/Editor-Common/**` - VRCFury (`com.vrcfury.vrcfury`), same
  relative paths.
* `Editor/ErrorReporting/**`, `Editor/Inspector/**`, `Editor/Localization/**`,
  `Editor/PluginDefinition/**`, `Editor/SyncParameterSequence/**`, `Runtime/**` - Modular Avatar
  (`nadena.dev.modular-avatar`), same relative paths. These are *copies*; apart from
  `MaSyncParameterSequenceDetector.cs` none of them call into Modular Avatar, so they work whether or
  not MA is installed.

VRCFury's legacy (bad-priority) compressor was not ported.
