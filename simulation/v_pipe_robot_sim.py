"""First-round quasi-static simulation for a V-shaped in-pipe robot.

The model is intentionally conservative and transparent.  It evaluates the
side-view linkage geometry, torsion-spring wall preload, friction-limited
traction, motor limits, tether drag, and parameter uncertainty.  It is not a
replacement for a 3-D CAD interference check or a physical prototype test.
"""

from __future__ import annotations

import csv
import json
import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "results"
OUT.mkdir(parents=True, exist_ok=True)

# Mechanical geometry
PIPE_MIN = 0.140  # m, inner diameter
PIPE_MAX = 0.160  # m, inner diameter
WHEEL_RADIUS = 0.030  # m, 60 mm omni wheel
WHEEL_WIDTH = 0.028  # m, conservative placeholder until the purchased wheel is measured
LINK_LENGTH = 0.150  # m, wheel-axis to center-wheel-axis
BETA_FREE_DEG = 0.0  # equivalent spring free angle after anchor indexing
K_TOTAL_NM_PER_DEG = 0.029  # two mirrored springs together
BETA_STOP_MIN_DEG = 90.0
BETA_STOP_MAX_DEG = 125.0

# Robot and motor
MASS_NOMINAL = 1.60  # kg
MASS_LIMIT = 1.80  # kg
G = 9.81  # m/s^2
MOTOR_COUNT = 2
MOTOR_NO_LOAD_RPM = 110.0
MOTOR_RATED_RPM = 85.0
MOTOR_RATED_TORQUE = 3.0 * 0.0980665  # kgf.cm -> N.m
MOTOR_STALL_TORQUE = 12.0 * 0.0980665  # kgf.cm -> N.m
MOTOR_NO_LOAD_CURRENT = 0.15  # A
MOTOR_RATED_CURRENT = 0.65  # A
MOTOR_STALL_CURRENT = 2.40  # A
EXTERNAL_GEAR_RATIO = 40.0 / 25.0  # 25T motor -> 25T idler -> 40T wheel; idler cancels in ratio
GEAR_MESH_COUNT = 2
EXTERNAL_GEAR_EFFICIENCY = 0.90  # conservative total efficiency across both external meshes

# Tether and motion targets
TARGET_DISTANCE = 10.0  # m inside the pipe
TETHER_TOTAL_LENGTH = 15.0  # m
TETHER_LINEAR_MASS_NOMINAL = 0.080  # kg/m
TETHER_LINEAR_MASS_WORST = 0.120  # kg/m
TETHER_FRICTION_NOMINAL = 0.35
TETHER_FRICTION_WORST = 0.50
ROLLING_RESISTANCE_NOMINAL = 0.05
ROLLING_RESISTANCE_WORST = 0.08
TARGET_SPEED = 0.15  # m/s


def geometry(pipe_diameter: np.ndarray | float):
    d = np.asarray(pipe_diameter, dtype=float)
    pipe_radius = d / 2.0
    # A finite-width wheel cannot place its axis exactly D/2-r from the pipe
    # center because the tread edges must also stay inside the circular section.
    radial_center_offset = (
        np.sqrt(pipe_radius**2 - (WHEEL_WIDTH / 2.0) ** 2) - WHEEL_RADIUS
    )
    h = 2.0 * radial_center_offset
    if np.any(h >= LINK_LENGTH):
        raise ValueError("Link length is too short for the requested pipe diameter")
    half_wheelbase = np.sqrt(LINK_LENGTH**2 - h**2)
    beta_rad = 2.0 * np.arccos(h / LINK_LENGTH)
    beta_deg = np.degrees(beta_rad)
    wheelbase = 2.0 * half_wheelbase
    overall_length = wheelbase + 2.0 * WHEEL_RADIUS
    return h, half_wheelbase, beta_deg, wheelbase, overall_length


def contact_state(
    pipe_diameter: np.ndarray | float,
    k_total_nm_per_deg: np.ndarray | float = K_TOTAL_NM_PER_DEG,
    beta_free_deg: np.ndarray | float = BETA_FREE_DEG,
):
    _, a, beta_deg, _, _ = geometry(pipe_diameter)
    spring_deflection = np.maximum(beta_deg - beta_free_deg, 0.0)
    spring_moment = k_total_nm_per_deg * spring_deflection
    # Each link receives the same central spring moment.  Moment balance for
    # each link gives N_outer * a = M; the center wheel carries 2*M/a.
    center_normal = 2.0 * spring_moment / a
    return beta_deg, spring_moment, center_normal


