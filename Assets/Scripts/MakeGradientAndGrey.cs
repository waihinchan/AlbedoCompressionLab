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
    #region members
    static private bool minReslookup = true;
    static private bool Autooptimize = false;
    static private float differenceThreshold = 0.05f;
    static private bool midmode = true;
    static private bool combineCut = true;
    static private Shader GreyMapShader;
    static private Shader utilsShader;
    
    static private Texture2D gradientMap;
    static private RenderTexture HistogramMap;
    static public Texture2D sourceImage;
    static public Texture2D rawImage;
    static public Texture2D greyImage;
    static public Texture2D lookupImage;
    static private List<Vector3> hsvPixels;
    static int mip;
    static string pathDir;
    #endregion
    [MenuItem("gradient/GetTexInfo")]
    static void showWindow(){
        var window = GetWindowWithRect<GradientMapGenerator>(new Rect(0, 0, 1024, 1024));
        LoadShader();
        window.Show();
    }
    static void LoadShader(){
        try
        {
            GreyMapShader = (Shader)AssetDatabase.LoadAssetAtPath("Assets/Shader/GradientMapGenerator.shadergraph", typeof(Shader));
            utilsShader = (Shader)AssetDatabase.LoadAssetAtPath("Assets/Shader/utils.shadergraph", typeof(Shader));
        }
        catch (System.Exception)
        {
            throw;
        }
    }
    #region savetextureutils
    static string SaveTexturePNG(Texture2D sourceTex,int width,int height ,string name ="result")
    {
        byte[] bytes = ImageConversion.EncodeArrayToPNG(sourceTex.GetRawTextureData(), sourceTex.graphicsFormat, (uint)width, (uint)height);
        string savePath = Application.dataPath +  $"/{name}.png";
        File.WriteAllBytes(savePath, bytes);
        AssetDatabase.Refresh();
        return "Assets" +  $"/{name}.png";
        // return AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Texture2D,name")[0]);
    }
    /// <summary>
    /// RT TO texture2d to PNG
    /// </summary>
    /// <param name="RT"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    static Texture2D toTexture2D(RenderTexture RT,TextureFormat format = TextureFormat.RFloat)
    {
        Texture2D tex = new Texture2D(RT.width, RT.height, format, false);
        RenderTexture.active = RT;
        tex.ReadPixels(new Rect(0, 0, RT.width, RT.height), 0, 0);
        tex.Apply();
        return tex;
    }
    static void SaveAllResults(Dictionary<string,Texture2D> results,int rawWidth,int rawHeight,string name,string mode,string extraInfo){
        if(results.ContainsKey("reference")){
            string path = SaveTexturePNG(results["reference"],rawWidth/(int)Mathf.Pow(2,mip),rawHeight/(int)Mathf.Pow(2,mip),$"{name}_reference_{mode}_{extraInfo}");
        }
        if(results.ContainsKey("grey")){
            string path = SaveTexturePNG(results["grey"],rawWidth/(int)Mathf.Pow(2,mip),rawHeight/(int)Mathf.Pow(2,mip),$"{name}_grey_{mode}_{extraInfo}");
            reImportTexture(path,false,false,false,FilterMode.Point,TextureWrapMode.Repeat, TextureImporterCompression.Uncompressed);
        }
        if(results.ContainsKey("sortLookup")){
            string path = SaveTexturePNG(results["sortLookup"],results["sortLookup"].width,results["sortLookup"].height,$"{name}_Lookup_{mode}_{extraInfo}");
            reImportTexture(path,false,false,true,FilterMode.Point,TextureWrapMode.Mirror, TextureImporterCompression.Uncompressed);

        }
        if(results.ContainsKey("Statistics")){
            string path = SaveTexturePNG(results["Statistics"],results["Statistics"].width,results["Statistics"].height,$"{name}_Statistics_{mode}_{extraInfo}");
        }
        if(results.ContainsKey("optimizeLookup")){
            string path = SaveTexturePNG(results["optimizeLookup"],results["optimizeLookup"].width,results["optimizeLookup"].height,$"{name}_optimizeLookup_{mode}_{extraInfo}");
        }
         
    }
    static void reImportTexture(string path,bool enableReadWrite = true,bool enableMipMap = false,bool enableSrgb = false,FilterMode flitermode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Repeat,TextureImporterCompression texturecompression = TextureImporterCompression.Uncompressed){
        TextureImporter textureImporter = (TextureImporter) AssetImporter.GetAtPath( path );
        textureImporter.isReadable = enableReadWrite;
        textureImporter.mipmapEnabled = enableMipMap; //turn off mipmap to make sure the same stuff. 
        textureImporter.sRGBTexture = enableSrgb;
        textureImporter.textureCompression = texturecompression;
        textureImporter.filterMode = flitermode;
        textureImporter.wrapMode = wrapMode;
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }

    #endregion
    static Texture2D generateStatisticsMap(Color[] _lookup,Color[] source,Texture2D _sourceImage,int[] StatisticsResult){
        Color[] StatisticsMapPixels  = new Color[_lookup.Length * _lookup.Length];
        for(int i = 0; i < StatisticsMapPixels.Length; i++){
            int index = i%_lookup.Length;
            if(i<_lookup.Length * (int)_lookup.Length/2){
                StatisticsMapPixels[i] = _lookup[index];
            }
            else{
                float p = StatisticsResult[index]>10?(100 * StatisticsResult[index] / (float)source.Length):0;
                StatisticsMapPixels[i] = new Color(p,p,p);
            }
        }
        Texture2D StatisticsMapTex = new Texture2D(_lookup.Length,_lookup.Length,TextureFormat.RGB24,false,true);
        StatisticsMapTex.SetPixels(StatisticsMapPixels);
        StatisticsMapTex.Apply();
        return StatisticsMapTex;
    }

    
    static Dictionary<string,Texture2D> generateGreyMap(Color[] _lookup,Color[] source,Texture2D _sourceImage){
        if(GreyMapShader==null){
            LoadShader();
        }
        Texture2D referenceTex = new Texture2D(_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),TextureFormat.RGBA32,false);
        int[] StatisticsMap; //frequencey result
        Color[] sortLookup; //new sortLookupMap
        Color[] referencePixels = MedianCut.FindCloestColor(_lookup,source,out StatisticsMap,out sortLookup);
        referenceTex.SetPixels(referencePixels);
        referenceTex.Apply();
        Texture2D lookupTex = GenerateLookupTex(sortLookup,_sourceImage);//can do the save stuff this outsite
        Texture2D StatisticsTex = generateStatisticsMap(sortLookup,source,_sourceImage,StatisticsMap);
        Color[] greyPixels = new Color[referencePixels.Length]; //RGBA -> A
        for(int i = 0; i < referencePixels.Length;i++){
            float a = referencePixels[i].a;
            greyPixels[i] = new Color(a,a,a);
        }
        Texture2D grey = new Texture2D(_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),TextureFormat.RFloat,false,true);
        grey.SetPixels(greyPixels);
        grey.Apply();
        #region use Shader to split the channel
        // Material tempMat = new Material(GreyMapShader);
        // RenderTexture checkMap = new RenderTexture(_sourceImage.width,_sourceImage.height,0,RenderTextureFormat.Default,RenderTextureReadWrite.Linear); //这里数据因为是已经计算好的 只是用于分离通道 所以直接线性写入不要修改
        // tempMat.SetTexture("_SourceTex",referenceTex);
        // tempMat.SetInt("_Grey",0);
        // Graphics.Blit(referenceTex,checkMap,tempMat);
        // tempMat.SetInt("_Grey",1);
        // tempMat.SetTexture("_SourceTex",referenceTex);
        // //这里这个图的尺寸需要根据MIPmap来的
        // RenderTexture greyMap = new RenderTexture(_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);
        // Graphics.Blit(referenceTex,greyMap,tempMat);
        // //保存两张图
        // Texture2D reference = toTexture2D(checkMap,TextureFormat.RGB24);
        // Texture2D grey = toTexture2D(greyMap,TextureFormat.RFloat);
        #endregion
        // SaveTexturePNG(reference,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_reference_{usmid}");
        // SaveTexturePNG(grey,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_grey_{usmid}");
        // SaveTexturePNG(referenceTex,_sourceImage.width/(int)Mathf.Pow(2,mip),_sourceImage.height/(int)Mathf.Pow(2,mip),$"{_sourceImage.name}_raw_{usmid}");
        // AssetDatabase.Refresh();
        //保存两张图
        Dictionary<string,Texture2D> results = new Dictionary<string, Texture2D>();

        results["grey"] = grey;
        results["reference"] = referenceTex;
        results["sortLookup"] = lookupTex;
        results["Statistics"] = StatisticsTex;
    
        return results;
    }
    static Color[] getLookupPixels(Color[] _pixels){
        Color[] result = midmode ? MedianCut.cut(_pixels.ToList()) : MedianCut.cutByAverage(_pixels.ToList());
        return result;
    }
    static Texture2D GenerateLookupTex(Color[] result,Texture2D _sourceImage,bool minRes=true){
        //TODO: 这里有一个256的要改成参数 看看是不是比如说用128或者512这样 有个插值 会更平滑一点。
        int Height = minRes?1:8;
        Texture2D gradientMap = new Texture2D(256,Height,TextureFormat.RGB24,true,false); //temp we want 256 res
        Color[] gradientMapPixel = new Color[ 256 * Height]; 
        for(int i = 0; i < gradientMapPixel.Length;i++){
            int index = (int)i % 256;
            gradientMapPixel[i] = result[index];
        } //按行填满
        gradientMap.SetPixels(gradientMapPixel); 
        gradientMap.Apply();
        return gradientMap;
    }
    
    static Color[][] Optimize(Texture2D greymap,Texture2D lookupmap,Texture2D rawMap, int[] frequenceMap){
        #region getdifference Index
        Material tempMat = new Material(utilsShader);
        tempMat.SetTexture("_grey",greymap);
        tempMat.SetTexture("_raw",rawMap);
        tempMat.SetTexture("_lookup",lookupmap);
        tempMat.SetFloat("_difference",differenceThreshold);
        RenderTexture differenceMap = new RenderTexture(rawMap.width,rawMap.height,0,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear); 
        Graphics.Blit(rawMap,differenceMap,tempMat);
        Texture2D difference = toTexture2D(differenceMap,TextureFormat.R8);
        #endregion

        Dictionary<int,Color> checkDict = new Dictionary<int,Color>();

        Color[] rawdifferencePixels = difference.GetPixels();
        Color[] rawPixels = rawMap.GetPixels();
        List<Color> checkdifferencePixels = new List<Color>();
        for(int i = 0; i < rawdifferencePixels.Length;i++){
            if(rawdifferencePixels[i].r>0){
                checkDict[i] = rawPixels[i]; //index -> (orginal color for re cut again)
                checkdifferencePixels.Add(rawPixels[i]); //we want to raw one but not the difference one
            }
        }
        int count = 0; //get how many not used lookup we can use
        //这里虽然排序过了。。但是还是这么写保险点 如果后面有不排序的也可以用
        List<int> recordIndex = new List<int>();
        for(int i = 0;i < frequenceMap.Length;i++)
        {
            if(frequenceMap[i] <= 10){ //this value can be change
                count++;
                recordIndex.Add(i);
            }
        }
        
        Color[] newLookup = MedianCut.cutByAverage(checkdifferencePixels,count,false); //用平均值的方法去关注异常值补全
        #region newLookup to oldLookup 0 index
        Color[] oldLookup = lookupmap.GetPixels();
        int pick = newLookup.Length-1; 
        foreach (int record in recordIndex)
        {
            oldLookup[record] = newLookup[pick];
            pick--;
        }
        #endregion
        Color[][] result = new Color[2][]; 
        result[0] = newLookup;
        result[1] = oldLookup;
        return result;
    }
    static void DoOptimize(Texture2D _rawImage,Texture2D _greyImage,Texture2D _lookupImage){
        if(utilsShader==null||GreyMapShader){
            LoadShader();
        }
        Color[] rawLookup = _lookupImage.GetPixels();
        Color[] rawSource = _rawImage.GetPixels();
        int[] smap;
        Color[] sortLookup;
        
        MedianCut.FindCloestColor(rawLookup,rawSource,out smap,out sortLookup); //before
        //this result should be match the 1st time cut
        //we just want the smap
        Color[][] result = Optimize(_greyImage,_lookupImage,_rawImage,smap); //0 is the optimize ,1 is combine the old 

        Dictionary<string,Texture2D> newResults = generateGreyMap(result[1],rawSource,_rawImage);
        int height = minReslookup?1:8;
        Texture2D optimizeLookupTex = new Texture2D(result[0].Length,height,TextureFormat.RGB24,false);
        optimizeLookupTex.SetPixels(result[0]);
        optimizeLookupTex.Apply();
        newResults["optimizeLookup"] = optimizeLookupTex;
        string mode = midmode?"mid":"avg";
        SaveAllResults(newResults,_rawImage.width,_rawImage.height,_rawImage.name,mode,"optimize");
    }
    static void DoSingle(){
        if(!sourceImage.isReadable){
            String path = AssetDatabase.GetAssetPath(sourceImage);
            reImportTexture(path,true,false,true);
        }
        Color[] pixels = sourceImage.GetPixels(mip);
        if(combineCut){
            midmode = true;
            Color[] resultmid = getLookupPixels(pixels);
            Dictionary<string,Texture2D> allResults =  generateGreyMap(resultmid,pixels,sourceImage);
            SaveAllResults(allResults,sourceImage.width,sourceImage.height,sourceImage.name,"mid","Normal");
            if(Autooptimize){
                DoOptimize(sourceImage,allResults["grey"],allResults["sortLookup"]);
            }
            midmode = false;
            Color[] resultavg = getLookupPixels(pixels);
            allResults = generateGreyMap(resultavg,pixels,sourceImage);
            SaveAllResults(allResults,sourceImage.width,sourceImage.height,sourceImage.name,"avg","Normal");
            if(Autooptimize){
                DoOptimize(sourceImage,allResults["grey"],allResults["sortLookup"]);
            }
        }
        else{
            Color[] result = getLookupPixels(pixels);
            Dictionary<string,Texture2D> allResults = generateGreyMap(result,pixels,sourceImage);
            string mode = midmode?"mid":"avg";
            SaveAllResults(allResults,sourceImage.width,sourceImage.height,sourceImage.name,mode,"");
            if(Autooptimize){
                DoOptimize(sourceImage,allResults["grey"],allResults["sortLookup"]);
            }
        }
        
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
        //                 reImportTexture(path);
        //                 sourceImage = tempTex; //这里是为了保证mipmap的尺寸。生成图的时候mipmap尺寸是找source的，这里给个reference
        //             }
        //             pixels = tempTex.GetPixels(mip);
        //             Color[] result = GenerateLookupTex(pixels,tempTex);
        //             generateGreyMap(result,pixels,tempTex);
        //         }
        //     }
        // }
    }
    static void CPUDecodeTest(Color[] greyPixels,Color[] LookupPixels,int width,int height){
        Color[] result = new Color[greyPixels.Length];
        Texture2D resultTex = new Texture2D(width,height,TextureFormat.RGB24,false);
        for(int i = 0;i<result.Length;i++){
            int lookupindex = Mathf.FloorToInt(greyPixels[i].r * 256); 
            result[i] = LookupPixels[lookupindex];
        }
        resultTex.SetPixels(result);
        resultTex.Apply();
        SaveTexturePNG(resultTex,width,height,"cpudecode");
    }
    void OnGUI()
    {   
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("targetMip");
        EditorGUILayout.IntSlider(mip, 0, 8);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Advanced");
        Autooptimize = EditorGUILayout.Toggle("AutoOptimize", Autooptimize);
        differenceThreshold = EditorGUILayout.FloatField("difference threshold", differenceThreshold);
        midmode = EditorGUILayout.Toggle("mid mode", midmode);
        combineCut = EditorGUILayout.Toggle("cut by both", combineCut);
        EditorGUILayout.EndHorizontal();
        #region single
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("SourceTex");
        sourceImage = (Texture2D)EditorGUILayout.ObjectField(sourceImage, typeof(Texture2D), true);
        EditorGUILayout.EndHorizontal();
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
        #region optimize
        // EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("grey");
        greyImage = (Texture2D)EditorGUILayout.ObjectField(greyImage, typeof(Texture2D), true);
        EditorGUILayout.LabelField("raw");
        rawImage = (Texture2D)EditorGUILayout.ObjectField(rawImage, typeof(Texture2D), true);
        EditorGUILayout.LabelField("lookup");
        lookupImage = (Texture2D)EditorGUILayout.ObjectField(lookupImage, typeof(Texture2D), true);
        // EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Optimize!") && greyImage != null &&rawImage != null &&lookupImage != null){
            if(!rawImage.isReadable){
                String path = AssetDatabase.GetAssetPath(rawImage);
                reImportTexture(path,true,false,true);
            }
            if(!greyImage.isReadable){
                String path1 = AssetDatabase.GetAssetPath(greyImage);
                reImportTexture(path1,true,false,false);
            }
            if(!lookupImage.isReadable){
                String path2 = AssetDatabase.GetAssetPath(lookupImage);
                reImportTexture(path2,true,false,true,FilterMode.Point,TextureWrapMode.Mirror);
            }
            Color[] greyPixels = greyImage.GetPixels();
            Color[] LookupPixels = lookupImage.GetPixels();
            // CPUDecodeTest(greyPixels,LookupPixels,greyImage.width,greyImage.height);
            DoOptimize(rawImage,greyImage,lookupImage);
        }
        #endregion

            
   
    }


}
