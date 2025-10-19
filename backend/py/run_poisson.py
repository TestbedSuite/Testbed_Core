import argparse, time, os, sys, random, math

def main():
    parser = argparse.ArgumentParser(description="Dummy Poisson solver placeholder.")
    parser.add_argument("--grid", type=int, default=256)
    parser.add_argument("--steps", type=int, default=1000)
    parser.add_argument("--out", type=str, required=True)
    parser.add_argument("--seed", type=int, default=None)
    args = parser.parse_args()

    # Ensure output directory exists
    os.makedirs(args.out, exist_ok=True)
    log_path = os.path.join(args.out, "run.log")

    # Optional deterministic seeding
    if args.seed is not None:
        random.seed(args.seed)

    t0 = time.perf_counter()

    # Simulate work and write progress into run.log
    with open(log_path, "w") as f:
        # Do grid-dependent math work per progress tick (roughly O(grid^2))
        work = max(1, (args.grid * args.grid) // 8)
        acc = 0.0
        for i in range(0, args.steps, max(1, args.steps // 10)):
            s = 0.0
            base = (i + 1)
            for k in range(work):
                x = (base * (k + 1)) * 1e-4
                s += math.sin(x) * math.cos(x + 1e-3)
            acc += s  # carry to prevent optimization

            msg = f"[step {i}] grid={args.grid} work={work}"
            print(msg)
            f.write(msg + "\n")
            f.flush()

        elapsed = time.perf_counter() - t0
        print(f"elapsed_s={elapsed:.6f}")
        print(f"METRIC value={elapsed:.6f}")
        f.write(f"elapsed_s={elapsed:.6f}\n")
        f.write(f"METRIC value={elapsed:.6f}\n")

    return 0

if __name__ == "__main__":
    sys.exit(main())
