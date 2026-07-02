# CLAUDE.md — MehawarTest (Unity 6)

> File di contesto per Claude Code. Leggilo a ogni sessione insieme a `Docs/`.
> Codice e commenti tecnici in **inglese**. Testi UI e spiegazioni in **italiano**.
> **`Docs/10_Narrative_Spine.md` è la fonte AUTORITATIVA** per beat narrativi, nomi
> dei livelli, ordine dei boss e vincoli di ambientazione: ogni brief di livello,
> nome o testo di gioco deriva da lì.

## Cos'è il progetto
Mehawar è il revival di un wargame da tavolo abbandonato ("Heroes of Empire"),
reimmaginato come **action platformer 2.5D in pixel art**. Il nome "Mehawar" resta
solo come brand. Setting fantasy romano-babilonese, due fazioni contrapposte.

## Ruolo richiesto a Claude
Agisci simultaneamente come Game Designer, Technical Director / Game Engineer e
CEO / Product Owner. Quando le tre prospettive sono in tensione, esplicitale e dai
una raccomandazione netta.

## Decisioni bloccate (NON rimetterle in discussione salvo richiesta esplicita)
- Due campagne scelte all'avvio: **Via Oscura** (avatar Lucius, posseduto da
  Tiamat) e **Via Romana** (avatar Cesare, potenziato da Marduk).
- Gli avatar sono **ospiti mortali**, non forme divine.
- 12 personaggi originali mappati sull'architettura platformer: 2 avatar giocabili,
  6 eroi imperiali come boss, 6 generali oscuri come boss, le truppe originali come
  nemici standard.
- **Mehawar** è un generale oscuro: alleato nella campagna Oscura, boss nella Romana.
- Mappatura **asimmetrica** delle meccaniche tra le due fazioni.

## Vincolo IP (hard constraint)
Igiene IP non negoziabile. Qualsiasi contenuto derivato da franchise esistenti va
identificato e sostituito con materiale originale **prima** di qualunque avanzamento.
- **QUARANTENA: `Missioni_oscure.rtf` è materiale derivato da WoW** (quest tradotte
  quasi verbatim: Forsaken, Crociata Scarlatta, Scholomance, ecc.) **e non va MAI
  usato come fonte per contenuti di gioco.** I beat della Via Oscura in
  `Docs/10_Narrative_Spine.md` sono interamente originali e lo sostituiscono.
- `Missioni_impero.rtf` è originale e utilizzabile.
- Pendente: verifica IP dell'artwork Cesare-Marduk prima dell'uso come anchor.
Segnala sempre eventuali rischi IP che noti nei materiali.

