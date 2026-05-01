# Checkpoints

The OSS tree includes a typed checkpoint foundation that avoids `BinaryFormatter`.

## Supported Components

Checkpoint support is available for components that implement `ICheckpointable`:

- `HyperParams`
- `QParam` schedules
- `RunningLogit`
- `ClassicQ`
- `PolicyGradientActionSelector<T>`

`TrainingCheckpoint` can save and load a Q learner, hyperparameters, the current episode number, and an optional selector payload.

```csharp
TrainingCheckpoint.Save(
    path: "checkpoint.hqcp",
    q: classicQ,
    hp: hyperParams,
    episode: episode,
    selector: selector);

TrainingCheckpointMetadata metadata = TrainingCheckpoint.Load(
    path: "checkpoint.hqcp",
    q: restoredQ,
    hp: restoredHyperParams,
    selector: restoredSelector);
```

The selector payload is optional. If a checkpoint contains selector data and no selector is supplied to `Load`, the loader skips that payload.

## Current Limitations

Many learner types do not yet implement `ICheckpointable`, including the mapped, double-Q, and HyperQ learners commonly used by the LEM and HuntTheWumpus samples. For that reason, the OSS sample runners currently reject `save=` and `load=` arguments.

This is intentional. Shipping a clear unsupported error is better than keeping old binary serialization around.

## Adding Checkpoint Support

To add checkpoint support to another component:

1. Implement `ICheckpointable`.
2. Write a deterministic version marker for that component.
3. Serialize only stable public model state, not runtime-only caches.
4. Add round-trip tests in `tests/HyperQ.Tests`.
5. Add a compatibility test if the format may be used across releases.

For learners, include:

- action-space identity or enough action metadata to validate compatibility;
- state mapping;
- all Q values;
- default-value behavior if it affects restored behavior;
- any secondary table state for double-Q learners.

## Recommended Release Message

For the initial OSS release, describe checkpointing as partial and typed:

```text
HyperQ OSS includes a typed checkpoint foundation for supported components. Sample save/load is disabled until all sample learner types implement the new checkpoint contract.
```
