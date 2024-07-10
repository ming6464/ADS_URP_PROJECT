using System.IO;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.ShaderGraph;


namespace AmazingAssets.ShaderGraphBaker
{
    static internal class TextureBaker
    {
        static Mesh quadMesh = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;


        static internal void Bake(Node node, Enum.TextureType textureType)
        {
            string savePath = GetTextureSavePath(node, node.Format, node.OutputTexture);
            if (string.IsNullOrWhiteSpace(savePath))
                return;


            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            Material material = GetPreviewMaterial(node, materialPropertyBlock, node.AlphaGammaCorrected.isOn);
            if (material == null)
                return;


            UnityEngine.Texture2D texture = GetPreviewTexture(material, materialPropertyBlock, node.Resolution, node.Antialiasing.isOn, node.Format, textureType);
            if (texture == null)
                return;




            byte[] bytes;
            switch (node.Format)
            {
                case Enum.Format.JPG: bytes = texture.EncodeToJPG(100); break;
                case Enum.Format.TGA: bytes = texture.EncodeToTGA(); break;
                case Enum.Format.EXR: bytes = texture.EncodeToEXR(UnityEngine.Texture2D.EXRFlags.None); break;
                case Enum.Format.EXRZip: bytes = texture.EncodeToEXR(UnityEngine.Texture2D.EXRFlags.CompressZIP); break;

                case Enum.Format.PNG:
                default: bytes = texture.EncodeToPNG(); break;
            }


            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.ImportAsset(savePath);


            UnityEngine.Object savedFile = AssetDatabase.LoadAssetAtPath(savePath, typeof(UnityEngine.Texture2D));
            ReimportTexture(savePath, textureType);

            if (node.OutputTexture == null)
                UnityEditor.EditorGUIUtility.PingObject(savedFile);
            else
                node.OutputTexture = (UnityEngine.Texture2D)savedFile;



            //Cleanup           
            GameObject.DestroyImmediate(material.shader);
            GameObject.DestroyImmediate(material);
            GameObject.DestroyImmediate(texture);
        }
        static UnityEngine.Texture2D GetPreviewTexture(Material material, MaterialPropertyBlock materialPropertyBlock, Enum.Resolution textureResolution, bool superSize, Enum.Format textureFormat, Enum.TextureType textureType)
        {            
            int textureSize = GetTextureSize(textureResolution, false);
            int textureSuperSize = GetTextureSize(textureResolution, superSize);

            RenderTextureReadWrite rtColorSpace = textureType == Enum.TextureType.Normal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default;

            RenderTexture renderTexture = null;
            RenderTexture renderTextureSuperSize = null;
            UnityEngine.Texture2D texture = null;
            if (textureFormat == Enum.Format.EXR || textureFormat == Enum.Format.EXRZip)
            {
                renderTexture = RenderTexture.GetTemporary(textureSuperSize, textureSuperSize, 0, RenderTextureFormat.ARGBFloat, rtColorSpace);

                if (superSize)
                    renderTextureSuperSize = RenderTexture.GetTemporary(textureSize, textureSize, 0, RenderTextureFormat.ARGBFloat, rtColorSpace);

                texture = new UnityEngine.Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, false);
            }
            else
            {
                renderTexture = RenderTexture.GetTemporary(textureSuperSize, textureSuperSize, 0, RenderTextureFormat.ARGB32, rtColorSpace);

                if (superSize)
                    renderTextureSuperSize = RenderTexture.GetTemporary(textureSize, textureSize, 0, RenderTextureFormat.ARGB32, rtColorSpace);

                texture = new UnityEngine.Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
            }



            GameObject cameraGO = new GameObject();
            cameraGO.transform.position = Vector3.forward * -1;
            cameraGO.transform.rotation = Quaternion.identity;

