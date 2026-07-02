#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// A low ground wave spawned by a boss slam: slides along the floor, hits the player once
    /// (unblockable), dies at the arena bound. LOW on purpose (0.5u): jumping clears it — the
    /// core skill is the dodge. Pure kinematic visual + explicit OverlapBox, no Rigidbody.
    /// </summary>
    public sealed class BossShockwave : MonoBehaviour
    {
        private const float Width = 0.9f;
        private const float Height = 0.5f;

        private int _dir;
        private int _damage;
        private float _hitstun;
        private LayerMask _playerMask;
        private float _speed;
        private float _boundX;
        private Transform? _source;
        private bool _hasHit;
        private Texture2D? _tex;

        public static void Spawn(float originX, float floorY, int dir, int damage, float hitstun,
            LayerMask playerMask, float speed, float boundX, Transform source)
        {
            var go = new GameObject("BossShockwave");
            go.transform.position = new Vector3(originX + dir * 1.2f, floorY + Height * 0.5f, 0f);
            var wave = go.AddComponent<BossShockwave>();
            wave._dir = dir;
            wave._damage = damage;
            wave._hitstun = hitstun;
            wave._playerMask = playerMask;
            wave._speed = speed;
            wave._boundX = boundX;
            wave._source = source;
            wave.BuildVisual();
        }

        private void BuildVisual()
        {
            _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(_tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sr.color = new Color(1f, 0.45f, 0.15f);   // hot orange: danger on the floor
            sr.sortingOrder = 8;
            transform.localScale = new Vector3(Width, Height, 1f);
        }

        private void OnDestroy()
        {
            if (_tex != null)
                Destroy(_tex);
        }

        private void Update()
        {
            transform.position += new Vector3(_dir * _speed * Time.deltaTime, 0f, 0f);

            if (!_hasHit)
            {
                Collider2D hit = Physics2D.OverlapBox(transform.position, new Vector2(Width, Height), 0f, _playerMask);
                if (hit != null)
                {
                    var target = hit.GetComponentInParent<IHittable>();
                    if (target != null)
                    {
                        _hasHit = true;
                        target.TakeHit(new HitInfo(_damage, new Vector2(_dir, 0.3f), _hitstun,
                            _source, unblockable: true));
                        Destroy(gameObject);   // absorbed by the impact
                        return;
                    }
                }
            }

            if ((_dir > 0 && transform.position.x >= _boundX) || (_dir < 0 && transform.position.x <= _boundX))
                Destroy(gameObject);
        }
    }
}
