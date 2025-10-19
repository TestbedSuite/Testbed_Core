import argparse
import json
from pathlib import Path
from lugon_test.core import run_test
from lugon_test.io_utils import ensure_dir
from typing import Optional

def parse_args():
    p = argparse.ArgumentParser(description='Lugon Framework — Test Harness')
    p.add_argument('--config', type=str, default=None, help='Path to JSON config (overrides defaults).')
    p.add_argument('--out', type=str, default='./runs/default', help='Output directory for logs/plots.')
    p.add_argument('--seed', type=int, default=12345, help='Random seed for reproducibility.')
    p.add_argument('--dry-run', action='store_true', help='Build everything, but skip the heavy work.')
    return p.parse_args()

def load_config(path: Optional[str]) -> dict:
    base = {'sample_rate_hz': 1000, 'duration_s': 5.0, 'noise_floor': 1e-3, 'model': 'toy_relaxation'}
    if not path:
        return base
    with open(path, 'r', encoding='utf-8') as f:
        base.update(json.load(f))
    return base

def main():
    args = parse_args()
    cfg = load_config(args.config)
    out_dir = Path(args.out)
    ensure_dir(out_dir)
    run_test(cfg=cfg, out_dir=out_dir, seed=args.seed, dry_run=args.dry_run)

if __name__ == '__main__':
    main()
