# Texturas de los modelos de gimnasio

Los GLB conservan su ruta y GUID, mallas, coordenadas UV, materiales, canales
metálico/rugosidad/oclusión, escala de normales y parámetros de muestreo. Sus imágenes
se referencian mediante URI externa para que glTFast use `TextureImporter`, en lugar
de producir subassets RGBA sin compresión.

`python tools/Externalize-GymTextures.py` extrae los bytes PNG/JPEG originales, sin
recodificarlos, a `Assets/ThirdParty/GymModels/Textures/`. El nombre contiene SHA256 y
el uso de color, normal o datos; las imágenes idénticas con igual uso comparten
archivo. Reempaqueta los buffer views referenciados y elimina las imágenes del BIN.
El script comprueba que conserva los bytes de geometría y es idempotente. Para una
verificación sin escribir: `python tools/Externalize-GymTextures.py --check`.

`sources.json` mantiene autores, licencias y enlaces; `originalSha256` identifica la
descarga y `sha256` el GLB derivado. La atribución distribuida está en
`Assets/StreamingAssets/ThirdParty/GymModels-CC-BY.txt`.

`QuestGymTextureSetup` configura color y datos a 1024 como máximo y normales a 512,
con mipmaps, sin copia
legible de CPU, ASTC 6x6 en Android y compresión nativa en Standalone. Las normales
se importan como NormalMap; color y emisión en sRGB; los mapas de datos en lineal.
La reducción de las 19 normales conserva colores y canales metálico/rugosidad/AO
de 1K; ahorra aproximadamente 19 MiB en Windows frente a normales DXT5 de 1K.
Las imágenes originales y sus hashes se conservan sin cambios.
`VisualMaterialsSetup.Ensure()` aplica la política antes de reimportar GLB y falla
si queda alguna textura incrustada. Este flujo se ejecuta antes del horneado.

Validación disponible: prueba binaria `python tools/test_gym_texture_extraction.py`,
comparación de los 18 GLB con sus fuentes en
`TestResults/astra/gym-texture-extraction.json` y prueba Unity
`QuestGymTextureTests`, que revisa las referencias reales de los materiales.
La prueba Unity debe ejecutarse en el laboratorio después de sincronizar assets.

Para la copia al laboratorio, la lista completa de modelos, imágenes, metas y
fuentes está en `TestResults/astra/gym-texture-sync-files.json`. La copia habitual
de Scripts/Editor no incluye ThirdParty. El próximo build deberá volver a hornear:
también se añaden hormigón CC0 en las gradas e interiores de escaparate emisivos.
La ilustración interior es original y se genera de forma determinista a 512×384
mediante `VisualMaterialsSetup`; no contiene fotografías ni contenido de terceros.
