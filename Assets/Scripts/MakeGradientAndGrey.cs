// using System.Collections;
using System.Collections.Generic;
// using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
public class GradientMapGenerator : EditorWindow
{   
    // static private bool DebugMode;
    static private Shader GreyMapShader;
    
    static private Texture2D gradientMap;
    static private RenderTexture lookupMap;
    
    static private RenderTexture HistogramMap;
    static public Texture2D sourceImage;
    static private List<Vector3> hsvPixels;
    static int mip;
    static float minH = 0;
    static float maxH = 0;
    static float minS = 0;
    static float maxS = 0;
    static float minV = 0;
    static float maxV = 0;
    static string result;
    static string pathDir;
    static Color[] pixels;
    
    [MenuItem("gradient/GetTexInfo")]
    static void showWindow(){
        var window = GetWindowWithRect<GradientMapGenerator>(new Rect(0, 0, 1024, 1024));
        init();
        window.Show();
    }
    static void LoadShader(){
        try
        {
            GreyMapShader = (Shader)AssetDatabase.LoadAssetAtPath("Assets/Shader/GradientMapGenerator.shadergraph", typeof(Shader));
        }
        catch (System.Exception)
        {
            throw;
        }
    }
    static void init(){
        // DebugMode = false;
        result = "";
        LoadShader();
        
    }
    static int SortByS(Vector3 pixel1,Vector3 pixel2){
        return pixel1.y.CompareTo(pixel2.y);
    }
    static int SortByV(Vector3 pixel1,Vector3 pixel2){
        return pixel1.z.CompareTo(pixel2.z);
    }
    static int SortByH(Vector3 pixel1,Vector3 pixel2){
        return pixel1.x.CompareTo(pixel2.x);
    }
    static void SortAllPixels(int sortby = 0){
        if(hsvPixels.Count<=0){
            return;
        }
       if(sortby==0){
           hsvPixels.Sort(SortByS);
       }
       if(sortby==1){
           hsvPixels.Sort(SortByV);
           
       }
       else{
           hsvPixels.Sort(SortByH);
       }
    }
    static void GetHSVRange(int mip){
        #region ???????????????
        String path = AssetDatabase.GetAssetPath(sourceImage);
        readwriteOnOff(path,true);
        Color[] _pixels = sourceImage.GetPixels(mip);
        hsvPixels = new List<Vector3>();
        float tempH = 0;
        float tempS = 0;
        float tempV = 0;
        float posMinH = 0;
        float posMaxH = 0;
        Color.RGBToHSV(_pixels[0],out tempH,out tempS,out tempV);
        tempH = (float)Math.Floor(tempH * 360);
        posMinH = posMaxH = tempH;
        minS = minS = tempS;
        minV = maxV = tempV;
        foreach (var pixel in _pixels)
        {   

            Color.RGBToHSV(pixel,out tempH,out tempS,out tempV);
            tempH = (float)Math.Floor(tempH * 360);
            hsvPixels.Add(new Vector3(tempH,tempS,tempV));
            if(tempH < posMinH){
                posMinH = tempH;
            }
            if(tempH > posMaxH){
                posMaxH = tempH;
            }

            #region sv
            if(tempS<minS){
                minS = tempS;
            }
            if(tempS>maxS){
                maxS = tempS;
            }

            if(tempV<minV){
                minV = tempV;
            }
            if(tempV>maxV){
                maxV = tempV;
            }
            #endregion
        }
        
        #endregion
        #region ????????????????????????
        if(hsvPixels.Count<=0){
            Debug.LogError("HSV pixels Array is Empty!");
        }
        SortAllPixels(2);
        float maxInterval = 0;
        float markMaxHue = 0;
        float markMinHue = 0;
        for(int i = 1; i < hsvPixels.Count; i++){
            float interval = hsvPixels[i].x - hsvPixels[i-1].x;
            
            if(maxInterval<interval){
                markMinHue = hsvPixels[i].x; //mark this as new Min Value
                markMaxHue = hsvPixels[i-1].x;
                maxInterval = interval;
            }
        }
        #endregion
        minH = posMinH;
        maxH = posMaxH;
        if(posMaxH - posMinH > markMaxHue - (markMinHue-360)){
            minH = markMinHue-360;
            maxH = markMaxHue;
        }
        readwriteOnOff(path,false);
        result = $"min hue is {(minH)}\nmax hue is {(maxH)}\nminV is {minV}\nmaxV is {maxV}\nminS is {minS}\nmaxS is {maxS}";
    }
    static void generateGreyMap(Color[] _lookup,Color[] source,Texture2D _sourceImage){
        if(GreyMapShader==null){
            LoadShader();
        }
        Texture2D tempTex = new Texture2D(_sourceImage.width,_sourceImage.height,TextureFormat.RGBA32,false,true);
        Color[] greyPixels = MedianCut.FindCloestColor(_lookup,source);
        tempTex.SetPixels(greyPixels);
        tempTex.Apply();
        Material tempMat = new Material(GreyMapShader);
        RenderTexture checkMap = new RenderTexture(_sourceImage.width,_sourceImage.height,0,RenderTextureFormat.Default,RenderTextureReadWrite.Linear); //??????????????????????????????????????? ???????????????????????? ????????????????????????????????????
        tempMat.SetTexture("_SourceTex",tempTex);
        tempMat.SetInt("_Grey",0);
        Graphics.Blit(tempTex,checkMap,tempMat);
        tempMat.SetInt("_Grey",1);
        tempMat.SetTexture("_SourceTex",tempTex);
        RenderTexture greyMap = new RenderTexture(_sourceImage.width,_sourceImage.height,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
        Graphics.Blit(tempTex,greyMap,tempMat);
        //???????????????

        Texture2D reference = toTexture2D(checkMap,TextureFormat.RGB24);
        Texture2D grey = toTexture2D(greyMap,TextureFormat.R8);;
            
        SaveTexturePNG(reference,$"{_sourceImage.name}_reference");
        SaveTexturePNG(grey,$"{_sourceImage.name}_grey");
        SaveTexturePNG(tempTex,$"{_sourceImage.name}_raw");
        AssetDatabase.Refresh();
        //???????????????
    }

    static void SaveTexturePNG(Texture2D sourceTex,string name ="result")
    {
        byte[] bytes = ImageConversion.EncodeArrayToPNG(sourceTex.GetRawTextureData(), sourceTex.graphicsFormat, (uint)sourceTex.width, (uint)sourceTex.height);
        File.WriteAllBytes(Application.dataPath +  $"/{name}.png", bytes);
    }
    static void SaveTextureEXR(Texture2D sourceTex,string name ="result")
    {
        byte[] bytes = ImageConversion.EncodeArrayToEXR(sourceTex.GetRawTextureData(), sourceTex.graphicsFormat, (uint)sourceTex.width, (uint)sourceTex.height);
        File.WriteAllBytes(Application.dataPath +  $"/{name}.exr", bytes);
    }
    static Texture2D toTexture2D(RenderTexture RT,TextureFormat format = TextureFormat.RFloat) //???????????????????????????????????????
    {
        Texture2D tex = new Texture2D(RT.width, RT.height, format, false);
        RenderTexture.active = RT;
        tex.ReadPixels(new Rect(0, 0, RT.width, RT.height), 0, 0);
        tex.Apply();
        return tex;
    }
    static bool getPixelRange(int mip = 0)
    {
        Color[] pixels = sourceImage.GetPixels(mip);
        hsvPixels = new List<Vector3>();
        float tempH = 0;
        float tempS = 0;
        float tempV = 0;
        float negMaxH = 0;
        float negMinH = 0;
        float posMinH = 0;
        float posMaxH = 0;
        Color.RGBToHSV(pixels[0],out tempH,out tempS,out tempV);
        
        tempH = (float)Math.Floor(tempH * 360);
        posMinH = posMaxH = tempH;
        if(tempH >=180){
            tempH -= 360;
        }
        negMinH = negMaxH = tempH; 
        minS = minS = tempS;
        minV = maxV = tempV;
        

        foreach (var pixel in pixels)
        {   

            Color.RGBToHSV(pixel,out tempH,out tempS,out tempV);
            tempH = (float)Math.Floor(tempH * 360);
            hsvPixels.Add(new Vector3(tempH,tempS,tempV));
            if(tempH < posMinH){
                posMinH = tempH;
            }
            if(tempH > posMaxH){
                posMaxH = tempH;
            }
            if(tempH >=180){
                tempH -= 360;
            }
            if(tempH < negMinH){
                negMinH = tempH; 
            }
            if(tempH > negMaxH){
                negMaxH = tempH;
            }
            #region sv
            if(tempS<minS){
                minS = tempS;
            }
            if(tempS>maxS){
                maxS = tempS;
            }

            if(tempV<minV){
                minV = tempV;
            }
            if(tempV>maxV){
                maxV = tempV;
            }
            #endregion
        }
        bool pos = true;
        if(negMaxH - negMinH >= posMaxH - posMinH){
            minH = posMinH;
            maxH = posMaxH;

        }
        else{
            minH = negMinH;
            maxH = negMaxH;
            pos = !pos;
        }
        String path = AssetDatabase.GetAssetPath(sourceImage);
        readwriteOnOff(path,false);
        result = $"min hue is {(minH)}\nmax hue is {(maxH)}\nminV is {minV}\nmaxV is {maxV}\nminS is {minS}\nmaxS is {maxS}\ninterval pos is {posMaxH - posMinH}\ninterval neg is {negMaxH - negMinH}";
        return pos;
    }
    static void readwriteOnOff(string path,bool enable = true){
        TextureImporter textureImporter = (TextureImporter) AssetImporter.GetAtPath( path );
        textureImporter.isReadable = enable;
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }
    static Color[] GenerateLookupTex(Color[] _pixels,Texture2D _sourceImage){
        Color[] result = MedianCut.cut(_pixels.ToList());
        Texture2D gradientMap = new Texture2D(256,8,TextureFormat.RGB24,true,false); //srgb lookuptex
        Color[] gradientMapPixel = new Color[2048]; 
        for(int i = 0; i < gradientMapPixel.Length;i++){
            int index = (int)i % 256;
            gradientMapPixel[i] = result[index];
        } //????????????
        gradientMap.SetPixels(gradientMapPixel);
        gradientMap.Apply();
        SaveTexturePNG(gradientMap,$"{_sourceImage.name}_lookup");
        return result;
    }
    void OnGUI()
    {   
        #region single
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("targetMip");
        mip = EditorGUILayout.IntSlider(mip, 0, 11);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("SourceTex");
        sourceImage = (Texture2D)EditorGUILayout.ObjectField(sourceImage, typeof(Texture2D), true);
        EditorGUILayout.EndHorizontal();
        // DebugMode = EditorGUILayout.Toggle("Debug Mode", DebugMode);
        if (GUILayout.Button("Generate!") && sourceImage != null){

            if(!sourceImage.isReadable){
                String path = AssetDatabase.GetAssetPath(sourceImage);
                readwriteOnOff(path);
            }
            pixels = sourceImage.GetPixels(mip);
            Color[] result = GenerateLookupTex(pixels,sourceImage);
            generateGreyMap(result,pixels,sourceImage);
        }
        // if(gradientMap!=null){
        //     EditorGUI.DrawPreviewTexture(new Rect(0, 300, 1024, 10), gradientMap);
        // }  
        #endregion
        #region path
        EditorGUILayout.LabelField("Path Dir.Please Put All the Texture2D in the root");
        pathDir = EditorGUILayout.TextField(pathDir);
        if (GUILayout.Button("Path Generate!") && pathDir != null){
            string[] guidsTexture2d = AssetDatabase.FindAssets("t:Texture2D", new String[1]{pathDir});
            if(guidsTexture2d.Length > 0){
                foreach (string guid in guidsTexture2d)
                {
                    Debug.Log(AssetDatabase.GUIDToAssetPath(guid));
                    Texture2D tempTex = (Texture2D)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Texture2D));
                    if(tempTex !=null){
                        if(!tempTex.isReadable){
                            String path = AssetDatabase.GetAssetPath(tempTex);
                            readwriteOnOff(path);
                        }
                        pixels = tempTex.GetPixels(mip);
                        Color[] result = GenerateLookupTex(pixels,tempTex);
                        generateGreyMap(result,pixels,tempTex);
                    }
                }
            }
        }
        #endregion
    

            
   
    }

}
