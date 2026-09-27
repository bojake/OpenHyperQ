# Action Selectors

Action selectors implement `IActionSelector<T>`. A selector receives the current Q view, the current state, and the current epsilon value, then returns a `QAction`.

```csharp
QAction SelectAction(IArgMinMax<T> q, T state, double epsilon);
```

`QAction` contains:

- `Action`: the domain action value passed to the environment;
- `Value`: the Q value or selector score associated with the action;
- `Index`: the action-space index.

Selectors also expose:

- `LastActionWasRandom` and `RandomActionCount`;
- `ActionMode` for evaluation behavior;
- `TelemetryCallback` for selected-action tracing;
- `StartEpisode()` and `EndEpisode(...)` hooks;
- `ApplyAdvantage(...)` for selectors that learn from advantage updates.

## UniformActionSelector<T>

`UniformActionSelector<T>` explores actions without consulting Q values. It tries unvisited actions for each mapped state first, then favors the least visited action.

Use it for:

- warmup episodes;
- smoke testing an environment;
- collecting broad early experience before exploiting Q values.

```csharp
var selector = new UniformActionSelector<decimal>(actions);
```

## eGreedyActionSelector<T>

`eGreedyActionSelector<T>` is the usual baseline for Q-learning and SARSA. With probability `epsilon`, it chooses a random action. Otherwise it chooses `ArgMax(state)`.

Use it for:

- most tabular Q-learning experiments;
- simple, understandable exploration;
- training where `HyperParams.Epsilon` decays over time.

```csharp
var selector = new eGreedyActionSelector<decimal>(actions);
```

Set `ActionMode = MinMaxActionEnum.LeastProbable` to make the non-random path choose `ArgMin` instead of `ArgMax`.

## MaxActionSelector<T> and MinActionSelector<T>

`MaxActionSelector<T>` always selects the current maximum-valued action. `MinActionSelector<T>` selects the current minimum-valued action.

Use them for:

- evaluation after training;
- deterministic behavior;
- adversarial or minimization-style environments.

```csharp
var evaluatorSelector = new MaxActionSelector<decimal>(actions);
```

These selectors ignore epsilon for normal selection, but still maintain action-history bookkeeping used by the base selector.

## MostProbableMaxActionSelector<T> and LeastProbableMaxActionSelector<T>

These selectors use `ArgMax` for exploitation and use their action-history distribution when epsilon triggers exploration.

Use them when:

- you want max-value exploitation;
- random fallback should be biased by recent action history;
- you want more structured exploration than plain epsilon-greedy.

## PolicyGradientActionSelector<T>

`PolicyGradientActionSelector<T>` maintains per-state logits and updates them through `ApplyAdvantage(...)`. After every Q update the SARSA trainer calls `ApplyAdvantage` with the advantage of the action just taken: the updated `Q(s,a)` minus the mean of the state's action values, scaled to unit magnitude over the episode. The trainer's advantage modes select the GAE-style variants of that signal.

Use it when:

- the selector should learn a policy separate from the Q values;
- you want entropy tracing;
- you want checkpoint support for selector state.

```csharp
var selector = new PolicyGradientActionSelector<decimal>(actions);
```

During training, keep `ActionMode = MinMaxActionEnum.Default` so the selector samples from the learned policy. During evaluation, set `MostProbable` or `LeastProbable` to choose deterministically from the policy distribution.

## PolicyGradientWithSoftmaxActionSelector<T>

This selector uses softmax-style probabilities for action choice. It is useful when action probabilities should be smoother and temperature-like behavior is desirable.

Use it when:

- you want probability-weighted action selection;
- deterministic max selection is too brittle;
- the policy should continue sampling among plausible actions.

## SoftmaxWithKLPenaltyActionSelector<T>

This selector extends the softmax policy-gradient behavior with a KL penalty. It is meant to discourage the policy from moving too far from its prior distribution too quickly.

Use it when:

- policy updates are unstable;
- you want a conservative policy improvement step;
- you are experimenting with PPO-like constraints in a tabular setting.

## GRPOWithKLPenaltyActionSelector<T>

`GRPOWithKLPenaltyActionSelector<T>` is a grouped relative policy optimization selector with KL penalty behavior.

Use it as an experimental selector when:

- relative advantages are more useful than raw TD deltas;
- policy updates need a KL constraint;
- you are comfortable reading the implementation before relying on it.

## HybridActionSelector<T>

`HybridActionSelector<T>` composes selector behavior. Use it when one selector should be used for part of training and another for a different phase or condition.

## Telemetry

Every selector has a `TelemetryCallback` hook:

```csharp
selector.TelemetryCallback = (state, action) =>
{
    Console.WriteLine($"{state}: {action.Action}");
};
```

This is the simplest way to trace policy behavior without changing the trainer.

## Practical Defaults

Start with:

- `eGreedyActionSelector<T>` for training a normal Q learner;
- `UniformActionSelector<T>` for warmup;
- `MaxActionSelector<T>` for evaluation;
- `PolicyGradientActionSelector<T>` only when you specifically want a trainable policy selector.
