# 31 — Environment Pipeline: R1 Babilonia (stato corrotto, regione PILOTA)

> **Fonte dei numeri: il codice runtime** (`GreyboxBootstrap`, `GameFlow`,
> `LevelCatalog`), non stime. Documento gemello di Docs/30 (sprite personaggio):
> stessa filosofia, soggetto diverso. Specifica per la generazione ESTERNA dei
> layer di ambientazione. Nessuna modifica al runtime è richiesta in questa fase,
> con l'unica eccezione dichiarata al §6 (contratto `BackgroundDefinition`, che è
> lavoro di una sessione successiva, non di questa).
>
> **Ambito:** R1 Babilonia in stato **corrotto** (Il Risveglio / Via Oscura L1).
> È la **regione pilota**: valida il workflow end-to-end su UN bioma prima di
> scalare agli altri cinque. Riuso a valle: R1 serve anche Romana L6 (Città
> Perduta) e L7 (Tempio) — vedi §4.
>
> **Lingua:** prosa in italiano; nomi di campo, classi, file in inglese.

---

## 1. Viewport di gioco — PPU 16 (allineato al codice, 2026-07-04)

**Ref interno: ~320 × 176 px · PPU 16 · `orthoSize` 5.5.** Ref di lavoro: **320×180**.

La migrazione a PPU 32 / 640×360 è stata **valutata e REVOCATA** (troppi problemi
pratici, beneficio netto insufficiente). Si resta su **PPU 16 · `orthoSize` 5.5**
con le sheet native di Lucius come in gioco oggi (commit 370a00b). Il pixel-perfect
1:1 (Pixel Perfect Camera) resta **disabilitato by design** nel greybox: è un
feel-gate futuro, non parte di questa pipeline.

| Voce | Valore | Derivazione |
|---|---|---|
| PPU | **16** | convenzione di progetto |
| `orthoSize` | 5.5 | `GreyboxBootstrap` |
| Viewport interno | ~**320 × 176 px** (20 × 11 u) | 2 × 5.5 × 16 |
| Ref di lavoro | 320 × 180 | arrotondamento pulito per gli asset |

> **Nota doc:** Docs/30 §1 cita un ref "640×360" incoerente con PPU 16 (a PPU 16
> → ~320×176). È un residuo da correggere in Docs/30 quando ci passi, **non** un
> cambio di pipeline: non riaprire il tema.

Tutte le dimensioni sotto assumono **PPU 16 / ~320×180**.

---

## 2. Spec dimensionale (derivata dal codice)

| Voce | Valore | Derivazione |
|---|---|---|
| PPU | **16** | convenzione di progetto |
| Viewport di gioco | ~**320 × 176 px** (20 × 11 u); ref di lavoro 320×180 | `orthoSize` 5.5 → §1 |
| Piano camminabile (top) | y = **−2.5 u** | `GreyboxBootstrap.GroundTopY` |
| Camera | segue X/Y del player, Z bloccata a −10 | `cameraZ`, `PositionComposer` |
| Larghezza livello (R1) | ~**152 u** (≈ 2432 px @ PPU 16) | arena boss a x=130–152 → il livello arriva lì |
| Colore di fondo attuale (placeholder) | RGB (0.16, 0.17, 0.20) | `cameraBackground` — lo sostituisce lo `sky` layer |
| Tile mondo | **16 × 16 px** = 1 unità | PPU 16 |

**Conseguenza sulla produzione:** il livello è lungo ~2432px. **Nessun layer di
fondo va prodotto full-width.** I layer lontani si producono **tileabili in
orizzontale** (una tile larga ~360–640px che si ripete). Solo il playfield-tileset
copre l'intera lunghezza, ma per composizione di tile 16px, non come immagine unica.

> **Nota TD sul parallax (critica):** la camera è **ortografica**. In ortho la
> profondità Z **non** produce parallax: due layer a Z diverse scrollano identici.
> Il parallax è quindi **interamente da codice** (moltiplicatore di scroll sulla X
> della camera, per layer). La Z serve **solo** all'ordine di rendering
> (`sortingOrder`), mai alla scala. Questo è il motivo per cui serve il componente
> `ParallaxBackground` del §6.

---

## 3. Anatomia dei layer (R1)

