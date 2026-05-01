# Release Checklist

This project is close enough for an initial FOSS source release if the release is scoped honestly. It is not ready to present as a polished 1.0 package yet.

## Must Do Before Publishing

- Confirm package and repository names.
- Decide whether this repository is source-only or will publish NuGet packages.
- Add a short `CONTRIBUTING.md` if external PRs are expected.
- Add CI that runs `dotnet build HyperQ.OSS.sln` and `dotnet test tests/HyperQ.Tests/HyperQ.Tests.csproj`.
- Keep the README checkpoint limitation visible.

## Should Do Soon

- Add checkpoint support for `MappedQ<T>` and `MappedQQ<T>`.
- Add a small environment-authoring tutorial.
- Add API XML documentation for the main public interfaces.
- Add a changelog once releases begin.
- Add screenshots or short GIFs for the WinForms visualizers.
- Decide which experimental selectors should be marked stable, preview, or internal.

## Known Gaps To State Openly

- License is GPL-3.0-only, matching the public HyperQ samples repository.
- Checkpointing is partial.
- The sample runners disable old `save=` and `load=` arguments.
- Layered HyperQ and GRPO/KL selectors are advanced APIs and need more examples.
- Visualizers are Windows-oriented because they use WinForms.
- The public API has not yet been shaped as a NuGet-stable semantic-versioned contract.

## Suggested Initial Positioning

Use wording like:

```text
HyperQ OSS is an early .NET 10 source release of the core HyperQ learner libraries and representative samples. It is intended for experimentation, review, and extension. The repository builds cleanly and includes GridWorld, LEM, HuntTheWumpus, and visualizers. Some advanced APIs and checkpoint coverage are still being completed.
```

That framing gives contributors a clear target and prevents reasonable gaps from looking accidental.
