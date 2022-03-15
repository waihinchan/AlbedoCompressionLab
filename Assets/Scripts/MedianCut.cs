using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
// TODO 这里加一些成员变量去做比较好 很多参数需要传递的 不过这样搞的有点复杂 等有心人去优化吧
public class MedianCut 
{   
    struct freandcolor
    {
        public Color color;
        public int freq;
    }
    static Color[] GetRange(List<Color> _pixels){
        Color[] result = new Color[2];
        Color minComponent = new Color(1,1,1);
        Color maxComponent = new Color(0,0,0);
        foreach (var item in _pixels)
        {
            minComponent[0] = Mathf.Min(minComponent[0],item[0]);
            minComponent[1] = Mathf.Min(minComponent[1],item[1]);
            minComponent[2] = Mathf.Min(minComponent[2],item[2]);

            maxComponent[0] = Mathf.Min(maxComponent[0],item[0]);
            maxComponent[1] = Mathf.Min(maxComponent[1],item[1]);
            maxComponent[2] = Mathf.Min(maxComponent[2],item[2]);
        }
        result[0] = minComponent;
        result[1] = maxComponent;
        
        return result;
    }
    static int getMaxRangeComponent(Color[] _range){
        
        float RRange = _range[1][0] - _range[0][0];//left for MinMax, right for RGB
        float GRange = _range[1][1] - _range[0][1];
        float BRange = _range[1][2] - _range[0][2];
        if(RRange>=GRange && RRange>=BRange){
            return 0;
        }
        if(GRange>=RRange && GRange>=BRange){
            return 1;
        }
        if(BRange>=RRange && BRange>=GRange){
            return 2;
        }
        return 0;
    }
    static int SortByR(Color pixel1,Color pixel2){
        return pixel1[0].CompareTo(pixel2[0]);
    }
    static int SortByG(Color pixel1,Color pixel2){
        return pixel1[1].CompareTo(pixel2[1]);
    }
    static int SortByB(Color pixel1,Color pixel2){
        return pixel1[2].CompareTo(pixel2[2]);
    }
    static int nextPowerOf2(int n)
    {
        int count = 0;
 
        // First n in the below
        // condition is for the
        // case where n is 0
        if (n > 0 && (n & (n - 1)) == 0)
            return n;
 
        while(n != 0)
        {
            n >>= 1;
            count += 1;
        }
 
        return 1 << count;
    }
    static List<Color>[] splitBox(int _component,List<Color> _box){
        switch (_component)
        {
            case 0:
                _box.Sort(SortByR);
                
                break;
            case 1:
                _box.Sort(SortByG);
                
                break;
            case 2:
                _box.Sort(SortByB);
                
                break;
        }
        int halfLength = (int)Mathf.Floor(_box.Count / 2);
        List<Color>[] result = new List<Color>[2];
        result[0] =  _box.Take(halfLength).ToList();
        result[1] = _box.Skip(halfLength).ToList();
        
        return result;
    }
    public static Color[] meanColor(int n, List<List<Color>> _boxs){
        Color[] result = new Color[n];
        for(int i = 0; i < _boxs.Count;i++){
            Color currentBoxMeanColor = new Color(0,0,0);
            for(int j = 0; j <_boxs[i].Count;j++){
                currentBoxMeanColor += _boxs[i][j];
            }
            currentBoxMeanColor/= _boxs[i].Count; //加权平均
            result[i] = currentBoxMeanColor;
        }
        return result;
    }
    public static Color[] cut(List<Color> _box,int n = 256,bool usePow = true){
        List<List<Color>> allpixels = new List< List<Color> >(); //这个用于最终的加权
        allpixels.Add(_box);//初始
        int size = usePow?nextPowerOf2(n):n;
        while(allpixels.Count<size){
            List<Color> currentBox = allpixels[0];
            allpixels.RemoveAt(0); //操作那个就把那个弹出 然后把分裂的结果加在最后面，等待下一次的分裂
            int currentMaxRangeComponentResult = getMaxRangeComponent(GetRange(currentBox)); //先获取Range 然后获取差值最大的分量
            List<Color>[] currentSplitResult = splitBox(currentMaxRangeComponentResult,currentBox);//然后分割成为两个Box
            allpixels.Add(currentSplitResult[0]); //left
            allpixels.Add(currentSplitResult[1]); //right
        }
        return meanColor(size,allpixels);
    }
    public static Color getMeanColor(List<Color> _box){
        Color result = new Color(0,0,0);
        for(int i = 0; i < _box.Count;i++){
            result += _box[i];
        }
        result/=_box.Count;
        
        return result;
    }
    public static  List<Color>[] splitBoxByMeanColor(List<Color> _box, int _component){
        switch (_component)
        {
            case 0:
                _box.Sort(SortByR);
                
                break;
            case 1:
                _box.Sort(SortByG);
                
                break;
            case 2:
                _box.Sort(SortByB);
                
                break;
        }

        List<Color>[] result = new List<Color>[2];
        int recordIndex  = 0;
        Color currentMeanColor = getMeanColor(_box); //获得平均颜色
        float minDistance = Mathf.Abs( currentMeanColor[_component] - _box[recordIndex][_component] );
        // Debug.Log(Mathf.Abs( currentMeanColor[_component] - _box[0][_component] ));
        for(int i = 0; i < _box.Count;i++){
            
            float currentDistance = Mathf.Abs( currentMeanColor[_component] - _box[i][_component] );
            if(currentDistance <=   minDistance){
                recordIndex = i;
                minDistance = currentDistance;
            }
        }

        result[0] = _box.Take(recordIndex).ToList();
        result[1] = _box.Skip(recordIndex).ToList();

        return result;
    }
    
