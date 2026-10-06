using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrnateBellRattle;

public static class BuildBellRattle
{
    const string Root = "Assets/BellRattle";
    static readonly HashSet<int> Bells = new HashSet<int>{8,12,14,15,16,17,18,20,21,26,27,29};
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Scenes");
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(Root + "/Textures", "*.JPEG"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType = path.Contains("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath("Assets/Source/Original.fbx");
        modelImporter.isReadable = true;
        modelImporter.importAnimation = false;
        modelImporter.SaveAndReimport();
        var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Source/Original.fbx"));
        var renderers = source.GetComponentsInChildren<Renderer>();
        if (renderers.Length != 42) throw new Exception("Expected 42 parts, got " + renderers.Length);
        var handle = renderers.First(r => r.name == "tripo_part_19");
        Vector3 grip = handle.bounds.center;
        Bounds total = renderers[0].bounds;
        foreach(var r in renderers) total.Encapsulate(r.bounds);
        var rig = new GameObject("OrnateBellRattle");
        rig.transform.localScale = Vector3.one * (0.45f / total.size.y);
        var clips = Directory.GetFiles(Root + "/Audio", "*.wav").OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\','/'))).ToArray();
        var report = new System.Text.StringBuilder();
        int triangles = 0;
        foreach (var renderer in renderers)
        {
            int id = int.Parse(renderer.name.Substring("tripo_part_".Length));
            var skin = renderer as SkinnedMeshRenderer;
            Mesh sourceMesh = skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            var mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = "Part_" + id;
            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            Vector3 center = Vector3.zero;
            float top = float.MinValue, bottom = float.MaxValue;
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]) - grip;
                center += vertices[i]; top = Mathf.Max(top, vertices[i].y); bottom = Mathf.Min(bottom, vertices[i].y);
            }
            center /= vertices.Length;
            Vector3 pivot = Vector3.zero;
            if (Bells.Contains(id))
            {
                int n=0;
                foreach(var v in vertices) if(v.y >= top - (top-bottom)*0.06f) { pivot+=v; n++; }
                pivot /= Mathf.Max(1,n);
            }
            for(int i=0;i<vertices.Length;i++) vertices[i] -= pivot;
            for(int i=0;i<normals.Length;i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.vertices = vertices; mesh.normals = normals;
            mesh.boneWeights = new BoneWeight[0]; mesh.bindposes = new Matrix4x4[0];
            if(matrix.determinant < 0)
            {
                for(int sm=0;sm<mesh.subMeshCount;sm++)
                {
                    int[] indices=mesh.GetTriangles(sm);
                    for(int k=0;k<indices.Length;k+=3) { int temp=indices[k+1]; indices[k+1]=indices[k+2]; indices[k+2]=temp; }
                    mesh.SetTriangles(indices,sm);
                }
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            triangles += mesh.triangles.Length/3;
            AssetDatabase.CreateAsset(mesh, Root + "/Meshes/Part_" + id + ".asset");
            var part = new GameObject((Bells.Contains(id)?"Bell_":"Fixed_")+id);
            part.transform.SetParent(rig.transform,false); part.transform.localPosition = pivot;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=part.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Standard"));
            material.name="Part_"+id;
            string prefix = Root + "/Textures/ornate_bell_rattle_3d_model_tripo_part_"+id;
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_basecolor.JPEG");
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_normal.JPEG"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic",Bells.Contains(id)?0.6f:0.25f);
            material.SetFloat("_Glossiness",Bells.Contains(id)?0.5f:0.3f);
            AssetDatabase.CreateAsset(material,Root+"/Materials/Part_"+id+".mat");mr.sharedMaterial=material;
            if(Bells.Contains(id))
            {
                part.AddComponent<AudioSource>().playOnAwake=false;
                var swing=part.AddComponent<BellSwing>();
                swing.restCenter=center-pivot; swing.chimes=clips;
                swing.frequency=2.7f+(id%5)*0.17f; swing.damping=0.22f+(id%3)*0.025f;
                swing.pitch=0.88f+(id%7)*0.04f;
            }
            report.AppendLine(id+", "+(Bells.Contains(id)?"bell":"fixed")+", "+pivot+", "+mesh.vertexCount);
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(rig,Root+"/Prefabs/OrnateBellRattle.prefab");
        UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(rig);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.AddComponent<BellRattleDemo>();
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.gameObject.AddComponent<AudioListener>();
        Bounds rigBounds=instance.GetComponentsInChildren<Renderer>()[0].bounds;
        foreach(var r in instance.GetComponentsInChildren<Renderer>())rigBounds.Encapsulate(r.bounds);
        camera.transform.position=rigBounds.center+new Vector3(0,0.03f,-0.92f);
        camera.transform.LookAt(rigBounds.center);camera.fieldOfView=36;camera.nearClipPlane=0.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.065f,0.085f,0.12f);
        var key=new GameObject("Key Light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.6f;key.transform.rotation=Quaternion.Euler(35,-30,0);
        var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=0.7f;fill.transform.rotation=Quaternion.Euler(-20,130,0);
        RenderSettings.ambientLight=new Color(0.48f,0.48f,0.52f);
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/BellRattleDemo.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/BellRattleDemo.unity",true)};
        AssetDatabase.SaveAssets();
        File.WriteAllText("../build_report.txt","Meshes: 42\nBells: 12\nTriangles: "+triangles+"\n"+report);
        Capture(camera,"../preview.png");
        Debug.Log("BELL_BUILD_SUCCESS triangles="+triangles);
    }
    static void Capture(Camera camera,string path)
    {
        var rt=new RenderTexture(1000,1000,24);camera.targetTexture=rt;camera.Render();
        RenderTexture.active=rt;var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1000,1000),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
    }
    [MenuItem("Tools/Ornate Bell Rattle/Adapt Materials to Current Pipeline")]
    public static void AdaptMaterials()
    {
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        string name=pipeline?pipeline.GetType().Name:"";
        string shaderName=name.Contains("Universal")?"Universal Render Pipeline/Lit":name.Contains("HDRender")?"HDRP/Lit":"Standard";
        var shader=Shader.Find(shaderName);if(!shader)throw new Exception("Shader unavailable: "+shaderName);
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            string prefix=Root+"/Textures/ornate_bell_rattle_3d_model_tripo_part_"+m.name.Substring(5);
            m.shader=shader;
            var color=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_basecolor.JPEG");
            var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_normal.JPEG");
            foreach(string key in new[]{"_MainTex","_BaseMap","_BaseColorMap"})if(m.HasProperty(key))m.SetTexture(key,color);
            foreach(string key in new[]{"_BumpMap","_NormalMap"})if(m.HasProperty(key))m.SetTexture(key,normal);
            m.EnableKeyword("_NORMALMAP");if(name.Contains("HDRender")){m.EnableKeyword("_NORMALMAP_TANGENT_SPACE");}
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }
    public static void Export()
    {
        AssetDatabase.ExportPackage(Root,"../../outputs/OrnateBellRattle_Unity.unitypackage",ExportPackageOptions.Recurse);
        Debug.Log("BELL_EXPORT_SUCCESS");
    }
}
