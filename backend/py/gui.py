import tkinter as tk
from tkinter import filedialog, messagebox
from pathlib import Path
import json
from lugon_test.core import run_test
from lugon_test.io_utils import ensure_dir

def run_clicked():
    out_dir = Path(out_var.get() or "./runs/gui_run")
    ensure_dir(out_dir)
    cfg = {
        "sample_rate_hz": int(sr_var.get()),
        "duration_s": float(dur_var.get()),
        "noise_floor": float(noise_var.get()),
        "model": model_var.get(),
    }
    try:
        run_test(cfg=cfg, out_dir=out_dir, seed=int(seed_var.get()), dry_run=dry_var.get())
        messagebox.showinfo("Done", f"Wrote outputs to: {out_dir}")
    except Exception as e:
        messagebox.showerror("Error", str(e))

def browse_out():
    p = filedialog.askdirectory()
    if p:
        out_var.set(p)

root = tk.Tk()
root.title("Lugon Testbed")

# Vars
sr_var   = tk.StringVar(value="1000")
dur_var  = tk.StringVar(value="5.0")
noise_var= tk.StringVar(value="0.001")
seed_var = tk.StringVar(value="12345")
model_var= tk.StringVar(value="toy_relaxation")
out_var  = tk.StringVar(value="./runs/gui_run")
dry_var  = tk.BooleanVar(value=False)

# Layout
row = 0
for label, var in [("Sample rate (Hz)", sr_var),
                   ("Duration (s)", dur_var),
                   ("Noise floor", noise_var),
                   ("Seed", seed_var)]:
    tk.Label(root, text=label, width=18, anchor="w").grid(row=row, column=0, padx=8, pady=4)
    tk.Entry(root, textvariable=var, width=24).grid(row=row, column=1, padx=8, pady=4)
    row += 1

tk.Label(root, text="Model", width=18, anchor="w").grid(row=row, column=0, padx=8, pady=4)
tk.OptionMenu(root, model_var, "toy_relaxation", "coherence_probe").grid(row=row, column=1, padx=8, pady=4, sticky="we")
row += 1

tk.Label(root, text="Output folder", width=18, anchor="w").grid(row=row, column=0, padx=8, pady=4)
tk.Entry(root, textvariable=out_var, width=24).grid(row=row, column=1, padx=8, pady=4, sticky="we")
tk.Button(root, text="Browse…", command=browse_out).grid(row=row, column=2, padx=8, pady=4)
row += 1

tk.Checkbutton(root, text="Dry run", variable=dry_var).grid(row=row, column=1, sticky="w", padx=8, pady=4)
row += 1

tk.Button(root, text="Run", command=run_clicked).grid(row=row, column=1, padx=8, pady=12)
row += 1

root.mainloop()
