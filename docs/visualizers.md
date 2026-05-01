# Visualizers

The visualizer samples make the learning process visible while training runs. They are WinForms projects, so they require Windows.

## GridWorld

![GridWorld visualizer showing learned cell values and training traces](gridworld-screen.png)

Run it with:

```powershell
dotnet run --project samples/GridWorld.Viz/GridWorld.Viz.csproj
```

The GridWorld visualizer is useful because it exposes the table-learning process directly:

- the left grid shows the current world layout;
- the colored grids show learned values and action preferences by cell;
- the circular view emphasizes per-cell action/value distributions;
- the lower charts track running average reward and exploration/learning parameters.

As training progresses, useful states and actions separate visually from poor choices. That makes it easier to explain what the learner is doing than reading raw reward logs.

## HuntTheWumpus

Run it with:

```powershell
dotnet run --project samples/Wumpus.Viz/Wumpus.Viz.csproj
```

The Wumpus visualizer is a richer environment demo. Use it after GridWorld when you want to inspect behavior in a larger, less uniform state space.
