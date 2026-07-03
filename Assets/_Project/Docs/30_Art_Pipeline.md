# 30 — Art Pipeline: spec sprite Lucius (Fase 1)

> **Fonte dei numeri: il codice runtime** (GreyboxBootstrap, AvatarProfiles,
> ActorAnimationSet, PlaceholderAnimationFactory), non stime. Questo documento
> è la specifica per la generazione ESTERNA degli sprite di Lucius. Nessuna
> modifica al runtime è richiesta o prevista in questa fase.

## 1. Spec dimensionale

| Voce | Valore | Derivazione |
|---|---|---|
| PPU | **16** | convenzione di progetto (Pixel Perfect Camera, ref 640×360) |
| Collider avatar | 0.75 × 1.5 unità | `GreyboxBootstrap.playerSize` |
| **Corpo personaggio** | **12 × 24 px** | 0.75×16 · 1.5×16 |
| Reach attacco dal centro | 21.6 px (1.35 u) | hitbox Lucius: offset 0.8 + metà larghezza 0.55 (`AvatarProfiles.Lucius`) |
| Minimo contenitivo | 48×48 px | punta dell'arma a 21.6 px < 24 (mezzo canvas), margine ~2 px |
| **Canvas RICHIESTO** | **64×64 px** (potenza di 2) | corpo + swing con ~10 px di margine alla punta |
| **Pivot** | **bottom-center (0.5, 0)** | allineato alla base del collider; i piedi toccano il bordo inferiore della cella |
| Altezza personaggio nel canvas | **24 px** (idle, eretto) | il corpo occupa la fascia inferiore; lo spazio sopra/lati è per swing, capelli/mantello, squash&stretch |

## 2. Tabella animazioni (nomi = stati del contratto `ActorAnimationSet`)

Il runtime stira i one-shot sulla durata di gameplay (`fps = frame/durata`,
tempo scalato): le durate qui sotto sono le sorgenti in secondi. I loop girano
al `loopFps` dichiarato nel contratto. Derivazione one-shot a 12 fps:
`frames = ceil(durata × 12)`, minimo 1.

| Stato (clip) | Frame | Durata sorgente | Loop | Note di leggibilità |
|---|---|---|---|---|
| `Idle` | **6** (gate: 4–6) | — | SÌ, ~6 fps | respiro; silhouette a riposo riconoscibile |
| `Run` | **8** (gate: 6–8) | — | SÌ, ~10 fps | ciclo corsa; il gioco corre a 12.5 u/s |
| `Jump` | **2** (salita + apice) | — | SÌ (statici) | pilotato dalla velocità Y del driver |
| `Fall` | **1** (caduta) | — | SÌ (statico) | terzo frame del set salto (vedi §Discrepanze) |
| `Windup` | **1** = ceil(0.08×12) | 0.08 s | no | anticipazione: carica leggibile in un frame |
| `Active` | **2** = ceil(0.10×12) | 0.10 s | no | il colpo: frame di contatto + scia |
| `Recovery` | **3** = ceil(0.18×12) | 0.18 s | no | rientro dell'arma, la fase più lunga |
| `Hurt` | **2** (gate: 1–2) | 0.15–0.25 s (hitstun ricevuto, varia per fonte) | no | alternanza leggibile ANCHE nel fermo-immagine dell'hitstop (0.06 s) |

**Attacco totale: 6 frame** (1+2+3) distribuiti su TRE clip separati — il
contratto tratta windup/active/recovery come stati distinti, non un'unica clip.

**Furia**: NON richiede frame dedicati. È un overlay di tinta (aura) applicato
dal runtime a qualunque stato; inoltre in Furia windup/recovery si accorciano
(÷1.4) e lo stretch dei medesimi frame è automatico.

## 3. Palette — Lucius come l'originale (DECISIONE PRODUCT OWNER 2026-07-03)

