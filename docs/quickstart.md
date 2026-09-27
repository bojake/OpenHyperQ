# Quickstart

## Prerequisites

- .NET SDK 10.0 or newer.
- Windows for the WinForms visualizer samples.
- A shell at the `OSS` directory.

The SDK is pinned by `global.json`:

```powershell
dotnet --version
```

## Build and Test

```powershell
dotnet restore HyperQ.OSS.sln
dotnet build HyperQ.OSS.sln
dotnet test tests/HyperQ.Tests/HyperQ.Tests.csproj
```

## Run Samples

GridWorld:

```powershell
dotnet run --project samples/GridWorld/GridWorld.csproj
```

GridWorld visualizer:

```powershell
dotnet run --project samples/GridWorld.Viz/GridWorld.Viz.csproj
```

The GridWorld visualizer is the best first interactive sample. It shows the environment, learned cell values, action preferences, running reward, and epsilon/alpha traces while the learner updates.

LEM:

```powershell
dotnet run --project samples/LEM/LEM.csproj -- 10 1 dyna_size=0 memory=0 warmup=0
```

HuntTheWumpus:

```powershell
dotnet run --project samples/HuntTheWumpus/HuntTheWumpus.csproj -- 10 1 dims=4x4 dyna_size=0 memory=0 warmup=0
```

HuntTheWumpus visualizer:

```powershell
dotnet run --project samples/Wumpus.Viz/Wumpus.Viz.csproj
```

## Minimal Training Shape

A normal single-action training loop has four pieces:

```csharp
using HyperQ.Env;
using HyperQ.Learners;
using HyperQ.Training;
using HyperQ.Util;

QRandom random = QRandom.Instance.Seed(0);
QActionSpace<int> actions = new QOrdinalActionSpace(random, 4);
var q = new MappedQ<decimal>(actions);
var selector = new eGreedyActionSelector<decimal>(actions);
var trainer = new PvESARSATrainer<decimal>(q, QEvalType.OnPolicy, selector, random);
var hp = new HyperParams(g: 0.95, e: 0.2, a: 0.1);

IPvEEnv<decimal> env = CreateEnvironment();

for (int episode = 0; episode < 1000; episode++)
{
    trainer.Episode(env, hp);
    hp.Decay();
}
```

The environment owns domain state and rewards. The Q learner owns the value table. The selector turns Q values into chosen actions. The trainer coordinates episodes and updates.

## Persistence Note

Learners persist through the typed checkpoint format (`LearnerCheckpoint`, `MACECheckpoint`, `TrainingCheckpoint`); the LEM and HuntTheWumpus runners expose it as `save=` and `load=`. See [Checkpoints](checkpoints.md).
