#!/usr/bin/env python3
"""Generate Assets/JajuchaSim/Scenes/JajuchaSimulator.unity (Step 11.2/11.3).

Builds a valid Unity 6 scene with the fixed authoritative hierarchy and wired
serialized references. This is a build-time helper; the scene itself is the
deliverable.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "JajuchaSim", "Scenes", "JajuchaSimulator.unity")
OUT = os.path.normpath(OUT)

# Script GUIDs (from .meta files)
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

lines = []
lines.append("%YAML 1.1")
lines.append("%TAG !u! tag:unity3d.com,2011:")
lines.append("--- !u!29 &1")
lines.append("OcclusionCullingSettings:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  serializedVersion: 2")
lines.append("  m_OcclusionBakeSettings:")
lines.append("    smallestOccluder: 5")
lines.append("    smallestHole: 0.25")
lines.append("    backfaceThreshold: 100")
lines.append("  m_SceneGUID: 00000000000000000000000000000000")
lines.append("  m_OcclusionCullingData: {fileID: 0}")
lines.append("--- !u!104 &2")
lines.append("RenderSettings:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  serializedVersion: 10")
lines.append("  m_Fog: 0")
lines.append("  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}")
lines.append("  m_FogMode: 3")
lines.append("  m_FogDensity: 0.01")
lines.append("  m_LinearFogStart: 0")
lines.append("  m_LinearFogEnd: 300")
lines.append("  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}")
lines.append("  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}")
lines.append("  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}")
lines.append("  m_AmbientIntensity: 1")
lines.append("  m_AmbientMode: 0")
lines.append("  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}")
lines.append("  m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}")
lines.append("  m_HaloStrength: 0.5")
lines.append("  m_FlareStrength: 1")
lines.append("  m_FlareFadeSpeed: 3")
lines.append("  m_HaloTexture: {fileID: 0}")
lines.append("  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}")
lines.append("  m_DefaultReflectionMode: 0")
lines.append("  m_DefaultReflectionResolution: 128")
lines.append("  m_ReflectionBounces: 1")
lines.append("  m_ReflectionIntensity: 1")
lines.append("  m_CustomReflection: {fileID: 0}")
lines.append("  m_Sun: {fileID: 0}")
lines.append("  m_UseRadianceAmbientProbe: 0")
lines.append("--- !u!157 &3")
lines.append("LightmapSettings:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  serializedVersion: 13")
lines.append("  m_BakeOnSceneLoad: 0")
lines.append("  m_GISettings:")
lines.append("    serializedVersion: 2")
lines.append("    m_BounceScale: 1")
lines.append("    m_IndirectOutputScale: 1")
lines.append("    m_AlbedoBoost: 1")
lines.append("    m_EnvironmentLightingMode: 0")
lines.append("    m_EnableBakedLightmaps: 1")
lines.append("    m_EnableRealtimeLightmaps: 0")
lines.append("  m_LightmapEditorSettings:")
lines.append("    serializedVersion: 12")
lines.append("    m_Resolution: 2")
lines.append("    m_BakeResolution: 40")
lines.append("    m_AtlasSize: 1024")
lines.append("    m_AO: 0")
lines.append("    m_AOMaxDistance: 1")
lines.append("    m_CompAOExponent: 1")
lines.append("    m_CompAOExponentDirect: 0")
lines.append("    m_ExtractAmbientOcclusion: 0")
lines.append("    m_Padding: 2")
lines.append("    m_LightmapParameters: {fileID: 0}")
lines.append("    m_LightmapsBakeMode: 1")
lines.append("    m_TextureCompression: 1")
lines.append("    m_ReflectionCompression: 2")
lines.append("    m_MixedBakeMode: 2")
lines.append("    m_BakeBackend: 2")
lines.append("    m_PVRSampling: 1")
lines.append("    m_PVRDirectSampleCount: 32")
lines.append("    m_PVRSampleCount: 512")
lines.append("    m_PVRBounces: 2")
lines.append("    m_PVREnvironmentSampleCount: 256")
lines.append("    m_PVREnvironmentReferencePointCount: 2048")
lines.append("    m_PVRFilteringMode: 1")
lines.append("    m_PVRDenoiserTypeDirect: 1")
lines.append("    m_PVRDenoiserTypeIndirect: 1")
lines.append("    m_PVRDenoiserTypeAO: 1")
lines.append("    m_PVRFilterTypeDirect: 0")
lines.append("    m_PVRFilterTypeIndirect: 0")
lines.append("    m_PVRFilterTypeAO: 0")
lines.append("    m_PVREnvironmentMIS: 1")
lines.append("    m_PVRCulling: 1")
lines.append("    m_PVRFilteringGaussRadiusDirect: 1")
lines.append("    m_PVRFilteringGaussRadiusIndirect: 1")
lines.append("    m_PVRFilteringGaussRadiusAO: 1")
lines.append("    m_PVRFilteringAtrousPositionSigmaDirect: 0.5")
lines.append("    m_PVRFilteringAtrousPositionSigmaIndirect: 2")
lines.append("    m_PVRFilteringAtrousPositionSigmaAO: 1")
lines.append("    m_ExportTrainingData: 0")
lines.append("    m_TrainingDataDestination: TrainingData")
lines.append("    m_LightProbeSampleCountMultiplier: 4")
lines.append("  m_LightingDataAsset: {fileID: 20201, guid: 0000000000000000f000000000000000, type: 0}")
lines.append("  m_LightingSettings: {fileID: 0}")
lines.append("--- !u!196 &4")
lines.append("NavMeshSettings:")
lines.append("  serializedVersion: 2")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_BuildSettings:")
lines.append("    serializedVersion: 3")
lines.append("    agentTypeID: 0")
lines.append("    agentRadius: 0.5")
lines.append("    agentHeight: 2")
lines.append("    agentSlope: 45")
lines.append("    agentClimb: 0.4")
lines.append("    ledgeDropHeight: 0")
lines.append("    maxJumpAcrossDistance: 0")
lines.append("    minRegionArea: 2")
lines.append("    manualCellSize: 0")
lines.append("    cellSize: 0.16666667")
lines.append("    manualTileSize: 0")
lines.append("    tileSize: 256")
lines.append("    buildHeightMesh: 0")
lines.append("    maxJobWorkers: 0")
lines.append("    preserveTilesOutsideBounds: 0")
lines.append("    debug:")
lines.append("      m_Flags: 0")
lines.append("  m_NavMeshData: {fileID: 0}")

def go(fid, name, layer=0, tag="Untagged", components=None, active=1):
    lines.append(f"--- !u!1 &{fid}")
    lines.append("GameObject:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append("  serializedVersion: 6")
    lines.append("  m_Component:")
    for c in (components or []):
        lines.append(f"  - component: {{fileID: {c}}}")
    lines.append(f"  m_Layer: {layer}")
    lines.append(f"  m_Name: {name}")
    lines.append(f"  m_TagString: {tag}")
    lines.append("  m_Icon: {fileID: 0}")
    lines.append("  m_NavMeshLayer: 0")
    lines.append("  m_StaticEditorFlags: 0")
    lines.append(f"  m_IsActive: {active}")

def transform(fid, gofid, pos, rot=(0,0,0,1), scale=(1,1,1), children=None, father=0, euler=(0,0,0)):
    lines.append(f"--- !u!4 &{fid}")
    lines.append("Transform:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
    lines.append("  serializedVersion: 2")
    lines.append(f"  m_LocalRotation: {{x: {rot[0]}, y: {rot[1]}, z: {rot[2]}, w: {rot[3]}}}")
    lines.append(f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}")
    lines.append(f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}")
    lines.append("  m_ConstrainProportionsScale: 0")
    lines.append("  m_Children:")
    for c in (children or []):
        lines.append(f"  - {{fileID: {c}}}")
    lines.append(f"  m_Father: {{fileID: {father}}}")
    lines.append(f"  m_LocalEulerAnglesHint: {{x: {euler[0]}, y: {euler[1]}, z: {euler[2]}}}")

def mono(fid, gofid, guid, classid, fields):
    lines.append(f"--- !u!114 &{fid}")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append(f"  m_GameObject: {{fileID: {gofid}}}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}")
    lines.append("  m_Name: ")
    lines.append(f"  m_EditorClassIdentifier: {classid}")
    for k, v in fields.items():
        lines.append(f"  {k}: {v}")

# ---- Node table: (goFid, transformFid, name, parentTransformFid, [component tuples]) ----
# component tuple: (compFid, kind) where kind is 'mono'/'camera'/'light'/'mesh' etc.

# Root
ROOT_GO, ROOT_T = 1000000, 4000000
go(ROOT_GO, "JajuchaSimulator", components=[ROOT_T, 1140000100, 1140000101])
transform(ROOT_T, ROOT_GO, (0,0,0), children=[4000100, 4000200, 4000300, 4000400, 4000500, 4000600, 4000700, 4000800, 4000900])

# ---- _Core ----
go(1000100, "_Core", components=[4000100])
transform(4000100, 1000100, (0,0,0), children=[4000101, 4000102, 4000103, 4000104, 4000105], father=ROOT_T)
go(1000101, "SimulationManager", components=[4000101, 1140000110, 1140000111])
transform(4000101, 1000101, (0,0,0), father=4000100)
mono(1140000110, 1000101, G_SIM_MANAGER, "JajuchaSim.Core::JajuchaSim.Core.SimulationManager", {
    "config": "{fileID: 11400000, guid: " + G_SIM_CONFIG + ", type: 2}",
    "simulationSystemBehaviours": "".join([
        "\n  - {fileID: 1140000331}\n  - {fileID: 1140000441}"])
})
mono(1140000111, 1000101, G_SIM_HUD, "JajuchaSim.Core::JajuchaSim.Core.SimulationDebugHud", {})
go(1000102, "SimulationClock", components=[4000102])
transform(4000102, 1000102, (0,0,0), father=4000100)
go(1000103, "SimulationRunner", components=[4000103, 1140000113])
transform(4000103, 1000103, (0,0,0), father=4000100)
mono(1140000113, 1000103, G_SIM_RUNNER, "JajuchaSim.App::JajuchaSim.App.SimulationRunner", {
    "manager": "{fileID: 1140000110}"})
go(1000104, "SimulationEventBus", components=[4000104])
transform(4000104, 1000104, (0,0,0), father=4000100)
go(1000105, "ApplicationBootstrap", components=[4000105, 1140000115])
transform(4000105, 1000105, (0,0,0), father=4000100)
mono(1140000115, 1000105, G_APP_BOOTSTRAP, "JajuchaSim.App::JajuchaSim.App.ApplicationBootstrap", {
    "simulationManager": "{fileID: 1140000110}",
    "simulationRunner": "{fileID: 1140000113}",
    "courseManager": "{fileID: 1140000221}",
    "mapEditor": "{fileID: 1140000884}",
    "vehicleBehaviour": "{fileID: 1140000331}",
    "sensorBehaviour": "{fileID: 1140000441}",
    "bridgeServer": "{fileID: 1140000551}",
    "observerController": "{fileID: 1140000772}",
    "errorDisplay": "{fileID: 1140000100}",
    "shutdownService": "{fileID: 1140000994}",
})

# ---- _Course ----
go(1000200, "_Course", components=[4000200])
transform(4000200, 1000200, (0,0,0), children=[4000201, 4000202, 4000203, 4000204, 4000205, 4000206, 4000207], father=ROOT_T)
go(1000201, "CourseManager", components=[4000201, 1140000221])
transform(4000201, 1000201, (0,0,0), father=4000200)
mono(1140000221, 1000201, G_COURSE_MGR, "JajuchaSim.App::JajuchaSim.App.CourseManager", {
    "mapEditor": "{fileID: 1140000884}",
    "courseRuntimeRoot": "{fileID: 4000202}",
    "vehicleBehaviour": "{fileID: 1140000331}",
})
for fid, tf, name in [(1000202, 4000202, "CourseRuntimeRoot"), (1000203, 4000203, "RoadLayerRoot"),
                      (1000204, 4000204, "StructureLayerRoot"), (1000205, 4000205, "ObjectLayerRoot"),
                      (1000206, 4000206, "TriggerLayerRoot"), (1000207, 4000207, "RuntimeOverlayRoot")]:
    go(fid, name, components=[tf])
    transform(tf, fid, (0,0,0), father=4000200)

# ---- _Vehicle ----
go(1000300, "_Vehicle", components=[4000300])
transform(4000300, 1000300, (0,0,0), children=[4000301], father=ROOT_T)
go(1000301, "JajuchaVehicle", components=[4000301, 1140000331])
transform(4000301, 1000301, (0,0,0), father=4000300)
mono(1140000331, 1000301, G_VEHICLE_BEHAV, "JajuchaSim.Vehicle::JajuchaSim.Vehicle.VehicleSystemBehaviour", {
    "vehicleConfig": "{fileID: 0}"})

# ---- _Sensors ----
go(1000400, "_Sensors", components=[4000400])
transform(4000400, 1000400, (0,0,0), children=[4000401], father=ROOT_T)
go(1000401, "SensorRuntimeRoot", components=[4000401, 1140000441])
transform(4000401, 1000401, (0,0,0), father=4000400)
mono(1140000441, 1000401, G_CAMERA_SENSOR, "JajuchaSim.Sensors::JajuchaSim.Sensors.CameraSensorSystemBehaviour", {
    "vehicleBehaviour": "{fileID: 1140000331}",
    "leftCameraConfig": "{fileID: 0}",
    "centerCameraConfig": "{fileID: 0}",
    "rightCameraConfig": "{fileID: 0}",
})

# ---- _Bridge ----
go(1000500, "_Bridge", components=[4000500])
transform(4000500, 1000500, (0,0,0), children=[4000501], father=ROOT_T)
go(1000501, "JajuchaBridgeServer", components=[4000501, 1140000551])
transform(4000501, 1000501, (0,0,0), father=4000500)
mono(1140000551, 1000501, G_BRIDGE, "JajuchaSim.Bridge::JajuchaSim.Bridge.JajuchaBridgeServer", {
    "config": "{fileID: 0}",
    "simulationManager": "{fileID: 1140000110}",
    "vehicleBehaviour": "{fileID: 1140000331}",
    "cameraBehaviour": "{fileID: 1140000441}",
})

# ---- _Scenario ----
go(1000600, "_Scenario", components=[4000600])
transform(4000600, 1000600, (0,0,0), children=[4000601, 4000602, 4000603], father=ROOT_T)
for fid, tf, name in [(1000601, 4000601, "ScenarioManager"), (1000602, 4000602, "ScoreManager"),
                      (1000603, 4000603, "TestRunner")]:
    go(fid, name, components=[tf])
    transform(tf, fid, (0,0,0), father=4000600)

# ---- _Observer ----
go(1000700, "_Observer", components=[4000700])
transform(4000700, 1000700, (0,0,0), children=[4000701, 4000702], father=ROOT_T)

go(1000701, "ObserverCamera", components=[4000701, 20000701, 81000701], tag="MainCamera")
transform(4000701, 1000701, (200, 350, -80), rot=(0.38268343, 0, 0, 0.92387956), father=4000700, euler=(45,0,0))
lines.append("--- !u!20 &20000701")
lines.append("Camera:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1000701}")
lines.append("  m_Enabled: 1")
lines.append("  serializedVersion: 2")
lines.append("  m_ClearFlags: 1")
lines.append("  m_BackGroundColor: {r: 0.19215687, g: 0.3019608, b: 0.4745098, a: 0}")
lines.append("  m_projectionMatrixMode: 1")
lines.append("  m_GateFitMode: 2")
lines.append("  m_FOVAxisMode: 0")
lines.append("  m_Iso: 200")
lines.append("  m_ShutterSpeed: 0.005")
lines.append("  m_Aperture: 16")
lines.append("  m_FocusDistance: 10")
lines.append("  m_FocalLength: 50")
lines.append("  m_BladeCount: 5")
lines.append("  m_Curvature: {x: 2, y: 11}")
lines.append("  m_BarrelClipping: 0.25")
lines.append("  m_Anamorphism: 0")
lines.append("  m_SensorSize: {x: 36, y: 24}")
lines.append("  m_LensShift: {x: 0, y: 0}")
lines.append("  m_NormalizedViewPortRect:")
lines.append("    serializedVersion: 2")
lines.append("    x: 0")
lines.append("    y: 0")
lines.append("    width: 1")
lines.append("    height: 1")
lines.append("  near clip plane: 1")
lines.append("  far clip plane: 5000")
lines.append("  field of view: 60")
lines.append("  orthographic: 0")
lines.append("  orthographic size: 5")
lines.append("  m_Depth: -1")
lines.append("  m_CullingMask:")
lines.append("    serializedVersion: 2")
lines.append("    m_Bits: 4294967295")
lines.append("  m_RenderingPath: -1")
lines.append("  m_TargetTexture: {fileID: 0}")
lines.append("  m_TargetDisplay: 0")
lines.append("  m_TargetEye: 3")
lines.append("  m_HDR: 1")
lines.append("  m_AllowMSAA: 1")
lines.append("  m_AllowDynamicResolution: 0")
lines.append("  m_ForceIntoRT: 0")
lines.append("  m_OcclusionCulling: 1")
lines.append("  m_StereoConvergence: 10")
lines.append("  m_StereoSeparation: 0.022")
lines.append("--- !u!81 &81000701")
lines.append("AudioListener:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1000701}")
lines.append("  m_Enabled: 1")

go(1000702, "ObserverCameraController", components=[4000702, 1140000772])
transform(4000702, 1000702, (0,0,0), father=4000700)
mono(1140000772, 1000702, G_OBSERVER, "JajuchaSim.App::JajuchaSim.App.ObserverCameraController", {
    "observerCamera": "{fileID: 20000701}",
    "target": "{fileID: 4000301}",
    "chaseHeightCm": "150",
    "chaseDistanceCm": "220",
    "topHeightCm": "450",
})

# ---- _RuntimeUI ----
go(1000800, "_RuntimeUI", components=[4000800])
transform(4000800, 1000800, (0,0,0), children=[4000801, 4000802, 4000803, 4000804, 4000805, 4000806], father=ROOT_T)
for fid, tf, name in [(1000801, 4000801, "MainViewport"), (1000802, 4000802, "MinimalHUD"),
                      (1000803, 4000803, "DebugUI"), (1000805, 4000805, "ScoringUI"),
                      (1000806, 4000806, "TestingUI")]:
    go(fid, name, components=[tf])
    transform(tf, fid, (0,0,0), father=4000800)
go(1000804, "MapEditorUI", components=[4000804, 1140000884])
transform(4000804, 1000804, (0,0,0), father=4000800)
mono(1140000884, 1000804, G_MAP_EDITOR, "JajuchaSim.MapEditor::JajuchaSim.MapEditor.MapEditorHud", {
    "_tileSizeCm": "20",
    "_defaultSaveName": "course.json",
})

# ---- _Services ----
go(1000900, "_Services", components=[4000900])
transform(4000900, 1000900, (0,0,0), children=[4000901, 4000902, 4000903, 4000904], father=ROOT_T)
for fid, tf, name in [(1000901, 4000901, "SaveLoadService"), (1000902, 4000902, "RuntimeFileDialogService"),
                      (1000903, 4000903, "ScreenshotService")]:
    go(fid, name, components=[tf])
    transform(tf, fid, (0,0,0), father=4000900)
go(1000904, "ApplicationShutdownService", components=[4000904, 1140000994])
transform(4000904, 1000904, (0,0,0), father=4000900)
mono(1140000994, 1000904, G_SHUTDOWN, "JajuchaSim.App::JajuchaSim.App.ApplicationShutdownService", {
    "manager": "{fileID: 1140000110}",
    "bridgeServer": "{fileID: 1140000551}",
})

# ---- Root components ----
mono(1140000100, ROOT_GO, G_ERROR_DISPLAY, "JajuchaSim.App::JajuchaSim.App.BootstrapErrorDisplay", {})
mono(1140000101, ROOT_GO, G_STATUS_BAR, "JajuchaSim.App::JajuchaSim.App.RuntimeStatusBar", {
    "bootstrap": "{fileID: 1140000115}"})

# ---- Directional Light ----
go(1001000, "Directional Light", components=[4001000, 108001000])
transform(4001000, 1001000, (0, 300, 0), rot=(0.40821788, -0.23456968, 0.10938163, 0.8754261), euler=(50,-30,0))
lines.append("--- !u!108 &108001000")
lines.append("Light:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1001000}")
lines.append("  m_Enabled: 1")
lines.append("  serializedVersion: 11")
lines.append("  m_Type: 1")
lines.append("  m_Color: {r: 1, g: 0.95686275, b: 0.8392157, a: 1}")
lines.append("  m_Intensity: 1")
lines.append("  m_Range: 10")
lines.append("  m_SpotAngle: 30")
lines.append("  m_InnerSpotAngle: 21.80208")
lines.append("  m_CookieSize: 10")
lines.append("  m_Shadows:")
lines.append("    m_Type: 2")
lines.append("    m_Resolution: -1")
lines.append("    m_CustomResolution: -1")
lines.append("    m_Strength: 1")
lines.append("    m_Bias: 0.05")
lines.append("    m_NormalBias: 0.4")
lines.append("    m_NearPlane: 0.2")
lines.append("    m_CullingMatrixOverride:")
lines.append("      e00: 1")
lines.append("      e01: 0")
lines.append("      e02: 0")
lines.append("      e03: 0")
lines.append("      e10: 0")
lines.append("      e11: 1")
lines.append("      e12: 0")
lines.append("      e13: 0")
lines.append("      e20: 0")
lines.append("      e21: 0")
lines.append("      e22: 1")
lines.append("      e23: 0")
lines.append("      e30: 0")
lines.append("      e31: 0")
lines.append("      e32: 0")
lines.append("      e33: 1")
lines.append("    m_UseCullingMatrixOverride: 0")
lines.append("  m_Cookie: {fileID: 0}")
lines.append("  m_DrawHalo: 0")
lines.append("  m_Flare: {fileID: 0}")
lines.append("  m_RenderMode: 0")
lines.append("  m_CullingMask:")
lines.append("    serializedVersion: 2")
lines.append("    m_Bits: 4294967295")
lines.append("  m_RenderingLayerMask: 1")
lines.append("  m_Lightmapping: 4")
lines.append("  m_LightShadowCasterMode: 0")
lines.append("  m_AreaSize: {x: 1, y: 1}")
lines.append("  m_BounceIntensity: 1")
lines.append("  m_ColorTemperature: 6570")
lines.append("  m_UseColorTemperature: 0")
lines.append("  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}")
lines.append("  m_UseBoundingSphereOverride: 0")
lines.append("  m_UseViewFrustumForShadowCasterCull: 1")
lines.append("  m_ForceVisible: 0")
lines.append("  m_ShadowRadius: 0")
lines.append("  m_ShadowAngle: 0")
lines.append("  m_LightUnit: 1")
lines.append("  m_LuxAtDistance: 1")
lines.append("  m_EnableSpotReflector: 1")

# ---- Ground ----
go(1001100, "Ground", components=[4001100, 33001100, 23001100, 64001100])
transform(4001100, 1001100, (0, -0.5, 0), scale=(500, 1, 500))
lines.append("--- !u!33 &33001100")
lines.append("MeshFilter:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1001100}")
lines.append("  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}")
lines.append("--- !u!23 &23001100")
lines.append("MeshRenderer:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1001100}")
lines.append("  m_Enabled: 1")
lines.append("  m_CastShadows: 1")
lines.append("  m_ReceiveShadows: 1")
lines.append("  m_DynamicOccludee: 1")
lines.append("  m_StaticShadowCaster: 0")
lines.append("  m_MotionVectors: 1")
lines.append("  m_LightProbeUsage: 1")
lines.append("  m_ReflectionProbeUsage: 1")
lines.append("  m_RayTracingMode: 2")
lines.append("  m_RayTraceProcedural: 0")
lines.append("  m_RayTracingAccelStructBuildFlagsOverride: 0")
lines.append("  m_RayTracingAccelStructBuildFlags: 1")
lines.append("  m_SmallMeshCulling: 1")
lines.append("  m_RenderingLayerMask: 1")
lines.append("  m_RendererPriority: 0")
lines.append("  m_Materials:")
lines.append("  - {fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}")
lines.append("  m_StaticBatchInfo:")
lines.append("    firstSubMesh: 0")
lines.append("    subMeshCount: 0")
lines.append("  m_StaticBatchRoot: {fileID: 0}")
lines.append("  m_ProbeAnchor: {fileID: 0}")
lines.append("  m_LightProbeVolumeOverride: {fileID: 0}")
lines.append("  m_ScaleInLightmap: 1")
lines.append("  m_ReceiveGI: 1")
lines.append("  m_PreserveUVs: 0")
lines.append("  m_IgnoreNormalsForChartDetection: 0")
lines.append("  m_ImportantGI: 0")
lines.append("  m_StitchLightmapSeams: 1")
lines.append("  m_SelectedEditorRenderState: 3")
lines.append("  m_MinimumChartSize: 4")
lines.append("  m_AutoUVMaxDistance: 0.5")
lines.append("  m_AutoUVMaxAngle: 89")
lines.append("  m_LightmapParameters: {fileID: 0}")
lines.append("  m_SortingLayerID: 0")
lines.append("  m_SortingLayer: 0")
lines.append("  m_SortingOrder: 0")
lines.append("  m_AdditionalVertexStreams: {fileID: 0}")
lines.append("--- !u!64 &64001100")
lines.append("MeshCollider:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 1001100}")
lines.append("  m_Material: {fileID: 0}")
lines.append("  m_IncludeLayers:")
lines.append("    serializedVersion: 2")
lines.append("    m_Bits: 0")
lines.append("  m_ExcludeLayers:")
lines.append("    serializedVersion: 2")
lines.append("    m_Bits: 0")
lines.append("  m_LayerOverridePriority: 0")
lines.append("  m_IsTrigger: 0")
lines.append("  m_ProvidesContacts: 0")
lines.append("  m_Enabled: 1")
lines.append("  serializedVersion: 5")
lines.append("  m_Convex: 0")
lines.append("  m_CookingOptions: 30")
lines.append("  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}")

# ---- SceneRoots ----
lines.append("--- !u!1660057539 &9223372036854775807")
lines.append("SceneRoots:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_Roots:")
lines.append(f"  - {{fileID: {ROOT_T}}}")
lines.append("  - {fileID: 4001000}")
lines.append("  - {fileID: 4001100}")

with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines) + "\n")
print("Wrote", OUT, os.path.getsize(OUT), "bytes")
