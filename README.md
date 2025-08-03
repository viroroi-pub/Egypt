# Great Pyramid Generator

A Unity‑based procedural tool that recreates the construction of the Great Pyramid of Giza with user‑configurable geometry, physics and ramp‑layout algorithms.

---

## 📂 Project structure

| Path | Description |
| ---- | ----------- |
|      |             |

|   |
| - |

| **Assets/Scenes/SampleScene**         | Main demo scene. Contains an empty **GameObject** with the \`\` script attached.                                               |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| **Assets/Scripts/GeneratePyramid.cs** | Core generator. Exposes all parameters in the Inspector and handles block placement, ramp construction, physics, exports, etc. |
| **Assets/Prefabs/**                   | Stones, wooden sled, Egyptians, vegetation…                                                                                    |
| **Assets/Materials/**                 | Sandstone, wood, floor, corner, etc.                                                                                           |

---

## 🚀 Quick start

1. **Open the project** in Unity 2021 LTS or newer.
2. Load \`\`.
3. Select the **Pyramid** GameObject → **Inspector**.
4. Tweak the exposed fields or press **Play** to generate the full model.

> ℹ️ Generation happens at \`\`; physics updates require Play Mode.

---

## 🔧 Key parameters (GeneratePyramid)

| Category              | Variable                                                                                 | Default     | Meaning                                                                            |
| --------------------- | ---------------------------------------------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------- |
| **Geometry**          | `BaseSize`                                                                               | 230 m       | Pyramid side length at ground.                                                     |
|                       | `Height`                                                                                 |  147 m      | Target apex height. Adjusted to nearest block layer (0.71 m each).                 |
|                       | `PyramidInclination`                                                                     |  51°        | Face angle of the pyramid.                                                         |
|                       | `RampInclination`                                                                        |  7°         | Edge‑ramp slope.                                                                   |
|                       | `blockwide`                                                                              |  1.27 m     | Block footprint. *(private const)*                                                 |
| **Physics**           | `massBlock`                                                                              |  2267.96 kg | Average limestone block mass.                                                      |
|                       | `frictionCoef`                                                                           |  0.7 (μ)    | Trineo/ground static friction.                                                     |
| **Ramp modes**        | `Method4Ramp` / `Method8Ramp` / `Method16Ramp`                                           | bool        | Enable 4‑, 8‑ or 16‑ramp layouts.                                                  |
|                       | `MethodInsideRamp`                                                                       | bool        | Shift ramps inwards (edge‑protected).                                              |
| **Visual toggles**    | `DrawWall`, `DrawFloor`, `DrawWoodenCyl`, `DrawEgyptians`, `DrawGranite`, `DrawCover`, … | bool        | Enable/disable cosmetic details.                                                   |
| **Simulation limits** | `maxBlocks`                                                                              | 100         | Hard cap during debugging (set 0 for full 2.3 M blocks).                           |
| **Export**            | `exportPyramidObj`                                                                       | false       | Export generated mesh to **OBJ** (`Application.persistentDataPath/PyramidModels`). |

Full list of public fields is available in the source code.

---

## 🏃‍♀️ Typical pipelines

### Build a half‑pyramid for performance

```csharp
GeneratePyramid gp = GetComponent<GeneratePyramid>();
gp.halfPyramid = true;
gp.DrawGranite = false;   // skip interior details
```

### Switch to the 16‑8‑4 ramp strategy

```csharp
gp.Method16Ramp = true;   // rows 1‑9 (16 ramps)
gp.Method8Ramp  = true;   // rows 10‑20 (8 ramps)
gp.Method4Ramp  = true;   // rows 21+  (4 ramps)
```

---

## 📤 OBJ export

Set \`\` and press **Play**.\
The mesh (optionally combined) is written to:

```
%APPDATA%\…\<Project>\PyramidModels\MyExportedPyramid.obj
```

You have zipped samples in the OBJRepository folder:
- Pyramid No Ramps
- Pyramid 1 Ramp
- Pyramid 4 Ramps

Change `exportSubFolder` and `outputFileName` to customise the path.

---

## 📝 License

This Unity implementation is released under the **MIT License**.\
Please cite the accompanying paper *“A Computationally Validated Integrated Edge‑Ramp Theory …”* if you use this code in academic work.

