#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Shared rigging helpers for actor children. Actor bodies are unit sprites stretched by
    /// transform scale, so any functional child (hitboxes, labels) must be COUNTER-SCALED to
    /// keep working in world units. One implementation, used by every actor — never copy it.
    /// </summary>
    public static class HitboxFactory
    {
        /// <summary>Create a child GameObject whose local scale cancels the parent's scale.</summary>
        public static GameObject CreateCounterScaledChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(
                Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
                Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
                1f);
            return go;
        }

        /// <summary>Create a disabled child trigger hitbox sized in world units (attack hitboxes).</summary>
        public static BoxCollider2D CreateChildTrigger(Transform parent, string name, Vector2 size)
        {
            GameObject go = CreateCounterScaledChild(parent, name);
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            box.enabled = false;
            return box;
        }
    }
}