- **Lucius mantiene SEMPRE i colori della reference approvata** (SpriteCook
  `eaab4e41`): pelle pallida, cremisi (gonna, polsiere, elmo), viola scuro
  (fascia), **lama del glaive ORO** (elemento più vistoso), cresta bianca.
- La precedente palette "stato corrotto" (bianco osseo / teal / viola) è
  **ABBANDONATA** per gli sprite di Lucius: nessun recolor.
- **Massimo 16 colori** totali.
- ⚠️ **Tensione aperta (Furia)**: l'aura runtime della Furia oggi è una tinta
  rossa (1, .35, .30) — su uno sprite a dominante cremisi è poco leggibile.
  Raccomandazione TD/GD: spostare l'accento Furia su un colore fuori palette
  sprite (es. bianco incandescente o nero-viola) al gate di Fase 3. Decisione
  runtime, zero impatto sulle sheet.

## 4. Formato di export richiesto

- **PNG con trasparenza** (RGBA, niente sfondo).
- **Sprite sheet a griglia fissa uniforme**: tutte le celle **64×64**,
  disposte in riga orizzontale; lo spazio vuoto nelle celle è accettato
  (lo slicing è a griglia, non trim).
- **Una sheet per animazione** (vedi §6 per i nomi).
- Il set salto è un'unica sheet da 3 celle: [salita][apice][caduta].
- L'attacco è un'unica sheet da 6 celle nell'ordine:
  [windup][active1][active2][rec1][rec2][rec3] — lo slicing scriptato
  assegnerà 1/2/3 celle ai tre clip.

## 5. Import Unity previsto (Fase 3, per riferimento)

- Texture Type: Sprite (2D), **Filter: Point**, **Compression: None**,
  **PPU: 16**, mipmap off.
- **Slicing scriptato** (editor script, niente slicing manuale): griglia 64×64,
  pivot **bottom-center** per ogni cella.
- Compilazione di un `ActorAnimationSet` asset per Lucius con i clip della
  tabella §2 (loop e loopFps come indicato; i one-shot senza fps: lo stira il
  runtime).
- ⚠️ Nota integrazione: oggi il corpo greybox è uno sprite 1×1 **stirato dal
  transform** (0.75, 1.5). Gli sprite reali a PPU 16 sono già in proporzione:
  andranno montati senza quella scala (renderer su child a scala 1) — è lavoro
  di Fase 3, segnalato qui perché la spec non lo nasconda.

## 6. Staging — consegna dei file

Cartella esterna al progetto: **`~/Mehawar_Art_Staging/Lucius/`**

| File | Contenuto | Celle |
|---|---|---|
| `lucius_idle.png` | clip Idle | 6 |
| `lucius_run.png` | clip Run | 8 |
| `lucius_jump.png` | salita/apice/caduta (→ clip Jump + Fall) | 3 |
| `lucius_attack.png` | windup/active/recovery (→ 3 clip) | 6 |
| `lucius_hurt.png` | clip Hurt | 2 |

## 7. Discrepanze contratto ↔ set del gate (segnalate, set NON ampliato)

1. **Jump**: il gate chiede "3 frame pilotati da velocità Y"; il contratto
   espone DUE clip distinti (`Jump` loop + `Fall` loop). Risolto in spec:
   sheet unica da 3 celle, slicing 2+1 sui due clip. Nessun cambio runtime.
2. **`Death` è usato dall'avatar** (fade sul respawn, 1.0 s) ma non è nel set
   del gate: il placeholder attuale (dissolvenza) resta il fallback; clip
   dedicata rimandata a un gate successivo.
3. **`Parry`** esiste nel contratto ma per Lucius non scatta mai (è il
   Bastione di Cesare): non serve.
4. **`Telegraph`** non è mai riprodotto dal driver dell'avatar: non serve.
5. **`Fury`** è nel contratto come stato ma il runtime la rende come overlay:
   coperta dallo slot accento riservato (§3), zero frame richiesti.
