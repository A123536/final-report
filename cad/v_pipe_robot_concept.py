"""Generate a conceptual 3-D assembly for the 140-160 mm V-pipe robot.

Outputs a colored GLB assembly and annotated PNG views.  The model is an
engineering packaging model, not a manufacturing-ready tolerance drawing.
"""

from __future__ import annotations

import json
import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np
import trimesh
from matplotlib.lines import Line2D
from matplotlib.patches import Circle, FancyArrowPatch, Patch, Rectangle
from mpl_toolkits.mplot3d import art3d


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "outputs"
OUT.mkdir(parents=True, exist_ok=True)

# Nominal packaging geometry, all units in millimetres.
PIPE_ID = 150.0
PIPE_RADIUS = PIPE_ID / 2.0
WHEEL_DIAMETER = 60.0
WHEEL_RADIUS = WHEEL_DIAMETER / 2.0
WHEEL_WIDTH = 28.0
LINK_LENGTH = 150.0
RADIAL_CENTER_OFFSET = (
    math.sqrt(PIPE_RADIUS**2 - (WHEEL_WIDTH / 2.0) ** 2) - WHEEL_RADIUS
)
RADIAL_SPAN = 2.0 * RADIAL_CENTER_OFFSET
HALF_WHEELBASE = math.sqrt(LINK_LENGTH**2 - RADIAL_SPAN**2)
V_ANGLE_DEG = math.degrees(2.0 * math.acos(RADIAL_SPAN / LINK_LENGTH))
ACTIVE_Z = -RADIAL_CENTER_OFFSET
PASSIVE_Z = RADIAL_CENTER_OFFSET

REAR_WHEEL = np.array([-HALF_WHEELBASE, 0.0, ACTIVE_Z])
CENTER_WHEEL = np.array([0.0, 0.0, PASSIVE_Z])
FRONT_WHEEL = np.array([HALF_WHEELBASE, 0.0, ACTIVE_Z])

PIPE_LENGTH = 430.0
PIPE_CENTER_X = 25.0

COLORS = {
    "drive_wheel": (38, 118, 184, 255),
    "passive_wheel": (110, 110, 110, 255),
    "roller": (240, 190, 40, 255),
    "rail": (60, 65, 72, 255),
    "housing": (170, 180, 190, 95),
    "motor": (175, 180, 185, 255),
    "encoder": (35, 35, 40, 255),
    "gear": (234, 126, 45, 255),
    "shaft": (90, 95, 100, 255),
    "spring": (160, 75, 180, 255),
    "driver": (205, 55, 60, 255),
    "controller": (45, 160, 90, 255),
    "power": (55, 120, 205, 255),
    "interface": (55, 190, 190, 255),
    "camera": (145, 80, 190, 120),
    "power_wire": (220, 45, 45, 255),
    "signal_wire": (40, 175, 95, 255),
    "pipe": (120, 175, 210, 28),
    "fastener": (40, 40, 45, 255),
}


scene = trimesh.Scene()
render_items: list[tuple[trimesh.Trimesh, tuple[int, int, int, int], str]] = []


def add_mesh(name: str, mesh: trimesh.Trimesh, color, group: str | None = None):
    rgba = np.array(color, dtype=np.uint8)
    mesh.visual.vertex_colors = np.tile(rgba, (len(mesh.vertices), 1))
    mesh.metadata["component"] = group or name
    scene.add_geometry(mesh, node_name=name, geom_name=name)
    render_items.append((mesh, tuple(int(x) for x in rgba), group or name))
    return mesh


def translated(mesh: trimesh.Trimesh, center):
    mesh.apply_translation(np.asarray(center, dtype=float))
    return mesh


def box_along(center, length, width, height, direction, color, name, group=None):
    mesh = trimesh.creation.box(extents=[length, width, height])
    direction = np.asarray(direction, dtype=float)
    direction /= np.linalg.norm(direction)
    transform = trimesh.geometry.align_vectors([1.0, 0.0, 0.0], direction)
    mesh.apply_transform(transform)
    mesh.apply_translation(np.asarray(center, dtype=float))
    return add_mesh(name, mesh, color, group)


