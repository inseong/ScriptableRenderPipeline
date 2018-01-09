using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Title("Utility", "Sub-graph")]
    public class SubGraphNode : AbstractMaterialNode
        , IGeneratesBodyCode
        , IOnAssetEnabled
        , IGeneratesFunction 
        , IMayRequireNormal
        , IMayRequireTangent
        , IMayRequireBitangent
        , IMayRequireMeshUV
        , IMayRequireScreenPosition
        , IMayRequireViewDirection
        , IMayRequirePosition
        , IMayRequireVertexColor
        , IMayRequireTime
    {
        [SerializeField]
        private string m_SerializedSubGraph = string.Empty;

        [Serializable]
        private class SubGraphHelper
        {
            public MaterialSubGraphAsset subGraph;
        }

        protected SubGraph referencedGraph
        {
            get
            {
                if (subGraphAsset == null)
                    return null;

                return subGraphAsset.subGraph;
            }
        }

#if UNITY_EDITOR
        [ObjectControl("")]
        public MaterialSubGraphAsset subGraphAsset
        {
            get
            {
                if (string.IsNullOrEmpty(m_SerializedSubGraph))
                    return null;

                var helper = new SubGraphHelper();
                EditorJsonUtility.FromJsonOverwrite(m_SerializedSubGraph, helper);
                return helper.subGraph;
            }
            set
            {
                if (subGraphAsset == value)
                    return;

                var helper = new SubGraphHelper();
                helper.subGraph = value;
                m_SerializedSubGraph = EditorJsonUtility.ToJson(helper, true);
                UpdateSlots();

                Dirty(ModificationScope.Topological);
            }
        }
#else
        public MaterialSubGraphAsset subGraphAsset {get; set; }
#endif

        public INode outputNode
        {
            get
            {
                if (subGraphAsset != null && subGraphAsset.subGraph != null)
                    return subGraphAsset.subGraph.outputNode;
                return null;
            }
        }

        public override bool hasPreview
        {
            get { return referencedGraph != null; }
        }

        public override PreviewMode previewMode
        {
            get
            {
                if (referencedGraph == null)
                    return PreviewMode.Preview2D;

                return PreviewMode.Preview3D;
            }
        }

        public SubGraphNode()
        {
            name = "Sub-graph";
        }

        public void GenerateNodeCode(ShaderGenerator visitor, GenerationMode generationMode)
        {
            if (referencedGraph == null)
                return;

            foreach (var outSlot in referencedGraph.graphOutputs)
                visitor.AddShaderChunk(string.Format("{0} {1};", NodeUtils.ConvertConcreteSlotValueTypeToString(precision, outSlot.concreteValueType), GetVariableNameForSlot(outSlot.id)), true);

            var arguments = new List<string>();
            foreach (var prop in referencedGraph.graphInputs)
            {
                var inSlotId = prop.guid.GetHashCode();

                if (prop is TextureShaderProperty)
                    arguments.Add(string.Format("TEXTURE2D_PARAM({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else if (prop is CubemapShaderProperty)
                    arguments.Add(string.Format("TEXTURECUBE_PARAM({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else
                    arguments.Add(GetSlotValue(inSlotId, generationMode));
            }

            // pass surface inputs through
            arguments.Add("IN");

            foreach (var outSlot in referencedGraph.graphOutputs)
                arguments.Add(GetVariableNameForSlot(outSlot.id));

            visitor.AddShaderChunk(
                string.Format("{0}({1});"
                    , SubGraphFunctionName()
                    , arguments.Aggregate((current, next) => string.Format("{0}, {1}", current, next)))
                , false);
        }

        public void OnEnable()
        {
            UpdateSlots();
        }

        public virtual void UpdateSlots()
        {
            var validNames = new List<int>();
            if (referencedGraph == null)
            {
                RemoveSlotsNameNotMatching(validNames);
                return;
            }

            var props = referencedGraph.properties;
            foreach (var prop in props)
            {
                var propType = prop.propertyType;
                SlotValueType slotType;

                switch (propType)
                {
                    case PropertyType.Color:
                        slotType = SlotValueType.Vector4;
                        break;
                    case PropertyType.Texture:
                        slotType = SlotValueType.Texture2D;
                        break;
                    case PropertyType.Cubemap:
                        slotType = SlotValueType.Cubemap;
                        break;
                    case PropertyType.Float:
                        slotType = SlotValueType.Vector1;
                        break;
                    case PropertyType.Vector2:
                        slotType = SlotValueType.Vector2;
                        break;
                    case PropertyType.Vector3:
                        slotType = SlotValueType.Vector3;
                        break;
                    case PropertyType.Vector4:
                        slotType = SlotValueType.Vector4;
                        break;
                    case PropertyType.Matrix2:
                        slotType = SlotValueType.Matrix2;
                        break;
                    case PropertyType.Matrix3:
                        slotType = SlotValueType.Matrix3;
                        break;
                    case PropertyType.Matrix4:
                        slotType = SlotValueType.Matrix4;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var id = prop.guid.GetHashCode();
                MaterialSlot slot = MaterialSlot.CreateMaterialSlot(slotType, id, prop.displayName, prop.referenceName, SlotType.Input, prop.defaultValue);
                // copy default for texture for niceness
                if (slotType == SlotValueType.Texture2D && propType == PropertyType.Texture)
                {
                    var tSlot = slot as Texture2DInputMaterialSlot;
                    var tProp = prop as TextureShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.texture = tProp.value.texture;
                }
                // copy default for cubemap for niceness
                else if (slotType == SlotValueType.Cubemap && propType == PropertyType.Cubemap)
                {
                    var tSlot = slot as CubemapInputMaterialSlot;
                    var tProp = prop as CubemapShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.cubemap = tProp.value.cubemap;
                }
                AddSlot(slot);
                validNames.Add(id);
            }

            var subGraphOutputNode = outputNode;
            if (outputNode != null)
            {
                foreach (var slot in NodeExtensions.GetInputSlots<MaterialSlot>(subGraphOutputNode))
                {
                    AddSlot(MaterialSlot.CreateMaterialSlot(slot.valueType, slot.id, slot.RawDisplayName(), slot.shaderOutputName, SlotType.Output, Vector4.zero));
                    validNames.Add(slot.id);
                }
            }

            RemoveSlotsNameNotMatching(validNames);
        }

        public override void ValidateNode()
        {
            if (referencedGraph != null)
            {
                referencedGraph.OnEnable();
                referencedGraph.ValidateGraph();

                if (referencedGraph.GetNodes<INode>().Any(x => x.hasError))
                    hasError = true;
            }

            base.ValidateNode();
        }

        public override void CollectShaderProperties(PropertyCollector visitor, GenerationMode generationMode)
        {
            base.CollectShaderProperties(visitor, generationMode);

            if (referencedGraph == null)
                return;

            referencedGraph.CollectShaderProperties(visitor, GenerationMode.ForReals);
        }

        public override void CollectPreviewMaterialProperties(List<PreviewProperty> properties)
        {
            base.CollectPreviewMaterialProperties(properties);

            if (referencedGraph == null)
                return;

            properties.AddRange(referencedGraph.GetPreviewProperties());
        }

        private string SubGraphFunctionName()
        {
            var functionName = subGraphAsset != null ? NodeUtils.GetHLSLSafeName(subGraphAsset.name) : "ERROR";
            return string.Format("{0}_{1}", functionName, GuidEncoder.Encode(referencedGraph.guid));
        }

        public virtual void GenerateNodeFunction(FunctionRegistry registry, GenerationMode generationMode)
        {
            if (subGraphAsset == null || referencedGraph == null)
                return;

            referencedGraph.GenerateNodeFunction(registry, GenerationMode.ForReals);
            referencedGraph.GenerateSubGraphFunction(SubGraphFunctionName(), registry, ShaderGraphRequirements.FromNodes(new List<INode> {this}), GenerationMode.ForReals);
        }

        public NeededCoordinateSpace RequiresNormal()
        {
            if (referencedGraph == null)
                return NeededCoordinateSpace.None;

            return referencedGraph.activeNodes.OfType<IMayRequireNormal>().Aggregate(NeededCoordinateSpace.None, (mask, node) =>
            {
                mask |= node.RequiresNormal();
                return mask;
            });
        }

        public bool RequiresMeshUV(UVChannel channel)
        {
            if (referencedGraph == null)
                return false;

            return referencedGraph.activeNodes.OfType<IMayRequireMeshUV>().Any(x => x.RequiresMeshUV(channel));
        }

        public bool RequiresScreenPosition()
        {
            if (referencedGraph == null)
                return false;

            return referencedGraph.activeNodes.OfType<IMayRequireScreenPosition>().Any(x => x.RequiresScreenPosition());
        }

        public NeededCoordinateSpace RequiresViewDirection()
        {
            if (referencedGraph == null)
                return NeededCoordinateSpace.None;

            return referencedGraph.activeNodes.OfType<IMayRequireViewDirection>().Aggregate(NeededCoordinateSpace.None, (mask, node) =>
            {
                mask |= node.RequiresViewDirection();
                return mask;
            });
        }

        public NeededCoordinateSpace RequiresPosition()
        {
            if (referencedGraph == null)
                return NeededCoordinateSpace.None;

            return referencedGraph.activeNodes.OfType<IMayRequirePosition>().Aggregate(NeededCoordinateSpace.None, (mask, node) =>
            {
                mask |= node.RequiresPosition();
                return mask;
            });
        }

        public NeededCoordinateSpace RequiresTangent()
        {
            if (referencedGraph == null)
                return NeededCoordinateSpace.None;

            return referencedGraph.activeNodes.OfType<IMayRequireTangent>().Aggregate(NeededCoordinateSpace.None, (mask, node) =>
            {
                mask |= node.RequiresTangent();
                return mask;
            });
        }

        public bool RequiresTime()
        {
            if (referencedGraph == null)
                return false;

            return referencedGraph.activeNodes.OfType<IMayRequireTime>().Any(x => x.RequiresTime());
        }

        public NeededCoordinateSpace RequiresBitangent()
        {
            if (referencedGraph == null)
                return NeededCoordinateSpace.None;

            return referencedGraph.activeNodes.OfType<IMayRequireBitangent>().Aggregate(NeededCoordinateSpace.None, (mask, node) =>
            {
                mask |= node.RequiresBitangent();
                return mask;
            });
        }

        public bool RequiresVertexColor()
        {
            if (referencedGraph == null)
                return false;

            return referencedGraph.activeNodes.OfType<IMayRequireVertexColor>().Any(x => x.RequiresVertexColor());
        }
    }
}