def traction_limit(center_normal, mass, pipe_mu):
    # The two driven lower wheels together carry center preload plus weight.
    driven_normal_total = center_normal + mass * G
    friction_limit = pipe_mu * driven_normal_total
    motor_continuous_limit = (
        MOTOR_COUNT
        * MOTOR_RATED_TORQUE
        * EXTERNAL_GEAR_RATIO
        * EXTERNAL_GEAR_EFFICIENCY
        / WHEEL_RADIUS
    )
    return np.minimum(friction_limit, motor_continuous_limit), friction_limit


def tether_drag(distance, linear_mass, friction):
    return distance * linear_mass * G * friction


def resistance(distance, mass, linear_mass, tether_mu, rolling_coefficient):
    return tether_drag(distance, linear_mass, tether_mu) + rolling_coefficient * mass * G


diameters = np.linspace(PIPE_MIN, PIPE_MAX, 161)
h, half_base, beta_deg, wheelbase, overall_length = geometry(diameters)
_, spring_moment, center_normal = contact_state(diameters)

pipe_mus = np.array([0.15, 0.2, 0.3, 0.4, 0.5])
tractions = {}
for mu in pipe_mus:
    tractions[float(mu)] = traction_limit(center_normal, MASS_NOMINAL, mu)[0]

nominal_resistance_10m = resistance(
    TARGET_DISTANCE,
    MASS_NOMINAL,
    TETHER_LINEAR_MASS_NOMINAL,
    TETHER_FRICTION_NOMINAL,
    ROLLING_RESISTANCE_NOMINAL,
)
worst_resistance_10m = resistance(
    TARGET_DISTANCE,
    MASS_LIMIT,
    TETHER_LINEAR_MASS_WORST,
    TETHER_FRICTION_WORST,
    ROLLING_RESISTANCE_WORST,
)

motor_continuous_force = (
    MOTOR_COUNT
    * MOTOR_RATED_TORQUE
    * EXTERNAL_GEAR_RATIO
    * EXTERNAL_GEAR_EFFICIENCY
    / WHEEL_RADIUS
)
motor_stall_force = (
    MOTOR_COUNT
    * MOTOR_STALL_TORQUE
    * EXTERNAL_GEAR_RATIO
    * EXTERNAL_GEAR_EFFICIENCY
    / WHEEL_RADIUS
)
no_load_speed = (
    math.pi
    * (2.0 * WHEEL_RADIUS)
    * (MOTOR_NO_LOAD_RPM / EXTERNAL_GEAR_RATIO)
    / 60.0
)
rated_speed = (
    math.pi
    * (2.0 * WHEEL_RADIUS)
    * (MOTOR_RATED_RPM / EXTERNAL_GEAR_RATIO)
    / 60.0
)
target_wheel_rpm = TARGET_SPEED / (2.0 * math.pi * WHEEL_RADIUS) * 60.0
target_motor_rpm = target_wheel_rpm * EXTERNAL_GEAR_RATIO


# Monte Carlo robustness sweep at 10 m.
rng = np.random.default_rng(20261011)
n_mc = 200_000
mc_d = rng.uniform(PIPE_MIN, PIPE_MAX, n_mc)
mc_mass = rng.uniform(1.40, MASS_LIMIT, n_mc)
mc_pipe_mu = rng.uniform(0.15, 0.50, n_mc)
mc_k = K_TOTAL_NM_PER_DEG * rng.uniform(0.90, 1.10, n_mc)
mc_beta_free = rng.uniform(0.0, 5.0, n_mc)
mc_tether_mass = rng.uniform(0.05, TETHER_LINEAR_MASS_WORST, n_mc)
mc_tether_mu = rng.uniform(0.20, TETHER_FRICTION_WORST, n_mc)
mc_rr = rng.uniform(0.03, ROLLING_RESISTANCE_WORST, n_mc)
_, _, mc_center_normal = contact_state(mc_d, mc_k, mc_beta_free)
mc_traction, _ = traction_limit(mc_center_normal, mc_mass, mc_pipe_mu)
mc_resistance = resistance(
    TARGET_DISTANCE, mc_mass, mc_tether_mass, mc_tether_mu, mc_rr
)
mc_safety = mc_traction / mc_resistance
mc_margin = mc_traction - mc_resistance