def cylinder_between(p0, p1, radius, color, name, sections=28, group=None):
    p0 = np.asarray(p0, dtype=float)
    p1 = np.asarray(p1, dtype=float)
    vector = p1 - p0
    length = float(np.linalg.norm(vector))
    mesh = trimesh.creation.cylinder(radius=radius, height=length, sections=sections)
    transform = trimesh.geometry.align_vectors([0.0, 0.0, 1.0], vector / length)
    mesh.apply_transform(transform)
    mesh.apply_translation((p0 + p1) / 2.0)
    return add_mesh(name, mesh, color, group)


def cylinder_y(center, radius, width, color, name, sections=48, group=None):
    center = np.asarray(center, dtype=float)
    return cylinder_between(
        center + np.array([0.0, -width / 2.0, 0.0]),
        center + np.array([0.0, width / 2.0, 0.0]),
        radius,
        color,
        name,
        sections,
        group,
    )


def omni_wheel(center, name, active=True):
    color = COLORS["drive_wheel"] if active else COLORS["passive_wheel"]
    cylinder_y(center, WHEEL_RADIUS, WHEEL_WIDTH, color, f"{name}_body", 64, name)
    cylinder_y(center, 9.0, WHEEL_WIDTH + 8.0, COLORS["shaft"], f"{name}_hub", 36, name)
    # Stylized rollers identify the wheels as omni wheels without overloading the mesh.
    for idx, theta in enumerate(np.linspace(0.0, 2.0 * math.pi, 12, endpoint=False)):
        roller_center = np.asarray(center) + np.array(
            [
                (WHEEL_RADIUS - 5.0) * math.cos(theta),
                (-1 if idx % 2 else 1) * (WHEEL_WIDTH / 2.0 - 3.2),
                (WHEEL_RADIUS - 5.0) * math.sin(theta),
            ]
        )
        roller = trimesh.creation.icosphere(subdivisions=1, radius=3.0)
        roller.apply_translation(roller_center)
        add_mesh(f"{name}_roller_{idx:02d}", roller, COLORS["roller"], name)


def pipe_surface(radius=PIPE_RADIUS, length=PIPE_LENGTH, center_x=PIPE_CENTER_X):
    sections = 96
    angles = np.linspace(0.0, 2.0 * math.pi, sections, endpoint=False)
    x0, x1 = center_x - length / 2.0, center_x + length / 2.0
    vertices = []
    for x in (x0, x1):
        for angle in angles:
            vertices.append([x, radius * math.cos(angle), radius * math.sin(angle)])
    faces = []
    for i in range(sections):
        j = (i + 1) % sections
        faces.append([i, j, sections + j])
        faces.append([i, sections + j, sections + i])
    mesh = trimesh.Trimesh(vertices=np.array(vertices), faces=np.array(faces), process=False)
    return add_mesh("pipe_ID150", mesh, COLORS["pipe"], "transparent_pipe")


