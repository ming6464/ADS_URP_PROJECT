using System.Reflection;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.ShaderGraph.Drawing.Controls;


namespace AmazingAssets.ShaderGraphBaker
{
    internal class Enum
    {
        public enum Resolution { _16, _32, _64, _128, _256, _512, _, _1024, _2048, _4096, _8192 }
        public enum Format { JPG, PNG, TGA, _, EXR, EXRZip }
        public enum TextureType { Default, Normal }
    }

    [Title("Amazing Assets", "Shader Graph Baker")]
    class Node : CodeFunctionNode
    {
        public override string documentationURL => "https://docs.google.com/document/d/18kOp3puA07dLq4vB2lSVqMDNCeYslyxvtKcUtsP63_E/edit?usp=sharing";


        [SerializeField]
        private Enum.Resolution m_Resolution = Enum.Resolution._1024;
        [EnumControl("Resolution")]
        public Enum.Resolution Resolution
        {
            get { return m_Resolution; }
            set
            {
                if (m_Resolution == value)
                    return;

                m_Resolution = value;

                Dirty(ModificationScope.Node);
            }
        }


        [SerializeField]
        private Enum.Format m_Format = Enum.Format.PNG;
        [EnumControl("Format")]
        public Enum.Format Format
        {
            get { return m_Format; }
            set
            {
                if (m_Format == value)
                    return;

                m_Format = value;

                Dirty(ModificationScope.Node);
            }
        }


        [SerializeField]
        private Enum.TextureType m_TextureType = Enum.TextureType.Default;
        [EnumControl("Texture Type")]
        public Enum.TextureType TextureType
        {
            get { return m_TextureType; }
            set
            {
                if (m_TextureType == value)
                    return;

                m_TextureType = value;

                Dirty(ModificationScope.Node);
            }
        }


        [SerializeField]
        private bool m_Antialiasing = false;
        [ToggleControl("Antialiasing")]
        public ToggleData Antialiasing
        {
            get { return new ToggleData(m_Antialiasing); }
            set
            {
                if (m_Antialiasing == value.isOn)
                    return;

                m_Antialiasing = value.isOn;

                Dirty(ModificationScope.Node);
            }
        }


        [SerializeField]
        private bool m_AlphaGammaCorrected = true;
        [ToggleControl("Alpha Gamma Corrected")]
        public ToggleData AlphaGammaCorrected
        {
            get { return new ToggleData(m_AlphaGammaCorrected); }
            set
            {
                if (m_AlphaGammaCorrected == value.isOn)
                    return;

                m_AlphaGammaCorrected = value.isOn;

                Dirty(ModificationScope.Node);
            }
        }


        [ButtonControl()]
        int buttonControll { get; set; }
        public UnityEngine.UIElements.Button m_button;


        [SerializeField]
        private SerializableTexture m_OutputTexture = new SerializableTexture();
        [TextureControl("")]
        public UnityEngine.Texture OutputTexture
        {
            get
            {
                return m_OutputTexture.texture;
            }

            set
            {
                if (m_OutputTexture.texture == value)
                {
                    return;
                }

                m_OutputTexture.texture = value;

                UpdateBakeButtonName();

                Dirty(ModificationScope.Node);
            }
        }
        


        public Node()
        {
            name = "Shader Graph Baker";

            previewExpanded = true;
            m_PreviewMode = PreviewMode.Preview2D;            
        }

        protected override MethodInfo GetFunctionToConvert()
        {
            string functionName = "FunctionDefault";
            if (TextureType == Enum.TextureType.Normal && CanBakeNormal())
                functionName = "FunctionNormal";


            return GetType().GetMethod(functionName, BindingFlags.Static | BindingFlags.NonPublic);
        }
         
        static string FunctionDefault(
            [Slot(0, Binding.None)] DynamicDimensionVector In,
            [Slot(1, Binding.None)] out DynamicDimensionVector Out)
        {
            return @"{Out = In;}";
        }

        static string FunctionNormal(
            [Slot(0, Binding.None)] DynamicDimensionVector In,
            [Slot(1, Binding.None)] out DynamicDimensionVector Out)
        {
            return @"{Out = float4(In.r * 0.5 + 0.5, In.g * 0.5 + 0.5, 1, 1);}";
        }

        public void UpdateBakeButtonName()
        {
            m_button.text = OutputTexture == null ? "Bake" : "Bake  (Overwrite)";
        }

        public void BakeButtonCallback()
        {
            Enum.TextureType tType = this.TextureType;

            if (TextureType == Enum.TextureType.Normal && CanBakeNormal() == false)
            {
                Debug.LogError("Cannot bake NormalMap. Input slot must be Vector3 or Vector4.\n");
                tType = Enum.TextureType.Default;
            }

            TextureBaker.Bake(this, tType);
        }

        public bool CanBakeNormal()
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetInputSlots(slots);
            if (slots != null && slots.Count == 1 && slots[0].isConnected)
            {
                if (slots[0].displayName.Contains("(3)") || slots[0].displayName.Contains("(4)"))
                    return true;
            }

            return false;
        }
    }
}
 