using UnityEngine;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour {

    static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] private Mesh mesh = default;

    [SerializeField] private Material material = default;

    [SerializeField] private LightProbeProxyVolume lightProbeVolume = null;
	
    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];
    float[]
        metallic = new float[1023],
        smoothness = new float[1023];

    MaterialPropertyBlock block;

    void Awake () {
        for (int i = 0; i < matrices.Length; i++) {
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f,
                Quaternion.Euler(
                    Random.value * 360f, Random.value * 360f, Random.value * 360f
                ),
                Vector3.one * Random.Range(0.5f, 1.5f)
            );
            baseColors[i] =
                new Vector4(
                    Random.value, Random.value, Random.value,
                    Random.Range(0.5f, 1f)
                );
            metallic[i] = Random.value < 0.25f ? 1f : 0f;
            smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    void Update () {
        if (block == null) {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);
            
            if (!lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    //配置实例块时需要访问实力位置。通过获得矩阵的第三行
                    positions[i] = matrices[i].GetColumn(3);
                }

                //光照探针必须通过矩阵提供。它通过使用位置和光探针数组作为参数来调用。
                //第三个用于遮挡的参数，故我们用null
                //因为此参数只用一次，不需要使用list，因为会产生一条新的变体
                var lightProbes = new SphericalHarmonicsL2[1023];
                var occlusionProbes = new Vector4[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions,lightProbes,occlusionProbes
                );
                block.CopySHCoefficientArraysFrom(lightProbes);
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }
        }
        //1 网格mesh, 2 子网格submesh, 3 材质, 4 transform矩阵, 5数量 ,6MaterialProperties
        //7 开启阴影投射模式, 8 是否接收阴影, 9 图层,默认0, 10 camera, 11 设置光探针模式
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block,
            ShadowCastingMode.On,true,0,null,
            lightProbeVolume ? 
                LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
            lightProbeVolume);
    }
}