def motor_and_drive(active_center, toward_center, side, name):
    u = np.asarray(toward_center, dtype=float)
    u /= np.linalg.norm(u)
    motor_axis_center = np.asarray(active_center) + u * 32.5
    gear_y = -side * 20.0

    # Independent wheel shaft and support bearings.
    cylinder_y(active_center, 4.0, 74.0, COLORS["shaft"], f"{name}_wheel_shaft", 32, name)
    for y in (-23.0, 23.0):
        cylinder_y(np.asarray(active_center) + np.array([0.0, y, 0.0]), 11.0, 7.0,
                   COLORS["fastener"], f"{name}_bearing_{'L' if y < 0 else 'R'}", 32, name)

    # 40-tooth wheel gear and 25-tooth motor gear, shown as pitch-envelope disks.
    cylinder_y(np.asarray(active_center) + np.array([0.0, gear_y, 0.0]), 21.0, 7.0,
               COLORS["gear"], f"{name}_gear_40T", 48, name)
    cylinder_y(motor_axis_center + np.array([0.0, gear_y, 0.0]), 13.5, 7.0,
               COLORS["gear"], f"{name}_gear_25T", 40, name)

    # JGB37-520 envelope, mirrored left/right to balance roll moment.
    gearbox_center_y = gear_y + side * 15.0
    motor_center_y = gear_y + side * (24.0 + 21.7 / 2.0)
    encoder_center_y = gear_y + side * (24.0 + 21.7 + 13.8 / 2.0)
    cylinder_y(motor_axis_center + np.array([0.0, gearbox_center_y, 0.0]), 18.5, 24.0,
               COLORS["motor"], f"{name}_gearbox", 48, name)
    cylinder_y(motor_axis_center + np.array([0.0, motor_center_y, 0.0]), 16.35, 21.7,
               COLORS["motor"], f"{name}_motor", 48, name)
    cylinder_y(motor_axis_center + np.array([0.0, encoder_center_y, 0.0]), 16.9, 13.8,
               COLORS["encoder"], f"{name}_encoder", 48, name)
    return motor_axis_center


def pod_and_board(active, center, name, board_specs):
    vector = np.asarray(center) - np.asarray(active)
    u = vector / np.linalg.norm(vector)
    normal = np.array([-u[2], 0.0, u[0]])
    pod_center = np.asarray(active) + 0.53 * vector + normal * 1.0
    box_along(pod_center, 92.0, 64.0, 30.0, u, COLORS["housing"], f"{name}_cover", name)
    for board_name, along_offset, y_offset, length, width, height, color in board_specs:
        board_center = pod_center + u * along_offset + np.array([0.0, y_offset, 0.0])
        box_along(board_center, length, width, height, u, color,
                  f"{name}_{board_name}", board_name)
        # Four generic M3 mounting bosses; final hole pattern uses the purchased module.
        for sx in (-1, 1):
            for sy in (-1, 1):
                boss_center = board_center + u * (sx * (length / 2.0 - 5.0)) + np.array(
                    [0.0, sy * (width / 2.0 - 4.0), 0.0]
                )
                cylinder_between(
                    boss_center - normal * (height / 2.0 + 2.0),
                    boss_center + normal * (height / 2.0 + 2.0),
                    1.6,
                    COLORS["fastener"],
                    f"{name}_{board_name}_boss_{sx}_{sy}",
                    16,
                    board_name,
                )
    return pod_center, u, normal


def torus_y(center, major_radius, minor_radius, color, name, group=None):
    mesh = trimesh.creation.torus(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_sections=48,
        minor_sections=12,
    )
    transform = trimesh.geometry.align_vectors([0.0, 0.0, 1.0], [0.0, 1.0, 0.0])
    mesh.apply_transform(transform)
    mesh.apply_translation(np.asarray(center))
    return add_mesh(name, mesh, color, group)


# Transparent nominal pipe first.
pipe_surface()

# Wheels.
omni_wheel(REAR_WHEEL, "rear_active_wheel", True)
omni_wheel(CENTER_WHEEL, "center_passive_wheel", False)
omni_wheel(FRONT_WHEEL, "front_active_wheel", True)

# Double side rails on each link.
for link_name, p0, p1 in (
    ("rear_link", REAR_WHEEL, CENTER_WHEEL),
    ("front_link", CENTER_WHEEL, FRONT_WHEEL),
):
    direction = p1 - p0
    u = direction / np.linalg.norm(direction)
    center = (p0 + p1) / 2.0
    for y in (-35.0, 35.0):
        box_along(center + np.array([0.0, y, 0.0]), 122.0, 7.0, 16.0, u,
                  COLORS["rail"], f"{link_name}_rail_{'L' if y < 0 else 'R'}", link_name)