def percentile(values, p):
    return float(np.percentile(values, p))


# Current estimate uses the torque required to overcome resistance, not stall.
def estimated_current(total_force):
    wheel_torque_each = total_force * WHEEL_RADIUS / MOTOR_COUNT
    torque_each = wheel_torque_each / (
        EXTERNAL_GEAR_RATIO * EXTERNAL_GEAR_EFFICIENCY
    )
    fraction = np.clip(torque_each / MOTOR_STALL_TORQUE, 0.0, 1.0)
    return MOTOR_NO_LOAD_CURRENT + fraction * (
        MOTOR_STALL_CURRENT - MOTOR_NO_LOAD_CURRENT
    )


summary = {
    "model_scope": "2-D symmetric quasi-static geometry, contact and tether-drag model",
    "design": {
        "pipe_inner_diameter_mm": [PIPE_MIN * 1000, PIPE_MAX * 1000],
        "wheel_diameter_mm": WHEEL_RADIUS * 2000,
        "wheel_width_placeholder_mm": WHEEL_WIDTH * 1000,
        "link_center_distance_mm": LINK_LENGTH * 1000,
        "equivalent_free_angle_deg": BETA_FREE_DEG,
        "combined_spring_stiffness_Nm_per_deg": K_TOTAL_NM_PER_DEG,
        "spring_count": 2,
        "each_spring_stiffness_Nm_per_deg": K_TOTAL_NM_PER_DEG / 2.0,
        "mechanical_angle_stops_deg": [BETA_STOP_MIN_DEG, BETA_STOP_MAX_DEG],
        "mass_nominal_kg": MASS_NOMINAL,
        "mass_limit_kg": MASS_LIMIT,
        "target_speed_m_per_s": TARGET_SPEED,
        "target_distance_m": TARGET_DISTANCE,
        "tether_total_length_m": TETHER_TOTAL_LENGTH,
    },
    "geometry_range": {
        "included_angle_deg": [float(beta_deg[-1]), float(beta_deg[0])],
        "wheelbase_mm": [float(wheelbase[-1] * 1000), float(wheelbase[0] * 1000)],
        "overall_length_mm": [
            float(overall_length[-1] * 1000),
            float(overall_length[0] * 1000),
        ],
        "spring_moment_Nm": [float(spring_moment[-1]), float(spring_moment[0])],
        "center_normal_force_N": [float(center_normal[-1]), float(center_normal[0])],
    },
    "motor": {
        "rated_torque_each_Nm": MOTOR_RATED_TORQUE,
        "stall_torque_each_Nm": MOTOR_STALL_TORQUE,
        "rated_current_each_A": MOTOR_RATED_CURRENT,
        "stall_current_each_A": MOTOR_STALL_CURRENT,
        "external_gear_ratio": EXTERNAL_GEAR_RATIO,
        "external_gear_efficiency_assumed": EXTERNAL_GEAR_EFFICIENCY,
        "motor_gear_teeth": 25,
        "idler_gear_teeth": 25,
        "wheel_gear_teeth": 40,
        "external_gear_mesh_count": GEAR_MESH_COUNT,
        "no_load_speed_m_per_s": no_load_speed,
        "rated_speed_m_per_s": rated_speed,
        "continuous_force_two_motors_N": motor_continuous_force,
        "stall_force_two_motors_N": motor_stall_force,
        "wheel_rpm_at_target_speed": target_wheel_rpm,
        "motor_rpm_at_target_speed": target_motor_rpm,
    },
    "ten_meter_load": {
        "nominal_resistance_N": nominal_resistance_10m,
        "worst_case_resistance_N": worst_resistance_10m,
        "nominal_current_each_A_estimate": float(
            estimated_current(nominal_resistance_10m)
        ),
        "worst_case_current_each_A_estimate": float(
            estimated_current(worst_resistance_10m)
        ),
        "minimum_traction_at_mu_0_15_N": float(np.min(tractions[0.15])),
        "nominal_minimum_safety_factor_at_mu_0_15": float(
            np.min(tractions[0.15]) / nominal_resistance_10m
        ),
        "worst_load_minimum_safety_factor_at_mu_0_15": float(
            np.min(tractions[0.15]) / worst_resistance_10m
        ),
    },
    "monte_carlo_200k": {
        "pass_fraction_margin_gt_0": float(np.mean(mc_margin > 0.0)),
        "fraction_safety_ge_1_2": float(np.mean(mc_safety >= 1.2)),
        "fraction_safety_ge_1_5": float(np.mean(mc_safety >= 1.5)),
        "safety_factor_p01": percentile(mc_safety, 1),
        "safety_factor_p05": percentile(mc_safety, 5),
        "safety_factor_median": percentile(mc_safety, 50),
        "safety_factor_p95": percentile(mc_safety, 95),
        "minimum_margin_N": float(np.min(mc_margin)),
    },
}

