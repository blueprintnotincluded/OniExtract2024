# Repository instructions for coding agents

Read `CLAUDE.md` before doing substantive work. Despite its name, it is the canonical
repository guide for all coding agents: architecture, the three export paths, build and
deploy commands, game-assembly gotchas, and the export contract invariants live there.
Consult `docs/` for durable reference and `agent/` for dated working notes; do not assume a
status heading in `agent/` or in `docs/archive/` is current.

## What this repository is

A mod for the game Oxygen Not Included that dumps game data and art to disk for the
blueprintnotincluded website. It runs inside the game. That shapes everything below.

## Verification reality

- The mod links against ONI's `Assembly-CSharp.dll` and `UnityEngine.dll`, which are not
  redistributable. CI builds and tests **only** `OniExtract2024.Core` — six unit tests.
- A green build, and a green CI run, say nothing about whether the export is correct. Correct
  output can only be confirmed by a human running the game.
- Report what you compiled and tested, and state plainly that in-game behaviour is unverified.
  Never describe an export-affecting change as working or done on the strength of a build.
- Two failure modes look like success and must be ruled out before believing a result: ONI
  caches mod DLLs for the whole session, so the game must be **fully restarted** after a
  rebuild; and the post-build deploy step fails while the game is running, which reads as a
  build error but is not.

## Safety and scope

- Do not circumvent safeguards or search for credentials, tokens, or alternate access paths.
  If required access is unavailable, stop and ask the user.
- Local code edits and local build/test commands made in good faith for the current task are
  in scope. Keep changes narrowly related to the request and preserve unrelated work.
- Never make changes on `master`. Before editing, verify the current branch and worktree. If
  on `master`, create or request a task branch first.
- Do not commit, push, open a PR, or mutate external systems unless the user asks.
- Do not kill the user's running game process to make a build succeed — say the game is
  running and let them close it.
- The export output directory (`Documents\Klei\OxygenNotIncluded\export`) can represent hours
  of manual in-game work, particularly building images and pose overrides. Do not delete or
  overwrite it as a side effect of a task.

## Repository conventions

- The mod targets .NET Framework 4.8; `OniExtract2024.Core` targets netstandard2.0. Do not
  retarget either.
- Pure logic with no Unity or ONI dependency belongs in `OniExtract2024.Core`, because that is
  the only code CI can verify. Anything needing game types stays in `OniExtract2024`.
- The `model/` DTOs are the export schema. A field renamed there is a breaking change for the
  consuming website; update `docs/EXPORT_SCHEMA.md` in the same commit.
- `utilities[]` must keep carrying every connection type with cell offsets. The power-specific
  fields are additive and never a replacement — this has been regressed once before.
- Tests are xunit. Do not introduce another test framework.
- Read building data from `buildingDef.BuildingComplete`, never from a spawned instance.
- Use `ilspycmd` to verify game-assembly details. Deep runtime reflection overflows the Unity
  stack.
- Working plans and implementation drafts go in the gitignored `specs/`, not in `docs/`.

## Validation

Choose checks proportional to the change. Fastest first:

1. `dotnet test OniExtract2024.Core.Tests/OniExtract2024.Core.Tests.csproj` — runs anywhere.
2. Build the mod with MSBuild (see `CLAUDE.md`) — requires the game's `Managed` folder, and
   requires ONI to be closed (or `-p:ModFolder=<temp-dir>` to compile without deploying).
3. `dotnet test OniExtract2024.Tests/OniExtract2024.Tests.csproj` — requires the game installed.
4. In-game run of the affected export path, checking `Player.log` for `OniExtract:` lines.
   Only a human can do this. Name it as outstanding when you cannot.