# Center axle, passive wheel bearings, mirrored torsion springs and stop tabs.
cylinder_y(CENTER_WHEEL, 5.0, 92.0, COLORS["shaft"], "center_hinge_axle", 36, "center_joint")
for y in (-28.0, 28.0):
    cylinder_y(CENTER_WHEEL + np.array([0.0, y, 0.0]), 11.0, 7.0,
               COLORS["fastener"], f"center_bearing_{y:+.0f}", 32, "center_joint")
    torus_y(CENTER_WHEEL + np.array([0.0, y, 0.0]), 16.0, 3.2,
            COLORS["spring"], f"torsion_spring_{y:+.0f}", "center_joint")
for xoff in (-18.0, 18.0):
    translated(add_mesh(
        f"center_stop_tab_{xoff:+.0f}",
        trimesh.creation.box(extents=[12.0, 9.0, 18.0]),
        COLORS["fastener"],
        "center_joint",
    ), CENTER_WHEEL + np.array([xoff, 0.0, -18.0]))
cylinder_between(CENTER_WHEEL + np.array([-15.0, -42.0, -13.0]),
                 CENTER_WHEEL + np.array([-15.0, 42.0, -13.0]),
                 2.0, COLORS["fastener"], "insertion_lock_pin", 20, "center_joint")

# Drive modules; front and rear motor bodies face opposite sides of the pipe.
rear_motor_center = motor_and_drive(REAR_WHEEL, CENTER_WHEEL - REAR_WHEEL, -1, "rear_drive")
front_motor_center = motor_and_drive(FRONT_WHEEL, CENTER_WHEEL - FRONT_WHEEL, 1, "front_drive")

# Equipment bays.  Board solids are reserved envelopes with universal mounting bosses.
rear_pod_center, rear_u, rear_n = pod_and_board(
    REAR_WHEEL,
    CENTER_WHEEL,
    "rear_power_bay",
    [
        ("24V_to_12V_buck_65x42", -7.0, 0.0, 65.0, 42.0, 17.0, COLORS["power"]),
        ("fuse_terminal_24x16", 32.0, 0.0, 24.0, 16.0, 12.0, COLORS["interface"]),
    ],
)
front_pod_center, front_u, front_n = pod_and_board(
    FRONT_WHEEL,
    CENTER_WHEEL,
    "front_control_bay",
    [
        ("dual_MC33926_50x30", -14.0, -16.0, 50.0, 30.0, 12.0, COLORS["driver"]),
        ("STM32F411_53x22", 13.0, 18.0, 53.0, 22.0, 10.0, COLORS["controller"]),
        ("RS485_25x18", 33.0, -18.0, 25.0, 18.0, 9.0, COLORS["interface"]),
    ],
)

# Rear tether entry and strain relief.
tether_entry = np.array([-171.0, 0.0, -12.0])
cylinder_between(tether_entry + np.array([-15.0, 0.0, 0.0]), tether_entry,
                 7.0, COLORS["fastener"], "M12_cable_gland", 32, "tether_entry")
cylinder_between(tether_entry + np.array([-30.0, 0.0, 0.0]), tether_entry,
                 3.5, COLORS["power_wire"], "external_tether", 24, "tether_entry")

# Reserved camera bay in front of the leading drive wheel.
camera_center = np.array([175.0, 0.0, 0.0])
camera_box = trimesh.creation.box(extents=[50.0, 48.0, 32.0])
camera_box.apply_translation(camera_center)
add_mesh("future_camera_bay_50x48x32", camera_box, COLORS["camera"], "future_camera")
cylinder_between(camera_center + np.array([25.0, 0.0, 0.0]),
                 camera_center + np.array([32.0, 0.0, 0.0]),
                 9.0, COLORS["encoder"], "camera_lens_port", 36, "future_camera")
