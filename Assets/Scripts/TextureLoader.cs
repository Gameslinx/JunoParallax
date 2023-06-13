using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using UnityEngine;

public static class TextureLoader
{
    public static string assetPath;
    public static void Initialize(string path)
    {
        assetPath = path;
    }
    public static Texture2D LoadTexture(string filePath)
    {
        string path = assetPath + "/" + filePath;
        Debug.Log("Loading texture from disk at " + path);
        if (filePath.EndsWith(".png"))
        {
            return LoadPNG(path);
        }
        else if (filePath.EndsWith(".dds"))
        {
            return LoadDDS(path);
        }
        else
        {
            Debug.Log("[Exception] Image extension is not supported. Must be PNG or DDS (DXT1 / DXT5)");
            return Texture2D.whiteTexture;
        }
    }
    public static Texture2D LoadPNG(string filePath)
    {
        Texture2D tex;
        tex = new Texture2D(2, 2);
        tex.LoadRawTextureData(File.ReadAllBytes(filePath));
        tex.Apply(true, false);
        Debug.Log("Warning: Loading PNG texture - This uses sRGB mode and normal maps may not work properly when loaded this way!");
        return tex;
    }
    public static Texture2D LoadDDS(string filePath)
    {
        byte[] data = File.ReadAllBytes(filePath);
        byte ddsSizeCheck = data[4];
        if (ddsSizeCheck != 124)
        {
            Debug.Log("This DDS texture is invalid - Unable to read the size check value from the header.");
        }

        int height = data[13] * 256 + data[12];
        int width = data[17] * 256 + data[16];

        int DDS_HEADER_SIZE = 128;
        byte[] dxtBytes = new byte[data.Length - DDS_HEADER_SIZE];
        Buffer.BlockCopy(data, DDS_HEADER_SIZE, dxtBytes, 0, data.Length - DDS_HEADER_SIZE);
        int mipMapCount = (data[28]) | (data[29] << 8) | (data[30] << 16) | (data[31] << 24);

        TextureFormat format = TextureFormat.DXT1;
        if (data[84] == 'D')
        {
            if (data[87] == 49) //Also char '1'
            {
                format = TextureFormat.DXT1;
            }
            else if (data[87] == 53)    //Also char '5'
            {
                format = TextureFormat.DXT5;
            }
            else
            {
                Debug.Log("Texture is not a DXT 1 or DXT5");
            }
        }
        Texture2D texture;
        if (mipMapCount == 1)
        {
            texture = new Texture2D(width, height, format, false, true);
        }
        else
        {
            texture = new Texture2D(width, height, format, true, true);
        }
        try
        {
            texture.LoadRawTextureData(dxtBytes);
        }
        catch
        {
            Debug.Log("[Exception] Parallax has halted the texture loading process because texture.LoadRawTextureData(dxtBytes) would have resulted in overread");
            Debug.Log("Please check the format for this texture");
        }
        texture.Apply(true, false);  //Recalculate mips, mark as no longer readable (to save memory)

        return (texture);
    }
    
}
