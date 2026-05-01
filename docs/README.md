# HyperQ Documentation

HyperQ is a compact reinforcement learning toolkit for tabular Q-learning, HyperQ state composition, multi-head reward decomposition, and multi-action collective environments. This OSS tree focuses on .NET 10, source-first usage, and a small set of representative samples.

The OSS release is licensed under GPL-3.0-only. See the repository root `LICENSE` file.

Start here:

- [Quickstart](quickstart.md): build, test, and run the included samples.
- [Q learners](q-learners.md): `ClassicQ`, mapped learners, double-Q learners, and HyperQ variants.
- [Action selectors](action-selectors.md): exploration, exploitation, policy-gradient selectors, and telemetry hooks.
- [SARSA, MACE, and environments](trainers-and-environments.md): how the trainers interact with environments and replay.
- [Visualizers](visualizers.md): how to read the GridWorld and HuntTheWumpus visualizer samples.
- [Checkpoints](checkpoints.md): current typed checkpoint support and limitations.
- [Release checklist](release-checklist.md): items to finish before a public FOSS announcement.

## Project Shape

- `src/HyperQ.Util`: shared primitives such as action spaces, rewards, hyperparameters, random state, and checkpoint interfaces.
- `src/HyperQ.Env`: environment contracts for single-action and multi-action training.
- `src/HyperQ.Learners`: Q learners, HyperQ learners, evaluators, and action selectors.
- `src/HyperQ.Training`: single-action PvE SARSA trainer, replay memory, Dyna support, and typed checkpoints.
- `src/HyperQ.MultiHead`: multi-head learners, scalarizers, evaluator, and trainer for vector rewards.
- `src/HyperQ.MACE`: multi-action collective trainer, evaluator, memory, and scalar aliases.
- `samples`: GridWorld, LEM, HuntTheWumpus, and WinForms visualizers.
- `tests`: focused regression tests for checkpoint support.

## Current Scope

The public release should be described as a source release for experimentation and extension. The core code builds and the samples run, but the API is not yet polished as a stable package contract. The docs call out the main limitations instead of hiding them.
