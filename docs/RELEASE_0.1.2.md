# VITAL VR 0.1.2 (código 4) — compilar y subir a Meta

## 1. Compilar la APK (30–40 min, en tu PC)

1. `git pull` en la carpeta del proyecto. Cierra Unity.
2. PowerShell en la raíz del proyecto:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-QuestQuick.ps1 -VersionCode 4
   ```

   Usa la llave de `C:\Users\Juan\.vitalvr` (la misma de 0.1.0 y 0.1.1). Resultado:
   `Builds\Android\VITAL-VR-0.1.2-4.apk`. Si falla, el error está al final de `Builds\quest-quick-build.log`.
3. Si el script dice que no encuentra la llave o `LEEME-FIRMA.txt`, usa el script habitual
   `tools/Build-AstraRelease.ps1` (ya preparado para 0.1.2 código 4).

Si Unity da errores de compilación, se ven en el log como `error CS....`: pásalos tal cual.

## 2. Subir a Meta

Developer Dashboard → VITAL VR → Distribution → Release channels → **Alpha** → **Upload new build** →
seleccionar la APK → esperar «Test status: Complete». El cliente ya es miembro del canal Alpha: en sus gafas,
Biblioteca → VITAL VR → Actualizar.

## 3. Qué probar con él (orden recomendado)

1. «Practicar los controles»: caminar con el stick izquierdo hasta el cuadrado verde, girar con el derecho.
2. Mareo de Daniel (gimnasio): tocarle el hombro moviendo la mano; coger el tensiómetro del maletín y
   soltarlo en su brazo; pulsioxímetro en el dedo; fonendoscopio sobre el pecho (se oye el latido);
   mano quieta en su muñeca 4 s (pulso).
3. Paro (cancha): hombros → no responde; acercar la cara a la suya 5 s → no respira; compresiones con
   las dos manos en el pecho y ambos grips; DEA: abrir, encender, quitar plástico de los parches (gatillo
   con el parche en la mano) y soltarlos en el pecho, botón naranja.

Pausa: B / Y. Si el panel queda lejos, B / Y lo vuelve a poner delante.
