using System;
using System.Linq;
using UnityEngine;
using UnityEditor.Rendering.MaterialVariants;

namespace UnityEditor.Rendering
{
    public abstract class SRPShaderGUI : ShaderGUI
    {
        protected MaterialVariant[] variants;
        protected UnityEngine.Object[] targets;

        /// <summary>
        /// Unity calls this function when you assign a new shader to the material.
        /// </summary>
        /// <param name="material">The current material.</param>
        /// <param name="oldShader">The shader the material currently uses.</param>
        /// <param name="newShader">The new shader to assign to the material.</param>
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            var variant = MaterialVariant.GetMaterialVariantFromObject(material);
            if (variant)
                variant.SetParent(newShader);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            variants = MaterialVariant.GetMaterialVariantsFor(materialEditor);
            targets = materialEditor.targets;
        }

        public MaterialPropertyScope CreateOverrideScopeFor(MaterialProperty property, bool forceMode = false)
            => CreateOverrideScopeFor(new MaterialProperty[] { property }, forceMode);

        public MaterialPropertyScope CreateOverrideScopeFor(params MaterialProperty[] properties)
            => CreateOverrideScopeFor(properties, false);

        public MaterialPropertyScope CreateOverrideScopeFor(MaterialProperty[] properties, bool forceMode = false)
        {
            if (variants != null)
                return new MaterialPropertyScope(properties, variants, forceMode);
            else
                return new MaterialPropertyScope(properties, targets);
        }

        public MaterialRenderQueueScope CreateRenderQueueOverrideScope(Func<int> valueGetter)
            => new MaterialRenderQueueScope(variants, valueGetter);

        public MaterialNonDrawnPropertyScope<T> CreateNonDrawnOverrideScope<T>(string propertyName, T value)
            where T : struct
            => new MaterialNonDrawnPropertyScope<T>(propertyName, value, variants);

        public void ShaderProperty(MaterialEditor materialEditor, MaterialProperty materialProperty, GUIContent label, int indent = 0)
        {
            using (CreateOverrideScopeFor(materialProperty))
            {
                materialEditor.ShaderProperty(materialProperty, label, indent);
            }
        }

        public void MinMaxSliderProperty(GUIContent label, MaterialProperty min, MaterialProperty max, float minLimit, float maxLimit)
        {
            using (CreateOverrideScopeFor(min, max))
            {
                float minValue = min.floatValue;
                float maxValue = max.floatValue;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.MinMaxSlider(label, ref minValue, ref maxValue, minLimit, maxLimit);
                if (EditorGUI.EndChangeCheck())
                {
                    min.floatValue = minValue;
                    max.floatValue = maxValue;
                }
            }
        }
    }
}
