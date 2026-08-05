using System;
using System.Collections.Generic;
using DONT_TOUCH.Enums;
using UnityEngine;

namespace DONT_TOUCH.Scripts.BlockComponents
{
    [ExecuteInEditMode]
    public sealed class CullingZoneComponent : SchematicBlock
    {
        public override BlockType BlockType { get; } = BlockType.CullingZone;
        public ColliderShape Type = ColliderShape.Box;
        [Min(0), Header("Сколько объектов будет появляться за кадр (0 = все объекты за кадр)")] public int ObjectPerSpawn = 0;
        
        internal MeshFilter _filter;
        private ColliderShape? _prevType;
        private static readonly Color GizmoColor = new Color(0.2f, 0.6f, 1f, 0.6f);
        
        public override void Compile(SchematicBlockData block)
        {
            block.Properties = new Dictionary<string, object>()
            {
                { "ColliderShape", Type },
                { nameof(ObjectPerSpawn), ObjectPerSpawn },
            };
            base.Compile(block);
        }

        public override void Decompile(ref GameObject gameObject, SchematicBlockData block, Transform parent)
        {
            CullingZoneComponent cullingZone =
                Create<CullingZoneComponent>("Assets/Resources/Blocks/CullingZone.prefab");
            gameObject = cullingZone.gameObject;
            cullingZone.Type = (ColliderShape)Convert.ToInt32(block.Properties["ColliderShape"]);
            cullingZone.ObjectPerSpawn = Convert.ToInt32(block.Properties["ObjectPerSpawn"]);
            base.Decompile(ref gameObject, block, parent);
        }

        private void Start()
        {
            TryGetComponent(out _filter);
        }

        private void Update()
        {
            if (_filter == null)
                return;
            
            _filter.hideFlags = HideFlags.HideInInspector;

            if (_prevType == Type)
                return;

            _prevType = Type;
            _filter.sharedMesh = PrimitiveMeshGetter.GetPrimitiveMesh(Type);
        }

        private void OnDrawGizmosSelected()
        {
            if (_filter == null || _filter.sharedMesh == null)
                return;
            Gizmos.color = GizmoColor;
            Gizmos.DrawMesh(_filter.sharedMesh, transform.position, transform.rotation, transform.lossyScale);
        }
    }
}