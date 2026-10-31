Item mesh optimization - 2026-10-30

Original FBX files, materials, textures and colliders are preserved.
Only MeshFilter references in Player/Item prefabs were replaced.
Reduced static meshes are grouped by item under this folder.
No Network, AI, Core or scene files were edited.

Triangles per complete model (all parts):
Salt: 1,955,408 -> 127,268 (93.5% reduction)
LampOil: 116,144 -> 13,936 (88.0% reduction)
BeanMugwort: 778,266 -> 179,650 (76.9% reduction)
Gong: 797,500 -> 175,802 (78.0% reduction)
Coin: 370,032 -> 52,946 (85.7% reduction)
CraftingItems/Paper: 115,068 -> 11,506 (90.0% reduction)
CraftingItems/Mugwort: 119,640 -> 14,356 (88.0% reduction)

Validation: Unity imports, mesh channels/submesh count/bounds and prefab renderer references checked.
UV0 and sampled original normals retained; tangents recalculated. Original meshes have no UV2/color/blendshape streams.
Front/side/back preview comparisons generated with existing prefab materials.
No build FPS benchmark or multiplayer play test performed.
Texture sizes, material counts and draw calls are not reduced.
Original mesh dependencies can still exist through the original model prefab; this is not a build size optimization.

Excluded: Lantern. Reduction distorted the emissive paper appearance; original remains active.
RegistryTalisman and BarrierTalisman have now been reduced from existing sources; user ZIP remains separate.

QA renders, manifests, scripts and exact prefab backups:
D:/Jun/ItemMeshOptimization_20261030
To restore: copy the backed-up prefabs from backup/Assets/Project/Prefabs/Items to matching paths.
Do not remove these optimized mesh assets while prefab references still point to them.

Second batch - talismans and remaining materials
Talisman/RegistryTalisman: 1,944,062 -> 9,720 triangles
Talisman/BarrierTalisman: 11,149 -> 2,897 triangles
CraftingItems/Beans: 117,955 -> 11,795 triangles
CraftingItems/CoarseSalt: 118,188 -> 23,636 triangles
CraftingItems/Coil: 117,891 -> 23,578 triangles
CraftingItems/CBlood: 118,630 -> 11,862 triangles
Sample strokes verified through TalismanDrawingData / TalismanDrawingSurface and original ink projection shader.
Detailed second-batch QA and backups: D:/Jun/ItemMeshOptimization_TalismansMaterials_20261030
IMPORTANT: 6 Network world prefabs still reference source FBXs. See NETWORK_MESH_HANDOFF.txt; Network files were not edited.
User ZIP inspection: 1,483 vertices / 2,972 triangles, different base-color texture. Not substituted for the authored model.

Third batch - 2026-10-31
Bell: 23,959 -> 14,675 triangles
Mirror: 8,948 -> 4,474 triangles
NoteBook: 757,895 -> 113,866 triangles
YinIncense: 11,040 -> 5,520 triangles
7 local prefabs updated; BellWorldItem Variant inherits the reduced BellVisual.
Original textures/materials/colliders/gameplay preserved. Front/side/back static Unity previews and vertex/bounds/reference checks passed.
Runtime mirror reflections, item VFX, multiplayer and build FPS were not re-tested.
Lantern reduction rejected again: visible emissive UV distortion, source remains active.
ExorcismFlag_01 uses an 80-triangle built-in mesh, not the high-poly source FBX. BarrierFieldMesh (5120 triangles) left intact as a VFX mesh.
Backups and QA: D:\Jun\ItemMeshOptimization_Remaining_20261031
