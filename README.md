Testbed Core is the foundational framework of the Testbed Suite — a modular environment for computational research, simulation, and experimental data analysis.
It provides the core infrastructure that other domain packages build on, including:
a unified experiment queue and run manager for reproducible testing;
a dynamic YAML-based equation and profile system for defining experiments;
integrated Python backend orchestration (automatic venv setup, dependency checks, and execution logging);
a visual WPF interface for parameter control, run tracking, and data aggregation;
and a growing symbol and equation catalog for consistent notation across projects.
Derived modules — such as Testbed_Lugon, Testbed_Quantum, or Testbed_GW — extend Core with domain-specific back-ends, equations, and visualizations.

Whether you’re modeling physical systems, testing algorithms, or managing reproducible simulations, Testbed Core provides the scaffolding: environment setup, job orchestration, results aggregation, and configuration persistence — so researchers can focus on the science, not the plumbing.

------------------          ------------------------             --------------------------                -------------------------              -------------------
Testbed Core is released under the Apache License 2.0.
That means you can use, modify, and redistribute the code—commercially or academically—so long as you:
include the copyright and license notice, and
state any significant changes you make.

You don’t have to open-source your own work that uses Testbed Core, but any copy of the framework you distribute must still carry this same license text.
The license also provides a standard patent grant and no warranty—use it at your own risk.
