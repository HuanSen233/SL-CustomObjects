using System;
using System.Collections.Generic;
using DONT_TOUCH.Enums;
using UnityEngine;

namespace DONT_TOUCH.Scripts.BlockComponents
{
    [ExecuteInEditMode]
    public class CullingZoneConnectorComponent : SchematicBlock
    {
        public override BlockType BlockType { get; } = BlockType.CullingZoneConnector;
        public ColliderShape Type = ColliderShape.Box;
        public List<CullingZoneComponent> CullingZones = new();

        internal MeshFilter _filter;
        private ColliderShape? _prevType;
        private static readonly Color GizmoColor = new Color(0.2f, 0.6f, 1f, 0.6f);

        public override void Compile(SchematicBlockData block)
        {
            var ids = new List<int>();
            foreach (var cullingZone in CullingZones)
            {
                ids.Add(cullingZone.transform.GetInstanceID());
            }
            block.Properties = new Dictionary<string, object>()
            {
                { "ColliderShape", Type },
                { "CullingZones", ids }
            };
            base.Compile(block);
        }

        public override void Decompile(ref GameObject gameObject, SchematicBlockData block, Transform parent)
        {
            var cullingZoneConnector = Create<CullingZoneConnectorComponent>("Assets/Resources/Blocks/CullingZoneConnector.prefab");
            gameObject = cullingZoneConnector.gameObject;
            cullingZoneConnector.Type = (ColliderShape)Convert.ToInt32(block.Properties["ColliderShape"]);
            
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