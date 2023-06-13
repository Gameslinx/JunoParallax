using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthTexture : MonoBehaviour
{
    // Start is called before the first frame update
    public Material material;
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, material);
    }
}
