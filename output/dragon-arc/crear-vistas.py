"""Assemble the actual Unity capture frames; no generated or simulated imagery."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent
for sequence in ("vuelo", "invocacion"):
    files = sorted((root / sequence).glob("[0-9][0-9][0-9].png"))
    if not files:
        raise RuntimeError(f"Missing Unity frames: {sequence}")
    # A slower rerun may write fewer frames. Exclude leftover frames from older runs.
    started = files[0].stat().st_mtime
    files = [path for path in files if path.stat().st_mtime >= started - 0.05]
    frames = [Image.open(path).convert("RGB") for path in files]
    frames[0].save(root / f"{sequence}.webp", save_all=True, append_images=frames[1:],
                   duration=160, loop=0, quality=88, method=5)
    print(f"{sequence}: {len(frames)} frames captured in Unity")

(root / "vista-previa.html").write_text('''<!doctype html>
<html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>La llamada de Soul Eater</title>
<style>
:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#101918;color:#ede9dc;font:18px/1.6 system-ui}
main{max-width:1100px;margin:auto;padding:48px 24px}h1{font-size:44px;line-height:1.1;color:#e3c67c;margin:12px 0 24px}h2{font-size:26px;margin:28px 0 12px}
p{max-width:850px;color:#c8d0c5}.eyebrow{letter-spacing:.16em;text-transform:uppercase;font-size:13px;color:#b0d28c}img{width:100%;display:block;border:1px solid #455446;border-radius:10px}
figure{margin:24px 0 48px}figcaption{font-size:15px;color:#a7b5aa;padding-top:10px}a{color:#e3c67c}.note{border-left:3px solid #b69450;padding:8px 22px;background:#1c2824}
</style><main><div class="eyebrow">Mismo · Arco del tutorial</div>
<h1>La llamada de Soul Eater</h1>
<p>El dragón sobrevuela el pueblo después de la introducción. Un vigía explica la amenaza y el rey encarga reactivar tres faros, en cualquier orden, para comenzar el ritual.</p>
<h2>Sobrevuelo del pueblo</h2><figure><img src="vuelo.webp" alt="Animación real de Soul Eater volando sobre la entrada del pueblo"><figcaption>Captura de la escena real. Vuelo existente, seguimiento de cámara y devolución del control. Se puede omitir con Esc.</figcaption></figure>
<h2>Invocación desde el altar</h2><figure><img src="invocacion.webp" alt="Soul Eater se aproxima, aterriza y ruge"><figcaption>Aproximación → aterrizaje → rugido → combate. Entrar en el claro no invoca al jefe: hay que activar los tres puntos y confirmar el ritual.</figcaption></figure>
<div class="note"><strong>Pendiente de arte:</strong> el concept y el modelo 3D del faro con llama arriba se dejan para el final. Ya están los tres puntos de activación y su progreso. Sin guardianes.<br><strong>Combate:</strong> Soul Eater tendrá dos fases. El ritual conecta la primera fase existente; la segunda sigue pendiente.</div>
<p>Las animaciones de esta página son capturas del juego. <a href="play.txt">Pruebas del recorrido</a> · <a href="rules.txt">Órdenes y persistencia</a> · <a href="build.txt">Compilación</a></p>
</main></html>''', encoding="utf-8")
