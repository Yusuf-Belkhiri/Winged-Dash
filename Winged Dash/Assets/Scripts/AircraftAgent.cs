using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

namespace Aircraft
{
    /// <summary>
    /// 3 Actions: pitchChange, yawChange, boost
    /// </summary>
    public class AircraftAgent : Agent
    {
    #region FIELDS
    
        [Header("Movement")] 
        [SerializeField] private float _thrust = 1000000f;      // to push the airplane forward (z)
        [SerializeField] private float _pitchSpeed = 100f;      // to rotate vertically (around x)
        [SerializeField] private float _yawSpeed = 100f;       // to rotate around y axis 
        [SerializeField] private float _rollSpeed= 100f;       // to rotate around z axis 
        [SerializeField] private float _boostMultiplier = 2f;   // extra force when the airplane is boosting

        [Header("Explosion")]
        [SerializeField] private GameObject _explosionEffect;
        [SerializeField] private GameObject _meshObject;     // the child mesh object that will disappear on explosion

        [Header("Training")] 
        [Tooltip("Number of steps to time out after in training")] [SerializeField] private int _stepTimeout = 300;     // if the agent does 300 steps (updates), and it hasn't  made it to the next checkpoint: reset it (for a better training)
        private float _nextStepTimeOut;
        private bool _frozen;       // whether the aircraft is frozen (intentionally not flying): when paused, or crashed or the at beginning of the race
        

        public int NextCheckpointIndex { get; set; }

        // Controls
        private float _pitchChange;         // 0, 1 or -1
        private float _smoothPitchChange;
        [SerializeField] private float _maxPitchAngle = 45f;
        private float _yawChange;       // 0, 1 or -1
        private float _smoothYawChange;
        private float _rollChange;
        private float _smoothRollChange;
        [SerializeField] private float _maxRollAngle = 120f;
        private bool _boost;
        
        // Components
        private AircraftArea _area;
        private Rigidbody _rb;
        private TrailRenderer _trail;
  
    #endregion


        public override void Initialize()
        {
            _area = GetComponentInParent<AircraftArea>();
            _rb = GetComponent<Rigidbody>();
            _trail = GetComponent<TrailRenderer>();

            // Override max step: 5000 if training, infinite if racing
            MaxStep = _area._trainingMode ? 5000 : 0;
        }


        public override void OnEpisodeBegin()
        {
            // Reset the velocity, orientation, trial and position
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _trail.emitting = false;
            _area.ResetAgentPosition(this, _area._trainingMode); 

            // Update the next step timeout
            if (_area._trainingMode)
            {
                _nextStepTimeOut = StepCount + _stepTimeout;
            }
        }

        // Works only if not frozen
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (_frozen) return;

            _pitchChange = actions.DiscreteActions[0];      // 0: don't move,   1: move up,     2: move down (-1)
            if (_pitchChange == 2) _pitchChange = -1f;

            _yawChange = actions.DiscreteActions[1];        // 0: don't move,   1: move right,  2: move left (-1)
            if (_yawChange == 2) _yawChange = -2;
            
            _boost = actions.DiscreteActions[2] == 1;
            if (_boost && !_trail.emitting)
                _trail.Clear();
            _trail.emitting = _boost;
            
            ProcessMovement();


            if (_area._trainingMode)
            {
                // Small negative reward every step to accelerate training
                AddReward(-1f / MaxStep);

                if (StepCount > _nextStepTimeOut)
                {
                    AddReward(-0.5f); 
                    EndEpisode();
                }

                // Curriculum learning, check if the agent is within a certain radius of the next checkpoint, the checkpoint_radius will start at 50m, 
                // once it's good at getting within 50m of the next checkpoint, then we gonna lower to 30m, and then 20m..and then it will need to fly through the checkpoint (make it more challenging each time)  
                Vector3 localCheckpointDir = VectorToNextCheckpoint();
                if (localCheckpointDir.magnitude < Academy.Instance.EnvironmentParameters.GetWithDefault("checkpoint_radius", 0f))
                {
                    GotCheckpoint();        
                }

            }
        }
        
        
        // Called to get the distance between the agent & the next checkpoint
        private Vector3 VectorToNextCheckpoint()
        {
            var nextCheckpointDir = _area.Checkpoints[NextCheckpointIndex].position - transform.position;
            var localCheckpointDir = transform.InverseTransformDirection(nextCheckpointDir);        // get the local direction
            return localCheckpointDir;
        }

        // Called to target a new checkpoint 
        private void GotCheckpoint()
        {
            NextCheckpointIndex = (NextCheckpointIndex + 1) % _area.Checkpoints.Count;

            if (_area._trainingMode)
            {
                AddReward(.5f);
                _nextStepTimeOut = StepCount + _stepTimeout;
            }
        }

        // Prevent the agent from moving and taking actions
        private void FreezeAgent()
        {
            Debug.Assert(!_area._trainingMode, "Freeze/Thaw is not supported in training");
            _frozen = true;
            _rb.Sleep();
            _trail.emitting = false;
        }

        // != FreezeAgent, Resume the agent movement and actions 
        private void ThawAgent()
        {
            Debug.Assert(!_area._trainingMode, "Freeze/Thraw is not supported in training");
            _frozen = false;
            _rb.WakeUp();
        }
        

        // Calculate & apply movement
        private void ProcessMovement()
        {
            // Move forward
            var boostModifier = _boost ? _boostMultiplier : 1f;
            _rb.AddForce(transform.forward * _thrust * boostModifier, ForceMode.Force); 
            
            // Rotations 
            Vector3 currentRot = transform.rotation.eulerAngles;        // to use clamp below (the use of:transform.rotation = instead of transform.Rotate()) 
            
            if (_yawChange == 0f)
            {
                float rollAngle = currentRot.z > 180f ? currentRot.z - 360 : currentRot.z;       // between (-180, 180)
                _rollChange = -rollAngle / _maxRollAngle;
            }
            else
                _rollChange = -_yawChange;      // the opposite direction

                // Smooth rotations 
            _smoothPitchChange = Mathf.MoveTowards(_smoothPitchChange, _pitchChange, 2f * Time.fixedDeltaTime);
            _smoothYawChange = Mathf.MoveTowards(_smoothYawChange, _yawChange, 2f * Time.deltaTime);
            _smoothRollChange = Mathf.MoveTowards(_smoothRollChange, _rollChange, 2f * Time.deltaTime);

            float pitch = currentRot.x + _pitchSpeed * _smoothPitchChange * Time.fixedDeltaTime;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -_maxPitchAngle, _maxPitchAngle);

            float yaw = currentRot.y + _yawSpeed * _smoothYawChange * Time.fixedDeltaTime;

            float roll = currentRot.x + _rollSpeed * _smoothRollChange * Time.fixedDeltaTime;
            if (roll > 180f) roll -= 360f;
            roll = Mathf.Clamp(roll, -_maxRollAngle, _maxRollAngle);
            
            
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        }


        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Checkpoint") && other.gameObject == _area.Checkpoints[NextCheckpointIndex])
            {
                GotCheckpoint();
            }
        }
    }
}
