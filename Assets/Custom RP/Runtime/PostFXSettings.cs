using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;

    [System.NonSerialized] private Material material;

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16f)] public int maxIterations;

        [Min(1f)] public int downscaleLimit;
        
        public bool bicubicUpsampling;       
    }

    [SerializeField] private BloomSettings bloom = default;

    public BloomSettings Bloom => bloom;
    
    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }

}