## Gate check (criteri di validazione di QUESTA fase)
Ogni decisione di prototipazione si misura su due domande pass/fail:
1. **Il salto è soddisfacente?**
2. **Il combattimento è divertente entro 30 secondi?**
Fase esplorativa: **non sovra-investire** prima che il core feel sia validato.
Non toccare nemici/combat finché il salto (gate #1) non passa.

## Stack e convenzioni tecniche
- **Unity 6** (6000.x), URP 2D (Renderer 2D).
- **C# strict**: `#nullable enable` dove sensato, niente `dynamic`, niente magic
  number sparsi (usa costanti o `[SerializeField]`).
- Un `MonoBehaviour` per responsabilità. Tunable esposti con `[SerializeField]` +
  `[Header]` in inglese.
- Fisica in `FixedUpdate`, input in `Update`. `Rigidbody2D` con `gravityScale = 0`:
  la gravità del salto è **derivata via codice** (vedi `Docs/01_Combat_Movement.md`).
- Hitstop via `Time.timeScale`: gestito solo da codice, mai dall'Inspector.
- Input System nuovo (asset `PlayerControls.inputactions`).
- Pixel Perfect Camera: **PPU = 16**, reference 640×360.
- Scene/prefab (`.unity`, `.prefab`) NON editabili a mano in modo affidabile (GUID).
  Costruzione scena: script di bootstrap da codice per il greybox, wiring Inspector
  altrove. Non generare/modificare YAML di scena a mano.

## Struttura cartelle
```
Assets/_Project/
├── Scripts/    # C# — movimento/combat (PlayerMovement, PlayerCombat, PlayerHealth,
│               #   PlayerHitReceiver), risorse firma (IAvatarResource, Bloodlust, Favore,
│               #   AvatarProfiles), nemici (TrainingDummy, EnemyFante, BreakableDummy),
│               #   boss (BossController, BossDefinition, BossGaull, BossShockwave),
│               #   animazione (SpriteAnimator, ActorAnimationSet, PlaceholderAnimationFactory,
│               #   *AnimationDriver), flusso (GameFlow, GameState, GoalTrigger), servizi
│               #   (Hitstop, Units, GreyboxBootstrap), debug HUD (AvatarDebugHUD, BossDebugHUD)
├── Scenes/     # Greybox.unity (solo il GameObject Bootstrap: tutto il resto è runtime)
├── Prefabs/
├── Art/
├── Input/      # PlayerControls.inputactions + wrapper generato
└── Docs/       # copia di questi .md, come riferimento in-editor
```

## Metriche di produzione (pipeline greybox)
- **Livello nuovo** ("Il Passo Conteso", da layout approvato a giocabile e testato):
  **~27 min**, 1 prompt, 1 ciclo di compilazione, ~20 chiamate MCP (12 di test).
- **Boss nuovo con estensione framework** ("Xardast", archetipo mobile/elusivo:
  Evade/Leap/Lunge aggiunti a BossController): **~40 min stimati**, 1 prompt,
  2 cicli di compilazione, incluso 1 bug reale di flow trovato e chiuso dai test.
  Il prossimo boss su archetipo esistente (solo `BossDefinition`) misurerà il
  costo marginale puro — atteso ben sotto i 20 min.
- Collo di bottiglia del progetto: NON la geometria — boss design, arte, tuning.

## Versioning
- Remote: `origin` → https://github.com/AlburniLab/MehawarTest.git (private).
- **Un commit per milestone chiusa** (checkpoint approvato / fase conclusa), non per file.
- Messaggi in inglese, formato `feat:` / `fix:` / `docs:` (+ `refactor:`, `test:` dove serve).
- **Push a fine sessione**, sempre.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`/`*.slnx` mai versionati
  (vedi `.gitignore`); i `Docs/` di progetto vivono in `Assets/_Project/Docs/` e sono tracciati.

## Stile di lavoro
Conciso e diretto. Niente introduzioni o conclusioni generiche. Fornisci blocchi di
codice completi e pronti all'incolla. Se rilevi problemi di usabilità o game design,
segnalali con suggerimenti migliorativi.

## Stato attuale (2026-07-02)
- **Gate #1 e #2 validati.** Salto tarato a 64px/0.30s (96px era troppo alto al playtest).
- **Struttura di gioco completa in greybox**: menu → selezione campagna → livello 1
  "Il Risveglio" (4 zone didattiche + finale) → boss **Gaull** (3 fasi a soglie HP) →
  "Livello completato". Tutto costruito a runtime da codice (level-as-code), UI navigabile
  da tastiera/gamepad, morte → respawn secco con risorsa firma azzerata.
- **Asimmetria v1 funzionante**: Lucius (Sete di sangue → Furia offensiva) vs Cesare
  (Favore di Marduk → Bastione difensivo, colpi lenti/lunghi/pesanti). Architettura:
  avatar = movimento condiviso + `AvatarProfiles.MeleeProfile` + `IAvatarResource` —
  configurazioni, mai fork.
- **Pipeline animazione pronta per lo sprite swap**: durate derivate dalle state machine,
  contratto `ActorAnimationSet` (ScriptableObject) da riempire con gli sprite veri.
- Pixel art e audio **rimandati** per decisione di produzione (placeholder procedurali).
- **Debito tecnico: azzerato (sprint 2026-07-02).** Chiusi: knockback bufferizzato e
  applicato in FixedUpdate; `PlayerInputHub` unico proprietario di PlayerControls;
  `HitboxFactory` per i child counter-scalati; lazy resolve del player in nemici/boss;
  reset timer al respawn; magic number promossi a tunable/costanti; pausa in-game
  (Esc/Start: Riprendi / Abbandona livello, coordinata con Hitstop); codice morto
  GroundSensor rimosso. **Residuo motivato**: HUD debug in IMGUI — by design fino
  alla UI definitiva (non è debito, è scaffolding dichiarato).
- **Livello 2 "Il Passo Conteso" costruito e validato** (pilota dual-moveset, R4):
  corridoio-strozzatura, cenge letali con KillZone, traversata anti-Bloodlust, arena
  a doppio fronte. `LevelCatalog` gestisce le sequenze per campagna ("Prosegui" a fine
  livello); pausa completa (Esc/Start via action map UI, PauseManager). **Boss Xardast
  implementata** (archetipo mobile/elusivo del framework: SuperArmor=false, evade +
  contrattacco, kit Lame/Affondo/Balzo, fasi La Caccia/La Trappola/La Belva) — il
  framework ora copre i due archetipi opposti (muro vs cacciatrice). "La Chiamata"
  (Romana liv.1, boss Dorlok) ancora da fare. Versioning attivo su origin/main.
