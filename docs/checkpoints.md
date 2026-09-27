# Checkpoints

HyperQ persists learners through a typed checkpoint format; `BinaryFormatter` is not used anywhere.

## Supported Components

Every component below implements `ICheckpointable` (a version marker, `SaveCheckpoint(BinaryWriter)`, `LoadCheckpoint(BinaryReader)`):

- learners: `ClassicQ`, `ClassicQQ`, `MappedQ<T>`, `MappedQQ<T>`, `SingleHyperQ<T>`, `DoubleHyperQ<T>`, `LayeredHyperQ<T>`
- action spaces: `QOrdinalActionSpace` and every `QMappedActionSpace<T>`; a learner writes its action space with its rows because the column an action occupies depends on the order the actions were first seen
- `PolicyGradientActionSelector<T>` (and the selectors derived from it)
- `HyperParams`, `QParam` schedules, `RunningLogit`

State keys are written through `IQKeySerializer<T>`. The built-in serializers cover `decimal`, `int`, `long`, `string` and `QState<T>` of those; a learner with another key type sets its `KeySerializer` property, and a mapped action space with another action type overrides `ActionKeySerializer`.

## Containers

`LearnerCheckpoint` wraps one component in a self-describing frame (magic string, format version, the component's type name, the payload framed with its length) and reads or writes files, compressing when the name ends in `.gz`:

```csharp
LearnerCheckpoint.SaveFile("wumpus-q.gz", learner);
// later, into a learner constructed with the same action space and options:
LearnerCheckpoint.LoadFile("wumpus-q.gz", learner);
```

`MACECheckpoint` does the same for an array of `MACEMind<T>`: each learner, and each action selector that keeps state. The minds are constructed first, exactly as they were when saved; the checkpoint restores their contents and rejects a different learner type or mind count.

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

## Sample Runners

The LEM and HuntTheWumpus runners accept `save=<file>` and `load=<file>`. `save=` writes the learners after training (`.gz` is appended unless the name already ends in it). `load=` is applied once `Run` has created the learners from the command line, because a checkpoint holds learned state, not the learner configuration; a checkpoint written by a different learner type is reported and ignored.

## Limitations

- Shared state maps (double-Q and layered learners) are written once per table that shares them; the restore is idempotent, but the file is larger than it needs to be.
- Index repositories that are shared process-wide (`IndexRepo.Instance`) are advanced past the restored indices; restoring into a process that already holds other learners on the same repository is safe but leaves gaps.
- `FileIndexMapper` persists itself and is not part of a checkpoint.

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

```text
HyperQ OSS persists every learner through a typed, versioned checkpoint format. The sample runners save and load trained learners with save= and load=.
```