for y in (-18.0, 18.0):
    for z in (-10.0, 10.0):
        cylinder_between(
            camera_center + np.array([-22.0, y, z]),
            camera_center + np.array([-15.0, y, z]),
            1.6,
            COLORS["fastener"],
            f"camera_M3_mount_{y:+.0f}_{z:+.0f}",
            16,
            "future_camera",
        )

# Power and signal harnesses.  Two separated routes reduce encoder noise.
power_points = [
    tether_entry,
    rear_pod_center + np.array([0.0, -18.0, 0.0]),
    CENTER_WHEEL + np.array([0.0, -18.0, -21.0]),
    front_pod_center + np.array([0.0, -18.0, 0.0]),
    camera_center + np.array([-25.0, -18.0, 0.0]),
]
signal_points = [
    tether_entry + np.array([0.0, 10.0, 0.0]),
    rear_pod_center + np.array([0.0, 18.0, 0.0]),
    CENTER_WHEEL + np.array([0.0, 18.0, -12.0]),
    front_pod_center + np.array([0.0, 18.0, 0.0]),
    camera_center + np.array([-25.0, 18.0, 0.0]),
]
for idx, (p0, p1) in enumerate(zip(power_points[:-1], power_points[1:])):
    cylinder_between(p0, p1, 2.2, COLORS["power_wire"], f"power_harness_{idx}", 18, "power_harness")
for idx, (p0, p1) in enumerate(zip(signal_points[:-1], signal_points[1:])):
    cylinder_between(p0, p1, 1.6, COLORS["signal_wire"], f"signal_harness_{idx}", 18, "signal_harness")

# Simple tie-down bridges on both links.
for idx, point in enumerate((
    rear_pod_center - rear_u * 38.0,
    rear_pod_center + rear_u * 38.0,
    front_pod_center - front_u * 38.0,
    front_pod_center + front_u * 38.0,
)):
    mesh = trimesh.creation.box(extents=[10.0, 76.0, 5.0])
    # Align bridge's local x with the nearest link direction.
    direction = rear_u if idx < 2 else front_u
    mesh.apply_transform(trimesh.geometry.align_vectors([1.0, 0.0, 0.0], direction))
    mesh.apply_translation(point)
    add_mesh(f"cable_tie_bridge_{idx}", mesh, COLORS["fastener"], "cable_management")


# Nominal 150 mm pipe radial-envelope check.  The transparent pipe itself is
# excluded; every robot vertex must remain inside the nominal inner radius.
component_radial_max = {}
for mesh, _, group in render_items:
    if group == "transparent_pipe":
        continue
    radial = np.sqrt(mesh.vertices[:, 1] ** 2 + mesh.vertices[:, 2] ** 2)
    component_radial_max[group] = max(
        component_radial_max.get(group, 0.0), float(np.max(radial))
    )
max_robot_radius = max(component_radial_max.values())
assert max_robot_radius <= PIPE_RADIUS + 0.5, (
    f"Nominal pipe interference: robot radius {max_robot_radius:.2f} mm "
    f"> pipe radius {PIPE_RADIUS:.2f} mm"
)


# Export a colored assembly that can be rotated in common 3-D viewers.
glb_path = OUT / "v_pipe_robot_concept.glb"
glb_path.write_bytes(scene.export(file_type="glb"))


def transformed_triangles(mesh: trimesh.Trimesh):
    return mesh.triangles


