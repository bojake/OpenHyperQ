# SARSA, MACE, and Environments

HyperQ separates environments, learners, selectors, and trainers.

- The environment owns the domain state and reward rules.
- The Q learner owns value estimates.
- The action selector chooses actions from the learner.
- The trainer runs episodes and applies updates.

## Environment Contracts

All environments implement `IEnv<T>`:

```csharp
public interface IEnv<T>
{
    void Render();
    void Reset();
    EnvMetrics Metrics { get; }
}
```

Single-action PvE environments implement `IPvEEnv<T, RT>`:

```csharp
public interface IPvEEnv<T, RT> : IEnv<T> where RT : IReward
{
    T Discretize();
    EnvResult<RT> Step(QAction action);
}
```

The legacy-compatible scalar form is `IPvEEnv<T>`. It returns `Tuple<double, bool>` and is adapted by the trainer to `ScalarReward`.

Multi-action collective environments implement `IMACEPvEEnv<T, RT>`:

```csharp
public interface IMACEPvEEnv<T, RT> : IEnv<T>
{
    T Discretize();
    EnvResult<RT> Step(QAction[] action);
}
```

Use `IPvEEnv<T>` when exactly one action is chosen per step. Use `IMACEPvEEnv<T, RT>` when multiple learners choose a vector of simultaneous actions for one environment step.

## PvESARSATrainer<T>

`PvESARSATrainer<T>` trains one Q learner in one environment. It supports on-policy SARSA and off-policy Q-learning style updates.

```csharp
QRandom random = QRandom.Instance.Seed(0);
var actions = new QOrdinalActionSpace(random, 4);
var q = new MappedQ<decimal>(actions);
var selector = new eGreedyActionSelector<decimal>(actions);
var trainer = new PvESARSATrainer<decimal>(
    q,
    QEvalType.OnPolicy,
    selector,
    random);

var hp = new HyperParams(g: 0.95, e: 0.2, a: 0.1);

for (int i = 0; i < 1000; i++)
{
    trainer.Episode(env, hp);
    hp.Decay();
}
```

### On-Policy vs Off-Policy

`QEvalType.OnPolicy` uses the selected next action in the update:

```text
Q(s,a) <- Q(s,a) + alpha * (r + gamma * Q(s',a') - Q(s,a))
```

This matches SARSA behavior and is sensitive to the current selector policy.

`QEvalType.OffPolicy` uses the best next action according to the Q table:

```text
Q(s,a) <- Q(s,a) + alpha * (r + gamma * max_a Q(s',a) - Q(s,a))
```

This is closer to classic Q-learning and can learn a greedy target policy while the selector still explores.

## Warmup

Warmup runs episodes with a different selector, commonly uniform exploration:

```csharp
var warmupSelector = new UniformActionSelector<decimal>(actions);
trainer.Warmup(env, hp, n_episodes: 100, warmupSelector);
```

Warmup is useful when the Q table starts empty and early exploration matters.

## Replay Memory

Replay memory stores experienced transitions and replays them through the same update path.

```csharp
trainer.EnableMemory(new QMemory<decimal>(capacity: 10000));
trainer.Reminisce(hp, count: 128);
```

Available memory types include:

- `QMemory<T>`: general replay;
- `QNegPosMemory<T>`: separates negative and positive experience;
- `QEpisodicMemory<T>`: remembers episode boundaries;
- `QEpisodicNegPosMemory<T>`: combines episodic and negative/positive behavior.

The trainer also supports `Obsess(...)`, which replays the most recent episode when the memory type supports it.

## Dyna

Dyna learns from a lightweight model of observed transitions.

```csharp
var dyna = new DynaState<decimal>(iterations: 100, capacity: 0);
trainer.EnableDyna(dyna, freq: 0.5, mode: DynaSweepMode.Uniform);
```

`DynaSweepMode.Prioritized` ranks replay by TD error. Start with uniform Dyna unless you specifically need prioritized planning.

## Advantage Modes

