#!/usr/bin/env python3
"""Generate the authoritative prefab assets (Step 11.30/11.31).

Creates valid Unity 6 prefab assets under Assets/JajuchaSim/Prefabs/:

    Core/SimulatorCore.prefab      - _Core group template (managers + bootstrap)
    Vehicle/JajuchaVehicle.prefab  - authoritative vehicle hierarchy (11.31)
    UI/RuntimeUI.prefab            - runtime UI group template
    Course/CourseRuntimeRoot.prefab- course runtime root template
    Objects/{Obstacle,SlowSign,StartSignal,SpeedTerminal}.prefab

These are the single authoritative prefab templates. The runtime continues to
build the vehicle/course procedurally (see DESIGN_DECISIONS DD-022); the
prefabs exist so there is exactly one authoritative source per object and no
hand-duplicated prefab copies anywhere in the project.

Run from the repository root:
    python tools/generate_prefabs.py
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
PREFABS = os.path.join(ROOT, "Assets", "JajuchaSim", "Prefabs")

# Script GUIDs (from .meta files; must match generate_scene.py).
G_SIM_MANAGER = "11111111111111111111111111111111"
G_SIM_HUD = "55555555555555555555555555555555"
G_VEHICLE_BEHAV = "045479faddaf9d44b93d8e968001170e"
G_CAMERA_SENSOR = "0a328dd5514979946a4431f9ff13763c"
G_BRIDGE = "8c1824941d8d2054b88b70e36dcd9399"
G_MAP_EDITOR = "7ab1faf79353e6749ab7cfedf6c7a72a"
G_APP_BOOTSTRAP = "a11b00000000000000000000000000c5"
G_ERROR_DISPLAY = "a11b00000000000000000000000000c1"
G_COURSE_MGR = "a11b00000000000000000000000000b6"
G_SIM_RUNNER = "a11b00000000000000000000000000b7"
G_OBSERVER = "a11b00000000000000000000000000b8"
G_SHUTDOWN = "a11b00000000000000000000000000b9"
G_STATUS_BAR = "a11b00000000000000000000000000c4"
G_SIM_CONFIG = "33333333333333333333333333333333"

# Built-in mesh assets (unity default resources).
MESH_CUBE = "{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}"
MESH_CYLINDER = "{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}"
MAT_DEFAULT = "{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}"


class Prefab:
    """Accumulates YAML lines for one prefab asset."""

    def __init__(self):
        self.lines = []
        self.lines.append("%YAML 1.1")
        self.lines.append("%TAG !u! tag:unity3d.com,2011:")

    def go(self, fid, name, layer=0, tag="Untagged", components=None, active=1):
        self.lines.append(f"--- !u!1 &{fid}")
        self.lines.append("GameObject:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append("  serializedVersion: 6")
        self.lines.append("  m_Component:")
        for c in (components or []):
            self.lines.append(f"  - component: {{fileID: {c}}}")
        self.lines.append(f"  m_Layer: {layer}")
        self.lines.append(f"  m_Name: {name}")
        self.lines.append(f"  m_TagString: {tag}")
        self.lines.append("  m_Icon: {fileID: 0}")
        self.lines.append("  m_NavMeshLayer: 0")
        self.lines.append("  m_StaticEditorFlags: 0")
        self.lines.append(f"  m_IsActive: {active}")

    def transform(self, fid, gofid, pos=(0, 0, 0), rot=(0, 0, 0, 1), scale=(1, 1, 1),
                  children=None, father=0, euler=(0, 0, 0)):
        self.lines.append(f"--- !u!4 &{fid}")
        self.lines.append("Transform:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  serializedVersion: 2")
        self.lines.append(f"  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}")
        self.lines.append(f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}")
        self.lines.append(f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}")
        self.lines.append("  m_ConstrainProportionsScale: 0")
        self.lines.append("  m_Children:")
        for c in (children or []):
            self.lines.append(f"  - {{fileID: {c}}}")
        self.lines.append(f"  m_Father: {{fileID: {father}}}")
        self.lines.append(f"  m_LocalEulerAnglesHint: {{x: {euler[0]}, y: {euler[1]}, z: {euler[2]}}}")

    def mono(self, fid, gofid, guid, classid, fields):
        self.lines.append(f"--- !u!114 &{fid}")
        self.lines.append("MonoBehaviour:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  m_Enabled: 1")
        self.lines.append("  m_EditorHideFlags: 0")
        self.lines.append(f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}")
        self.lines.append("  m_Name: ")
        self.lines.append(f"  m_EditorClassIdentifier: {classid}")
        for k, v in fields.items():
            self.lines.append(f"  {k}: {v}")

    def mesh_filter(self, fid, gofid, mesh=MESH_CUBE):
        self.lines.append(f"--- !u!33 &{fid}")
        self.lines.append("MeshFilter:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append(f"  m_Mesh: {mesh}")

    def mesh_renderer(self, fid, gofid, material=MAT_DEFAULT):
        self.lines.append(f"--- !u!23 &{fid}")
        self.lines.append("MeshRenderer:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  m_Enabled: 1")
        self.lines.append("  m_CastShadows: 1")
        self.lines.append("  m_ReceiveShadows: 1")
        self.lines.append("  m_DynamicOccludee: 1")
        self.lines.append("  m_StaticShadowCaster: 0")
        self.lines.append("  m_MotionVectors: 1")
        self.lines.append("  m_LightProbeUsage: 1")
        self.lines.append("  m_ReflectionProbeUsage: 1")
        self.lines.append("  m_RayTracingMode: 2")
        self.lines.append("  m_RayTraceProcedural: 0")
        self.lines.append("  m_RayTracingAccelStructBuildFlagsOverride: 0")
        self.lines.append("  m_RayTracingAccelStructBuildFlags: 1")
        self.lines.append("  m_SmallMeshCulling: 1")
        self.lines.append("  m_RenderingLayerMask: 1")
        self.lines.append("  m_RendererPriority: 0")
        self.lines.append("  m_Materials:")
        self.lines.append(f"  - {material}")
        self.lines.append("  m_StaticBatchInfo:")
        self.lines.append("    firstSubMesh: 0")
        self.lines.append("    subMeshCount: 0")
        self.lines.append("  m_StaticBatchRoot: {fileID: 0}")
        self.lines.append("  m_ProbeAnchor: {fileID: 0}")
        self.lines.append("  m_LightProbeVolumeOverride: {fileID: 0}")
        self.lines.append("  m_ScaleInLightmap: 1")
        self.lines.append("  m_ReceiveGI: 1")
        self.lines.append("  m_PreserveUVs: 0")
        self.lines.append("  m_IgnoreNormalsForChartDetection: 0")
        self.lines.append("  m_ImportantGI: 0")
        self.lines.append("  m_StitchLightmapSeams: 1")
        self.lines.append("  m_SelectedEditorRenderState: 3")
        self.lines.append("  m_MinimumChartSize: 4")
        self.lines.append("  m_AutoUVMaxDistance: 0.5")
        self.lines.append("  m_AutoUVMaxAngle: 89")
        self.lines.append("  m_LightmapParameters: {fileID: 0}")
        self.lines.append("  m_SortingLayerID: 0")
        self.lines.append("  m_SortingLayer: 0")
        self.lines.append("  m_SortingOrder: 0")
        self.lines.append("  m_AdditionalVertexStreams: {fileID: 0}")

    def box_collider(self, fid, gofid, size=(10, 10, 20), center=(0, 0, 0)):
        self.lines.append(f"--- !u!65 &{fid}")
        self.lines.append("BoxCollider:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  m_Material: {fileID: 0}")
        self.lines.append("  m_IncludeLayers:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    m_Bits: 0")
        self.lines.append("  m_ExcludeLayers:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    m_Bits: 0")
        self.lines.append("  m_LayerOverridePriority: 0")
        self.lines.append("  m_IsTrigger: 0")
        self.lines.append("  m_ProvidesContacts: 0")
        self.lines.append("  m_Enabled: 1")
        self.lines.append("  serializedVersion: 3")
        self.lines.append(f"  m_Size: {{x: {size[0]}, y: {size[1]}, z: {size[2]}}}")
        self.lines.append(f"  m_Center: {{x: {center[0]}, y: {center[1]}, z: {center[2]}}}")

    def rigidbody(self, fid, gofid, mass=1.5, drag=0.5, angular_drag=0.05):
        self.lines.append(f"--- !u!54 &{fid}")
        self.lines.append("Rigidbody:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  serializedVersion: 4")
        self.lines.append(f"  m_Mass: {mass}")
        self.lines.append(f"  m_Drag: {drag}")
        self.lines.append(f"  m_AngularDrag: {angular_drag}")
        self.lines.append("  m_CenterOfMass: {x: 0, y: 0, z: 0}")
        self.lines.append("  m_InertiaTensor: {x: 1, y: 1, z: 1}")
        self.lines.append("  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}")
        self.lines.append("  m_IncludeLayers:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    m_Bits: 0")
        self.lines.append("  m_ExcludeLayers:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    m_Bits: 0")
        self.lines.append("  m_ImplicitCom: 1")
        self.lines.append("  m_ImplicitTensor: 1")
        self.lines.append("  m_UseGravity: 1")
        self.lines.append("  m_IsKinematic: 0")
        self.lines.append("  m_Interpolate: 0")
        self.lines.append("  m_ContactsGeneration: 1")
        self.lines.append("  m_ConstraintFlags: 0")
        self.lines.append("  m_SolverIterations: 20")
        self.lines.append("  m_SolverVelocityIterations: 5")
        self.lines.append("  m_Type: 0")
        self.lines.append("  m_Seed: 0")
        self.lines.append("  m_LinearVelocity: {x: 0, y: 0, z: 0}")
        self.lines.append("  m_AngularVelocity: {x: 0, y: 0, z: 0}")
        self.lines.append("  m_ExternalForce: {x: 0, y: 0, z: 0}")
        self.lines.append("  m_ExternalTorque: {x: 0, y: 0, z: 0}")
        self.lines.append("  m_LinearDamping: 0.5")
        self.lines.append("  m_AngularDamping: 0.05")
        self.lines.append("  m_WasSleeping: 0")
        self.lines.append("  m_IsSleeping: 0")
        self.lines.append("  m_UseAutoMass: 0")
        self.lines.append("  m_AutomaticCenterOfMass: 1")
        self.lines.append("  m_AutomaticInertia: 1")

    def camera(self, fid, gofid, name="Camera", near=1, far=5000, fov=60, depth=-1):
        self.lines.append(f"--- !u!20 &{fid}")
        self.lines.append("Camera:")
        self.lines.append("  m_ObjectHideFlags: 0")
        self.lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
        self.lines.append("  m_PrefabInstance: {fileID: 0}")
        self.lines.append("  m_PrefabAsset: {fileID: 0}")
        self.lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
        self.lines.append("  m_Enabled: 1")
        self.lines.append("  serializedVersion: 2")
        self.lines.append("  m_ClearFlags: 2")
        self.lines.append("  m_BackGroundColor: {r: 0.19215687, g: 0.3019608, b: 0.4745098, a: 0}")
        self.lines.append("  m_projectionMatrixMode: 1")
        self.lines.append("  m_GateFitMode: 2")
        self.lines.append("  m_FOVAxisMode: 0")
        self.lines.append("  m_Iso: 200")
        self.lines.append("  m_ShutterSpeed: 0.005")
        self.lines.append("  m_Aperture: 16")
        self.lines.append("  m_FocusDistance: 10")
        self.lines.append("  m_FocalLength: 50")
        self.lines.append("  m_BladeCount: 5")
        self.lines.append("  m_Curvature: {x: 2, y: 11}")
        self.lines.append("  m_BarrelClipping: 0.25")
        self.lines.append("  m_Anamorphism: 0")
        self.lines.append("  m_SensorSize: {x: 36, y: 24}")
        self.lines.append("  m_LensShift: {x: 0, y: 0}")
        self.lines.append("  m_NormalizedViewPortRect:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    x: 0")
        self.lines.append("    y: 0")
        self.lines.append("    width: 1")
        self.lines.append("    height: 1")
        self.lines.append(f"  near clip plane: {near}")
        self.lines.append(f"  far clip plane: {far}")
        self.lines.append(f"  field of view: {fov}")
        self.lines.append("  orthographic: 0")
        self.lines.append("  orthographic size: 5")
        self.lines.append(f"  m_Depth: {depth}")
        self.lines.append("  m_CullingMask:")
        self.lines.append("    serializedVersion: 2")
        self.lines.append("    m_Bits: 4294967295")
        self.lines.append("  m_RenderingPath: -1")
        self.lines.append("  m_TargetTexture: {fileID: 0}")
        self.lines.append("  m_TargetDisplay: 0")
        self.lines.append("  m_TargetEye: 3")
        self.lines.append("  m_HDR: 1")
        self.lines.append("  m_AllowMSAA: 1")
        self.lines.append("  m_AllowDynamicResolution: 0")
        self.lines.append("  m_ForceIntoRT: 0")
        self.lines.append("  m_OcclusionCulling: 1")
        self.lines.append("  m_StereoConvergence: 10")
        self.lines.append("  m_StereoSeparation: 0.022")

    def write(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(self.lines) + "\n")
        print("Wrote", os.path.relpath(path, ROOT), os.path.getsize(path), "bytes")


# ---------------------------------------------------------------------------
# 1. Core/SimulatorCore.prefab
# ---------------------------------------------------------------------------
def build_simulator_core():
    p = Prefab()
    # Root "SimulatorCore"
    go_root, t_root = 1000, 4000
    go_mgr, t_mgr = 1100, 4100
    go_runner, t_runner = 1200, 4200
    go_bootstrap, t_bootstrap = 1300, 4300

    p.go(go_root, "SimulatorCore", components=[t_root])
    p.transform(t_root, go_root, children=[t_mgr, t_runner, t_bootstrap])

    p.go(go_mgr, "SimulationManager", components=[t_mgr, 1140001, 1140002])
    p.transform(t_mgr, go_mgr, father=t_root)
    p.mono(1140001, go_mgr, G_SIM_MANAGER, "JajuchaSim.Core::JajuchaSim.Core.SimulationManager", {
        "config": "{fileID: 11400000, guid: " + G_SIM_CONFIG + ", type: 2}",
        "simulationSystemBehaviours": "",
    })
    p.mono(1140002, go_mgr, G_SIM_HUD, "JajuchaSim.Core::JajuchaSim.Core.SimulationDebugHud", {})

    p.go(go_runner, "SimulationRunner", components=[t_runner, 1140003])
    p.transform(t_runner, go_runner, father=t_root)
    p.mono(1140003, go_runner, G_SIM_RUNNER, "JajuchaSim.App::JajuchaSim.App.SimulationRunner", {
        "manager": "{fileID: 1140001}"})

    p.go(go_bootstrap, "ApplicationBootstrap", components=[t_bootstrap, 1140004])
    p.transform(t_bootstrap, go_bootstrap, father=t_root)
    p.mono(1140004, go_bootstrap, G_APP_BOOTSTRAP, "JajuchaSim.App::JajuchaSim.App.ApplicationBootstrap", {
        "simulationManager": "{fileID: 1140001}",
        "simulationRunner": "{fileID: 1140003}",
        "courseManager": "{fileID: 0}",
        "mapEditor": "{fileID: 0}",
        "vehicleBehaviour": "{fileID: 0}",
        "sensorBehaviour": "{fileID: 0}",
        "bridgeServer": "{fileID: 0}",
        "observerController": "{fileID: 0}",
        "errorDisplay": "{fileID: 0}",
        "shutdownService": "{fileID: 0}",
    })
    return p


# ---------------------------------------------------------------------------
# 2. Vehicle/JajuchaVehicle.prefab  (Step 11.31)
# ---------------------------------------------------------------------------
def build_vehicle():
    p = Prefab()
    # Root "JajuchaVehicle" — chassis + collision body + 4 wheels + sensor
    # mounts + debug anchors. All root transforms are identity (0,0,0 / 0 / 1).
    go_root, t_root = 2000, 5000
    go_chassis, t_chassis = 2100, 5100
    go_fl, t_fl = 2200, 5200
    go_fr, t_fr = 2300, 5300
    go_rl, t_rl = 2400, 5400
    go_rr, t_rr = 2500, 5500
    go_mounts, t_mounts = 2600, 5600
    go_cam_l, t_cam_l = 2610, 5610
    go_cam_c, t_cam_c = 2620, 5620
    go_cam_r, t_cam_r = 2630, 5630
    go_debug, t_debug = 2700, 5700

    p.go(go_root, "JajuchaVehicle",
        components=[t_root, 11400010, 11400011],
        tag="Player")
    p.transform(t_root, go_root, children=[t_chassis, t_fl, t_fr, t_rl, t_rr,
                                           t_mounts, t_debug])
    p.mono(11400010, go_root, G_VEHICLE_BEHAV,
           "JajuchaSim.Vehicle::JajuchaSim.Vehicle.VehicleSystemBehaviour", {
               "vehicleConfig": "{fileID: 0}"})
    p.rigidbody(11400011, go_root, mass=1.5, drag=0.5, angular_drag=0.05)

    # Chassis visual + collision body.
    p.go(go_chassis, "Chassis", components=[t_chassis, 3300001, 2300001, 6500001])
    p.transform(t_chassis, go_chassis, pos=(0, 2, 0), scale=(14, 4, 22), father=t_root)
    p.mesh_filter(3300001, go_chassis)
    p.mesh_renderer(2300001, go_chassis)
    p.box_collider(6500001, go_chassis, size=(14, 4, 22), center=(0, 0, 0))

    # Wheels (front-left / front-right steering wheels, rear-left / rear-right).
    # The runtime VehicleSystem.CreateWheel() adds the tuned WheelCollider +
    # friction to each wheel node at spawn (see VehicleSystem.cs); the prefab
    # keeps the authoritative wheel nodes + visuals so there is exactly one
    # source for the hierarchy.
    wheels = [
        (go_fl, t_fl, "FL_Wheel", (-10, 0, 11), (3310002, 2310002)),
        (go_fr, t_fr, "FR_Wheel", (10, 0, 11), (3320002, 2320002)),
        (go_rl, t_rl, "RL_Wheel", (-10, 0, -11), (3330002, 2330002)),
        (go_rr, t_rr, "RR_Wheel", (10, 0, -11), (3340002, 2340002)),
    ]
    for go_w, t_w, name, pos, fids in wheels:
        mf_fid, mr_fid = fids
        p.go(go_w, name, components=[t_w, mf_fid, mr_fid])
        p.transform(t_w, go_w, pos=pos, father=t_root)
        p.mesh_filter(mf_fid, go_w, mesh=MESH_CYLINDER)
        p.mesh_renderer(mr_fid, go_w)

    # Sensor mounts with left / center / right cameras.
    p.go(go_mounts, "SensorMounts", components=[t_mounts])
    p.transform(t_mounts, go_mounts, pos=(0, 4, 12), father=t_root,
                children=[t_cam_l, t_cam_c, t_cam_r])
    for go_cam, t_cam, name, pos, cam_fid in [
        (go_cam_l, t_cam_l, "LeftCamera", (-5, 0, 0), 2010001),
        (go_cam_c, t_cam_c, "CenterCamera", (0, 0, 0), 2020001),
        (go_cam_r, t_cam_r, "RightCamera", (5, 0, 0), 2030001),
    ]:
        p.go(go_cam, name, components=[t_cam, cam_fid])
        p.transform(t_cam, go_cam, pos=pos, father=t_mounts)
        p.camera(cam_fid, go_cam, name=name, near=1, far=5000, fov=60, depth=-1)

    # Debug anchors.
    p.go(go_debug, "DebugAnchors", components=[t_debug])
    p.transform(t_debug, go_debug, father=t_root)
    return p


# ---------------------------------------------------------------------------
# 3. UI/RuntimeUI.prefab
# ---------------------------------------------------------------------------
def build_runtime_ui():
    p = Prefab()
    go_root, t_root = 3000, 6000
    go_editor, t_editor = 3100, 6100

    p.go(go_root, "RuntimeUI", components=[t_root, 11400020])
    p.transform(t_root, go_root, children=[t_editor])
    p.mono(11400020, go_root, G_MAP_EDITOR, "JajuchaSim.MapEditor::JajuchaSim.MapEditor.MapEditorHud", {
        "_tileSizeCm": "20",
        "_defaultSaveName": "course.json",
    })

    p.go(go_editor, "MapEditorUI", components=[t_editor])
    p.transform(t_editor, go_editor, father=t_root)
    return p


# ---------------------------------------------------------------------------
# 4. Course/CourseRuntimeRoot.prefab
# ---------------------------------------------------------------------------
def build_course_root():
    p = Prefab()
    go_root, t_root = 4000, 7000
    p.go(go_root, "CourseRuntimeRoot", components=[t_root])
    children = []
    for i, name in enumerate(["RoadLayerRoot", "StructureLayerRoot", "ObjectLayerRoot",
                              "TriggerLayerRoot", "RuntimeOverlayRoot"]):
        go, t = 4100 + i, 7100 + i
        p.go(go, name, components=[t])
        p.transform(t, go, father=t_root)
        children.append(t)
    p.transform(t_root, go_root, children=children)
    return p


# ---------------------------------------------------------------------------
# 5. Objects
# ---------------------------------------------------------------------------
def build_object(name, size, color_hint="Obstacle"):
    p = Prefab()
    go, t = 5000, 8000
    p.go(go, name, components=[t, 3300100, 2300100, 6500100])
    p.transform(t, go)
    p.mesh_filter(3300100, go)
    p.mesh_renderer(2300100, go)
    p.box_collider(6500100, go, size=size)
    return p


def main():
    prefabs = {
        os.path.join("Core", "SimulatorCore.prefab"): build_simulator_core(),
        os.path.join("Vehicle", "JajuchaVehicle.prefab"): build_vehicle(),
        os.path.join("UI", "RuntimeUI.prefab"): build_runtime_ui(),
        os.path.join("Course", "CourseRuntimeRoot.prefab"): build_course_root(),
        os.path.join("Objects", "Obstacle.prefab"): build_object("Obstacle", (40, 40, 40)),
        os.path.join("Objects", "SlowSign.prefab"): build_object("SlowSign", (12, 40, 4)),
        os.path.join("Objects", "StartSignal.prefab"): build_object("StartSignal", (12, 60, 4)),
        os.path.join("Objects", "SpeedTerminal.prefab"): build_object("SpeedTerminal", (30, 20, 4)),
    }
    for rel, p in prefabs.items():
        p.write(os.path.join(PREFABS, rel))


if __name__ == "__main__":
    main()
