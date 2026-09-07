# ARMIS - Autonomous Robotic Manipulation and Intelligent Servo Control

![Status](https://img.shields.io/badge/status-prototype-yellow)
![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black)
![Arduino](https://img.shields.io/badge/Arduino-Uno%2FNano-teal)
![License](https://img.shields.io/badge/license-MIT-blue)

## Authors
**Mr. Aayush Mishra - 4th Year, Mechatronics @ MIT Manipal**

**Dr. Abhay Jangid -  Oncology Resident @ KMC Manipal**

## Co-Author 

**Mr. Pranay Shet - 2nd Year, Electronics & Communication @ MIT Manipal**

## Project Overview

ARMIS is a 4-DOF robotic arm control stack pairing a Unity front end
(analytical IK, trajectory planning, mission orchestration, telemetry) with
lightweight Arduino firmware (servo control, independent safety clamping,
comms watchdog). It targets a real 2-link planar arm on a rotating base -
base, shoulder, elbow, gripper - driven by hobby servos over USB serial.

The project is scoped deliberately: no ROS2, no simulation physics engine,
no reinforcement learning, no cloud services. Every feature either runs on
the current four-servo hardware, requires only inexpensive additions
(limit switches, a distance sensor), or is explicitly marked
simulation-only.

## System Architecture

```
Target Position
      |
      v
 IK (IKManager)
      |
      v
 Constraint Validation (workspace + joint limits, FK verification)
      |
      v
 Trajectory Planner (quintic smootherstep, joint-synchronized)
      |
      v
 Robot Command (logical angles)
      |
      v
 ArmController (calibration offsets + direction mapping)
      |
      v
 Robot Transport Interface
      |-- Simulation Transport   (SIMULATION mode, no I/O)
      '-- Serial Transport       (HARDWARE mode, USB to Arduino)
      |
      v
 Arduino (independent parsing, independent clamping, watchdog)
      |
      v
 Servos
```

```
Mission
   |
   v
Mission Scheduler (FIFO queue, retry, pause/cancel)
   |
   v
Robot State Machine (explicit whitelisted transitions)
   |
   v
Trajectory Execution (Orchestrator)
   |
   v
Hardware (ArmController -> Transport -> Arduino)
```

## Objectives

- Preserve and harden the existing analytical 2-link IK rather than
  replacing it with a numerical solver.
- Make IK accuracy objectively measurable via forward-kinematics
  verification, not just visually plausible.
- Add two independent layers of joint/workspace safety (Unity and
  Arduino), because the Arduino must never blindly trust Unity.
- Replace ad-hoc `MoveTowards()` motion with a mathematically explainable,
  jointly-synchronized trajectory generator.
- Give the robot an explicit, whitelisted state machine instead of
  behavior buried in `Update()`.
- Support teach-and-repeat and mission-level pick-and-place on top of the
  same primitives, without hardcoding a single sequence.
- Keep simulation and hardware mode behaviorally identical except for the
  final transport hop.

## Key Features

### Analytical Inverse Kinematics
Closed-form 2-link planar solve with a rotating base. Numerically
protected (`Acos` argument clamped to `[-1, 1]`) against NaN from floating
point drift at full extension/fold. Returns a structured result: valid
flag, requested vs. clamped angles, workspace status, joint-limit status.

### Forward Kinematics Verification
Every IK solve is checked by running FK on the clamped result and
reporting per-axis and Euclidean position error against the original
target - an objective accuracy number, not just a visual check.

### Workspace and Joint Safety
Independent configurable limits per joint. Two safety layers: Unity
clamps/rejects before transmission; the Arduino independently clamps
every parsed packet against its own limit table. Neither layer trusts the
other.

### Smooth Trajectory Planning
Quintic smootherstep interpolation (`6t^5 - 15t^4 + 10t^3`) — zero
velocity and zero acceleration at both endpoints, so motion starts and
stops cleanly. A single time parameter drives all four joints, so they
are synchronized by construction, not by post-hoc tuning.

### Teach-and-Repeat
Record the arm's current commanded pose as a waypoint; build, edit, save
(JSON), load, and play back waypoint sequences.

### Mission-Level Task Scheduling
FIFO mission queue with pause/resume/cancel/retry-last-failed and
completed/failed counters.

### Autonomous Pick-and-Place
A reusable `Mission`/`MissionAction` abstraction. The standard pick-and-place
workflow (`HOME -> MOVE_ABOVE_PICK -> MOVE_TO_PICK -> GRIP -> LIFT ->
MOVE_TO_PLACE -> RELEASE -> RETURN_HOME`) is one call:
`Mission.CreatePickAndPlace(name, pickPos, placePos, approachHeight)`.
Pick/place positions are specified manually in Unity — no object detection
in the core implementation.

### Telemetry
Live dashboard: connection state, command rate, time since last
successful transmission, current state, joint angles, IK target/FK/error,
workspace and joint-limit status, active mission/action, and
completed/failed mission counts. Fully populated in SIMULATION mode with
no Arduino attached.

### Serial Hardware Control
Human-readable `B<n>S<n>E<n>G<n>` packets at 115200 baud, ~20 Hz,
rate-limited transport-side so a fast Unity frame rate cannot flood the
serial buffer.

### Communication Watchdog
Arduino-side: any joint hold (not move-to-90) if no valid packet arrives
within a configurable timeout (default 500 ms). Malformed packets are
dropped without resetting the watchdog or crashing the parser.

### Homing
Configurable software home pose, reachable via a `Move Home` command or
as a mission action. Physical limit-switch homing is an optional,
clearly separated extension — not required for the core project.

### Emergency Stop
Software E-stop cancels the active trajectory and mission, forces
`FAULT`, and blocks all new motion until an explicit reset. Documented as
a software-only safeguard — see Safety Architecture.

## Hardware

| Component | Notes |
|---|---|
| Controller | Arduino-compatible microcontroller (Uno/Nano-class), example |
| Servos | 4x hobby servo, user-configurable torque/range |
| Gripper | Simple open/close servo gripper, user-configurable |
| Power | External servo power rail recommended, user-configurable |
| Communication | USB serial, 115200 baud |
| Optional sensors | Limit switches, E-stop input, gripper feedback, object-present sensor — supported interfaces, not required |

## Software Stack

- Unity (2021.3 LTS or newer recommended) + C#
- Arduino IDE (or PlatformIO) + C++ (`Servo.h` only — a standard Arduino
  library, no third-party dependency)
- No external Unity packages beyond the built-in `UnityEngine.UI` module

## Repository Structure

```
Assets/
  Scripts/
    IK/
      IKManager.cs
      ForwardKinematics.cs
    Motion/
      TrajectoryPlanner.cs
      Waypoint.cs
    Robot/
      RobotConfiguration.cs
      RobotCommand.cs
      RobotStateMachine.cs
      RobotOrchestrator.cs
    Missions/
      Mission.cs
      MissionAction.cs
      MissionScheduler.cs
    Hardware/
      ArmController.cs
      IRobotTransport.cs
      SimulationRobotTransport.cs
      SerialRobotTransport.cs
    UI/
      RobotDashboard.cs
    Safety/
      SafetyManager.cs
Arduino/
  ARMIS_Controller/
    ARMIS_Controller.ino
README.md
```

> `RobotOrchestrator.cs` is an addition beyond the original component list:
> IK, trajectory, state machine, missions, and hardware are each correct in
> isolation but need one class that sequences them end to end. See its
> header comment for the reasoning.

## Installation

### Unity Setup

1. Create/open a 3D Unity project (2021.3 LTS+).
2. Copy `Assets/Scripts/` into your project's `Assets/` folder.
3. Create a `RobotConfiguration` asset: right-click in the Project window
   → `Create > ARMIS > Robot Configuration`. Set `L0/L1/L2` to your arm's
   real link lengths (keep units consistent everywhere — e.g. meters).
4. GameObject hierarchy:
   ```
   ARMIS_System
     ├── IKTarget            (empty Transform, moved by hand/UI to set targets)
     ├── ArmVisual
     │     └── LineRenderer  (3 points: base, elbow, end-effector)
     ├── Systems             (empty, holds the logic components below)
     └── Canvas
           └── (Dashboard Text elements)
   ```
5. On `Systems`, add one of each: `IKManager`, `TrajectoryPlanner`,
   `RobotStateMachine`, `MissionScheduler`, `ArmController`,
   `SafetyManager`, `RobotOrchestrator`. Wire their public references to
   each other and to the shared `RobotConfiguration` asset in the
   Inspector.
6. On `IKManager`: assign `config`, `target` (the `IKTarget` transform),
   and `lineRenderer`.
7. On `ArmController`: assign `config`; set `mode` to `Simulation` for
   bench-testing without hardware.
8. On `RobotDashboard` (add to the `Canvas` object): assign UI `Text`
   elements for each telemetry field, and the component references
   (`armController`, `stateMachine`, `ikManager`, `missionScheduler`,
   `safetyManager`).

### UI Setup

Use a standard `Canvas` (Screen Space – Overlay) with plain
`UnityEngine.UI.Text` elements (or `TextMeshPro`, adjusting
`RobotDashboard` field types if you prefer it) for each telemetry field
listed in the Telemetry feature above. No custom UI package required.

### Arduino Setup

1. Wiring:

   | Servo | Pin |
   |---|---|
   | Base | 6 |
   | Shoulder | 9 |
   | Elbow | 10 |
   | Gripper | 11 |

   Power servos from an external 5–6V rail rated for stall current × 4,
   not from the Arduino 5V pin. Common ground between the Arduino and the
   servo power rail is required.

2. Open `Arduino/ARMIS_Controller/ARMIS_Controller.ino` in the Arduino
   IDE, select your board, and upload.
3. Edit `JOINT_MIN[]` / `JOINT_MAX[]` in the `.ino` to match your servos'
   safe mechanical range, and mirror the same values into
   `RobotConfiguration.servoMin` / `servoMax` in Unity.

### Serial Configuration

- Baud: 115200 (both sides — already set in the firmware and in
  `RobotConfiguration.baudRate`).
- Set `RobotConfiguration.serialPortName` to your OS's port
  (`COM3` on Windows, `/dev/tty.usbmodemXXXX` on macOS,
  `/dev/ttyACM0`/`/dev/ttyUSB0` on Linux).
- Switch `ArmController.mode` to `Hardware` only after simulation testing
  passes (see below).

## Calibration

1. With the arm powered and `ArmController.mode = Simulation`, verify the
   Unity-only IK/trajectory/mission behavior first (Simulation-Only Test
   Procedure below).
2. Set `mode = Hardware`, connect via USB, and command the arm to its
   configured home pose (`Move Home`).
3. For each joint, compare the physical angle to the intended logical
   angle. Adjust `RobotConfiguration.<joint>CalibrationOffset` (degrees)
   until the physical pose matches the logical intent.
4. If a joint moves the wrong direction relative to its logical angle,
   set that joint's `ServoDirection` to `Reversed` instead of trying to
   fix it with a large offset — offsets shift the whole range, direction
   flips it.
5. Re-verify `JOINT_MIN[]`/`JOINT_MAX[]` in the firmware and
   `servoMin`/`servoMax`/logical joint limits in Unity are consistent
   with the arm's real mechanical range before running any mission.
6. Save the `RobotConfiguration` asset — calibration persists as part of
   the asset file.

## Operating Modes

**Simulation Mode** — `ArmController.mode = Simulation`. No serial
required. IK, trajectories, missions, and telemetry all run exactly as in
hardware mode; only the final packet transmission is skipped.

**Hardware Mode** — `ArmController.mode = Hardware`. Unity simulation
remains visible alongside real servo motion; the orchestrator's output is
transmitted to the Arduino over serial.

## Mission Example

```csharp
var pick = new Vector3(0.12f, 0.02f, 0.05f);
var place = new Vector3(-0.10f, 0.02f, 0.08f);
var mission = Mission.CreatePickAndPlace("Move Block A", pick, place, approachHeight: 0.05f);
missionScheduler.AddMission(mission);
```

This produces: `HOME → MOVE_ABOVE_PICK → MOVE_TO_PICK → GRIP → LIFT →
MOVE_TO_PLACE_ABOVE → MOVE_TO_PLACE → RELEASE → RETURN_HOME`, with the
orchestrator solving IK and running a synchronized trajectory for every
`MOVE_TO` step, and failing the mission (not the whole system) if any
target turns out to be unreachable.

## Safety Architecture

- **Joint limits** - configurable per joint, enforced in Unity before
  transmission.
- **Workspace limits** - IK rejects targets outside `[minReach, maxReach]`
  (with a configurable safety margin) before any angles are computed.
- **Software E-stop** - cancels trajectory/mission and forces `FAULT`,
  requiring explicit operator reset.
- **Serial watchdog** - Arduino-side, independent of Unity; holds last
  position (does not move to an arbitrary pose) if commands stop
  arriving.
- **Arduino-side validation** - every parsed packet is independently
  clamped to the firmware's own limit table, regardless of what Unity
  sent.
- **Physical E-stop (recommended, not included in software)** - a real
  deployment should wire a hardware emergency-stop that cuts servo power
  directly. The software E-stop in this project stops commands from the
  Unity side; it cannot cut power and cannot protect against a
  hung/crashed microcontroller. Do not treat it as equivalent to a
  hardware safety circuit.

## Performance Metrics

The following are **measured at runtime** via the dashboard and should be
recorded per test session - this repository does not ship pre-recorded
results:

- IK position error (Euclidean, from FK verification)
- Trajectory duration (configurable, `RobotConfiguration.defaultTrajectoryDuration`)
- Command frequency (configurable, `RobotConfiguration.commandRateHz`)
- Mission completion rate (`MissionScheduler.CompletedCount` /
  total dispatched)
- Serial packet loss/failure count (transport-level `SendPacket` failures)
- Joint-limit violations (`IKResult.JointLimits`)
- Average mission duration (`Mission.elapsedSeconds`, tracked externally)

## Testing

**IK unit-style checks** — feed `IKManager.Solve()` known targets
(including exact full-extension and near-zero-reach points) and assert
`PositionErrorEuclidean` stays within tolerance and no NaN/Infinity is
produced.

**Workspace tests** — targets just inside/outside `maxReach`/`minReach`
correctly flip `IsValid` and `WorkspaceStatus`.

**Trajectory tests** — `TrajectoryPlanner.Play()` with a 0-duration edge
case; confirm it completes on the next `Tick()` without dividing by zero.

**Serial tests** — send truncated/garbled packets to the Arduino over a
terminal and confirm no crash and no unclamped servo motion.

**Simulation tests** — run full missions with `mode = Simulation` and no
Arduino attached; dashboard should show all fields populated.

**Hardware tests** — run the Safe First-Power-On procedure below before
any mission.

**Failure tests** — physically unplug the USB cable mid-mission; confirm
the watchdog fault triggers within `WATCHDOG_TIMEOUT_MS` and the arm holds
position.

### Safe First-Power-On Test Procedure

1. Servos unpowered, Arduino connected via USB only. Upload firmware.
2. In Unity, `mode = Simulation`. Confirm IK/trajectory/dashboard behave
   correctly with no hardware at all.
3. Power the servo rail. Do **not** send commands yet — confirm servos
   hold whatever position they power up in.
4. Switch `mode = Hardware`, connect serial, issue a single `Move Home`
   command at a slow `defaultTrajectoryDuration` (e.g. 3–5 s). Watch for
   correct direction and no mechanical binding before trusting it with
   faster or larger motions.
5. Only after home behaves correctly, test a single manual `MoveToPosition`
   inside a conservative, known-safe sub-region of the workspace.

### Simulation-Only Test Procedure

Set `mode = Simulation`; run missions, teach-and-repeat, and the E-stop
button; confirm dashboard telemetry updates correctly with `Serial:
DISCONNECTED` never appearing (simulation transport always reports
connected) and no exceptions in the console.

### Hardware Test Procedure

After the Safe First-Power-On procedure passes: run a full
`CreatePickAndPlace` mission at conservative speed, then progressively
reduce `defaultTrajectoryDuration` while watching for joint-limit
violations or missed targets in the dashboard.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Arm doesn't move in Hardware mode | Wrong `serialPortName`, port in use by Arduino IDE Serial Monitor, or `mode` still `Simulation` |
| Arm moves opposite to expected direction | `ServoDirection` should be `Reversed` for that joint |
| Arm's physical pose doesn't match Unity's visual pose | Calibration offset needed, or `JOINT_MIN/MAX` mismatch between Unity and firmware |
| Arm freezes mid-motion, dashboard shows fault | Serial watchdog tripped — check USB connection and cable strain relief |
| `IKResult.IsValid = false` for a point that looks reachable | Check `workspaceMargin` and confirm `L0/L1/L2` match the physical arm |
| Mission fails immediately | A `MOVE_TO` target is outside the workspace or violates joint limits — check the dashboard's `TARGET` field |
| Jerky motion despite trajectory planner | `defaultTrajectoryDuration` too short for the distance being traveled |

## Limitations

- Open-loop servo control — no absolute encoder feedback unless added.
- Servo backlash and general mechanical uncertainty are not modeled or
  compensated.
- No guaranteed physical collision avoidance; Unity's visual simulation is
  not a collision safety guarantee.
- Position accuracy is bounded by servo resolution and calibration
  quality, not by the IK math itself (FK verification reports IK/model
  error only, not real-world servo error).
- `JOINT_MIN/MAX` in firmware and joint/servo limits in
  `RobotConfiguration` are two independent, hand-maintained sources of
  truth — nothing enforces they stay in sync besides this document.

## Future Work

- Physical joint encoders for closed-loop feedback
- Force/torque sensing at the gripper
- Camera-based object localization
- Depth sensing for approach planning
- Closed-loop position control
- Physical collision sensing
- More advanced motion planning
- ROS2 integration

None of the above is implemented in this repository; they are listed as
possible extensions only.

## Research / Engineering Value

Analytical kinematics with objective FK-based error measurement,
mathematically explainable joint-synchronized motion planning, an
explicit whitelisted state machine, mission-level task orchestration with
a reusable action abstraction, two independent safety layers, and a
simulation/hardware interface designed to keep those layers behaviorally
identical up to the final transport hop.

## License

MIT
