# Текстуры «Огни в тумане» — заброшенный затопленный город

Набор бесшовных (tileable) текстур 1024×1024, PNG. Для каждой: `_albedo`, `_normal`, `_roughness`, `_height`.
Ivy_leaves — декаль с прозрачностью (альфа в albedo), без height.

| Набор | Назначение |
|---|---|
| concrete_mossy | стены, тротуары, бетонные плиты с мхом и пятнами |
| asphalt_wet | дорога, лужи с низким roughness |
| brick_overgrown | кирпичные фасады с грязью и плесенью |
| rust_metal | кузова машин, ограждения, пожарные лестницы |
| grass_overgrowth | трава и земля, заросшие улицы |
| plaster_peeling | облупленная штукатурка |
| ivy_leaves | плющ/листва на фасады (декаль) |

## Импорт в Unity (URP/HDRP)
1. Положите папку в `Assets/Textures/City/`.
2. `*_normal`: Texture Type → **Normal map**.
3. `*_albedo`: sRGB включён. `*_roughness`, `*_height`: sRGB **выключен**.
4. Unity использует Smoothness, а не Roughness: в URP/Lit положите smoothness в альфа-канал albedo или используйте инвертированный roughness (smoothness = 1 − roughness). В HDRP проще собрать Mask Map.
5. Для `ivy_leaves`: материал Surface Type → Transparent или Alpha Clipping (порог ~0.5).
6. Tiling в материале подбирайте под размер поверхности (например, 2×2 для стены 4 м).

Нормали в формате OpenGL (Y вверх), как требует Unity.
