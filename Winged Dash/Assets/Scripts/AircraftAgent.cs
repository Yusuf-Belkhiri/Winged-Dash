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
        }
        
        
        public override void OnActionReceived(ActionBuffers actions)
        {
            _pitchChange = actions.DiscreteActions[0];      // 0: don't move,   1: move up,     2: move down (-1)
            if (_pitchChange == 2) _pitchChange = -1f;

            _yawChange = actions.DiscreteActions[1];        // 0: don't move,   1: move right,  2: move left (-1)
            if (_yawChange == 2) _yawChange = -2;
            
            _boost = actions.DiscreteActions[2] == 1;
            if (_boost && !_trail.emitting)
                _trail.Clear();
            _trail.emitting = _boost;
            
            ProcessMovement();
        }

        // Calculate & apply movement
        private void ProcessMovement()
        {
            // Move forward
            var boostModifier = _boost ? _boostMultiplier : 1f;
            _rb.AddForce(Vector3.forward * _thrust * boostModifier, ForceMode.Force); 
            
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
    }
    
}