    public static Color[] cutByAverage(List<Color> _box,int n = 256,bool usePow = true){
        List<List<Color>> allpixels = new List< List<Color> >(); 
        List<List<Color>> backuppixels = new List< List<Color> >(); 
        allpixels.Add(_box);//初始
        int size = usePow?nextPowerOf2(n):n;
        Debug.Log(size);
        while(allpixels.Count + backuppixels.Count<size){
            Debug.Log(allpixels.Count);
            Debug.Log(backuppixels.Count);
            List<Color> currentBox = allpixels[0];    
            allpixels.RemoveAt(0); 
            
            if(currentBox.Count>2){
                int currentMaxRangeComponentResult = getMaxRangeComponent(GetRange(currentBox)); 
                List<Color>[] currentSplitResult = splitBoxByMeanColor(currentBox,currentMaxRangeComponentResult); 
                allpixels.Add(currentSplitResult[0]); //left
                allpixels.Add(currentSplitResult[1]); //right
            }
            else{
                if(currentBox.Count>0){
                    backuppixels.Add(currentBox);
                }
            }
        }
        allpixels.Concat(backuppixels).ToList<List<Color>>();    
        
        return meanColor(size,allpixels);
    }
    /// <summary>
    /// this is to regenerate the greymap from a given lookup tex.
    /// </summary>
    /// <param name="lookup"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public static Color[] FindCloestColor(Color[] lookup,Color[] source){
        Color[] greyImagePixels = new Color[source.Length];
        int count = 0;
        foreach (var pixel in source)
        {   
            int index = 0;
            int record = 0;
            Vector3 currentPixel = new Vector3(pixel[0],pixel[1],pixel[2]);
            float minDistance = Vector3.Distance(currentPixel,new Vector3(lookup[0][0],lookup[0][1],lookup[0][2]));
            foreach (var col in lookup)
            {
                float currentDistance = Vector3.Distance(currentPixel,new Vector3(col[0],col[1],col[2]));
                if(currentDistance < minDistance){
                    record = index;
                    minDistance = currentDistance;
                }
                index++;
            }
            float lookupIndex = record  /  (float)lookup.Length; 
            greyImagePixels[count] = new Color(lookup[record][0],lookup[record][1],lookup[record][2],lookupIndex); //写入以后再放到RT里面分离出来，方便检索
            count++;
        }
        return greyImagePixels;
    }
    /// <summary>
    /// we don't wrap this because we do this twice in one function to get the frequence at first time.
    /// TODO optimize the int[] stuff into a map
    /// </summary>
    /// <param name="lookup">the look up gradient map</param>
    /// <param name="source">raw image</param>
    /// <param name="StatisticsMap">for check the frequence of the look up pixels</param>
    /// <returns>new pixels</returns>
    public static Color[] FindCloestColor(Color[] lookup,Color[] source,out int[] StatisticsMap,out Color[] newlookup){
        #region first time pre calculate frequence and ready for sort
        Color[] greyImagePixels = new Color[source.Length];
        StatisticsMap = new int[lookup.Length];
        for(int i = 0; i<StatisticsMap.Length;i++){
            StatisticsMap[i] = 0;
        }
        int count = 0;
        foreach (var pixel in source)
        {   
            int index = 0;
            int record = 0;
            Vector3 currentPixel = new Vector3(pixel[0],pixel[1],pixel[2]);
            float minDistance = Vector3.Distance(currentPixel,new Vector3(lookup[0][0],lookup[0][1],lookup[0][2]));
            foreach (var col in lookup)
            {
                float currentDistance = Vector3.Distance(currentPixel,new Vector3(col[0],col[1],col[2]));
                if(currentDistance < minDistance){
                    record = index;
                    minDistance = currentDistance;
                }
                index++;
            }
            float lookupIndex = record  /  (float)lookup.Length; //这里两个int相除会变0 所以要转个float
            StatisticsMap[record] +=1;
            greyImagePixels[count] = new Color(lookup[record][0],lookup[record][1],lookup[record][2],lookupIndex); //写入以后再放到RT里面分离出来，方便检索
            count++;
        }
        #endregion

            #region check the frequence
            List<freandcolor> freandcolors = new List<freandcolor>();
            for(int i = 0; i<lookup.Length;i++){
                freandcolor fc = new freandcolor();
                fc.color = lookup[i];
                fc.freq = StatisticsMap[i];
                freandcolors.Add(fc);
            }
            freandcolors.Sort(delegate(freandcolor a, freandcolor b) { return -a.freq.CompareTo(b.freq); });
            newlookup = new Color[lookup.Length];
            for(int i = 0;i<freandcolors.Count;i++){
                StatisticsMap[i] = freandcolors[i].freq;
                newlookup[i] = freandcolors[i].color;
            }
            #endregion

            #region regenerategreymap
            greyImagePixels = null;
            greyImagePixels = FindCloestColor(newlookup,source);
            #endregion
        
        return greyImagePixels;
    }

}
