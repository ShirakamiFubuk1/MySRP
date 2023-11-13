using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor");
    private static int cutOffId = Shader.PropertyToID("_CutOff");

    [SerializeField] private Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)] private float cutOff = 0.5f;

    private static MaterialPropertyBlock block;

    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId,baseColor);
        block.SetFloat(cutOffId,cutOff);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }
}