with (OUT / "summary.json").open("w", encoding="utf-8") as handle:
    json.dump(summary, handle, ensure_ascii=False, indent=2)

with (OUT / "diameter_sweep.csv").open("w", newline="", encoding="utf-8") as handle:
    writer = csv.writer(handle)
    writer.writerow(
        [
            "pipe_diameter_mm",
            "included_angle_deg",
            "wheelbase_mm",
            "overall_length_mm",
            "spring_moment_Nm",
            "center_normal_N",
            "traction_mu_0_15_N",
            "traction_mu_0_2_N",
            "traction_mu_0_3_N",
            "traction_mu_0_4_N",
            "traction_mu_0_5_N",
        ]
    )
    for i in range(len(diameters)):
        writer.writerow(
            [
                diameters[i] * 1000,
                beta_deg[i],
                wheelbase[i] * 1000,
                overall_length[i] * 1000,
                spring_moment[i],
                center_normal[i],
                tractions[0.15][i],
                tractions[0.2][i],
                tractions[0.3][i],
                tractions[0.4][i],
                tractions[0.5][i],
            ]
        )


plt.style.use("seaborn-v0_8-whitegrid")

# Figure 1: geometry and preload.
fig, axes = plt.subplots(2, 2, figsize=(11, 8), constrained_layout=True)
x_mm = diameters * 1000
axes[0, 0].plot(x_mm, beta_deg, linewidth=2.3)
axes[0, 0].axhspan(BETA_STOP_MIN_DEG, BETA_STOP_MAX_DEG, alpha=0.12, color="green")
axes[0, 0].set(title="V-joint angle", xlabel="Pipe inner diameter (mm)", ylabel="Included angle (deg)")
axes[0, 1].plot(x_mm, wheelbase * 1000, label="Axis-to-axis wheelbase", linewidth=2.3)
axes[0, 1].plot(x_mm, overall_length * 1000, label="Overall length incl. wheel radii", linewidth=2.3)
axes[0, 1].set(title="Robot length envelope", xlabel="Pipe inner diameter (mm)", ylabel="Length (mm)")
axes[0, 1].legend()
axes[1, 0].plot(x_mm, spring_moment, linewidth=2.3, color="#8b4c9f")
axes[1, 0].set(title="Combined spring moment", xlabel="Pipe inner diameter (mm)", ylabel="Moment (N m)")
axes[1, 1].plot(x_mm, center_normal, linewidth=2.3, color="#c74b50")
axes[1, 1].axhspan(40, 60, alpha=0.12, color="green", label="Design band")
axes[1, 1].set(title="Center-wheel wall preload", xlabel="Pipe inner diameter (mm)", ylabel="Normal force (N)")
axes[1, 1].legend()
fig.suptitle("V-shaped pipe robot: geometry and spring preload", fontsize=15)
fig.savefig(OUT / "geometry_and_preload.png", dpi=180)
plt.close(fig)


# Figure 2: side-view configurations.
fig, axes = plt.subplots(1, 3, figsize=(14, 4.5), constrained_layout=True)
for ax, d in zip(axes, [0.14, 0.15, 0.16]):
    _, a, bdeg, _, _ = geometry(d)
    h_here = d - 2 * WHEEL_RADIUS
    centers = np.array([[-a, 0.0], [0.0, h_here], [a, 0.0]])
    ax.plot(centers[:, 0] * 1000, centers[:, 1] * 1000, "k-", linewidth=6, solid_capstyle="round")
    for idx, (cx, cy) in enumerate(centers):
        circle = plt.Circle((cx * 1000, cy * 1000), WHEEL_RADIUS * 1000,
                            color="#2676b8" if idx != 1 else "#777777", alpha=0.85)
        ax.add_patch(circle)
    ax.axhline(-WHEEL_RADIUS * 1000, color="black", linewidth=2)
    ax.axhline((d - WHEEL_RADIUS) * 1000, color="black", linewidth=2)
    ax.set_aspect("equal")
    ax.set_xlim(-180, 180)
    ax.set_ylim(-50, 150)
    ax.set_title(f"ID {d*1000:.0f} mm | angle {float(bdeg):.1f} deg")
    ax.set_xlabel("Pipe-axis direction (mm)")
