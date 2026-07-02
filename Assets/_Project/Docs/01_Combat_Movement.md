# 01 — Combat & Movement Spec (Unity 6 / C# port)

Porting della spec v0.1 da GDScript a Unity 6. I **numeri sono in pixel** (coerenti
con la spec originale). Conversione unità Unity: **PPU = 16**, quindi
`unità_Unity = pixel / 16`. Tieni i tunable in pixel nel codice e converti solo
dove serve (o lavora già in unità: vedi nota sotto).

> Regola d'oro del feel: **pesante il personaggio, non i comandi.**
> Peso = momentum/animazione. Reattività = input immediato. Sono cose separate.

---

## 0. Impostazione fisica
- `Rigidbody2D`: `bodyType = Dynamic`, `gravityScale = 0` (gravità custom da codice),
  `freezeRotation = true`, `interpolation = Interpolate`, `collisionDetection =
  Continuous`.
- `BoxCollider2D` per il corpo. Ground check con `Physics2D.BoxCast` o `OverlapBox`
  verso il basso su layer `Ground`.
- Fixed Timestep = `0.0166667` (60 Hz) per salto deterministico.

## 1. Movement (orizzontale)
| Parametro | Valore | Note |
|---|---|---|
| `runSpeedMax` | 200 px/s (12.5 u/s) | velocità di picco |
| `groundAccelTime` | 0.12 s | tempo 0 → max a terra |
| `groundDecelTime` | 0.08 s | tempo max → 0 a terra |
| `airControl` | 0.65 | scala accel/decel in aria |

Deriva accelerazioni da tempo e velocità:
`accel = runSpeedMax / groundAccelTime`, `decel = runSpeedMax / groundDecelTime`.
In aria moltiplica per `airControl`. Muovi verso la velocità target con
`Mathf.MoveTowards` in `FixedUpdate`.

## 2. Jump feel (gravità DERIVATA da altezza + tempo)
| Parametro | Valore | Note |
|---|---|---|
| `jumpApexHeight` | 96 px (6 u) | altezza massima del salto |
| `jumpTimeToApex` | 0.35 s | tempo per raggiungere l'apice |
| `fallGravityMult` | 1.8 | gravità più pesante in caduta |
| `jumpCutMult` | 0.45 | velocità mantenuta al rilascio anticipato |
| `maxFallSpeed` | 900 px/s | terminal velocity |
| `coyoteTime` | 0.10 s | salto tollerato dopo aver lasciato il bordo |
| `jumpBuffer` | 0.12 s | input di salto bufferizzato prima dell'atterraggio |

Formule (calcolate in `Awake`/`OnValidate`, NON hardcodare gravità/velocità):
```
gravity      = (2 * jumpApexHeight) / (jumpTimeToApex * jumpTimeToApex)
jumpVelocity =  gravity * jumpTimeToApex   // = 2*height/time
```
Applicazione:
- Salita: applica `gravity`.
- Caduta (velocity.y < 0): applica `gravity * fallGravityMult` (caduta asimmetrica).
- **Jump cut** (variabile): al rilascio anticipato di Jump mentre `velocity.y > 0`,
  `velocity.y *= jumpCutMult`.
- Clampa la caduta a `maxFallSpeed`.
- **Coyote time**: contatore che parte quando lasci il terreno; salto valido finché
  `> 0`.
- **Jump buffer**: contatore che parte alla pressione di Jump; se tocchi terra entro
  la finestra, salta.

## 3. Melee attack — state machine 3 fasi
| Fase | Durata | Comportamento |
|---|---|---|
| Windup | 0.08 s | anticipo, hitbox OFF |
| Active | 0.10 s | hitbox ON (solo qui) |
| Recovery | 0.18 s | recupero, hitbox OFF, non re-innescabile |

- Hitbox = trigger collider figlio, attivo **solo** in fase Active.
- **No-double-hit guard**: `HashSet<Collider2D>` degli obiettivi già colpiti nello
  swing corrente; svuotalo all'inizio del Windup.
- Al contatto valido: applica danno, **knockback** al bersaglio, **hitstun**, e
  **hitstop** (vedi §4).

## 4. Hitstop (priorità assoluta del combat feel)
Micro-freeze all'impatto: è ciò che dà "peso" a un colpo, più della grafica.
```
// coroutine: freeze poi ripristina
Time.timeScale = 0f;              // o un valore molto basso, es. 0.05f
yield return new WaitForSecondsRealtime(hitstopDuration); // es. 0.06 s
Time.timeScale = 1f;
```
Usa `WaitForSecondsRealtime` (non scala col timeScale). Ripristina sempre a 1.

## 5. "Sete di sangue" (Bloodlust) + "Furia"
- Risorsa che sale colpendo/uccidendo, con **decadimento fuori combattimento**.
- Superata la soglia → stato **Furia**: moltiplicatori di danno e velocità d'attacco
  per una durata limitata.
- Esporre soglia, guadagno per colpo, tasso di decadimento, moltiplicatori come
  `[SerializeField]`.

## 6. Componenti C# (un MonoBehaviour per responsabilità)
- `PlayerMovement` — §1, §2.
- `PlayerCombat` — §3, §4.
- `Bloodlust` — §5.
- `GroundSensor` — ground check condiviso.
- `GreyboxBootstrap` — costruisce la scena di test da codice (terreno + piattaforma +
  spawn) per il gate #1.

## 7. Checklist greybox (in ordine)
1. ☐ Rettangolo che cammina con accel/decel.
2. ☐ Salto: gravità asimmetrica + coyote + buffer + altezza variabile.
3. ☐ Una piattaforma e un dislivello → **il movimento è soddisfacente?** ← GATE #1
4. ☐ Attacco con windup/active/recovery + hitbox.
5. ☐ Un nemico base con AI minima.
6. ☐ Hitstop + knockback + hitstun.
7. ☐ Barra Sete di sangue + soglia Furia + buff danno.
8. ☐ **Combattere è divertente in 30 secondi?** ← GATE #2

> I numeri sono punti di partenza, non verità. Il feel si trova a mano: aspettati di
> ritoccarli decine di volte dall'Inspector.