def render_3d(filename, elev, azim, title, show_pipe=True):
    fig = plt.figure(figsize=(14, 9))
    ax = fig.add_subplot(111, projection="3d")
    for mesh, rgba, group in render_items:
        if group == "transparent_pipe" and not show_pipe:
            continue
        triangles = transformed_triangles(mesh)
        alpha = rgba[3] / 255.0
        collection = art3d.Poly3DCollection(
            triangles,
            facecolor=np.array(rgba[:3]) / 255.0,
            edgecolor="none",
            linewidths=0.0,
            alpha=alpha,
        )
        ax.add_collection3d(collection)
    ax.set_xlim(-205, 220)
    ax.set_ylim(-90, 90)
    ax.set_zlim(-85, 85)
    ax.set_box_aspect((425, 180, 170))
    ax.view_init(elev=elev, azim=azim)
    ax.set_xlabel("Pipe axis X (mm)")
    ax.set_ylabel("Transverse Y (mm)")
    ax.set_zlabel("Radial Z (mm)")
    ax.set_title(title, fontsize=16, pad=18)
    legend_items = [
        Patch(color=np.array(COLORS["drive_wheel"][:3]) / 255.0, label="Driven omni wheels"),
        Patch(color=np.array(COLORS["passive_wheel"][:3]) / 255.0, label="Passive center wheel"),
        Patch(color=np.array(COLORS["power"][:3]) / 255.0, label="Power conversion bay"),
        Patch(color=np.array(COLORS["driver"][:3]) / 255.0, label="Motor driver"),
        Patch(color=np.array(COLORS["controller"][:3]) / 255.0, label="Controller"),
        Patch(color=np.array(COLORS["camera"][:3]) / 255.0, label="Reserved camera bay"),
        Line2D([0], [0], color=np.array(COLORS["power_wire"][:3]) / 255.0, lw=4, label="Power harness"),
        Line2D([0], [0], color=np.array(COLORS["signal_wire"][:3]) / 255.0, lw=4, label="Signal harness"),
    ]
    ax.legend(handles=legend_items, loc="upper left", fontsize=9)
    fig.savefig(OUT / filename, dpi=190, bbox_inches="tight")
    plt.close(fig)


render_3d("assembly_isometric.png", 21, -58,
          "V-shaped pipe robot packaging model - nominal 150 mm pipe")
render_3d("assembly_opposite.png", 24, 128,
          "Opposite view - mirrored motors and center spring joint")
render_3d("equipment_and_wiring.png", 54, -72,
          "Equipment bays and separated power/signal harness routes", show_pipe=False)


# Annotated side packaging drawing.
fig, ax = plt.subplots(figsize=(15, 8.5))
ax.set_aspect("equal")
ax.axhline(-PIPE_RADIUS, color="black", lw=2)
ax.axhline(PIPE_RADIUS, color="black", lw=2)
for center, color in (
    (REAR_WHEEL, "#2676b8"),
    (CENTER_WHEEL, "#777777"),
    (FRONT_WHEEL, "#2676b8"),
):
    ax.add_patch(Circle((center[0], center[2]), WHEEL_RADIUS, facecolor=color, alpha=0.85))
ax.plot([REAR_WHEEL[0], CENTER_WHEEL[0], FRONT_WHEEL[0]],
        [REAR_WHEEL[2], CENTER_WHEEL[2], FRONT_WHEEL[2]], "k-", lw=7, solid_capstyle="round")

def add_rotated_rect(center, length, height, angle_deg, color, alpha):
    rect = Rectangle((center[0] - length / 2, center[2] - height / 2), length, height,
                     angle=angle_deg, rotation_point="center", facecolor=color,
                     edgecolor="black", alpha=alpha)
    ax.add_patch(rect)

rear_angle = math.degrees(math.atan2((CENTER_WHEEL - REAR_WHEEL)[2], (CENTER_WHEEL - REAR_WHEEL)[0]))
front_angle = math.degrees(math.atan2((FRONT_WHEEL - CENTER_WHEEL)[2], (FRONT_WHEEL - CENTER_WHEEL)[0]))
add_rotated_rect(rear_pod_center, 92, 30, rear_angle, "#7ca8d8", 0.45)
add_rotated_rect(front_pod_center, 92, 30, front_angle, "#82c995", 0.45)
camera_rect = Rectangle((150, -16), 50, 32, facecolor="#a66bc2", edgecolor="black", alpha=0.35)
ax.add_patch(camera_rect)
ax.text(175, 0, "Future camera bay\n50 x 48 x 32 mm", ha="center", va="center", fontsize=9)

