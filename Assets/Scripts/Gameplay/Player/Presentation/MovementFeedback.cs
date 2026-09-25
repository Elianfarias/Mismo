using Mismo.Gameplay.Player.Dash;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Feedback funcional provisional para que cada acción de movimiento sea legible al jugar.</summary>
    [DisallowMultipleComponent]
    public sealed class MovementFeedback : MonoBehaviour
    {
        [Header("Provisional feedback")]
        [SerializeField] private bool animateVisualScale = true;
        public void UseAuthoredAnimations() => animateVisualScale = false;
        [SerializeField] private Color sprintColor = new Color(0.65f, 0.9f, 1f, 0.65f);
        [SerializeField] private Color dashColor = new Color(1f, 0.62f, 0.15f, 0.9f);
        [SerializeField] private Color landingColor = new Color(0.8f, 0.9f, 1f, 0.75f);

        private PlayerMotor motor;
        private PlayerController controller;
        private BeltDash dash;
        private Transform visual;
        private ParticleSystem movementParticles;
        private Material particleMaterial;
        private Vector3 restScale;
        private bool wasDashing;
        private float squashRemaining;
        private float sprintPulseRemaining;
        private Mesh sampledGroundMesh;
        private Color[] groundColors;
        private int[] groundTriangles;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            controller = GetComponent<PlayerController>();
            dash = GetComponent<BeltDash>();
            visual = motor != null ? motor.Visual : null;
            if (visual != null) restScale = visual.localScale;
            CreatePresentationObjects();
        }

        private void OnEnable()
        {
            if (motor == null) return;
            motor.Jumped += OnJumped;
            motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (motor == null) return;
            motor.Jumped -= OnJumped;
            motor.Landed -= OnLanded;
        }

        private void OnDestroy()
        {
            if (particleMaterial != null) Destroy(particleMaterial);
        }

        private void Update()
        {
            if (motor == null || dash == null || visual == null) return;
            bool sprinting = controller != null && controller.IsSprinting;
            bool dashing = dash.IsActive;
            if (sprinting && motor.IsGrounded)
            {
                sprintPulseRemaining -= Time.deltaTime;
                if (sprintPulseRemaining <= 0f)
                {
                    Emit(ResolveGroundColor(), 3, 0.45f, 0.12f);
                    sprintPulseRemaining = 0.18f;
                }
            }
            else sprintPulseRemaining = 0f;
            if (dashing && !wasDashing)
            {
                Emit(dashColor, 16, 1.2f, 0.28f);
                Squash(0.10f);
            }
            wasDashing = dashing;
            Vector3 targetScale = restScale;
            if (squashRemaining > 0f)
            {
                squashRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(squashRemaining / 0.12f);
                targetScale = Vector3.Lerp(restScale, Vector3.Scale(restScale, new Vector3(1.12f, 0.78f, 1.12f)), t);
            }
            else if (dashing) targetScale = Vector3.Scale(restScale, new Vector3(0.9f, 0.9f, 1.18f));
            else if (!motor.IsGrounded) targetScale = Vector3.Scale(restScale, new Vector3(0.94f, 1.08f, 0.94f));
            else if (sprinting) targetScale = Vector3.Scale(restScale, new Vector3(0.97f, 1.02f, 1.08f));
            if(animateVisualScale) visual.localScale = Vector3.Lerp(visual.localScale, targetScale, Time.deltaTime * 15f);
        }

        private void OnJumped()
        {
            Emit(sprintColor, 8, 0.75f, 0.22f);
            Squash(0.08f);
        }

        private void OnLanded()
        {
            var contextual=GetComponent<WorldSurfaceFeedback>();
            if(contextual!=null)contextual.Emit(transform.position+Vector3.up*.1f,ResolveGroundColor(),.9f,8);
            else Emit(landingColor, 12, 0.9f, 0.25f);
            Squash(0.12f);
        }

        private void Squash(float duration) => squashRemaining = Mathf.Max(squashRemaining, duration);

        private void CreatePresentationObjects()
        {
            GameObject particleObject = new GameObject("Movement Feedback Particles");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            movementParticles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleMaterial = RuntimeParticleMaterial.Create("Movement Feedback Material (Runtime)", Color.white);
            particleRenderer.sharedMaterial = particleMaterial;
            var main = movementParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.3f;
            main.startSize = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = movementParticles.emission;
            emission.enabled = false;
            var shape = movementParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;
        }

        private void Emit(Color color, int count, float speed, float size)
        {
            if (movementParticles == null) return;
            var main = movementParticles.main;
            main.startSpeed = speed;
            var parameters = new ParticleSystem.EmitParams { startColor = color, startSize = size };
            movementParticles.Emit(parameters, count);
        }

        /// <summary>Obtiene el color visible del material inmediatamente bajo los pies.</summary>
        private Color ResolveGroundColor()
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return sprintColor;

            Renderer groundRenderer = hit.collider.GetComponent<Renderer>();
            Material material = groundRenderer != null ? groundRenderer.sharedMaterial : null;
            if (material == null) return sprintColor;

            Color sampled;
            if (material.shader == null || material.shader.name != "Mismo/Textured Terrain" ||
                !TrySampleTerrain(hit, material, out sampled))
            {
                if (material.HasProperty("_BaseColor")) sampled = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color")) sampled = material.GetColor("_Color");
                else return sprintColor;
            }

            sampled.a = sprintColor.a;
            return sampled;
        }

        private bool TrySampleTerrain(RaycastHit hit, Material material, out Color color)
        {
            color = default;
            var collider = hit.collider as MeshCollider;
            var mesh = collider != null ? collider.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || hit.triangleIndex < 0) return false;
            // Keep only the current chunk cached; do not allocate mesh arrays on every footstep.
            if (sampledGroundMesh != mesh)
            {
                sampledGroundMesh = mesh;
                groundColors = mesh.colors;
                groundTriangles = mesh.triangles;
            }
            int triangle = hit.triangleIndex * 3;
            if (groundColors.Length != mesh.vertexCount || triangle + 2 >= groundTriangles.Length) return false;
            Vector3 weights = hit.barycentricCoordinate;
            color = groundColors[groundTriangles[triangle]] * weights.x +
                    groundColors[groundTriangles[triangle + 1]] * weights.y +
                    groundColors[groundTriangles[triangle + 2]] * weights.z;
            // Match TerrainSurface's palette mapping. Alpha zero preserves literal biome colors.
            Color mapped = color;
            if (color.r >= color.b * 1.3f && color.r >= color.g)
                mapped = material.GetColor("_DirtColor");
            if (color.g >= color.r * 1.08f && color.g >= color.b * 1.3f)
                mapped = hit.normal.y < .6f ? material.GetColor("_DirtColor") * .8f : material.GetColor("_GrassColor");
            color = Color.Lerp(color, mapped, Mathf.Clamp01(color.a));
            return true;
        }

    }
}
