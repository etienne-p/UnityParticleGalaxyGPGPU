using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(Texture3D))]
public class VelocityField : MonoBehaviour 
{
    [System.Serializable]
    public struct NoiseParms
    {
        public float scale;
        [SerializeField, Range(0, 1)]
        public float mul;
        public Vector3 offset;
    }

	[SerializeField] int size;
    [SerializeField] NoiseParms[] layers;
	[SerializeField] bool autoApply;

	Texture3D field;

	void OnEnable()
	{
		if (autoApply)
		{
			Apply ();
		}
	}

	void OnValidate()
	{
        if (autoApply)
		{
			Apply ();
		}
	}

#if UNITY_EDITOR
    [ContextMenu("Save Asset")]
    void SaveAsset()
    {
        if (field == null)
        {
            Debug.LogError("Velocity Field not generated yet, use Apply() before saving the asset.");
            return;
        }
        var path = EditorUtility.SaveFilePanelInProject("Save", "velocity_field", "asset", "");
        AssetDatabase.CreateAsset(field, path);
    }
#endif

    [ContextMenu("Apply")]
	void Apply()
	{
		if (size < 2)
		{
			Debug.LogError ("size should at least be 2");
			return;
		}

		if (field == null || field.width != size)
		{
			if (field != null)
			{
				Texture3D.DestroyImmediate (field);
			}
			field = new Texture3D (size, size, size, TextureFormat.ARGB32, false);
		}

        // TODO should compute accMul beforehand?
        var tensor = new Vector3[size * size * size];
        float accMul = 0;
        if (layers != null)
        {
            foreach (var l in layers)
            {
                if (l.mul > 0)
                {
                    accMul += l.mul;
                    NoiseLayer(tensor, size, l.scale, l.offset, l.mul);
                }
            }
        }

        var texels = new Color[tensor.Length];

        if (accMul > 0)
        {
            for (int i = 0; i != tensor.Length; ++i)
            {
                var tmp = ((tensor[i] / accMul) + Vector3.one) / 2.0f;
                texels[i] = new Color(tmp.x, tmp.y, tmp.z);
            }
        }
        else
        {
            for (int i = 0; i != tensor.Length; ++i)
            {
                // We encode normalized values so 0 is gray
                texels[i] = Color.white / 2.0f;
            }
        }
        
		field.SetPixels (texels);
		field.Apply ();
		GetComponent<ParticleSystemGPGPU> ().velocityField = field;
	}

    static void NoiseLayer(Vector3[] texels, int size, float scale, Vector3 offset, float mul)
    {
        Assert.IsTrue(texels.Length == size * size * size);

        // noise scalar field
        var scalarField = new float[(size + 2) * (size + 2) * (size + 2)];
        float h = 1.0f / (float)(size - 1);
        for (int z = 0; z != size + 2; ++z)
        {
            for (int y = 0; y != size + 2; ++y)
            {
                for (int x = 0; x != size + 2; ++x)
                {
                    Vector3 p = new Vector3(x, y, z) * (h * scale) + offset;
                    scalarField[z * (size + 2) * (size + 2) + y * (size + 2) + x] = Perlin.Noise(p.x, p.y, p.z);
                }
            }
        }

        // gradient
        float maxSqrLen = 0;
        var grad = new Vector3[size * size * size];
        for (int z = 0; z != size; ++z)
        {
            for (int y = 0; y != size; ++y)
            {
                for (int x = 0; x != size; ++x)
                {
                    var dx = 
                        scalarField[(z + 1) * (size + 2) * (size + 2) + (y + 1) * (size + 2) + x + 2] -
                        scalarField[(z + 1) * (size + 2) * (size + 2) + (y + 1) * (size + 2) + x + 0];
                    var dy = 
                        scalarField[(z + 1) * (size + 2) * (size + 2) + (y + 2) * (size + 2) + x + 1] -
                        scalarField[(z + 1) * (size + 2) * (size + 2) + (y + 0) * (size + 2) + x + 1];
                    var dz = 
                        scalarField[(z + 2) * (size + 2) * (size + 2) + (y + 1) * (size + 2) + x + 1] -
                        scalarField[(z + 0) * (size + 2) * (size + 2) + (y + 1) * (size + 2) + x + 1];
                    var g = new Vector3(dx, dy, dz);
                    maxSqrLen = Mathf.Max(maxSqrLen, g.sqrMagnitude);
                    grad[z * size * size + y * size + x] = g;
                }
            }
        }

        float normFactor = mul /  Mathf.Sqrt(maxSqrLen);
        for (int i = 0; i != texels.Length; ++i)
        {
            texels[i] += grad[i] * normFactor;
        }
    }
}