ax.plot([p[0] for p in power_points], [p[2] for p in power_points], color="#dc2d2d", lw=3, label="Power cable channel")
ax.plot([p[0] for p in signal_points], [p[2] for p in signal_points], color="#28af60", lw=3, label="Encoder/RS-485 channel")

# Component callouts are placed outside the assembly to avoid label overlap.
callouts = [
    ("Rear driven omni wheel", REAR_WHEEL[[0, 2]], (-180, -100)),
    ("Center passive wheel\nTwin torsion springs", CENTER_WHEEL[[0, 2]], (-72, 104)),
    ("Front driven omni wheel", FRONT_WHEEL[[0, 2]], (105, -100)),
    ("Power bay\n24V->12V buck + fuse", rear_pod_center[[0, 2]], (-120, 56)),
    ("Control bay\nMC33926 + STM32 + RS-485", front_pod_center[[0, 2]], (74, 57)),
    ("M12 gland + strain relief", tether_entry[[0, 2]], (-215, 34)),
    ("25T/40T module-1 gears\nIndependent 8 mm wheel shaft", FRONT_WHEEL[[0, 2]], (155, -83)),
]
for text_value, target, text_pos in callouts:
    ax.annotate(text_value, xy=target, xytext=text_pos,
                arrowprops=dict(arrowstyle="->", lw=1.2), fontsize=9,
                ha="center", va="center")

# Link center distance dimension, offset normal to the rear link.
rear_vec = CENTER_WHEEL[[0, 2]] - REAR_WHEEL[[0, 2]]
rear_unit = rear_vec / np.linalg.norm(rear_vec)
rear_normal_2d = np.array([-rear_unit[1], rear_unit[0]])
dim_offset = rear_normal_2d * 22.0
dim_start = REAR_WHEEL[[0, 2]] + dim_offset
dim_end = CENTER_WHEEL[[0, 2]] + dim_offset
ax.annotate("", xy=dim_start, xytext=dim_end,
            arrowprops=dict(arrowstyle="<->", color="black"))
dim_mid = (dim_start + dim_end) / 2.0
ax.text(dim_mid[0], dim_mid[1] + 5.0, "150 mm link center distance",
        rotation=rear_angle, ha="center", va="center", fontsize=9)

ax.annotate("", xy=(-194, -PIPE_RADIUS), xytext=(-194, PIPE_RADIUS),
            arrowprops=dict(arrowstyle="<->", color="black"))
ax.text(-201, 0, "150 mm pipe ID", rotation=90, ha="center", va="center")
ax.text(0, 89, f"Nominal V angle {V_ANGLE_DEG:.1f} deg", ha="center", fontsize=10)

ax.set_xlim(-225, 225)
ax.set_ylim(-112, 116)
ax.set_xlabel("Pipe-axis direction X (mm)")
ax.set_ylabel("Radial direction Z (mm)")
ax.set_title("Side packaging drawing and reserved equipment locations", fontsize=15)
ax.grid(True, alpha=0.3)
ax.legend(loc="upper right")
fig.savefig(OUT / "annotated_side_layout.png", dpi=190, bbox_inches="tight")
plt.close(fig)


