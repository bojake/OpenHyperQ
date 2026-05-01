# HyperQ OSS

This is a .NET 10-focused OSS slice of HyperQ. It keeps the core learner libraries and the sample environments that are most suitable for public release:

- GridWorld
- GridWorld Visualizer
- LEM
- HuntTheWumpus
- HuntTheWumpus Visualizer
- MultiHead learners

The closed-source licensing integration, prebuilt binaries, legacy project files, and experimental/distributed samples are intentionally not included in this directory.

![GridWorld visualizer showing learned cell values and training traces](docs/gridworld-screen.png)

## License

HyperQ OSS is licensed under the GNU General Public License version 3.0 only. See [LICENSE](LICENSE).

## Release Status

This tree is suitable for an initial source release, but should be presented as an early OSS release rather than a finished 1.0 API. It builds cleanly on .NET 10, includes focused samples, and avoids legacy binary serialization. The main known gap is checkpoint coverage: the typed checkpoint format exists, but not every learner or sample runner can persist yet.

## Documentation

- [Documentation index](docs/README.md)
- [Quickstart](docs/quickstart.md)
- [Q learners](docs/q-learners.md)
- [Action selectors](docs/action-selectors.md)
- [SARSA, MACE, and environments](docs/trainers-and-environments.md)
- [Visualizers](docs/visualizers.md)
- [Checkpoints](docs/checkpoints.md)
- [Release checklist](docs/release-checklist.md)

## Build

```powershell
dotnet build HyperQ.OSS.sln
```

## Run Samples

```powershell
dotnet run --project samples/GridWorld/GridWorld.csproj
dotnet run --project samples/GridWorld.Viz/GridWorld.Viz.csproj
dotnet run --project samples/LEM/LEM.csproj -- 10 1 dyna_size=0 memory=0 warmup=0
dotnet run --project samples/HuntTheWumpus/HuntTheWumpus.csproj -- 10 1 dims=4x4 dyna_size=0 memory=0 warmup=0
dotnet run --project samples/Wumpus.Viz/Wumpus.Viz.csproj
```

Persistence in the original samples used `BinaryFormatter`, which is removed in modern .NET. The OSS sample runners currently reject `save=` and `load=` arguments until their learner types implement the typed checkpoint contract.

## Checkpoints

The OSS build includes the new typed checkpoint foundation for supported components:

- `HyperQ.Util.ICheckpointable`
- `HyperQ.Training.TrainingCheckpoint`
- `HyperParams`, `QParam`, `RunningLogit`
- `ClassicQ`
- `PolicyGradientActionSelector<T>`

The selected samples still keep `save=` and `load=` disabled because they commonly use learner types that do not yet implement `ICheckpointable`.
