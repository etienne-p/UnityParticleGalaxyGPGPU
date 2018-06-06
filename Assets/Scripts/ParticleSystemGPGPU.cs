using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public class ParticleSystemGPGPU : MonoBehaviour
{
    struct Particle
    {
		public Vector3 position;
        public Vector3 velocity;
		public Vector3 axis;
		public Vector3 random;
        public Color color;
		public float age;
    }

	[SerializeField] int numParticles;
    [SerializeField] ComputeShader updateShader;
    [SerializeField] Shader renderShader;
  
	[SerializeField, Range(0.1f, 2)] float velocitySmoothing = 1;
    [SerializeField, Range(0.1f, 2)] float colorSmoothing = 1;
    [SerializeField, Range(0, 1f)] float speed;
    [SerializeField, Range(0, 20)] float lifetime;
	[SerializeField, Range(0, 2)] float fieldPower;
	[SerializeField, Range(0, 1)] float orbitMotion;
    [SerializeField, Range(0, 1)] float fieldMotion;
    [SerializeField, Range(0, 0.5f)] float spawnRadiusMin;
    [SerializeField, Range(0, 0.5f)] float spawnRadiusMax;
    [SerializeField] Color colorMin;
    [SerializeField] Color colorMax;
    [SerializeField, Range(0, 12)] float velocityToColor;
    [SerializeField, Range(0, 0.2f)] float sizeMin;
    [SerializeField, Range(0, 0.2f)] float sizeMax;
    [SerializeField, Range(0, 1)] float sizeContrast;
    [SerializeField, Range(0, 1)] float sizeDistribution;
	[SerializeField, Range(0, 1)] float smoothWithSize;
	[SerializeField, Range(0, 6)] float sizeToSmoothExpMul;
	[SerializeField, Range(0, 1)] float sizeSmoothOffset;

    [SerializeField] Texture particleTex;
    [SerializeField, Range(0, 1)] float axisSpread;

    [SerializeField] public Texture3D velocityField;
    [SerializeField] bool paused;

    Material renderMaterial;
    ComputeBuffer particlesBuffer;
    int kernelIndex;

    #region Monobehaviour

    void OnEnable()
    {
        CheckResources();
    }

    void OnValidate()
    {
        CheckResources();
    }

    void OnDisable()
    {
        ReleaseResources();
    }

    void Update()
    {
        if (!paused)
        {
            UpdateParticles();
        }
    }

    void OnRenderObject()
    {
        if (enabled && gameObject.activeInHierarchy)
        {
            RenderParticles();
        }
    }

    #endregion

    #region Resources Management

    void ReleaseResources()
    {
        if (particlesBuffer != null)
        {
            particlesBuffer.Dispose();
        }
    }

    void CheckResources()
    {
        // hardcoded kernel name
        kernelIndex = updateShader == null ? -1 : updateShader.FindKernel("CSMain");
        CheckMaterial();
        CheckParticlesBuffer();
    }

    void CheckMaterial()
    {
        if (renderShader != null && (renderMaterial == null || renderMaterial.shader != renderShader))
        {
            renderMaterial = new Material(renderShader);
            renderMaterial.hideFlags = HideFlags.DontSave;
        }
    }

    bool CheckParticlesBuffer()
    {
        if (particlesBuffer == null || particlesBuffer.count != numParticles)
        {
            if (particlesBuffer != null)
            {
                particlesBuffer.Dispose();
            }
            var stride = Marshal.SizeOf(typeof(Particle));
            particlesBuffer = new ComputeBuffer(numParticles, stride);
            particlesBuffer.SetData(InitializeParticles(numParticles));
            return true;
        }
        return false;
    }

    #endregion

    #region System Lifecycle

    Particle[] InitializeParticles(int numParticles)
    {
		Random.InitState (0);
        var particles = new Particle[numParticles];
        for (int i = 0; i != numParticles; ++i)
        {
			particles[i].velocity = Vector3.zero;
			particles [i].position = Random.onUnitSphere * 0.5f;
            particles[i].axis = Random.onUnitSphere;
            particles[i].random = new Vector3(Random.value, Random.value, Random.value);
            particles[i].color = Color.Lerp(colorMin, colorMax, Random.value);
			particles[i].age = 1; // force respawn
        }
        return particles;
    }

    void UpdateParticles()
    {
        if (kernelIndex == -1)
        {
            return;
        }
			
		updateShader.SetBuffer (kernelIndex, "particles", particlesBuffer);
		updateShader.SetTexture(kernelIndex, "velocity_field", velocityField);
		updateShader.SetFloat("velocity_smoothing", velocitySmoothing);
        updateShader.SetFloat("color_smoothing", colorSmoothing);
        updateShader.SetFloat("lifetime", lifetime);
        updateShader.SetFloat("speed", speed);
        updateShader.SetFloat("dt", Time.deltaTime);
		updateShader.SetFloat("field_power", fieldPower);
		updateShader.SetFloat("orbit_motion", orbitMotion);
        updateShader.SetFloat("field_motion", fieldMotion);
        updateShader.SetFloat("spawn_radius_min", spawnRadiusMin);
        updateShader.SetFloat("spawn_radius_max", spawnRadiusMax);
        updateShader.SetVector("color_min", (Vector4)colorMin);
        updateShader.SetVector("color_max", (Vector4)colorMax);
        updateShader.SetFloat("velocity_to_color", velocityToColor);
        updateShader.SetFloat("axis_spread", axisSpread);
        updateShader.SetFloat("time", Time.time);

        updateShader.Dispatch(kernelIndex, particlesBuffer.count / 32, 1, 1);
    }

    void RenderParticles()
    {
        // Set renderMaterial uniforms
        renderMaterial.SetTexture("_MainTex", particleTex);
        renderMaterial.SetFloat("_SizeMin", sizeMin);
        renderMaterial.SetFloat("_SizeMax", sizeMax);
        renderMaterial.SetFloat("_SizeContrast", sizeContrast);
        renderMaterial.SetFloat("_SizeDistribution", sizeDistribution);
		renderMaterial.SetFloat("_SmoothWithSize", smoothWithSize);
		renderMaterial.SetFloat("_SizeSmoothOffset", sizeSmoothOffset);
		renderMaterial.SetFloat("_SizeToSmoothExpMul", sizeToSmoothExpMul);
        renderMaterial.SetMatrix("_LocalToWorldMatrix", transform.localToWorldMatrix);
		renderMaterial.SetBuffer ("particles", particlesBuffer);
        renderMaterial.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, particlesBuffer.count, 1);
    }

    #endregion
}