# Cross-sectional clearance check at all three target diameters.
fig, axes = plt.subplots(1, 3, figsize=(14, 5))
clearance_results = {}
for ax, diameter in zip(axes, (140.0, 150.0, 160.0)):
    radius = diameter / 2.0
    offset = math.sqrt(radius**2 - (WHEEL_WIDTH / 2.0) ** 2) - WHEEL_RADIUS
    h_here = 2.0 * offset
    a_here = math.sqrt(LINK_LENGTH**2 - h_here**2)
    u_z = h_here / LINK_LENGTH
    motor_z = -offset + 32.5 * u_z
    pipe_circle = Circle((0, 0), radius, fill=False, color="black", lw=2)
    ax.add_patch(pipe_circle)
    wheel_rect = Rectangle((-WHEEL_WIDTH / 2.0, -offset - WHEEL_RADIUS),
                           WHEEL_WIDTH, 2.0 * WHEEL_RADIUS,
                           facecolor="#2676b8", alpha=0.75, label="Wheel envelope")
    motor_rect = Rectangle((-40.0, motor_z - 18.5), 80.0, 37.0,
                           facecolor="#b0b4b8", alpha=0.65, label="Motor envelope")
    pod_rect = Rectangle((-32.0, -15.0), 64.0, 30.0,
                         facecolor="#7ca8d8", alpha=0.35, label="Electronics bay")
    camera_rect_cs = Rectangle((-24.0, -16.0), 48.0, 32.0,
                               facecolor="#a66bc2", alpha=0.25, label="Camera bay")
    for patch in (wheel_rect, motor_rect, pod_rect, camera_rect_cs):
        ax.add_patch(patch)
    wheel_corner_radius = math.hypot(WHEEL_WIDTH / 2.0, offset + WHEEL_RADIUS)
    motor_corner_radius = max(
        math.hypot(y, z)
        for y in (-40.0, 40.0)
        for z in (motor_z - 18.5, motor_z + 18.5)
    )
    clearance_results[str(int(diameter))] = {
        "wheel_edge_clearance_mm": radius - wheel_corner_radius,
        "motor_box_clearance_mm": radius - motor_corner_radius,
        "half_wheelbase_mm": a_here,
    }
    ax.set_aspect("equal")
    ax.set_xlim(-85, 85)
    ax.set_ylim(-85, 85)
    ax.set_title(
        f"Pipe ID {diameter:.0f} mm\nMotor box clearance {radius - motor_corner_radius:.1f} mm"
    )
    ax.set_xlabel("Transverse Y (mm)")
    ax.grid(True, alpha=0.25)
axes[0].set_ylabel("Radial Z (mm)")
handles, labels = axes[-1].get_legend_handles_labels()
fig.legend(handles, labels, loc="lower center", ncol=4, bbox_to_anchor=(0.5, 0.01))
fig.suptitle("Cross-sectional keep-out envelopes", fontsize=15, y=0.98)
fig.subplots_adjust(left=0.05, right=0.99, bottom=0.18, top=0.83, wspace=0.24)
fig.savefig(OUT / "cross_section_clearance.png", dpi=190, bbox_inches="tight")
plt.close(fig)


metadata = {
    "units": "mm",
    "pipe_ID_nominal": PIPE_ID,
    "pipe_ID_range": [140, 160],
    "wheel_diameter": WHEEL_DIAMETER,
    "wheel_width_placeholder": WHEEL_WIDTH,
    "finite_wheel_width_contact_correction_mm": PIPE_ID / 2.0 - WHEEL_RADIUS - RADIAL_CENTER_OFFSET,
    "link_axis_spacing": LINK_LENGTH,
    "nominal_V_angle_deg": V_ANGLE_DEG,
    "active_wheel_centers": [REAR_WHEEL.tolist(), FRONT_WHEEL.tolist()],
    "passive_wheel_center": CENTER_WHEEL.tolist(),
    "reserved_bays": {
        "rear_power_bay": [92, 64, 30],
        "front_control_bay": [92, 64, 30],
        "future_camera_bay": [50, 48, 32],
    },
    "transverse_envelope_target": 95,
    "nominal_max_robot_radius_mm": max_robot_radius,
    "nominal_pipe_radius_mm": PIPE_RADIUS,
    "cross_section_clearance": clearance_results,
    "notes": [
        "Mirrored motor placement balances roll moment.",
        "Power and signal routes are separated across the center hinge.",
        "Module solids are keep-out envelopes; final hole patterns follow purchased boards.",
        "Final manufacturing drawings require exact omni-wheel width and spring geometry.",
    ],
}
(OUT / "model_metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")

print(glb_path)
print(json.dumps(metadata, indent=2))