axes[0].set_ylabel("Radial direction (mm)")
fig.suptitle("Side-view kinematic envelope (blue: driven, gray: passive)", fontsize=15)
fig.savefig(OUT / "geometry_snapshots.png", dpi=180)
plt.close(fig)


# Figure 3: traction and tether load.
fig, axes = plt.subplots(1, 2, figsize=(12, 4.8), constrained_layout=True)
for mu in pipe_mus:
    axes[0].plot(x_mm, tractions[float(mu)], label=f"pipe mu={mu:.2f}", linewidth=2)
axes[0].axhline(motor_continuous_force, color="black", linestyle="--", label="motor continuous limit")
axes[0].axhline(nominal_resistance_10m, color="#2d8b57", linestyle=":", linewidth=2, label="10 m nominal resistance")
axes[0].axhline(worst_resistance_10m, color="#c74b50", linestyle=":", linewidth=2, label="10 m conservative resistance")
axes[0].set(title="Available traction", xlabel="Pipe inner diameter (mm)", ylabel="Force (N)")
axes[0].legend(fontsize=8)

distances = np.linspace(0, TARGET_DISTANCE, 101)
nominal_drag = resistance(
    distances,
    MASS_NOMINAL,
    TETHER_LINEAR_MASS_NOMINAL,
    TETHER_FRICTION_NOMINAL,
    ROLLING_RESISTANCE_NOMINAL,
)
worst_drag = resistance(
    distances,
    MASS_LIMIT,
    TETHER_LINEAR_MASS_WORST,
    TETHER_FRICTION_WORST,
    ROLLING_RESISTANCE_WORST,
)
min_traction_mu015 = float(np.min(tractions[0.15]))
axes[1].plot(distances, nominal_drag, label="nominal cable", linewidth=2)
axes[1].plot(distances, worst_drag, label="conservative cable", linewidth=2)
axes[1].axhline(min_traction_mu015, color="#c74b50", linestyle="--", label="minimum traction at pipe mu=0.15")
axes[1].set(title="Resistance versus inserted tether length", xlabel="Inserted tether length (m)", ylabel="Resistance (N)")
axes[1].legend(fontsize=8)
fig.suptitle("Traction margin for a straight horizontal pipe", fontsize=15)
fig.savefig(OUT / "traction_and_tether.png", dpi=180)
plt.close(fig)


# Figure 4: Monte Carlo robustness.
fig, axes = plt.subplots(1, 2, figsize=(12, 4.8), constrained_layout=True)
hist_max = min(20.0, float(np.percentile(mc_safety, 99.5)))
axes[0].hist(mc_safety, bins=np.linspace(0, hist_max, 120), color="#2676b8", alpha=0.9)
axes[0].axvline(1.0, color="#c74b50", linestyle="--", label="zero-margin boundary")
axes[0].axvline(1.2, color="#d89026", linestyle="--", label="1.2 design factor")
axes[0].set(title="Monte Carlo safety factor", xlabel="Traction / resistance", ylabel="Samples")
axes[0].legend()
axes[1].scatter(mc_resistance[::200], mc_traction[::200], s=6, alpha=0.25)
limit = max(float(np.max(mc_resistance[::200])), float(np.max(mc_traction[::200])))
axes[1].plot([0, limit], [0, limit], "--", color="#c74b50")
axes[1].set(title="Traction versus required resistance", xlabel="Resistance (N)", ylabel="Available traction (N)")
fig.suptitle("200,000-case uncertainty sweep at 10 m", fontsize=15)
fig.savefig(OUT / "monte_carlo_robustness.png", dpi=180)
plt.close(fig)


# Reproducible pass/fail gates for this first-round design.
assert float(np.min(beta_deg)) > BETA_STOP_MIN_DEG
assert float(np.max(beta_deg)) < BETA_STOP_MAX_DEG
assert float(np.min(center_normal)) >= 40.0
assert float(np.max(center_normal)) <= 60.0
assert float(np.min(tractions[0.15])) > worst_resistance_10m
assert float(np.min(mc_margin)) > 0.0


print(json.dumps(summary, ensure_ascii=False, indent=2))
