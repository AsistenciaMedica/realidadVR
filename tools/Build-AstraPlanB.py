#!/usr/bin/env python3
"""Build an honest 180-second editorial video and local before/after gallery.

Requires Pillow and local ffmpeg/ffprobe executables. Does not download tools,
launch Unity, capture a headset, or substitute baseline pictures for final ones.

Example:
  python tools/Build-AstraPlanB.py --after TestResults/astra/99-despues \
    --ffmpeg .utmp/tools/ffmpeg-9.0.2/ffmpeg.exe --output TestResults/astra/plan-b
Use --validate-only first, or --preview-only to inspect all 18 title cards.
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from fractions import Fraction

from PIL import Image, ImageDraw, ImageFont, ImageOps, __version__ as pillow_version


PROJECT = Path(__file__).resolve().parent.parent
WIDTH, HEIGHT, FPS = 1920, 1080, 30
SECONDS_PER_SLIDE, SLIDE_COUNT = 10, 18
TOTAL_FRAMES = FPS * SECONDS_PER_SLIDE * SLIDE_COUNT
BACKGROUND = (8, 19, 29)
INK, SOFT, ACCENT = (239, 246, 247), (165, 188, 197), (72, 216, 186)
LIMITATION = "Galería de capturas · simulación en Windows · sin visor"


@dataclass(frozen=True)
class Slide:
    image: str
    chapter: str
    title: str
    copy: str


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source_record(path: Path, role: str) -> dict:
    return {"path": str(path.resolve()), "role": role, "bytes": path.stat().st_size, "sha256": sha256(path)}


def timestamp(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def roster(directory: Path) -> dict:
    report = read_json(directory / "patient-roster-smoke.json")
    patients = report.get("patients", [])
    if report.get("result") != "PASS" or report.get("questLookSimulation") is not True:
        raise ValueError(f"{directory}: se requiere un roster PASS generado con -vital-quest-look.")
    if report.get("headsetTested") is not False or len({patient["scenarioId"] for patient in patients}) != 15:
        raise ValueError(f"{directory}: el informe debe declarar sin visor y los quince casos distintos.")
    return report


def case_image(report: dict, case: str, view: str) -> str:
    patient = next((entry for entry in report["patients"] if entry["scenarioId"] == case), None)
    if patient is None:
        raise ValueError(f"No aparece el caso {case} en el roster.")
    return patient["screenshotPrefix"] + "-" + view + ".png"


def storyboard(report: dict, directory: Path) -> list[Slide]:
    daniel = lambda view: case_image(report, "review-hypotension-v2", view)
    andres = lambda view: case_image(report, "arrest-witnessed", view)
    controls = "00-tutorial.png" if (directory / "00-tutorial.png").is_file() else "00-welcome.png"
    return [
        Slide("00-welcome.png", "01 · Bienvenida", "VITAL VR", "Un recorrido visual de respaldo para la reunión. Las imágenes proceden de la captura final de la aplicación en modo Quest-look."),
        Slide("00-environments.png", "02 · Elige un entorno", "Tres entornos. Quince pacientes.", "Gimnasio, centro comercial y campo de fútbol. Cada caso empieza con una preparación y termina con una revisión."),
        Slide(controls, "03 · Instrucciones de control", "Apunta. Selecciona. Practica.", "Gatillo: seleccionar. Grip: coger o soltar. B / Y: centrar y abrir la pausa. Esta tarjeta explica los controles; no muestra gestos grabados."),
        Slide(daniel("briefing"), "04 · Daniel / preparación", "La situación, antes de empezar", "El briefing presenta al paciente y permite elegir cómo practicar. El recorrido de capturas prueba la interfaz; no representa una intervención clínica completada."),
        Slide(daniel("session"), "05 · Daniel / sesión", "Observar y acompañar", "Vista de la aplicación durante el caso. La información clínica debe obtenerse mediante las interacciones disponibles."),
        Slide(daniel("patient"), "06 · Daniel / presentación", "Una persona en su entorno", "Vista de inspección del personaje: la interfaz se ocultó para revisar su presentación. No es una grabación de la vista operativa del alumno."),
        Slide(daniel("conversation"), "07 · Daniel / conversación", "Preguntar y escuchar", "La captura muestra el espacio de conversación y la información obtenida. El vídeo conserva imágenes estáticas del recorrido."),
        Slide(daniel("pause"), "08 · Daniel / pausa", "Parar sin perder el hilo", "El menú de pausa permite continuar o cerrar el intento. La imagen muestra la pantalla; las pruebas automáticas verifican por separado el reloj y el audio."),
        Slide(daniel("debrief"), "09 · Daniel / revisión", "Revisar también lo pendiente", "Este debrief procede de un intento de captura, sin completar el tratamiento. Sus omisiones no deben interpretarse como el resultado de una demostración clínica."),
        Slide(andres("briefing"), "10 · Andrés / preparación", "Otro paciente, otro entorno", "La segunda parte del recorrido presenta a Andrés en el campo de fútbol y la preparación del caso de parada con DEA."),
        Slide(andres("session"), "11 · Andrés / sesión", "La escena de entrenamiento", "Vista real de la aplicación durante el caso. Este montaje no simula compresiones, colocación de parches ni descargas ejecutadas."),
        Slide(andres("patient"), "12 · Andrés / presentación", "Revisar el contexto", "Vista de inspección del paciente y su entorno, con la interfaz oculta. El movimiento de la imagen es únicamente editorial."),
        Slide(andres("conversation"), "13 · Andrés / testigo", "Escuchar a quien estaba allí", "Cuando el paciente no puede responder, el testigo aporta contexto. Se muestra la conversación recogida en la captura final."),
        Slide(andres("pause"), "14 · Andrés / pausa", "Controles siempre disponibles", "Continuar, finalizar y revisar. Las pantallas de este vídeo son capturas de la aplicación; no una prueba física con el visor."),
        Slide(andres("debrief"), "15 · Andrés / revisión", "Una oportunidad para aprender", "La revisión enumera lo registrado y lo omitido en este intento de captura. No acredita una técnica clínica ni el rendimiento en Quest."),
        Slide("00-catalog-mall.png", "16 · Centro comercial", "Más situaciones para practicar", "El catálogo del centro comercial completa los tres entornos del producto. El contenido clínico permanece sujeto a revisión."),
        Slide("00-catalog-football.png", "17 · Campo de fútbol", "Elegir el siguiente caso", "La galería antes/después que acompaña este vídeo permite comparar las capturas originales y finales, con sus datos de procedencia."),
        Slide("00-welcome.png", "18 · Preparados para la reunión", "El siguiente paso es el visor", "Respaldo editorial de tres minutos. La experiencia física y los FPS de Quest todavía requieren una prueba en el dispositivo."),
    ]


def verify_capture(directory: Path, filename: str) -> dict:
    path = directory / filename
    metadata_path = path.with_suffix(".capture.json")
    if not path.is_file() or not metadata_path.is_file():
        raise ValueError(f"Falta la captura final o su metadata: {path}")
    metadata = read_json(metadata_path)
    if (metadata.get("width"), metadata.get("height")) != (2064, 2208):
        raise ValueError(f"{filename}: resolución distinta de 2064×2208.")
    if metadata.get("desktopPath") is not False or metadata.get("headsetTested") is not False or metadata.get("canvasMode") != "WorldSpace":
        raise ValueError(f"{filename}: no es una captura declarada del camino VR en simulación.")
    if not str(metadata.get("renderPipeline", "")).startswith("QuestURP") or abs(metadata.get("verticalFieldOfView", 0) - 100) > .1:
        raise ValueError(f"{filename}: no usa QuestURP / FOV 100.")
    with Image.open(path) as image:
        if image.size != (2064, 2208):
            raise ValueError(f"{filename}: la imagen no coincide con la resolución declarada.")
        image.verify()
    return metadata


def comparisons(before: dict, after: dict) -> list[tuple[str, str, str]]:
    pairs = [("Bienvenida", "00-welcome.png", "00-welcome.png"),
             ("Selección de entorno", "00-environments.png", "00-environments.png")]
    for title, case, view in [
        ("Daniel · preparación", "review-hypotension-v2", "briefing"),
        ("Daniel · sesión", "review-hypotension-v2", "session"),
        ("Daniel · personaje", "review-hypotension-v2", "patient"),
        ("Andrés · sesión", "arrest-witnessed", "session"),
        ("Andrés · personaje", "arrest-witnessed", "patient"),
        ("Andrés · revisión", "arrest-witnessed", "debrief"),
    ]:
        pairs.append((title, case_image(before, case, view), case_image(after, case, view)))
    return pairs


def fonts(font_path: str | None) -> dict:
    candidates = [Path(font_path)] if font_path else [Path("C:/Windows/Fonts/segoeui.ttf"), Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")]
    regular = next((path for path in candidates if path.is_file()), None)
    if regular is None:
        raise ValueError("No se encontró una tipografía legible; proporciona --font /ruta/fuente.ttf.")
    bold = regular.with_name("segoeuib.ttf") if regular.name.lower() == "segoeui.ttf" else regular
    if not bold.exists():
        bold = regular
    return {"path": regular, "bold_path": bold, "brand": ImageFont.truetype(str(bold), 37),
            "title": ImageFont.truetype(str(bold), 66), "copy": ImageFont.truetype(str(regular), 33),
            "chapter": ImageFont.truetype(str(bold), 25), "small": ImageFont.truetype(str(regular), 23)}


def wrapped(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.FreeTypeFont, width: int) -> list[str]:
    result = []
    for paragraph in text.split("\n"):
        current = ""
        for word in paragraph.split():
            candidate = word if not current else current + " " + word
            if current and draw.textlength(candidate, font=font) > width:
                result.append(current)
                current = word
            else:
                current = candidate
        result.append(current)
    return result


def draw_text_block(draw: ImageDraw.ImageDraw, text: str, x: int, y: int, width: int, font: ImageFont.FreeTypeFont, fill: tuple, spacing: int) -> int:
    for line in wrapped(draw, text, font, width):
        draw.text((x, y), line, font=font, fill=fill)
        y += spacing
    return y


def prepare_slide(slide: Slide, index: int, after: Path, font: dict) -> tuple[Image.Image, Image.Image]:
    base = Image.new("RGB", (WIDTH, HEIGHT), BACKGROUND)
    draw = ImageDraw.Draw(base)
    draw.rounded_rectangle((924, 108, 1850, 982), radius=28, fill=(16, 34, 46))
    draw.text((78, 42), "VITAL VR", font=font["brand"], fill=INK)
    draw.text((78, 185), slide.chapter.upper(), font=font["chapter"], fill=ACCENT)
    end = draw_text_block(draw, slide.title, 78, 254, 776, font["title"], INK, 80)
    end = draw_text_block(draw, slide.copy, 78, max(end + 36, 472), 776, font["copy"], SOFT, 47)
    if end > 938:
        raise ValueError(f"La tarjeta {index + 1} desborda: ajusta su texto antes de generar el vídeo.")
    draw.text((78, 954), f"{index + 1:02d} / {SLIDE_COUNT:02d}  ·  {index * 10 // 60:02d}:{index * 10 % 60:02d}", font=font["small"], fill=ACCENT)
    with Image.open(after / slide.image) as original:
        # The complete original frame remains inside the panel throughout the motion.
        photo = ImageOps.contain(original.convert("RGB"), (854, 810), Image.Resampling.LANCZOS)
    return base, photo


def render_frame(base: Image.Image, photo: Image.Image, frame: int, slide_index: int, font: dict) -> Image.Image:
    within = FPS * SECONDS_PER_SLIDE
    t = frame / (within - 1)
    eased = t * t * (3 - 2 * t)
    scale = 1 + .018 * eased
    scaled = photo.resize((round(photo.width * scale), round(photo.height * scale)), Image.Resampling.BICUBIC)
    image = base.copy()
    drift = math.sin((eased - .5) * math.pi) * 7
    x = round(1387 - scaled.width / 2 + drift)
    y = round(545 - scaled.height / 2 - drift * .5)
    image.paste(scaled, (x, y))
    fade = min(1.0, frame / 12, (within - 1 - frame) / 12)
    if fade < 1:
        image = Image.blend(Image.new("RGB", (WIDTH, HEIGHT), BACKGROUND), image, max(0.0, fade))
    draw = ImageDraw.Draw(image)
    draw.text((78, 1017), LIMITATION, font=font["small"], fill=INK)
    draw.line((78, 1002, 1842, 1002), fill=(38, 60, 73), width=2)
    progress = (slide_index * within + frame + 1) / TOTAL_FRAMES
    draw.line((78, 1002, 78 + round(1764 * progress), 1002), fill=ACCENT, width=3)
    return image


def gallery(before: Path, after: Path, output: Path, pairs: list, metadata: dict) -> list[dict]:
    cards, records = [], []
    for title, old, new in pairs:
        figures = []
        for role, directory, filename in (("before", before, old), ("after", after, new)):
            destination = output / "images" / role / filename
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(directory / filename, destination)
            shutil.copyfile((directory / filename).with_suffix(".capture.json"), destination.with_suffix(".capture.json"))
            records.append(source_record(destination, "gallery-" + role))
            label = "Antes · línea base" if role == "before" else "Después · captura final"
            relative = destination.relative_to(output).as_posix()
            metadata_link = Path(relative).with_suffix(".capture.json").as_posix()
            figures.append(f'<figure><figcaption>{label}</figcaption><a href="{html.escape(relative)}"><img loading="lazy" src="{html.escape(relative)}" alt="{html.escape(title + " — " + label)}"></a><a class="metadata" href="{html.escape(metadata_link)}">Datos de captura</a></figure>')
        old_meta = metadata[str((before / old).resolve())]
        new_meta = metadata[str((after / new).resolve())]
        different_pose = any(old_meta.get(key) != new_meta.get(key) for key in ("cameraPosition", "cameraEulerAngles"))
        note = "Los puntos de vista son distintos; no es una comparación píxel a píxel." if different_pose else "Misma pose de cámara declarada; se conserva el encuadre completo de cada captura."
        cards.append(f'<section><h2>{html.escape(title)}</h2><p>{note}</p><div class="pair">{"".join(figures)}</div></section>')
    document = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>VITAL VR · antes y después</title><style>
body{margin:0;background:#08131d;color:#eff6f7;font:18px/1.6 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:36px}h1{font-size:clamp(30px,4vw,56px);margin-bottom:12px}h2{font-size:28px}p{max-width:1000px;color:#a5bcc5}a{color:#48d8ba}header{padding-bottom:30px;border-bottom:1px solid #263c49}.notice{padding:16px 22px;background:#10222e;border-left:4px solid #48d8ba}section{margin:50px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:22px}figure{margin:0;background:#10222e;padding:16px;border-radius:14px}figcaption{font-size:22px;font-weight:650;margin:0 0 12px}img{width:100%;height:auto;display:block}.metadata{font-size:14px;display:block;padding-top:12px}video{width:100%;max-width:1100px;display:block;margin:24px 0}code{overflow-wrap:anywhere}@media(max-width:750px){main{padding:20px}.pair{grid-template-columns:1fr}}
</style><main><header><h1>VITAL VR · antes y después</h1><p class="notice">Capturas reales de la aplicación en <b>simulación Quest-look en Windows</b>. Sin prueba física en el visor, sin estereoscopía y sin medición de FPS de Quest.</p><p>Las imágenes originales están completas. Los retratos son vistas de inspección con la interfaz oculta; las sesiones muestran la vista operativa. El vídeo es un montaje editorial de imágenes estáticas: no acredita procedimientos ejecutados.</p><p><a href="manifest.json">Procedencia, tiempos y SHA-256</a> · <a href="storyboard.json">Guion de los 18 capítulos</a> · <a href="vital-vr-plan-b.mp4">Vídeo de respaldo · 3 minutos</a></p><video controls preload="metadata" src="vital-vr-plan-b.mp4">El vídeo se genera después de revisar las tarjetas.</video></header>'''
    (output / "index.html").write_text(document + "".join(cards) + "</main></html>", encoding="utf-8")
    return records


def executable(explicit: str | None, name: str, sibling: Path | None = None) -> Path:
    choices = [Path(explicit)] if explicit else []
    if sibling:
        choices.extend((sibling / (name + ".exe"), sibling / name))
    found = shutil.which(name)
    if found:
        choices.append(Path(found))
    for candidate in choices:
        if candidate.is_file():
            return candidate.resolve()
    raise ValueError(f"No se encontró {name}. Proporciona --{name} /ruta/{name}; no se descarga automáticamente.")


def encode(slides: list[Slide], after: Path, output: Path, font: dict, ffmpeg: Path, ffprobe: Path) -> tuple[dict, list]:
    partial = output / "vital-vr-plan-b.partial.mp4"
    video = output / "vital-vr-plan-b.mp4"
    command = [str(ffmpeg), "-hide_banner", "-loglevel", "warning", "-y", "-f", "rawvideo", "-pixel_format", "rgb24",
               "-video_size", f"{WIDTH}x{HEIGHT}", "-framerate", str(FPS), "-i", "pipe:0", "-an", "-c:v", "libx264",
               "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-frames:v", str(TOTAL_FRAMES),
               "-movflags", "+faststart", "-metadata", "title=VITAL VR - galeria editorial de capturas simuladas, sin visor", str(partial)]
    with (output / "ffmpeg.log").open("wb") as log:
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=log)
        try:
            for index, slide in enumerate(slides):
                base, photo = prepare_slide(slide, index, after, font)
                print(f"Render {index + 1:02d}/{SLIDE_COUNT}: {slide.chapter}", flush=True)
                for frame in range(FPS * SECONDS_PER_SLIDE):
                    process.stdin.write(render_frame(base, photo, frame, index, font).tobytes())
            process.stdin.close()
            code = process.wait()
            if code:
                raise RuntimeError(f"ffmpeg falló con código {code}; revisar {output / 'ffmpeg.log'}.")
        except BaseException:
            process.kill()
            process.wait()
            raise
    probe_command = [str(ffprobe), "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries",
                     "stream=codec_name,width,height,r_frame_rate,nb_read_frames,duration:format=duration", "-of", "json", str(partial)]
    probe = json.loads(subprocess.check_output(probe_command, text=True, encoding="utf-8"))
    stream = probe["streams"][0]
    if int(stream["nb_read_frames"]) != TOTAL_FRAMES or Fraction(stream["r_frame_rate"]) != FPS:
        raise RuntimeError("El vídeo no tiene exactamente 5400 fotogramas a 30 fps; se conserva como partial.mp4.")
    if abs(float(probe["format"]["duration"]) - 180.0) > .000001 or (stream["width"], stream["height"]) != (WIDTH, HEIGHT):
        raise RuntimeError("Duración/resolución incorrecta; no se publica el archivo final.")
    os.replace(partial, video)
    (output / "ffprobe.json").write_text(json.dumps(probe, indent=2), encoding="utf-8")
    return probe, command


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--after", required=True, type=Path, help="Directorio de capturas finales Quest-look PASS, posterior a la línea base.")
    parser.add_argument("--before", type=Path, default=PROJECT / "TestResults/astra/00-antes")
    parser.add_argument("--output", type=Path, default=PROJECT / "TestResults/astra/plan-b")
    parser.add_argument("--ffmpeg")
    parser.add_argument("--ffprobe")
    parser.add_argument("--font", help="Fuente TTF, si no se dispone de Segoe UI o DejaVu Sans.")
    parser.add_argument("--validate-only", action="store_true", help="Valida fuentes y cronología; no escribe artefactos.")
    parser.add_argument("--preview-only", action="store_true", help="Genera tarjetas PNG, galería y manifiesto; no necesita ffmpeg.")
    parser.add_argument("--overwrite", action="store_true", help="Permite regenerar este directorio de salida.")
    args = parser.parse_args()
    before, after, output = args.before.resolve(), args.after.resolve(), args.output.resolve()
    if after == before or "00-antes" in after.parts or after.name.lower() == "baseline":
        raise ValueError("La línea base no puede utilizarse como captura final.")
    if any(output.is_relative_to(source) or source.is_relative_to(output) for source in (before, after)):
        raise ValueError("La salida debe ser un directorio independiente; no puede contener ni reemplazar las fuentes.")
    old_report, new_report = roster(before), roster(after)
    if timestamp(new_report["capturedUtc"]) <= timestamp(old_report["capturedUtc"]):
        raise ValueError("La captura final debe ser posterior a la línea base.")
    slides = storyboard(new_report, after)
    if len(slides) != SLIDE_COUNT:
        raise ValueError("El guion debe contener exactamente 18 capítulos de diez segundos.")
    pairs = comparisons(old_report, new_report)
    inputs = {(after / slide.image).resolve(): "final-video" for slide in slides}
    for _, old, new in pairs:
        inputs[(before / old).resolve()] = "baseline-comparison"
        inputs.setdefault((after / new).resolve(), "final-comparison")
    metadata = {str(path): verify_capture(path.parent, path.name) for path in inputs}
    for path, role in inputs.items():
        if role.startswith("final") and timestamp(metadata[str(path)]["capturedUtc"]) < timestamp(new_report["capturedUtc"]):
            raise ValueError(f"{path.name}: captura anterior al inicio del recorrido final; no se aceptan archivos residuales.")
    if all(sha256(before / old) == sha256(after / new) for _, old, new in pairs):
        raise ValueError("Todas las capturas finales coinciden con la línea base; no se aceptan copias como evidencia nueva.")
    print(f"Fuentes verificadas: {len(inputs)} imágenes · 15 pacientes · {SLIDE_COUNT} capítulos · 180 s · sin visor", flush=True)
    if args.validate_only:
        return 0
    if output.exists() and any(output.iterdir()) and not args.overwrite:
        raise ValueError("La salida ya contiene archivos. Usa otro directorio o --overwrite para regenerarla.")
    font = fonts(args.font)
    ffmpeg = ffprobe = None
    if not args.preview_only:
        ffmpeg = executable(args.ffmpeg, "ffmpeg")
        ffprobe = executable(args.ffprobe, "ffprobe", ffmpeg.parent)
    output.mkdir(parents=True, exist_ok=True)
    (output / "manifest.json").write_text(json.dumps({"status": "BUILDING", "headsetTested": False,
                                                      "sourceCaptureRun": new_report["capturedUtc"]}, indent=2), encoding="utf-8")
    # --overwrite authorizes only replacing this tool's own named outputs.
    # Remove an older encoded video so a failed rerun cannot display it as new evidence.
    for stale in ("vital-vr-plan-b.mp4", "ffprobe.json"):
        (output / stale).unlink(missing_ok=True)
    preview = output / "cards"
    preview.mkdir(exist_ok=True)
    for index, slide in enumerate(slides):
        base, photo = prepare_slide(slide, index, after, font)
        render_frame(base, photo, FPS * SECONDS_PER_SLIDE // 2, index, font).save(preview / f"{index + 1:02d}.png")
    timeline = [dict(asdict(slide), startSeconds=index * 10, durationSeconds=10) for index, slide in enumerate(slides)]
    (output / "storyboard.json").write_text(json.dumps(timeline, ensure_ascii=False, indent=2), encoding="utf-8")
    gallery_records = gallery(before, after, output, pairs, metadata)
    probe = command = None
    if not args.preview_only:
        probe, command = encode(slides, after, output, font, ffmpeg, ffprobe)
    manifest = {"schemaVersion": 1, "createdUtc": datetime.now(timezone.utc).isoformat(),
                "status": "PREVIEW_ONLY" if args.preview_only else "VERIFIED_180_SECONDS",
                "description": "Editorial slideshow from final Windows Quest-look screenshots. No recorded procedures or headset performance claim.",
                "headsetTested": False, "questFpsMeasured": False, "durationSeconds": 180, "videoFps": FPS,
                "videoFrameCount": TOTAL_FRAMES, "resolution": [WIDTH, HEIGHT], "audio": "Silent; no fabricated conversation or procedure audio.",
                "software": {"python": sys.version, "pillow": pillow_version},
                "sourceCaptureRun": new_report["capturedUtc"], "baselineCaptureRun": old_report["capturedUtc"],
                "script": source_record(Path(__file__), "generator"),
                "fonts": [source_record(font["path"], "regular-font"), source_record(font["bold_path"], "bold-font")],
                "inputs": [dict(source_record(path, role), metadata=metadata[str(path)], metadataSha256=sha256(path.with_suffix(".capture.json"))) for path, role in sorted(inputs.items())],
                "reports": [source_record(before / "patient-roster-smoke.json", "baseline-report"), source_record(after / "patient-roster-smoke.json", "final-report")],
                "galleryImages": gallery_records, "ffmpegCommand": command, "probe": probe}
    if ffmpeg:
        manifest["ffmpeg"] = source_record(ffmpeg, "encoder")
        manifest["ffprobe"] = source_record(ffprobe, "verification-tool")
        manifest["video"] = source_record(output / "vital-vr-plan-b.mp4", "final-video")
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Listo: {output / 'index.html'} · {manifest['status']}", flush=True)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, KeyError, StopIteration, RuntimeError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(2)
