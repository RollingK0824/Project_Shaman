using UnityEngine;
using UnityEditor;
namespace Exorcist.FirstPerson.Editor
{
    public static class RightHandMaterials
    {
        [MenuItem("Tools/Exorcist Right Hand/Adapt Materials to Current Pipeline")]
        public static void Convert()
        {
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            string pipelineName=pipeline?pipeline.GetType().Name:"";
            string shaderName=pipelineName.Contains("Universal")?"Universal Render Pipeline/Lit":pipelineName.Contains("HDRender")?"HDRP/Lit":"Standard";
            var shader=Shader.Find(shaderName);
            if(!shader){Debug.LogError("Cannot find shader: "+shaderName);return;}
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/FirstPersonRightHand/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Texture tex=material.mainTexture;Color color=material.color;
                material.shader=shader;
                foreach(string property in new[]{"_MainTex","_BaseMap","_BaseColorMap"})if(material.HasProperty(property))material.SetTexture(property,tex);
                foreach(string property in new[]{"_Color","_BaseColor"})if(material.HasProperty(property))material.SetColor(property,color);
                if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.18f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
