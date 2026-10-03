# Contributing

OniExtract2024 is a mod that runs inside Oxygen Not Included and writes the game's data and art
to disk for the [blueprintnotincluded](https://github.com/blueprintnotincluded/blueprintnotincluded)
website. This page is the short version of how to work on it. The detail — architecture, the
three export paths, the gotchas, the export contract — is in [CLAUDE.md](CLAUDE.md), which is
written for coding agents but applies equally to people.

## What you need

- Oxygen Not Included installed. The mod links against the game's `Assembly-CSharp.dll` and
  `UnityEngine.dll`, which cannot be redistributed, so there is no building the mod without it.
- MSBuild from Visual Studio (the mod project is an old-style .NET Framework 4.8 project).
- The .NET 8 SDK, for the test projects.

Build, deploy and test commands are in the README's [Build](README.md#build) section and in
CLAUDE.md under "Development Commands".

## What CI checks, and what it does not

CI builds and tests **only** `OniExtract2024.Core`, the small Unity-free library: 6 tests. It
cannot build the mod or the main test project, because a hosted runner has no copy of the game.

So a green check on a pull request means very little. Before you open one, on a machine with the
game installed:

1. Build the mod with MSBuild.
2. Run both test projects (`OniExtract2024.Core.Tests` and `OniExtract2024.Tests`).

Even that only shows the code compiles and the unit tests pass. Whether an export is *correct*
can only be seen by running the game and looking at the output. If your change touches anything
the export writes — `building.json`, the images, the connection sprites — say in the pull request
whether it has been run in game, and if it has not, what should be run and what to look for.

Keep changes that can alter the export in their own pull request, apart from refactoring and
documentation. A cleanup that is meant to change nothing should produce an export identical to
the previous one, and that is only checkable when the cleanup is not mixed with something else.

## Sending a change

- Branch from `master`; never commit to it directly. Everything lands through a pull request.
- Open the pull request against `blueprintnotincluded/OniExtract2024`. GitHub lists this
  repository as a fork of `cnctemaR/OniExtract2024`, so both the web UI and `gh pr create` may
  offer the upstream repository as the base — check before submitting
  (`gh pr create --repo blueprintnotincluded/OniExtract2024`).
- Conventional commit subjects (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`).
- Open it as a draft if it changes the export and has not been run in game yet.
- CodeRabbit reviews a pull request when it is opened. It does not re-review on later pushes;
  comment `@coderabbitai review` to ask for another pass.
- Issues are disabled on this repository. Raise problems in a pull request or with the
  maintainers directly.

## Conventions

- **The `model/` classes are the export schema.** A field added there appears in the JSON; a
  field renamed there breaks the website. Update [docs/EXPORT_SCHEMA.md](docs/EXPORT_SCHEMA.md)
  in the same commit.
- **Comments are `//`, and they say why.** Types carry a `/// <summary>` where it helps a reader
  find their way; members and logic use plain `//` comments explaining the non-obvious. There is
  no XML-documentation coverage target — the mod is not a library and ships no XML docs — so do
  not add `///` blocks to satisfy a tool.
- **Logic that needs no game types goes in `OniExtract2024.Core`**, because that is the only
  code CI can test.
- **Tests are xunit.**
- **Documentation:** durable reference in `docs/`, resolved investigations in `docs/archive/`,
  dated working notes in `agent/`. Working plans go in the gitignored `specs/`.

## Licence

GPL-2.0 — see [LICENSE](LICENSE). The camera-snapshot code in `building/` and `connection/` is
adapted from other GPL-2.0 mods; keep the attribution headers at the top of those files.
