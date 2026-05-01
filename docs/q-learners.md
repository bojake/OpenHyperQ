# Q Learners

All Q learners implement `Q<T>`, where `T` is the discretized state key type used by the environment. A learner provides:

- an action space through `ActionSpace`;
- indexed access with `q[state, action]`;
- `OnPolicyUpdate(...)` for SARSA-style updates;
- `OffPolicyUpdate(...)` for Q-learning-style updates;
- `ArgMax(...)` and `ArgMin(...)` for action selection;
- `Snapshot(...)`, `AsMatrix`, `Shape`, and `Clone()` for inspection.

The action parameter is the domain action value, not always a dense zero-based array index. `QActionSpace<int>` maps between domain actions and internal action indexes.

## ClassicQ

`ClassicQ` is the simplest table learner. It uses `decimal` state keys and dense action rows.

Use it when:

- your environment can encode every state as a stable `decimal`;
- the number of actions is fixed and modest;
- you want checkpoint support today.

```csharp
var actions = new QOrdinalActionSpace(QRandom.Instance, 4);
var q = new ClassicQ(actions, defaultValueFunc: () => 0.0);
```

`ClassicQ` currently implements `ICheckpointable`.

## MappedQ<T>

`MappedQ<T>` supports arbitrary state key types through an index mapper. It stores sparse rows and maps states as they are encountered.

Use it when:

- your state is not naturally a `decimal`;
- you want a custom key type, such as `string`, `int`, or `QState<decimal>`;
- you want the learner to grow with visited states.

```csharp
var actions = new QOrdinalActionSpace(QRandom.Instance, 4);
var q = new MappedQ<string>(actions);
```

`MappedQ<T>` is used heavily by the samples because it is flexible for ported environments.

## ClassicQQ and MappedQQ<T>

The `QQ` learners are double-Q learners. They keep two value estimates and use them to reduce the positive bias that can come from repeatedly selecting a maximum over noisy values.

Use them when:

- max-value overestimation is a concern;
- the environment has noisy rewards;
- you can afford the extra table state.

`ClassicQQ` is the decimal-state version. `MappedQQ<T>` is the mapped-state version.

```csharp
var q = new MappedQQ<decimal>(actions);
```

## SingleHyperQ<T>

`SingleHyperQ<T>` is a HyperQ learner over `QState<T>`. A `QState<T>` composes multiple state components into one structured state key.

Use it when:

- a flat scalar state hides useful structure;
- the environment naturally has multiple state dimensions;
- you want the learner and selector to reason over composed state.

```csharp
var q = new SingleHyperQ<decimal>(actions);
```

## DoubleHyperQ<T>

`DoubleHyperQ<T>` combines HyperQ state composition with double-Q learning.

Use it when:

- you want `QState<T>` state composition;
- you also want double-Q behavior to reduce max-selection bias.

```csharp
var q = new DoubleHyperQ<decimal>(actions);
```

## LayeredHyperQ<T>

`LayeredHyperQ<T>` composes multiple HyperQ layers. It is the most experimental and flexible learner in the OSS slice.

Use it when:

- you want a hierarchy of learners rather than one flat table;
- different layers should learn different state abstractions;
- you are working from the existing LEM or Wumpus sample patterns.

Layered learners are best treated as advanced APIs until more public examples are added.

## Generator Helpers

`SingleQGenerator<T>` and `DoubleQGenerator<T>` create learners for layered configurations. They are mainly useful with `LayeredHyperQ<T>` and sample runner code.

## MultiHead Learners

MultiHead learners keep one Q learner per reward dimension and use an `IRewardScalarizer` to score the per-head action values for selection.

Use them when:

- the environment returns a `MultiReward`;
- separate reward dimensions should remain inspectable;
- action choice should use a weighted scalarization instead of collapsing rewards before learning.

Available concrete learners include:

- `MultiHeadClassicQ`
- `MultiHeadMappedQ<T>`
- `MultiHeadClassicQQ`
- `MultiHeadMappedQQ<T>`
- `MultiHeadHyperQ<T>`
- `MultiHeadHyperQQ<T>`

```csharp
var actions = new QOrdinalActionSpace(QRandom.Instance, 4);
var scalarizer = new LinearScalarizer(new[] { 0.7, 0.3 });
var q = new MultiHeadMappedQ<decimal>(
    actions,
    headCount: 2,
    scalarizer: scalarizer);
```

Each head receives its matching reward component during updates. `ArgMax` and `ArgMin` scalarize the per-head action values, then choose from the combined score.

## Choosing a Learner

| Learner | State type | Main strength | Best first choice |
| --- | --- | --- | --- |
| `ClassicQ` | `decimal` | Simple dense table, checkpoint support | Small numeric environments |
| `MappedQ<T>` | any key type | Flexible state keys | Most new environments |
| `ClassicQQ` | `decimal` | Double-Q with scalar state | Noisy scalar environments |
| `MappedQQ<T>` | any key type | Double-Q with flexible states | Noisy custom-state environments |
| `SingleHyperQ<T>` | `QState<T>` | Structured state | Multi-feature state spaces |
| `DoubleHyperQ<T>` | `QState<T>` | Structured double-Q | Multi-feature noisy spaces |
| `LayeredHyperQ<T>` | `QState<T>` | Hierarchical composition | Advanced experiments |
| `MultiHead*` | scalar or `QState<T>` | Vector rewards with inspectable heads | Multi-objective reward spaces |

For a new OSS user, start with `MappedQ<T>` unless there is a specific reason to use a different learner.
