#nullable enable
using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal; // PixelPerfectCamera

namespace Mehawar.Greybox
{
    /// <summary>
    /// Scene entry point + builder of level 1, "Il Risveglio" (Act I) — everything at runtime.
    /// On Start it validates layers and hands over to <see cref="GameFlow"/> (menu first); the
    /// level itself is built ON DEMAND by <see cref="BuildLevel"/> under a disposable LevelRoot,
    /// so the flow can tear it down and rebuild it cleanly. The level teaches move -> jump ->
    /// attack with geometry alone: zone 1 walk + low step; zone 2 pit + floating platforms
    /// (coyote/buffer); zone 3 breakable dummy that opens a gate; zone 4 first Fante; finale with
    /// two Fanti; gold goal trigger at the end (boss will replace it as the real gate, STEP 4).
    /// Layout lives in CODE (constants below), never in scene-serialized fields.
    /// </summary>
    public sealed class GreyboxBootstrap : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("Name of the dedicated Ground layer (must exist in Tags & Layers).")]
        [SerializeField] private string groundLayerName = "Ground";
        [Tooltip("Name of the dedicated Hittable layer for combat targets (must exist).")]
        [SerializeField] private string hittableLayerName = "Hittable";
        [Tooltip("Name of the dedicated Player layer, targeted by enemy attacks (must exist).")]
        [SerializeField] private string playerLayerName = "Player";

        [Header("Actors")]
        [Tooltip("Actor body size in Unity units (PPU 16).")]
        [SerializeField] private Vector2 dummySize = new Vector2(0.75f, 1.5f);
        [SerializeField] private Color dummyColor = new Color(0.55f, 0.75f, 0.85f);
        [SerializeField] private Color fanteColor = new Color(0.80f, 0.30f, 0.30f);

        [Header("Player")]
        [Tooltip("Player collider size in Unity units (PPU 16). 0.75 x 1.5 u = 12 x 24 px.")]
        [SerializeField] private Vector2 playerSize = new Vector2(0.75f, 1.5f);

        [Header("Camera")]
        [Tooltip("Orthographic half-height in units; ~5-6 frames the player nicely.")]
        [SerializeField] private float orthoSize = 5.5f;
        [Tooltip("Fixed camera depth. The follow only tracks X/Y; Z stays locked here.")]
        [SerializeField] private float cameraZ = -10f;
        [Tooltip("Visible dark-gray clear color (never pure black, so an empty frame is obvious).")]
        [SerializeField] private Color cameraBackground = new Color(0.16f, 0.17f, 0.20f);
        [Tooltip("Pixel Perfect Camera overrides ortho size and can black-screen under Cinemachine; " +
                 "disable it for the greybox feel test, re-enable when we care about pixel crispness.")]
        [SerializeField] private bool disablePixelPerfectForGreybox = true;

        private static readonly Color GroundColor = new Color(0.32f, 0.30f, 0.36f);
        private static readonly Color PlayerColor = new Color(0.85f, 0.55f, 0.30f);
        private static readonly Color GoalColor = new Color(0.95f, 0.80f, 0.30f);

        // Identity tints multiplied over the shared placeholder frames (soft, so state colors read).
        private static readonly Color PlayerTint = new Color(1f, 0.94f, 0.82f);
        private static readonly Color FanteTint = new Color(1f, 0.80f, 0.80f);
        private static readonly Color DummyTint = new Color(0.80f, 0.92f, 1f);

        // --- "Il Risveglio", segment 1 — layout as code (x grows left -> right, ~155 u long) ---
        private const float GroundTopY = -2.5f;                                  // walkable top of the main floor
        private static readonly Vector2 PlayerSpawnPoint = new Vector2(2f, -1.5f);
        private const float BreakableDummyX = 69f;                               // zone 3, in front of the gate
        private static readonly float[] FanteXs = { 84f, 106f, 112f };           // zone 4 + finale pair
        private const float ArenaMinX = 130f;                                    // boss arena bounds
        private const float ArenaMaxX = 152f;
        private const float BossX = 143f;

        private Sprite _unitSprite = null!;
        private GameObject? _gateWall;
        private GameObject? _goal;
        private GoalTrigger? _goalTrigger;
        private Transform? _levelRoot;
        private PlayerMovement? _spawnedPlayer;   // build-scoped, set by SpawnPlayer (like _levelRoot)
        private CinemachineCamera? _vcam;         // resolved once (scene-authored or created), then reused
        private int _groundLayerId = -1;
        private int _hittableLayerId = -1;
        private int _playerLayerId = -1;

        private void Start()
        {
            _groundLayerId = LayerMask.NameToLayer(groundLayerName);
            _hittableLayerId = LayerMask.NameToLayer(hittableLayerName);
            _playerLayerId = LayerMask.NameToLayer(playerLayerName);
            if (_groundLayerId < 0 || _hittableLayerId < 0 || _playerLayerId < 0)
            {
                Debug.LogError($"[GreyboxBootstrap] Missing layer '{groundLayerName}', '{hittableLayerName}' " +
                               $"or '{playerLayerName}'. Create them in Project Settings > Tags and Layers.");
                return;
            }

            // Combat uses OverlapBox + direct-velocity knockback, NOT physical collision. So actors
            // must not body-shove the player (or each other). They still collide with Ground.
            Physics2D.IgnoreLayerCollision(_playerLayerId, _hittableLayerId, true);
            Physics2D.IgnoreLayerCollision(_hittableLayerId, _hittableLayerId, true);

            _unitSprite = CreateUnitSprite();

            // Menu first: the level is built on demand by the flow (see BuildLevel).
            var flow = new GameObject("GameFlow").AddComponent<GameFlow>();
            flow.Initialize(this);
        }

        /// <summary>
        /// Builds a level (by LevelCatalog builder id) under a fresh, disposable "LevelRoot"
        /// (geometry, player, actors, camera, debug HUD) and wires the goal to
        /// <paramref name="onGoalReached"/>. Destroying the returned root removes it cleanly.
        /// </summary>
        public GameObject BuildLevel(int builderId, Action onGoalReached)
        {
            var root = new GameObject("LevelRoot");
            _levelRoot = root.transform;
            _gateWall = null;
            _goal = null;
            _goalTrigger = null;
            _spawnedPlayer = null;

            GameObject player;
            if (builderId == LevelCatalog.PassoContesoBuilder)
            {
                BuildPassoGeometry(_groundLayerId);
                player = SpawnPlayer(_groundLayerId, _hittableLayerId, _playerLayerId);
                SpawnPassoActors(_hittableLayerId, _playerLayerId);
            }
            else if (builderId == LevelCatalog.ChiamataBuilder)
            {
                BuildChiamataGeometry(_groundLayerId);
                player = SpawnPlayer(_groundLayerId, _hittableLayerId, _playerLayerId);
                SpawnChiamataActors(_hittableLayerId, _playerLayerId);
            }
            else
            {
                BuildRisveglioGeometry(_groundLayerId);
                player = SpawnPlayer(_groundLayerId, _hittableLayerId, _playerLayerId);
                SpawnRisveglioActors(_hittableLayerId, _playerLayerId);
            }

            SetupCamera(player.transform);
            SpawnDebugHud(player);

            if (_goalTrigger != null)
                _goalTrigger.Reached += onGoalReached;

            _levelRoot = null;
            _spawnedPlayer = null;
            return root;
        }

        private void SpawnDebugHud(GameObject player)
        {
            var go = new GameObject("AvatarDebugHUD");
            go.transform.SetParent(_levelRoot, false);
            var hud = go.AddComponent<AvatarDebugHUD>();
            hud.Resource = player.GetComponent<IAvatarResource>();   // read-only overlay, removable
            hud.Health = player.GetComponent<PlayerHealth>();
            hud.ActorName = GameState.AvatarName;
        }

        private void BuildRisveglioGeometry(int groundLayer)
        {
            // Zona 1 (x 0..34) — walk + a low step up/down: first jump with zero stakes.
            CreateSolid("Wall.Left", new Vector2(-0.5f, 1.5f), new Vector2(1f, 8f), groundLayer);
            CreateSolid("Floor.Z1", new Vector2(19f, -4.5f), new Vector2(38f, 4f), groundLayer);
            CreateSolid("Z1.Step", new Vector2(22f, -1.75f), new Vector2(8f, 1.5f), groundLayer);

            // Zona 2 (x 34..60) — jumps: a 5u pit (3u deep: jumping out is easy, falling costs
            // only time) and two floating platforms where coyote + buffer are felt.
            CreateSolid("Z2.PitFloor", new Vector2(40.5f, -6f), new Vector2(5f, 1f), groundLayer);
            CreateSolid("Floor.Z2toEnd", new Vector2(98.5f, -4.5f), new Vector2(111f, 4f), groundLayer); // x 43..154 (arena incl.)
            CreateSolid("Z2.PlatA", new Vector2(50f, -0.75f), new Vector2(4f, 0.5f), groundLayer);
            CreateSolid("Z2.PlatB", new Vector2(57f, 0.75f), new Vector2(4f, 0.5f), groundLayer);

            // Zona 3 (x 60..76) — the gate: too tall to jump (top 5.5 > apex), opened by the dummy.
            _gateWall = CreateSolid("Z3.Gate", new Vector2(73f, 1.5f), new Vector2(2f, 8f), groundLayer);

            // Zona 4 (x 76..96) is open arena; finale (x 96..126) holds the Fante pair; the boss
            // arena (x 130..152) closes the level. Goal: a TRIGGER gate on the Default layer
            // (GroundSensor must ignore it), placed PAST the boss — gray and inert until he falls.
            _goal = CreateSolid("Goal", new Vector2(150f, -1f), new Vector2(2f, 3f), 0);
            _goal.GetComponent<SpriteRenderer>().color = new Color(0.45f, 0.45f, 0.48f);
            var goalCollider = _goal.GetComponent<BoxCollider2D>();
            goalCollider.isTrigger = true;
            goalCollider.enabled = false;                       // activated by the boss's death
            _goalTrigger = _goal.AddComponent<GoalTrigger>();
            CreateSolid("Wall.Right", new Vector2(154.5f, 1.5f), new Vector2(1f, 8f), groundLayer);
        }

        private void SpawnRisveglioActors(int hittableLayer, int playerLayer)
        {
            // Zona 3 — breakable dummy wired to the gate.
            var breakGo = CreateActor("BreakableDummy", BreakableDummyX, GroundTopY, dummyColor, hittableLayer);
            var breakable = breakGo.AddComponent<BreakableDummy>();
            var dummyAnim = breakGo.AddComponent<SpriteAnimator>();
            dummyAnim.Configure(PlaceholderAnimationFactory.GetShared(), DummyTint);
            breakGo.AddComponent<DummyAnimationDriver>();
            GameObject? gate = _gateWall;
            breakable.Broken += () =>
            {
                if (gate != null)
                    Destroy(gate);
                Debug.Log("[Level] Il passaggio si apre.");
            };

            // Zona 4 + finale — kills are permanent progression here.
            foreach (float x in FanteXs)
                SpawnFante(hittableLayer, playerLayer, x, GroundTopY);

            SpawnBoss(hittableLayer, playerLayer, BossGaull.Create(),
                new Vector2(BossX, GroundTopY + 1.25f), new Vector2(1.5f, 2.5f),
                new Color(1f, 0.70f, 0.65f), ArenaMinX, ArenaMaxX);
        }

        // --- "Il Passo Conteso" (Via Romana liv. 2, R4) — layout as code, west -> east, CLIMBING.
        // Dual-moveset test level (Docs/10 §7): S1 choke corridor (A), S2 lethal ledges (B, D bonus),
        // S3 empty traverse that drains Bloodlust (C), S4 walled two-front pocket arena (D), S5 top.
        private void BuildPassoGeometry(int groundLayer)
        {
            // S0 + S1 — approach and low-ceiling choke corridor (interior 3u: jump-over denied).
            CreateSolid("Wall.Left", new Vector2(-0.5f, 1.5f), new Vector2(1f, 8f), groundLayer);
            CreateSolid("Floor.S0S1", new Vector2(20f, -4.5f), new Vector2(40f, 4f), groundLayer);   // x 0..40, top -2.5
            CreateSolid("S1.Ceiling", new Vector2(26f, 1.5f), new Vector2(20f, 2f), groundLayer);    // x 16..36, bottom 0.5

            // S2 — five narrow ledges (3u) climbing over a LETHAL chasm; a hit near the edge is a fall.
            CreateSolid("S2.L1", new Vector2(41.5f, -1.75f), new Vector2(3f, 0.5f), groundLayer);    // top -1.5
            CreateSolid("S2.L2", new Vector2(46.5f, -0.75f), new Vector2(3f, 0.5f), groundLayer);    // top -0.5
            CreateSolid("S2.L3", new Vector2(51.5f, 0.25f), new Vector2(3f, 0.5f), groundLayer);     // top  0.5
            CreateSolid("S2.L4", new Vector2(56.5f, 1.25f), new Vector2(3f, 0.5f), groundLayer);     // top  1.5
            CreateSolid("S2.L5", new Vector2(61.5f, 2.25f), new Vector2(3f, 0.5f), groundLayer);     // top  2.5

            // S3 — high empty traverse: three 3u gaps, ~12-15s with no combat (Bloodlust drains).
            CreateSolid("S3.PlateauA", new Vector2(67f, 0.5f), new Vector2(6f, 4f), groundLayer);    // x 64..70, top 2.5
            CreateSolid("S3.PlatB", new Vector2(75f, 2.75f), new Vector2(4f, 0.5f), groundLayer);    // top 3.0
            CreateSolid("S3.PlatC", new Vector2(82f, 3.25f), new Vector2(4f, 0.5f), groundLayer);    // top 3.5
            CreateSolid("S3.PlateauD", new Vector2(89.5f, 2f), new Vector2(5f, 4f), groundLayer);    // x 87..92, top 4.0

            // S4 — walled pocket arena (two-front squeeze), entered by dropping down 1u.
            CreateSolid("Floor.S4", new Vector2(104f, 1f), new Vector2(24f, 4f), groundLayer);       // x 92..116, top 3.0

            // S5 — final steps to the SUMMIT ARENA (Xardast: mobile boss needs room, x 122..140).
            CreateSolid("S5.StepA", new Vector2(119f, 1.5f), new Vector2(6f, 6f), groundLayer);      // x 116..122, top 4.5
            CreateSolid("S5.StepB", new Vector2(131f, 2f), new Vector2(18f, 8f), groundLayer);       // x 122..140, top 6.0
            CreateSolid("Wall.Right", new Vector2(140.5f, 8f), new Vector2(1f, 10f), groundLayer);

            // Chasm hazard: everything that falls, dies (player via the death rule).
            var kz = CreateSolid("KillZone", new Vector2(66f, -9f), new Vector2(54f, 2f), 0);        // x 39..93
            kz.GetComponent<SpriteRenderer>().enabled = false;   // invisible: the depth reads as void
            kz.GetComponent<BoxCollider2D>().isTrigger = true;
            kz.AddComponent<KillZone>();

            // Goal PAST the arena: gray and inert until Xardast falls (Gaull-gate pattern).
            _goal = CreateSolid("Goal", new Vector2(137f, 7.5f), new Vector2(2f, 3f), 0);
            _goal.GetComponent<SpriteRenderer>().color = new Color(0.45f, 0.45f, 0.48f);
            var goalCol = _goal.GetComponent<BoxCollider2D>();
            goalCol.isTrigger = true;
            goalCol.enabled = false;
            _goalTrigger = _goal.AddComponent<GoalTrigger>();
        }

        private void SpawnPassoActors(int hittableLayer, int playerLayer)
        {
            // S1 — staggered column: same aggro, decreasing speeds keep them arriving in sequence.
            SpawnFante(hittableLayer, playerLayer, 26f, GroundTopY).MoveSpeed = 110f;
            SpawnFante(hittableLayer, playerLayer, 30f, GroundTopY).MoveSpeed = 85f;
            SpawnFante(hittableLayer, playerLayer, 34f, GroundTopY).MoveSpeed = 60f;

            // S2 — ledge sentinels: short aggro + slow creep so they hold their ledge, not dive off it.
            EnemyFante s1 = SpawnFante(hittableLayer, playerLayer, 46.5f, -0.5f);
            s1.AggroRange = 2.5f;
            s1.MoveSpeed = 40f;
            s1.PatrolRange = 0.4f;
            EnemyFante s2 = SpawnFante(hittableLayer, playerLayer, 56.5f, 1.5f);
            s2.AggroRange = 2.5f;
            s2.MoveSpeed = 40f;
            s2.PatrolRange = 0.4f;

            // S4 — two-front squeeze: pair ahead, one closing in from behind the exit.
            SpawnFante(hittableLayer, playerLayer, 100f, 3.0f);
            SpawnFante(hittableLayer, playerLayer, 103f, 3.0f);
            SpawnFante(hittableLayer, playerLayer, 112f, 3.0f);

            // S5 — last guard at the arena entrance, then the huntress herself.
            SpawnFante(hittableLayer, playerLayer, 124f, 6.0f);
            SpawnBoss(hittableLayer, playerLayer, BossXardast.Create(),
                new Vector2(133f, 7.0f), new Vector2(1.0f, 2.0f),
                new Color(1f, 0.78f, 0.55f), 122f, 140f);
        }

        // --- "La Chiamata" (Via Romana liv. 1, R6 Roma intatta) — the Cesare TUTORIAL: flat,
        // monumental, west -> east toward the gates. Encounter structure teaches the philosophy:
        // T1 reach, T2 Favore grows clean, T3 the ambush zeroes it, T4 the Bastione showcase.
        private void BuildChiamataGeometry(int groundLayer)
        {
            // S0 Foro + T1 Strada + T2 Colonnato: one long ceremonial street (x 0..58).
            CreateSolid("Wall.Left", new Vector2(-0.5f, 1.5f), new Vector2(1f, 8f), groundLayer);
            CreateSolid("Floor.Strada", new Vector2(29f, -4.5f), new Vector2(58f, 4f), groundLayer);   // top -2.5

            // R6 flavor: monumental backdrop (sprite-only, never blocks).
            CreateDecor("Decor.Arco", new Vector2(8f, 1.5f), new Vector2(6f, 8f));
            CreateDecor("Decor.Colonna1", new Vector2(34f, 0.5f), new Vector2(1.2f, 6f));
            CreateDecor("Decor.Colonna2", new Vector2(42f, 0.5f), new Vector2(1.2f, 6f));
            CreateDecor("Decor.Colonna3", new Vector2(50f, 0.5f), new Vector2(1.2f, 6f));

            // T3 — La Piazza dell'Agguato: a sunken bowl (drop 1u in, hop 1u out).
            CreateSolid("T3.Conca", new Vector2(68f, -5.5f), new Vector2(20f, 4f), groundLayer);       // x 58..78, top -3.5
            CreateDecor("Decor.Pilastro", new Vector2(62.5f, -1.5f), new Vector2(1.4f, 4f));           // hides the ambusher

            // T4 — Il Camminamento: steps up to the city walls, with a BREACH in the walkway.
            CreateSolid("T4.StepA", new Vector2(80f, -3.5f), new Vector2(4f, 4f), groundLayer);        // x 78..82, top -1.5
            CreateSolid("T4.StepB", new Vector2(84f, -2.5f), new Vector2(4f, 4f), groundLayer);        // x 82..86, top -0.5
            CreateSolid("T4.MuraA", new Vector2(88f, -2f), new Vector2(4f, 4f), groundLayer);          // x 86..90, top 0
            CreateSolid("T4.MuraB", new Vector2(94.5f, -2f), new Vector2(3f, 4f), groundLayer);        // x 93..96, top 0 (breach 90..93)
            var kz = CreateSolid("KillZone", new Vector2(91.5f, -7f), new Vector2(5f, 2f), 0);
            kz.GetComponent<SpriteRenderer>().enabled = false;
            kz.GetComponent<BoxCollider2D>().isTrigger = true;
            kz.AddComponent<KillZone>();

            // ARENA — Le Porte (x 96..122, back at street level), goal inert until Dorlok falls.
            CreateSolid("Floor.Porte", new Vector2(109f, -4.5f), new Vector2(26f, 4f), groundLayer);   // top -2.5
            CreateDecor("Decor.Porta", new Vector2(120f, 1.5f), new Vector2(4f, 8f));
            CreateSolid("Wall.Right", new Vector2(122.5f, 1.5f), new Vector2(1f, 8f), groundLayer);

            _goal = CreateSolid("Goal", new Vector2(118f, -1f), new Vector2(2f, 3f), 0);
            _goal.GetComponent<SpriteRenderer>().color = new Color(0.45f, 0.45f, 0.48f);
            var goalCol = _goal.GetComponent<BoxCollider2D>();
            goalCol.isTrigger = true;
            goalCol.enabled = false;
            _goalTrigger = _goal.AddComponent<GoalTrigger>();

            // The three environmental inscriptions (Italian, one line each — the whole "text").
            CreateInscription(new Vector2(32f, -1.2f), "Il Favore di Marduk cresce solo in chi non viene toccato.");
            CreateInscription(new Vector2(83f, 0.8f), "Il Bastione respinge ciò che osa colpirti.");
            CreateInscription(new Vector2(98f, -1.2f), "Alle porte, il sicario attende.");
        }

        private void SpawnChiamataActors(int hittableLayer, int playerLayer)
        {
            // T1 — the reach lesson: one Fante, open ground.
            SpawnFante(hittableLayer, playerLayer, 22f, GroundTopY);

            // T2 — clean kills make the bar climb in plain sight.
            SpawnFante(hittableLayer, playerLayer, 38f, GroundTopY);
            SpawnFante(hittableLayer, playerLayer, 50f, GroundTopY);

            // T3 — the ambush: A visible ahead, B tucked behind the pillar, flanking mid-fight.
            SpawnFante(hittableLayer, playerLayer, 68f, -3.5f);
            EnemyFante ambusher = SpawnFante(hittableLayer, playerLayer, 61f, -3.5f);
            ambusher.AggroRange = 4f;

            // T4 — the Bastione showcase: slow sentinel on the walls, breach on its right.
            EnemyFante sentinel = SpawnFante(hittableLayer, playerLayer, 88.5f, 0f);
            sentinel.AggroRange = 2.5f;
            sentinel.MoveSpeed = 40f;
            sentinel.PatrolRange = 0.3f;

            SpawnBoss(hittableLayer, playerLayer, BossDorlok.Create(),
                new Vector2(112f, -1.4f), new Vector2(1.2f, 2.2f),
                new Color(0.75f, 0.75f, 0.95f), 97f, 121f);   // black-armor cool tint
        }

        /// <summary>Sprite-only scenery (never collides): monumental R6 backdrop blocks.</summary>
        private GameObject CreateDecor(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_levelRoot, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _unitSprite;
            sr.color = new Color(0.45f, 0.42f, 0.38f);   // warm imperial stone, behind the action
            sr.sortingOrder = -5;
            return go;
        }

        /// <summary>A stone slab with a one-line Italian inscription (world-space TextMesh).</summary>
        private void CreateInscription(Vector2 position, string text)
        {
            GameObject slab = CreateDecor("Inscription", position, new Vector2(1.8f, 1.2f));
            slab.GetComponent<SpriteRenderer>().color = new Color(0.55f, 0.50f, 0.42f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(slab.transform, false);
            textGo.transform.localScale = new Vector3(1f / 1.8f, 1f / 1.2f, 1f);   // undo slab scale
            textGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.07f;
            tm.fontSize = 32;
            tm.color = new Color(0.9f, 0.85f, 0.7f);
            textGo.GetComponent<MeshRenderer>().sortingOrder = 40;
        }

        private void SpawnBoss(int hittableLayer, int playerLayer, BossDefinition def,
            Vector2 position, Vector2 bodySize, Color tint, float arenaMinX, float arenaMaxX)
        {
            var bossGo = new GameObject("Boss" + def.Name) { layer = hittableLayer };
            bossGo.transform.SetParent(_levelRoot, false);
            bossGo.transform.position = position;
            bossGo.transform.localScale = new Vector3(bodySize.x, bodySize.y, 1f);

            var sr = bossGo.AddComponent<SpriteRenderer>();
            sr.sprite = _unitSprite;
            sr.color = fanteColor;
            sr.sortingOrder = 6;

            bossGo.AddComponent<BoxCollider2D>();
            bossGo.AddComponent<Rigidbody2D>();                 // configured by TrainingDummy.Awake

            var boss = bossGo.AddComponent<BossController>();
            boss.Configure(def, 1 << playerLayer, arenaMinX, arenaMaxX,
                _spawnedPlayer!);   // BuildLevel always spawns the player before actors
            boss.BossDefeated += ActivateGoal;

            var anim = bossGo.AddComponent<SpriteAnimator>();
            anim.Configure(PlaceholderAnimationFactory.GetShared(), tint);
            bossGo.AddComponent<BossAnimationDriver>();

            var hudGo = new GameObject("BossDebugHUD");
            hudGo.transform.SetParent(_levelRoot, false);
            hudGo.AddComponent<BossDebugHUD>().Boss = boss;
        }

        private void ActivateGoal()
        {
            if (_goal == null)
                return;
            _goal.GetComponent<SpriteRenderer>().color = GoalColor;
            _goal.GetComponent<BoxCollider2D>().enabled = true;
            Debug.Log("[Level] Il guardiano è caduto — il varco dorato si apre.");
        }

        private GameObject CreateSolid(string name, Vector2 position, Vector2 size, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(_levelRoot, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _unitSprite;
            sr.color = GroundColor;

            go.AddComponent<BoxCollider2D>(); // 1x1 local, scaled to world size
            return go;
        }

        private GameObject SpawnPlayer(int groundLayer, int hittableLayer, int playerLayer)
        {
            // Avatar from the chosen campaign (same placeholder body until STEP 3 gives Cesare his moveset).
            var player = new GameObject($"Player ({GameState.AvatarName})") { layer = playerLayer };
            player.transform.SetParent(_levelRoot, false);
            player.transform.position = PlayerSpawnPoint;
            player.transform.localScale = new Vector3(playerSize.x, playerSize.y, 1f);

            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = _unitSprite;
            sr.color = PlayerColor;
            sr.sortingOrder = 10;

            player.AddComponent<BoxCollider2D>();
            player.AddComponent<Rigidbody2D>();          // configured by PlayerMovement.Awake

            var sensor = player.AddComponent<GroundSensor>();
            sensor.GroundMask = 1 << groundLayer;        // dedicated Ground layer only

            player.AddComponent<PlayerInputHub>();        // single PlayerControls owner
            _spawnedPlayer =
                player.AddComponent<PlayerMovement>();    // self-wires rb + sensor + input hub in Awake

            // Avatar = configuration: signature resource + melee profile (never a code fork).
            bool oscura = GameState.Campaign == Campaign.ViaOscura;
            if (oscura)
                player.AddComponent<Bloodlust>();         // Lucius: aggression (Furia)
            else
                player.AddComponent<Favore>();            // Cesare: discipline (Bastione)

            var combat = player.AddComponent<PlayerCombat>();
            combat.TargetMask = 1 << hittableLayer;       // hit only the dedicated Hittable layer
            combat.ApplyProfile(oscura ? AvatarProfiles.Lucius : AvatarProfiles.Cesare);

            player.AddComponent<PlayerHealth>();          // HP + death/respawn; spawn point = spawn position
            player.AddComponent<PlayerHitReceiver>();     // receives enemy hits (knockback + hitstun)

            // Real sprites (Fase 3): counter-scaled child at scale 1 (Docs/30 §5) so the native
            // art is not distorted by the stretched greybox body. Placeholder path is the fallback
            // (Cesare, or Lucius before the set asset exists).
            var realSet = Resources.Load<ActorAnimationSet>("LuciusAnimationSet");
            if (oscura && realSet != null)
            {
                sr.enabled = false;                       // hide the greybox slab, keep the collider
                GameObject visual = HitboxFactory.CreateCounterScaledChild(player.transform, "Visual");
                visual.transform.localPosition = new Vector3(0f, -0.5f, 0f); // sprite pivot (feet) at collider bottom
                var vsr = visual.AddComponent<SpriteRenderer>();
                vsr.sortingOrder = 10;
                var realAnim = visual.AddComponent<SpriteAnimator>();
                realAnim.Configure(realSet, Color.white); // real art: no identity tint, aura still overlays
                // The debug label default offset assumes the stretched slab; real art is ~2u tall
                // with the pivot at the feet, so lift it above the head.
                Transform label = visual.transform.Find("StateLabel");
                if (label != null)
                    label.localPosition = new Vector3(0f, 2.15f, 0f);
            }
            else
            {
                var anim = player.AddComponent<SpriteAnimator>();
                anim.Configure(PlaceholderAnimationFactory.GetShared(), PlayerTint);
            }
            player.AddComponent<PlayerAnimationDriver>();
            return player;
        }

        private GameObject CreateActor(string name, float x, float floorTopY, Color color, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(_levelRoot, false);
            go.transform.position = new Vector2(x, floorTopY + dummySize.y * 0.5f);
            go.transform.localScale = new Vector3(dummySize.x, dummySize.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _unitSprite;
            sr.color = color;            // read as base color in Awake
            sr.sortingOrder = 5;

            go.AddComponent<BoxCollider2D>();
            go.AddComponent<Rigidbody2D>(); // configured by TrainingDummy.Awake
            return go;
        }

        private EnemyFante SpawnFante(int hittableLayer, int playerLayer, float x, float floorTopY)
        {
            // On Hittable so the player can hit it; it attacks the Player layer.
            var fante = CreateActor("EnemyFante", x, floorTopY, fanteColor, hittableLayer);
            var brain = fante.AddComponent<EnemyFante>();
            brain.PlayerMask = 1 << playerLayer;
            brain.SetPlayer(_spawnedPlayer!);   // BuildLevel always spawns the player before actors
            brain.Respawns = false;   // level mode: a kill is progress, not a 1.5s pause

            var anim = fante.AddComponent<SpriteAnimator>();
            anim.Configure(PlaceholderAnimationFactory.GetShared(), FanteTint);
            fante.AddComponent<FanteAnimationDriver>();
            return brain;
        }

        private void SetupCamera(Transform target)
        {
            // (1) Main Camera: exists, tagged MainCamera, orthographic, visible bg, Z locked.
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackground;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            Vector3 cp = cam.transform.position;
            cam.transform.position = new Vector3(cp.x, cp.y, cameraZ);

            // (3) Pixel Perfect Camera fights Cinemachine (overrides ortho size, can render black
            // via its Upscale Render Texture). Disable it for the greybox so framing is predictable.
            if (disablePixelPerfectForGreybox)
            {
                var ppc = cam.GetComponent<PixelPerfectCamera>();
                if (ppc != null)
                {
                    ppc.enabled = false;
                    Debug.Log("[GreyboxBootstrap] PixelPerfectCamera disabled for the greybox feel test.");
                }
            }

            // CinemachineBrain drives the Main Camera from the vcam.
            if (cam.GetComponent<CinemachineBrain>() == null)
                cam.gameObject.AddComponent<CinemachineBrain>();

            // (2) Follow only X/Y. PositionComposer places the camera at target - forward * CameraDistance;
            // with forward = +Z and target.z = 0, the camera Z stays locked at -CameraDistance (= cameraZ).
            // Cached across rebuilds; first resolve finds the scene-authored vcam (Greybox.unity).
            // One-shot lookup: there is a single vcam, so instance ordering is irrelevant.
            CinemachineCamera? vcam = _vcam;
            if (vcam == null)
                vcam = FindAnyObjectByType<CinemachineCamera>();
            if (vcam == null)
            {
                var vcamGo = new GameObject("CM vcam1");
                vcamGo.transform.SetParent(_levelRoot, false);   // torn down with the level
                vcamGo.transform.position = new Vector3(target.position.x, target.position.y, cameraZ);
                vcam = vcamGo.AddComponent<CinemachineCamera>();
            }
            _vcam = vcam;

            var composer = vcam.GetComponent<CinemachinePositionComposer>();
            if (composer == null)
                composer = vcam.gameObject.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = Mathf.Abs(cameraZ); // = 10 -> keeps camera Z at cameraZ
            // Long linear level: soften the follow so sprints/jumps don't snap the frame.
            // Y slightly lighter than X to keep landings readable.
            composer.Damping = new Vector3(1f, 0.7f, 0f);

            // (3) Drive ortho size through the vcam lens so the brain outputs the framed size.
            LensSettings lens = vcam.Lens;
            lens.OrthographicSize = orthoSize;
            vcam.Lens = lens;

            vcam.Follow = target;   // track the player on X/Y
        }

        private static Sprite CreateUnitSprite()
        {
            // 1x1 white texture -> 1 unit square sprite (pixelsPerUnit = 1); tinted per renderer.
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