Ordine dal fondo verso il giocatore. `scrollX` = frazione del movimento camera che
il layer eredita (0 = fisso, 1 = solidale al mondo). I valori sono un punto di
partenza da tarare a runtime.

| # | Layer | Contenuto (R1 corrotta) | `scrollX` | `sortingOrder` | Tileable H | Dim. consigliata (PPU 16) |
|---|---|---|---|---|---|---|
| 1 | `sky` | gradiente cupo, **luce dal basso** (bagliore del tempio sepolto) | 0.00 | −100 | no | 360 × 220 px (margine per pan Y) |
| 2 | `far` | silhouette dell'architettura babilonese sepolta, bassissimo contrasto | 0.15 | −80 | **sì** | ~512 × 200 px |
| 3 | `mid` | strutture medie, colonne, ziggurat parziali | 0.35 | −60 | **sì** | ~512 × 220 px |
| 4 | `near` | dettaglio dietro il piano di gioco | 0.60 | −40 | **sì** | ~384 × 220 px |
| 5 | `tileset` | **il greybox vestito**: terreno e piattaforme camminabili | 1.00 | 0 | tile 16px | set di tile 16 × 16 px |
| 6 | `props` | detriti, ossa, colonne spezzate, catene (non-collisione) | 1.00 | −10 / +10 | no | sprite singoli |
| 7 | `fg` | occlusione in primo piano (passa davanti al player) | 1.20 | +50 | no/sì | sprite/strisce |
| 8 | `atmo` | nebbia, particellare, bagliore | runtime | +60 | — | **non è un asset**: pass runtime |

Il layer **5 (`tileset`) non è un background**: è la skin del livello che
`GreyboxBootstrap` già costruisce. Va prodotto come **set di tile 16×16** (1 tile
= 1 unità mondo), non come immagine unica, così il codice lo compone sulla
geometria esistente senza toccare la collisione.

---

## 4. Principio "una struttura + due grade" (efficienza di produzione)

Docs/10 §1: ogni regione esiste **intatta** (Romana) e **corrotta** (Oscura); il
gradiente di corruzione *è* la narrazione ambientale. R1 serve **tre** livelli
(Oscura L1 corrotta, Romana L6 e L7 in stato intatto/esterno-tempio).

**Non produrre due ambienti separati.** Produci R1 separando fin dall'inizio:
- **Layer di struttura** (forme, linee, silhouette, disposizione) → **condiviso**
  tra i due stati.
- **Layer di grade** (palette + luce) → **per stato**.

Così il gemello intatto costa un **regrade**, non un rifacimento — lo stesso
principio per cui Lucius era "una REF + animazioni", non un ridisegno per clip.

**Palette corrotta R1 (da Docs/30, bloccata):** bianco osseo `#E8E0D0`,
teal abissale `#1E5854`, viola vuoto `#4A2C5E`, outline scuro `#141018`.
**Massimo 16 colori** totali. Il teal è **accento**, non tinta di fondo (vedi §5).

---

## 5. Regola di leggibilità (GD — fail-gate, non opinione)

Palette Oscura già scura + parallax scuro = **mud**: il giocatore perde il piano
su cui salta. Regola imposta **prima** della produzione:

- Il **playfield giocabile** (layer 5–6, ciò che è solido/interagibile) sta in una
  **fascia di valore alta** e usa il **teal abissale come accento** (bordi
  piattaforme, appigli, hazard).
- I **background** (layer 1–4) stanno in **valori medio-scuri desaturati**. Nessun
  teal saturo sui fondali.
- **Test oggettivo:** screenshot del livello in movimento, convertito in scala di
  grigi. Se il playfield non stacca nettamente dal `mid`/`near`, il livello è
  **illeggibile → fail**. Si rifà il grade, non lo si discute.

---

## 6. Contratto `BackgroundDefinition` (TD — scheletro, non implementazione)

Environment = **configurazione, non fork**, identico a `BossDefinition`. Un nuovo
bioma = un file `BackgroundDefinition`; `GreyboxBootstrap` istanzia i layer dalla
definizione, come già istanzia gli attori. Scheletro del contratto:

```csharp
#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>Data-driven parallax background for one region/state.
    /// Parallax is code-driven (scrollFactor), NOT camera-Z: ortho projection
    /// makes Z irrelevant to scale — it only orders rendering (sortingOrder).</summary>
    [CreateAssetMenu(menuName = "Mehawar/Background Definition")]
    public sealed class BackgroundDefinition : ScriptableObject
    {
        [System.Serializable]
        public sealed class LayerDef
        {
            public string name = "";
            public Sprite? sprite;
            public float scrollFactorX = 1f;   // 0 = fixed, 1 = world-locked
            public float scrollFactorY = 1f;
            public int sortingOrder;
            public bool tileHorizontally;
            public bool tileVertically;
        }

        public Color skyClear = new Color(0.16f, 0.17f, 0.20f);
        public LayerDef[] layers = System.Array.Empty<LayerDef>();
    }
}
```

> **Fuori ambito di questa sessione:** l'implementazione del componente
> `ParallaxBackground` e il wiring in `GreyboxBootstrap` sono lavoro di una
> sessione Claude Code separata, **dopo** che gli asset del pilota sono approvati.
> Questa spec definisce il contratto perché la generazione sappia cosa deve
> alimentare — non lo si costruisce a vuoto.

---

## 7. Split dei tool (workflow in due livelli)

Coerente con la separazione già in uso (Midjourney = concept; produzione = tool
dedicato).

**Livello concept — Midjourney (no asset finali).** Fissa il *look* di R1.
`--sref` per la house style condivisa. Produci **due mood-plate dalla stessa
struttura**: R1 corrotta e R1 intatta — serve a **validare il regrade del §4
prima** di pixellare. Questo è il gate estetico, fuori Unity.

**Livello produzione — split onesto per tipo di asset:**
- Layer **1–3 (`sky`/`far`/`mid`)**: grandi, sfocati, organici, non devono
  combaciare al pixel → **l'AI li regge bene**.
- Layer **5 (`tileset`)** e i `near` tileabili: devono essere **seamless** e a
  **palette bloccata** → **controllo pixel a mano** (Aseprite o equivalente),
  palette importata dai 16 colori del §4. I generatori AI sbagliano proprio la
  coerenza tile-a-tile: un set di tile 16px piccolo si pixella a mano in meno
  tempo di quanto se ne inseguano le versioni AI che non combaciano.

Regola sintetica: **AI per i piani lontani e organici; mano per ciò che deve
combaciare o ripetersi.**

---

## 8. Staging — consegna dei file

Cartella esterna al progetto (come Lucius):
**`~/Mehawar_Art_Staging/Environments/R1_Babilonia/`**

| File | Layer | Formato |
|---|---|---|
| `r1_sky.png` | 1 | PNG RGBA |
| `r1_far.png` | 2 | PNG RGBA, tileabile H |
| `r1_mid.png` | 3 | PNG RGBA, tileabile H |
| `r1_near.png` | 4 | PNG RGBA, tileabile H |
| `r1_tileset.png` | 5 | PNG RGBA, griglia 16×16 |
| `r1_props.png` | 6 | PNG RGBA, sprite singoli |
| `r1_fg.png` | 7 | PNG RGBA |

Tutti a palette ≤16 colori (§4), Filter Point, Compression None, PPU 16 all'import.

---

## 9. Gate PILOTA (cosa deve passare prima di scalare)

R1 è promosso e si passa alle altre regioni **solo se**:
1. **Leggibilità in movimento** a risoluzione di gioco (§5, test scala di grigi) —
   lo stesso gate degli sprite.
2. **Regrade corrotto → intatto** dimostrato su una mood-plate (§4): la stessa
   struttura regge entrambi gli stati.
3. **Montaggio da `BackgroundDefinition`** a runtime, **zero scene editing** (§6).

Passati i tre, gli altri cinque biomi sono ripetizione del workflow, non ricerca.

---

## 10. Fuori ambito (anti scope-creep, un asse per sessione)

- NON implementare `ParallaxBackground` in questa fase (è la sessione successiva).
- NON produrre le altre cinque regioni finché il gate §9 non passa.
- NON formalizzare le palette Romane (intatte) ora: il pilota è **solo** R1
  corrotta. Il gemello intatto di R1 entra come regrade, non come nuovo bioma.