            Camera camera = cameraGO.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Preview;
            camera.orthographic = true;
            camera.orthographicSize = 0.5f;
            camera.farClipPlane = 10.0f;
            camera.nearClipPlane = 1.0f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.clear;
            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.allowMSAA = false;
            camera.allowHDR = true;


            camera.targetTexture = renderTexture;
            Graphics.DrawMesh(quadMesh, Matrix4x4.identity, material, 1, camera, 0, materialPropertyBlock, ShadowCastingMode.Off, false, null, false);
            camera.Render();


            if(superSize)
            {
                Graphics.Blit(renderTexture, renderTextureSuperSize);

                RenderTexture.active = renderTextureSuperSize;
            }
            else
            {
                 RenderTexture.active = renderTexture;
            }

            
            texture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0, false);
            texture.Apply(false);



            //Cleanup
            RenderTexture.active = null;

            GameObject.DestroyImmediate(cameraGO);
            RenderTexture.ReleaseTemporary(renderTexture);
            if (renderTextureSuperSize != null)
                RenderTexture.ReleaseTemporary(renderTextureSuperSize);



            return texture;
        }
        static Material GetPreviewMaterial(Node node, MaterialPropertyBlock materialPropertyBlock, bool alphaGammaCorrected)
        {
            var wasAsyncAllowed = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = true;


            HashSet<AbstractMaterialNode> sources = new HashSet<AbstractMaterialNode>() { node };
            HashSet<AbstractMaterialNode> nodesToDraw = new HashSet<AbstractMaterialNode>();
            PreviewManager.PropagateNodes(sources, PreviewManager.PropagationDirection.Upstream, nodesToDraw);


            PooledList<PreviewProperty> perMaterialPreviewProperties = PooledList<PreviewProperty>.Get();
            PreviewManager.CollectPreviewProperties(node.owner, nodesToDraw, perMaterialPreviewProperties, materialPropertyBlock);


            Generator generator = new Generator(node.owner, node, GenerationMode.ForReals, $"hidden/preview/{node.GetVariableNameForNode()}", null);
            string generatedShader = generator.generatedShader;



            //fix alpha rendering
            ShaderStringBuilder shaderStringBuilder = new ShaderStringBuilder();
            node.GenerateNodeCode(shaderStringBuilder, GenerationMode.Preview);
            string sData = shaderStringBuilder.ToString();
            sData = sData.Substring(0, sData.IndexOf(';'));
            if (sData.Contains("$precision4 "))
            {
                string varName = sData.Substring(sData.IndexOf(" ") + 1);

                //Linear > RGB
                //https://docs.unity3d.com/Packages/com.unity.shadergraph@10.10/manual/Colorspace-Conversion-Node.html
                if (QualitySettings.activeColorSpace == ColorSpace.Linear && alphaGammaCorrected)
                    generatedShader = generatedShader.Replace($"{varName}.z, 1.0", $"{varName}.z, ({varName}.w <= 0.0031308 ? ({varName}.w * 12.92) : ((pow(max(abs({varName}.w), 1.192092896e-07), 1.0 / 2.4) * 1.055) - 0.055))");
                else
                    generatedShader = generatedShader.Replace($"{varName}.z, 1.0", $"{varName}.z, {varName}.w");
            }
            else if (sData.Contains("$precision "))
            {
                string varName = sData.Substring(sData.IndexOf(" ") + 1);

                //Linear > RGB
                //https://docs.unity3d.com/Packages/com.unity.shadergraph@10.10/manual/Colorspace-Conversion-Node.html
                if (QualitySettings.activeColorSpace == ColorSpace.Linear && alphaGammaCorrected)
                    generatedShader = generatedShader.Replace($"{varName}, 1.0", $"{varName}, ({varName} <= 0.0031308 ? ({varName} * 12.92) : ((pow(max(abs({varName}), 1.192092896e-07), 1.0 / 2.4) * 1.055) - 0.055))");
                else
                    generatedShader = generatedShader.Replace($"{varName}, 1.0", $"{varName}, {varName}");
            }



            Shader shader = ShaderUtil.CreateShaderAsset(generatedShader);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                return null;

            Material material = new Material(shader);
            PreviewManager.AssignPerMaterialPreviewProperties(material, perMaterialPreviewProperties);


            ShaderUtil.allowAsyncCompilation = wasAsyncAllowed;

            return material;
        }
        static string GetTextureSavePath(Node node, Enum.Format format, Texture sourceTexture)
        {
            if (sourceTexture != null)
            {
                string path = AssetDatabase.GetAssetPath(sourceTexture);
                if (string.IsNullOrWhiteSpace(path) == false)
                {
                    if (Path.GetExtension(path).ToLowerInvariant() == ("." + GetTextureSaveExtension(format)))
                    {
                        return path;
                    }
                    else
                    {
                        string newPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)) + "." + GetTextureSaveExtension(format);

                        //Change file extension
                        try
                        {
                            File.Move(path, newPath);
                        }
                        catch (System.Exception)
                        {
                            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(newPath, typeof(Texture));
                            Debug.LogError($"Texture baking has failed.\nFile with an extension '{Path.GetExtension(newPath)}' already exists.\n'{newPath}'.\nRemove above file first for overwriting current texture.\n", asset);
                            return string.Empty;
                        }
                        

                        //Update meta
                        if (File.Exists(path + ".meta"))
                            File.Move(path + ".meta", newPath + ".meta");

                        AssetDatabase.Refresh();

                        return newPath;
                    }
                }
            }


            string graphPath = string.Empty;
            GUID guid;
            if (GUID.TryParse(node.owner.owner.graph.assetGuid, out guid))
                graphPath = AssetDatabase.GUIDToAssetPath(guid);


            string savePath;
            if (File.Exists(graphPath))
                savePath = UnityEditor.EditorUtility.SaveFilePanel("Save texture", Path.GetDirectoryName(graphPath), Path.GetFileNameWithoutExtension(graphPath), GetTextureSaveExtension(format));
            else
                savePath = UnityEditor.EditorUtility.SaveFilePanel("Save texture", "Assets", "New Shader Graph Texture", GetTextureSaveExtension(format));


            if (string.IsNullOrWhiteSpace(savePath) == false && savePath.Length > Application.dataPath.Length)
            {
                //If extension is changed inside SavePanel
                string extension = Path.GetExtension(savePath).ToLowerInvariant();
                if ((extension == ".jpg" || extension == ".png" || extension == ".tga" || extension == ".exr") == false)
                    savePath = Path.ChangeExtension(savePath, "." + GetTextureSaveExtension(format));


                savePath = "Assets" + savePath.Substring(Application.dataPath.Length);

                return savePath;
            }
            else
                return string.Empty;
        }
        static string GetTextureSaveExtension(Enum.Format format)
        {
            if (format == Enum.Format.EXRZip)
                return "exr";
            else
                return format.ToString().ToLowerInvariant();
        }
        static int GetTextureSize(Enum.Resolution resolution, bool superSize)
        {
            int size;
            if (int.TryParse(resolution.ToString().Replace("_", string.Empty), out size) == false)
                size = 1024;


            //We do not need to reach limits
            if (superSize && size * 2 < SystemInfo.maxTextureSize)
                size *= 2;


            size = (int)Mathf.Clamp(size, 16, SystemInfo.maxTextureSize);


            return size;
        }
        static void ReimportTexture(string assetPath, Enum.TextureType textureType)
        {
            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (textureImporter != null)
            {
                if (textureType == Enum.TextureType.Normal)
                {
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.convertToNormalmap = false;
                }
                else
                {
                    textureImporter.textureType = TextureImporterType.Default;
                }

                textureImporter.textureShape = TextureImporterShape.Texture2D;                

                textureImporter.SaveAndReimport();
            }
        }
    }
}
