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
    static private bool midmode = true;
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
    #region notused
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
        #region 正向找范围
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
        #region 排序后找最大间隔
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
    #endregion
    static void generateStatisticsMap(Color[] _lookup,Color[] source,Texture2D _sourceImage,int[] StatisticsResult){
        Color[] StatisticsMapPixels  = new Color[_lookup.Length * _lookup.Length];
        for(int i = 0; i < StatisticsMapPixels.Length; i++){
            int index = i%_lookup.Length;
            if(i<_lookup.Length * (int)_lookup.Length/2){
                StatisticsMapPixels[i] = _lookup[index];
            }
            else{
                float p = 10 * StatisticsResult[index] / (float)source.Length;
                StatisticsMapPixels[i] = new Color(p,p,p);
            }
        }
        
        Texture2D StatisticsMapTex = new Texture2D(_lookup.Length,_lookup.Length,TextureFormat.RGB24,false,true);
        StatisticsMapTex.SetPixels(StatisticsMapPixels);
        StatisticsMapTex.Apply();
        string usmid = midmode ? "mid" : "avg" ;
        SaveTexturePNG(StatisticsMapTex,_lookup.Length,_lookup.Length,$"{_sourceImage.name}_Statistics_{usmid}");
    }
    static void generateGreyMap(Color[] _lookup,Color[] source,Texture2D _sourceImage){
        if(GreyMapShader==null){
            LoadShader();
        }
        Texture2D tempTex = new Texture2D(_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),TextureFormat.RGBA32,false,true);
        int[] StatisticsMap;
        Color[] newlookup;
        Color[] greyPixels = MedianCut.FindCloestColor(_lookup,source,out StatisticsMap,out newlookup);
        GenerateLookupTex(newlookup,_sourceImage);
        generateStatisticsMap(_lookup,source,_sourceImage,StatisticsMap);
        tempTex.SetPixels(greyPixels);
        tempTex.Apply();

        Material tempMat = new Material(GreyMapShader);
        RenderTexture checkMap = new RenderTexture(_sourceImage.width,_sourceImage.height,0,RenderTextureFormat.Default,RenderTextureReadWrite.Linear); //这里数据因为是已经计算好的 只是用于分离通道 所以直接线性写入不要修改
        tempMat.SetTexture("_SourceTex",tempTex);
        tempMat.SetInt("_Grey",0);
        Graphics.Blit(tempTex,checkMap,tempMat);
        tempMat.SetInt("_Grey",1);
        tempMat.SetTexture("_SourceTex",tempTex);
        //这里这个图的尺寸需要根据MIPmap来的
        RenderTexture greyMap = new RenderTexture(_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
        Graphics.Blit(tempTex,greyMap,tempMat);
        //保存两张图

        Texture2D reference = toTexture2D(checkMap,TextureFormat.RGB24);
        Texture2D grey = toTexture2D(greyMap,TextureFormat.RFloat);;
        string usmid = midmode ? "mid" : "avg" ;
        SaveTexturePNG(reference,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_reference_{usmid}");
        SaveTexturePNG(grey,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_grey_{usmid}");
        SaveTexturePNG(tempTex,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_raw_{usmid}");
        AssetDatabase.Refresh();
        //保存两张图
    }

    static void SaveTexturePNG(Texture2D sourceTex,int width,int height ,string name ="result")
    {
        byte[] bytes = ImageConversion.EncodeArrayToPNG(sourceTex.GetRawTextureData(), sourceTex.graphicsFormat, (uint)width, (uint)height);
        File.WriteAllBytes(Application.dataPath +  $"/{name}.png", bytes);
    }
    static void SaveTextureEXR(Texture2D sourceTex,string name ="result")
    {
        byte[] bytes = ImageConversion.EncodeArrayToEXR(sourceTex.GetRawTextureData(), sourceTex.graphicsFormat, (uint)sourceTex.width, (uint)sourceTex.height);
        File.WriteAllBytes(Application.dataPath +  $"/{name}.exr", bytes);
    }
    static Texture2D toTexture2D(RenderTexture RT,TextureFormat format = TextureFormat.RFloat) //保证灰度图的精度以便重映射
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
    static Color[] getLookupPixels(Color[] _pixels){
        Color[] result = midmode ? MedianCut.cut(_pixels.ToList()) : MedianCut.cutByAverage(_pixels.ToList());
        return result;
    }
    static void GenerateLookupTex(Color[] result,Texture2D _sourceImage){
        //TODO: 这里有一个256的要改成参数 看看是不是比如说用128或者512这样 有个插值 会更平滑一点。
        Texture2D gradientMap = new Texture2D(256,8,TextureFormat.RGB24,true,false); //srgb lookuptex
        Color[] gradientMapPixel = new Color[2048]; 
        for(int i = 0; i < gradientMapPixel.Length;i++){
            int index = (int)i % 256;
            gradientMapPixel[i] = result[index];
        } //按行填满
        gradientMap.SetPixels(gradientMapPixel); 
        gradientMap.Apply();
        string usmid = midmode ? "mid" : "avg" ;
        SaveTexturePNG(gradientMap,256,8,$"{_sourceImage.name}_lookup_{usmid}");
    }
    static void DoSingle(){
        if(!sourceImage.isReadable){
            String path = AssetDatabase.GetAssetPath(sourceImage);
            readwriteOnOff(path);
        }
        pixels = sourceImage.GetPixels(mip);
        Color[] result = getLookupPixels(pixels);
        generateGreyMap(result,pixels,sourceImage);
        // GenerateLookupTex(result,sourceImage);
    }
    static void DoALot(){
        // string[] guidsTexture2d = AssetDatabase.FindAssets("t:Texture2D", new String[1]{pathDir});
        // if(guidsTexture2d.Length > 0){
        //     foreach (string guid in guidsTexture2d)
        //     {
        //         // Debug.Log(AssetDatabase.GUIDToAssetPath(guid));
        //         Texture2D tempTex = (Texture2D)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Texture2D));
        //         if(tempTex !=null){
        //             if(!tempTex.isReadable){
        //                 String path = AssetDatabase.GetAssetPath(tempTex);
        //                 readwriteOnOff(path);
        //                 sourceImage = tempTex; //这里是为了保证mipmap的尺寸。生成图的时候mipmap尺寸是找source的，这里给个reference
        //             }
        //             pixels = tempTex.GetPixels(mip);
        //             Color[] result = GenerateLookupTex(pixels,tempTex);
        //             generateGreyMap(result,pixels,tempTex);
        //         }
        //     }
        // }
    }
    void OnGUI()
    {   
        #region single
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("targetMip");

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("SourceTex");
        sourceImage = (Texture2D)EditorGUILayout.ObjectField(sourceImage, typeof(Texture2D), true);
        EditorGUILayout.EndHorizontal();
        midmode = EditorGUILayout.Toggle("mid mode", midmode);
        
        if (GUILayout.Button("Generate!") && sourceImage != null){
            DoSingle();

        }
        #endregion
        #region path
        EditorGUILayout.LabelField("Path Dir.Please Put All the Texture2D in the root");
        pathDir = EditorGUILayout.TextField(pathDir);
        if (GUILayout.Button("Path Generate!") && pathDir != null){
            DoALot();
        }
        #endregion
    

            
   
    }

}
