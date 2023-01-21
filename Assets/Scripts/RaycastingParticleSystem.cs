using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

namespace Game
{
    [RequireComponent(typeof(ParticleSystem))]
    public class RaycastingParticleSystem : MonoBehaviour
    {
        [SerializeField] private float _bounciness = 0.5f;
        [SerializeField] private ParticleSystem _contactParticles;
        
        private ParticleSystem _particleSystem;
        private NativeList<Hit> _hits;
        private JobHandle _jobHandle;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnParticleUpdateJobScheduled()
        {
            var particlesCount = _particleSystem.particleCount;
            var commands = new NativeArray<RaycastCommand>(particlesCount, Allocator.TempJob);

            var buildRaycastCommandsJob = new BuildRaycastCommandsJob { DeltaTime = Time.deltaTime, Commands = commands };
            _jobHandle = buildRaycastCommandsJob.ScheduleBatch(_particleSystem, 512);
            
            var raycastHits = new NativeArray<RaycastHit>(particlesCount, Allocator.TempJob);
            _jobHandle = RaycastCommand.ScheduleBatch(commands, raycastHits, 512, _jobHandle);

            _hits = new NativeList<Hit>(particlesCount, Allocator.TempJob);
            var applyHitsJob = new ApplyHitsJob { Bounciness = _bounciness, Hits = raycastHits, Result = _hits.AsParallelWriter()};
            _jobHandle = applyHitsJob.ScheduleBatch(_particleSystem, 512, _jobHandle);

            commands.Dispose(_jobHandle);
            raycastHits.Dispose(_jobHandle);
        }

        private void LateUpdate()
        {
            if (_contactParticles != null)
            {
                _jobHandle.Complete();

                var size = _contactParticles.main.startSize.Evaluate(0);
                var emitParams = new ParticleSystem.EmitParams();
                foreach (var hit in _hits)
                {
                    emitParams.position = hit.Position;
                    emitParams.rotation3D = hit.Rotation;
                    emitParams.startSize = hit.Size * size;
                    _contactParticles.Emit(emitParams, 1);
                }

                _hits.Dispose();
            }
            else
            {
                _hits.Dispose(_jobHandle);
            }
        }

        [BurstCompile]
        private struct ApplyHitsJob : IJobParticleSystemParallelForBatch
        {
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public float Bounciness;
            public NativeList<Hit>.ParallelWriter Result;

            public void Execute(ParticleSystemJobData jobData, int startIndex, int count)
            {
                var velocities = jobData.velocities;
                var endIndex = startIndex + count;
                for (int i = 0; i < endIndex; i += 1)
                {
                    var hit = Hits[i];
                    if (hit.distance > 0)
                    {
                        var velocity = velocities[i];
                        velocity = Vector3.Reflect(velocity, hit.normal) * Bounciness;
                        velocities[i] = velocity;

                        var resultHit = new Hit
                        {
                            Position = hit.point + hit.normal * 0.01f,
                            Rotation = Quaternion.LookRotation(-hit.normal, hit.normal.y > 0.9f ? Vector3.forward : Vector3.up).eulerAngles,
                            Size = Mathf.Clamp(velocity.sqrMagnitude / 25f, 0.5f, 1f),
                        };
                        Result.AddNoResize(resultHit);
                    }
                }
            }
        }

        [BurstCompile]
        private struct BuildRaycastCommandsJob : IJobParticleSystemParallelForBatch
        {
            public NativeArray<RaycastCommand> Commands;
            [ReadOnly] public float DeltaTime;

            public void Execute(ParticleSystemJobData jobData, int startIndex, int count)
            {
                int endIndex = startIndex + count;
                for (int i = startIndex; i < endIndex; i += 1)
                {
                    var position = jobData.positions[i];
                    var velocity = jobData.velocities[i];
                    var speed = velocity.magnitude;
                    var direction = Vector3.Normalize(velocity);
                    var distance = speed * DeltaTime;
                    Commands[i] = new RaycastCommand(Physics.defaultPhysicsScene, position, direction, QueryParameters.Default, distance);
                }
            }
        }
        
        private struct Hit
        {
            public Vector3 Position;
            public Vector3 Rotation;
            public float Size;
        }
    }
}