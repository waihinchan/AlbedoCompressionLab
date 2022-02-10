using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MedianCut 
{
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
        if(RRange>GRange && RRange>BRange){
            return 0;
        }
        if(GRange>=RRange && GRange>=BRange){
            return 1;
        }
        if(BRange>RRange && BRange>GRange){
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
        // IEnumerable<Color> _left = _box.Take(halfLength);
        // IEnumerable<Color> _right = _box.Skip(halfLength);
        // List<Color> left = new List<Color>();
        // List<Color> right = new List<Color>();
        // foreach (var item in _left)
        // {
        //     left.Add(item);
        // }
        // foreach (var item in _right)
        // {
        //     right.Add(item);
        // }
        result[0] =  _box.Take(halfLength).ToList();
        result[1] = _box.Skip(halfLength).ToList();
        
        return result;
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
    static Color[] meanColor(int n, List<List<Color>> _boxs){
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
    public static Color[] cut(List<Color> _box,int n = 256){
        List<List<Color>> allpixels = new List< List<Color> >(); //这个用于最终的加权
        allpixels.Add(_box);//初始
        int size = nextPowerOf2(n);
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
            float lookupIndex = record/(float)lookup.Length; //这里两个int相除会变0 所以要转个float
            greyImagePixels[count] = new Color(lookup[record][0],lookup[record][1],lookup[record][2],lookupIndex); //写入以后再放到RT里面分离出来，方便检索
            count++;
        }
        return greyImagePixels;
    }
}
