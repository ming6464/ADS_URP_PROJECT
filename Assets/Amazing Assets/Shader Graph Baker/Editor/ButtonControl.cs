using System;
using System.Reflection;

using UnityEngine.UIElements;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing.Controls;


namespace AmazingAssets.ShaderGraphBaker
{
    class ButtonControlAttribute : Attribute, IControlAttribute
    {
        public ButtonControlAttribute()
        {

        }

        VisualElement IControlAttribute.InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            if (!(node is Node))
                throw new ArgumentException("Property must be a 'Shader Graph Baker' Node.", "node");

            return new ButtonControlView((Node)node);
        }
    }

    class ButtonControlView : VisualElement
    {
        Node m_Node;
        UnityEngine.UIElements.Button m_Button;

        public ButtonControlView(Node node)
        {
            m_Node = node;

            m_Button = new Button(Callback);

            m_Node.m_button = m_Button;
            m_Node.UpdateBakeButtonName();

            Add(m_Button);            
        }

        void Callback()
        {            
            m_Node.BakeButtonCallback();
        }
    }
}