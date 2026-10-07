VENENO Y COLORES DE DAÑO POR ESTADO

Efecto propio integrado automáticamente en CombatAilment.Poison, incluida
la habilidad existente Flecha venenosa. No requiere paquetes externos.

Ajustes:
- Assets/Data/Combat/StatusEffects/StatusEffectPresentation.asset:
  colores de ticks (veneno verde, sangrado rojo, quemadura naranja),
  intensidad del overlay y referencias al material/prefab.
- Assets/Art/Prefabs/Combat/StatusEffects/PoisonAura.prefab:
  neblina verde, matiz violeta y motas cúbicas; 78 partículas máximo.
- Assets/Art/Materials/Combat/StatusEffects y Assets/Art/Shaders/StatusPoison*:
  materiales y shaders URP procedurales, sin texturas importadas.

Uso desde cualquier arma:
CombatAilment.Poison(target, source, familyId, damagePerTick, duration, abilityId, castId);
CombatAilment.Bleed(...) y CombatAilment.Burn(...) comparten la misma firma.
También se puede usar CombatAilment.Apply con StatusEffectType.
Clear(tipo) elimina un estado; ClearAll() limpia todos y la ralentización.

Un tick por segundo, incluido el segundo final. Reaplicar un mismo estado
refresca su duración sin duplicar el aura ni posponer su próximo tick;
el daño y la atribución corresponden a la última aplicación. Diferentes
estados pueden coexistir. Sangrado y quemadura tienen ticks y colores;
el VFX corporal creado en este cambio corresponde al veneno.

Los ticks conservan tipo, arma y habilidad al llegar a Health y al número
flotante. No generan impactos de arma, hit stop, sacudidas ni reacciones de
golpe. Una flecha esquivada no aplica veneno; el veneno ya activo continúa
durante las ventanas defensivas. La pausa detiene su reloj.

El overlay sigue las mallas y huesos originales en una jerarquía cosmética
separada, sin reemplazar materiales ni entrar en las mallas de muerte.
Se limpia al vencer, curar, morir, desactivar o destruir al actor/fuente.
La configuración se referencia desde RuntimeAssetCatalog: Combat/StatusEffects.

Validación:
- checks.txt: regresiones de daño, atribución, colores, estados simultáneos,
  duración, reaplicación, cura, muerte, pooling y fuentes destruidas.
- play-checks.txt: flecha real contra el prefab Goblin, tick/número verde,
  seguimiento del modelo, pausa, expiración y flecha esquivada.
- goblin-play-poison.png: render de cámara en Play Mode del efecto real.
- runtime-compilation.txt: scripts Windows sin UNITY_EDITOR aprobados.
- content-build.txt: bundle construido y vuelto a cargar con sus shaders,
  materiales, perfil y tres sistemas de partículas.
- game-build.txt: build completa Windows Succeeded; registra 1 error del
  verificador por assets previos fuera de categoría. No es una build sin
  incidencias. Detalle de organización en organization.txt; ninguna ruta
  corresponde a los nuevos assets de estados. Ejecutable de validación:
  .validation/StatusEffectGame/Mismo.exe.

Herramientas en Unity: Mismo > Combate > Estados.
Las pruebas de Play Mode usan una escena temporal y restauran las anteriores.