The trainer can pass advantage estimates into selectors that use `ApplyAdvantage(...)`, such as policy-gradient selectors.

Every Q update records an advantage for its step, `A(s,a) = Q(s,a) - V(s)`, where `Q(s,a)` is the value just written and `V(s)` is the mean of the state's action row. The baseline depends only on the state, which keeps the policy gradient unbiased and, unlike a plain TD error, keeps the signal alive after the critic has converged. `Advantage[i]` returns the step's advantage divided by the root mean square of the episode's advantages so far; it is not centred, because the sign carries the information.

```csharp
trainer.AdvantageEstimation = AdvantageMode.OneStep;
```

Available modes:

- `OneStep`: applies the scaled step advantage during each step;
- `PostEpisodeGAE`: applies the step advantage during the episode and a generalized-advantage pass over the recorded advantages after it;
- `EligibilityTraceGAE`: applies an online eligibility trace;
- `DeferredGAE`: applies full GAE after the episode.

For normal Q learners with epsilon-greedy selection, the default `OneStep` is sufficient.

## MACEPvESARSATrainer<T, RT>

MACE stands for Multi-Action Collective Environment. A MACE trainer owns multiple minds. Each mind has a Q learner and selector. On every environment step, the trainer asks each mind for an action and passes the resulting `QAction[]` to the environment.

```csharp
using HyperQ.MACE.Training;

var trainer = new MACEPvESARSATrainer<QState<decimal>>();

trainer.Add(
    new MappedQ<QState<decimal>>(moveActions),
    new eGreedyActionSelector<QState<decimal>>(moveActions));

trainer.Add(
    new MappedQ<QState<decimal>>(toolActions),
    new eGreedyActionSelector<QState<decimal>>(toolActions));

var hp = new HyperParams(g: 0.95, e: 0.2, a: 0.1);

for (int i = 0; i < 1000; i++)
{
    trainer.Episode(maceEnv, hp);
    hp.Decay();
}
```

Use MACE when:

- one environment step is controlled by multiple decisions;
- those decisions can be learned by separate Q tables;
- the combined action vector is more natural than one large cross-product action space.

Avoid MACE when a single discrete action is enough. A single learner is easier to inspect, checkpoint, and evaluate.

## SARSA vs MACE

| Question | Use `PvESARSATrainer<T>` | Use `MACEPvESARSATrainer<T, RT>` |
| --- | --- | --- |
| Actions per step | One | Multiple simultaneous actions |
| Environment step signature | `Step(QAction)` | `Step(QAction[])` |
| Learners | One Q learner | One or more minds |
| Best for | GridWorld-style tasks | Wumpus/LEM-style collective actions |
| Complexity | Lower | Higher |

If you are building a new sample for OSS, start with `PvESARSATrainer<T>` unless the domain truly needs multiple independent action choices per step.

## PvEMultiHeadSARSATrainer<T>

`PvEMultiHeadSARSATrainer<T>` trains one multi-head learner against an environment that returns `MultiReward`. It still chooses one action per step, but each reward dimension updates its matching learner head.

```csharp
using HyperQ.MultiHead;
using HyperQ.MultiHead.Training;

var actions = new QOrdinalActionSpace(QRandom.Instance, 4);
var scalarizer = new LinearScalarizer(new[] { 0.7, 0.3 });
var q = new MultiHeadMappedQ<decimal>(
    actions,
    headCount: 2,
    scalarizer: scalarizer);

var selector = new eGreedyActionSelector<decimal>(actions);
var trainer = new PvEMultiHeadSARSATrainer<decimal>(
    q,
    QEvalType.OnPolicy,
    selector);

IPvEEnv<decimal, MultiReward> env = CreateMultiRewardEnvironment();
trainer.Episode(env, new HyperParams(g: 0.95, e: 0.2, a: 0.1));
```

Use MultiHead when the environment has one action per step and multiple reward dimensions. Use MACE when the environment has multiple simultaneous actions per step.
