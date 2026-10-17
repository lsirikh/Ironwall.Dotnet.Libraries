# Map symbol housing

The renderer includes procedural models; no asset download is needed. Enable **지도에 3D 하우징 사용** in the map's **아이콘 등록** palette, then restart the application. The default remains 2D.

```json
{
  "AppSettings": {
    "Symbol3D": {
      "IsEnabled": true,
      "Directory": "C:\\MySymbols3D"
    }
  }
}
```

Settings are read once from the executable directory's `appsettings.json`. The palette saves this section through the existing serialized settings writer. Hosts that rewrite the entire settings file must retain `AppSettings.Symbol3D` in their own settings model. This library does not modify another application's setup model or installer.

The optional asset search order is `Symbol3D.Directory` (otherwise `<app>/Symbols3D`), `<app>/Resources/Symbols3D`, then built-in meshes. Copy OBJ/MTL/JSON assets here to include them in the build output. Camera variants first try `camera.dome.obj` or `camera.ptz.obj`, then `camera.obj` within each directory. Models are cached for the process lifetime; restart after replacing assets.

Available keys: `camera`, `camera.dome`, `camera.ptz`, `sensor`, `multi`, `smartmulti`, `speaker`, `controller`, `iocontroller`, `fence`, `underground`, `contact`, `pir`, `laser`, `radar`, `lamp`, `enclosure`, `fencegate` (통문 — two leaves on `DoorLeft`/`DoorRight` joints, opened by `DoorOpen` 0~1), and `infra.{building-type}` using lowercase enum names.

OBJ defaults to +Z forward, +Y up. Faces support positive/negative indices and convex polygon fan triangulation. Normals are calculated per triangle; texture maps, skeletal animation and concave polygon tessellation are not supported. The same-name MTL file supports `Kd` colors. Materials `mat_body`, `mat_metal`, `mat_trim`, `mat_glass`, `mat_led`, `mat_roof` use the housing palette. Other materials use their MTL color. Invalid/missing assets fall back; more than 500 triangles logs a warning. Files over 8 MiB and meshes over 100,000 triangles are rejected.

An optional same-name JSON file describes the source axes and lens point:

```json
{"front":[0,0,1],"up":[0,1,0],"yawOffset":0,"lens":[0,0.64,0.385]}
```

The camera pitch stays at 35 degrees. Display yaw follows map rotation. The orthographic fit reserves space for all yaw angles, so rendering does not write normalized dimensions into the marker model. For built-in cameras the bracket/body and optical head articulate separately; imported OBJs are single rigid models.

Fill color tints the housing, stroke color/thickness controls the ground ring, and event badges/pulses retain their separate colors. PIDS and infrastructure property panels expose a session-only tint-strength slider. Altitude uses a bounded schematic lift with a support line; building area scales the ground footprint, floors control window bands, basement floors add a dashed footprint, and lock state adds a dashed ring and lock glyph. These are map symbols, not georeferenced architectural meshes; visual floor/elevation ranges are bounded while stored values are retained.

PIDS `DeviceType` remains unchanged. Camera appearance persists as nullable `PidsSymbols.ModelVariant VARCHAR(20)` (`Fixed`, `Dome`, `Ptz`). The existing guarded schema migration adds this column on application startup. JSON copies, DB read/write paths and undo preserve the variant. The implementation session did not run migrations or database integration tests.

Safe verification:

```powershell
dotnet test tests/GMaps.Housing.Tests/GMaps.Housing.Tests.csproj --no-restore
```

Set `SYMBOL3D_ARTIFACTS` to an output directory to capture native WPF gallery/palette images and the offscreen yaw CPU measurement. These tests do not connect to a database. They cover geometry, heading direction, stored-size protection, a transparent hit target hosted in a hidden HWND, fractional map coordinates, material updates, palette search, OBJ parsing, DTO mapping and undo. Full application OLE drag/drop, sustained 500-marker GPU performance and RDP transitions still need validation in the host application; the feature therefore ships disabled by default.
